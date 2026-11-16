// NpcVoice: static yelp helper for NPC reaction sounds (Pass-2 Phase 5b).
// Rate-limits yelps globally so a pile-on of hits does not machine-gun squeaks.
using UnityEngine;

namespace WhackySlappy.Core
{
    public static class NpcVoice
    {
        private const float MinGapSeconds = 0.12f;
        private const float WindowSeconds = 1.0f;
        private const int MaxYelpsPerWindow = 3;

        private static float s_nextAllowedTime;
        // Fixed-size ring buffer of recent yelp times: no per-call allocation.
        private static readonly float[] s_recentTimes = new float[MaxYelpsPerWindow];
        private static int s_writeIndex;

        public static void Yelp(Vector3 position, float strength)
        {
            float now = Time.unscaledTime; // hit-stop safe: ignores Time.timeScale
            if (now < s_nextAllowedTime)
            {
                return;
            }

            // Count yelps inside the rolling 1 s window from the ring buffer.
            int recentCount = 0;
            for (int i = 0; i < s_recentTimes.Length; i++)
            {
                if (now - s_recentTimes[i] <= WindowSeconds)
                {
                    recentCount++;
                }
            }
            if (recentCount >= MaxYelpsPerWindow)
            {
                return;
            }

            SlapAudio.SoundKind kind;
            if (strength < 0.35f)
            {
                kind = SlapAudio.SoundKind.Squeak;
            }
            else if (strength < 0.7f)
            {
                kind = Random.value < 0.5f
                    ? SlapAudio.SoundKind.Boing
                    : SlapAudio.SoundKind.Honk;
            }
            else
            {
                kind = SlapAudio.SoundKind.SlideWhistle;
            }

            float volume = Mathf.Lerp(0.4f, 1f, strength)
                         * Mathf.Lerp(0.6f, 1f, SillyPhysics.Silliness);

            s_nextAllowedTime = now + MinGapSeconds;
            s_recentTimes[s_writeIndex] = now;
            s_writeIndex = (s_writeIndex + 1) % s_recentTimes.Length;

            // volume is fed through Play's strength parameter to scale the sound.
            SlapAudio.Play(kind, position, volume);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_nextAllowedTime = 0f;
            for (int i = 0; i < s_recentTimes.Length; i++)
            {
                s_recentTimes[i] = float.NegativeInfinity;
            }
            s_writeIndex = 0;
        }
    }
}
