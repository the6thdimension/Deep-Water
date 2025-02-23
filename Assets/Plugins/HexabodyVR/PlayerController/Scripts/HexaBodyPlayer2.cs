using System;
using System.Collections;
using UnityEngine;

namespace HexabodyVR.PlayerController
{
    [RequireComponent(typeof(HexaBodyPlayerInputs))]
    public class HexaBodyPlayer2 : MonoBehaviour
    {
        [Header("Locomotion")]
        [Tooltip("Air Acceleration")]
        public float AirAcceleration = 1.2f;
        [Tooltip("Angular Velocity Acceleration of the locosphere")]
        public float Acceleration = 1f;
        [Tooltip("Deacelleration of locosphere")]
        public float Deacelleration = .25f;

        [Tooltip("RunAcceleration of the locosphere")]
        public float RunAcceleration = 1.5f;

        [Tooltip("Target linear walking speed")]
        public float WalkSpeed = 1.25f;
        [Tooltip("Target Linear sprinting speed")]
        public float RunSpeed = 2.5f;
        [Tooltip("Crouch max speed curve adjust by crouch amount")]
        public AnimationCurve CrouchSpeedCurve;
        [Tooltip("Crouch acceleration curve adjust by crouch amount")]
        public AnimationCurve CrouchAccelerationCurve;
        [Tooltip("Movement acceleration modification based on grounded angle")]
        public AnimationCurve SlopeCurve;
        [Tooltip("Movement speed modification based on grounded angle")]
        public AnimationCurve SlopeSpeedCurve;
        [Tooltip("Camera deadzone for moving the player")]
        public float CameraMoveThreshold = .001f;


        [Tooltip("Double click timeout for sprinting,(vive controllers).")]
        public float SprintDoubleClickThreshold = .25f;

        [Header("Body Adjustment")]
        [Tooltip("Height of the virtual player")]
        public float PlayerHeight = 1.66f;
        [Tooltip("Percent of player height for determining crouching")]
        public float LegPercent = .7f;
        [Tooltip("length of the neck for looking down and offsetting real life crouch")]
        public float NeckFactor = .3f;
        [Tooltip("How far above the locosphere the bumper sits")]
        public float BumperOffset = .07f;
        [Tooltip("Sitting or standing mode")]
        public SitStand SitStanding = SitStand.Standing;
        [Tooltip("How far above the players head can the RL camera go before capping out?")]
        public float CameraCeilingOffset = .15f;

        [Header("Turning")]
        public bool SmoothTurn = true;
        public float SmoothTurnSpeed = 90f;
        public float SmoothTurnThreshold = .1f;

        public float SnapTurnSpeed = 450f;
        public float SnapAmount = 45f;
        [Tooltip("Axis threshold to be considered valid for snap turning.")]
        public float SnapThreshold = .75f;

        [Header("Ground Checking")]
        [Tooltip("If on a slope below this angle then lock the locosphere")]
        public float SlopeAngle = 45f;
        [Tooltip("How far down to check for ground")]
        public float GroundedRayLength = .07f;
        public LayerMask GroundedLayerMask;

        [Header("Standing")]
        [Tooltip("Force of the spring keeping the player standing")]
        public float StandingForce = 10000f;
        [Tooltip("Damper for standing, should aim for a critical damped ratio")]
        public float StandingDamper = 1256f;
        public float StandingMaxForce = 20000;

        [Header("Jumping")]
        [Tooltip("Max for to push off the ground with")]
        public float JumpForce = 100000;
        [Tooltip("Curve adjustment against JumpForce based on the crouch amount, less force is needed the more the player is crouched")]
        public AnimationCurve JumpCurve;
        [Tooltip("The ball becomes this mass when jumping")]
        public float BallJumpMass = 5f;
        [Tooltip("If you don't leave the ground after this amount of time when jumping the jump is reset")]
        public float JumpTimeout = .50f;



        public CrouchLevel RetractLevel = CrouchLevel.Squat;

        [Header("Crouching")]
        public float LevelOneHeight = .75f;
        public float LevelTwoHeight = .5f;
        public float LevelThreeHeight = .25f;
        [Tooltip("How far the camera has to go down before the player starts crouching")]
        public float CrouchThreshold = 0f;
        [Tooltip("Crouch speed")]
        public float CrouchSpeed = 1.5f;
        [Tooltip("If the crouch or stand button is held for this long crouching will continue")]
        public float ContinueCrouchThreshold = .15f;

        public float JumpCrouchSpeed = 2f;

        [Header("Required Transforms")]
        public HexaCameraRig CameraRig;
        public Transform Camera;
        public Transform NeckPivot;
        public Transform CameraScale;
        public Transform Neck;
        public Transform UpperBody;
        public Transform NeckAnchor;
        public Transform Bumper;
        public Transform MantleCheck;

        [Header("RigidBodies")]
        public Rigidbody LocoBall;
        public Rigidbody Torso;
        public Rigidbody Knee;
        public Rigidbody HeadRigidbody;

        [Header("Colliders")]

        public CapsuleCollider LegsCapsule;
        public CapsuleCollider CapsuleTorso;
        public SphereCollider LocoSphere;
        public SphereCollider BumperSphere;
        public CapsuleCollider NeckCapsule;


        [Header("Joints")]
        public ConfigurableJoint JointLegTorso;
        public ConfigurableJoint JointLegBall;
        public ConfigurableJoint JointHead;

        [Header("Hands")]
        public Transform LeftController;
        public Transform RightController;

        [Space(30)]

        [Header("Height Stuff")]
        [Header("------Debug-----")]

        public float CalibratedHeight = 1.66f;
        public float SittingOffset;
        public float FloorOffsetAdjustment;
        public float VirtualWaistHeight;
        public float WaistHeight;
        public float WaistToBallHeight;
        public float CrouchOffset;
        public float TargetCrouchOffset;
        public float LegHeight;
        public float FakeCrouchAmount;
        public float RealCrouchOffset;
        public float NeckBendOffset;

        [Header("Camera Offsets")]
        public float PelvisHeightOffset;
        public float RealCrouchHeightOffset;
        public float MinCameraHeightOffset;
        public float MaxCameraHeightOffset;
        public float HeadOffset;
        public float MinCameraHeight = .3f;
        public Vector3 CameraRigPosition;
        public Vector3 PreviousCameraRigPosition;

        [Header("Misc")]
        [SerializeField] private float _jumpForce;
        [SerializeField] private float _groundAngle;
        [SerializeField] private CrouchLevel _crouchLevel;
        [SerializeField] private float _jumpTime;
        [SerializeField] private LegStage _legStage;
        [SerializeField] private float _actualSpeed;
        [SerializeField] private float _crouchPercent;

        [Header("Velocities")]
        [SerializeField] private float _verticalSpeed;
        [SerializeField] private float _locoAngularVelocity;
        [SerializeField] private float _targetAngularVelocity;
        [SerializeField] private float _modifiedTargetSpeed;
        [SerializeField] private float _modifiedTargetAcceleration;

        private float _originalLocoRadius;
        private float _originalBumperRadius;
        private float _ballMass;
        private float _kneeMass;
        private bool _jump;
        private float _crouchTimer;
        private float _scale;

        private float _timeSinceLastPress;
        private bool _awaitingSecondClick;

        private Vector3 _previousCamera;
        private Vector3 _crouchVelocity;
        private Vector3 _jumpVelocity;

        public bool Sprinting { get; set; }

        public Quaternion GroundRotation { get; private set; }

        public HexaBodyPlayerInputs Inputs { get; private set; }

        public Vector3 Forward { get; private set; }
        public Vector3 Right { get; private set; }

        public bool IsGrounded { get; set; }

        public float CameraHeight => SittingOffset + FloorOffsetAdjustment + Camera.localPosition.y * _scale;

        public float EyeLevel => PlayerHeight;

        void Start()
        {
            Inputs = GetComponent<HexaBodyPlayerInputs>();
            _originalLocoRadius = LocoSphere.radius;
            _originalBumperRadius = BumperSphere.radius;
            _ballMass = LocoBall.mass;
            _kneeMass = Knee.mass;
            _crouchLevel = CrouchLevel.Standing;

            CalibrateHeight(PlayerHeight);
        }


        public void CalibrateHeight(float height)
        {
            CalibratedHeight = height;

            if (SitStanding == SitStand.Standing)
            {
                SittingOffset = 0f;
                _scale = PlayerHeight / CalibratedHeight;
            }
            else
            {
                SittingOffset = PlayerHeight - height;
                _scale = 1f;
            }

            CameraScale.localScale = new Vector3(_scale, _scale, _scale);
        }

        [SerializeField]
        // Update is called once per frame
        void Update()
        {
            if (Inputs.RecalibratePressed)
            {
                Calibrate();
            }

            WaistHeight = PlayerHeight * LegPercent;
            WaistToBallHeight = WaistHeight - LocoSphere.radius;

            _crouchPercent = (Torso.position.y - LocoBall.position.y) / WaistToBallHeight;
            _crouchPercent = Mathf.Clamp01(_crouchPercent);

            VirtualWaistHeight = Torso.position.y - LocoBall.position.y;

            UpdateCrouch();

            ApplyCameraOffsets();
            CheckInputs();

            Neck.position = NeckAnchor.position + (HeadRigidbody.position - NeckAnchor.position) / 2f;
            NeckCapsule.height = Vector3.Distance(HeadRigidbody.position, NeckAnchor.position);
            var target = HeadRigidbody.position - Neck.position;
            var rotation = Quaternion.FromToRotation(Neck.up, target);
            Neck.transform.rotation = rotation * Neck.transform.rotation;

            _locoAngularVelocity = LocoBall.angularVelocity.magnitude;
        }


        private void UpdateCrouch()
        {
            var level = (int)_crouchLevel;


            if (Inputs.CrouchState.JustActivated)
            {
                _crouchTimer = 0f;
                level++;
            }
            else if (Inputs.StandState.JustActivated)
            {
                _crouchTimer = 0f;
                level--;
            }

            _crouchTimer += Time.deltaTime;


            if (_crouchTimer > ContinueCrouchThreshold)
            {
                if (Inputs.CrouchState.Active && Mathf.Approximately(CrouchOffset, TargetCrouchOffset))
                {
                    level++;
                }
                else if (Inputs.StandState.Active && Mathf.Approximately(CrouchOffset, TargetCrouchOffset))
                {
                    level--;
                }
            }

            level = Mathf.Clamp(level, 0, 3);

            SetCrouchLevel((CrouchLevel)level);


            if (!Inputs.CrouchState.Active && _legStage == LegStage.Standing && IsGrounded && !Inputs.JumpState.Active)
            {
                if (Sprinting && LocoBall.linearVelocity.sqrMagnitude > 2f)
                {
                    SetCrouchLevel(CrouchLevel.Standing);
                }
                else if (_crouchLevel == CrouchLevel.ButtOnTheFloor)
                {
                    SetCrouchLevel(CrouchLevel.SuperSquat);
                }
                else if (LocoBall.linearVelocity.sqrMagnitude > 2f && (_crouchLevel == CrouchLevel.ButtOnTheFloor || _crouchLevel == CrouchLevel.SuperSquat || _crouchLevel == CrouchLevel.Squat))
                {
                    SetCrouchLevel(CrouchLevel.KneeBent);
                }
            }

            if (_legStage == LegStage.Standing)
            {
                CrouchOffset = Mathf.MoveTowards(CrouchOffset, TargetCrouchOffset, CrouchSpeed * Time.deltaTime);
            }
            else
            {
                CrouchOffset = TargetCrouchOffset;
            }

            if (_legStage == LegStage.Standing || _legStage == LegStage.Jumping)
            {
                SetLegHeight(CrouchOffset);
            }
        }

        private void ForceCrouchHeight(CrouchLevel level)
        {
            SetCrouchLevel(level);
            SetLegHeight(TargetCrouchOffset);
        }

        private void SetLegHeight(float offset)
        {
            var neckDirection = Camera.rotation * new Vector3(0, NeckFactor, 0);
            var amount = neckDirection.y - NeckFactor;
            NeckBendOffset = Mathf.Clamp(amount, -NeckFactor, 0);

            RealCrouchOffset = EyeLevel - CameraHeight - CrouchThreshold + NeckBendOffset;
            RealCrouchOffset = Mathf.Clamp(RealCrouchOffset, 0, WaistToBallHeight);

            FakeCrouchAmount = offset;
            FakeCrouchAmount = Mathf.Clamp(FakeCrouchAmount, 0, WaistToBallHeight);

            LegHeight = WaistToBallHeight - FakeCrouchAmount - RealCrouchOffset;
            LegHeight = Mathf.Clamp(LegHeight, 0, WaistToBallHeight);
        }

        private void SetCrouchLevel(CrouchLevel level)
        {
            _crouchLevel = level;
            TargetCrouchOffset = GetCrouchOffset(level);
        }

        private float GetCrouchOffset(CrouchLevel level)
        {
            var offset = 0f;
            switch (level)
            {
                case CrouchLevel.Standing:
                    offset = 0f;
                    break;
                case CrouchLevel.KneeBent:
                    offset = WaistToBallHeight * (1 - LevelOneHeight);
                    break;
                case CrouchLevel.Squat:
                    offset = WaistToBallHeight * (1 - LevelTwoHeight);
                    break;
                case CrouchLevel.SuperSquat:
                    offset = WaistToBallHeight * (1 - LevelThreeHeight);
                    break;
                case CrouchLevel.ButtOnTheFloor:
                    offset = WaistToBallHeight;
                    break;
            }

            return offset;
        }

        public void ApplyCameraOffsets()
        {
            PelvisHeightOffset = -WaistHeight;
            RealCrouchHeightOffset = Mathf.Clamp(RealCrouchOffset, 0f, RealCrouchOffset);

        

            MinCameraHeightOffset = 0f;
            MaxCameraHeightOffset = 0f;

            if (CameraHeight < MinCameraHeight)
            {
                MinCameraHeightOffset = MinCameraHeight - CameraHeight;
            }
            else if (CameraHeight > EyeLevel + CameraCeilingOffset)
            {
                MaxCameraHeightOffset = -(CameraHeight - EyeLevel - CameraCeilingOffset);
            }

            var offset = SittingOffset + PelvisHeightOffset + RealCrouchHeightOffset + MinCameraHeightOffset + MaxCameraHeightOffset;

            CameraRig.FloorOffset = FloorOffsetAdjustment + offset;
        }


        private void FixedUpdate()
        {
            _verticalSpeed = Torso.linearVelocity.y;
            _actualSpeed = Torso.linearVelocity.magnitude;

            UpdateLegs();
            CheckGrounded();
            Jump();
            Move();
            Turn();
            UpdateHead();
        }

        private void UpdateHead()
        {
            HeadOffset = EyeLevel - WaistHeight;

            var dir = Camera.position - Torso.transform.position;
            dir.Normalize();
            var anchor = Torso.transform.TransformDirection(new Vector3(0, HeadOffset, -NeckPivot.localPosition.z));

            anchor = Quaternion.FromToRotation(anchor, dir) * anchor;
            JointHead.connectedAnchor = Torso.transform.InverseTransformDirection(anchor);
        }

        private void UpdateLegs()
        {
            var cameraVelocity = (Camera.localPosition - _previousCamera) / Time.fixedDeltaTime;
            cameraVelocity.x = cameraVelocity.z = 0f;
            _previousCamera = Camera.localPosition;

            Bumper.position = LocoBall.position + new Vector3(0, BumperOffset, 0f);
            JointLegTorso.anchor = Vector3.up * -(WaistToBallHeight);
            JointLegTorso.targetPosition = Vector3.up * (WaistToBallHeight - LegHeight);

            if (_legStage == LegStage.Standing)
            {
                _jumpVelocity = new Vector3(0f, Inputs.JumpState.Active ? JumpCrouchSpeed : 0, 0f);

                if (Inputs.CrouchState.Active)
                {
                    _crouchVelocity.y = CrouchSpeed;
                }
                else if (Inputs.StandState.Active)
                {
                    _crouchVelocity.y = -CrouchSpeed;
                }
                else
                {
                    _crouchVelocity.y = 0f;
                }

                if (_crouchLevel == CrouchLevel.SuperSquat)
                {
                    if (Inputs.JumpState.Active)
                    {
                        _crouchVelocity.y = 0f;
                    }
                }

                JointLegTorso.targetVelocity = -cameraVelocity + _crouchVelocity + _jumpVelocity;
            }
            else if (_legStage == LegStage.Jumping)
            {
                JointLegTorso.targetVelocity = Vector3.zero;
            }

            LegsCapsule.height = Vector3.Distance(Torso.position, LocoBall.position);
            LegsCapsule.center = new Vector3(0, LegsCapsule.height / 2f, 0f);

            var drive = JointLegTorso.yDrive;

            if (_legStage != LegStage.Jumping)
            {
                drive.positionSpring = StandingForce;
                drive.positionDamper = StandingDamper;
                drive.maximumForce = StandingMaxForce;
            }

            JointLegTorso.yDrive = drive;
        }

        private float _previousTurnAxis;
        private bool _turning;
        private void Turn()
        {
            if (SmoothTurn)
            {
                if (Math.Abs(Inputs.TurnAxis.x) > SmoothTurnThreshold)
                {
                    Torso.AddTorque(Vector3.up * (SmoothTurnSpeed * Inputs.TurnAxis.x * Mathf.Deg2Rad), ForceMode.VelocityChange);
                }
            }
            else if (!_turning)
            {
                var input = Inputs.TurnAxis.x;
                if (Math.Abs(input) >= SnapThreshold && Mathf.Abs(_previousTurnAxis) < SnapThreshold)
                {
                    StartCoroutine(SnapTurn(Mathf.Sign(input)));
                }

                _previousTurnAxis = input;
            }
        }

        private IEnumerator SnapTurn(float sign)
        {
            _turning = true;

            var rotation = Quaternion.Euler(0, sign * SnapAmount, 0);
            var finish = Torso.transform.rotation * rotation;
            var elapsed = 0f;
            var time = SnapAmount / SnapTurnSpeed;
            while (elapsed < time)
            {
                Torso.transform.rotation = Quaternion.RotateTowards(Torso.transform.rotation, finish, SnapTurnSpeed * Time.deltaTime);
                yield return null;
                elapsed += Time.deltaTime;
            }

            _turning = false;
        }


        private float Mass => Torso.mass + LocoBall.mass + HeadRigidbody.mass + Knee.mass + 4;

        private void Jump()
        {
            if (_jump && IsGrounded)
            {
                _jump = false;
                StartCoroutine(Jumping());
            }
        }

        private IEnumerator Jumping()
        {
            try
            {
                _jumpTime = 0f;

                Knee.mass = BallJumpMass;
                LocoBall.mass = BallJumpMass;
                _legStage = LegStage.Jumping;

                var spring = JumpCurve.Evaluate(_crouchPercent) * JumpForce;
                _jumpForce = spring;
                _crouchLevel = CrouchLevel.Standing;

                LegHeight = WaistToBallHeight - RealCrouchOffset;

                var drive = JointLegTorso.yDrive;
                drive.positionSpring = spring;
                drive.positionDamper = 2 * Mathf.Sqrt(spring * LocoBall.mass);
                drive.maximumForce = spring;
                JointLegTorso.yDrive = drive;

                while (IsGrounded)
                {
                    if (_jumpTime > JumpTimeout)
                    {
                        Debug.Log($"liftoff failed!");
                        _legStage = LegStage.Standing;
                        LocoBall.mass = _ballMass;
                        if (Inputs.JumpState.Active)
                        {
                            SetCrouchLevel(CrouchLevel.KneeBent);
                        }
                        yield break;
                    }
                    yield return new WaitForFixedUpdate();
                    _jumpTime += Time.deltaTime;
                }

                StartCoroutine(JumpRetract());
            }
            finally
            {
            }
        }

        private void ResetLegs()
        {
            LocoBall.mass = _ballMass;
            Knee.mass = _kneeMass;
        }

        private IEnumerator JumpRetract()
        {
            var time = Torso.linearVelocity.y / Mathf.Abs(Physics.gravity.y);

            _legStage = LegStage.JumpRetract;

            ScaleBalls(true);

            var start = LegHeight;
            var target = WaistToBallHeight - GetCrouchOffset(RetractLevel);
            var elapsed = 0f;
            var velocity = Mathf.Abs(start - target) / time;
            JointLegTorso.targetVelocity = new Vector3(0f, velocity, 0f);
            var jumped = false;
            while (elapsed < time && _legStage == LegStage.JumpRetract)
            {
                if (Inputs.JumpState.JustActivated)
                {
                    jumped = true;
                    ForceCrouchHeight(CrouchLevel.KneeBent);
                    JointLegTorso.targetVelocity = Vector3.zero;
                }

                if (!jumped)
                {
                    LegHeight = Mathf.Lerp(start, target, elapsed / time);
                }

                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            JointLegTorso.targetVelocity = Vector3.zero;

            ScaleBalls(false);

            if (_legStage != LegStage.JumpRetract)
            {
                Debug.Log($"retract broken");
                yield break;
            }

            _legStage = LegStage.Landing;

            start = LegHeight;

            elapsed = 0f;

            velocity = Mathf.Clamp(velocity, velocity, 2f);
            JointLegTorso.targetVelocity = new Vector3(0f, -velocity, 0f);

            while (elapsed < time && _legStage == LegStage.Landing)
            {
                var grounded = Physics.SphereCast(LocoBall.position + Vector3.up * Physics.defaultContactOffset, LocoSphere.radius - Physics.defaultContactOffset, Vector3.down, out var h, GroundedRayLength, GroundedLayerMask, QueryTriggerInteraction.Ignore);
                if (grounded)
                {
                    JointLegTorso.targetVelocity = Vector3.zero;
                }
                if (grounded && Inputs.JumpState.Active)
                {
                    ResetLegs();
                    break;
                }

                if (Inputs.JumpState.JustActivated)
                {
                    jumped = true;
                    ForceCrouchHeight(CrouchLevel.KneeBent);
                    JointLegTorso.targetVelocity = Vector3.zero;
                }

                if (!jumped)
                {
                    LegHeight = Mathf.Lerp(start, WaistToBallHeight, elapsed / time);
                }


                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            JointLegTorso.targetVelocity = Vector3.zero;

            if (_legStage == LegStage.Landing)
            {
                _legStage = LegStage.Standing;
                ResetLegs();
            }
        }

        private void ScaleBalls(bool shrink)
        {
            if (shrink)
            {
                LocoSphere.radius = LegsCapsule.radius;
                BumperSphere.radius = LegsCapsule.radius + .01f;
            }
            else
            {
                LocoSphere.radius = _originalLocoRadius;
                BumperSphere.radius = _originalBumperRadius;
            }
        }

        private void CheckInputs()
        {
            CheckSprinting();

            Forward = Camera.forward;
            Right = Camera.right;

            if (Inputs.JumpState.JustDeactivated && IsGrounded)
            {
                _jump = true;
            }
        }

        private bool HandleHMDMovement()
        {
            HandleHMDRotation();
            PreviousCameraRigPosition = CameraRigPosition;

            var delta = NeckPivot.transform.position - Torso.transform.position;
            delta.y = 0f;
            var direction = delta.normalized;
            var magnitude = delta.magnitude;
            if (magnitude > CameraMoveThreshold)
            {
                //var linearVelocity = GroundRotation * (delta / Time.fixedDeltaTime);

                var moveDelta = GroundRotation * delta;
                LocoBall.MovePosition(LocoBall.position + moveDelta);
                Knee.MovePosition(Knee.position + moveDelta);
                HeadRigidbody.MovePosition(HeadRigidbody.position + moveDelta);
                Torso.MovePosition(Torso.position + moveDelta);

                var axis = Vector3.Cross(Vector3.up, direction).normalized;
                //var angularVelocity = axis * (magnitude / Time.fixedDeltaTime) / LocoSphere.radius;
                //LocoBall.angularVelocity += angularVelocity;

                //LocoBall.velocity += linearVelocity;
                CameraRig.transform.localPosition -= Torso.transform.InverseTransformDirection(delta);
            }

            return magnitude > CameraMoveThreshold;
        }

        private void HandleHMDRotation()
        {
            var angles = Quaternion.LookRotation(Camera.forward, Vector3.up).eulerAngles;
            angles.x = UpperBody.transform.eulerAngles.x;
            angles.z = UpperBody.transform.eulerAngles.z;
            UpperBody.transform.rotation = Quaternion.Euler(angles);
        }

        private void AirMove(Vector3 direction)
        {
            AirAccelerate(Torso, direction);
            AirAccelerate(Knee, direction);
            AirAccelerate(LocoBall, direction);
            AirAccelerate(HeadRigidbody, direction);
        }

        private void AirAccelerate(Rigidbody rb, Vector3 direction)
        {
            rb.linearVelocity += direction * AirAcceleration * Time.fixedDeltaTime;
            //rb.velocity = Vector3.ClampMagnitude(rb.velocity, AirMaxSpeed);
        }

        private void Move()
        {
            var movement = Inputs.MovementAxis;
            var wasd = CheckWASD();
            if (wasd.magnitude > 0)
            {
                movement = wasd;
            }

            var direction = Forward * movement.y + Right * movement.x;

            if (!IsGrounded)
            {
                AirMove(direction);
            }

            var noMovement = Mathf.Abs(movement.x) < .1f && Mathf.Abs(movement.y) < .1f;

            var targetSpeed = Sprinting ? RunSpeed : WalkSpeed;
            var axis = Vector3.Cross(Vector3.up, direction).normalized;
            var groundAngle = Mathf.Clamp(_groundAngle, 0, 90);
            var groundPercent = groundAngle / 90f;
            var crouchCurve = CrouchSpeedCurve.Evaluate(_crouchPercent);
            targetSpeed = targetSpeed * crouchCurve;
            var targetAngularVelocity = SlopeSpeedCurve.Evaluate(groundPercent) * targetSpeed / LocoSphere.radius;
            var adjustedAcceleration = (SlopeCurve.Evaluate(groundPercent) * CrouchAccelerationCurve.Evaluate(_crouchPercent) * (Sprinting ? RunAcceleration : Acceleration)) / LocoSphere.radius;

            LocoBall.angularVelocity += adjustedAcceleration * axis;

            if (IsGrounded && _legStage != LegStage.Jumping && _legStage != LegStage.JumpRetract)
            {
                LocoBall.angularVelocity = Vector3.ClampMagnitude(LocoBall.angularVelocity, targetAngularVelocity);
            }

            //JointLegBall.targetAngularVelocity = JointLegBall.transform.InverseTransformDirection(targetAngularVelocity * -axis);

            var hmdMoved = HandleHMDMovement();
            LocoBall.freezeRotation = false;
            if (noMovement && IsGrounded && _groundAngle < SlopeAngle && _legStage != LegStage.Jumping && _legStage != LegStage.JumpRetract)
            {
                LocoBall.angularVelocity *= Deacelleration;

                if (LocoBall.angularVelocity.magnitude < 1f)
                {
                    LocoBall.freezeRotation = true;
                }
            }

            _modifiedTargetSpeed = SlopeSpeedCurve.Evaluate(groundPercent) * targetSpeed;
            _modifiedTargetAcceleration = (SlopeCurve.Evaluate(groundPercent) * CrouchAccelerationCurve.Evaluate(_crouchPercent) * (Sprinting ? RunAcceleration : Acceleration));
            _targetAngularVelocity = targetAngularVelocity;
        }

        private void CheckGrounded()
        {
            IsGrounded = Physics.SphereCast(LocoBall.position + Vector3.up * Physics.defaultContactOffset, LocoSphere.radius - Physics.defaultContactOffset, Vector3.down, out var h, GroundedRayLength, GroundedLayerMask, QueryTriggerInteraction.Ignore);
            if (IsGrounded)
            {
                _groundAngle = Vector3.Angle(h.normal, Vector3.up);
                GroundRotation = Quaternion.FromToRotation(Vector3.up, h.normal);
            }
        }


        private Vector2 CheckWASD()
        {
            var x = 0f;
            var y = 0f;
            if (Input.GetKey(KeyCode.W))
                y += 1f;
            if (Input.GetKey(KeyCode.S))
                y -= 1f;
            if (Input.GetKey(KeyCode.A))
                x += -1f;
            if (Input.GetKey(KeyCode.D))
                x += 1f;

            return new Vector2(x, y);
        }

        protected virtual void CheckSprinting()
        {
            if (Inputs.SprintRequiresDoubleClick)
            {
                if (_awaitingSecondClick)
                {
                    _timeSinceLastPress += Time.deltaTime;
                }

                if (!Sprinting && Inputs.SprintingPressed)
                {
                    if (_timeSinceLastPress < SprintDoubleClickThreshold && _awaitingSecondClick)
                    {
                        Sprinting = true;
                        _awaitingSecondClick = false;
                    }
                    else
                    {
                        _timeSinceLastPress = 0f;
                        _awaitingSecondClick = true;
                    }
                }
            }
            else
            {

                if (Sprinting && Inputs.SprintingPressed)
                    Sprinting = false;
                else if (!Sprinting && Inputs.SprintingPressed)
                    Sprinting = true;
            }

            if (Inputs.MovementAxis.magnitude < .01f)
            {
                Sprinting = false;
            }
        }

        public void SetSitStandMode(bool sitting)
        {
            SitStanding = sitting ? SitStand.Sitting : SitStand.Standing;
            Calibrate();
        }

        public void Calibrate()
        {
            CalibrateHeight(Camera.localPosition.y);
        }

    }
}