using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Solid blade-tuft grass inside the arena: hundreds of small bent
    /// blade spikes merged into a 4x4 grid of chunk meshes (16
    /// MeshRenderers, shared material, frustum-culled, no colliders, no
    /// shadows), so the whole lawn costs 16 draw calls. Blade colour
    /// comes from UVs sampling a 1x4 dark-green-to-yellow-green gradient
    /// texture on a DefaultLitMaterial copy. Placement is seeded and
    /// identical every run. Called by EnvironmentBuilder after the path
    /// border; the tier decides the blade budget (Off = no grass at all).
    /// </summary>
    public static class GrassTufts
    {
        // Tuning values are constants because a static builder has no
        // Inspector (same style as EnvironmentBuilder / ParkHorizon).
        private const int Seed = 20261125;
        private const int ChunksPerSide = 4;        // 16 chunk renderers

        // Blade budgets per tier. High is about 20000 blades; each blade
        // is 6 triangles (3 front + 3 reversed), so the High tier lands
        // at about 120k triangles over 16 draw calls.
        private const int BladesHigh = 20000;
        private const int BladesLow = 6000;

        private const int BladesPerTuftMin = 5;
        private const int BladesPerTuftMax = 7;     // inclusive
        private const float BladeWidthMin = 0.04f;  // ~0.05 m base width
        private const float BladeWidthMax = 0.06f;
        private const float BladeHeightMin = 0.12f;
        private const float BladeHeightMax = 0.30f;
        private const float EdgeInset = 0.5f;       // tufts stay inside +-19.5 m
        private const float TuftJitter = 0.08f;     // blade root offset in a tuft
        private const float LeanMaxRad = 0.17f;     // ~10 deg random lean
        private const float BaseY = -0.012f;        // blade roots under the floor

        // Gradient (dark green base to light yellow-green tip). Kept in
        // the same cartoon range as EnvironmentBuilder's floor greens.
        private static readonly Color GradientBase = new Color(0.16f, 0.32f, 0.10f);
        private static readonly Color GradientTip = new Color(0.62f, 0.78f, 0.28f);

        /// <summary>
        /// Total blade count for a tier (0 for Off). Public so the
        /// EditMode tests can assert the budget without any scene work.
        /// </summary>
        public static int BladeCountFor(GraphicsQuality.Tier tier)
        {
            switch (tier)
            {
                case GraphicsQuality.Tier.High:
                    return BladesHigh;
                case GraphicsQuality.Tier.Low:
                    return BladesLow;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Builds the "GrassTufts" child of parent holding 16 chunk
        /// GameObjects with merged blade meshes. Does nothing for tier
        /// Off, and a second call is ignored while "GrassTufts" exists.
        /// </summary>
        public static void Build(GameObject parent, GraphicsQuality.Tier tier)
        {
            if (parent == null)
                return;
            if (parent.transform.Find("GrassTufts") != null)
                return;

            int bladeBudget = BladeCountFor(tier);
            if (bladeBudget == 0)
                return;

            // One Standard-shader material (repo pattern: no Shader.Find,
            // no primitive sharedMaterial) shared by all 16 chunks.
            Material grassMat = DefaultLitMaterial.Create();
            if (grassMat == null)
                return;
            grassMat.color = Color.white;
            grassMat.mainTexture = GradientTexture();

            var rng = new System.Random(Seed);

            var root = new GameObject("GrassTufts");
            root.transform.SetParent(parent.transform, false);

            // Chunks tile the whole 40x40 arena; tufts inside each chunk
            // are clamped away from the outer arena border (EdgeInset),
            // so edge chunks are only 0.5 m narrower than inner ones.
            float half = GameBootstrap.FloorHalf;
            float insetMin = -half + EdgeInset;
            float insetMax = half - EdgeInset;
            float cell = GameBootstrap.FloorSize / ChunksPerSide;

            int chunks = ChunksPerSide * ChunksPerSide;
            int baseBlades = bladeBudget / chunks;
            int extraBlades = bladeBudget % chunks;

            int chunkIndex = 0;
            for (int cz = 0; cz < ChunksPerSide; cz++)
            {
                for (int cx = 0; cx < ChunksPerSide; cx++)
                {
                    float x0 = Mathf.Max(-half + cx * cell, insetMin);
                    float x1 = Mathf.Min(-half + (cx + 1) * cell, insetMax);
                    float z0 = Mathf.Max(-half + cz * cell, insetMin);
                    float z1 = Mathf.Min(-half + (cz + 1) * cell, insetMax);

                    int target = baseBlades + (chunkIndex < extraBlades ? 1 : 0);
                    BuildChunk(root.transform, rng, grassMat, cx, cz, x0, x1, z0, z1, target);
                    chunkIndex++;
                }
            }
        }

        /// <summary>
        /// Generates every blade of one chunk as raw vertex/triangle
        /// lists in world space, then uploads one Mesh onto a
        /// MeshFilter + MeshRenderer pair. Every chunk stays far below
        /// 65000 vertices (High tier: 1250 blades * 10 verts), so the
        /// default UInt16 index format is kept; the UInt32 branch is a
        /// guard in case the budgets above grow.
        /// </summary>
        private static void BuildChunk(
            Transform parent, System.Random rng, Material grassMat,
            int cx, int cz, float x0, float x1, float z0, float z1, int bladeTarget)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();

            int bladesLeft = bladeTarget;
            while (bladesLeft > 0)
            {
                // 5-7 blades per tuft; the last tuft may get fewer so the
                // chunk budget stays exact and placement stays uniform.
                int bladesInTuft = Mathf.Min(
                    rng.Next(BladesPerTuftMin, BladesPerTuftMax + 1), bladesLeft);

                float tuftX = Mathf.Lerp(x0, x1, (float)rng.NextDouble());
                float tuftZ = Mathf.Lerp(z0, z1, (float)rng.NextDouble());

                for (int b = 0; b < bladesInTuft; b++)
                    AddBlade(verts, uvs, tris, rng, tuftX, tuftZ);

                bladesLeft -= bladesInTuft;
            }

            var mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            if (verts.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            // RecalculateNormals gives each face its own flat normal
            // because the back-face vertices are exact duplicates with
            // reversed winding, so both sides of each blade light well.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var chunk = new GameObject("GrassChunk_" + cx + "_" + cz);
            chunk.transform.SetParent(parent, false);
            // Chunk transform stays at the identity; blade vertices are
            // already in arena-world coordinates.
            chunk.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = chunk.AddComponent<MeshRenderer>();
            // sharedMaterial keeps all 16 chunks on one material instance.
            renderer.sharedMaterial = grassMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // new GameObject has no collider; the default layer is kept.
        }

        /// <summary>
        /// Appends one bent blade spike: a five-point bent ribbon (base,
        /// mid, tip) drawn as 3 front triangles plus 3 reversed ones so
        /// both faces render when blades lean past culling. Vertices for
        /// the two faces are duplicated (10 per blade) so each face gets
        /// its own flat normal from RecalculateNormals. UV v runs 0 at
        /// the base to 1 at the tip, sampling the 1x4 gradient texture.
        /// </summary>
        private static void AddBlade(
            System.Collections.Generic.List<Vector3> verts,
            System.Collections.Generic.List<Vector2> uvs,
            System.Collections.Generic.List<int> tris,
            System.Random rng, float tuftX, float tuftZ)
        {
            float height = Mathf.Lerp(BladeHeightMin, BladeHeightMax, (float)rng.NextDouble());
            float width = Mathf.Lerp(BladeWidthMin, BladeWidthMax, (float)rng.NextDouble());

            // Blade root jittered a little inside the tuft.
            float rootX = tuftX + (float)(rng.NextDouble() - 0.5) * 2f * TuftJitter;
            float rootZ = tuftZ + (float)(rng.NextDouble() - 0.5) * 2f * TuftJitter;

            // Random roll of the flat ribbon around its own spine.
            float roll = (float)rng.NextDouble() * Mathf.PI * 2f;
            float ux = Mathf.Cos(roll);
            float uz = Mathf.Sin(roll);

            // Slight random lean toward a random compass direction, plus
            // a stronger quadratic bend (the "bent solid spike" look).
            float leanAz = (float)rng.NextDouble() * Mathf.PI * 2f;
            float lean = (float)rng.NextDouble() * LeanMaxRad;
            float leanSin = Mathf.Sin(lean);
            float azCos = Mathf.Cos(leanAz);
            float azSin = Mathf.Sin(leanAz);
            float bend = height * Mathf.Lerp(0.15f, 0.45f, (float)rng.NextDouble());

            // Spine centre and half-width at base (f=0), mid (f=0.5) and
            // tip (f=1); the tip half-width is 0, giving one tip vertex.
            float b0x = rootX, b0z = rootZ;
            float mOff = leanSin * height * 0.5f + bend * 0.25f;
            float mX = rootX + mOff * azCos, mZ = rootZ + mOff * azSin;
            float tOff = leanSin * height + bend;
            float tX = rootX + tOff * azCos, tZ = rootZ + tOff * azSin;

            float halfBase = width * 0.5f;
            float halfMid = width * 0.25f;

            // Five unique positions walked as the outline B0 -> M0 -> T ->
            // M1 -> B1; the tip sits at halfMid... tip width 0 (T only).
            float b0ax = b0x - ux * halfBase, b0az = b0z - uz * halfBase;
            float b0bx = b0x + ux * halfBase, b0bz = b0z + uz * halfBase;
            float m0ax = mX - ux * halfMid, m0az = mZ - uz * halfMid;
            float m1bx = mX + ux * halfMid, m1bz = mZ + uz * halfMid;

            int i = verts.Count;

            // Front face: 5 verts (B0, M0, T, M1, B1) fanned from B0.
            verts.Add(new Vector3(b0ax, BaseY, b0az));
            uvs.Add(new Vector2(0.5f, 0f));
            verts.Add(new Vector3(m0ax, BaseY + height * 0.5f, m0az));
            uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(new Vector3(tX, BaseY + height, tZ));
            uvs.Add(new Vector2(0.5f, 1f));
            verts.Add(new Vector3(m1bx, BaseY + height * 0.5f, m1bz));
            uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(new Vector3(b0bx, BaseY, b0bz));
            uvs.Add(new Vector2(0.5f, 0f));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            tris.Add(i); tris.Add(i + 3); tris.Add(i + 4);

            // Back face: the same 5 positions again, opposite winding, so
            // the blade is visible from both sides and each face keeps a
            // correct flat normal after RecalculateNormals.
            int j = i + 5;
            verts.Add(new Vector3(b0ax, BaseY, b0az));
            uvs.Add(new Vector2(0.5f, 0f));
            verts.Add(new Vector3(m0ax, BaseY + height * 0.5f, m0az));
            uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(new Vector3(tX, BaseY + height, tZ));
            uvs.Add(new Vector2(0.5f, 1f));
            verts.Add(new Vector3(m1bx, BaseY + height * 0.5f, m1bz));
            uvs.Add(new Vector2(0.5f, 0.5f));
            verts.Add(new Vector3(b0bx, BaseY, b0bz));
            uvs.Add(new Vector2(0.5f, 0f));
            tris.Add(j); tris.Add(j + 2); tris.Add(j + 1);
            tris.Add(j); tris.Add(j + 3); tris.Add(j + 2);
            tris.Add(j); tris.Add(j + 4); tris.Add(j + 3);
        }

        /// <summary>
        /// 1x4 vertical gradient (dark green at v = 0 to yellow-green at
        /// v = 1) sampled by the blade UVs; Bilinear + Clamp so the
        /// gradient is smooth and never repeats along the blade.
        /// </summary>
        private static Texture2D GradientTexture()
        {
            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixel(0, 0, GradientBase);
            tex.SetPixel(0, 1, Color.Lerp(GradientBase, GradientTip, 1f / 3f));
            tex.SetPixel(0, 2, Color.Lerp(GradientBase, GradientTip, 2f / 3f));
            tex.SetPixel(0, 3, GradientTip);
            tex.Apply();
            return tex;
        }
    }
}
