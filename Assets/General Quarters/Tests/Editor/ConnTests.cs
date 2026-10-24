using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XInput;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Examples;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>The conn's logic: what each control means as Orders.</summary>
    public class ConnLogicTests
    {
        static readonly float[] Bells = { 0f, 5f, 10f, 15f, 20f, 31f };
        static ConnLogic Logic() => new ConnLogic(35f, Bells);
        static HelmCommand Holding(float course, float knots) => HelmCommand.Apply(HelmCommand.Stopped(0f), Order.Helm(OrderIssuer.Captain, course, knots));

        [Test]
        public void Bells_StepOneAtATime_AndStartFromTheOrderedSpeed()
        {
            ConnLogic logic = Logic();
            var stopped = HelmCommand.Stopped(0f);
            Assert.AreEqual(EngineBell.AheadOneThird, logic.Next(stopped, +1));
            Assert.AreEqual(EngineBell.BackOneThird, logic.Next(stopped, -1));
            Assert.AreEqual(EngineBell.AheadStandard, logic.Next(Holding(0f, 12f), +1), "12 kn held: up is standard (15)");
            Assert.AreEqual(EngineBell.AheadTwoThirds, logic.Next(Holding(0f, 12f), -1), "and down is two thirds (10)");
            var emergency = HelmCommand.Apply(stopped, Order.EngineOrder(OrderIssuer.Captain, EngineBell.BackEmergency));
            Assert.AreEqual(EngineBell.BackEmergency, logic.Next(emergency, -1), "nothing below back emergency");
            var flank = HelmCommand.Apply(stopped, Order.EngineOrder(OrderIssuer.Captain, EngineBell.AheadFlank));
            Assert.AreEqual(EngineBell.AheadFlank, logic.Next(flank, +1), "nothing above flank");
        }

        [Test]
        public void Rudder_TakesTheWheel_AndLettingGoSteadiesHer()
        {
            ConnLogic logic = Logic();
            var c = Holding(90f, 15f);
            var orders = logic.Step(new ConnInputFrame { Rudder = 0.5f }, c, 92.4f, 0.02f).ToList();
            Assert.AreEqual(1, orders.Count);
            Assert.AreEqual(OrderKind.Rudder, orders[0].Kind);
            Assert.AreEqual(17.5f, orders[0].RudderDeg, 1e-4f);

            Assert.IsEmpty(logic.Step(new ConnInputFrame { Rudder = 0.51f }, c, 93f, 0.02f), "under a degree of change: no new order");

            orders = logic.Step(new ConnInputFrame { Rudder = 0f }, c, 104.6f, 0.02f).ToList();
            Assert.AreEqual(1, orders.Count, "let go of the wheel");
            Assert.AreEqual(OrderKind.Course, orders[0].Kind);
            Assert.AreEqual(105f, orders[0].CourseDeg, "steady on the heading she has now");
            Assert.IsEmpty(logic.Step(new ConnInputFrame(), c, 105f, 0.02f), "and nothing more until the next control");
        }

        [Test]
        public void CourseChange_TurnsTheOrderedCourse_AndLeavesTheEnginesAlone()
        {
            ConnLogic logic = Logic();
            var c = Holding(350f, 15f);
            var all = Enumerable.Range(0, 50).SelectMany(_ => logic.Step(new ConnInputFrame { CourseChange = 1f }, c, 350f, 0.02f).ToList()).ToList();
            Assert.IsTrue(all.All(o => o.Kind == OrderKind.Course), "course orders only");
            Assert.AreEqual(2f, all.Last().CourseDeg, 1f, "a second at 12°/s from 350 is about 002");
            Assert.AreEqual(12, all.Count, 1, "one order per whole degree");
        }

        [Test]
        public void Buttons_AllStopMidshipsSteady()
        {
            ConnLogic logic = Logic();
            var c = Holding(0f, 20f);
            Assert.AreEqual(EngineBell.Stop, logic.Step(new ConnInputFrame { AllStop = true }, c, 0f, 0.02f).Single().Bell);
            Order mid = logic.Step(new ConnInputFrame { Midships = true }, c, 0f, 0.02f).Single();
            Assert.AreEqual(OrderKind.Rudder, mid.Kind);
            Assert.AreEqual(0f, mid.RudderDeg);
            Order steady = logic.Step(new ConnInputFrame { Steady = true }, c, 271.4f, 0.02f).Single();
            Assert.AreEqual(OrderKind.Course, steady.Kind);
            Assert.AreEqual(271f, steady.CourseDeg);
        }
    }

    /// <summary>
    /// The "Conn" bindings in the project-wide actions, driven by virtual devices: a keyboard and mouse (a laptop),
    /// an Xbox controller (XInput), and a generic HID pad (a Joystick with a hat and numbered buttons, the layout
    /// Unity gives a pad it does not recognise).
    /// </summary>
    public class ConnBindingTests : InputTestFixture
    {
        const string ActionsPath = "Assets/RHMILCOM.inputactions";
        const string GenericPad = @"{
            ""name"": ""GQTestGenericPad"", ""extend"": ""Joystick"",
            ""controls"": [
                { ""name"": ""hat"", ""layout"": ""Dpad"", ""offset"": 0, ""bit"": 0, ""sizeInBits"": 4, ""format"": ""BIT"" },
                { ""name"": ""button2"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 5, ""format"": ""BIT"" },
                { ""name"": ""button3"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 6, ""format"": ""BIT"" },
                { ""name"": ""button4"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 7, ""format"": ""BIT"" },
                { ""name"": ""button5"", ""layout"": ""Button"", ""offset"": 0, ""bit"": 8, ""format"": ""BIT"" }
            ]}";

        InputActionAsset _asset;
        InputActionMap _map;

        public override void Setup()
        {
            base.Setup();
            InputSystem.RegisterLayout(GenericPad);
            _asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            _map = _asset.FindActionMap(ConnInput.MapName, true);
        }

        public override void TearDown()
        {
            _map?.Disable();
            Object.DestroyImmediate(_asset);
            base.TearDown();
        }

        InputAction A(string name) => _map.FindAction(name, true);

        [Test]
        public void TheConnMap_IsInTheProjectWideActions_WithAllThreeSchemes()
        {
            // The asset Project Settings → Input System Package shows as "Project-wide Actions".
            string projectWide = AssetDatabase.GetAssetPath(EditorBuildSettingsActions());
            Assert.AreEqual(ActionsPath, projectWide, "the Conn map ships in the project-wide actions");
            CollectionAssert.IsSubsetOf(new[] { "Keyboard&Mouse", "Gamepad", "Joystick" }, _asset.controlSchemes.Select(s => s.name).ToArray());
            // Every conn action on all three, except the keyboard's hard-rudder modifier and the camera's analogue actions.
            foreach (InputAction action in _map.actions.Where(a => a.name != "RudderHard" && (!a.name.StartsWith("Camera") || a.name == "CameraMode")))
                foreach (string scheme in new[] { "Keyboard&Mouse", "Gamepad", "Joystick" })
                    Assert.IsTrue(action.bindings.Any(b => !b.isComposite && b.groups != null && b.groups.Contains(scheme)), $"{action.name} has no {scheme} binding");
        }

        static Object EditorBuildSettingsActions()
        {
            EditorBuildSettings.TryGetConfigObject("com.unity.input.settings.actions", out InputActionAsset asset);
            return asset;
        }

        [Test]
        public void Laptop_KeyboardAndMouse()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            _map.Enable();

            Press(keyboard.aKey);
            Assert.AreEqual(-15f / 35f, A("Rudder").ReadValue<float>(), 0.01f, "A: standard rudder left");
            Press(keyboard.leftShiftKey);   // after A: Shift counts either way round
            Assert.AreEqual(-1f, ConnInput.CombineRudder(A("Rudder").ReadValue<float>(), A("RudderHard").IsPressed()), 1e-4f, "Shift+A: hard left");
            Release(keyboard.aKey);
            Assert.AreEqual(0f, ConnInput.CombineRudder(A("Rudder").ReadValue<float>(), A("RudderHard").IsPressed()), 1e-4f, "Shift alone: nothing");
            Press(keyboard.dKey);
            Assert.AreEqual(1f, ConnInput.CombineRudder(A("Rudder").ReadValue<float>(), A("RudderHard").IsPressed()), 1e-4f, "Shift+D: hard right");
            Release(keyboard.dKey); Release(keyboard.leftShiftKey);
            Press(keyboard.rightArrowKey);
            Assert.AreEqual(15f / 35f, A("Rudder").ReadValue<float>(), 0.01f, "→: standard rudder right");
            Release(keyboard.rightArrowKey);
            Assert.AreEqual(0f, A("Rudder").ReadValue<float>(), 1e-4f);

            Press(keyboard.wKey); Assert.IsTrue(A("RingUp").WasPressedThisFrame(), "W rings up"); Release(keyboard.wKey);
            Press(keyboard.downArrowKey); Assert.IsTrue(A("RingDown").WasPressedThisFrame(), "↓ rings down"); Release(keyboard.downArrowKey);
            Press(keyboard.qKey); Assert.AreEqual(-1f, A("CourseChange").ReadValue<float>(), 1e-4f, "Q: course left"); Release(keyboard.qKey);
            Press(keyboard.spaceKey); Assert.IsTrue(A("Steady").WasPressedThisFrame()); Release(keyboard.spaceKey);
            Press(keyboard.xKey); Assert.IsTrue(A("Midships").WasPressedThisFrame()); Release(keyboard.xKey);
            Press(keyboard.zKey); Assert.IsTrue(A("AllStop").WasPressedThisFrame()); Release(keyboard.zKey);
            Press(keyboard.vKey); Assert.IsTrue(A("CameraMode").WasPressedThisFrame()); Release(keyboard.vKey);

            Press(mouse.rightButton);
            Set(mouse.delta, new Vector2(6f, -2f));
            Assert.AreEqual(new Vector2(6f, -2f), A("CameraDrag").ReadValue<Vector2>(), "right-drag orbits the camera");
            Release(mouse.rightButton);
        }

        [Test]
        public void XboxController()
        {
            var pad = InputSystem.AddDevice<XInputController>();
            _map.Enable();

            Set(pad.leftStick, new Vector2(0.8f, 0f));
            float rudder = A("Rudder").ReadValue<float>();
            Assert.That(rudder, Is.InRange(0.6f, 0.85f), "left stick right: proportional right rudder");
            Set(pad.leftStick, new Vector2(0.1f, 0f));
            Assert.AreEqual(0f, A("Rudder").ReadValue<float>(), 1e-4f, "inside the dead zone: hands off");

            Press(pad.dpad.up); Assert.IsTrue(A("RingUp").WasPressedThisFrame(), "d-pad up rings up"); Release(pad.dpad.up);
            Press(pad.leftShoulder); Assert.IsTrue(A("RingDown").WasPressedThisFrame(), "LB rings down"); Release(pad.leftShoulder);
            Press(pad.dpad.right); Assert.AreEqual(1f, A("CourseChange").ReadValue<float>(), 1e-4f, "d-pad right: course right"); Release(pad.dpad.right);
            Press(pad.buttonSouth); Assert.IsTrue(A("Steady").WasPressedThisFrame(), "A: steady"); Release(pad.buttonSouth);
            Press(pad.buttonEast); Assert.IsTrue(A("Midships").WasPressedThisFrame(), "B: midships"); Release(pad.buttonEast);
            Press(pad.buttonNorth); Assert.IsTrue(A("AllStop").WasPressedThisFrame(), "Y: all stop"); Release(pad.buttonNorth);
            Press(pad.buttonWest); Assert.IsTrue(A("CameraMode").WasPressedThisFrame(), "X: camera"); Release(pad.buttonWest);
            Press(pad.selectButton); Assert.IsTrue(A("TogglePanel").WasPressedThisFrame(), "View: panel"); Release(pad.selectButton);
            Set(pad.rightTrigger, 1f); Assert.AreEqual(1f, A("CameraZoom").ReadValue<float>(), 1e-4f, "RT zooms in"); Set(pad.rightTrigger, 0f);
            Set(pad.rightStick, new Vector2(0f, 0.9f)); Assert.Greater(A("CameraOrbit").ReadValue<Vector2>().y, 0.5f, "right stick orbits");
        }

        [Test]
        public void GenericHidPad()
        {
            var pad = (Joystick)InputSystem.AddDevice("GQTestGenericPad");
            _map.Enable();

            Set(pad.stick, new Vector2(-0.7f, 0f));
            Assert.Less(A("Rudder").ReadValue<float>(), -0.4f, "stick left: left rudder");
            Set(pad.stick, Vector2.zero);

            var hatUp = (UnityEngine.InputSystem.Controls.ButtonControl)pad["hat/up"];
            var hatLeft = (UnityEngine.InputSystem.Controls.ButtonControl)pad["hat/left"];
            Press(hatUp); Assert.IsTrue(A("RingUp").WasPressedThisFrame(), "hat up rings up"); Release(hatUp);
            Press(hatLeft); Assert.AreEqual(-1f, A("CourseChange").ReadValue<float>(), 1e-4f, "hat left: course left"); Release(hatLeft);
            Press(pad.trigger); Assert.IsTrue(A("Steady").WasPressedThisFrame(), "trigger: steady"); Release(pad.trigger);
            foreach (var (control, action) in new[] { ("button2", "Midships"), ("button3", "AllStop"), ("button4", "CameraMode"), ("button5", "TogglePanel") })
            {
                var button = (UnityEngine.InputSystem.Controls.ButtonControl)pad[control];
                Press(button);
                Assert.IsTrue(A(action).WasPressedThisFrame(), $"{control}: {action}");
                Release(button);
            }
        }
    }
}
