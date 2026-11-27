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

        // Real Ouch clips from the SoundBank, loaded once lazily on the
        // first Yelp. Null/empty bank falls back to the procedural kit.
        private static AudioClip[] s_ouchClips;
        private static bool s_ouchLoadAttempted;
        private static int s_lastOuchIndex = -1;

        /// <summary>
        /// Play one NPC reaction/yelp sound, rate-limited globally.
        /// Pass the NPC's root Transform as follow so the sound travels
        /// with the NPC while it is flying away.
        /// </summary>
        public static void Yelp(Vector3 position, float strength, Transform follow = null)
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

            // Prefer a real Ouch clip from the SoundBank; the procedural
            // kit below is the fallback when the bank is missing or empty.
            AudioClip ouchClip = PickOuchClip();
            float volume = Mathf.Lerp(0.4f, 1f, strength)
                         * Mathf.Lerp(0.6f, 1f, SillyPhysics.Silliness);

            s_nextAllowedTime = now + MinGapSeconds;
            s_recentTimes[s_writeIndex] = now;
            s_writeIndex = (s_writeIndex + 1) % s_recentTimes.Length;

            if (ouchClip != null)
            {
                // Louder hits also sound slightly higher-pitched.
                float ouchPitch = Random.Range(0.92f, 1.1f) + 0.08f * strength;
                SlapAudio.PlayClip(ouchClip, position, volume, ouchPitch, follow);
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

            // volume is fed through Play's strength parameter to scale the sound.
            SlapAudio.Play(kind, position, volume, follow);
        }

        // Returns a random non-null Ouch clip, avoiding the same clip two
        // times in a row when the bank has more than one. Loads the bank
        // lazily once; returns null when no clips are available. The
        // random-start scan allocates nothing.
        private static AudioClip PickOuchClip()
        {
            if (!s_ouchLoadAttempted)
            {
                s_ouchLoadAttempted = true;
                SoundBank bank = SoundBank.Load();
                s_ouchClips = bank != null ? bank.Ouch : null;
            }

            AudioClip[] clips = s_ouchClips;
            if (clips == null || clips.Length == 0) return null;

            int start = Random.Range(0, clips.Length);
            for (int n = 0; n < clips.Length; n++)
            {
                int index = (start + n) % clips.Length;
                AudioClip candidate = clips[index];
                if (candidate == null) continue;
                if (index == s_lastOuchIndex && clips.Length > 1) continue;
                s_lastOuchIndex = index;
                return candidate;
            }

            // Only one non-null clip exists (it matched s_lastOuchIndex):
            // replay it rather than staying silent.
            for (int n = 0; n < clips.Length; n++)
            {
                if (clips[n] != null)
                {
                    s_lastOuchIndex = n;
                    return clips[n];
                }
            }
            return null;
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
            s_ouchClips = null;
            s_ouchLoadAttempted = false;
            s_lastOuchIndex = -1;
        }
    }
}
