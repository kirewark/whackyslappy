using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Builds the cartoon atmosphere for the arena: solid sky colour on the
    /// cameras, trilight ambient light, soft horizon-matching linear fog, a
    /// warm sun, a procedural grass texture on the floor, a big visual-only
    /// meadow ground plane with a mown-path border, and a distant hill and
    /// tree horizon (ParkHorizon). Called once by GameBootstrap after the
    /// floor exists. Safe to call twice: settings are re-applied and objects
    /// are not duplicated.
    /// </summary>
    public static class EnvironmentBuilder
    {
        // Tuning values live here as constants because a static builder
        // has no Inspector (same style as GameBootstrap.FloorSize).
        // Sky clear colour is a deeper blue than the fog colour (a pale
        // blue-white horizon), so hills and meadow fade into the lower sky.
        private static readonly Color SkyColor = new Color(0.45f, 0.72f, 1.0f);
        private static readonly Color HorizonHaze = new Color(0.75f, 0.88f, 0.98f);
        private static readonly Color AmbientSky = new Color(0.6f, 0.8f, 1.0f);
        private static readonly Color AmbientEquator = new Color(0.8f, 0.8f, 0.7f);
        private static readonly Color AmbientGround = new Color(0.4f, 0.35f, 0.3f);
        private static readonly Color SunColor = new Color(1.0f, 0.95f, 0.85f);
        private static readonly Color GrassLightGreen = new Color(0.45f, 0.70f, 0.30f);
        private static readonly Color GrassDarkGreen = new Color(0.38f, 0.62f, 0.26f);
        private static readonly Color FleckLight = new Color(0.55f, 0.78f, 0.35f);
        private static readonly Color FleckDark = new Color(0.30f, 0.50f, 0.22f);
        private static readonly Color MeadowColor = new Color(0.32f, 0.45f, 0.25f);
        private static readonly Color PathColor = new Color(0.60f, 0.45f, 0.28f);

        private const float FogStart = 40f;
        private const float FogEnd = 160f;
        private const float SunIntensity = 1.1f;
        private const float GroundPlaneSize = 400f;
        // One grass texture tile spans 8 m of world, so the light/dark
        // mowing bands baked two-per-tile are about 4 m wide in world space.
        private const float GrassTileWorldSize = 8f;

        /// <summary>
        /// Applies sky, ambient and fog settings, and adds the grass floor
        /// look, the meadow ground plane, a path border and the horizon
        /// under arenaRoot.
        /// </summary>
        public static void Build(GameObject arenaRoot)
        {
            ApplySky();
            ApplyAmbientAndFog();
            EnsureSun(arenaRoot);
            ApplyFloorLook(arenaRoot);
            CreateGroundPlane(arenaRoot);
            CreatePathBorder(arenaRoot);
            GrassTufts.Build(arenaRoot, GraphicsQuality.Level);
            ParkHorizon.Build(arenaRoot);
        }

        /// <summary>
        /// Removes any skybox material and makes every camera clear to a
        /// solid sky-blue colour. A gradient sky dome would need a custom
        /// shader, and Shader.Find is banned for runtime materials in this
        /// repo (it can return null in a WebGL build), so we use a solid
        /// colour instead and match the fog colour to the near-horizon
        /// part of that sky (see ApplyAmbientAndFog).
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
        /// Trilight ambient light plus linear fog. The fog colour is the
        /// pale blue-white horizon haze (a touch lighter than the sky clear
        /// colour) so the hills and the 400 m meadow plane fade out before
        /// their edges ever show against the deeper upper sky.
        /// </summary>
        private static void ApplyAmbientAndFog()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = HorizonHaze;
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
        /// Replaces the floor's texture with procedural grass (base greens,
        /// blade flecks and faint mowing bands). Only the renderer material
        /// is touched; the collider and size stay as-is.
        /// </summary>
        private static void ApplyFloorLook(GameObject arenaRoot)
        {
            Transform floorTf = arenaRoot.transform.Find("Floor");
            if (floorTf == null)
            {
                Debug.LogWarning("[EnvironmentBuilder] Floor object not found; skipped grass texture.");
                return;
            }

            var floorRenderer = floorTf.GetComponent<MeshRenderer>();
            if (floorRenderer == null)
                return;

            // renderer.material gives a per-object instance, so the shared
            // primitive material is never modified (repo material pattern).
            Material floorMat = floorRenderer.material;
            floorMat.mainTexture = MakeGrassTexture();
            // GrassTileWorldSize (8 m) spans one tile, so flecks and the
            // ~4 m mowing bands keep the same world size on any arena size.
            floorMat.mainTextureScale = new Vector2(
                GameBootstrap.FloorSize / GrassTileWorldSize,
                GameBootstrap.FloorSize / GrassTileWorldSize);
        }

        /// <summary>
        /// 128x128 procedural grass tile: the two cartoon greens alternate
        /// as faint full-tile mowing bands (two per tile), a smooth seeded
        /// value-noise blotch adds large-scale variation, and random light
        /// or dark flecks read as blades. Fully deterministic; the noise
        /// grid indices wrap so the tile is seamless.
        /// </summary>
        private static Texture2D MakeGrassTexture()
        {
            const int Size = 128;
            const int BlotchCells = 8;

            var rng = new System.Random(20261125);
            var blotch = new float[BlotchCells, BlotchCells];
            for (int by = 0; by < BlotchCells; by++)
                for (int bx = 0; bx < BlotchCells; bx++)
                    blotch[bx, by] = (float)rng.NextDouble();

            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, true);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Repeat;

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Two mowing bands per tile: hard edges read as a
                    // mown-lawn stripe in the cartoon palette.
                    bool lightBand = ((int)(x * 2f / Size)) % 2 == 0;
                    Color c = lightBand ? GrassLightGreen : GrassDarkGreen;

                    float b = SampleTilingNoise(blotch, BlotchCells, x, y, Size) - 0.5f;
                    c += new Color(b, b, b, 0f) * 0.12f;

                    double r = rng.NextDouble();
                    if (r < 0.12)
                        c = Color.Lerp(c, FleckLight, 0.45f);
                    else if (r > 0.88)
                        c = Color.Lerp(c, FleckDark, 0.45f);

                    pixels[y * Size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Bilinear sample of a small seeded random grid whose indices wrap
        /// at the grid edge, so the noise tiles seamlessly across the whole
        /// texture (Mathf.PerlinNoise alone does not tile at small scales).
        /// </summary>
        private static float SampleTilingNoise(float[,] values, int cells, int x, int y, int size)
        {
            float gx = x * (cells / (float)size);
            float gy = y * (cells / (float)size);
            int x0 = Mathf.FloorToInt(gx) % cells;
            int y0 = Mathf.FloorToInt(gy) % cells;
            int x1 = (x0 + 1) % cells;
            int y1 = (y0 + 1) % cells;

            float tx = gx - Mathf.FloorToInt(gx);
            float ty = gy - Mathf.FloorToInt(gy);
            tx = tx * tx * (3f - 2f * tx); // smoothstep so blotches have no creases
            ty = ty * ty * (3f - 2f * ty);

            float a = Mathf.Lerp(values[x0, y0], values[x1, y0], tx);
            float b = Mathf.Lerp(values[x0, y1], values[x1, y1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>
        /// Wide plain meadow-green plane (no texture: low contrast, so the
        /// tiled grass inside the arena reads as a mown lawn). Visual only:
        /// its collider is removed and its top face sits just under the
        /// floor top (y = -0.05). 400 m wide so its far edge sits well
        /// beyond the fog end (160 m) and never shows a hard line.
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
            ground.transform.localScale = new Vector3(GroundPlaneSize, 0.1f, GroundPlaneSize);

            // DestroyImmediate: this runs at scene init, before the first
            // physics step, so the huge BoxCollider must be gone at once.
            var groundCollider = ground.GetComponent<Collider>();
            if (groundCollider != null)
                Object.DestroyImmediate(groundCollider);

            // Standard-shader base from DefaultLitMaterial (repo pattern: no
            // Shader.Find, no copy of the primitive's stripped built-in).
            var mat = DefaultLitMaterial.Create();
            if (mat != null)
            {
                mat.color = MeadowColor;
                ground.GetComponent<MeshRenderer>().material = mat;
            }
        }

        /// <summary>
        /// Thin warm-brown mown-path strip hugging the arena edge, sitting
        /// above the meadow plane and below the floor top. Visual only:
        /// every collider is destroyed and the four strips share one
        /// material instance.
        /// </summary>
        private static void CreatePathBorder(GameObject arenaRoot)
        {
            if (arenaRoot.transform.Find("PathBorder") != null)
                return;

            var border = new GameObject("PathBorder");
            border.transform.SetParent(arenaRoot.transform, false);

            var half = GameBootstrap.FloorHalf;
            const float Offset = 1.1f; // strip centre distance from the arena edge
            const float Width = 1.4f;
            const float StripY = -0.03f; // below the floor top (0), above the meadow (-0.05)

            Vector3[] positions =
            {
                new Vector3(0f, StripY, half + Offset), new Vector3(0f, StripY, -(half + Offset)),
                new Vector3(half + Offset, StripY, 0f), new Vector3(-(half + Offset), StripY, 0f)
            };
            Vector3[] scales =
            {
                new Vector3(GameBootstrap.FloorSize + 2f * Width, 0.05f, Width),
                new Vector3(GameBootstrap.FloorSize + 2f * Width, 0.05f, Width),
                new Vector3(Width, 0.05f, GameBootstrap.FloorSize),
                new Vector3(Width, 0.05f, GameBootstrap.FloorSize)
            };

            Material pathMat = DefaultLitMaterial.Create();
            if (pathMat != null)
                pathMat.color = PathColor;

            for (int i = 0; i < 4; i++)
            {
                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "PathStrip_" + i;
                strip.transform.SetParent(border.transform, false);
                strip.transform.position = positions[i];
                strip.transform.localScale = scales[i];

                var stripCollider = strip.GetComponent<Collider>();
                if (stripCollider != null)
                    Object.DestroyImmediate(stripCollider);

                if (pathMat != null)
                    strip.GetComponent<MeshRenderer>().material = pathMat;
            }
        }
    }
}
