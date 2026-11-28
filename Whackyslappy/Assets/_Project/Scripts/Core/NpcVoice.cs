// NpcVoice: static yelp helper for NPC reaction sounds (Pass-2 Phase 5b).
// Rate-limits yelps globally so a pile-on of hits does not machine-gun squeaks.
using System.Collections.Generic;
using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>Pitch band of an NPC's voice clips; each NPC keeps one tier.</summary>
    public enum VoiceTier { Low, Mid, High }

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
        private static readonly AudioClip[][] s_tierClips = new AudioClip[3][];
        private static readonly int[] s_tierLast = { -1, -1, -1 };

        /// <summary>
        /// Play one NPC reaction/yelp sound, rate-limited globally.
        /// Pass the NPC's root Transform as follow so the sound travels
        /// with the NPC while it is flying away.
        /// </summary>
        public static void Yelp(Vector3 position, float strength, Transform follow = null,
            VoiceTier tier = VoiceTier.Mid)
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
            AudioClip ouchClip = PickOuchClip(tier);
            float volume = Mathf.Lerp(0.4f, 1f, strength)
                         * Mathf.Lerp(0.6f, 1f, SillyPhysics.Silliness);

            s_nextAllowedTime = now + MinGapSeconds;
            s_recentTimes[s_writeIndex] = now;
            s_writeIndex = (s_writeIndex + 1) % s_recentTimes.Length;

            if (ouchClip != null)
            {
                // Real clips only get +/-5% jitter: the tier sets the pitch band.
                float ouchPitch = Random.Range(0.95f, 1.05f);
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
        private static AudioClip PickOuchClip(VoiceTier tier)
        {
            if (!s_ouchLoadAttempted)
            {
                s_ouchLoadAttempted = true;
                SoundBank bank = SoundBank.Load();
                s_ouchClips = bank != null ? bank.Ouch : null;
                BuildVoiceTiers();
            }

            int tierIndex = (int)tier;
            AudioClip[] clips = s_tierClips[tierIndex];
            if (clips == null || clips.Length == 0) clips = s_ouchClips;
            if (clips == null || clips.Length == 0) return null;

            int start = Random.Range(0, clips.Length);
            for (int n = 0; n < clips.Length; n++)
            {
                int index = (start + n) % clips.Length;
                AudioClip candidate = clips[index];
                if (candidate == null) continue;
                if (index == s_tierLast[tierIndex] && clips.Length > 1) continue;
                s_tierLast[tierIndex] = index;
                return candidate;
            }

            // Only one non-null clip exists (it matched s_tierLast):
            // replay it rather than staying silent.
            for (int n = 0; n < clips.Length; n++)
            {
                if (clips[n] != null)
                {
                    s_tierLast[tierIndex] = n;
                    return clips[n];
                }
            }
            return null;
        }

        // Bucket the loaded clips once into the three tiers by name suffix.
        private static void BuildVoiceTiers()
        {
            if (s_ouchClips == null) return;
            List<AudioClip>[] buckets = new List<AudioClip>[3];
            for (int t = 0; t < buckets.Length; t++) buckets[t] = new List<AudioClip>();
            foreach (AudioClip clip in s_ouchClips)
            {
                if (clip == null) continue;
                string name = clip.name;
                int t = name.EndsWith("_Low", System.StringComparison.Ordinal) ? 0
                      : name.EndsWith("_High", System.StringComparison.Ordinal) ? 2 : 1;
                buckets[t].Add(clip);
            }
            for (int t = 0; t < buckets.Length; t++) s_tierClips[t] = buckets[t].ToArray();
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
            System.Array.Clear(s_tierClips, 0, s_tierClips.Length);
            s_tierLast[0] = s_tierLast[1] = s_tierLast[2] = -1;
        }
    }
}
