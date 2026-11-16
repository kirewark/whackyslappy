using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// ScriptableObject that groups the slap and ouch sound clips.
    /// Stored at Assets/_Project/Resources/SoundBank.asset and loaded
    /// at runtime with SoundBank.Load(). Built by the editor tool
    /// WhackySlappy/Build Sound Bank.
    /// </summary>
    public class SoundBank : ScriptableObject
    {
        /// <summary>Path under Resources/ that Load() uses.</summary>
        public const string ResourceName = "SoundBank";

        /// <summary>Light slap sounds (Slap_Crisp, Slap_Cartoon, Slap_Whip).</summary>
        public AudioClip[] SlapLight = new AudioClip[0];

        /// <summary>Heavy slap sounds (Slap_Hard, Slap_Thud).</summary>
        public AudioClip[] SlapHard = new AudioClip[0];

        /// <summary>Reaction sounds (Ouch_*, Oof_01, Aah_01).</summary>
        public AudioClip[] Ouch = new AudioClip[0];

        /// <summary>Returns the sound bank from Resources, or null if it was never built.</summary>
        public static SoundBank Load() => Resources.Load<SoundBank>(ResourceName);
    }
}
