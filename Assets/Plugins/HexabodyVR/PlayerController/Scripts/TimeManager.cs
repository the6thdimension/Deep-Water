using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HexabodyVR.PlayerController
{
    public class TimeManager : MonoBehaviour
    {
        [Header("Smoothing Settings")]
        [Tooltip("If true, the fixed time step will be averaged by FPS")]
        public bool SmoothFixedTimeStep = true;
        [Tooltip("Amount of frames to average time step over")]
        public int FrameSamples = 30;
        [Tooltip("Minimum time step allowed")]
        public int MinTimeStep = 72;
        [Tooltip("Maximum time step allowed")]
        public int MaxTimeStep = 144;

        public float RefreshRate { get; private set; }

        [Header("Debug")] public bool Debug;
        public float DebugRefreshRate;

        private CircularBuffer<float> _buffer;
        private float averageFrameDelta;

        public static TimeManager Instance { get; private set; }

        private void Awake()
        {

            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(this);
            }
        }

        private void Start()
        {
            SetTimeStep();

            _buffer = new CircularBuffer<float>(FrameSamples);
            ResetBuffer();

            DebugRefreshRate = RefreshRate;
        }

        private void SetTimeStep()
        {
            RefreshRate = UnityEngine.XR.XRDevice.refreshRate;
            if (RefreshRate < 60f)
            {
                RefreshRate = 90f;
            }

            Time.fixedDeltaTime = 1f / RefreshRate;
        }

        private void ResetBuffer()
        {
            for (int i = 0; i < FrameSamples; i++)
            {
                _buffer.Enqueue(Time.fixedDeltaTime);
            }
        }

        private void Update()
        {
            if (Debug)
            {
                DebugRefreshRate = RefreshRate;
            }

            _buffer.Enqueue(Time.deltaTime);

            var sum = 0f;
            for (var i = 0; i < FrameSamples; i++)
            {
                sum += _buffer[i];
            }

            if (Mathf.Approximately(sum, 0f))
                return;

            var average = sum / FrameSamples;

            //if (HVRGlobalInputs.Instance.LeftPrimaryButtonState.JustActivated || HVRGlobalInputs.Instance.LeftTrackPadDown.JustActivated)
            //{
            //    ToggleTime();
            //}

            var min = 1f / MaxTimeStep * Time.timeScale;
            var max = 1f / MinTimeStep * Time.timeScale;

            averageFrameDelta = Mathf.Clamp(average, min, max);
            if (SmoothFixedTimeStep)
            {
                Time.fixedDeltaTime = averageFrameDelta;
            }
            else
            {
                SetTimeStep();
            }
        }

        public void OverrideRefreshRate(float refresh)
        {
            RefreshRate = refresh;
            if (!SmoothFixedTimeStep)
            {
                Time.fixedDeltaTime = 1 / refresh;
            }
        }

        public void ResetRefreshRate()
        {
            RefreshRate = UnityEngine.XR.XRDevice.refreshRate;
            if (!SmoothFixedTimeStep)
            {
                Time.fixedDeltaTime = 1 / RefreshRate;
            }
        }

    }
}
