using UnityEngine;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Shared factory for the Standard-shader material used by runtime-built visuals.
    /// Loads the Resources/DefaultLit asset once, clones it per call, and falls back
    /// to Shader.Find shaders if the asset is missing.
    /// </summary>
    public static class DefaultLitMaterial
    {
        private static Material m_base;
        private static bool m_loadAttempted;
        private static bool m_baseMissingLogged;

        /// <summary>
        /// Returns a new Material instance based on Resources/DefaultLit.
        /// Never returns the cached base itself, so callers can tint their copy.
        /// </summary>
        public static Material Create()
        {
            if (!m_loadAttempted)
            {
                m_loadAttempted = true;
                m_base = Resources.Load<Material>("DefaultLit");
                if (m_base == null && !m_baseMissingLogged)
                {
                    m_baseMissingLogged = true;
                    Debug.LogError("DefaultLitMaterial: Resources/DefaultLit is missing. " +
                        "Falling back to Shader.Find shaders.");
                }
            }

            if (m_base != null)
            {
                return new Material(m_base);
            }

            Shader fallbackShader = Shader.Find("Standard");
            if (fallbackShader == null)
            {
                fallbackShader = Shader.Find("Sprites/Default");
            }
            if (fallbackShader == null)
            {
                // No new Material(null): that throws and aborts the bootstrap chain.
                return null;
            }
            return new Material(fallbackShader);
        }
    }
}
