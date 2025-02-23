using UnityEngine;
using UnityEngine.Events;

namespace HexabodyVR.PlayerController
{
    /// <summary>
    /// Used to broadcast when a gameobject is enabled. Don't enable / disable this component otherwise false positives will arise.
    /// </summary>
    public class ObjectEnabledWatcher : MonoBehaviour
    {
        public UnityEvent Enabled = new UnityEvent();

        private void OnEnable()
        {
            Enabled.Invoke();
        }
    }
}