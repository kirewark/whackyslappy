using UnityEngine;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// What kind of hit landed on an ISlappable object.
    /// </summary>
    public enum SlapKind
    {
        /// <summary>The normal left-mouse slap.</summary>
        Slap,

        /// <summary>The right-mouse leg sweep.</summary>
        Sweep
    }

    /// <summary>
    /// Everything a hit target needs to know about one slap or sweep hit:
    /// where it landed, the impulse to apply, which action caused it,
    /// and a 0-1 strength used for reactions and sound pitch.
    /// </summary>
    public readonly struct SlapHitInfo
    {
        /// <summary>World-space contact point on the hit object.</summary>
        public Vector3 Point { get; }

        /// <summary>Impulse vector to apply at the contact point.</summary>
        public Vector3 Impulse { get; }

        /// <summary>Which action landed: Slap or Sweep.</summary>
        public SlapKind Kind { get; }

        /// <summary>Hit strength, 0 to 1.</summary>
        public float Strength { get; }

        /// <summary>
        /// Builds one hit record.
        /// </summary>
        public SlapHitInfo(Vector3 point, Vector3 impulse, SlapKind kind, float strength)
        {
            Point = point;
            Impulse = impulse;
            Kind = kind;
            Strength = strength;
        }
    }
}
