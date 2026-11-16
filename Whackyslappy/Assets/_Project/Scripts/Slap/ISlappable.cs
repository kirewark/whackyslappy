using UnityEngine;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// Interface for objects that can react to a slap impulse.
    /// </summary>
    public interface ISlappable
    {
        /// <summary>
        /// Called when this object is hit by a slap.
        /// </summary>
        /// <param name="impulse">The impulse vector applied to the object.</param>
        /// <param name="point">The world-space contact point on the object.</param>
        void OnSlapped(Vector3 impulse, Vector3 point);

        /// <summary>
        /// Called when this object is hit by a slap or sweep, with full hit data.
        /// The default forwards to OnSlapped so existing implementers keep
        /// working without changes until they opt in to the richer data.
        /// </summary>
        /// <param name="hit">The hit record (point, impulse, kind, strength).</param>
        void OnHit(in SlapHitInfo hit) => OnSlapped(hit.Impulse, hit.Point);
    }
}
