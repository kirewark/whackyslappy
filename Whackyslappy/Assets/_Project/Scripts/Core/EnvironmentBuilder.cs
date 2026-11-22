using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Builds the cartoon atmosphere for the arena: solid sky colour on the
    /// cameras, trilight ambient light, linear fog, a warm sun, a green
    /// checker texture on the floor, and a big visual-only ground plane.
    /// Called once by GameBootstrap after the floor exists. Safe to call
    /// twice: settings are re-applied and objects are not duplicated.
    /// </summary>
    public static class EnvironmentBuilder
    {
        // Tuning values live here as constants because a static builder
        // has no Inspector (same style as GameBootstrap.FloorSize).
        private static readonly Color SkyColor = new Color(0.45f, 0.75f, 1.0f);
        private static readonly Color AmbientSky = new Color(0.6f, 0.8f, 1.0f);
        private static readonly Color AmbientEquator = new Color(0.8f, 0.8f, 0.7f);
        private static readonly Color AmbientGround = new Color(0.4f, 0.35f, 0.3f);
        private static readonly Color SunColor = new Color(1.0f, 0.95f, 0.85f);
        private static readonly Color CheckerLightGreen = new Color(0.55f, 0.70f, 0.42f);
        private static readonly Color CheckerDarkGreen = new Color(0.44f, 0.60f, 0.33f);
        private static readonly Color GroundColor = new Color(0.35f, 0.45f, 0.28f);

        private const float FogStart = 25f;
        private const float FogEnd = 90f;
        private const float SunIntensity = 1.1f;

        /// <summary>
        /// Applies sky, ambient, fog and sun settings, and adds the checker
        /// floor look plus the visual ground plane under arenaRoot.
        /// </summary>
        public static void Build(GameObject arenaRoot)
        {
            ApplySky();
            ApplyAmbientAndFog();
            EnsureSun(arenaRoot);
            ApplyFloorLook(arenaRoot);
            CreateGroundPlane(arenaRoot);
        }

        /// <summary>
        /// Removes any skybox material and makes every camera clear to a
        /// solid sky-blue colour. A gradient sky dome would need a custom
        /// shader, and Shader.Find is banned for runtime materials in this
        /// repo (it can return null in a WebGL build), so we use a solid
        /// colour instead and match the fog to it.
        /// </summary>
        private static void ApplySky()
        {
            RenderSettings.skybox = null;

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].clearFlags = CameraClearFlags.SolidColor;
                cameras[i].backgroundColor = SkyColor;
            }
        }

        /// <summary>
        /// Trilight ambient light plus linear fog. The fog colour equals the
        /// sky clear colour so distant ground blends into the background.
        /// </summary>
        private static void ApplyAmbientAndFog()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
        }

        /// <summary>
        /// Creates a warm directional sun if none exists, otherwise only
        /// re-tunes the existing one. Parented under arenaRoot.
        /// </summary>
        private static void EnsureSun(GameObject arenaRoot)
        {
            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                var sunObj = new GameObject("Sun");
                sunObj.transform.SetParent(arenaRoot.transform, false);
                sunObj.transform.position = new Vector3(0f, 10f, 0f);
                sunObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun = sunObj.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
            }

            sun.color = SunColor;
            sun.intensity = SunIntensity;
        }

        /// <summary>
        /// Replaces the floor's texture with a soft green checker. Only the
        /// renderer material is touched; the collider and size stay as-is.
        /// </summary>
        private static void ApplyFloorLook(GameObject arenaRoot)
        {
            Transform floorTf = arenaRoot.transform.Find("Floor");
            if (floorTf == null)
            {
                Debug.LogWarning("[EnvironmentBuilder] Floor object not found; skipped checker texture.");
                return;
            }

            var floorRenderer = floorTf.GetComponent<MeshRenderer>();
            if (floorRenderer == null)
                return;

            // renderer.material gives a per-object instance, so the shared
            // primitive material is never modified (repo material pattern).
            Material floorMat = floorRenderer.material;
            floorMat.mainTexture = MakeCheckerTexture();
            floorMat.mainTextureScale = new Vector2(10f, 10f);
        }

        /// <summary>
        /// 64x64 checker of two soft greens, two big cells per tile,
        /// point filtered and repeated.
        /// </summary>
        private static Texture2D MakeCheckerTexture()
        {
            const int Size = 64;
            const int Cell = 32;

            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool lightCell = ((x / Cell) + (y / Cell)) % 2 == 0;
                    tex.SetPixel(x, y, lightCell ? CheckerLightGreen : CheckerDarkGreen);
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Large dull-green plane outside the arena so the world does not
        /// end at the floor edge. Visual only: its collider is removed and
        /// its top face sits just under the floor top (y = -0.05).
        /// </summary>
        private static void CreateGroundPlane(GameObject arenaRoot)
        {
            // Guard against a second call duplicating the plane.
            if (arenaRoot.transform.Find("GroundPlane") != null)
                return;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "GroundPlane";
            ground.transform.SetParent(arenaRoot.transform, false);
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.transform.localScale = new Vector3(200f, 0.1f, 200f);

            // DestroyImmediate: this runs at scene init, before the first
            // physics step, so the huge BoxCollider must be gone at once.
            var groundCollider = ground.GetComponent<Collider>();
            if (groundCollider != null)
                Object.DestroyImmediate(groundCollider);

            // Copy the primitive's default material (repo pattern; no Shader.Find).
            var mat = new Material(ground.GetComponent<MeshRenderer>().sharedMaterial);
            mat.color = GroundColor;
            ground.GetComponent<MeshRenderer>().material = mat;
        }
    }
}
