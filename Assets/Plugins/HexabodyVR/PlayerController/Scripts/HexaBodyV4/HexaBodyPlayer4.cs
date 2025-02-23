using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace HexabodyVR.PlayerController
{
    [RequireComponent(typeof(HexaBodyInputsBase))]
    public class HexaBodyPlayer4 : MonoBehaviour
    {
        public const string HeightKey = "SaveHeight";

        [Header("Jumping")]
        public float JumpDamper = 3000f;

        //set to public to debug jump heights
        private float JumpVelGoal;
        private float JumpVelActual;
        private float JumpHeightGoal;
        private float JumpHeightActual;
        private bool SaveHeadHeight;

        [Tooltip("Crouch offset when holding down the jump button")]
        public float JumpCrouchAmount = .25f;

        [Tooltip("Crouch amount to jump height curve, not 100% precise due to the jointed nature of the body.")]
        public AnimationCurve JumpHeightCurve;

        [Tooltip("Curve to control the foot position after jumping")]
        public AnimationCurve RetractCurve;

        [Tooltip("Curve to control the foot position when stomping after hitting peak of the jump")]
        public AnimationCurve StompCurve;

        [Tooltip("Target Crouch Level to retract the football to when jumping. Percentage of WaistToBallHeight")]
        public float RetractLevel = .9f;

        [Tooltip("If you don't leave the ground after this amount of time when jumping the jump is reset")]
        public float JumpTimeout = .50f;

        [Tooltip("If jump button is pressed during a jump cycle, the legs will extend to the standing pose at this rate. " +
                 "Useful if you want to try and forcefully kick an object below you while you're jumping.")]
        public float StompSpeed = 9f;

        [Header("Jump Cycle Speed Bonus")]

        [Tooltip("While jumping, the ball will move at this speed in the movement direction until the player leaves the ground.")]
        public float JumpMovementBonus = .5f;

        [Tooltip("While jumping and sprinting, the ball will move at this speed in the movement direction until the player leaves the ground.")]
        public float JumpSprintBonus = .75f;

        [Tooltip("0 to 1 curve factor applied to the JumpMovementBonus based on crouch amount prior to the jump.")]
        public AnimationCurve JumpMovementCurve;

        [Tooltip("-1 to 1 curve to control the jump movement bonus amount when the controller input movement is in a different direction than the player's forward direction.")]
        public AnimationCurve JumpMovementDirectionCurve;



        [Header("Locomotion")]

        [Tooltip("Air Acceleration")]
        public float AirAcceleration = 1.2f;

        [Tooltip("Target linear walking speed")]
        public float WalkSpeed = 2f;

        [Tooltip("Target Linear sprinting speed")]
        public float RunSpeed = 3.5f;

        [Tooltip("SmoothTime parameter to smooth damp function, roughly the amount of time to hit desired speed. Too low and the ball might slip.")]
        public float SmoothTime = .15f;

        [Tooltip("Max speed of the smooth damp function")]
        public float MaxAcceleration = 9f;

        [Tooltip("Crouch max speed curve adjust by crouch amount")]
        public AnimationCurve CrouchSpeedCurve;

        [Tooltip("Max speed factor based on slope angle, 0 being level and 1 being 90 degrees")]
        public AnimationCurve SlopeMaxSpeedCurve;

        [Tooltip("LocoBall Friction factor based on slope angle, 0 being level and 1 being 90 degrees")]
        public AnimationCurve SlopeFrictionCurve;

        [Tooltip("-1 to 1 curve acceleration multiplier when the input direction is not in the same direction as the current direction")]
        public AnimationCurve DirectionAcceleration;

        [Tooltip("Camera deadzone for moving the player")]
        public float CameraMoveThreshold = .001f;

        [Tooltip("Double click timeout for sprinting.")]
        public float SprintDoubleClickThreshold = .25f;

        [Header("Body Adjustment")]

        [Tooltip("Height of the virtual player")]
        public float EyeHeight = 1.66f;

        public float PlayerWaistHeight = 1f;

        [Tooltip("How far above the locosphere the bumper sits")]
        public float BumperOffset = .07f;

        [Tooltip("Sitting or standing mode")]
        public SitStand SitStanding = SitStand.Standing;

        [Tooltip("Optional tip toes height offset")]
        public float TipToesOffset = .20f;

        [Tooltip("How far above the players head can the RL camera go before capping out?")]
        public float CameraCeilingOffset = .15f;

        [Tooltip("Max pelvis offset when crouching")]
        public float PelvisMaxOffset = .15f;

        [Tooltip("Pelvis offset based on crouch amount.")]
        public AnimationCurve PelvisOffset;

        [Tooltip("The hand will be limited to this length from the shoulder transforms")]
        public float DefaultArmLength = .75f;

        [Header("Turning")]
        public bool SmoothTurn = true;
        public float SmoothTurnSpeed = 90f;
        public float SmoothTurnThreshold = .1f;

        public float SnapTurnSpeed = 450f;
        public float SnapAmount = 45f;

        [Tooltip("Axis threshold to be considered valid for snap turning.")]
        public float SnapThreshold = .75f;

        [Header("Ground Checking")]

        [Tooltip("How far down to check for ground")]
        public float GroundedRayLength = .07f;

        [Tooltip("Assign layers that will decide if the player is grounded or not")]
        public LayerMask GroundedLayerMask;

        private static readonly float[] defaultCrouch;
        static HexaBodyPlayer4()
        {
            defaultCrouch = new[] { .25f, .5f, .75f, 1f };
        }

        [Header("Crouching (Percent of Pelvis to Ball Height)")]
        [Tooltip("Crouch levels are percentages of the WaistToBallHeight, defaults to 4 crouch levels with .25f added each level")]
        public float[] CrouchLevels = defaultCrouch;

        [Tooltip("Crouch % to stand up to when sprinting.")]
        public float RunningCrouchLevel;

        [Tooltip("Crouch % to stand up to when walking.")]
        public float WalkingCrouchLevel = .25f;

        [Tooltip("Max crouch while not grounded, higher values will make vaulting easier")]
        public float AirCrouchMaxHeight = 1.35f;

        [Tooltip("How far the camera has to go down before the player starts crouching")]
        public float CrouchThreshold;

        [Tooltip("Max Crouch / standing speed")]
        public float MaxCrouchSpeed = 2.2f;

        [Tooltip("Max crouch speed when not actively standing, crouching, or jumping")]
        public float PassiveCrouchSpeed = 1.5f;

        [Header("Physics Settings")]
        [Tooltip("Position Iterations applied to the Ball and Pelvis. Higher will lead to higher accuracy of body solving.")]
        public int SolverIterations = 6;

        [Tooltip("Velocity Iterations applied to the Ball and Pelvis. Higher will lead to higher accuracy of body solving.")]
        public int SolverVelocityIterations = 15;

        [Header("Required Transforms")]
        public HexaCameraRig CameraRig;
        public Transform Camera;
        public Transform NeckPivot;
        public Transform CameraScale;
        public Transform Neck;
        public Transform Bumper;
        public Transform Legs;
        public Transform Chest;
        public Transform ChestAnchor;
        public Transform LeftShoulder;
        public Transform RightShoulder;

        [Header("Debug Shapes")] public bool ShowShapes;
        public Transform ChestCapsule;
        public Transform KneeCapsule;
        public Transform PelvisCapsule;
        public Transform LocoSphere;
        public Transform FenderSphere;

        [Header("RigidBodies")]
        public Rigidbody LocoBall;

        public Rigidbody Pelvis;
        public Rigidbody Knee;
        public Rigidbody Head;
        public Rigidbody LeftHandRigidBody;
        public Rigidbody RightHandRigidBody;

        [Header("Colliders")]

        public CapsuleCollider KneeCollider;
        public CapsuleCollider ChestCollider;
        public SphereCollider LocoCollider;
        public SphereCollider FenderCollider;
        public CapsuleCollider NeckCollider;
        public CapsuleCollider PelvisCollider;

        [Header("Hands")]

        public HexaHandsBase LeftHand;
        public HexaHandsBase RightHand;

        [Header("Joints")]
        public ConfigurableJoint JointLegTorso;
        public ConfigurableJoint JointLegBall;
        public ConfigurableJoint JointHead;

        [Header("Tracking")]
        public Transform LeftController;
        public Transform RightController;

        [Header("------Debug-----")]
        [Tooltip("Height calibration is triggered once the HMD moves, if save calibration is enabled, any saved value will be used.")]
        public bool CalibrateHeightOnStart;

        [Tooltip("Calibration height is saved to player prefs when height is calibrated.")]
        public bool SaveCalibrationHeight;

        private float CalibratedHeight = 1.66f;
        private float SittingOffset;
        public float FloorOffsetAdjustment;

        public float ActualWaistHeight;
        public float ActualCrouchAmount;

        public float WaistToBallHeight;
        public float CrouchTarget;// { get; private set; }
        [SerializeField] private float _crouchAmount;

        private float TargetWaistHeight;
        private float FakeCrouchAmount;
        private float RealCrouchOffset;
        private float NeckBendOffset;

        //private[Header("Camera Offsets")]
        private float PelvisHeightOffset;
        private float RealCrouchHeightOffset;
        private float MinCameraHeightOffset;
        private float MaxCameraHeightOffset;
        private float HeadOffset;
        private float MinCameraHeight = .3f;

        [Header("Misc")]
        [SerializeField] private float _groundAngle;
        [SerializeField] private float _jumpTime;
        [SerializeField] private float _actualSpeed;
        [SerializeField] private float _standingPercent;
        private float crouchLimit = 1.0f;


        [Header("Velocities")]
        [SerializeField] private float _verticalSpeed;
        [SerializeField] private float _locoAngularVelocity;
        [SerializeField] private float _targetAngularVelocity;

        public float CrouchSpeed = 1.5f;

        private float _originalLocoRadius;
        private float _originalBumperRadius;

        private float _ballMass;
        private float _kneeMass;
        private float _headMass;
        private float _pelvisMass;

        private bool _jump;

        private float _scale = 1f;

        private float _timeSinceLastPress;
        private bool _awaitingSecondClick;

        private bool _previousShowShapes;
        protected LegStatus _legStatus;


        protected Vector3 goalVelocity;
        private Vector3 _acceleration;
        private Vector3 _cameraStartingPosition;
        private float _timeToPeak;
        private float _jumpCrouchStart;
        private float _cachedHeadHeight;

        private WaitForFixedUpdate _wffu;

        private float _headDrag;
        private float _pelvisDrag;
        private float _kneeDrag;
        private float _ballDrag;
        private float _dynamicFriction;
        private float _staticFriction;

        private bool _frictionOverriden;
        protected float crouchAmountAtJump;

        [SerializeField] private float _jumpTimer;

        private float _previousTurnAxis;
        private bool _turning;
        private ConfigurableJoint _leftArmJoint;
        private ConfigurableJoint _rightArmJoint;
        private LocoBallCollision _locoGrounder;
        private float _stompCounter;
        [SerializeField] private JumpStage _jumpStage;
        private bool _waitingForCameraMovement = true;
        private JointDrive _legYDrive;

        private bool _jumpMassApplied;
        private bool _jumpDriveApplied;
        public bool IsGrounded;

        public float BodyWeight => Mathf.Abs(Physics.gravity.y) * CombinedMass;
        private bool _airCrouch;
        private float StopAirCrouch = .8f;
        private float StartAirCrouch = .3f;

        public bool Sprinting { get; set; }

        public float TurnSpeed { get; private set; }

        public Vector3 HMDSpeed { get; private set; }

        public Quaternion GroundRotation { get; private set; }

        public HexaBodyInputsBase Inputs { get; private set; }

        public Vector3 Forward => DirectionTransform.forward;
        public Vector3 Right => DirectionTransform.right;

        public Transform DirectionTransform { get; set; }

        public float CrouchAmount
        {
            get
            {
                return _crouchAmount;
            }
            set
            {
                _crouchAmount = value;
            }
        }

        public bool WasGrounded { get; set; }

        public float Gravity => Mathf.Abs(Physics.gravity.y);

        public float MaxVerticalAcceleration => (Gravity * LowerMass / UpperMass + Gravity);

        public float MaxCrouch;



        public float CameraHeight => SittingOffset + FloorOffsetAdjustment + Camera.localPosition.y * _scale;

        public float EyeLevel => EyeHeight;

        public bool IsStanding => _legStatus == LegStatus.Standing;

        public bool IsJumpCycle => _legStatus == LegStatus.Jumping;

        /// <summary>
        /// If in the jumping stage of the jump cycle
        /// </summary>
        public bool IsJumping => IsJumpCycle && JumpStage == JumpStage.Jumping;

        public float LowerMass => Knee.mass + LocoBall.mass;

        public float CombinedMass => UpperMass + LowerMass;

        public float UpperMass => Pelvis.mass + Head.mass + LeftHandRigidBody.mass + RightHandRigidBody.mass;

        public float GravityOffset { get; set; }

        protected JumpStage JumpStage
        {
            get => _jumpStage;
            set
            {
                if (_jumpStage == JumpStage.Jumping && value != JumpStage.Jumping)
                    ResetYDrive();
                _jumpStage = value;
            }
        }

        protected virtual void Awake()
        {
            Inputs = GetComponent<HexaBodyInputsBase>();
            _originalLocoRadius = LocoCollider.radius;
            _originalBumperRadius = FenderCollider.radius;
            _wffu = new WaitForFixedUpdate();
            DirectionTransform = Camera;

            _cameraStartingPosition = Camera.localPosition;

            JointLegBall.slerpDrive = new JointDrive()
            {
                positionDamper = 1000000000f,
                maximumForce = 2000f
            };

            JointLegBall.rotationDriveMode = RotationDriveMode.Slerp;

            Pelvis.constraints = RigidbodyConstraints.FreezeRotation;
            Knee.constraints = RigidbodyConstraints.FreezeRotation;

            LocoBall.inertiaTensor = new Vector3(5f, 5f, 5f);
            Bumper.position = LocoBall.position + new Vector3(0, BumperOffset, 0f);

            //trying to prevent the loco ball from hitting walls if the joint stretches a bit due to high torque and friction
            FenderCollider.contactOffset = .01f;
            LocoCollider.contactOffset = .001f;
            JointLegBall.projectionDistance = .0001f;
            JointLegTorso.projectionDistance = .0001f;

            //prevent joint stretch from high forces bending the player off axis
            JointLegTorso.projectionAngle = 5f;

            //setting these so project settings don't need high default settings
            Pelvis.solverIterations = SolverIterations;
            Pelvis.solverVelocityIterations = SolverVelocityIterations;

            Knee.solverIterations = SolverIterations;
            Knee.solverVelocityIterations = SolverVelocityIterations;

            LocoBall.solverIterations = SolverIterations;
            LocoBall.solverVelocityIterations = SolverVelocityIterations;

            SetupShapes();
            SetArmLength(DefaultArmLength);
            //setting these so project settings don't need high default settings
            LocoBall.maxAngularVelocity = 100f;
            Pelvis.maxAngularVelocity = 100f;
            _dynamicFriction = LocoCollider.material.dynamicFriction;
            _staticFriction = LocoCollider.material.staticFriction;
            CacheMass();
            ValidateFields();
            CalibrateHeight(Camera.localPosition.y);
            //CalibrateHeight(0f);

            GravityOffset = UpperMass * Gravity / JointLegTorso.yDrive.positionSpring;

            if (!LocoBall.gameObject.TryGetComponent(out _locoGrounder))
            {
                _locoGrounder = LocoBall.gameObject.AddComponent<LocoBallCollision>();
            }
        }

        protected virtual void Start()
        {
            //in start as hands locate their controllers if not assigned in Awake()
            if (!LeftHand)
            {
                LeftHand = transform.root.GetComponentsInChildren<HexaHandsBase>().FirstOrDefault(e => e.IsLeft);
            }

            if (!RightHand)
            {
                RightHand = transform.root.GetComponentsInChildren<HexaHandsBase>().FirstOrDefault(e => !e.IsLeft);
            }
        }


        protected virtual void ValidateFields()
        {
            if (JumpMovementCurve.keys.Length == 0)
            {
                JumpMovementCurve.AddKey(0f, 0f);
                JumpMovementCurve.AddKey(1f, 1f);
            }

            if (JumpMovementDirectionCurve.keys.Length == 0)
            {
                JumpMovementDirectionCurve.AddKey(-1f, 0f);
                JumpMovementDirectionCurve.AddKey(0f, 0f);
                JumpMovementDirectionCurve.AddKey(1f, 1f);
            }

            if (PelvisOffset.keys.Length == 0)
            {
                PelvisOffset.AddKey(0f, 0f);
                PelvisOffset.AddKey(1f, 1f);
            }

            if (DirectionAcceleration.keys.Length == 0)
            {
                DirectionAcceleration.AddKey(-1f, 1f);
                DirectionAcceleration.AddKey(0f, 0f);
                DirectionAcceleration.AddKey(1f, 0f);
            }
        }

        private void OnValidate()
        {
            Knee.transform.position = LocoBall.transform.position;
        }

        public void CalibrateHeight(float height)
        {
            if (SitStanding == SitStand.Standing)
            {
                if (height < .01f)
                {
                    height = EyeHeight;
                }

                SittingOffset = 0f;
                _scale = EyeHeight / height;
            }
            else
            {
                SittingOffset = EyeHeight - height;
                _scale = 1f;
            }

            CalibratedHeight = height;
            CameraScale.localScale = new Vector3(_scale, _scale, _scale);
        }

        void Update()
        {
            if (_waitingForCameraMovement)
                CheckCameraMovement();


            ApplyCameraOffsets();
            CheckGrounded();
            CheckInputs();
            UpdateShapes();
            UpdateMaxCrouch();
            UpdateCrouchAmount();

            _locoAngularVelocity = LocoBall.angularVelocity.magnitude;

        }

        private void FixedUpdate()
        {
            CalcJumpHeight();

            _verticalSpeed = Pelvis.linearVelocity.y;
            var flat = Pelvis.linearVelocity;
            flat.y = 0f;
            _actualSpeed = flat.magnitude;

            ActualWaistHeight = Pelvis.transform.localPosition.y - LocoBall.transform.localPosition.y;
            WaistToBallHeight = PlayerWaistHeight - _originalLocoRadius;
            ActualCrouchAmount = WaistToBallHeight - ActualWaistHeight;
            _standingPercent = ActualWaistHeight / WaistToBallHeight;
            _standingPercent = Mathf.Clamp01(_standingPercent);

            CheckGrounded();
            StartJumping();
            //CheckMassUpdate();
            Move();
            Turn();
            UpdateBody();
            UpdateLegs();
            UpdateLegHeight();
            UpdateShoulderAnchors();
            WasGrounded = IsGrounded;
        }

        private void CalcJumpHeight()
        {
            if (SaveHeadHeight)
            {
                SaveHeadHeight = false;
                _cachedHeadHeight = Head.transform.position.y;
            }

            JumpHeightActual = Mathf.Max(JumpHeightActual, Head.transform.position.y - _cachedHeadHeight);
        }

        //protected virtual void CheckMassUpdate()
        //{
        //    if (_legStatus == LegStatus.Standing)
        //    {
        //        if (_ungroundedTime > .7f && !_jumpMassApplied)
        //        {
        //            Debug.Log($"jumpmass");
        //            ApplyJumpMass();
        //        }
        //        else if (_jumpMassApplied && _groundedTime > .7f)
        //        {
        //            Debug.Log($"groundmass");
        //            ResetMass();
        //        }
        //    }
        //}

        protected virtual bool CheckCameraMovement()
        {
            if (Vector3.Distance(_cameraStartingPosition, Camera.transform.localPosition) < .05f)
            {
                return false;
            }

            var delta = NeckPivot.transform.position - Pelvis.transform.position;
            delta.y = 0f;
            CameraRig.transform.position -= delta;
            _waitingForCameraMovement = false;
            CameraRig.CameraYOffset = 0f;

            if (CalibrateHeightOnStart)
            {
                if (SaveCalibrationHeight)
                    CalibrateFromSaved();
                else
                    Calibrate();
            }

            return true;
        }



        protected virtual void CalibrateFromSaved()
        {
            if (PlayerPrefs.HasKey(HeightKey))
            {
                var height = PlayerPrefs.GetFloat(HeightKey);
                CalibrateHeight(height);
            }
            else
            {
                Calibrate();
            }
        }

        private void UpdateLegs()
        {
            if (IsJumpCycle)
            {
                switch (JumpStage)
                {
                    case JumpStage.Jumping:
                        Jumping();
                        break;
                    case JumpStage.Retracting:
                        Retracting();
                        break;
                    case JumpStage.Landing:
                    case JumpStage.LandingGrounded:
                        Landing();
                        break;
                    case JumpStage.Stomping:
                        Stomping();
                        break;
                }
            }
        }



        protected virtual void UpdateMaxCrouch()
        {
            var canAirCrouch = GetCanAirCrouch();
            var target = 0f;

            var ratio = _locoGrounder.AverageBodyWeight / BodyWeight;

            if (_airCrouch && ratio > StopAirCrouch || Inputs.CrouchRate < .2f)
            {
                _airCrouch = false;
            }
            else if (!_airCrouch)
            {
                _airCrouch = canAirCrouch;
            }

            if (!_airCrouch)
            {
                if (CrouchLevels.Length > 0)
                {
                    target = WaistToBallHeight * CrouchLevels[CrouchLevels.Length - 1];
                }
            }
            else
            {
                _airCrouch = true;
                target = WaistToBallHeight * this.AirCrouchMaxHeight;
            }

            MaxCrouch = Mathf.MoveTowards(MaxCrouch, target, canAirCrouch ? 8f : 2f * Time.deltaTime);
        }

        protected virtual void UpdateCrouchAmount()
        {
            if (IsJumpCycle)
            {
                if (Inputs.CrouchState.JustActivated)
                {
                    StopJumpCycle();
                }
                else if (Inputs.JumpState.JustActivated)
                {
                    //does jumping stop the jump stage??
                    if (JumpStage == JumpStage.Retracting || JumpStage == JumpStage.Landing || JumpStage == JumpStage.LandingGrounded || JumpStage == JumpStage.Jumping)
                    {
                        Stand();

                        if (JumpStage == JumpStage.LandingGrounded)
                        {
                            StopJumpCycle();
                        }
                        else
                        {
                            JumpStage = JumpStage.Stomping;
                            _stompCounter = 0f;
                            ScaleBalls(false);
                        }

                        CrouchSpeed = 0f;
                    }

                    // Debug.Log($"Jumping {JumpStage}");
                }
            }
            else
            {
                //if (Inputs.JumpState.JustActivated)
                // Debug.Log($"standing {JumpStage}");
            }



            //boneworks style leg logic
            //holding jump crouches you further from your current level
            //crouching moves down / up based on track pad / joystick rate and sets closest crouch level if you stop
            //sprinting forces you to stand if you aren't holding down jump or crouch
            //walking moves you to first crouch level if you are moving and crouching lower than the first level

            if (Inputs.JumpState.Active)
            {
                CrouchSpeed = Mathf.MoveTowards(CrouchSpeed, MaxCrouchSpeed, MaxVerticalAcceleration * Time.deltaTime);
                CrouchSpeed = Mathf.Clamp(CrouchSpeed, -MaxCrouchSpeed, MaxCrouchSpeed);

                var speed = CrouchSpeed;
                if (CrouchAmount > CrouchTarget + JumpCrouchAmount)
                {
                    speed = PassiveCrouchSpeed;
                }

                CrouchAmount = Mathf.MoveTowards(CrouchAmount, CrouchTarget + JumpCrouchAmount, Time.deltaTime * speed);

                if (IsJumpCycle && JumpStage == JumpStage.Stomping)
                {
                    _stompCounter += Time.deltaTime;
                    if (_stompCounter > .15f)
                    {
                        StopJumpCycle();
                        CrouchTarget = GetCrouchTarget(CrouchAmount);
                        crouchLimit = CrouchTarget;
                    }
                }
            }
            else if (Inputs.CrouchState.Active && (IsStanding || (IsJumpCycle && !IsJumping)))
            {
                float goal;
                float sign = 1f;
                float factor = 1f;

                if (Inputs.CrouchRate < 0f) //standing up
                {
                    goal = -TipToesOffset;
                    sign = -1f;
                    if (CrouchSpeed > 0)
                    {
                        //rapidly changing speeds from crouch to stand for response time
                        CrouchSpeed = 0f;
                    }

                    //slowing down tip toe a bit to prevent rapid stand mini hops
                    if (ActualWaistHeight > WaistToBallHeight * .9f)
                    {
                        factor = 1f - Mathf.InverseLerp(WaistToBallHeight * .9f, WaistToBallHeight + TipToesOffset, ActualWaistHeight);
                        factor = Mathf.Clamp(factor, .3f, 1f);
                    }
                }
                else //crouching
                {
                    goal = MaxCrouch;
                    if (CrouchSpeed < 0)
                    {
                        if (!IsGrounded)
                        {
                            CrouchSpeed = 0f;
                        }
                        else
                        {
                            //can uncomment this if you want to rapidly crouch after standing - it will lead to a small hop off the floor from the rapid change in velocity though
                            CrouchSpeed *= .5f;
                        }
                    }
                }

                CrouchSpeed = Mathf.MoveTowards(CrouchSpeed, sign * MaxCrouchSpeed, MaxVerticalAcceleration * Time.deltaTime);
                CrouchAmount = Mathf.MoveTowards(CrouchAmount, goal, Inputs.CrouchRate * Time.deltaTime * CrouchSpeed * factor);
            }
            else if (IsJumpCycle)
            {
                if (JumpStage == JumpStage.Jumping)
                {
                    CrouchAmount = Mathf.MoveTowards(CrouchAmount, CrouchTarget, Time.deltaTime * CrouchSpeed);
                }
                else if (JumpStage == JumpStage.Stomping)
                {
                    CrouchAmount = Mathf.MoveTowards(CrouchAmount, CrouchTarget, Time.deltaTime * StompSpeed);
                }
            }
            else
            {
                CrouchSpeed = Mathf.MoveTowards(CrouchSpeed, 0f, MaxVerticalAcceleration * Time.deltaTime);
                CrouchAmount = Mathf.MoveTowards(CrouchAmount, CrouchTarget, Time.deltaTime * PassiveCrouchSpeed);
            }

            UpdateCrouchTarget();
        }



        protected virtual void UpdateCrouchTarget()
        {
            if (!Inputs.JumpState.Active && !Inputs.CrouchState.Active)
            {
                var limit = Sprinting ? RunningCrouchLevel : WalkingCrouchLevel;
                var target = Mathf.Lerp(MaxCrouch, GetCrouchLevel(limit), Inputs.MovementMagnitude);

                crouchLimit = Mathf.MoveTowards(crouchLimit, target, PassiveCrouchSpeed * Time.deltaTime);

                if (CrouchTarget < crouchLimit)
                    crouchLimit = CrouchTarget;
            }
            else
            {
                crouchLimit = MaxCrouch;
            }

            if (Inputs.JumpState.JustActivated && JumpStage != JumpStage.Stomping)
            {
                CrouchTarget = GetCrouchTarget(CrouchAmount);
                crouchLimit = CrouchTarget;
            }

            if (!IsJumpCycle && !Inputs.JumpPressed) // && IsGrounded || !IsGrounded && Inputs.CrouchState.JustDeactivated)
            {
                if (CrouchLevels.Length <= 0) return;

                if (Inputs.MovementMagnitude < .05f)
                {
                    CrouchTarget = GetCrouchTarget(CrouchAmount);
                }
                else
                {

                    if (CrouchTarget > crouchLimit)
                    {
                        CrouchTarget = crouchLimit;
                    }
                }
            }
        }

        private float GetCrouchTarget(float crouchAmount)
        {
            var previousCrouchLevel = 0f;

            if (CrouchLevels.Length > 0)
            {
                var mid = GetCrouchLevel(CrouchLevels[0]) * .45f;
                if (crouchAmount <= mid)
                {
                    return 0f;
                }
            }
            else
            {
                return 0f;
            }

            for (var i = 0; i < CrouchLevels.Length; i++)
            {
                var crouchLevel = GetCrouchLevel(CrouchLevels[i]);
                var delta = (crouchLevel - previousCrouchLevel) * .5f;
                var lowerBound = crouchLevel - delta;
                var upperBound = crouchLevel + delta;

                if (crouchAmount >= lowerBound && crouchAmount < upperBound)
                {
                    return crouchLevel;
                }

                previousCrouchLevel = crouchLevel;
            }

            return CrouchTarget;
        }

        protected virtual void StopJumpCycle()
        {
            _legStatus = LegStatus.Standing;
            JumpStage = JumpStage.Jumping;
            ScaleBalls(false);
            ResetMass();
            ResetDrag();
            ResetYDrive();
            Stand();
        }




        public virtual void ApplyJumpDrive()
        {
            if (_jumpDriveApplied)
                return;
            _jumpDriveApplied = true;
            _legYDrive = JointLegTorso.yDrive;
            var drive = JointLegTorso.yDrive;
            drive.positionSpring = 0f;
            drive.positionDamper = JumpDamper;
            JointLegTorso.yDrive = drive;
        }

        public virtual void ResetYDrive()
        {
            _jumpDriveApplied = false;
            JointLegTorso.yDrive = _legYDrive;
        }

        private void StartJumping()
        {
            if (_jump)
            {
                ApplyJumpDrive();
                //StopJumping();
                RemoveDrag();
                _jumpTimer = 0f;
                _jump = false;
                _legStatus = LegStatus.Jumping;
                JumpStage = JumpStage.Jumping;

                //CrouchTarget = -TipToesOffset;
                CrouchTarget = -.2f; // hard coding incase user doesn't have tip toe set

                crouchAmountAtJump = 1 - ActualWaistHeight / WaistToBallHeight;
                JumpHeightGoal = JumpHeightCurve.Evaluate(crouchAmountAtJump);

                CrouchSpeed = Kinematics.GetJumpVelocity(Gravity, JumpHeightGoal) - Gravity * Time.fixedDeltaTime;

                JumpVelGoal = CrouchSpeed;
            }
        }

        protected virtual void Jumping()
        {
            var locoGrounderJustUncollided = _locoGrounder.Collision.JustUncollided && _jumpTimer > .1f;
            var tiptoeing = ActualCrouchAmount < CrouchTarget;

            //if (locoGrounderJustUncollided) Debug.Log($"uncollided");
            //if (b) Debug.Log($"actual < target   ");

            if (locoGrounderJustUncollided || tiptoeing)
            {
                JumpHeightActual = -10f;
                JumpVelActual = Pelvis.linearVelocity.y;
                StartRetracting();
                ApplyJumpMass();
            }
            else
            {
                if (_jumpTimer > JumpTimeout)
                {
                    Debug.Log($"liftoff failed!");
                    StopJumpCycle();
                    CrouchSpeed = 0f;
                    return;
                }
            }

            _jumpTimer += Time.deltaTime;
        }

        protected virtual void StartRetracting()
        {
            JumpStage = JumpStage.Retracting;
            CrouchSpeed = 0f;
            _timeToPeak = Kinematics.SolveTime(Pelvis.linearVelocity.y - Gravity * Time.deltaTime * Time.deltaTime, Gravity);

            _jumpTimer = 0f;
            _jumpCrouchStart = CrouchAmount;
            ScaleBalls(true);
            ResetDrag();
            JointLegTorso.yDrive = _legYDrive;
        }

        protected virtual void Retracting()
        {
            var percent = _jumpTimer / _timeToPeak;
            var curve = RetractCurve.Evaluate(percent);
            CrouchAmount = Mathf.Lerp(_jumpCrouchStart, WaistToBallHeight * RetractLevel, curve);

            _jumpTimer += Time.deltaTime;

            if (Pelvis.linearVelocity.y < 0)
            {
                if (_jumpTimer < _timeToPeak)
                {
                    _timeToPeak = _jumpTimer;
                }

                ScaleBalls(false);
                JumpStage = JumpStage.Landing;
                _jumpCrouchStart = CrouchAmount;
                _jumpTimer = 0f;
                Stand();
            }
        }

        protected virtual void Landing()
        {
            if (JumpStage == JumpStage.Landing && IsGrounded)
            {
                JumpStage = JumpStage.LandingGrounded;
                ResetMass();
            }

            if (JumpStage == JumpStage.LandingGrounded)
            {
                CrouchAmount = Mathf.MoveTowards(CrouchAmount, CrouchTarget, Time.deltaTime * PassiveCrouchSpeed);
            }
            else
            {
                var percent = _jumpTimer / _timeToPeak;
                var curve = StompCurve.Evaluate(percent);
                CrouchAmount = Mathf.Lerp(_jumpCrouchStart, 0, curve);
            }

            if (_jumpTimer / _timeToPeak > 1.7f || ActualWaistHeight >= WaistToBallHeight)
            {
                StopJumpCycle();
            }

            _jumpTimer += Time.deltaTime;
        }

        protected virtual void Stomping()
        {
            if (IsGrounded)
            {
                StopJumpCycle();
                CrouchSpeed = 0f;
            }
        }


        protected virtual bool GetCanAirCrouch()
        {
            if (!LeftHand || !RightHand)
                return false;

            return _locoGrounder.AverageBodyWeight / BodyWeight < StartAirCrouch &&
                   (LeftHand.Collision.Average(.25f, Vector3.zero, Vector3.up) / BodyWeight > .2f ||
                    RightHand.Collision.Average(.25f, Vector3.zero, Vector3.up) / BodyWeight > .2f);
        }

        private float _min;

        private void UpdateLegHeight()
        {
            var delta = Camera.position.y - NeckPivot.position.y;
            NeckBendOffset = NeckPivot.localPosition.y + delta;

            RealCrouchOffset = EyeLevel - CameraHeight - CrouchThreshold + NeckBendOffset;
            RealCrouchOffset = Mathf.Clamp(RealCrouchOffset, -TipToesOffset, WaistToBallHeight * AirCrouchMaxHeight);

            FakeCrouchAmount = CrouchAmount;
            FakeCrouchAmount = Mathf.Clamp(FakeCrouchAmount, -TipToesOffset, WaistToBallHeight * AirCrouchMaxHeight);

            TargetWaistHeight = WaistToBallHeight - FakeCrouchAmount - RealCrouchOffset;

            float minTarget;
            float speed;

            if (_airCrouch && AirCrouchMaxHeight > 1f)
            {
                minTarget = -WaistToBallHeight * (AirCrouchMaxHeight - 1f);
                speed = 8f;
            }
            else
            {
                minTarget = 0f;
                speed = 2f;
            }

            //smooth out max crouch from air lifting
            _min = Mathf.MoveTowards(_min, minTarget, speed * Time.deltaTime);

            TargetWaistHeight = Mathf.Clamp(TargetWaistHeight, _min, WaistToBallHeight + TipToesOffset);

            var crouchAmount = WaistToBallHeight - TargetWaistHeight;

            var anchor = (WaistToBallHeight + TipToesOffset) * .5f;

            if (IsGrounded && _min > -0.0001f)
            {
                JointLegTorso.yMotion = ConfigurableJointMotion.Limited;
            }
            else
            {
                JointLegTorso.yMotion = ConfigurableJointMotion.Free;
            }

            var limit = JointLegTorso.linearLimit;
            limit.bounciness = 0f;
            limit.contactDistance = .001f;
            limit.limit = (WaistToBallHeight * AirCrouchMaxHeight + TipToesOffset) * .5f;
            JointLegTorso.linearLimit = limit;
            JointLegTorso.anchor = new Vector3(0f, -anchor, 0f);

            var previousPosition = JointLegTorso.targetPosition;
            ////JointLegTorso.targetPosition = Vector3.down * (WaistToBallHeight - TargetWaistHeight) + Vector3.up * (GravityOffset + WaistToBallHeight * .5f);
            JointLegTorso.targetPosition = Vector3.up * (crouchAmount - GravityOffset - WaistToBallHeight * .5f + TipToesOffset * .5f);

            if (IsJumping)
            {
                JointLegTorso.targetVelocity = new Vector3(0f, -CrouchSpeed, 0f);
            }
            else
            {
                JointLegTorso.targetVelocity = (JointLegTorso.targetPosition - previousPosition) / Time.deltaTime;
            }
        }

        protected void Stand()
        {
            CrouchTarget = 0f;
        }

        /// <summary>
        /// Sets the crouch level by the CrouchLevels array index
        /// </summary>
        /// <param name="level"></param>
        public void SetCrouchLevel(int level)
        {
            if (level >= 0 && level < CrouchLevels.Length)
            {
                CrouchTarget = GetCrouchLevel(CrouchLevels[level]);
            }
        }

        /// <summary>
        /// Sets crouch level by crouch %
        /// </summary>
        /// <param name="percent"></param>
        public void SetCrouchPercent(float percent)
        {
            CrouchTarget = GetCrouchLevel(percent);
        }

        /// <summary>
        /// Gets crouch amount by crouch %
        /// </summary>
        /// <param name="crouchPercent"></param>
        /// <returns></returns>
        public float GetCrouchLevel(float crouchPercent)
        {
            return crouchPercent * WaistToBallHeight;
        }

        protected virtual void ApplyCameraOffsets()
        {
            PelvisHeightOffset = -PlayerWaistHeight;
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

        protected virtual void UpdateBody()
        {
            var pelvisOffset = GetPelvisOffset();

            var dir = -Camera.forward;
            dir.y = 0f;
            dir.Normalize();
            var localDir = Pelvis.transform.InverseTransformDirection(dir);

            var pelvisTarget = Pelvis.transform.InverseTransformDirection(dir * pelvisOffset);
            pelvisTarget.y = Mathf.Max(LocoBall.position.y - Pelvis.position.y, 0f);
            PelvisCollider.center = Vector3.MoveTowards(PelvisCollider.center, pelvisTarget, Time.deltaTime);

            UpdateLegCollider();
            UpdateChest(localDir, dir);
            UpdateNeck();
            UpdateHead();
        }

        protected virtual void UpdateNeck()
        {
            Neck.position = Chest.position + (Head.position - Chest.position) / 2f;
            NeckCollider.height = Vector3.Distance(Head.position, Chest.position);
            var target = Head.position - Neck.position;
            var rotation = Quaternion.FromToRotation(Neck.up, target);
            Neck.transform.rotation = rotation * Neck.transform.rotation;
        }

        protected virtual void UpdateChest(Vector3 localDir, Vector3 dir)
        {
            var chestAnchorLocal = ChestAnchor.localPosition;
            chestAnchorLocal = Quaternion.FromToRotation(chestAnchorLocal, localDir) * chestAnchorLocal;

            var chestAnchorPosition = Pelvis.transform.TransformPoint(chestAnchorLocal) + dir * PelvisCollider.center.magnitude;
            var target = NeckPivot.position - chestAnchorPosition;
            var rotation = Quaternion.FromToRotation(Chest.transform.up, target);
            Chest.transform.rotation = rotation * Chest.transform.rotation;
            ChestCollider.height = Vector3.Distance(NeckPivot.position, ChestAnchor.position);

            Chest.position = chestAnchorPosition + (NeckPivot.position - chestAnchorPosition) / 2f;
        }

        protected virtual void UpdateLegCollider()
        {
            var pelvisPos = GetPelvisPosition();

            var target = pelvisPos - LocoBall.position;
            var rotation = Quaternion.FromToRotation(Legs.transform.up, target);
            Legs.transform.rotation = rotation * Legs.transform.rotation;
            KneeCollider.height = Vector3.Distance(pelvisPos, LocoBall.position);
            Legs.transform.position = LocoBall.position + (pelvisPos - LocoBall.position) / 2f;
        }

        private void UpdateHead()
        {
            HeadOffset = EyeLevel - PlayerWaistHeight;

            var dir = Camera.position - Pelvis.transform.position;
            dir.Normalize();
            var anchor = Pelvis.transform.TransformDirection(new Vector3(0, HeadOffset, -NeckPivot.localPosition.z));

            anchor = Quaternion.FromToRotation(anchor, dir) * anchor;
            JointHead.connectedAnchor = Pelvis.transform.InverseTransformDirection(anchor);
        }

        protected virtual void Turn()
        {
            if (SmoothTurn)
            {
                ProcessSmoothTurn();
            }
            else if (!_turning)
            {
                ProcessSnapTurn();
            }
        }



        protected virtual void ProcessSmoothTurn()
        {
            if (Math.Abs(Inputs.TurnAxis.x) > SmoothTurnThreshold)
            {
                TurnSpeed = SmoothTurnSpeed * Inputs.TurnAxis.x * Time.deltaTime;
                //Pelvis.MoveRotation(Pelvis.rotation * Quaternion.AngleAxis(TurnSpeed, Vector3.up));
                Pelvis.transform.RotateAround(LocoBall.transform.position, Vector3.up, TurnSpeed);
            }
            else
            {
                TurnSpeed = 0f;
            }
        }

        protected virtual void ProcessSnapTurn()
        {
            var input = Inputs.TurnAxis.x;
            if (Math.Abs(input) >= SnapThreshold && Mathf.Abs(_previousTurnAxis) < SnapThreshold)
            {
                StartCoroutine(SnapTurn(Mathf.Sign(input)));
            }
            _previousTurnAxis = input;
        }

        protected virtual IEnumerator SnapTurn(float sign)
        {
            _turning = true;

            var rotation = Quaternion.Euler(0, sign * SnapAmount, 0);
            var finish = Pelvis.transform.rotation * rotation;
            var elapsed = 0f;
            var time = SnapAmount / SnapTurnSpeed;

            while (elapsed < time)
            {

                var angle = Quaternion.Angle(Pelvis.transform.rotation, finish);
                if (angle < SnapTurnSpeed * Time.fixedDeltaTime)
                    TurnSpeed = sign * angle;
                else
                    TurnSpeed = sign * SnapTurnSpeed * Time.fixedDeltaTime;
                //Pelvis.MoveRotation(Quaternion.RotateTowards(Pelvis.transform.rotation, finish, SnapTurnSpeed * Time.fixedDeltaTime));
                Pelvis.transform.rotation = Quaternion.RotateTowards(Pelvis.transform.rotation, finish, SnapTurnSpeed * Time.deltaTime);
                yield return _wffu;
                elapsed += Time.fixedDeltaTime;
            }

            TurnSpeed = 0f;
            _turning = false;
        }


        private void CheckInputs()
        {
            if (Inputs.RecalibratePressed)
            {
                Calibrate();
            }

            CheckSprinting();

            if (Inputs.JumpState.JustDeactivated && (_legStatus == LegStatus.Standing || JumpStage == JumpStage.LandingGrounded || IsGrounded))
            {
                _jump = true;
                JumpStage = JumpStage.Jumping;
                _legStatus = LegStatus.Jumping;
            }
        }

        private bool HandleHMDMovement()
        {
            var delta = NeckPivot.transform.position - Pelvis.transform.position;
            delta.y = 0f;

            var magnitude = delta.magnitude;
            if (magnitude > CameraMoveThreshold)
            {
                var moveDelta = GroundRotation * delta;
                var actualDelta = Move(moveDelta);
                actualDelta.y = 0f;
                HMDSpeed = actualDelta / Time.fixedDeltaTime;
                CameraRig.transform.localPosition -= Pelvis.transform.InverseTransformDirection(delta);
            }
            else
            {
                HMDSpeed = Vector3.zero;
            }

            return magnitude > CameraMoveThreshold;
        }

        protected virtual void Move()
        {
            var movement = Inputs.MovementAxis;
            var direction = Forward * movement.y + Right * movement.x;
            var hasInput = movement.sqrMagnitude > .1f;

            GroundMove(direction, hasInput);

            if (!IsGrounded && hasInput)
                AirMove(direction);

            if (!_waitingForCameraMovement)
                HandleHMDMovement();
        }

        protected virtual void AirMove(Vector3 direction)
        {
            AddVelocity(direction * AirAcceleration * Time.deltaTime);
        }

        protected virtual void GroundMove(Vector3 direction, bool hasInput)
        {
            float dot;

            if (hasInput)
            {
                dot = Vector3.Dot(direction, goalVelocity.normalized);
            }
            else
            {
                dot = -1;
            }

            var dotAccelFactor = DirectionAcceleration.Evaluate(dot);

            var jumpBonus = Vector3.zero;
            var jumpBonusMag = 0f;
            if (IsJumping && JumpMovementBonus > 0f)
            {
                jumpBonus = GetJumpMoveVelocity(direction, hasInput);
                jumpBonusMag = jumpBonus.magnitude;
            }

            var maxSpeed = (Sprinting ? RunSpeed : WalkSpeed) * CrouchSpeedCurve.Evaluate(_standingPercent) + jumpBonusMag;
            if (_airCrouch) maxSpeed = WalkSpeed;
            var groundAngle = Mathf.Clamp(_locoGrounder.GroundAngle, 0, 90);
            var groundPercent = groundAngle / 90f;
            var slopeFactor = SlopeMaxSpeedCurve.Evaluate(groundPercent);
            var frictionFactor = SlopeFrictionCurve.Evaluate(groundPercent);

            ApplyFrictionFactor(frictionFactor);

            maxSpeed *= slopeFactor;

            goalVelocity = Vector3.SmoothDamp(goalVelocity, maxSpeed * direction, ref _acceleration, SmoothTime, MaxAcceleration * dotAccelFactor);


            var axis = Vector3.Cross(Vector3.up, goalVelocity.normalized).normalized;


            //LocoBall.angularVelocity = _goalVelocity.magnitude / LocoCollider.radius * axis;

            var targAV = goalVelocity.magnitude / LocoCollider.radius * axis;
            JointLegBall.targetAngularVelocity = -LocoBall.transform.InverseTransformDirection(targAV + jumpBonus);

            _targetAngularVelocity = JointLegBall.targetAngularVelocity.magnitude;
        }

        protected virtual Vector3 GetJumpMoveVelocity(Vector3 direction, bool hasInput)
        {
            if (!hasInput)
            {
                return Vector3.zero;
            }

            var dot = Vector3.Dot(direction, Forward);
            var bonus = Sprinting ? JumpSprintBonus : JumpMovementBonus;
            var speed = JumpMovementCurve.Evaluate(crouchAmountAtJump) * JumpMovementDirectionCurve.Evaluate(dot) * bonus;
            var jumpAxis = Vector3.Cross(Vector3.up, direction).normalized;
            return speed / LocoCollider.radius * jumpAxis;
        }

        protected virtual void CheckGrounded()
        {
            var radius = LocoCollider.radius * .95f;

            var startPos = LocoBall.position + Vector3.down * LocoCollider.radius + Vector3.up * (LocoCollider.contactOffset + radius);

            IsGrounded = Physics.SphereCast(
                startPos,
                radius,
                Vector3.down,
                out var h,
                GroundedRayLength,
                GroundedLayerMask, QueryTriggerInteraction.Ignore);

            if (IsGrounded)
            {
                _groundAngle = Vector3.Angle(h.normal, Vector3.up);
                GroundRotation = Quaternion.FromToRotation(Vector3.up, h.normal);
            }
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

            if (Inputs.MovementMagnitude < .01f)
            {
                Sprinting = false;
            }
        }


        public void SetSitStandMode(bool sitting)
        {
            SitStanding = sitting ? SitStand.Sitting : SitStand.Standing;
            Calibrate();
        }

        /// <summary>
        /// Calibrates the height based on the Camera's Y position.
        /// </summary>
        public void Calibrate()
        {
            CalibrateHeight(Camera.localPosition.y);
            if (SaveCalibrationHeight)
            {
                PlayerPrefs.SetFloat(HeightKey, Camera.localPosition.y);
                PlayerPrefs.Save();
            }
        }
        //todo add safe flatten versions...
        /// <summary>
        /// Rotates the pelvis so that the camera is facing in the desired input direction
        /// </summary>
        public void FaceDirection(Vector3 forward)
        {
            var euler = Camera.rotation.eulerAngles;
            euler.x = 0f;
            Camera.rotation = Quaternion.Euler(euler);
            Pelvis.rotation = Quaternion.FromToRotation(Camera.forward, forward) * Pelvis.rotation;
        }


        public virtual void NormalizeVelocity()
        {
            LocoBall.linearVelocity = Pelvis.linearVelocity;
            Knee.linearVelocity = Pelvis.linearVelocity;
            Head.linearVelocity = Pelvis.linearVelocity;
        }

        public void RemoveDrag()
        {
            _headDrag = Head.linearDamping;
            _pelvisDrag = Pelvis.linearDamping;
            _kneeDrag = Knee.linearDamping;
            _ballDrag = LocoBall.linearDamping;

            Pelvis.linearDamping = 0f;
            Head.linearDamping = 0f;
            Knee.linearDamping = 0f;
            LocoBall.linearDamping = 0f;
        }

        public void ResetDrag()
        {
            Pelvis.linearDamping = _pelvisDrag;
            Head.linearDamping = _headDrag;
            Knee.linearDamping = _kneeDrag;
            LocoBall.linearDamping = _ballDrag;
        }


        private void UpdateShoulderAnchors()
        {
            if (_leftArmJoint)
            {
                _leftArmJoint.anchor = Pelvis.transform.InverseTransformPoint(LeftShoulder.position);
            }

            if (_rightArmJoint)
            {
                _rightArmJoint.anchor = Pelvis.transform.InverseTransformPoint(RightShoulder.position);
            }
        }

        protected virtual Vector3 GetPelvisPosition()
        {
            var pelvisPos = Pelvis.transform.TransformPoint(PelvisCollider.center);
            return pelvisPos;
        }

        protected virtual float GetPelvisOffset()
        {
            var PelvisTargetOffset = PelvisOffset.Evaluate(_standingPercent) * PelvisMaxOffset;
            return PelvisTargetOffset;
        }


        protected virtual void ScaleBalls(bool shrink)
        {
            if (shrink)
            {
                LocoCollider.radius = KneeCollider.radius * .8f;
                FenderCollider.radius = KneeCollider.radius * .9f;
            }
            else
            {
                LocoCollider.radius = _originalLocoRadius;
                FenderCollider.radius = _originalBumperRadius;
            }
        }

        public void SetArmLength(float armLength)
        {
            if (LeftShoulder && LeftHandRigidBody)
            {
                if (!_leftArmJoint)
                {
                    _leftArmJoint = Pelvis.gameObject.AddComponent<ConfigurableJoint>();
                    _leftArmJoint.autoConfigureConnectedAnchor = false;
                    _leftArmJoint.connectedAnchor = Vector3.zero;
                    _leftArmJoint.connectedBody = LeftHandRigidBody;
                    _leftArmJoint.anchor = Pelvis.transform.InverseTransformPoint(LeftShoulder.position);
                    _leftArmJoint.xMotion = ConfigurableJointMotion.Limited;
                    _leftArmJoint.yMotion = ConfigurableJointMotion.Limited;
                    _leftArmJoint.zMotion = ConfigurableJointMotion.Limited;
                }

                var limit = _leftArmJoint.linearLimit;
                limit.limit = armLength;
                _leftArmJoint.linearLimit = limit;

            }

            if (RightShoulder && RightHandRigidBody)
            {
                if (!_rightArmJoint)
                {
                    _rightArmJoint = Pelvis.gameObject.AddComponent<ConfigurableJoint>();

                    _rightArmJoint.autoConfigureConnectedAnchor = false;
                    _rightArmJoint.connectedAnchor = Vector3.zero;
                    _rightArmJoint.connectedBody = RightHandRigidBody;
                    _rightArmJoint.anchor = Pelvis.transform.InverseTransformPoint(RightShoulder.position);
                    _rightArmJoint.xMotion = ConfigurableJointMotion.Limited;
                    _rightArmJoint.yMotion = ConfigurableJointMotion.Limited;
                    _rightArmJoint.zMotion = ConfigurableJointMotion.Limited;
                }


                var limit = _rightArmJoint.linearLimit;
                limit.limit = armLength;
                _rightArmJoint.linearLimit = limit;

            }
        }

        private void CheckShapes()
        {
            if (_previousShowShapes != ShowShapes)
            {
                SetupShapes();
                _previousShowShapes = ShowShapes;
            }
        }

        private void UpdateShapes()
        {
            CheckShapes();

            if (ShowShapes)
            {
                var pelvisPos = Pelvis.transform.TransformPoint(PelvisCollider.center);
                //KneeCapsule.position = Knee.position + KneeCollider.center;
                var pos = KneeCapsule.localPosition;
                pos.z = 0f;
                //KneeCapsule.localPosition = pos;
                //KneeCapsule.rotation = Legs.transform.rotation;
                PelvisCapsule.position = pelvisPos;

                var scale = ChestCapsule.localScale;
                scale.y = ChestCollider.height * .5f;
                scale.x = scale.z = ChestCollider.radius * 2f;
                ChestCapsule.localScale = scale;

                scale = KneeCapsule.localScale;
                scale.y = KneeCollider.height * .5f;
                scale.x = scale.z = KneeCollider.radius * 2f;
                KneeCapsule.localScale = scale;

                scale = PelvisCapsule.localScale;
                scale.y = PelvisCollider.height * .5f;
                scale.x = scale.z = PelvisCollider.radius * 2f;
                PelvisCapsule.localScale = scale;

                scale = FenderSphere.localScale;
                scale.x = scale.y = scale.z = FenderCollider.radius * 2f;
                FenderSphere.localScale = scale;

                scale = LocoSphere.localScale;
                scale.x = scale.y = scale.z = LocoCollider.radius * 2f;
                LocoSphere.localScale = scale;
            }
        }

        private void SetupShapes()
        {
            PelvisCapsule.gameObject.SetActive(ShowShapes);
            KneeCapsule.gameObject.SetActive(ShowShapes);
            ChestCapsule.gameObject.SetActive(ShowShapes);
            LocoSphere.gameObject.SetActive(ShowShapes);
            FenderSphere.gameObject.SetActive(ShowShapes);
        }

        /// <summary>
        /// Moves the entire rig, the input parameter is where the bottom of the ball will move to
        /// </summary>
        public void MoveToPosition(Vector3 locoBallBottom)
        {
            var offsetHead = Head.transform.position - LocoBall.transform.position;
            var offsetTorso = Pelvis.transform.position - LocoBall.transform.position;
            var offsetKnee = Knee.transform.position - LocoBall.transform.position;
            var offsetLeft = LeftHandRigidBody.transform.position - LocoBall.transform.position;
            var offsetRight = RightHandRigidBody.transform.position - LocoBall.transform.position;

            LocoBall.transform.position = locoBallBottom + new Vector3(0, LocoCollider.radius, 0f);
            LocoBall.position = LocoBall.transform.position;

            Head.transform.position = Head.position = LocoBall.transform.position + offsetHead;
            Pelvis.transform.position = Pelvis.position = LocoBall.transform.position + offsetTorso;
            Knee.transform.position = Knee.position = LocoBall.transform.position + offsetKnee;
            LeftHandRigidBody.transform.position = LeftHandRigidBody.position = LocoBall.transform.position + offsetLeft;
            RightHandRigidBody.transform.position = RightHandRigidBody.position = LocoBall.transform.position + offsetRight;

            Stop();
        }

        public void Stop()
        {
            LocoBall.linearVelocity = Head.linearVelocity = Pelvis.linearVelocity = Knee.linearVelocity = Vector3.zero;
            LeftHandRigidBody.linearVelocity = Vector3.zero;
            RightHandRigidBody.linearVelocity = Vector3.zero;
            LocoBall.angularVelocity = Vector3.zero;
        }


        public virtual void ApplyJumpMass()
        {
            if (_jumpMassApplied)
                return;

            var total = Pelvis.mass + Head.mass + Knee.mass + LocoBall.mass;

            var lower = total * .05f;
            var upper = total * .95f;

            CacheMass();

            Pelvis.mass = upper * .85f;
            Head.mass = upper * .15f;
            Knee.mass = lower * .5f;
            LocoBall.mass = lower * .5f;

            _jumpMassApplied = true;
        }

        private void CacheMass()
        {
            _headMass = Head.mass;
            _pelvisMass = Pelvis.mass;
            _kneeMass = Knee.mass;
            _ballMass = LocoBall.mass;
        }

        protected virtual void ResetMass()
        {
            if (!_jumpMassApplied)
                return;
            Pelvis.mass = _pelvisMass;
            Head.mass = _headMass;
            Knee.mass = _kneeMass;
            LocoBall.mass = _ballMass;
            _jumpMassApplied = false;
        }

        public Vector3 Move(Vector3 moveDelta)
        {
            var pos = Pelvis.position;
            LocoBall.MovePosition(LocoBall.position + moveDelta);
            Knee.MovePosition(Knee.position + moveDelta);
            Head.MovePosition(Head.position + moveDelta);
            Pelvis.MovePosition(Pelvis.position + moveDelta);
            return Pelvis.position - pos;
        }

        public void AddVelocity(Vector3 velocity)
        {
            Head.linearVelocity += velocity;
            Pelvis.linearVelocity += velocity;
            Knee.linearVelocity += velocity;
            LocoBall.linearVelocity += velocity;
        }

        private void ApplyFrictionFactor(float factor)
        {
            if (_frictionOverriden)
                return;
            LocoCollider.material.dynamicFriction = _dynamicFriction * factor;
            LocoCollider.material.staticFriction = _staticFriction * factor;
        }



        /// <summary>
        /// Overrides the friction of the locoball.
        /// </summary>
        public void OverrideBallFriction(float dynamicFriction, float staticFriction)
        {
            _frictionOverriden = true;
            LocoCollider.material.dynamicFriction = dynamicFriction;
            LocoCollider.material.staticFriction = staticFriction;
        }

        /// <summary>
        /// Returns the friction to the starting state
        /// </summary>
        public void StopFrictionOverride()
        {
            _frictionOverriden = false;
            ApplyFrictionFactor(1f);
        }
    }
}