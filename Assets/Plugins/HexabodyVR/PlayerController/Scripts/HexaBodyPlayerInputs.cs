using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexabodyVR.PlayerController
{
    public class HexaBodyPlayerInputs : MonoBehaviour
    {


        [Header("Debugging")] public bool KeyboardDebug;

#if ENABLE_LEGACY_INPUT_MANAGER

  public KeyCode CrouchKey = KeyCode.X;
        public KeyCode StandKey = KeyCode.Z;
        public KeyCode JumpKey = KeyCode.Space;
        public KeyCode RunKey = KeyCode.LeftShift;
        public KeyCode RecalibrateKey = KeyCode.R;

#elif ENABLE_INPUT_SYSTEM

        public Key CrouchingKey = Key.X;
        public Key StandingKey = Key.Z;
        public Key JumpingKey = Key.Space;
        public Key RunningKey = Key.LeftShift;
        public Key HeightCalibrateKey = Key.R;

#endif

        public Vector2 MovementAxis;
        public Vector2 TurnAxis;

        public bool SprintRequiresDoubleClick;
        public bool SprintingPressed;
        public bool RecalibratePressed;

        public bool JumpPressed;
        public bool CrouchPressed;
        public bool StandPressed;

        internal PlayerInputState JumpState;
        internal PlayerInputState CrouchState;
        internal PlayerInputState StandState;

        void Update()
        {
            ResetState(ref CrouchState);
            ResetState(ref StandState);
            ResetState(ref JumpState);

            if (KeyboardDebug)
            {

#if ENABLE_LEGACY_INPUT_MANAGER

     CrouchPressed = CrouchPressed || Input.GetKey(CrouchKey);
            StandPressed = StandPressed || Input.GetKey(StandKey);
            JumpPressed = JumpPressed || Input.GetKey(JumpKey);
            SprintingPressed = SprintingPressed || Input.GetKey(RunKey);
            RecalibratePressed = RecalibratePressed || Input.GetKey(RecalibrateKey);

#elif ENABLE_INPUT_SYSTEM

                CrouchPressed = CrouchPressed || Keyboard.current[CrouchingKey].isPressed;
                StandPressed = StandPressed || Keyboard.current[StandingKey].isPressed;
                JumpPressed = JumpPressed || Keyboard.current[JumpingKey].isPressed;
                SprintingPressed = SprintingPressed || Keyboard.current[RunningKey].isPressed;
                RecalibratePressed = RecalibratePressed || Keyboard.current[HeightCalibrateKey].isPressed;

#endif
            }

            SetState(ref CrouchState, CrouchPressed);
            SetState(ref StandState, StandPressed);
            SetState(ref JumpState, JumpPressed);
        }

        protected void ResetState(ref PlayerInputState buttonState)
        {
            buttonState.JustDeactivated = false;
            buttonState.JustActivated = false;
            buttonState.Value = 0f;
        }

        protected void SetState(ref PlayerInputState buttonState, bool pressed)
        {
            if (pressed)
            {
                if (!buttonState.Active)
                {
                    buttonState.JustActivated = true;
                    buttonState.Active = true;
                }
            }
            else
            {
                if (buttonState.Active)
                {
                    buttonState.Active = false;
                    buttonState.JustDeactivated = true;
                }
            }
        }
    }
}