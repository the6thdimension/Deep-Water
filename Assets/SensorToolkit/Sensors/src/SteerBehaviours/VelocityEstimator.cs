using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Micosmo.SensorToolkit {

    public struct VelocityEstimator {

        public float PrevSampleTime;
        public Vector3 PrevSamplePosition;
        public Vector3 Velocity;

        public void Sample(GameObject target) => Sample(target.transform.position);

        public void Sample(Vector3 newPosition) {
            if (PrevSampleTime == 0) {
                PrevSampleTime = Time.time;
                PrevSamplePosition = newPosition;
                Velocity = Vector3.zero;
                return;
            }

            var sampleTime = Time.time;
            if (sampleTime == PrevSampleTime) {
                return;
            }
            var samplePosition = newPosition;
            var deltaTime = sampleTime - PrevSampleTime;
            if (deltaTime > 0) {
                Velocity = (samplePosition - PrevSamplePosition) / deltaTime;
            }
            PrevSampleTime = sampleTime;
            PrevSamplePosition = samplePosition;
        }

    }

}