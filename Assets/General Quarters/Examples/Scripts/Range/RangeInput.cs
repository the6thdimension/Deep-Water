using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>One frame of TAO input, whatever device it came from (the "Tao" action map).</summary>
    public struct TaoInputFrame
    {
        public bool PrevTrack, NextTrack, EngageEssm, EngageMk45, EngageCiws, CeaseFire, CeaseFireAll, CycleRoe, ZoomIn, ZoomOut;
    }

    /// <summary>
    /// Reads the range's two action maps, the one reader for each (as ConnInput is for the conn): "Range" (seat,
    /// raid, time compression, camera target: live in both seats) and "Tao" (the TAO's track and weapon keys: read
    /// only in the TAO seat). Both live in the project-wide actions next to "Conn", with the same three control
    /// schemes. Actions are read as performed, so a pad's Start can switch seats on a tap and start the raid on a hold.
    /// </summary>
    public sealed class RangeInput : MonoBehaviour
    {
        public const string RangeMap = "Range", TaoMap = "Tao";

        [Tooltip("Leave empty to use the project-wide actions (Project Settings → Input System Package).")]
        [SerializeField] InputActionAsset actions;

        InputActionMap _range, _tao;
        InputAction _seat, _raid, _time, _camera;
        InputAction _prev, _next, _essm, _mk45, _ciws, _cease, _ceaseAll, _roe, _zoomIn, _zoomOut;

        public bool Ready => _range != null && _tao != null;

        void Awake()
        {
            InputActionAsset asset = actions != null ? actions : InputSystem.actions;
            _range = asset != null ? asset.FindActionMap(RangeMap) : null;
            _tao = asset != null ? asset.FindActionMap(TaoMap) : null;
            if (_range == null || _tao == null)
            {
                Debug.LogError($"[RangeInput] The '{RangeMap}' and '{TaoMap}' action maps are missing from " +
                               $"{(asset != null ? asset.name : "the project-wide actions (none set)")}; they ship in Assets/RHMILCOM.inputactions.", this);
                enabled = false;
                return;
            }
            _seat = _range.FindAction("SwitchSeat", true);
            _raid = _range.FindAction("StartRaid", true);
            _time = _range.FindAction("TimeCompression", true);
            _camera = _range.FindAction("CameraTarget", true);
            _prev = _tao.FindAction("PrevTrack", true);
            _next = _tao.FindAction("NextTrack", true);
            _essm = _tao.FindAction("EngageEssm", true);
            _mk45 = _tao.FindAction("EngageMk45", true);
            _ciws = _tao.FindAction("EngageCiws", true);
            _cease = _tao.FindAction("CeaseFire", true);
            _ceaseAll = _tao.FindAction("CeaseFireAll", true);
            _roe = _tao.FindAction("CycleRoe", true);
            _zoomIn = _tao.FindAction("ScopeZoomIn", true);
            _zoomOut = _tao.FindAction("ScopeZoomOut", true);
        }

        void OnEnable() { if (Ready) { _range.Enable(); _tao.Enable(); } }
        void OnDisable() { if (Ready) { _range.Disable(); _tao.Disable(); } }

        public bool SwitchSeat => Ready && _seat.WasPerformedThisFrame();
        public bool StartRaid => Ready && _raid.WasPerformedThisFrame();
        public bool TimeCompression => Ready && _time.WasPerformedThisFrame();
        public bool CameraTarget => Ready && _camera.WasPerformedThisFrame();

        /// <summary>This frame's TAO input.</summary>
        public TaoInputFrame Tao() => !Ready ? default : new TaoInputFrame
        {
            PrevTrack = _prev.WasPerformedThisFrame(),
            NextTrack = _next.WasPerformedThisFrame(),
            EngageEssm = _essm.WasPerformedThisFrame(),
            EngageMk45 = _mk45.WasPerformedThisFrame(),
            EngageCiws = _ciws.WasPerformedThisFrame(),
            CeaseFire = _cease.WasPerformedThisFrame(),
            CeaseFireAll = _ceaseAll.WasPerformedThisFrame(),
            CycleRoe = _roe.WasPerformedThisFrame(),
            ZoomIn = _zoomIn.WasPerformedThisFrame(),
            ZoomOut = _zoomOut.WasPerformedThisFrame(),
        };
    }
}
