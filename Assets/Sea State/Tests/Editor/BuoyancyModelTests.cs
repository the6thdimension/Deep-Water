using NUnit.Framework;
using Unity.Mathematics;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.Tests
{
    /// <summary>Probe buoyancy math and a 1-D floating-box sanity check.</summary>
    public class BuoyancyModelTests
    {
        const float Rho = 1025f, G = 9.81f;
        static readonly BuoyancyProbe Probe = new BuoyancyProbe { AreaM2 = 2f, HeightM = 1f };
        static readonly BuoyancyDamping NoDamping = default;

        [Test]
        public void DryProbe_HasNoForce()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, 2f, 0f), float3.zero, float3.zero);
            Assert.AreEqual(float3.zero, f);
        }

        [Test]
        public void FullySubmergedProbe_DisplacesItsVolume()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, -5f, 0f), float3.zero, float3.zero);
            Assert.AreEqual(Rho * G * 2f * 1f, f.y, 1e-2f);
        }

        [Test]
        public void HalfSubmergedProbe_GivesHalfTheForce()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, 0f, 0f), float3.zero, float3.zero);
            Assert.AreEqual(Rho * G * 2f * 0.5f, f.y, 1e-2f);
        }

        [Test]
        public void Drag_OpposesMotionRelativeToTheWater()
        {
            var damping = new BuoyancyDamping { LinearMs = 1f, QuadraticCd = 1f };
            var still = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, float3.zero, new float3(1f, 0f, 0f), new float3(1f, 0f, 0f));
            var moving = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, float3.zero, new float3(2f, 0f, 0f), new float3(1f, 0f, 0f));
            Assert.AreEqual(0f, still.x, 1e-4f, "moving with the water: no drag");
            Assert.Less(moving.x, 0f, "faster than the water: drag pushes back");
        }

        [Test]
        public void FloatingBox_SettlesAtArchimedesDraft()
        {
            // A 1 m-tall column of 2 m² with mass for a 0.4 m draft, heave only, damped.
            float mass = Rho * Probe.AreaM2 * 0.4f;
            var damping = new BuoyancyDamping { LinearMs = 2f * 0.3f * math.sqrt(G * 0.4f), QuadraticCd = 0f };
            float y = 1f, v = 0f;
            const float dt = 0.02f;
            for (int i = 0; i < 3000; i++)
            {
                float3 f = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, new float3(0f, y, 0f), new float3(0f, v, 0f), float3.zero);
                v += (f.y / mass - G) * dt;
                y += v * dt;
            }
            float draft = 0.5f - y;   // column bottom is 0.5 m below its centre
            Assert.AreEqual(0.4f, draft, 0.01f);
        }
    }
}
