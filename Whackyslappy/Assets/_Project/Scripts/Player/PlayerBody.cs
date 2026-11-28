// Whackyslappy/Assets/_Project/Scripts/Player/PlayerBody.cs
using UnityEngine;
using UnityEngine.Rendering;
using WhackySlappy.Core;
using WhackySlappy.NPC;

namespace WhackySlappy.Player
{
    /// <summary>Visual-only NPC-like body under the player root: torso,
    /// legs and a head whose renderer is off (the eye camera sits inside
    /// it), turning with body yaw, not camera pitch. Colliders are
    /// destroyed; no rigidbodies, joints or arms (the SlapHand, the
    /// first-person hand model, stays as the only arm-like visual).</summary>
    public class PlayerBody : MonoBehaviour
    {
        // Torso/head scales copied from NpcController.Create's literals:
        // primitive capsule is 2 high, 1 wide; head sphere is 1 wide.
        private const float kTorsoScaleXZ = 0.7f;
        private const float kTorsoScaleY = 0.45f;
        private const float kHeadScale = 0.6f;

        // Legs use a lighter tint, same formula as NPC limbs in Create.
        [SerializeField] private Color m_bodyColor = new Color(0.85f, 0.45f, 0.25f, 1f);

        // The eye camera pivot is at y 1.6 with a 0.3 near plane; keeping
        // the torso top at 1.45, pushed 0.18 back, clears look-down.
        [SerializeField] private float m_torsoTopY = 1.45f;
        [SerializeField] private float m_torsoBackOffset = 0.18f;

        /// <summary>Hip pivot of the right leg, for a later task.</summary>
        public Transform RightLegRoot { get; private set; }
        private Material m_limbMat;
        private Renderer[] m_rightLegRenderers;

        private void Awake()
        {
            Material bodyMat = DefaultLitMaterial.Create();
            if (bodyMat != null) bodyMat.color = m_bodyColor;
            Material limbMat = GetLimbMaterial();
            // NPC hips sit 0.8 m up; derived from the shared leg constants
            // so the lower capsules land exactly on y = 0 (the feet).
            float legRootY = 2f * NpcBodyBuilder.LegLength - NpcBodyBuilder.Overlap;
            float hipY = legRootY - NpcBodyBuilder.HipY;
            MakePart("Body_Torso", PrimitiveType.Capsule, transform,
                new Vector3(0f, m_torsoTopY - kTorsoScaleY, -m_torsoBackOffset),
                new Vector3(kTorsoScaleXZ, kTorsoScaleY, kTorsoScaleXZ), bodyMat);
            GameObject head = MakePart("Body_Head", PrimitiveType.Sphere, transform,
                new Vector3(0f, hipY + 1f, -m_torsoBackOffset),
                new Vector3(kHeadScale, kHeadScale, kHeadScale), bodyMat);
            Renderer headRenderer = head.GetComponent<Renderer>();
            if (headRenderer != null) headRenderer.enabled = false;
            RightLegRoot = BuildLeg("Leg_R", NpcBodyBuilder.HipX, legRootY, limbMat);
            m_rightLegRenderers = RightLegRoot.GetComponentsInChildren<Renderer>();
            BuildLeg("Leg_L", -NpcBodyBuilder.HipX, legRootY, limbMat);
        }

        /// <summary>Hides the standing right leg during a leg sweep and
        /// restores it afterwards; renderers only, no collider changes.</summary>
        public void SetRightLegVisible(bool visible)
        {
            if (m_rightLegRenderers == null)
                return;
            for (int i = 0; i < m_rightLegRenderers.Length; i++)
                if (m_rightLegRenderers[i] != null)
                    m_rightLegRenderers[i].enabled = visible;
        }

        /// <summary>Builds a copy of the NPC-style leg for the sweep visual:
        /// root with the same two capsules (radius, length, colour) down its
        /// local -Y, no colliders, shadows off, layer Player; the caller
        /// parents and places it.</summary>
        public GameObject CreateSweepLeg()
        {
            GameObject root = new GameObject("LegSweep_NpcLeg");
            Vector3 legScale = new Vector3(2f * NpcBodyBuilder.LegRadius,
                NpcBodyBuilder.LegLength * 0.5f, 2f * NpcBodyBuilder.LegRadius);
            Material mat = GetLimbMaterial();
            MakePart("SweepLeg_Upper", PrimitiveType.Capsule, root.transform,
                new Vector3(0f, -NpcBodyBuilder.LegLength * 0.5f, 0f), legScale, mat);
            MakePart("SweepLeg_Lower", PrimitiveType.Capsule, root.transform,
                new Vector3(0f, -1.5f * NpcBodyBuilder.LegLength + NpcBodyBuilder.Overlap, 0f), legScale, mat);
            return root;
        }

        /// <summary>Limb tint material, lazily created so external callers
        /// get the same colour as the standing legs.</summary>
        private Material GetLimbMaterial()
        {
            if (m_limbMat == null)
            {
                m_limbMat = DefaultLitMaterial.Create();
                if (m_limbMat != null)
                {
                    Color c = m_bodyColor;
                    m_limbMat.color = new Color(c.r * 1.15f + 0.05f,
                        c.g * 1.15f + 0.05f, c.b * 1.15f + 0.05f, c.a);
                }
            }
            return m_limbMat;
        }

        /// <summary>Leg pivot at the NPC hip offset with NPC capsules.</summary>
        private Transform BuildLeg(string legName, float x, float legRootY, Material mat)
        {
            GameObject pivot = new GameObject(legName);
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = new Vector3(x, legRootY, 0f);

            Vector3 legScale = new Vector3(2f * NpcBodyBuilder.LegRadius,
                NpcBodyBuilder.LegLength * 0.5f, 2f * NpcBodyBuilder.LegRadius);
            MakePart(legName + "_Upper", PrimitiveType.Capsule, pivot.transform,
                new Vector3(0f, -NpcBodyBuilder.LegLength * 0.5f, 0f), legScale, mat);
            MakePart(legName + "_Lower", PrimitiveType.Capsule, pivot.transform,
                new Vector3(0f, -1.5f * NpcBodyBuilder.LegLength
                    + NpcBodyBuilder.Overlap, 0f), legScale, mat);
            return pivot.transform;
        }

        /// <summary>Primitive part with collider destroyed, shadows off.</summary>
        private GameObject MakePart(
            string partName, PrimitiveType type, Transform parent,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            part.layer = GameLayers.Player;

            Collider partCollider = part.GetComponent<Collider>();
            if (partCollider != null) Object.Destroy(partCollider);
            Renderer partRenderer = part.GetComponent<Renderer>();
            if (partRenderer != null)
            {
                partRenderer.shadowCastingMode = ShadowCastingMode.Off;
                partRenderer.receiveShadows = false;
                if (mat != null) partRenderer.sharedMaterial = mat;
            }
            return part;
        }
    }
}
