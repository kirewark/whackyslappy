// Whackyslappy/Assets/_Project/Scripts/Slap/HandModelBuilder.cs
using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// Builds a chunky cartoon white-glove open hand from primitives
    /// (rounded palm, four slightly fanned fingers, outward-angled thumb,
    /// wrist cuff) with all colliders destroyed and shadows off. One-shot
    /// builder: no per-frame work.
    ///
    /// Convention (matches SlapHand's +Z-forward hand): fingers point along
    /// local +Z (forward), the thumb sits on the local -X side (right hand:
    /// SlapHand's Rz(-90) roll maps -X to +Y so the thumb is up), and the
    /// palm's flat face normal points along local -Y (mapped to -X, screen
    /// centre). Overall size is about 0.22 m wide (X) x 0.06 m thick (Y) x
    /// 0.30 m long (Z); the palm's -Z end is the wrist, where the cuff sits.
    /// </summary>
    public static class HandModelBuilder
    {
        // Cartoon white-glove colours: glove white and a slightly grey
        // wrist cuff for the shared fallback materials.
        private static readonly Color s_gloveColor = new Color(0.97f, 0.97f, 0.97f);
        private static readonly Color s_cuffColor = new Color(0.85f, 0.85f, 0.85f);

        // One shared glove material, and one shared cuff material, created
        // lazily when Build is called without one.
        private static Material s_sharedMaterial;
        private static Material s_cuffMaterial;

        /// <summary>
        /// Creates a "HandModel" root under <paramref name="parent"/> (local
        /// position zero, identity rotation, scale one) holding the cartoon
        /// hand and returns its Transform. If <paramref name="material"/> is
        /// null, every part shares a lazily created skin-tone material.
        /// Prefer passing the existing hand renderer's material: Shader.Find
        /// can return null in stripped WebGL builds, so keep the fallback
        /// shaders in Project Settings > Graphics > Always Included Shaders,
        /// or replace this fallback with a material asset later.
        /// </summary>
        public static Transform Build(Transform parent, Material material = null)
        {
            var root = new GameObject("HandModel");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var handMat = material != null ? material : GetSharedMaterial();
            var cuffMat = material != null ? material : GetSharedCuffMaterial();

            // Palm: unit sphere scaled into a rounded slab, sitting toward the
            // back so the fingers bring the total length to about 0.30 m.
            MakePart("Palm", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, -0.06f), Quaternion.identity,
                new Vector3(0.18f, 0.055f, 0.17f), handMat);

            // Four fat finger capsules fanned slightly in the X-Z plane.
            // The capsule primitive is 2.0 tall along its local Y axis, so an
            // Euler(90, fan, 0) rotation points it forward and Yaw fans it.
            var fingerNames = new[] { "Finger_Index", "Finger_Middle", "Finger_Ring", "Finger_Pinky" };
            var fingerX = new[] { 0.055f, 0.018f, -0.018f, -0.065f };
            var fingerLen = new[] { 0.115f, 0.135f, 0.110f, 0.085f }; // middle longest, pinky shortest
            var fingerFanDeg = new[] { 6f, 0f, -5f, -11f };
            const float FingerDiameter = 0.042f; // fat, cartoon proportions
            const float FingerBaseZ = 0.02f;     // where the fingers meet the palm

            for (int fi = 0; fi < fingerNames.Length; fi++)
            {
                float fingerLength = fingerLen[fi];
                var fingerRot = Quaternion.Euler(90f, fingerFanDeg[fi], 0f);
                var fingerDir = fingerRot * Vector3.up; // capsule long axis, fanned forward
                var fingerCenter = new Vector3(fingerX[fi], 0f, FingerBaseZ)
                    + fingerDir * (fingerLength * 0.5f);
                MakePart(fingerNames[fi], PrimitiveType.Capsule, root.transform,
                    fingerCenter, fingerRot,
                    new Vector3(FingerDiameter, fingerLength, FingerDiameter), handMat);
            }

            // Thumb: fat capsule angled about 40 degrees outward on -X
            // (right-hand side of the model; SlapHand's Rz(-90) roll maps
            // -X to +Y so the thumb points up with fingers forward).
            var thumbRot = Quaternion.Euler(90f, -40f, 0f);
            var thumbDir = thumbRot * Vector3.up;
            var thumbBase = new Vector3(-0.062f, 0f, -0.02f);
            const float ThumbLength = 0.085f;
            const float ThumbDiameter = 0.050f;
            MakePart("Thumb", PrimitiveType.Capsule, root.transform,
                thumbBase + thumbDir * (ThumbLength * 0.5f), thumbRot,
                new Vector3(ThumbDiameter, ThumbLength, ThumbDiameter), handMat);

            // Cuff: short fat cylinder over the wrist end (palm's -Z end
            // sits near z -0.145), slightly grey to read as the glove's
            // opening. The capsule/cylinder primitive is 2.0 tall along
            // local Y, so Euler(90,0,0) points its axis forward along Z;
            // cross-section sizes are chosen for SlapHand's anisotropic
            // pivot scale (x -> upright width, z -> screen thickness).
            MakePart("Cuff", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, -0.13f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.12f, 0.05f, 0.06f), cuffMat);

            return root.transform;
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
            var partCollider = part.GetComponent<Collider>();
            if (partCollider != null) Object.Destroy(partCollider);

            var partRenderer = part.GetComponent<MeshRenderer>();
            if (partRenderer != null)
            {
                partRenderer.shadowCastingMode = ShadowCastingMode.Off;
                partRenderer.receiveShadows = false;
                if (mat != null) partRenderer.sharedMaterial = mat;
            }
            return part;
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
            // Matte cartoon glove: low smoothness, non-metallic, only when
            // the shader actually exposes the property (URP Lit uses
            // _Smoothness, the built-in Standard shader _Glossiness).
            ApplyMatte(s_sharedMaterial);
            return s_sharedMaterial;
        }

        /// <summary>Returns the lazily created shared cuff material
        /// (slightly grey glove opening), or null if no fallback shader
        /// survived the build.</summary>
        private static Material GetSharedCuffMaterial()
        {
            if (s_cuffMaterial != null) return s_cuffMaterial;

            var shader = FindFallbackShader();
            if (shader == null) return null;

            s_cuffMaterial = new Material(shader);
            s_cuffMaterial.color = s_cuffColor;
            s_cuffMaterial.SetColor("_BaseColor", s_cuffColor);
            ApplyMatte(s_cuffMaterial);
            return s_cuffMaterial;
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
            s_cuffMaterial = null;
        }
    }
}
