using UnityEngine;
using UnityEngine.InputSystem;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Modules.Input
{
    /// <summary>
    /// The player's seat in a scene: owns the <see cref="FlightDeviceReader"/> and the <see cref="PlayerSeat"/>.
    /// Update gathers presses and mouse travel; FixedUpdate hands the seat this tick's frame. It runs early
    /// (execution order −60) so the frame is in before the aircraft adapter steps the core.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class PlayerSeatInput : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] PlayerSeatSettingsSO settings;
        [Tooltip("Lever position at the start, 0 … 1 (below the afterburner detent is idle … military).")]
        [Range(0f, 1f)] [SerializeField] float startLever = 0.8f;

        FlightDeviceReader _reader;

        public PlayerSeat Seat { get; private set; }

        void Awake()
        {
            if (actions == null || settings == null)
            {
                Debug.LogError($"[{nameof(PlayerSeatInput)}] {name}: assign the input actions and the seat settings.", this);
                enabled = false;
                return;
            }
            _reader = new FlightDeviceReader(actions, settings.BakeReader());
            Seat = new PlayerSeat(settings.BakeSeat(), Time.fixedDeltaTime, startLever: startLever);
        }

        void Update() => _reader?.Poll();

        void FixedUpdate()
        {
            if (_reader == null) return;
            Seat.Submit(_reader.Read());
            CyclePresses += _reader.TakeCyclePresses();
        }

        /// <summary>Seat-cycle presses waiting for the runner (W42); it takes them before the tick.</summary>
        public int CyclePresses { get; private set; }

        public int TakeCyclePresses()
        {
            int n = CyclePresses;
            CyclePresses = 0;
            return n;
        }

        void OnDestroy() => _reader?.Dispose();
    }
}
