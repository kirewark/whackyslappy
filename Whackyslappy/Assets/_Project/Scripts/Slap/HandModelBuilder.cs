// Whackyslappy/Assets/_Project/Scripts/Slap/HandModelBuilder.cs
using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// Builds a chunky cartoon white-glove open hand from primitives
    /// (rounded palm, four clearly splayed fingers with rounded tips, an
    /// outward-angled thumb, and a thin tapered wrist sleeve) with all
    /// colliders destroyed and shadows off. One-shot builder: no per-frame
    /// work.
    ///
    /// Convention (matches SlapHand's +Z-forward hand): fingers point along
    /// local +Z (forward), the thumb sits on the local +X side angled ~55 deg
    /// off the index direction and slightly out of the palm plane, and the
    /// back of the hand faces local +Y. The wrist is at local z = 0 (SlapHand
    /// pivots the model there). Palm ~0.10 wide (X) x ~0.05 thick (Y) x 0.10
    /// long (Z), fingers 0.024 dia fanned -24..+24 deg about local Y along a
    /// knuckle arc, and a thin (0.064 dia) sleeve extending backward along
    /// -Z so no flat end cap faces the camera in the default pose.
    /// </summary>
    public static class HandModelBuilder
    {
        // Cartoon white-glove colours: glove white and a slightly grey wrist
        // sleeve for the shared fallback materials.
        private static readonly Color s_gloveColor = new Color(0.97f, 0.97f, 0.97f);
        private static readonly Color s_sleeveColor = new Color(0.9f, 0.9f, 0.9f);

        // One shared glove material, and one shared sleeve material, created
        // lazily when Build is called without one.
        private static Material s_sharedMaterial;
        private static Material s_sleeveMaterial;

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
            var sleeveMat = material != null ? material : GetSharedSleeveMaterial();

            // Palm: three overlapping unit spheres scaled to a flattened
            // slab ~0.10 wide (X) x 0.05 thick (Y) x 0.10 long (Z), wrist
            // end at local z = 0, giving a rounded-box back-of-hand.
            var palmScale = new Vector3(0.1f, 0.05f, 0.1f);
            MakePart("Palm_Lower", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, 0.018f), Quaternion.identity, palmScale, handMat);
            MakePart("Palm_Mid", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, 0.055f), Quaternion.identity, palmScale, handMat);
            MakePart("Palm_Upper", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, 0.088f), Quaternion.identity, palmScale, handMat);

            // Four finger capsules on a knuckle arc (0.024 dia so neighbours
            // never touch), fanned -24/-8/+8/+24 deg about local Y: outer
            // fingers splay more, middle longest. The capsule primitive is
            // 2.0 tall along its local Y axis, so an Euler(90, fan, 0)
            // rotation points it forward and the yaw fans it outward.
            var fingerNames = new[] { "Finger_Index", "Finger_Middle", "Finger_Ring", "Finger_Pinky" };
            var fingerX = new[] { 0.040f, 0.013f, -0.013f, -0.050f };
            var fingerBaseZ = new[] { 0.085f, 0.097f, 0.092f, 0.065f };
            var fingerLen = new[] { 0.075f, 0.095f, 0.088f, 0.065f }; // middle longest, pinky shortest
            var fingerFanDeg = new[] { 24f, 8f, -8f, -24f };
            const float FingerDiameter = 0.024f;

            for (int fi = 0; fi < fingerNames.Length; fi++)
            {
                float fingerLength = fingerLen[fi];
                var fingerRot = Quaternion.Euler(90f, fingerFanDeg[fi], 0f);
                var fingerDir = fingerRot * Vector3.up; // capsule long axis, fanned outward
                var fingerCenter = new Vector3(fingerX[fi], 0f, fingerBaseZ[fi])
                    + fingerDir * (fingerLength * 0.5f);
                MakePart(fingerNames[fi], PrimitiveType.Capsule, root.transform,
                    fingerCenter, fingerRot,
                    new Vector3(FingerDiameter, fingerLength, FingerDiameter), handMat);

                // Rounded fingertip cap so ends are not hard-cut.
                MakePart("FingerTip_" + fingerNames[fi].Substring(7), PrimitiveType.Sphere,
                    root.transform,
                    new Vector3(fingerX[fi], 0f, fingerBaseZ[fi])
                        + fingerDir * fingerLength,
                    Quaternion.identity,
                    new Vector3(FingerDiameter, FingerDiameter, FingerDiameter), handMat);
            }

            // Thumb: base near the wrist on +X, aimed ~55 deg off the index
            // direction (index sits +24 from +Z, so the thumb axis is 79 deg
            // from +Z) and tilted 10 deg out of the palm plane so it
            // separates clearly from the finger fan.
            var thumbBase = new Vector3(0.05f, 0f, 0.025f);
            var thumbDir = new Vector3(0.978f, -0.174f, 0.122f);
            const float ThumbLength = 0.075f;
            const float ThumbDiameter = 0.03f;
            var thumbRot = Quaternion.LookRotation(thumbDir, Vector3.up);
            MakePart("Thumb", PrimitiveType.Capsule, root.transform,
                thumbBase + thumbDir * (ThumbLength * 0.5f), thumbRot,
                new Vector3(ThumbDiameter, ThumbLength, ThumbDiameter), handMat);
            MakePart("FingerTip_Thumb", PrimitiveType.Sphere, root.transform,
                thumbBase + thumbDir * ThumbLength, Quaternion.identity,
                new Vector3(ThumbDiameter, ThumbDiameter, ThumbDiameter), handMat);

            // Wrist sleeve: a thin capsule (0.064 dia - narrower than the
            // palm) running backward along -Z from the wrist, length 0.25,
            // slightly tapered by a smaller sphere at the far end. No flat
            // cylinder end cap ever faces the camera in the default pose.
            MakePart("Wrist_Sleeve", PrimitiveType.Capsule, root.transform,
                new Vector3(0f, 0f, -0.08f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.064f, 0.19f, 0.064f), sleeveMat);
            MakePart("Wrist_SleeveFar", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, -0.175f), Quaternion.identity,
                new Vector3(0.055f, 0.055f, 0.055f), sleeveMat);

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

        /// <summary>Returns the lazily created shared wrist-sleeve material
        /// (slightly grey glove opening), or null if no fallback shader
        /// survived the build.</summary>
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
