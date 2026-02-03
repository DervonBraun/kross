using UnityEngine;

namespace Player
{
    public struct MotorTelemetry
    {
        public PlayerState State;
        public bool IsGrounded;

        public Vector3 PlanarVelocity; // world m/s (XZ)
        public float PlanarSpeed;      // m/s
        public float Speed01;          // 0..1 (relative to MoveSpeed)
    }
}