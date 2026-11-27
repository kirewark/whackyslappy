using UnityEngine;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Phase 5b: pure aggro math for the NpcController chase behaviour,
    /// split out so EditMode tests can pin it without any physics.
    /// NpcController owns when these run (State.Wander only).
    /// </summary>
    public static class NpcAggro
    {
        /// <summary>
        /// Hysteresis: an NPC that is not aggro becomes aggro when the
        /// horizontal distance drops to enterRadius or below; an aggro NPC
        /// stays aggro until the distance exceeds exitRadius. Callers are
        /// expected to keep enterRadius &lt;= exitRadius.
        /// </summary>
        public static bool Evaluate(
            bool wasAggro, float distance, float enterRadius, float exitRadius)
        {
            if (wasAggro)
                return distance <= exitRadius;
            return distance <= enterRadius;
        }

        /// <summary>
        /// Normalized XZ direction from 'from' towards 'to'; the height
        /// difference is ignored. Vector3.zero when the two points are
        /// horizontally coincident.
        /// </summary>
        public static Vector3 FlatDirection(Vector3 from, Vector3 to)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-10f)
                return Vector3.zero;
            return flat.normalized;
        }
    }
}
