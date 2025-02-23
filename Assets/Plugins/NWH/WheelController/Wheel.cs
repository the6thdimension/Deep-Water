using System;
using System.Collections.Generic;
using UnityEngine;

namespace NWH.WheelController3D
{
    /// <summary>
    ///     Contains everything wheel related, including rim and tire.
    /// </summary>
    [Serializable]
    public class Wheel
    {
        /// <summary>
        ///     Current angular velocity of the wheel in rad/s.
        /// </summary>
        [Tooltip("    Current angular velocity of the wheel in rad/s.")]
        public float angularVelocity;

        /// <summary>
        ///     Brake torque applied to the wheel in Nm.
        /// </summary>
        [Tooltip("    Brake torque applied to the wheel in Nm.")]
        public float brakeTorque;

        /// <summary>
        ///     Current camber angle.
        /// </summary>
        [Tooltip("    Current camber angle.")]
        public float camberAngle;

        /// <summary>
        ///     Camber angle at the bottom of suspension travel (fully extended).
        /// </summary>
        [Tooltip("    Camber angle at the bottom of suspension travel (fully extended).")]
        public float camberAtBottom;

        /// <summary>
        ///     Camber angle at the top of suspension travel (fully compressed).
        /// </summary>
        [Tooltip("    Camber angle at the top of suspension travel (fully compressed).")]
        public float camberAtTop;

        /// <summary>
        /// Curve determining friction in relation to the camber / lean angle of the tyre.
        /// </summary>
        [UnityEngine.Tooltip("Curve determining friction in relation to the camber / lean angle of the tyre.")]
        public AnimationCurve camberFrictionCurve;

        /// <summary>
        ///     Forward vector of the wheel in world coordinates.
        /// </summary>
        [Tooltip("    Forward vector of the wheel in world coordinates.")]
        public Vector3 forward;

        /// <summary>
        ///     Total inertia of the wheel, including the attached powertrain.
        /// </summary>
        [UnityEngine.Tooltip("    Total inertia of the wheel, including the attached powertrain.")]
        public float inertia;

        /// <summary>
        /// Initial inertia of the wheel, disconnected from the powertrain.
        /// </summary>
        [UnityEngine.Tooltip("Initial inertia of the wheel, disconnected from the powertrain.")]
        public float baseInertia;

        /// <summary>
        ///     Vector in world coordinates pointing towards the inside of the wheel.
        /// </summary>
        [Tooltip("    Vector in world coordinates pointing towards the inside of the wheel.")]
        public Vector3 inside;

        /// <summary>
        ///     Tire load in Nm.
        /// </summary>
        [UnityEngine.Tooltip("    Tire load in Nm.")]
        public float load;

        /// <summary>
        ///     Maximum load the tire is rated for in [N]. 
        ///     Used to calculate friction. Default value is adequate for most cars but 
        ///     larger and heavier vehicles such as semi trucks will use higher values.
        ///     If left too low the tire will get overloaded and tend to skid.
        ///     
        ///     Typical values:
        ///     3000 - motorcycle tyre
        ///     5400 - typical car tyre
        ///     25000 - semi steerer tyre
        ///     40000 - rear semi tyre
        /// </summary>
        [UnityEngine.Tooltip("    Maximum load the tire is rated for in [N]. \r\n    Used to calculate friction. Default value is adequate for most cars but \r\n    larger and heavier vehicles such as semi trucks will use higher values.\r\n    If left too low the tire will get overloaded and tend to skid.\r\n    \r\n    Typical values:\r\n    3000 - motorcycle tyre\r\n    5400 - typical car tyre\r\n    25000 - semi steerer tyre\r\n    40000 - rear semi tyre")]
        public float maxLoad = 5400;

        /// <summary>
        ///     Mass of the wheel. Inertia is calculated from this.
        /// </summary>
        [Tooltip("    Mass of the wheel. Inertia is calculated from this.")]
        public float mass = 20.0f;

        /// <summary>
        ///     Motor torque applied to the wheel. Since NWH Vehicle Physics 2 the value is readonly and setting it will have no
        ///     effect
        ///     since torque calculation is done inside powertrain solver.
        /// </summary>
        [Tooltip(
            "Motor torque applied to the wheel. Since NWH Vehicle Physics 2 the value is readonly and setting it will have no effect\r\nsince torque calculation is done inside powertrain solver.")]
        public float motorTorque;

        /// <summary>
        ///     Position offset of the non-rotating part.
        /// </summary>
        [Tooltip("    Position offset of the non-rotating part.")]
        public Vector3 nonRotatingPositionOffset;

        /// <summary>
        ///     Total radius of the tire in [m].
        /// </summary>
        [Tooltip("    Total radius of the tire in [m].")]
        public float radius = 0.35f;

        /// <summary>
        ///     Vector in world coordinates pointing to the right of the wheel.
        /// </summary>
        [Tooltip("    Vector in world coordinates pointing to the right of the wheel.")]
        public Vector3 right;

        /// <summary>
        ///     GameObject containing the rim MeshCollider. This is used to prevent objects from penetrating into the wheel from
        ///     sides
        ///     or top,
        ///     where the ground detection does not work.
        /// </summary>
        [Tooltip(
            "GameObject containing the rim MeshCollider. This is used to prevent objects from penetrating into the wheel from sides\r\nor top,\r\nwhere the ground detection does not work.")]
        public GameObject rimColliderGO;

        /// <summary>
        ///     Offset of the rim from the center of steering rotation.
        /// </summary>
        [Tooltip("    Offset of the rim from the center of steering rotation.")]
        public float rimOffset;

        /// <summary>
        ///     Current rotation angle of the wheel visual in regards to it's X axis vector.
        /// </summary>
        [Tooltip("    Current rotation angle of the wheel visual in regards to it's X axis vector.")]
        public float rotationAngle;

        /// <summary>
        ///     Current wheel RPM.
        /// </summary>
        [Tooltip("    Current wheel RPM.")]
        public float RPM;

        /// <summary>
        ///     Current steer angle of the wheel.
        /// </summary>
        [Tooltip("    Current steer angle of the wheel.")]
        public float steerAngle;

        /// <summary>
        ///     Wheel's up vector in world coordinates.
        /// </summary>
        [Tooltip("    Wheel's up vector in world coordinates.")]
        public Vector3 up;

        /// <summary>
        ///     Width of the tyre.
        /// </summary>
        [Tooltip("    Width of the tyre.")]
        public float width = 0.25f;

        /// <summary>
        ///     Position of the wheel in world coordinates.
        /// </summary>
        [Tooltip("    Position of the wheel in world coordinates.")]
        public Vector3 worldPosition;

        public float smoothYPosition;

        public float prevAngularVelocity;

        /// <summary>
        ///     Rotation of the wheel in world coordinates.
        /// </summary>
        [Tooltip("    Rotation of the wheel in world coordinates.")]
        public Quaternion worldRotation;

        /// <summary>
        ///     Object representing non-rotating part of the wheel. This could be things such as brake calipers, external fenders,
        ///     etc.
        /// </summary>
        [Tooltip(
            "Object representing non-rotating part of the wheel. This could be things such as brake calipers, external fenders, etc.")]
        [SerializeField]
        private GameObject nonRotatingVisual;

        /// <summary>
        ///     GameObject representing the visual aspect of the wheel / wheel mesh.
        ///     Should not have any physics colliders attached to it.
        /// </summary>
        [Tooltip(
            "GameObject representing the visual aspect of the wheel / wheel mesh.\r\nShould not have any physics colliders attached to it.")]
        [SerializeField]
        private GameObject visual;

        public GameObject Visual
        {
            get { return visual; }
            set
            {
                visual       = value;
            }
        }

        public GameObject NonRotatingVisual
        {
            get { return nonRotatingVisual; }
            set
            {
                nonRotatingVisual       = value;
            }
        }

        public float LoadPercent
        {
            get { return Mathf.Clamp01(load / maxLoad); }
        }


        /// <summary>
        ///     Calculation of static parameters and creation of rim collider.
        /// </summary>
        public void Initialize(WheelController wc)
        {
            baseInertia = inertia = 0.5f * mass * radius * radius;

            if (rimColliderGO != null || !wc.useRimCollider || visual == null) return;

            // Instantiate rim (prevent ground passing through the side of the wheel)
            rimColliderGO      = new GameObject();
            rimColliderGO.name = "RimCollider";
            rimColliderGO.transform.position =
                wc.transform.position + wc.transform.right * (rimOffset * (int) wc.vehicleSide);
            rimColliderGO.transform.parent = wc.transform;
            rimColliderGO.layer            = LayerMask.NameToLayer("Ignore Raycast");

            try
            {
                MeshFilter mf = rimColliderGO.AddComponent<MeshFilter>();
                mf.name      = "Rim Mesh Filter";
                mf.mesh      = GenerateRimColliderMesh(visual.transform);
                mf.mesh.name = "Rim Mesh";

                MeshCollider mc = rimColliderGO.AddComponent<MeshCollider>();
                mc.name   = "Rim MeshCollider";
                mc.convex = true;

                PhysicsMaterial material = new PhysicsMaterial();
                material.staticFriction  = 0f;
                material.dynamicFriction = 0f;
                material.bounciness      = 0f;
                material.bounceCombine   = PhysicsMaterialCombine.Minimum;
                material.frictionCombine = PhysicsMaterialCombine.Minimum;
                mc.material              = material;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"An error has happened while setting up RimCollider. RimCollider might have not been generated. Error: {e}");
            }
        }


        private Mesh GenerateRimColliderMesh(Transform rt)
        {
            Mesh          mesh               = new Mesh();
            List<Vector3> vertices           = new List<Vector3>();
            List<int>     triangles          = new List<int>();
            Matrix4x4     worldToLocalMatrix = Matrix4x4.TRS(rt.position, rt.rotation, Vector3.one).inverse; // TODO

            float   halfWidth        = width / 1.4f;
            float   theta            = 0.0f;
            float   startAngleOffset = Mathf.PI / 18.0f;
            float   x                = radius * 0.5f * Mathf.Cos(theta);
            float   y                = radius * 0.5f * Mathf.Sin(theta);
            Vector3 pos              = worldToLocalMatrix.MultiplyPoint3x4(worldPosition + up * y + forward * x);
            Vector3 newPos           = pos;

            int vertexIndex = 0;
            for (theta = startAngleOffset; theta <= Mathf.PI * 2 + startAngleOffset; theta += Mathf.PI / 12.0f)
            {
                if (theta <= Mathf.PI - startAngleOffset)
                {
                    x = radius * 1.1f * Mathf.Cos(theta);
                    y = radius * 1.1f * Mathf.Sin(theta);
                }
                else
                {
                    x = radius * 0.2f * Mathf.Cos(theta);
                    y = radius * 0.2f * Mathf.Sin(theta);
                }

                newPos = worldToLocalMatrix.MultiplyPoint3x4(worldPosition + up * y + forward * x);

                Vector3 localRightOffset = worldToLocalMatrix.MultiplyVector(right) * halfWidth;

                // Left Side
                Vector3 p0 = pos - localRightOffset;
                Vector3 p1 = newPos - localRightOffset;

                // Right side
                Vector3 p2 = pos + localRightOffset;
                Vector3 p3 = newPos + localRightOffset;

                vertices.Add(p0);
                vertices.Add(p1);
                vertices.Add(p2);
                vertices.Add(p3);

                // Triangles (double sided)
                // 013
                triangles.Add(vertexIndex + 3);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 0);

                // 023
                triangles.Add(vertexIndex + 0);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 3);

                pos         =  newPos;
                vertexIndex += 4;
            }

            mesh.vertices  = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}