using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexabodyVR.PlayerController
{

    public abstract class HexaBodyInputsBase : MonoBehaviour
    {
        [Header("Settings")]
        public bool CanMove = true;
        public bool CanTurn = true;
        public bool CanCrouch = true;
        public bool CanJump = true;


        [Header("Gathered Inputs")]
        public Vector2 MovementAxis;
        public Vector2 TurnAxis;

        public bool SprintRequiresDoubleClick;
        public bool SprintingPressed;
        public bool RecalibratePressed;
        public bool JumpPressed;

        public float CrouchRate;

        /// <summary>
        /// Cached movement magnitude
        /// </summary>
        public float MovementMagnitude { get; private set; }


        internal PlayerInputState JumpState;
        internal PlayerInputState CrouchState;

        protected virtual void Update()
        {
            GatherInputs();
            SetStates();
        }

        protected virtual void SetStates()
        {
            ResetState(ref CrouchState);
            ResetState(ref JumpState);

            SetState(ref JumpState, JumpPressed);
            SetState(ref CrouchState, Mathf.Abs(CrouchRate) > .01f);
        }

        protected virtual void GatherInputs()
        {
            ResetFields();

            if (CanJump)
            {
                JumpPressed = UpdateJump();
            }

            if (CanCrouch)
            {
                CrouchRate = Mathf.Clamp(UpdateCrouchRate(), -1f, 1f);
            }

            if (CanTurn)
            {
                TurnAxis = UpdateTurnAxis();
            }

            if (CanMove)
            {
                MovementAxis = UpdateMovementAxis();
                MovementMagnitude = MovementAxis.magnitude;
            }

        
            SprintingPressed = UpdateSprinting();
            RecalibratePressed = UpdateRecalibrate();
        }

        protected void ResetFields()
        {
            JumpPressed = false;
            MovementAxis = Vector2.zero;
            TurnAxis = Vector2.zero;
            CrouchRate = 0f;
            MovementMagnitude = 0f;
        }

        /// <summary>
        /// Return true if jump is held down.
        /// </summary>
        protected abstract bool UpdateJump();

        /// <summary>
        /// Return the rate of crouching between -1 and 1. The output of this method will be clamped to -1 to 1.
        /// </summary>

        protected abstract float UpdateCrouchRate();

        protected abstract Vector2 UpdateMovementAxis();

        protected abstract Vector2 UpdateTurnAxis();

        protected abstract bool UpdateSprinting();

        protected abstract bool UpdateRecalibrate();


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