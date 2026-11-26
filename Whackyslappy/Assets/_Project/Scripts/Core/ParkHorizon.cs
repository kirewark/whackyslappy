using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Builds the distant cartoon backdrop that turns the floating arena
    /// into a park: a ring of low rounded hills and clustered simple trees
    /// outside the player walls, sized to fade into the horizon-matching
    /// fog. Visual only: every collider is destroyed, shadows are off and
    /// the default layer is kept, so nothing physics or AI touches changes.
    /// Placement is seeded and identical every run. Called by
    /// EnvironmentBuilder.Build after the ground plane exists; a second
    /// call does nothing while the "Horizon" parent exists.
    /// </summary>
    public static class ParkHorizon
    {
        // Tuning values are constants because a static builder has no
        // Inspector (same style as EnvironmentBuilder / GameBootstrap).
        // Hills sit beyond the walls (arena half is GameBootstrap.
        // FloorHalf = 20 m). A hill is an axis-aligned ellipsoid with
        // horizontal half-footprint max(sx, sz) / 2, so before placing
        // we push its centre out until its centre distance minus that
        // half-footprint keeps a HillClearance = 8 m gap to the ±20 m
        // wall square — the max-norm clearance test holds in every
        // direction, diagonals included (an axis-aligned 70 m-wide
        // hill at 55 m used to spill right into the arena corner).
        // They close the grey band between the arena and the old
        // 70..95 m ring.
        private const int Seed = 4711;
        private const int HillCount = 20;
        private const float HillRadiusMin = 55f;
        private const float HillRadiusMax = 80f;
        private const float HillClearance = 8f;
        // Part budget for tree props; with HillCount hills the whole
        // backdrop stays at or under 60 GameObjects.
        private const int TreePartBudget = 40;

        // Meadow patches: flat discs scattered in the 25..70 m band
        // between the walls and the hills. The bare meadow plane read as
        // one flat grey-green slab there; squashed cylinders in two
        // alternating greens break it up. Colliders removed (the horizon
        // never blocks the player); y = 0.01 clears the plane (whose top
        // face is at y = -0.05) so the discs never z-fight with it, and
        // is far below the wall tops so the arena walls still read.
        private const int MeadowPatchCount = 24;
        private const float MeadowPatchInner = 25f;
        private const float MeadowPatchOuter = 70f;
        // A disc is only placed when the WHOLE disc lies outside the
        // ±20 m arena square plus a MeadowPatchMargin = 2 m band:
        // max(|cx|, |cz|) >= FloorHalf + r + 2. A radial clamp cannot
        // express that (a 12 m disc at 25 m distance and 45 deg has its
        // centre inside the wall square to begin with), so failing
        // samples are rejected and redrawn, bounded like
        // ArenaProps.TryPlace.
        private const float MeadowPatchMargin = 2f;
        private const int MeadowPatchTries = 40;
        private const float MeadowPatchMinR = 6f;
        private const float MeadowPatchMaxR = 12f;
        private const float MeadowPatchY = 0.01f;
        private const float MeadowPatchThick = 0.055f;

        private static readonly Color HillGreen = new Color(0.30f, 0.55f, 0.28f);
        private static readonly Color HillHaze = new Color(0.55f, 0.72f, 0.62f);
        private static readonly Color TrunkColor = new Color(0.45f, 0.30f, 0.18f);
        private static readonly Color LeafLight = new Color(0.38f, 0.62f, 0.30f);
        private static readonly Color LeafDark = new Color(0.22f, 0.50f, 0.24f);
        private static readonly Color MeadowPatchA = new Color(0.40f, 0.62f, 0.30f);
        private static readonly Color MeadowPatchB = new Color(0.30f, 0.52f, 0.24f);

        /// <summary>
        /// Builds the "Horizon" child of parent: HillCount flattened,
        /// half-buried spheres on a ring, with clusters of trunk-plus-
        /// canopy trees on some of them. No colliders, no shadows.
        /// </summary>
        public static void Build(GameObject parent)
        {
            if (parent == null)
                return;
            if (parent.transform.Find("Horizon") != null)
                return;

            var horizon = new GameObject("Horizon");
            horizon.transform.SetParent(parent.transform, false);

            var rng = new System.Random(Seed);
            int treePartsLeft = TreePartBudget;

            BuildMeadowPatches(horizon.transform, rng);

            for (int i = 0; i < HillCount; i++)
            {
                // One hill per ring slot with jitter so spacing is even but
                // never a perfect circle.
                float angle = (i + (float)(rng.NextDouble() - 0.5) * 0.7f)
                    / HillCount * (Mathf.PI * 2f);
                float radius = Mathf.Lerp(HillRadiusMin, HillRadiusMax, (float)rng.NextDouble());
                float sx = Mathf.Lerp(40f, 70f, (float)rng.NextDouble());
                float sy = Mathf.Lerp(6f, 14f, (float)rng.NextDouble());
                float sz = Mathf.Lerp(30f, 50f, (float)rng.NextDouble());

                // Horizontal footprint check BEFORE placing: the
                // ellipsoid's wider half-axis reaches HillClearance short
                // of the centre, so push the centre out along the same
                // ray until d - half-footprint >= FloorHalf + 8. With
                // the current size ranges that never exceeds
                // HillRadiusMax; the shrink branch only guards future
                // tuning (a hill that cannot fit even at 80 m gets its
                // horizontal scale reduced instead).
                float halfFoot = Mathf.Max(sx, sz) * 0.5f;
                float need = GameBootstrap.FloorHalf + HillClearance + halfFoot;
                if (radius < need)
                {
                    if (need <= HillRadiusMax)
                    {
                        radius = need;
                    }
                    else
                    {
                        float maxFoot = HillRadiusMax - GameBootstrap.FloorHalf - HillClearance;
                        float shrink = maxFoot / halfFoot;
                        sx *= shrink;
                        sz *= shrink;
                        radius = HillRadiusMax;
                    }
                }

                // Centre at y = 0: exactly half the sphere is buried in the
                // meadow plane (whose top face is at y = -0.05).
                var hillPos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = "Hill_" + i;
                hill.transform.SetParent(horizon.transform, false);
                hill.transform.position = hillPos;
                hill.transform.localScale = new Vector3(sx, sy, sz);
                RemoveCollider(hill);
                DisableShadow(hill);

                // Farther hills lean toward the hazy blue-green so the ring
                // blends into the fog colour.
                float haze = Mathf.InverseLerp(HillRadiusMin, HillRadiusMax, radius);
                Tint(hill, Color.Lerp(HillGreen, HillHaze, 0.35f + 0.45f * haze));

                // Trees on most hills, within the remaining part budget.
                if (treePartsLeft >= 2 && rng.NextDouble() < 0.7)
                {
                    int trees = Mathf.Min(rng.Next(1, 4), treePartsLeft / 2);
                    for (int t = 0; t < trees; t++)
                    {
                        BuildTree(horizon.transform, rng, hillPos, sx, sy, sz);
                        treePartsLeft -= 2;
                    }
                }
            }
        }

        /// <summary>
        /// One tree (trunk cylinder + cone or sphere canopy) standing on
        /// the buried ellipsoid hill surface near its top. The canopy is a
        /// cone (cartoon pine) just over half the time, else a squashed
        /// round bush.
        /// </summary>
        private static void BuildTree(
            Transform horizon, System.Random rng,
            Vector3 hillPos, float sx, float sy, float sz)
        {
            // Offset from the hill centre; kept inside 0.5 of each half-
            // axis so the surface-height maths below stays real.
            float ox = (float)(rng.NextDouble() - 0.5) * sx * 0.5f;
            float oz = (float)(rng.NextDouble() - 0.5) * sz * 0.5f;
            float nx = ox / (sx * 0.5f);
            float nz = oz / (sz * 0.5f);
            float groundY = hillPos.y + sy * 0.5f
                * Mathf.Sqrt(Mathf.Max(0.05f, 1f - nx * nx - nz * nz));

            var basePos = hillPos + new Vector3(ox, groundY, oz);

            float trunkH = Mathf.Lerp(1.8f, 3.2f, (float)rng.NextDouble());
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "TreeTrunk";
            trunk.transform.SetParent(horizon, false);
            // Cylinders are centred, so the trunk bottom sinks 0.25 m into
            // the hillside instead of balancing on a hair-thin contact.
            trunk.transform.position = basePos + new Vector3(0f, trunkH * 0.5f - 0.25f, 0f);
            trunk.transform.localScale = new Vector3(0.45f, trunkH, 0.45f);
            RemoveCollider(trunk);
            DisableShadow(trunk);
            Tint(trunk, TrunkColor);

            bool pine = rng.NextDouble() < 0.55;
            float canopyH = pine
                ? Mathf.Lerp(4.0f, 6.5f, (float)rng.NextDouble())
                : Mathf.Lerp(3.0f, 4.5f, (float)rng.NextDouble());
            float canopyW = pine ? canopyH * 0.55f : canopyH * 1.2f;

            // PrimitiveType has no Cone (CS0117), so pines use one shared
            // procedural cone mesh (unit cone: base ring at local y = 0,
            // radius 0.5, apex at y = 1, pivot at the base). Material and
            // shadows are handled by Tint/DisableShadow like the other
            // props; the cone gets no collider at all.
            GameObject canopy;
            if (pine)
            {
                canopy = new GameObject("TreePineTop");
                canopy.AddComponent<MeshFilter>().sharedMesh = PineConeMesh();
                canopy.AddComponent<MeshRenderer>();
            }
            else
            {
                canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = "TreeCanopy";
                RemoveCollider(canopy);
            }
            canopy.transform.SetParent(horizon, false);
            // Canopy centre sits 0.42 of the canopy height above the
            // trunk top so it overlaps the trunk slightly, as before.
            // Spheres are centred on their transform; the cone mesh has
            // its pivot at the base, so its origin drops half a scaled
            // height to keep the same centre (scale y = canopyH).
            float canopyY = groundY + trunkH - 0.25f + canopyH * 0.42f;
            canopy.transform.position = basePos + new Vector3(
                0f, pine ? canopyY - canopyH * 0.5f : canopyY, 0f);
            canopy.transform.localScale = pine
                ? new Vector3(canopyW, canopyH, canopyW)
                : new Vector3(canopyW, canopyH * 0.9f, canopyW);
            DisableShadow(canopy);
            Tint(canopy, Color.Lerp(LeafDark, LeafLight, (float)rng.NextDouble()));
        }

        // One procedural cone shared by every pine canopy, built on first
        // use and never modified afterwards, so assigning it to several
        // MeshFilters as sharedMesh is safe.
        private static Mesh m_pineConeMesh;

        /// <summary>
        /// Unit cartoon cone: 12-vertex base ring (radius 0.5) at y = 0,
        /// one apex at y = 1, plus the base-centre vertex for the bottom
        /// cap. Height 1 and radius 0.5 in mesh space mean a world scale
        /// of (width, height, width) produces the wanted proportions.
        /// </summary>
        private static Mesh PineConeMesh()
        {
            if (m_pineConeMesh != null)
                return m_pineConeMesh;

            const int Segments = 12;
            // Vertex 0 = base centre, 1 = apex, 2..13 = base ring walking
            // counter-clockwise around +Y.
            var verts = new Vector3[Segments + 2];
            verts[0] = new Vector3(0f, 0f, 0f);
            verts[1] = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < Segments; i++)
            {
                float a = 2f * Mathf.PI * i / Segments;
                verts[2 + i] = new Vector3(0.5f * Mathf.Cos(a), 0f, 0.5f * Mathf.Sin(a));
            }

            // Unity culls front faces as triangles seen CLOCKWISE from the
            // viewer, and RecalculateNormals puts the face normal on the
            // side its vertices wind clockwise from. So winding each
            // triangle clockwise as seen from OUTSIDE the cone gives
            // outward normals: sides apex -> next -> base (the ring walks
            // CCW from above), base cap base -> next -> centre (clockwise
            // seen from below, normal -Y).
            var tris = new int[Segments * 6];
            int t = 0;
            for (int i = 0; i < Segments; i++)
            {
                int next = (i + 1) % Segments;
                tris[t++] = 1;
                tris[t++] = 2 + next;
                tris[t++] = 2 + i;
                tris[t++] = 2 + i;
                tris[t++] = 2 + next;
                tris[t++] = 0;
            }

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            m_pineConeMesh = mesh;
            return m_pineConeMesh;
        }

        /// <summary>
        /// MeadowPatchCount flattened cylinders (radius 6..12 m) at random
        /// positions in the 25..70 m band, REJECT-AND-RESAMPLED until the
        /// whole disc lies outside the arena square plus a 2 m band
        /// (max(|cx|, |cz|) >= FloorHalf + r + 2); after MeadowPatchTries
        /// failed draws the patch is skipped. Rejection instead of a
        /// clamp, because a radial min-distance cannot express the square
        /// test: a 12 m disc at 25 m and 45 degrees has its centre inside
        /// the ±20 m walls to begin with. No colliders; y = 0.01 above the
        /// meadow plane so no z-fight.
        /// </summary>
        private static void BuildMeadowPatches(Transform horizon, System.Random rng)
        {
            for (int i = 0; i < MeadowPatchCount; i++)
            {
                float cx = 0f, cz = 0f, r = 0f;
                bool placed = false;
                for (int tryIdx = 0; tryIdx < MeadowPatchTries; tryIdx++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist = Mathf.Lerp(MeadowPatchInner, MeadowPatchOuter,
                        (float)rng.NextDouble());
                    r = Mathf.Lerp(MeadowPatchMinR, MeadowPatchMaxR,
                        (float)rng.NextDouble());
                    cx = Mathf.Cos(angle) * dist;
                    cz = Mathf.Sin(angle) * dist;

                    float reach = GameBootstrap.FloorHalf + r + MeadowPatchMargin;
                    if (Mathf.Max(Mathf.Abs(cx), Mathf.Abs(cz)) < reach)
                        continue;

                    // And keep the far rim under the tree line (78 m).
                    if (dist + r > MeadowPatchOuter + 8f)
                        continue;

                    placed = true;
                    break;
                }

                if (!placed)
                    continue;

                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "MeadowPatch_" + i;
                disc.transform.SetParent(horizon, false);
                disc.transform.position = new Vector3(cx, MeadowPatchY, cz);
                disc.transform.localScale = new Vector3(
                    r * 2f, MeadowPatchThick, r * 2f);
                RemoveCollider(disc);
                Tint(disc, i % 2 == 0 ? MeadowPatchA : MeadowPatchB);
            }
        }

        /// <summary>
        /// Removes the collider CreatePrimitive added. DestroyImmediate
        /// mirrors the ground-plane code: builders run at scene init,
        /// before the first physics step.
        /// </summary>
        private static void RemoveCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
        }

        /// <summary>
        /// Horizon props never cast shadows: they are far away and dozens
        /// of big shadow-casting spheres would waste WebGL draw budget.
        /// </summary>
        private static void DisableShadow(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Gives the object its own DefaultLitMaterial base tinted to
        /// color (repo material pattern: never Shader.Find, never touch a
        /// primitive's stripped built-in sharedMaterial).
        /// </summary>
        private static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;
            Material mat = DefaultLitMaterial.Create();
            if (mat == null)
                return;
            mat.color = color;
            renderer.material = mat;
        }
    }
}
