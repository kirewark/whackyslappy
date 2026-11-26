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
        // Hills sit far beyond the walls (arena half is GameBootstrap.
        // FloorHalf = 20 m; even the widest hill's near edge stays ~35 m
        // from the origin), so players can never reach them.
        private const int Seed = 4711;
        private const int HillCount = 20;
        private const float HillRadiusMin = 70f;
        private const float HillRadiusMax = 95f;
        // Part budget for tree props; with HillCount hills the whole
        // backdrop stays at or under 60 GameObjects.
        private const int TreePartBudget = 40;

        private static readonly Color HillGreen = new Color(0.30f, 0.55f, 0.28f);
        private static readonly Color HillHaze = new Color(0.55f, 0.72f, 0.62f);
        private static readonly Color TrunkColor = new Color(0.45f, 0.30f, 0.18f);
        private static readonly Color LeafLight = new Color(0.38f, 0.62f, 0.30f);
        private static readonly Color LeafDark = new Color(0.22f, 0.50f, 0.24f);

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

            for (int i = 0; i < HillCount; i++)
            {
                // One hill per ring slot with jitter so spacing is even but
                // never a perfect circle.
                float angle = (i + (float)(rng.NextDouble() - 0.5) * 0.7)
                    / HillCount * (Mathf.PI * 2f);
                float radius = Mathf.Lerp(HillRadiusMin, HillRadiusMax, (float)rng.NextDouble());
                float sx = Mathf.Lerp(40f, 70f, (float)rng.NextDouble());
                float sy = Mathf.Lerp(6f, 14f, (float)rng.NextDouble());
                float sz = Mathf.Lerp(30f, 50f, (float)rng.NextDouble());

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
            float ox = (float)(rng.NextDouble() - 0.5) * sx * 0.5;
            float oz = (float)(rng.NextDouble() - 0.5) * sz * 0.5;
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

            var canopy = GameObject.CreatePrimitive(pine ? PrimitiveType.Cone : PrimitiveType.Sphere);
            canopy.name = pine ? "TreePineTop" : "TreeCanopy";
            canopy.transform.SetParent(horizon, false);
            // Cones and spheres are centred too; 0.42 of the canopy height
            // above the trunk top overlaps it slightly.
            float canopyY = groundY + trunkH - 0.25f + canopyH * 0.42f;
            canopy.transform.position = basePos + new Vector3(0f, canopyY, 0f);
            canopy.transform.localScale = pine
                ? new Vector3(canopyW, canopyH, canopyW)
                : new Vector3(canopyW, canopyH * 0.9f, canopyW);
            RemoveCollider(canopy);
            DisableShadow(canopy);
            Tint(canopy, Color.Lerp(LeafDark, LeafLight, (float)rng.NextDouble()));
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
