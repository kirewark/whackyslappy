using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Scatters cartoon props INSIDE the arena: a wooden fence just inside
    /// the wall line, round cartoon trees, half-buried rocks and flower
    /// clusters. The layout is deterministic (fixed System.Random seed,
    /// same style as ParkHorizon.Seed) and runs at arena build time, so
    /// the colliders are already in the physics broadphase before the
    /// player or any NPC CharacterController moves.
    ///
    /// The fence and the flowers are visual only (colliders destroyed,
    /// shadows off): the invisible GameLayers.PlayerBlocker walls already
    /// contain the player. Tree trunks (capsule) and rocks (sphere) do get
    /// colliders, on GameLayers.Environment, the layer the floor already
    /// uses and that GameBootstrap's layer-collision matrix already
    /// configures; no matrix entry and no player/NPC code is touched.
    ///
    /// Nothing spawns within 4 m of the origin (the player start). Tuning
    /// values are constants because a static builder has no Inspector.
    /// </summary>
    public static class ArenaProps
    {
        /// <summary>Round cartoon trees placed inside the arena.</summary>
        public const int TreeCount = 12;

        /// <summary>Half-buried rocks placed inside the arena.</summary>
        public const int RockCount = 14;

        private const int FlowerClusterCount = 6;
        private const int Seed = 31337;

        // Keep-outs and fence geometry (metres). Fence line: 0.6 m inside
        // the wall face at GameBootstrap.FloorHalf (arena half is 20 m).
        private const float PlayerKeepOut = 4f;
        private const float TreeKeepOut = 6f;
        private const float RockKeepOut = 4.5f;
        private const float FlowerKeepOut = 4f;
        private const float TreeTreeSpacing = 4f;
        private const float RockRockSpacing = 2.5f;
        private const float FlowerFlowerSpacing = 2f;
        private const float FenceInset = 0.6f;
        private const float FencePostSpacing = 2.5f;
        private const float FencePostW = 0.15f;
        private const float FencePostH = 1.1f;
        private const float FenceRailThick = 0.08f;
        private const float FenceRailDepth = 0.18f;
        private const float TreeFenceClear = 3f;
        // Widest canopy: foliage scale tops out at 3.2 m, so 1.6 m half
        // width plus stacking jitter.
        private const float TreeCanopyMaxHalf = 1.75f;
        private const float TrunkH = 2.2f;

        private static readonly Color FenceDark = new Color(0.40f, 0.25f, 0.14f);
        private static readonly Color FenceLight = new Color(0.55f, 0.38f, 0.22f);
        private static readonly Color TrunkBrown = new Color(0.42f, 0.28f, 0.16f);
        private static readonly Color LeafLight = new Color(0.45f, 0.72f, 0.32f);
        private static readonly Color LeafMid = new Color(0.36f, 0.64f, 0.28f);
        private static readonly Color LeafDark = new Color(0.28f, 0.56f, 0.26f);
        private static readonly Color StemGreen = new Color(0.28f, 0.50f, 0.22f);
        private static readonly Color PetalRed = new Color(0.90f, 0.25f, 0.25f);
        private static readonly Color PetalYellow = new Color(0.95f, 0.80f, 0.25f);
        private static readonly Color PetalPink = new Color(0.92f, 0.50f, 0.68f);

        /// <summary>
        /// Builds (or re-checks) the props under an "ArenaProps" child of
        /// parent. Idempotent like the other builders.
        /// </summary>
        public static void Build(GameObject parent)
        {
            if (parent == null)
                return;
            if (parent.transform.Find("ArenaProps") != null)
                return;

            var root = new GameObject("ArenaProps");
            root.transform.SetParent(parent.transform, false);

            var rng = new System.Random(Seed);
            BuildFence(root.transform);

            var spots = new List<Vector3>();
            BuildTrees(root.transform, rng, spots);
            var solids = new List<Vector3>(spots);
            BuildRocks(root.transform, rng, solids);
            BuildFlowers(root.transform, rng, solids);
        }

        /// <summary>
        /// Four fence runs 0.6 m inside the wall faces: posts every 2.5 m
        /// plus two thin rails per side, alternating brown tints. All
        /// colliders destroyed and shadows off — decoration only.
        /// </summary>
        private static void BuildFence(Transform parent)
        {
            float half = GameBootstrap.FloorHalf - FenceInset; // 19.4 m
            int posts = Mathf.FloorToInt(2f * half / FencePostSpacing) + 1;
            float pad = (2f * half - (posts - 1) * FencePostSpacing) * 0.5f;

            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float linePos = (side % 2 == 0 ? 1f : -1f) * half;

                for (int p = 0; p < posts; p++)
                {
                    float along = -half + pad + p * FencePostSpacing;
                    Vector3 pos = alongX
                        ? new Vector3(along, FencePostH * 0.5f, linePos)
                        : new Vector3(linePos, FencePostH * 0.5f, along);

                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = "FencePost_" + side + "_" + p;
                    post.transform.SetParent(parent, false);
                    post.transform.position = pos;
                    post.transform.localScale =
                        new Vector3(FencePostW, FencePostH, FencePostW);
                    RemoveCollider(post);
                    DisableShadow(post);
                    Tint(post, (p + side) % 2 == 0 ? FenceDark : FenceLight);
                }

                for (int r = 0; r < 2; r++)
                {
                    float railY = 0.4f + r * 0.45f;
                    var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rail.name = "FenceRail_" + side + "_" + r;
                    rail.transform.SetParent(parent, false);
                    rail.transform.position = alongX
                        ? new Vector3(0f, railY, linePos)
                        : new Vector3(linePos, railY, 0f);
                    // Cube primitives are 1 unit per axis, so the scale IS
                    // the size: length spans the whole side.
                    rail.transform.localScale = alongX
                        ? new Vector3(2f * half, FenceRailThick, FenceRailDepth)
                        : new Vector3(FenceRailDepth, FenceRailThick, 2f * half);
                    RemoveCollider(rail);
                    DisableShadow(rail);
                    Tint(rail, FenceDark);
                }
            }
        }

        /// <summary>
        /// TreeCount trees: cylinder trunk (radius 0.25, height 2.2 m)
        /// plus 2-3 stacked sphere puffs (bright greens, widest 1.8-3.2 m).
        /// CapsuleCollider on the trunk only, layer Environment.
        /// spots accumulates accepted centres.
        /// </summary>
        private static void BuildTrees(Transform parent, System.Random rng, List<Vector3> spots)
        {
            // Centre limit so the widest canopy stays TreeFenceClear from
            // the fence line.
            float limit = GameBootstrap.FloorHalf - FenceInset
                - TreeFenceClear - TreeCanopyMaxHalf;
            Color[] greens = { LeafLight, LeafMid, LeafDark };

            for (int i = 0; i < TreeCount; i++)
            {
                if (!TryPlace(rng, spots, TreeKeepOut, TreeTreeSpacing, limit, out Vector3 pos))
                    continue;
                spots.Add(pos);

                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Tree_" + i + "_Trunk";
                trunk.transform.SetParent(parent, false);
                // Sunk 0.1 m so the trunk base is buried.
                trunk.transform.position = pos + new Vector3(0f, TrunkH * 0.5f - 0.1f, 0f);
                // Cylinder primitive: radius 0.5, height 2 -> halve scales.
                trunk.transform.localScale = new Vector3(0.5f, TrunkH * 0.5f, 0.5f);
                trunk.layer = GameLayers.Environment;
                Tint(trunk, TrunkBrown);
                var capsule = trunk.AddComponent<CapsuleCollider>();
                capsule.radius = 0.5f;  // world radius after 0.5 trunk scale
                capsule.height = 2.2f;  // world height after 1.1 trunk scale

                // Stack 2-3 sphere puffs over the trunk top; diameters
                // 1.8-3.2 m then 72% and 50% of the widest.
                float widest = Mathf.Lerp(1.8f, 3.2f, (float)rng.NextDouble());
                int puffs = rng.Next(0, 3) == 0 ? 2 : 3;
                float top = pos.y + TrunkH - 0.1f;
                float d = widest;
                float cy = top + 0.30f * widest;
                float jx = 0f, jz = 0f;
                for (int p = 0; p < puffs; p++)
                {
                    if (p > 0)
                    {
                        float next = p == 1 ? 0.72f * widest : 0.5f * widest;
                        cy += (d + next) * 0.42f;
                        d = next;
                        jx += (float)(rng.NextDouble() - 0.5) * 0.24f;
                        jz += (float)(rng.NextDouble() - 0.5) * 0.24f;
                    }

                    var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    leaf.name = "Tree_" + i + "_Leaf" + p;
                    leaf.transform.SetParent(parent, false);
                    leaf.transform.position =
                        new Vector3(pos.x + jx, cy, pos.z + jz);
                    leaf.transform.localScale = new Vector3(d, d * 0.92f, d);
                    leaf.layer = GameLayers.Environment;
                    // Colliders only on trunks: a canopy collider at head
                    // height would block the player away from the trunk.
                    RemoveCollider(leaf);
                    Tint(leaf, greens[p % 3]);
                }
            }
        }

        /// <summary>
        /// RockCount rocks: an empty root on layer Environment with a
        /// SphereCollider (so the collider is never distorted by a non-
        /// uniform scale) plus a sphere child scaled irregularly
        /// 0.5-1.4 m, half-buried, grey tints.
        /// </summary>
        private static void BuildRocks(Transform parent, System.Random rng, List<Vector3> avoid)
        {
            float limit = GameBootstrap.FloorHalf - FenceInset - 1.5f;

            for (int i = 0; i < RockCount; i++)
            {
                if (!TryPlace(rng, avoid, RockKeepOut, RockRockSpacing, limit, out Vector3 pos))
                    continue;

                float sx = Mathf.Lerp(0.5f, 1.4f, (float)rng.NextDouble());
                float sy = Mathf.Lerp(0.5f, 1.4f, (float)rng.NextDouble());
                float sz = Mathf.Lerp(0.5f, 1.4f, (float)rng.NextDouble());
                float maxs = Mathf.Max(sx, Mathf.Max(sy, sz));

                var root = new GameObject("Rock_" + i);
                root.transform.SetParent(parent, false);
                // Centre 30% of the vertical size up: mostly half-buried.
                root.transform.position = pos + new Vector3(0f, sy * 0.3f, 0f);
                root.transform.rotation = Quaternion.Euler(
                    0f,
                    (float)rng.NextDouble() * 360f,
                    (float)(rng.NextDouble() - 0.5) * 30f);
                root.layer = GameLayers.Environment;
                var sphere = root.AddComponent<SphereCollider>();
                sphere.radius = maxs * 0.5f;

                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Rock_" + i + "_Mesh";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = new Vector3(sx, sy, sz);
                visual.layer = GameLayers.Environment;
                // The root's uniform SphereCollider is the rock's physics;
                // the stretched visual collider would be a bad duplicate.
                RemoveCollider(visual);
                float g = Mathf.Lerp(0.45f, 0.65f, (float)rng.NextDouble());
                Tint(visual, new Color(g, g, g * 1.04f));

                avoid.Add(pos);
            }
        }

        /// <summary>
        /// FlowerClusterCount clusters of 3-5 tiny sphere blooms on thin
        /// cylinder stems (red/yellow/pink). No colliders, shadows off.
        /// </summary>
        private static void BuildFlowers(Transform parent, System.Random rng, List<Vector3> avoid)
        {
            var own = new List<Vector3>();

            for (int i = 0; i < FlowerClusterCount; i++)
            {
                if (!TryPlace(rng, avoid, FlowerKeepOut, FlowerFlowerSpacing, 16f, out Vector3 pos))
                    continue;
                own.Add(pos);

                var cluster = new GameObject("Flowers_" + i);
                cluster.transform.SetParent(parent, false);
                cluster.transform.position = pos;

                int blooms = rng.Next(3, 6);
                for (int b = 0; b < blooms; b++)
                {
                    float ang = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.PI;
                    float off = 0.06f + (float)rng.NextDouble() * 0.22f;
                    var at = new Vector3(Mathf.Cos(ang) * off, 0f, Mathf.Sin(ang) * off);

                    float stemH = 0.2f + (float)rng.NextDouble() * 0.1f;
                    var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stem.name = "FlowerStem_" + i + "_" + b;
                    stem.transform.SetParent(cluster.transform, false);
                    // Cylinder primitive radius 0.5, height 2: halve scales
                    // (stem radius 0.015 m).
                    stem.transform.localScale = new Vector3(0.03f, stemH * 0.5f, 0.03f);
                    stem.transform.localPosition = at + new Vector3(0f, stemH * 0.5f, 0f);
                    RemoveCollider(stem);
                    DisableShadow(stem);
                    Tint(stem, StemGreen);

                    float bloom = 0.10f + (float)rng.NextDouble() * 0.05f;
                    var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    head.name = "Flower_" + i + "_" + b;
                    // Parented to the cluster (not the scaled stem) so the
                    // bloom's world size is its own scale.
                    head.transform.SetParent(cluster.transform, false);
                    head.transform.localPosition = at + new Vector3(0f, stemH + bloom * 0.3f, 0f);
                    head.transform.localScale = new Vector3(bloom, bloom * 0.6f, bloom);
                    RemoveCollider(head);
                    DisableShadow(head);
                    Tint(head, PetalColor(rng));
                }
            }
        }

        private static Color PetalColor(System.Random rng)
        {
            switch (rng.Next(0, 3))
            {
                case 0: return PetalRed;
                case 1: return PetalYellow;
                default: return PetalPink;
            }
        }

        /// <summary>
        /// Rejection sampler: uniform in [-limit, limit]^2, outside
        /// keepOut of the origin and at least spacing from every accepted
        /// point in avoid. Returns false after 40 tries so a crowded
        /// keeper never blocks the builder.
        /// </summary>
        private static bool TryPlace(
            System.Random rng, List<Vector3> avoid,
            float keepOut, float spacing, float limit, out Vector3 pos)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                pos = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * limit,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * limit);
                if (pos.magnitude < keepOut)
                    continue;

                bool clear = true;
                for (int a = 0; a < avoid.Count; a++)
                {
                    if ((avoid[a] - pos).sqrMagnitude < spacing * spacing)
                    {
                        clear = false;
                        break;
                    }
                }
                if (clear)
                    return true;
            }

            pos = Vector3.zero;
            return false;
        }

        // Same helper idioms as ParkHorizon / EnvironmentBuilder (private
        // to this file on purpose: each builder owns its own copies).
        private static void RemoveCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
        }

        private static void DisableShadow(GameObject go)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Tint(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
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
