using System;
using UnityEngine;
using UnityEngine.XR;
#if HEXA_STEAMVR
using Valve.VR;
#endif

namespace HexabodyVR.PlayerController
{
    public class HexaXRInputs : MonoBehaviour
    {
        public const string OpenVR = "openvr";
        public const string WindowsMR = "windowsmr";
        public const string Vive = "vive";
        public const string Cosmos = "cosmos";
        public const string Oculus = "oculus";
        public const string Knuckles = "knuckles";
        public const string WMRController = "spatial";
        public const string HTC = "htc";

        public Transform ControllerOffset;

        public bool IsLeft;

        public Vector2 ThumbstickDeadZone = new Vector2(.15f, .15f);

        public float GripThreshold = .7f;
        public float TriggerThreshold = .7f;

        public float Axis2DUpThreshold = .7f;
        public float Axis2DDownThreshold = .7f;
        public float Axis2DLeftThreshold = .7f;
        public float Axis2DRightThreshold = .7f;

        public Vector3 OpenVRPositionOffset = new Vector3(-0.0175f, .01f, -.0725f);
        public Vector3 OpenVRRotationOffset = new Vector3(40f, 0f, 0f);

        [Header("Visuals Only")]

        public PlayerInputState GripButtonState;
        public PlayerInputState TriggerButtonState;
        public PlayerInputState PrimaryButtonState;
        public PlayerInputState SecondaryButtonState;
        public PlayerInputState MenuButtonState;
        public PlayerInputState JoystickButtonState;
        public PlayerInputState TrackpadButtonState;

        public PlayerInputState TrackPadUp;
        public PlayerInputState TrackPadLeft;
        public PlayerInputState TrackPadRight;
        public PlayerInputState TrackPadDown;

        public Vector2 JoystickAxis;
        public Vector2 TrackpadAxis;

        public bool PrimaryButton;
        public bool SecondaryButton;
        public bool JoystickClicked;
        public bool TrackPadClicked;
        public bool MenuButton;

        public float Grip;
        public bool GripButton;
        public float Trigger;

        public Vector3 Velocity;
        public Vector3 AngularVelocity;

        public bool IsActive { get; set; }

        public XRNode XRNode;
        public bool IsWindowsMR;
        public bool IsOpenVR;
        public bool IsVive;
        public bool IsKnuckles;
        public bool IsOculus;
        public bool IsCosmos;
        public bool HasTrackPad;
        public bool IsXRInputs;

        public bool WindowsWithTrackPad => IsWindowsMR && HasTrackPad;

        private InputDevice _device;
        public InputDevice Device
        {
            get
            {
                if (_device.isValid)
                    return _device;
                _device = InputDevices.GetDeviceAtXRNode(XRNode);
                return _device;
            }
        }

        private static bool _steamInit;
        void Start()
        {
            IsXRInputs = true;
#if HEXA_STEAMVR
            if (!_steamInit)
            {
                Valve.VR.SteamVR.Initialize();
            }
            
            IsXRInputs = false;
#endif

        }

        void FixedUpdate()
        {
            //UpdateInputs();
        }


        private void Update()
        {
            UpdateInputs();
        }

        private void UpdateInputs()
        {
            UpdateDevice();
            UpdateInput();
            CorrectDeadzone();

            CheckButtonState(HexaButtons.Grip, ref GripButtonState);
            CheckButtonState(HexaButtons.Trigger, ref TriggerButtonState);
            CheckButtonState(HexaButtons.JoystickButton, ref JoystickButtonState);
            CheckButtonState(HexaButtons.TrackPadButton, ref TrackpadButtonState);
            CheckButtonState(HexaButtons.Primary, ref PrimaryButtonState);
            CheckButtonState(HexaButtons.Secondary, ref SecondaryButtonState);
            CheckButtonState(HexaButtons.Menu, ref MenuButtonState);

            CheckButtonState(HexaButtons.TrackPadUp, ref TrackPadUp);
            CheckButtonState(HexaButtons.TrackPadLeft, ref TrackPadLeft);
            CheckButtonState(HexaButtons.TrackPadRight, ref TrackPadRight);
            CheckButtonState(HexaButtons.TrackPadDown, ref TrackPadDown);
        }

        private void UpdateDevice()
        {
            CheckControllerType(Device.manufacturer, Device.name);



            var manufacturer = Device.manufacturer;
            var controllerName = Device.name;

            if (manufacturer == null)
                manufacturer = "";
            if (controllerName == null)
                controllerName = "";

            manufacturer = manufacturer.ToLower();
            controllerName = controllerName.ToLower();


            IsOpenVR = controllerName.Contains(OpenVR);

            JoystickAxisFeature = CommonUsages.primary2DAxis;
            TrackPadAxisFeature = CommonUsages.secondary2DAxis;

            JoystickFeature = HexaXRInputFeatures.Primary2DAxisClick;
            TrackpadFeature = HexaXRInputFeatures.Secondary2DAxisClick;

            if (IsWindowsMR && IsOpenVR)
            {
                TrackpadFeature = HexaXRInputFeatures.Primary2DAxisClick;
                JoystickFeature = HexaXRInputFeatures.Secondary2DAxisClick; // can't click anyway in openvr
            }

            if (IsVive)
            {
                TrackPadAxisFeature = CommonUsages.primary2DAxis;
                TrackpadFeature = HexaXRInputFeatures.Primary2DAxisClick;
            }


            UpdateControllerOffset();

#if USING_XR_MANAGEMENT
     if (IsWindowsMR)
        {
            TrackPadAxisFeature = CommonUsages.primary2DAxis;
            JoystickAxisFeature = CommonUsages.secondary2DAxis;

             JoystickFeature = HexaXRInputFeatures.Secondary2DAxisClick;
        TrackpadFeature = HexaXRInputFeatures.Primary2DAxisClick;
        }
#endif

            HasTrackPad = Device.TryGetFeatureValue(TrackPadAxisFeature, out var dummy);
            //Debug.Log($"{HasTrackPad}");
        }

        private void UpdateControllerOffset()
        {
            var rotation = Vector3.zero;
            var position = Vector3.zero;

            if (IsOpenVR)
            {
                rotation = OpenVRRotationOffset;
                position = OpenVRPositionOffset;
            }

            var angles = ControllerOffset.localEulerAngles = rotation;
            ControllerOffset.localPosition = position;
            if (IsLeft)
            {
                var localPos = ControllerOffset.localPosition;
                localPos.x *= -1;
                ControllerOffset.localPosition = localPos;

                angles.y *= -1;
                angles.z *= -1;

                ControllerOffset.localEulerAngles = angles;
            }
        }


        protected InputFeatureUsage<Vector2> JoystickAxisFeature;

        protected InputFeatureUsage<Vector2> TrackPadAxisFeature;

        public HexaXRInputFeatures JoystickFeature;
        public HexaXRInputFeatures TrackpadFeature;

        protected void UpdateInput()
        {

#if HEXA_STEAMVR
            SteamUpdateInput();
#else
            Device.TryGetFeatureValue(JoystickAxisFeature, out JoystickAxis);
            Device.TryGetFeatureValue(TrackPadAxisFeature, out TrackpadAxis);
#endif


        }

        private void CorrectDeadzone()
        {
            if (Mathf.Abs(JoystickAxis.x) < ThumbstickDeadZone.x) JoystickAxis.x = 0f;
            if (Mathf.Abs(JoystickAxis.y) < ThumbstickDeadZone.y) JoystickAxis.y = 0f;
        }

        protected void CheckButtonState(HexaButtons button, ref PlayerInputState buttonState)
        {

            ResetButton(ref buttonState);

#if HEXA_STEAMVR
            SteamCheckButtonState(button, ref buttonState);
#else
           

            switch (button)
            {
                case HexaButtons.Grip:
                    Device.TryGetFeatureValue(CommonUsages.grip, out Grip);
                    buttonState.Value = Grip;
                    SetButtonState(ref buttonState, Grip >= GripThreshold);
                    break;
                case HexaButtons.Trigger:
                    Device.TryGetFeatureValue(CommonUsages.trigger, out Trigger);
                    buttonState.Value = Trigger;
                    SetButtonState(ref buttonState, Trigger >= TriggerThreshold);
                    break;
                case HexaButtons.Primary:
                    if (!IsVive)
                    {
                        SetButtonState(ref buttonState, IsPressed(Device, HexaXRInputFeatures.PrimaryButton));
                        PrimaryButton = buttonState.Active;
                    }

                    break;
                case HexaButtons.Secondary:
                    SetButtonState(ref buttonState, IsPressed(Device, HexaXRInputFeatures.SecondaryButton));
                    SecondaryButton = buttonState.Active;
                    break;
                case HexaButtons.Menu:
                    if (IsVive)
                    {
                        SetButtonState(ref buttonState, IsPressed(Device, HexaXRInputFeatures.PrimaryButton));
                        MenuButton = buttonState.Active;
                    }
                    else
                    {
                        SetButtonState(ref buttonState, IsPressed(Device, HexaXRInputFeatures.MenuButton));
                        MenuButton = buttonState.Active;
                    }
                    break;
                case HexaButtons.JoystickButton:
                    SetButtonState(ref buttonState, IsPressed(Device, JoystickFeature));
                    JoystickClicked = buttonState.Active;
                    break;
                case HexaButtons.TrackPadButton:
                    SetButtonState(ref buttonState, IsPressed(Device, TrackpadFeature));
                    TrackPadClicked = buttonState.Active;
                    break;
                case HexaButtons.TrackPadLeft:
                    SetButtonState(ref TrackPadLeft, TrackpadButtonState.Active && TrackpadAxis.x <= -Axis2DLeftThreshold);
                    break;
                case HexaButtons.TrackPadRight:
                    SetButtonState(ref TrackPadRight, TrackpadButtonState.Active && TrackpadAxis.x >= Axis2DRightThreshold);

                    break;
                case HexaButtons.TrackPadUp:
                    SetButtonState(ref TrackPadUp, TrackpadButtonState.Active && TrackpadAxis.y >= Axis2DUpThreshold);

                    break;
                case HexaButtons.TrackPadDown:
                    SetButtonState(ref TrackPadDown, TrackpadButtonState.Active && TrackpadAxis.y <= -Axis2DDownThreshold);

                    break;
            }
#endif
        }

#if HEXA_STEAMVR
        protected void SteamUpdateInput()
        {
            if (XRNode == XRNode.LeftHand)
            {
                JoystickAxis = SteamVR_Actions.hVR_LeftPrimaryAxis.axis;
                JoystickClicked = SteamVR_Actions.hVR_LeftPrimaryAxisClick.state;
                TrackpadAxis = SteamVR_Actions.hVR_LeftSecondaryAxis.axis;
                TrackPadClicked = SteamVR_Actions.hVR_LeftSecondaryAxisClick.state;

                Grip = SteamVR_Actions.hVR_LeftGrip.axis;
                Trigger = SteamVR_Actions.hVR_LeftTrigger.axis;

                PrimaryButton = SteamVR_Actions.hVR_LeftPrimary.state;
                SecondaryButton = SteamVR_Actions.hVR_LeftSecondary.state;

                MenuButton = SteamVR_Actions.hVR_LeftMenu.state;
                GripButton = SteamVR_Actions.hVR_LeftGripButton.state;
            }
            else
            {
                JoystickAxis = SteamVR_Actions.hVR_RightPrimaryAxis.axis;
                JoystickClicked = SteamVR_Actions.hVR_RightPrimaryAxisClick.state;
                TrackpadAxis = SteamVR_Actions.hVR_RightSecondaryAxis.axis;
                TrackPadClicked = SteamVR_Actions.hVR_RightSecondaryAxisClick.state;

                Grip = SteamVR_Actions.hVR_RightGrip.axis;
                Trigger = SteamVR_Actions.hVR_RightTrigger.axis;

                PrimaryButton = SteamVR_Actions.hVR_RightPrimary.state;
                SecondaryButton = SteamVR_Actions.hVR_RightSecondary.state;

                MenuButton = SteamVR_Actions.hVR_RightMenu.state;
                GripButton = SteamVR_Actions.hVR_RightGripButton.state;
            }
        }

        protected void SteamCheckButtonState(HexaButtons button, ref PlayerInputState buttonState)
        {
            ResetButton(ref buttonState);

            switch (button)
            {
                case HexaButtons.Grip:
                    buttonState.Value = Grip;
                    
                    if (IsKnuckles)
                    {
                        SetButtonState(ref buttonState, GripButton);
                        if (GripButton)
                        {
                            Grip = GripButton ? 1f : 0f;
                        }
                    }
                    else
                    {
                        SetButtonState(ref buttonState, Grip >= GripThreshold);
                    }
                    break;
                case HexaButtons.Trigger:
                    buttonState.Value = Trigger;
                    SetButtonState(ref buttonState, Trigger >= TriggerThreshold);
                    break;
                case HexaButtons.Primary:
                    SetButtonState(ref buttonState, PrimaryButton);
                    break;
                case HexaButtons.Secondary:
                    SetButtonState(ref buttonState, SecondaryButton);
                    break;
                case HexaButtons.Menu:
                    SetButtonState(ref buttonState, MenuButton);
                    break;
                case HexaButtons.JoystickButton:
                    SetButtonState(ref buttonState, JoystickClicked);
                    break;
                case HexaButtons.TrackPadButton:
                    SetButtonState(ref buttonState, TrackPadClicked);
                    break;
                case HexaButtons.TrackPadLeft:
                    SetButtonState(ref buttonState, TrackPadClicked && TrackpadAxis.x <= -Axis2DLeftThreshold);
                    break;
                case HexaButtons.TrackPadRight:
                    SetButtonState(ref buttonState, TrackPadClicked && TrackpadAxis.x >= Axis2DRightThreshold);
                    break;
                case HexaButtons.TrackPadUp:
                    SetButtonState(ref buttonState, TrackPadClicked && TrackpadAxis.y >= Axis2DUpThreshold);
                    break;
                case HexaButtons.TrackPadDown:
                    SetButtonState(ref buttonState, TrackPadClicked && TrackpadAxis.y <= -Axis2DDownThreshold);
                    break;
            }
        }
#endif
        protected void ResetButton(ref PlayerInputState buttonState)
        {
            buttonState.JustDeactivated = false;
            buttonState.JustActivated = false;
            buttonState.Value = 0f;
        }

        protected void SetButtonState(ref PlayerInputState buttonState, bool pressed)
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

        public bool IsPressed(InputDevice device, HexaXRInputFeatures inputFeature, float threshold = 0f)
        {
            if ((int)inputFeature >= s_ButtonData.Length)
            {
                throw new ArgumentException("[InputHelpers.IsPressed] The value of <button> is out or the supported range.");
            }

            var info = s_ButtonData[(int)inputFeature];
            switch (info.type)
            {
                case ButtonReadType.Binary:
                    {
                        if (device.TryGetFeatureValue(new InputFeatureUsage<bool>(info.name), out bool value))
                        {
                            return value;
                        }
                    }
                    break;
            }

            return false;
        }

        private void CheckControllerType(string manufacturer, string controllerName)
        {
            IsCosmos = IsOculus = IsWindowsMR = IsVive = IsKnuckles = false;

            if (string.IsNullOrWhiteSpace(manufacturer) && string.IsNullOrWhiteSpace(controllerName))
                return;

            if (manufacturer == null)
                manufacturer = "";
            if (controllerName == null)
                controllerName = "";

            manufacturer = manufacturer.ToLower();
            controllerName = controllerName.ToLower();

            if (manufacturer.Contains(Oculus))
            {
                IsOculus = true;
            }

            //connected OpenVR Controller(vive_cosmos_controller) - Right,HTC - courtesy of AnriCZ
            if (controllerName.Contains(Cosmos))
            {
                IsCosmos = true;
            }

            if (manufacturer.Contains(HTC) || controllerName.Contains(Vive))
            {
                IsVive = true;
            }

            if (manufacturer.Contains(WindowsMR) || controllerName.Contains(WMRController))
            {
                IsWindowsMR = true;
            }

            if (controllerName.Contains(Knuckles))
            {
                IsKnuckles = true;
            }
        }

        static ButtonInfo[] s_ButtonData = new ButtonInfo[]
        {
            new ButtonInfo("", ButtonReadType.None),
            new ButtonInfo("MenuButton", ButtonReadType.Binary),
            new ButtonInfo("Trigger", ButtonReadType.Axis1D),
            new ButtonInfo("Grip", ButtonReadType.Axis1D),
            new ButtonInfo("TriggerPressed", ButtonReadType.Binary),
            new ButtonInfo("GripPressed", ButtonReadType.Binary),
            new ButtonInfo("PrimaryButton", ButtonReadType.Binary),
            new ButtonInfo("PrimaryTouch", ButtonReadType.Binary),
            new ButtonInfo("SecondaryButton", ButtonReadType.Binary),
            new ButtonInfo("SecondaryTouch", ButtonReadType.Binary),
            new ButtonInfo("Primary2DAxisTouch", ButtonReadType.Binary),
            new ButtonInfo("Primary2DAxisClick", ButtonReadType.Binary),
            new ButtonInfo("Secondary2DAxisTouch", ButtonReadType.Binary),
            new ButtonInfo("Secondary2DAxisClick", ButtonReadType.Binary),
            new ButtonInfo("Primary2DAxis", ButtonReadType.Axis2DUp),
            new ButtonInfo("Primary2DAxis", ButtonReadType.Axis2DDown),
            new ButtonInfo("Primary2DAxis", ButtonReadType.Axis2DLeft),
            new ButtonInfo("Primary2DAxis", ButtonReadType.Axis2DRight),
            new ButtonInfo("Secondary2DAxis", ButtonReadType.Axis2DUp),
            new ButtonInfo("Secondary2DAxis", ButtonReadType.Axis2DDown),
            new ButtonInfo("Secondary2DAxis", ButtonReadType.Axis2DLeft),
            new ButtonInfo("Secondary2DAxis", ButtonReadType.Axis2DRight),
        };

        enum ButtonReadType
        {
            None = 0,
            Binary,
            Axis1D,
            Axis2DUp,
            Axis2DDown,
            Axis2DLeft,
            Axis2DRight
        }

        struct ButtonInfo
        {
            public ButtonInfo(string name, ButtonReadType type)
            {
                this.name = name;
                this.type = type;
            }

            public string name;
            public ButtonReadType type;
        }
    }

    public enum HexaButtons
    {
        Grip,
        Trigger,
        Primary,
        PrimaryTouch,
        Secondary,
        SecondaryTouch,
        Menu,
        JoystickButton,
        TrackPadButton,
        JoystickTouch,
        TriggerTouch,
        ThumbTouch,
        TriggerNearTouch,
        ThumbNearTouch,
        TrackPadLeft,
        TrackPadRight,
        TrackPadUp,
        TrackPadDown
    }

    public enum HexaXRInputFeatures
    {
        None = 0,
        MenuButton,
        Trigger,
        Grip,
        TriggerPressed,
        GripPressed,
        PrimaryButton,
        PrimaryTouch,
        SecondaryButton,
        SecondaryTouch,
        Primary2DAxisTouch,
        Primary2DAxisClick,
        Secondary2DAxisTouch,
        Secondary2DAxisClick,
        PrimaryAxis2DUp,
        PrimaryAxis2DDown,
        PrimaryAxis2DLeft,
        PrimaryAxis2DRight,
        SecondaryAxis2DUp,
        SecondaryAxis2DDown,
        SecondaryAxis2DLeft,
        SecondaryAxis2DRight
    };
}