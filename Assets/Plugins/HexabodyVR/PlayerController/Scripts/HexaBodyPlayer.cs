using System;
using System.Collections;
using UnityEngine;

namespace HexabodyVR.PlayerController
{
    [RequireComponent(typeof(HexaBodyPlayerInputs))]
    public class HexaBodyPlayer : MonoBehaviour
    {
        [Header("Locomotion")]

        [Tooltip("Acceleration of the locosphere")]
        public float Acceleration = 10f;
        [Tooltip("Deaccelleration of locosphere")]
        public float Deacelleration = .25f;
        [Tooltip("RunAcceleration of the locosphere")]
        public float RunAcceleration;
        [Tooltip("Force mode for torquing the locosphere")]
        public ForceMode RollMode = ForceMode.VelocityChange;

        [Tooltip("Target linear walking speed")]
        public float WalkSpeed = 1.25f;
        [Tooltip("Target Linear sprinting speed")]
        public float RunSpeed = 2.5f;
        [Tooltip("Crouch max speed curve adjust by crouch amount")]
        public AnimationCurve CrouchSpeedCurve;
        [Tooltip("Crouch acceleration curve adjust by crouch amount")]
        public AnimationCurve CrouchAccelerationCurve;
        [Tooltip("Movement speed modification based on grounded angle")]
        public AnimationCurve SlopeCurve;
        [Tooltip("Camera deadzone for moving the player")]
        public float CameraMoveThreshold = .001f;


        [Tooltip("Double click timeout for sprinting,(vive controllers).")]
        public float SprintDoubleClickThreshold = .25f;

        [Header("Body Adjustment")]
        [Tooltip("Height of the virtual player")]
        public float PlayerHeight = 1.66f;
        [Tooltip("Percent of player height for determining crouching")]
        public float LegPercent = .7f;
        [Tooltip("length of the neck for looking down")]
        public float NeckFactor = .3f;
        [Tooltip("How far above the locosphere the bumper sits")]
        public float BumperOffset = .07f;
        [Tooltip("Sitting or standing mode")]
        public SitStand SitStanding = SitStand.Standing;

        [Header("Turning")]
        public bool SmoothTurn = true;
        public float SmoothTurnSpeed = 90f;
        public float SmoothTurnThreshold = .1f;


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


        [Header("Jumping")]
        [Tooltip("Max for to push off the ground with")]
        public float JumpForce = 40000;
        [Tooltip("Curve adjustment against JumpForce based on the crouch amount, less force is needed the more the player is crouched")]
        public AnimationCurve JumpCurve;
        [Tooltip("Curve to determine the ball retraction speed when jumping")]
        public AnimationCurve JumpRetractCurve;
        [Tooltip("Force applied to lift the ball when jumping")]
        public float RetractForce = 5000;
        [Tooltip("Force to stand the player back up after landing a jump")]
        public float LandingForce = 4000;
        [Tooltip("Spring damper to stand the player back up after landing a jump")]
        public float LandingDamper = 3000;
        [Tooltip("Max speed to retract the ball when jumping")]
        public float RetractSpeed = 6;
        [Tooltip("Max speed to push down on the ball when landing a jump")]
        public float LandingSpeed = 4;
        [Tooltip("The ball becomes this mass when jumping")]
        public float BallJumpMass = 5f;
        [Tooltip("If you don't leave the ground after this amount of time when jumping the jump is reset")]
        public float JumpTimeout = .20f;


        [Header("Crouching")]
        public float LevelOneHeight = .75f;
        public float LevelTwoHeight = .5f;
        public float LevelThreeHeight = .25f;
        [Tooltip("How far the camera has to go down before the player starts crouching")]
        public float CrouchThreshold = 0f;
        [Tooltip("Lerp speed when fake crouching")]
        public float CrouchSpeed = 1.5f;
        [Tooltip("If the crouch or stand button is held for this long crouching will continue")]
        public float ContinueCrouchThreshold = .15f;

        [Header("Required Transforms")]
        public HexaCameraRig CameraRig;
        public Transform Camera;
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
        public float TargetLegHeight;
        public float LegHeight;
        public float FakeCrouchAmount;
        public float RealCrouchOffset;

        [Header("Camera Offsets")]
        public float PelvisHeightOffset;
        public float RealCrouchHeightOffset;
        public float NeckBendOffset;
        public float MinCameraHeightOffset;
        public float MaxCameraHeightOffset;
        public float HeadOffset;
        public float MinCameraHeight = .3f;
        public Vector3 CameraRigPosition;
        public Vector3 PreviousCameraRigPosition;

        [SerializeField]
        private float _jumpSpring;
        [SerializeField]
        private float _groundAngle;
        [SerializeField]
        private CrouchLevel _crouchLevel;
        [SerializeField]
        private float _jumpTime;
        [SerializeField]
        private LegStage _legStage;
        [SerializeField]
        private Vector3 _lastCameraDirection;
        [SerializeField]
        private Vector3 _previousPosition;
        [SerializeField]
        private float _actualSpeed;

        private float _originalLocoRadius;
        private float _originalBumperRadius;
        private float _ballMass;
        private bool _jump;
        private float _crouchTimer;
        private float _crouchPercent;
        private float _scale;


        private Vector3 _neckDirection;
        private float _timeSinceLastPress;
        private bool _awaitingSecondClick;
        private Coroutine _retractRoutine;
        private Coroutine _jumpRoutine;

        public bool Sprinting { get; set; }

        public Quaternion GroundRotation { get; private set; }

        public HexaBodyPlayerInputs Inputs { get; private set; }

        public Vector3 Forward { get; private set; }
        public Vector3 Right { get; private set; }

        public bool IsGrounded { get; set; }

        public float CameraHeight => SittingOffset + FloorOffsetAdjustment + Camera.localPosition.y * _scale;

        public float EyeLevel => PlayerHeight - FloorOffsetAdjustment;

        // Start is called before the first frame update
        void Start()
        {
            Inputs = GetComponent<HexaBodyPlayerInputs>();
            _originalLocoRadius = LocoSphere.radius;
            _originalBumperRadius = BumperSphere.radius;
            _ballMass = LocoBall.mass;

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

            _crouchPercent = (Knee.position.y - LocoBall.position.y) / WaistToBallHeight;
            _crouchPercent = Mathf.Clamp01(_crouchPercent);

            VirtualWaistHeight = Torso.position.y - LocoBall.position.y;

            UpdateCrouch();

            ApplyCameraOffsets();
            CheckInputs();

            if (Math.Abs(Inputs.TurnAxis.x) > SmoothTurnThreshold)
            {
                //Torso.MoveRotation(Torso.rotation * Quaternion.Euler(0, (SmoothTurnSpeed * Time.deltaTime * _rotateAxis.x), 0));
                //Torso.AddTorque(Vector3.up * (SmoothTurnSpeed * Inputs.TurnAxis.x * Mathf.Deg2Rad), ForceMode.VelocityChange);
                //Torso.transform.RotateAround(Torso.transform.position, Vector3.up, SmoothTurnSpeed * Time.deltaTime * _rotateAxis.x);
            }

            Neck.position = NeckAnchor.position + (HeadRigidbody.position - NeckAnchor.position) / 2f;
            NeckCapsule.height = Vector3.Distance(HeadRigidbody.position, NeckAnchor.position);
            var target = HeadRigidbody.position - Neck.position;
            var rotation = Quaternion.FromToRotation(Neck.up, target);
            Neck.transform.rotation = rotation * Neck.transform.rotation;

        }


        private void UpdateCrouch()
        {
            var level = (int)_crouchLevel;


            if (Input.GetKeyDown(KeyCode.X) || Inputs.CrouchState.JustActivated)
            {
                _crouchTimer = 0f;
                level++;
            }
            else if (Input.GetKeyDown(KeyCode.Z) || Inputs.StandState.JustActivated)
            {
                _crouchTimer = 0f;
                level--;
            }

            _crouchTimer += Time.deltaTime;


            var crouchFromJump = false;
            if (IsGrounded && (Input.GetKeyDown(KeyCode.Space) || Inputs.JumpState.JustActivated))
            {
                if (_legStage == LegStage.Standing)
                {
                    level++;
                    crouchFromJump = true;
                }
            }

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

            level = Mathf.Clamp(level, 0, 4);

            SetCrouchLevel((CrouchLevel)level);

            if (IsGrounded && Inputs.JumpState.Active && _legStage == LegStage.Standing && _crouchLevel == CrouchLevel.Standing)
            {
                SetCrouchLevel(CrouchLevel.KneeBent);
            }

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

            if (_legStage == LegStage.Standing && !crouchFromJump)
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
            RealCrouchOffset = EyeLevel - CameraHeight - CrouchThreshold + NeckBendOffset;
            RealCrouchOffset = Mathf.Clamp(RealCrouchOffset, 0, WaistToBallHeight);

            FakeCrouchAmount = offset;// - RealCrouchOffset;
            FakeCrouchAmount = Mathf.Clamp(FakeCrouchAmount, 0, WaistToBallHeight);

            TargetLegHeight = WaistToBallHeight - FakeCrouchAmount - RealCrouchOffset;

            TargetLegHeight = Mathf.Clamp(TargetLegHeight, 0, WaistToBallHeight);

            LegHeight = TargetLegHeight;
        }

        private void SetCrouchLevel(CrouchLevel level)
        {
            _crouchLevel = level;
            switch (_crouchLevel)
            {
                case CrouchLevel.Standing:
                    TargetCrouchOffset = 0f;
                    break;
                case CrouchLevel.KneeBent:
                    TargetCrouchOffset = WaistToBallHeight * (1 - LevelOneHeight);
                    break;
                case CrouchLevel.Squat:
                    TargetCrouchOffset = WaistToBallHeight * (1 - LevelTwoHeight);
                    break;
                case CrouchLevel.SuperSquat:
                    TargetCrouchOffset = WaistToBallHeight * (1 - LevelThreeHeight);
                    break;
                case CrouchLevel.ButtOnTheFloor:
                    TargetCrouchOffset = WaistToBallHeight;
                    break;
            }
        }

        public void ApplyCameraOffsets()
        {
            PelvisHeightOffset = -WaistHeight;
            RealCrouchHeightOffset = Mathf.Clamp(RealCrouchOffset, 0f, RealCrouchOffset);

            var amount = _neckDirection.y - NeckFactor;
            NeckBendOffset = Mathf.Clamp(amount, -NeckFactor, 0);

            MinCameraHeightOffset = 0f;
            MaxCameraHeightOffset = 0f;

            if (CameraHeight < MinCameraHeight)
            {
                MinCameraHeightOffset = MinCameraHeight - CameraHeight;
            }
            else if (CameraHeight > EyeLevel + .1f)
            {
                MaxCameraHeightOffset = -(CameraHeight - EyeLevel);
            }

            var offset = SittingOffset + PelvisHeightOffset + RealCrouchHeightOffset + MinCameraHeightOffset + MaxCameraHeightOffset;

            CameraRig.FloorOffset = FloorOffsetAdjustment + offset;
        }


        private Vector3 _previousAnchor;

        private void FixedUpdate()
        {
            Bumper.position = LocoBall.position + new Vector3(0, BumperOffset, 0f);
            JointLegBall.anchor = Vector3.up * -(LegHeight);

            if (Math.Abs(Inputs.TurnAxis.x) > SmoothTurnThreshold)
            {
                Torso.AddTorque(Vector3.up * (SmoothTurnSpeed * Inputs.TurnAxis.x * Mathf.Deg2Rad), ForceMode.VelocityChange);
            }

            if (IsGrounded)
            {
                JointLegBall.targetVelocity = (JointLegBall.anchor - _previousAnchor) / Time.fixedDeltaTime;
            }
            else
            {
                JointLegBall.targetVelocity = Vector3.zero;
            }
            
            _previousAnchor = JointLegBall.anchor;

            LegsCapsule.height = Vector3.Distance(Torso.position, LocoBall.position);
            LegsCapsule.center = new Vector3(0, -LegsCapsule.height / 2f, 0f);

            CheckGrounded();
            Jump();
            Move();

            _neckDirection = Camera.rotation * new Vector3(0, NeckFactor, 0);

            Debug.DrawLine(Torso.position, Torso.position + _neckDirection, Color.red);

            HeadOffset = EyeLevel - WaistHeight;

            var t = _neckDirection;
            t.y = 0f;

            JointHead.connectedAnchor = transform.InverseTransformDirection(t + new Vector3(0, HeadOffset, 0));

            var drive = JointLegBall.yDrive;

            if (_legStage == LegStage.Standing)
            {
                drive.positionSpring = StandingForce;
                drive.positionDamper = StandingDamper;
                drive.maximumForce = StandingForce;
            }

            JointLegBall.yDrive = drive;
        }

        private void Jump()
        {
            if (_jump && IsGrounded)
            {
                _jump = false;
                _jumpRoutine = StartCoroutine(Jumping());
            }
        }



        private IEnumerator Jumping()
        {
            try
            {
                _jumpTime = 0f;

                LocoBall.mass = BallJumpMass;
                _legStage = LegStage.Jumping;

                var crouchPercent = _crouchPercent;

                var spring = JumpCurve.Evaluate(_crouchPercent) * JumpForce;
                _jumpSpring = spring;
                _crouchLevel = CrouchLevel.Standing;

                LegHeight = WaistToBallHeight - RealCrouchOffset;

                var drive = JointLegBall.yDrive;
                drive.positionSpring = spring;
                drive.positionDamper = 2 * Mathf.Sqrt(spring * LocoBall.mass);
                drive.maximumForce = spring;
                JointLegBall.yDrive = drive;

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

                _retractRoutine = StartCoroutine(JumpRetract(crouchPercent));
            }
            finally
            {
            }
        }

        private IEnumerator JumpRetract(float crouchPercent)
        {
            _legStage = LegStage.JumpRetract;

            ScaleBalls(true);

            var drive = JointLegBall.yDrive;
            drive.positionSpring = RetractForce;
            drive.positionDamper = 2 * Mathf.Sqrt(RetractForce * LocoBall.mass);
            drive.maximumForce = RetractForce;
            JointLegBall.yDrive = drive;

            var start = LegHeight;
            var elapsed = 0f;

            var speed = JumpRetractCurve.Evaluate(crouchPercent) * RetractSpeed;

            var time = Mathf.Abs(LegHeight / speed);
            var jumped = false;
            while (elapsed < time && !IsGrounded && _legStage == LegStage.JumpRetract)
            {
                if (Inputs.JumpState.JustActivated)
                {
                    Debug.Log($"forced knee retract");
                    jumped = true;
                    ForceCrouchHeight(CrouchLevel.KneeBent);
                }

                if (!jumped)
                {
                    LegHeight = Mathf.Lerp(start, 0, elapsed / time);
                }

                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            ScaleBalls(false);

            if (_legStage != LegStage.JumpRetract)
            {
                Debug.Log($"retract broken");
                yield break;
            }

            _legStage = LegStage.Landing;

            start = LegHeight;

            elapsed = 0f;

            drive = JointLegBall.yDrive;
            drive.positionSpring = LandingForce;
            drive.positionDamper = LandingDamper;
            drive.maximumForce = LandingForce;
            JointLegBall.yDrive = drive;

            speed = JumpRetractCurve.Evaluate(crouchPercent) * LandingSpeed;

            time = Mathf.Abs(WaistToBallHeight / speed);
            while (elapsed < time && _legStage == LegStage.Landing)
            {
                var grounded = Physics.SphereCast(LocoBall.position + Vector3.up * Physics.defaultContactOffset, LocoSphere.radius - Physics.defaultContactOffset, Vector3.down, out var h, GroundedRayLength, GroundedLayerMask, QueryTriggerInteraction.Ignore);
                if (grounded && Inputs.JumpState.Active)
                {
                    LocoBall.mass = _ballMass;
                    break;
                }

                if (Inputs.JumpState.JustActivated)
                {
                    Debug.Log($"forced knee");
                    jumped = true;
                    ForceCrouchHeight(CrouchLevel.KneeBent);
                }

                if (!jumped)
                {
                    //LegHeight = Mathf.MoveTowards(LegHeight, target, speed);
                    LegHeight = Mathf.Lerp(start, WaistToBallHeight, elapsed / time);
                }


                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            if (_legStage == LegStage.Landing)
            {
                _legStage = LegStage.Standing;
                LocoBall.mass = _ballMass;
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

            if (Input.GetKeyUp(KeyCode.Space) || Inputs.JumpState.JustDeactivated && IsGrounded)
            {
                _jump = true;
            }
        }

        private bool HandleHMDMovement()
        {
            HandleHMDRotation();
            PreviousCameraRigPosition = CameraRigPosition;

            var v = _neckDirection;
            v.y = 0f;

            var delta = Camera.transform.position - Torso.transform.position - v;
            delta.y = 0f;
            var direction = delta.normalized;
            var magnitude = delta.magnitude;
            if (magnitude > CameraMoveThreshold)
            {
                var linearVelocity = GroundRotation * (delta / Time.fixedDeltaTime);

                var moveDelta = GroundRotation * delta;
                LocoBall.MovePosition(LocoBall.position + moveDelta);
                Knee.MovePosition(Knee.position + moveDelta);
                HeadRigidbody.MovePosition(HeadRigidbody.position + moveDelta);
                Torso.MovePosition(Torso.position + moveDelta);

                var axis = Vector3.Cross(Vector3.up, direction).normalized;
                //var angularVelocity = axis * (magnitude / Time.fixedDeltaTime) / LocoSphere.radius;
                //LocoBall.angularVelocity += angularVelocity;

                //LocoBall.velocity += linearVelocity;

                _lastCameraDirection = linearVelocity.normalized;

                CameraRig.transform.localPosition -= Torso.transform.InverseTransformDirection(delta);
            }

            Debug.DrawLine(LocoBall.position, LocoBall.position + _lastCameraDirection * 3f, Color.blue);

            return magnitude > CameraMoveThreshold;
        }

        private void HandleHMDRotation()
        {
            //until i can figure out how to torque the torso
            var angles = Quaternion.LookRotation(Camera.forward, Vector3.up).eulerAngles;
            angles.x = UpperBody.transform.eulerAngles.x;
            angles.z = UpperBody.transform.eulerAngles.z;
            UpperBody.transform.rotation = Quaternion.Euler(angles);
        }

        private void Move()
        {
            var movement = Inputs.MovementAxis;
            var wasd = CheckWASD();
            if (wasd.magnitude > 0)
            {
                movement = wasd;
            }

            var noMovement = Mathf.Abs(movement.x) < .1f && Mathf.Abs(movement.y) < .1f;

            LocoBall.freezeRotation = false;
           

            var targetSpeed = CrouchSpeedCurve.Evaluate(_crouchPercent) * (Sprinting ? RunSpeed : WalkSpeed);

            var targetAngularVelocity = targetSpeed / LocoSphere.radius;

            var direction = Forward * movement.y + Right * movement.x;
            var axis = Vector3.Cross(Vector3.up, direction).normalized;

            var groundAngle = Mathf.Clamp(_groundAngle, 0, 90);
            var groundPercent = 1 - groundAngle / 90f;

            LocoBall.angularVelocity = Vector3.ClampMagnitude(LocoBall.angularVelocity, targetAngularVelocity);
            if (IsGrounded)
            {
                LocoBall.linearVelocity = Vector3.ClampMagnitude(LocoBall.linearVelocity, targetSpeed);
            }
            
            var adjustedAcceleration = SlopeCurve.Evaluate(groundPercent) *  CrouchAccelerationCurve.Evaluate(_crouchPercent) * (Sprinting ? RunAcceleration : Acceleration);
            LocoBall.AddTorque(adjustedAcceleration * axis, RollMode);

            var hmdMoved = HandleHMDMovement();

            //if (!hmdMoved && noMovement && IsGrounded && _groundAngle < SlopeAngle)// && _groundAngle > 5)
            //{
               
            //}

            if (noMovement && IsGrounded && _groundAngle < SlopeAngle)
            {
                LocoBall.angularVelocity *= Deacelleration;

                if (LocoBall.angularVelocity.magnitude < 1f)
                {
                    LocoBall.freezeRotation = true;
                    LocoBall.angularVelocity = Vector3.zero;
                    ZeroXZ(Knee);
                    ZeroXZ(HeadRigidbody);
                    ZeroXZ(LocoBall);
                }
            }

            var velocity = (Torso.position - _previousPosition) / Time.fixedDeltaTime;
            _previousPosition = Torso.position;
            _actualSpeed = velocity.magnitude;
        }

        private void Stop()
        {
            LocoBall.angularVelocity = Vector3.zero;
            ZeroXZ(Torso);
            ZeroXZ(LocoBall);
            ZeroXZ(Knee);
            ZeroXZ(HeadRigidbody);
        }

        private void ZeroXZ(Rigidbody rigid)
        {
            var v = rigid.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            rigid.linearVelocity = v;
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

                //if (Sprinting && !LeftController.TrackpadButtonState.Active)
                //{
                //    Sprinting = false;
                //}
                //else 
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