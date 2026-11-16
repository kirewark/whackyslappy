#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WhackySlappy.Core;

namespace WhackySlappy.EditorTools
{
    /// <summary>
    /// Editor tool that fills Assets/_Project/Resources/SoundBank.asset from the
    /// clips under Assets/_Project/Audio and normalises their import settings.
    /// Runs from the WhackySlappy/Build Sound Bank menu, and once automatically
    /// after each editor load if the bank does not exist yet.
    /// </summary>
    [InitializeOnLoad]
    public static class SoundBankBuilder
    {
        private const string ProjectFolder = "Assets/_Project";
        private const string AudioFolder = ProjectFolder + "/Audio";
        private const string ResourcesFolder = ProjectFolder + "/Resources";
        private const string AssetPath = ResourcesFolder + "/SoundBank.asset";

        // Runs on editor load and after every domain reload. The delayCall keeps
        // it out of the load/compile phase itself.
        static SoundBankBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfNeeded;
        }

        private static void AutoBuildIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (AssetDatabase.LoadAssetAtPath<SoundBank>(AssetPath) != null) return;
            if (!AssetDatabase.IsValidFolder(AudioFolder)) return;
            if (AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }).Length == 0) return;
            Build();
        }

        [MenuItem("WhackySlappy/Build Sound Bank")]
        public static void Build()
        {
            string[] bankGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder });

            // Pass 1: normalise import settings (SaveAndReimport can swap the
            // in-memory clip objects, so load the clips only afterwards).
            foreach (string importGuid in bankGuids)
            {
                ApplyImportSettings(AssetDatabase.GUIDToAssetPath(importGuid));
            }

            // Pass 2: load the clips and sort them into the bank arrays by name prefix.
            List<AudioClip> lightClips = new List<AudioClip>();
            List<AudioClip> hardClips = new List<AudioClip>();
            List<AudioClip> ouchClips = new List<AudioClip>();

            foreach (string clipGuid in bankGuids)
            {
                string clipPath = AssetDatabase.GUIDToAssetPath(clipGuid);
                string fileName = Path.GetFileNameWithoutExtension(clipPath);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null) continue;

                if (fileName.StartsWith("Slap_Crisp", StringComparison.Ordinal) ||
                    fileName.StartsWith("Slap_Cartoon", StringComparison.Ordinal) ||
                    fileName.StartsWith("Slap_Whip", StringComparison.Ordinal))
                {
                    lightClips.Add(clip);
                }
                else if (fileName.StartsWith("Slap_Hard", StringComparison.Ordinal) ||
                         fileName.StartsWith("Slap_Thud", StringComparison.Ordinal))
                {
                    hardClips.Add(clip);
                }
                else if (fileName.StartsWith("Ouch_", StringComparison.Ordinal) ||
                         fileName.StartsWith("Oof_", StringComparison.Ordinal) ||
                         fileName.StartsWith("Aah_", StringComparison.Ordinal))
                {
                    ouchClips.Add(clip);
                }
            }

            SortByName(lightClips);
            SortByName(hardClips);
            SortByName(ouchClips);

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder(ProjectFolder, "Resources");
            }

            SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(AssetPath);
            bool bankWasNew = bank == null;
            if (bankWasNew) bank = ScriptableObject.CreateInstance<SoundBank>();

            bank.SlapLight = lightClips.ToArray();
            bank.SlapHard = hardClips.ToArray();
            bank.Ouch = ouchClips.ToArray();

            if (bankWasNew) AssetDatabase.CreateAsset(bank, AssetPath);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            Debug.Log("[SoundBankBuilder] " + AssetPath + " built. SlapLight: " +
                      lightClips.Count + ", SlapHard: " + hardClips.Count +
                      ", Ouch: " + ouchClips.Count + " (of " + bankGuids.Length + " clips found).");
        }

        private static void SortByName(List<AudioClip> clips)
        {
            clips.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
        }

        private static void ApplyImportSettings(string clipPath)
        {
            AudioImporter importer = AssetDatabase.GetImporter(clipPath) as AudioImporter;
            if (importer == null) return;

            AudioImporterSampleSettings wanted = new AudioImporterSampleSettings
            {
                loadType = AudioDataLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.Vorbis,
                sampleRateSetting = AudioSampleRateSetting.KeepOriginalRate,
                quality = 0.5f,
            };

            bool changed = false;

            if (!importer.forceToMono)
            {
                importer.forceToMono = true;
                changed = true;
            }
            if (!importer.preloadAudioData)
            {
                importer.preloadAudioData = true;
                changed = true;
            }
            if (!SettingsMatch(importer.defaultSampleSettings, wanted))
            {
                importer.defaultSampleSettings = wanted;
                changed = true;
            }
            if (!SettingsMatch(importer.GetOverrideSampleSettings("WebGL"), wanted))
            {
                importer.SetOverrideSampleSettings("WebGL", wanted);
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }

        // Tolerance on quality because Unity can round stored Vorbis quality,
        // which would otherwise reimport the clip on every run.
        private static bool SettingsMatch(AudioImporterSampleSettings current, AudioImporterSampleSettings wanted)
        {
            return current.loadType == wanted.loadType &&
                   current.compressionFormat == wanted.compressionFormat &&
                   current.sampleRateSetting == wanted.sampleRateSetting &&
                   Mathf.Abs(current.quality - wanted.quality) < 0.01f;
        }
    }
}
#endif
