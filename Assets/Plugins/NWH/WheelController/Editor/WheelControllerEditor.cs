#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
using UnityEngine;

namespace NWH.WheelController3D
{
    /// <summary>
    ///     Editor for WheelController.
    /// </summary>
    [CustomEditor(typeof(WheelController))]
    [CanEditMultipleObjects]
    public class WheelControllerEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI()) return false;

            WheelController wc = target as WheelController;
            
            float logoHeight = 40f;
            Rect  texRect    = drawer.positionRect;
            texRect.height = logoHeight;
            drawer.DrawEditorTexture(texRect, "Wheel Controller 3D/Editor/logo_wc3d", ScaleMode.ScaleToFit);
            drawer.Space(logoHeight + 4);

            drawer.Field("showAdvancedSettings");
            
            drawer.BeginSubsection("Debug Values");
            if (Application.isPlaying)
            {
                drawer.Field("forwardFriction.slip", false, null, "Longitudinal Slip");
                drawer.Field("sideFriction.slip", false, null, "Lateral Slip");
                drawer.Field("suspensionForceMagnitude", false);
                drawer.Field("spring.bottomedOut", false);
                drawer.Field("wheel.motorTorque", false);
                drawer.Field("wheel.brakeTorque", false);
            }
            else
            {
                drawer.Info("Enter play mode to view debug values.");
            }

            drawer.EndSubsection();


            drawer.BeginSubsection("Wheel");
            drawer.Field("vehicleSide");

            drawer.Field("wheel.radius", true, "m");
            drawer.Field("wheel.width",  true, "m");
            drawer.Field("wheel.mass",   true, "kg");
            if (wc.showAdvancedSettings) drawer.Field("wheel.maxLoad", true, "N");
            drawer.Field("dragTorque", true, "Nm");

            drawer.Field("parent");

            if (wc.showAdvancedSettings)
            {
                if (!drawer.Field("useRimCollider").boolValue)
                {
                    drawer.Info("Use of Rim Collider is highly recommended when wheels stick out of the body collider of the vehicle.");
                }
            }

            drawer.EndSubsection();

            drawer.BeginSubsection("Wheel Model");
            drawer.Field("wheel.visual");
            drawer.Field("wheel.nonRotatingVisual", true, "", "Non-Rotating Visual (opt.)");
            drawer.EndSubsection();

            drawer.BeginSubsection("Spring");
            drawer.Field("spring.maxForce", true, "Nm");
            if (Application.isPlaying)
                if (wc != null && wc.vehicleWheelCount > 0)
                {
                    float minRecommended = wc.parentNRigidbody.mass * -Physics.gravity.y * 1.4f / wc.vehicleWheelCount;
                    if (wc.spring.maxForce < minRecommended)
                        drawer.Info(
                            "MaxForce of Spring is most likely too low for the vehicle mass. Minimum recommended for current configuration is" +
                            $" {minRecommended}N.", MessageType.Warning);
                }

            if (drawer.Field("spring.maxLength", true, "m").floatValue < Time.fixedDeltaTime * 10f)
                drawer.Info(
                    $"Minimum recommended spring length for Time.fixedDeltaTime of {Time.fixedDeltaTime} is {Time.fixedDeltaTime * 10f}");

            if (wc.showAdvancedSettings)
            {
                drawer.Field("spring.forceCurve");
                drawer.Info("X: Spring compression [%], Y: Force coefficient");
            }

            drawer.EndSubsection();

            drawer.BeginSubsection("Damper");
            drawer.Field("damper.bumpForce", true, "Ns/m");

            if (wc.showAdvancedSettings)
            {
                drawer.Field("damper.bumpCurve");
                drawer.Info("X: Spring velocity (normalized) [m/s/10], Y: Force coefficient (normalized)");
            }

            drawer.Field("damper.reboundForce", true, "Ns/m");

            if (wc.showAdvancedSettings)
            {
                drawer.Field("damper.reboundCurve");
                drawer.Info("X: Spring velocity (normalized) [m/s/10], Y: Force coefficient (normalized)");
            }

            drawer.EndSubsection();

            drawer.BeginSubsection("Geometry");
            drawer.Field("wheel.rimOffset", true, "m");
            if (wc.showAdvancedSettings) drawer.Field("antiSquat", true, "x100%");
            drawer.Field("wheel.camberAtTop",    true, "deg");
            drawer.Field("wheel.camberAtBottom", true, "deg");

            drawer.EndSubsection();

            drawer.BeginSubsection("Friction");

            drawer.Field("activeFrictionPreset");
            drawer.EmbeddedObjectEditor<NUIEditor>(((WheelController) target).activeFrictionPreset,
                                                   drawer.positionRect);

            if (wc.showAdvancedSettings)
            {
                drawer.Field("wheel.camberFrictionCurve", true, null, "Camber/Friction Curve");
                if (Application.isPlaying) drawer.Field("_effectiveCamber", false, "deg", "Effective Camber");
            }

            if (wc.showAdvancedSettings)
            {
                drawer.BeginSubsection("Friction Circle");
                drawer.Field("frictionCircleStrength");
                drawer.Info("1 - Realistic (no lateral grip with wheel spin, wheel lock)\r\n0 - Arcade (stable, no doughnuts, powerslides)");
                drawer.Field("frictionCircleShape");
                drawer.EndSubsection();
            }

            drawer.BeginSubsection("Longitudinal");
            drawer.Field("forwardFriction.slipCoefficient",  true, "x100 %");
            drawer.Field("forwardFriction.forceCoefficient", true, "x100 %");
            drawer.EndSubsection();

            drawer.BeginSubsection("Lateral");
            drawer.Field("sideFriction.slipCoefficient",  true, "x100 %");
            drawer.Field("sideFriction.forceCoefficient", true, "x100 %");
            drawer.EndSubsection();

            drawer.EndSubsection();

            drawer.BeginSubsection("Ground Detection");
            SerializedProperty longScanRes =
                drawer.Field("longitudinalScanResolution", !Application.isPlaying);
            SerializedProperty latScanRes =
                drawer.Field("lateralScanResolution", !Application.isPlaying);
            if (longScanRes.intValue < 1) longScanRes.intValue = 1;
            if (latScanRes.intValue < 1) latScanRes.intValue = 1;

            int rayCount = longScanRes.intValue * latScanRes.intValue;
            drawer.Info($"Ray count: {rayCount}");

            if (wc.showAdvancedSettings)
            {
                drawer.Field("applyForceToOthers");

                if (!drawer.Field("autoSetupLayerMask").boolValue)
                {
                    drawer.Field("layerMask");
                    drawer.Info(
                        "Make sure that vehicle's collider layers are unselected in the layerMask, as well as Physics.IgnoreRaycast layer. If not, " +
                        "wheels will collide with vehicle itself sand result in it behaving unpredictably.");
                }
                else
                    drawer.Info(
                        "Vehicle colliders will be set to Ignore Raycast layer while Auto Setup Layer Mask is active. Use manual setup " +
                        "if you need to be able to raycast the vehicle.");
            }

            drawer.EndSubsection();
            
            if (wc.showAdvancedSettings)
            {
                drawer.BeginSubsection("Physics Depenetration");
                drawer.Info("These settings are only used when the suspension bottoms out.");
                drawer.Field("depenetrationSpring");
                drawer.Field("depenetrationDamping");
                drawer.EndSubsection();
            }



            drawer.EndEditor(this);
            return true;
        }


        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}

#endif