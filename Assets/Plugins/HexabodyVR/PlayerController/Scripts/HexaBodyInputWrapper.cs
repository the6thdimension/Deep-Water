using UnityEngine;

namespace HexabodyVR.PlayerController
{
    public class HexaBodyInputWrapper : MonoBehaviour
    {
        public HexaXRInputs LeftController;
        public HexaXRInputs RightController;

        public float CrouchThreshold = .7f;
        public float StandThreshold = .7f;

        public HexaBodyPlayerInputs PlayerInputs;

        public bool EnableDebugCalibrationButton;

        protected void Update()
        {
            if (EnableDebugCalibrationButton)
            {
                PlayerInputs.RecalibratePressed = LeftController.PrimaryButtonState.JustActivated;

                if (LeftController.IsWindowsMR && !LeftController.IsXRInputs)
                {
                    PlayerInputs.RecalibratePressed = LeftController.MenuButtonState.JustActivated;
                }
            }

            if (LeftController.IsVive)
            {
                PlayerInputs.RecalibratePressed = LeftController.MenuButtonState.JustActivated;
            }

            if (LeftController.IsKnuckles && LeftController.IsXRInputs)
            {
                PlayerInputs.RecalibratePressed = LeftController.GripButtonState.JustActivated;
            }

            PlayerInputs.SprintRequiresDoubleClick = LeftController.IsVive;
            PlayerInputs.SprintingPressed = GetSprinting();

            if (LeftController.IsVive)
            {
                PlayerInputs.MovementAxis = LeftController.TrackpadButtonState.Active ?  LeftController.TrackpadAxis : Vector2.zero;
            }
            else
            {
                PlayerInputs.MovementAxis = LeftController.JoystickAxis;
            }

            if (RightController.IsVive)
            {
                PlayerInputs.TurnAxis = RightController.TrackpadButtonState.Active ? RightController.TrackpadAxis : Vector2.zero;
            }
            else
            {
                PlayerInputs.TurnAxis = RightController.JoystickAxis;
            }



            PlayerInputs.CrouchPressed = GetCrouch();
            PlayerInputs.StandPressed = GetStand();
            PlayerInputs.JumpPressed = GetJump();
        }

        protected virtual bool GetSprinting()
        {
            if (LeftController.IsVive)
            {
                return LeftController.TrackpadButtonState.JustActivated;
            }

            if (RightController.WindowsWithTrackPad)
            {
                return RightController.TrackPadRight.JustActivated;
            }

            return LeftController.JoystickButtonState.JustActivated;
        }

        protected bool GetCrouch()
        {
            if (RightController.IsVive)
            {
                return RightController.TrackpadButtonState.Active && RightController.TrackpadAxis.y < -CrouchThreshold;
            }
            return RightController.JoystickAxis.y < -CrouchThreshold;
        }

        protected bool GetStand()
        {
            if (RightController.IsVive)
            {
                return RightController.TrackpadButtonState.Active && RightController.TrackpadAxis.y > StandThreshold;
            }
            return RightController.JoystickAxis.y > StandThreshold;
        }

        protected bool GetJump()
        {
            if (RightController.IsVive)
            {
                return RightController.MenuButtonState.Active;
            }
            else if (RightController.WindowsWithTrackPad)
            {
                return RightController.TrackPadDown.Active;
            }
            else if (RightController.IsKnuckles && RightController.IsXRInputs)
            {
                return RightController.GripButtonState.Active;
            }
            else
            {
                return RightController.PrimaryButtonState.Active;
            }
        }
    }
}