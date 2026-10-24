using UnityEngine;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Modules.Presentation
{
    /// <summary>
    /// Plays the fight's sounds from a fixed pool of 3D voices. Each sound starts where it happened and arrives late by
    /// its distance from the listener over the speed of sound, so a distant intercept flashes first and booms after.
    /// When every voice is busy the oldest is taken.
    /// </summary>
    public sealed class CombatAudio
    {
        readonly AudioSource[] _voices;
        readonly float[] _startedAt;
        readonly float _speedOfSound;

        public CombatAudio(Transform root, int voices, float speedOfSoundMs)
        {
            _speedOfSound = Mathf.Max(1f, speedOfSoundMs);
            _voices = new AudioSource[Mathf.Max(1, voices)];
            _startedAt = new float[_voices.Length];
            for (int i = 0; i < _voices.Length; i++)
            {
                var go = new GameObject($"Voice {i}");
                go.transform.SetParent(root, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.dopplerLevel = 0f;              // the camera jumps between targets; Doppler would warble on every cut
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                _voices[i] = s;
                _startedAt[i] = float.NegativeInfinity;
            }
        }

        /// <summary>Play <paramref name="slot"/> at <paramref name="position"/>, delayed by the sound's travel time to the listener.</summary>
        public void Play(in SoundSlot slot, Vector3 position, Vector3 listener, float now)
        {
            AudioClip clip = slot.clip != null ? slot.clip : SynthClips.Get(slot.synth);
            if (clip == null || slot.volume <= 0f) return;
            float distance = Vector3.Distance(position, listener);
            if (slot.maxDistanceM > 0f && distance > slot.maxDistanceM) return;

            int pick = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].isPlaying && now >= _startedAt[i]) { pick = i; break; }
                if (_startedAt[i] < _startedAt[pick]) pick = i;
            }
            AudioSource s = _voices[pick];
            s.Stop();
            s.transform.position = position;
            s.spatialBlend = 1f;
            s.clip = clip;
            s.volume = slot.volume;
            s.pitch = slot.pitch > 0f ? slot.pitch : 1f;
            s.minDistance = slot.minDistanceM > 0f ? slot.minDistanceM : 20f;
            s.maxDistance = slot.maxDistanceM > 0f ? slot.maxDistanceM : 5000f;
            float delay = distance / _speedOfSound;
            s.PlayDelayed(delay);
            _startedAt[pick] = now + delay;
        }

        /// <summary>Play <paramref name="slot"/> with no position (the alarm on the ship's speakers).</summary>
        public void PlayFlat(in SoundSlot slot)
        {
            AudioClip clip = slot.clip != null ? slot.clip : SynthClips.Get(slot.synth);
            if (clip == null || slot.volume <= 0f) return;
            AudioSource s = _voices[0];
            s.Stop();
            s.clip = clip;
            s.volume = slot.volume;
            s.pitch = slot.pitch > 0f ? slot.pitch : 1f;
            s.spatialBlend = 0f;
            s.Play();
            _startedAt[0] = Time.time + clip.length;
        }
    }
}
