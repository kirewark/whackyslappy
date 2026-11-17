// Whackyslappy/Assets/_Project/Scripts/Slap/HandModelBuilder.cs
//
// Cartoon mitten-glove geometry (metres at scale 1; wrist at the model
// origin, fingers point +Z, thumb on -X, palm normal -Y). That makes it
// a RIGHT hand (palm down, fingers forward: a right hand has its thumb
// on -X). Every limb is attached through an empty base pivot at its
// attachment point, so it rotates about its BASE (Unity capsules rotate
// about their CENTRE, which made the old fingers float off the palm).
// Capsule child of each pivot:
// localRotation Euler(90,0,0), localScale (dia, length/2, dia),
// localPosition (0, 0, length/2 - 0.01) so the base end overlaps 0.01 m
// into the part it attaches to.
//
// | Part          | type   | pivot pos (x, y, z)  | pivot Euler     | dia    | length |
// |---------------|--------|----------------------|-----------------|--------|--------|
// | Palm          | sphere | - (at 0, 0, 0.06)    | -               | scale (0.12, 0.05, 0.11), spans x +-0.06, z 0.005..0.115 |
// | Finger_Index  | capsule| (-0.045, 0, 0.095)  | (0, -6, 0)      | 0.030  | 0.075  |
// | Finger_Middle | capsule| (-0.015, 0, 0.095)  | (0, -2, 0)      | 0.030  | 0.090  |
// | Finger_Ring   | capsule| (+0.015, 0, 0.095)   | (0, +2, 0)      | 0.030  | 0.080  |
// | Finger_Pinky  | capsule| (+0.045, 0, 0.095)   | (0, +6, 0)      | 0.030  | 0.060  |
// | Thumb         | capsule| (-0.050, 0, 0.035)  | (0, -50, 0)     | 0.034  | 0.070  |
// | Wrist_Sleeve  | capsule| (0, 0, 0.030)       | (0, 180, 0)     | 0.085  | 0.100  |
//
// Total reach ~0.19 m forward of the wrist (middle fingertip z ~0.185)
// and 0.10 m of sleeve backward. No cuff disc, no flat end cap facing +Z.
using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// Builds a chunky cartoon white-glove MITTEN from primitives: a
    /// flattened sphere palm, four capsule fingers and one capsule thumb,
    /// each hinged on its own base pivot on the palm edge, plus one short
    /// light-grey wrist sleeve running backward from the wrist. All
    /// colliders destroyed, shadows off. One-shot builder: no per-frame
    /// work.
    /// </summary>
    public static class HandModelBuilder
    {
        // Cartoon white-glove colours: glove white and a slightly grey
        // wrist sleeve for the shared fallback materials.
        private static readonly Color s_gloveColor = new Color(0.97f, 0.97f, 0.97f);
        private static readonly Color s_sleeveColor = new Color(0.9f, 0.9f, 0.9f);

        // One shared glove material, and one shared sleeve material, created
        // lazily when Build is called without one.
        private static Material s_sharedMaterial;
        private static Material s_sleeveMaterial;

        /// <summary>
        /// Creates a "HandModel" root under <paramref name="parent"/> (local
        /// position zero, identity rotation, scale one) holding the cartoon
        /// mitten and returns its Transform. If <paramref name="material"/>
        /// is null, every part shares a lazily created white-glove material
        /// (the sleeve uses a light-grey one). Prefer passing the existing
        /// hand renderer's material: Shader.Find can return null in stripped
        /// WebGL builds, so keep the fallback shaders in Project Settings >
        /// Graphics > Always Included Shaders, or replace this fallback with
        /// a material asset later.
        /// </summary>
        public static Transform Build(Transform parent, Material material = null)
        {
            var root = new GameObject("HandModel");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var handMat = material != null ? material : GetSharedMaterial();
            var sleeveMat = material != null ? material : GetSharedSleeveMaterial();

            // Palm: one flattened unit sphere ~0.12 wide (X) x 0.05 thick
            // (Y) x 0.11 long (Z), centred at (0, 0, 0.06) so it spans
            // x +-0.06 and z 0.005..0.115 (wrist end near local z = 0).
            var palmGo = MakePart("Palm", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, 0.06f), Quaternion.identity,
                new Vector3(0.12f, 0.05f, 0.11f), handMat);
            DestroyPartCollider(palmGo);

            // Four fingers: knuckle pivots sit on the palm front edge at
            // z = 0.095, y = 0, 0.030 diameter so neighbours exactly touch
            // (0.030 spacing). Small yaws about Y fan them slightly; the
            // X signs are mirrored for the RIGHT hand (index at x -0.045,
            // pinky at +0.045).
            Limb(root.transform, "Finger_Index",
                new Vector3(-0.045f, 0f, 0.095f), new Vector3(0f, -6f, 0f),
                0.030f, 0.075f, handMat);
            Limb(root.transform, "Finger_Middle",
                new Vector3(-0.015f, 0f, 0.095f), new Vector3(0f, -2f, 0f),
                0.030f, 0.090f, handMat);
            Limb(root.transform, "Finger_Ring",
                new Vector3(0.015f, 0f, 0.095f), new Vector3(0f, 2f, 0f),
                0.030f, 0.080f, handMat);
            Limb(root.transform, "Finger_Pinky",
                new Vector3(0.045f, 0f, 0.095f), new Vector3(0f, 6f, 0f),
                0.030f, 0.060f, handMat);

            // Thumb: pivot on the palm -X side near the wrist (right
            // hand), yawed -50 deg about Y so it points forward-outward
            // like a mitten thumb.
            Limb(root.transform, "Thumb",
                new Vector3(-0.050f, 0f, 0.035f), new Vector3(0f, -50f, 0f),
                0.034f, 0.070f, handMat);

            // Wrist sleeve: ONE short fat capsule running backward. The
            // pivot Euler(0,180,0) flips the child's local +Z to model -Z,
            // so the sleeve extends behind the wrist with no flat end cap
            // facing the camera in the default pose.
            Limb(root.transform, "Wrist_Sleeve",
                new Vector3(0f, 0f, 0.030f), new Vector3(0f, 180f, 0f),
                0.085f, 0.100f, sleeveMat);

#if UNITY_EDITOR
            // One-time sanity print of the assembled world bounds so the
            // Editor console shows the glove is where the geometry table
            // says it is. Build-time only, never per-frame.
            var boundsBox = new Bounds(root.transform.position, Vector3.zero);
            var partRenderers = root.GetComponentsInChildren<MeshRenderer>();
            for (int bi = 0; bi < partRenderers.Length; bi++)
            {
                boundsBox.Encapsulate(partRenderers[bi].bounds);
            }
            Debug.Log("[HandModelBuilder] HandModel world bounds center "
                + boundsBox.center + ", size " + boundsBox.size
                + " (at parent scale " + parent.lossyScale + ").");
#endif

            return root.transform;
        }

        /// <summary>Creates a limb as an empty base-pivot GameObject at
        /// <paramref name="pivotPos"/> rotated Euler(<paramref name=
        /// "pivotEuler"/>) under <paramref name="parent"/>, then attaches a
        /// capsule child pointing along the pivot's local +Z: the Unity
        /// capsule axis is local Y, so the child gets localRotation
        /// Euler(90,0,0), localScale (diameter, length/2, diameter) (the
        /// primitive is 2 units tall, 1 wide) and localPosition
        /// (0, 0, length/2 - 0.01) so its base end sits 0.01 m behind the
        /// pivot, overlapping the part it attaches to. Returns the pivot
        /// Transform. Collider removed, material applied, shadows off.</summary>
        private static Transform Limb(Transform parent, string limbName,
            Vector3 pivotPos, Vector3 pivotEuler,
            float diameter, float length, Material mat)
        {
            var pivotGo = new GameObject(limbName + "_Pivot");
            pivotGo.transform.SetParent(parent, false);
            pivotGo.transform.localPosition = pivotPos;
            pivotGo.transform.localRotation = Quaternion.Euler(pivotEuler);

            var limbGo = MakePart(limbName, PrimitiveType.Capsule, pivotGo.transform,
                new Vector3(0f, 0f, length * 0.5f - 0.01f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(diameter, length * 0.5f, diameter), mat);
            DestroyPartCollider(limbGo);
            return pivotGo.transform;
        }

        /// <summary>Creates one primitive part, strips its collider and
        /// shadows, and applies the shared material.</summary>
        private static GameObject MakePart(
            string partName, PrimitiveType type, Transform parent,
            Vector3 localPos, Quaternion localRot, Vector3 localScale, Material mat)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = localRot;
            part.transform.localScale = localScale;

            // Visual-only: SlapHand keeps the physics hitbox on its own cube.
            // Object.Destroy is the runtime-safe pattern (DestroyImmediate
            // during physics callbacks is not).
            var partRenderer = part.GetComponent<MeshRenderer>();
            if (partRenderer != null)
            {
                partRenderer.shadowCastingMode = ShadowCastingMode.Off;
                partRenderer.receiveShadows = false;
                if (mat != null) partRenderer.sharedMaterial = mat;
            }
            return part;
        }

        private static void DestroyPartCollider(GameObject partGo)
        {
            var partCollider = partGo.GetComponent<Collider>();
            if (partCollider != null) Object.Destroy(partCollider);
        }

        /// <summary>Returns the lazily created shared white-glove material
        /// (cartoon white, matte), or null if no fallback shader survived
        /// the build.</summary>
        private static Material GetSharedMaterial()
        {
            if (s_sharedMaterial != null) return s_sharedMaterial;

            var shader = FindFallbackShader();
            if (shader == null) return null;

            s_sharedMaterial = new Material(shader);
            s_sharedMaterial.color = s_gloveColor;
            // URP Lit reads _BaseColor; other shaders ignore it harmlessly.
            s_sharedMaterial.SetColor("_BaseColor", s_gloveColor);
            ApplyMatte(s_sharedMaterial);
            return s_sharedMaterial;
        }

        /// <summary>Returns the lazily created shared wrist-sleeve material
        /// (light grey, matte), or null if no fallback shader survived the
        /// build.</summary>
        private static Material GetSharedSleeveMaterial()
        {
            if (s_sleeveMaterial != null) return s_sleeveMaterial;

            var shader = FindFallbackShader();
            if (shader == null) return null;

            s_sleeveMaterial = new Material(shader);
            s_sleeveMaterial.color = s_sleeveColor;
            s_sleeveMaterial.SetColor("_BaseColor", s_sleeveColor);
            ApplyMatte(s_sleeveMaterial);
            return s_sleeveMaterial;
        }

        /// <summary>Shared matte setup for the glove materials.</summary>
        private static void ApplyMatte(Material mat)
        {
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
        }

        /// <summary>Shader.Find can return null in WebGL builds when shaders
        /// are stripped: add these to Always Included Shaders, or pass a real
        /// material asset into Build() and this path never runs.</summary>
        private static Shader FindFallbackShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[HandModelBuilder] No fallback shader found; "
                    + "hand parts keep their default material.");
            }
            return shader;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain reload can be disabled: drop the cached materials so
            // each play session builds fresh ones.
            s_sharedMaterial = null;
            s_sleeveMaterial = null;
        }
    }
}
