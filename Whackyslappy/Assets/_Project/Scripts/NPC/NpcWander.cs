using UnityEngine;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Pure math for NPC wander steering (Phase 5a), split out from
    /// NpcController so EditMode tests can verify the steering law
    /// without a rigidbody. No Unity object access: static only.
    /// </summary>
    public static class NpcWander
    {
        /// <summary>
        /// Moves the current horizontal velocity toward the desired one
        /// by at most accel * dt per call (Vector3.MoveTowards), so the
        /// NPC ramps and brakes at a fixed acceleration instead of
        /// snapping. The y component of the result is forced to 0: the
        /// vertical velocity belongs to gravity and collisions, handled
        /// outside this helper.
        /// </summary>
        public static Vector3 SteerHorizontal(
            Vector3 currentHorizontal, Vector3 desiredHorizontal,
            float accel, float dt)
        {
            Vector3 steered = Vector3.MoveTowards(
                currentHorizontal, desiredHorizontal, accel * dt);
            steered.y = 0f;
            return steered;
        }
    }
}
