using System.Collections.Generic;
using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Procedural placeholder slap sound. Generates a short noise burst
    /// with fast exponential decay, pitch-randomised per hit, volume
    /// scaled by impact strength.
    ///
    /// WebGL-safe: AudioClips are baked with AudioClip.Create + SetData
    /// (no onLoad callback) and AudioSources are pooled to avoid per-hit
    /// GameObject allocations.
    ///
    /// Design 3.7 / Plan Phase C1.
    /// </summary>
    public class SlapAudio : MonoBehaviour
    {
        [Header("Slap Sound Generation")]
        [SerializeField] private float m_decayTime = 0.06f;
        [SerializeField] private float m_minPitch = 0.8f;
        [SerializeField] private float m_maxPitch = 1.4f;

        // Static mirrors of the tuning fields: static Play cannot read
        // instance fields directly. Awake copies them from the component.
        private static float s_minPitch = 0.8f;
        private static float s_maxPitch = 1.4f;
        private static float s_decayTime = 0.06f;

        private static readonly List<AudioSource> s_sourcePool = new();
        private static AudioClip s_slapClip;

        /// <summary>
        /// Play the procedural slap sound at the given world position
        /// with volume scaled by impact strength.
        /// </summary>
        /// <param name="position">World-space position for spatial audio.</param>
        /// <param name="strength">Impact strength; 1.0 = normal volume.</param>
        public static void Play(Vector3 position, float strength)
        {
            if (s_slapClip == null)
            {
                EnsureClipCreated(s_decayTime);
            }

            if (s_slapClip == null) return;

            var source = GetPooledSource();
            if (source == null) return;

            source.transform.position = position;
            source.clip = s_slapClip;
            source.volume = strength;
            source.pitch = Random.Range(s_minPitch, s_maxPitch);
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
            EnsureClipCreated(s_decayTime);

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

        private static void EnsureClipCreated(float decayTime)
        {
            if (s_slapClip != null) return;

            int sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0) sampleRate = 44100;

            int lengthSamples = Mathf.CeilToInt(sampleRate * decayTime);

            var samples = new float[lengthSamples];
            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-3.0f * t / decayTime);
                samples[i] = (Random.value * 2f - 1f) * decay;
            }

            s_slapClip = AudioClip.Create("SlapPlaceholder", lengthSamples, 1, sampleRate, false);
            s_slapClip.SetData(samples, 0);
        }
    }
}
