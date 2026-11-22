using System.Collections.Generic;
using UnityEngine;
using WhackySlappy.Core;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Adds capsule arms and legs with CharacterJoints to an NPC body built
    /// by NpcController.Create(). Pure construction helper: no behaviour,
    /// no Update. The torso root Rigidbody stays the only balance body.
    /// </summary>
    public static class NpcBodyBuilder
    {
        // Arm proportions (metres).
        private const float ArmRadius = 0.09f;
        private const float ArmLength = 0.45f;
        private const float ArmMass = 0.15f;

        // Leg proportions (metres).
        private const float LegRadius = 0.12f;
        private const float LegLength = 0.5f;
        private const float LegMass = 0.25f;

        // Attachment points, relative to the npcRoot transform: root origin
        // is torso centre; feet at -0.8.
        private const float ShoulderX = 0.45f;
        private const float ShoulderY = 0.45f;
        private const float HipX = 0.2f;
        private const float HipY = 0.1f;

        // How far lower segments overlap upper segments so joints have no gap.
        private const float Overlap = 0.1f;

        // Joint anchor at the top of a capsule primitive. A capsule
        // primitive is 2 units tall in its own local space (before scaling),
        // so y = 1 is its top; Unity scales the anchor with the transform,
        // landing the joint at the limb's upper end.
        private static readonly Vector3 TopAnchor = new Vector3(0f, 1f, 0f);

        /// <summary>
        /// Adds two arms (upper + lower capsule each) and two legs (upper +
        /// lower capsule each) under npcRoot, joins them with CharacterJoints
        /// and turns off collisions between all body colliders.
        /// </summary>
        public static void AddLimbs(GameObject npcRoot, Material material, Transform torso)
        {
            if (npcRoot == null)
            {
                Debug.LogWarning("NpcBodyBuilder: npcRoot is null, skipping limbs.");
                return;
            }

            Rigidbody rootRigidbody = npcRoot.GetComponent<Rigidbody>();
            if (rootRigidbody == null && torso != null)
            {
                rootRigidbody = torso.GetComponent<Rigidbody>();
            }
            if (rootRigidbody == null)
            {
                Debug.LogWarning("NpcBodyBuilder: no Rigidbody on npcRoot, skipping limbs.");
                return;
            }

            // Left is -x, right is +x (character forward is +z).
            Rigidbody leftUpperArm = BuildLimb(npcRoot, material, "Arm_L_Upper",
                new Vector3(-ShoulderX, ShoulderY - ArmLength * 0.5f, 0f),
                ArmRadius, ArmLength, ArmMass);
            AddJoint(leftUpperArm.gameObject, rootRigidbody, TopAnchor, 70f, 50f, -30f, 30f);

            Rigidbody leftForearm = BuildLimb(npcRoot, material, "Arm_L_Lower",
                new Vector3(-ShoulderX, ShoulderY - ArmLength - ArmLength * 0.5f + Overlap, 0f),
                ArmRadius, ArmLength, ArmMass);
            AddJoint(leftForearm.gameObject, leftUpperArm, TopAnchor, 60f, 0f, -10f, 10f);

            Rigidbody rightUpperArm = BuildLimb(npcRoot, material, "Arm_R_Upper",
                new Vector3(ShoulderX, ShoulderY - ArmLength * 0.5f, 0f),
                ArmRadius, ArmLength, ArmMass);
            AddJoint(rightUpperArm.gameObject, rootRigidbody, TopAnchor, 70f, 50f, -30f, 30f);

            Rigidbody rightForearm = BuildLimb(npcRoot, material, "Arm_R_Lower",
                new Vector3(ShoulderX, ShoulderY - ArmLength - ArmLength * 0.5f + Overlap, 0f),
                ArmRadius, ArmLength, ArmMass);
            AddJoint(rightForearm.gameObject, rightUpperArm, TopAnchor, 60f, 0f, -10f, 10f);

            Rigidbody leftThigh = BuildLimb(npcRoot, material, "Leg_L_Upper",
                new Vector3(-HipX, HipY - LegLength * 0.5f, 0f),
                LegRadius, LegLength, LegMass);
            AddJoint(leftThigh.gameObject, rootRigidbody, TopAnchor, 35f, 20f, -15f, 15f);

            Rigidbody leftShin = BuildLimb(npcRoot, material, "Leg_L_Lower",
                new Vector3(-HipX, HipY - LegLength - LegLength * 0.5f + Overlap, 0f),
                LegRadius, LegLength, LegMass);
            AddJoint(leftShin.gameObject, leftThigh, TopAnchor, 25f, 0f, -10f, 10f);

            Rigidbody rightThigh = BuildLimb(npcRoot, material, "Leg_R_Upper",
                new Vector3(HipX, HipY - LegLength * 0.5f, 0f),
                LegRadius, LegLength, LegMass);
            AddJoint(rightThigh.gameObject, rootRigidbody, TopAnchor, 35f, 20f, -15f, 15f);

            Rigidbody rightShin = BuildLimb(npcRoot, material, "Leg_R_Lower",
                new Vector3(HipX, HipY - LegLength - LegLength * 0.5f + Overlap, 0f),
                LegRadius, LegLength, LegMass);
            AddJoint(rightShin.gameObject, rightThigh, TopAnchor, 25f, 0f, -10f, 10f);

            // Ignore collisions between every pair of body colliders
            // (torso, head and all limbs) so the joints do not self-collide.
            List<Collider> allColliders =
                new List<Collider>(npcRoot.GetComponentsInChildren<Collider>());
            for (int a = 0; a < allColliders.Count; a++)
            {
                for (int b = a + 1; b < allColliders.Count; b++)
                {
                    if (allColliders[a] != null && allColliders[b] != null)
                    {
                        Physics.IgnoreCollision(allColliders[a], allColliders[b]);
                    }
                }
            }
        }

        /// <summary>
        /// Creates one hanging capsule limb under npcRoot and returns its
        /// Rigidbody. centerLocal is the limb centre offset from npcRoot.
        /// </summary>
        private static Rigidbody BuildLimb(GameObject npcRoot, Material material,
            string limbName, Vector3 centerLocal, float radius, float length, float mass)
        {
            GameObject limb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            limb.name = limbName;

            // Place at its centre in world space before any physics is added.
            limb.transform.position = npcRoot.transform.position + centerLocal;
            limb.transform.rotation = Quaternion.identity;
            // A capsule primitive is 2 high and 1 wide, so these scales give
            // a capsule of the wanted radius and segment length.
            limb.transform.localScale = new Vector3(2f * radius, length * 0.5f, 2f * radius);
            limb.transform.SetParent(npcRoot.transform);
            limb.layer = GameLayers.NPC;

            Renderer renderer = limb.GetComponent<Renderer>();
            if (material != null && renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Rigidbody rb = limb.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.None;
            // Collision detection stays at the default (Discrete).
            return rb;
        }

        /// <summary>
        /// Adds a CharacterJoint on limbGo joined to connectedBody, with
        /// degrees for the swing and twist limits.
        /// </summary>
        private static void AddJoint(GameObject limbGo, Rigidbody connectedBody,
            Vector3 anchor, float swing1, float swing2, float twistLow, float twistHigh)
        {
            CharacterJoint joint = limbGo.AddComponent<CharacterJoint>();
            joint.connectedBody = connectedBody;
            joint.axis = Vector3.right;
            joint.anchor = anchor;

            // SoftJointLimit is a struct: copy, change, assign back.
            SoftJointLimit swing1Limit = joint.swing1Limit;
            swing1Limit.limit = swing1;
            joint.swing1Limit = swing1Limit;

            SoftJointLimit swing2Limit = joint.swing2Limit;
            swing2Limit.limit = swing2;
            joint.swing2Limit = swing2Limit;

            SoftJointLimit lowTwistLimit = joint.lowTwistLimit;
            lowTwistLimit.limit = twistLow;
            joint.lowTwistLimit = lowTwistLimit;

            SoftJointLimit highTwistLimit = joint.highTwistLimit;
            highTwistLimit.limit = twistHigh;
            joint.highTwistLimit = highTwistLimit;

            // Snap the connected anchor to the current world position.
            joint.autoConfigureConnectedAnchor = true;
        }
    }
}
