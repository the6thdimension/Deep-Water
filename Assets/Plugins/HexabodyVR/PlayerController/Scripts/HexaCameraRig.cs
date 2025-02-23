using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace HexabodyVR.PlayerController
{
    public class HexaCameraRig : MonoBehaviour
    {
        [Header("Transforms")]
        public Transform Camera;
        public Transform FloorOffsetTransform;
        public float CameraYOffset;
        public TrackingOriginModeFlags TrackingSpace;

        private TrackingOriginModeFlags _previousTrackingSpace;



        public float FloorOffset
        {
            get => _floorOffset;
            set
            {
                _floorOffset = value;
                UpdateFloorOffset();
            }
        }

        [SerializeField] private float _floorOffset;

        void Start()
        {
            _previousTrackingSpace = TrackingSpace;

            Setup();
        }

        public void UpdateFloorOffset()
        {
            FloorOffsetTransform.localPosition = new Vector3(0f, _floorOffset + CameraYOffset, 0f);
        }

        void Update()
        {
            if (TrackingSpace != _previousTrackingSpace)
            {
                Setup();
                _previousTrackingSpace = TrackingSpace;
            }

        }

        private void Setup()
        {
            StartCoroutine(UpdateTrackingOrigin(TrackingSpace));
            UpdateFloorOffset();
        }


        IEnumerator UpdateTrackingOrigin(TrackingOriginModeFlags originFlags)
        {
            yield return null;

#if USING_XR_MANAGEMENT
             var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances<XRInputSubsystem>(subsystems);
            Debug.Log("Found " + subsystems.Count + " input subsystems.");

            for (int i = 0; i < subsystems.Count; i++)
            {
                if (subsystems[i].TrySetTrackingOriginMode(originFlags))
                    Debug.Log("Successfully set TrackingOriginMode to Floor");
                else
                    Debug.Log("Failed to set TrackingOriginMode to Floor");
            }

#elif !UNITY_2020_1_OR_NEWER


            if (originFlags == TrackingOriginModeFlags.Floor)
            {
#pragma warning disable 0618
                if (XRDevice.SetTrackingSpaceType(TrackingSpaceType.RoomScale))
                {
                    Debug.Log("Tracking change to RoomScale.");
                }
                else
                {
                    Debug.Log("Failed Tracking change to RoomScale.");
                }
            }
            else
            {

                XRDevice.SetTrackingSpaceType(TrackingSpaceType.Stationary);
#pragma warning restore 0618
                Debug.Log("Tracking change to stationary.");
            }

#endif

        }
    }
}