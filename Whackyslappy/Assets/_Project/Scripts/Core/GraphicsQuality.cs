using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Global graphics-quality tier for the WebGL player. The tier is set
    /// once before the AfterSceneLoad bootstraps run, from the page URL's
    /// ?q=low|high|off query parameter; the default is High (the 60 fps
    /// mid-range-laptop target). Boot-time builders (EnvironmentBuilder,
    /// GrassTufts) branch on GraphicsQuality.Level.
    /// </summary>
    public static class GraphicsQuality
    {
        /// <summary>Quality levels the boot-time builders branch on.</summary>
        public enum Tier { Off = 0, Low = 1, High = 2 }

        /// <summary>Current tier; defaults to High in Editor and tests.</summary>
        public static Tier Level = Tier.High;

        /// <summary>
        /// WebGL player only: read ?q= from Application.absoluteURL at
        /// SubsystemRegistration, i.e. before the AfterSceneLoad
        /// bootstraps call EnvironmentBuilder, so the tier is already
        /// correct when the arena is built. Unrecognised or missing
        /// values keep the High default.
        /// </summary>
#if UNITY_WEBGL && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReadQualityFromUrl()
        {
            string q = QueryValue(Application.absoluteURL, "q");
            if (q == "off")
                Level = Tier.Off;
            else if (q == "low")
                Level = Tier.Low;
            // "high", missing or unrecognised: stay on the High default.
        }
#endif

        /// <summary>
        /// Returns the lower-cased value of a URL query parameter, or
        /// null when the URL has no such parameter. Hand-parsed because
        /// System.Web does not exist in a WebGL player. Keys are matched
        /// case-insensitively; a trailing URL fragment is ignored.
        /// </summary>
        internal static string QueryValue(string url, string key)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            int qPos = url.IndexOf('?');
            if (qPos < 0)
                return null;

            string query = url.Substring(qPos + 1);
            int hashPos = query.IndexOf('#');
            if (hashPos >= 0)
                query = query.Substring(0, hashPos);

            string[] pairs = query.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                int eq = pairs[i].IndexOf('=');
                if (eq <= 0)
                    continue;
                if (string.Equals(pairs[i].Substring(0, eq), key, System.StringComparison.OrdinalIgnoreCase))
                    return pairs[i].Substring(eq + 1).ToLowerInvariant();
            }
            return null;
        }
    }
}
