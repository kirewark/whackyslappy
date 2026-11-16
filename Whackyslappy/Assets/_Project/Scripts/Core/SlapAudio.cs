using System.Collections.Generic;
using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Procedural sound kit. The slap is a short noise burst with fast
    /// exponential decay; each goofy cartoon sound (Sweep, Boing, Squeak,
    /// Honk, SlideWhistle, Whoosh) is a procedurally generated clip baked
    /// once on first use and cached. Slap plays randomly layer one of the
    /// silly sounds on top of the base hit, with a chance scaled by
    /// SillyPhysics.Silliness. Pitch is randomised per play, volume is
    /// scaled by impact strength.
    ///
    /// WebGL-safe: AudioClips are baked with AudioClip.Create + SetData
    /// (no onLoad callback, no threads, no asset files) and AudioSources
    /// are pooled to avoid per-hit GameObject allocations.
    ///
    /// Design 3.7 / Plan Phase C1, goofy kit in Pass-2 Phase 5.
    /// </summary>
    public class SlapAudio : MonoBehaviour
    {
        /// <summary>Which procedural clip the static Play overloads bake and play.</summary>
        public enum SoundKind
        {
            Slap,
            Sweep,
            Boing,
            Squeak,
            Honk,
            SlideWhistle,
            Whoosh,
        }

        [Header("Slap Sound Generation")]
        [SerializeField] private float m_decayTime = 0.06f;
        [SerializeField] private float m_minPitch = 0.8f;
        [SerializeField] private float m_maxPitch = 1.4f;

        [Header("Goofy Sound Kit (Pass-2 Phase 5)")]
        [SerializeField] private float m_sillyLayerChance = 0.6f;
        [SerializeField] private float m_sillyMinPitch = 0.6f;
        [SerializeField] private float m_sillyMaxPitch = 1.8f;
        [SerializeField] private float m_boingDuration = 0.5f;
        [SerializeField] private float m_squeakDuration = 0.12f;
        [SerializeField] private float m_honkDuration = 0.35f;
        [SerializeField] private float m_slideWhistleDuration = 0.5f;
        [SerializeField] private float m_whooshDuration = 0.3f;
        [SerializeField] private float m_sweepDuration = 0.35f;

        // Static mirrors of the tuning fields: static Play cannot read
        // instance fields directly. Awake copies them from the component.
        private static float s_minPitch = 0.8f;
        private static float s_maxPitch = 1.4f;
        private static float s_decayTime = 0.06f;
        private static float s_sillyLayerChance = 0.6f;
        private static float s_sillyMinPitch = 0.6f;
        private static float s_sillyMaxPitch = 1.8f;
        private static float s_boingDuration = 0.5f;
        private static float s_squeakDuration = 0.12f;
        private static float s_honkDuration = 0.35f;
        private static float s_slideWhistleDuration = 0.5f;
        private static float s_whooshDuration = 0.3f;
        private static float s_sweepDuration = 0.35f;

        private const float TwoPi = 2f * Mathf.PI;

        // One baked clip per SoundKind, generated lazily on first use.
        private static readonly AudioClip[] s_clips = new AudioClip[(int)SoundKind.Whoosh + 1];

        // Kinds eligible to layer on top of a slap hit.
        private static readonly SoundKind[] s_sillyKinds =
        {
            SoundKind.Boing,
            SoundKind.Squeak,
            SoundKind.Honk,
            SoundKind.SlideWhistle,
        };

        private static readonly List<AudioSource> s_sourcePool = new();

        /// <summary>
        /// Play the procedural slap sound at the given world position
        /// with volume scaled by impact strength. With probability
        /// SillyPhysics.Silliness * m_sillyLayerChance, one random silly
        /// sound (Boing/Squeak/Honk/SlideWhistle) is layered on top at
        /// half volume.
        /// </summary>
        /// <param name="position">World-space position for spatial audio.</param>
        /// <param name="strength">Impact strength; 1.0 = normal volume.</param>
        public static void Play(Vector3 position, float strength)
        {
            Play(SoundKind.Slap, position, strength);

            float chance = SillyPhysics.Silliness * s_sillyLayerChance;
            if (chance > 0f && Random.value < chance)
            {
                SoundKind sillyKind = s_sillyKinds[Random.Range(0, s_sillyKinds.Length)];
                Play(sillyKind, position, 0.5f * strength);
            }
        }

        /// <summary>
        /// Play any kit sound at the given world position with pitch
        /// randomised per play and volume scaled by strength. The clip is
        /// generated once on first use of its kind and cached afterwards.
        /// </summary>
        /// <param name="kind">Which procedural sound to play.</param>
        /// <param name="position">World-space position for spatial audio.</param>
        /// <param name="strength">Volume strength; 1.0 = normal volume.</param>
        public static void Play(SoundKind kind, Vector3 position, float strength)
        {
            EnsureClipForKind(kind);

            AudioClip clip = s_clips[(int)kind];
            if (clip == null) return;

            AudioSource source = GetPooledSource();
            if (source == null) return;

            float pitchMin = kind == SoundKind.Slap ? s_minPitch : s_sillyMinPitch;
            float pitchMax = kind == SoundKind.Slap ? s_maxPitch : s_sillyMaxPitch;

            source.transform.position = position;
            source.clip = clip;
            source.volume = strength;
            source.pitch = Random.Range(pitchMin, pitchMax);
            source.spatialBlend = 1f;
            source.Play();
        }

        private static AudioSource GetPooledSource()
        {
            for (int i = 0; i < s_sourcePool.Count; i++)
            {
                var candidate = s_sourcePool[i];
                if (!candidate.isPlaying) return candidate;
            }

            var go = new GameObject("SlapAudioSource");
            go.hideFlags = HideFlags.HideAndDontSave;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            s_sourcePool.Add(src);
            return src;
        }

        private void Awake()
        {
            // Route serialized tuning through the static fields so Play
            // (static) honours them, including m_decayTime for the clip.
            s_minPitch = m_minPitch;
            s_maxPitch = m_maxPitch;
            s_decayTime = m_decayTime;
            s_sillyLayerChance = m_sillyLayerChance;
            s_sillyMinPitch = m_sillyMinPitch;
            s_sillyMaxPitch = m_sillyMaxPitch;
            s_boingDuration = m_boingDuration;
            s_squeakDuration = m_squeakDuration;
            s_honkDuration = m_honkDuration;
            s_slideWhistleDuration = m_slideWhistleDuration;
            s_whooshDuration = m_whooshDuration;
            s_sweepDuration = m_sweepDuration;
            EnsureClipForKind(SoundKind.Slap);

            if (s_sourcePool.Count == 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    var go = new GameObject("SlapAudioSource");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    var src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    s_sourcePool.Add(src);
                }
            }
        }

        private static void EnsureClipForKind(SoundKind kind)
        {
            int slot = (int)kind;
            if (s_clips[slot] != null) return;

            switch (kind)
            {
                case SoundKind.Slap:
                    s_clips[slot] = BakeSlap(s_decayTime);
                    break;
                case SoundKind.Sweep:
                    s_clips[slot] = BakeNoiseSweep("Sweep", s_sweepDuration, 200f, 1200f);
                    break;
                case SoundKind.Boing:
                    s_clips[slot] = BakeBoing("Boing", s_boingDuration);
                    break;
                case SoundKind.Squeak:
                    s_clips[slot] = BakeSqueak("Squeak", s_squeakDuration);
                    break;
                case SoundKind.Honk:
                    s_clips[slot] = BakeHonk("Honk", s_honkDuration);
                    break;
                case SoundKind.SlideWhistle:
                    s_clips[slot] = BakeSlideWhistle("SlideWhistle", s_slideWhistleDuration);
                    break;
                case SoundKind.Whoosh:
                    s_clips[slot] = BakeNoiseSweep("Whoosh", s_whooshDuration, 300f, 1800f);
                    break;
            }
        }

        private static int SampleRate()
        {
            int sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0) sampleRate = 44100;
            return sampleRate;
        }

        private static AudioClip Bake(string name, float[] samples, int sampleRate)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void Normalize(float[] samples, float peakTarget)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float magnitude = Mathf.Abs(samples[i]);
                if (magnitude > peak) peak = magnitude;
            }
            if (peak < 1e-5f) return;

            float gain = peakTarget / peak;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }

        // Original slap: white noise burst with fast exponential decay.
        private static AudioClip BakeSlap(float decayTime)
        {
            decayTime = Mathf.Max(0.01f, decayTime);
            int sampleRate = SampleRate();
            int lengthSamples = Mathf.CeilToInt(sampleRate * decayTime);

            var samples = new float[lengthSamples];
            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-3.0f * t / decayTime);
                samples[i] = (Random.value * 2f - 1f) * decay;
            }

            return Bake("SlapPlaceholder", samples, sampleRate);
        }

        // Sine whose pitch drops then bounces back up, exponential decay.
        private static AudioClip BakeBoing(string name, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            int sampleRate = SampleRate();
            int lengthSamples = Mathf.CeilToInt(sampleRate * duration);

            var samples = new float[lengthSamples];
            float phase = 0f;
            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;

                float pitchFactor = p < 0.35f
                    ? Mathf.Lerp(1f, 0.35f, p / 0.35f)
                    : Mathf.Lerp(0.35f, 0.8f, (p - 0.35f) / 0.65f);
                float freq = 420f * pitchFactor
                    * (1f + 0.15f * Mathf.Sin(TwoPi * 7f * t));

                phase += TwoPi * freq / sampleRate;
                float env = Mathf.Exp(-4f * p) * Mathf.Clamp01((1f - p) * 10f);
                samples[i] = Mathf.Sin(phase) * env;
            }

            Normalize(samples, 0.85f);
            return Bake(name, samples, sampleRate);
        }

        // Very short high sine with rising pitch.
        private static AudioClip BakeSqueak(string name, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            int sampleRate = SampleRate();
            int lengthSamples = Mathf.CeilToInt(sampleRate * duration);

            var samples = new float[lengthSamples];
            float phase = 0f;
            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;

                float freq = Mathf.Lerp(1800f, 3200f, p);
                phase += TwoPi * freq / sampleRate;

                float attack = Mathf.Clamp01(t / 0.008f);
                float env = attack * Mathf.Exp(-5f * p) * Mathf.Clamp01((1f - p) * 10f);
                samples[i] = Mathf.Sin(phase) * env;
            }

            Normalize(samples, 0.8f);
            return Bake(name, samples, sampleRate);
        }

        // Two detuned oscillators around 220 Hz with a soft envelope.
        private static AudioClip BakeHonk(string name, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            int sampleRate = SampleRate();
            int lengthSamples = Mathf.CeilToInt(sampleRate * duration);

            var samples = new float[lengthSamples];
            float phaseA = 0f;
            float phaseB = 0f;
            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;

                float vibrato = 1f + 0.04f * Mathf.Sin(TwoPi * 5.5f * t);
                phaseA += TwoPi * 220f * vibrato / sampleRate;
                phaseB += TwoPi * 227f * vibrato / sampleRate;

                float square = Mathf.Sin(phaseA) >= 0f ? 1f : -1f;
                float saw = 2f * (phaseB / TwoPi % 1f) - 1f;

                float env = t < 0.03f
                    ? t / 0.03f
                    : Mathf.Lerp(1f, 0.25f, (p - 0.03f) / 0.97f);
                samples[i] = 0.5f * (square + saw) * env;
            }

            Normalize(samples, 0.7f);
            return Bake(name, samples, sampleRate);
        }

        // Sine sweeping up through the kit's whistling range.
        private static AudioClip BakeSlideWhistle(string name, float duration)
        {
            duration = Mathf.Max(0.01f, duration);
            int sampleRate = SampleRate();
            int lengthSamples = Mathf.CeilToInt(sampleRate * duration);

            var samples = new float[lengthSamples];
            float phase = 0f;
            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;

                float freq = Mathf.Lerp(400f, 1600f, p);
                phase += TwoPi * freq / sampleRate;

                float env = p < 0.08f
                    ? p / 0.08f
                    : Mathf.Lerp(1f, 0.2f, (p - 0.08f) / 0.92f);
                samples[i] = Mathf.Sin(phase) * env;
            }

            Normalize(samples, 0.8f);
            return Bake(name, samples, sampleRate);
        }

        // Noise through a one-pole low-pass whose cutoff sweeps from
        // startHz to endHz; fast attack, medium decay. Used by Sweep too.
        private static AudioClip BakeNoiseSweep(string name, float duration, float startHz, float endHz)
        {
            duration = Mathf.Max(0.01f, duration);
            int sampleRate = SampleRate();
            int lengthSamples = Mathf.CeilToInt(sampleRate * duration);

            var samples = new float[lengthSamples];
            float lpState = 0f;
            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;

                float cutoff = startHz * Mathf.Pow(endHz / startHz, p);
                float alpha = 1f - Mathf.Exp(-TwoPi * cutoff / sampleRate);
                float noise = Random.value * 2f - 1f;
                lpState += alpha * (noise - lpState);

                float attack = Mathf.Clamp01(t / 0.02f);
                float env = attack * Mathf.Exp(-3.5f * p);
                samples[i] = lpState * env;
            }

            Normalize(samples, 0.9f);
            return Bake(name, samples, sampleRate);
        }
    }
}
