using UnityEngine;
using WhackySlappy.Core;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Silly reactive cartoon face for the NPC head: white eyes with pupils,
    /// brow bars, a mouth and a tongue. Expressions (Idle, Hit, Dizzy, Recover)
    /// are made by lerping face-part transforms toward per-expression targets.
    /// Built once by Build(); the head itself is never rescaled.
    /// </summary>
    public class NpcFace : MonoBehaviour
    {
        public enum Expression { Idle, Hit, Dizzy, Recover }

        [Header("Tuning")]
        [SerializeField] private float m_lerpSpeed = 12f;
        [SerializeField] private float m_orbitSpeed = 8f;        // Dizzy pupil orbit, rad/s
        [SerializeField] private float m_blinkMinDelay = 2f;     // Idle blink, seconds
        [SerializeField] private float m_blinkMaxDelay = 5f;
        [SerializeField] private float m_blinkDuration = 0.12f;

        // Part transforms (cached once, never fetched in Update).
        private Transform m_leftEye, m_rightEye;
        private Transform m_leftPupil, m_rightPupil;
        private Transform m_leftBrow, m_rightBrow;
        private Transform m_mouth, m_tongue;
        private bool m_ready;

        // Base geometry in Face local space (unit scale, front is +Z).
        private static readonly Vector3 LeftEyePos = new Vector3(-0.1f, 0.06f, 0.25f);
        private static readonly Vector3 RightEyePos = new Vector3(0.1f, 0.06f, 0.25f);
        private static readonly Vector3 EyeBaseScale = new Vector3(0.16f, 0.16f, 0.16f);
        // Right eye is a bit fatter for silliness.
        private static readonly Vector3 RightEyeBaseScale = new Vector3(0.2f, 0.16f, 0.16f);
        private static readonly Vector3 PupilBaseScale = new Vector3(0.07f, 0.07f, 0.07f);
        private static readonly Vector3 LeftBrowPos = new Vector3(-0.1f, 0.17f, 0.26f);
        private static readonly Vector3 RightBrowPos = new Vector3(0.1f, 0.17f, 0.26f);
        private static readonly Vector3 BrowBaseScale = new Vector3(0.14f, 0.03f, 0.03f);
        private static readonly Vector3 MouthPos = new Vector3(0f, -0.08f, 0.28f);
        private static readonly Vector3 MouthBaseScale = new Vector3(0.14f, 0.04f, 0.03f);
        private static readonly Vector3 TonguePos = new Vector3(0f, -0.095f, 0.3f);
        private const float PupilZ = 0.07f;    // Pupil centre sits on the eye surface.
        private const float OrbitR = 0.045f;   // Dizzy pupil orbit radius around eye centre.
        private const float CrossShift = 0.035f; // Hit pupils slide toward the nose.

        // Current targets, lerped to every frame (struct fields, no allocations).
        private Vector3 m_leftEyeScaleT, m_rightEyeScaleT;
        private Vector3 m_leftPupilPosT, m_rightPupilPosT;
        private Vector3 m_leftPupilScaleT, m_rightPupilScaleT;
        private Vector3 m_leftBrowPosT, m_rightBrowPosT;
        private Quaternion m_leftBrowRotT, m_rightBrowRotT;
        private Vector3 m_mouthScaleT;

        private Expression m_expression = Expression.Idle;
        private float m_returnTimer;   // >0 counts down, then expression resets to Idle.
        private float m_dizzyAngle;
        private float m_blinkIn = 2f;  // Seconds until the next blink.
        private float m_blinkTimer;    // >0 while a blink is playing.

        /// <summary>
        /// Builds the face as a unit-scale 'Face' child of the head's parent
        /// (the NPC root), placed at the head centre, so head scaling never
        /// distorts the parts. Colliders are destroyed: these are visual only.
        /// </summary>
        public static NpcFace Build(Transform npcRoot, Transform head, Material baseMaterial)
        {
            Transform faceParent = head.parent != null ? head.parent : npcRoot;

            var faceGO = new GameObject("Face");
            faceGO.transform.SetParent(faceParent, false);
            faceGO.transform.localPosition = head.localPosition;
            faceGO.transform.localRotation = head.localRotation;
            faceGO.transform.localScale = Vector3.one;
            faceGO.layer = head.gameObject.layer;

            var face = faceGO.AddComponent<NpcFace>();

            int layer = head.gameObject.layer;

            // Eyes: big white spheres.
            face.m_leftEye = MakePart("EyeL", PrimitiveType.Sphere, faceGO.transform,
                LeftEyePos, EyeBaseScale, baseMaterial, Color.white, layer).transform;
            face.m_rightEye = MakePart("EyeR", PrimitiveType.Sphere, faceGO.transform,
                RightEyePos, RightEyeBaseScale, baseMaterial, Color.white, layer).transform;

            // Pupils: black spheres sitting on the eye surface (z + 0.07).
            face.m_leftPupil = MakePart("PupilL", PrimitiveType.Sphere, faceGO.transform,
                LeftEyePos + new Vector3(0f, 0f, PupilZ), PupilBaseScale,
                baseMaterial, new Color(0.05f, 0.05f, 0.05f), layer).transform;
            face.m_rightPupil = MakePart("PupilR", PrimitiveType.Sphere, faceGO.transform,
                RightEyePos + new Vector3(0f, 0f, PupilZ), PupilBaseScale,
                baseMaterial, new Color(0.05f, 0.05f, 0.05f), layer).transform;

            // Brows: thick dark bars above the eyes.
            face.m_leftBrow = MakePart("BrowL", PrimitiveType.Cube, faceGO.transform,
                LeftBrowPos, BrowBaseScale, baseMaterial,
                new Color(0.15f, 0.12f, 0.1f), layer).transform;
            face.m_rightBrow = MakePart("BrowR", PrimitiveType.Cube, faceGO.transform,
                RightBrowPos, BrowBaseScale, baseMaterial,
                new Color(0.15f, 0.12f, 0.1f), layer).transform;

            // Mouth: flattened cube.
            face.m_mouth = MakePart("Mouth", PrimitiveType.Cube, faceGO.transform,
                MouthPos, MouthBaseScale, baseMaterial,
                new Color(0.45f, 0.1f, 0.1f), layer).transform;

            // Tongue: small red sphere, hidden until Hit or Dizzy.
            face.m_tongue = MakePart("Tongue", PrimitiveType.Sphere, faceGO.transform,
                TonguePos, new Vector3(0.05f, 0.05f, 0.05f), baseMaterial,
                new Color(0.85f, 0.2f, 0.2f), layer).transform;
            face.m_tongue.gameObject.SetActive(false);

            face.InitTargets();
            return face;
        }

        /// <summary>
        /// Requests an expression. After duration seconds the face returns to Idle.
        /// </summary>
        public void SetExpression(Expression e, float duration)
        {
            m_expression = e;
            m_returnTimer = Mathf.Max(0f, duration);
            if (e == Expression.Dizzy)
                m_dizzyAngle = 0f;
        }

        private static GameObject MakePart(string name, PrimitiveType type, Transform parent,
            Vector3 localPos, Vector3 localScale, Material baseMaterial, Color color, int layer)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            part.layer = layer;

            // Visual only: remove the primitive collider so nothing on the NPC
            // layer adds hit surface to the face.
            var col = part.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            // Clone the head material the caller passes (or a DefaultLitMaterial
            // Standard-shader base when it passes none), then tint it.
            var renderer = part.GetComponent<Renderer>();
            var mat = baseMaterial != null
                ? new Material(baseMaterial)
                : DefaultLitMaterial.Create();
            if (mat != null)
            {
                mat.color = color;
                renderer.material = mat;
            }

            return part;
        }

        private void InitTargets()
        {
            SetIdleTargets(1f);
            m_ready = true;
        }

        private void Update()
        {
            if (!m_ready)
                return;

            float dt = Time.deltaTime;

            // Auto-return to Idle when the requested duration has passed.
            if (m_returnTimer > 0f)
            {
                m_returnTimer -= dt;
                if (m_returnTimer <= 0f)
                    m_expression = Expression.Idle;
            }

            // Idle blink timer.
            if (m_expression == Expression.Idle)
            {
                if (m_blinkTimer > 0f)
                {
                    m_blinkTimer -= dt;
                }
                else
                {
                    m_blinkIn -= dt;
                    if (m_blinkIn <= 0f)
                    {
                        m_blinkTimer = m_blinkDuration;
                        m_blinkIn = Random.Range(m_blinkMinDelay, m_blinkMaxDelay);
                    }
                }
            }
            else
            {
                m_blinkTimer = 0f;
            }

            float blinkY = m_blinkTimer > 0f ? 0.1f : 1f;

            switch (m_expression)
            {
                case Expression.Hit:
                    SetHitTargets();
                    break;
                case Expression.Dizzy:
                    m_dizzyAngle += m_orbitSpeed * dt;
                    SetDizzyTargets();
                    break;
                case Expression.Recover:
                    SetRecoverTargets();
                    break;
                default:
                    SetIdleTargets(blinkY);
                    break;
            }

            // Tongue shows on Hit and Dizzy only. Toggle once per change.
            bool showTongue = m_expression == Expression.Hit || m_expression == Expression.Dizzy;
            if (m_tongue.gameObject.activeSelf != showTongue)
                m_tongue.gameObject.SetActive(showTongue);

            // Ease every part toward its target.
            float t = Mathf.Clamp01(dt * m_lerpSpeed);
            m_leftEye.localScale = Vector3.Lerp(m_leftEye.localScale, m_leftEyeScaleT, t);
            m_rightEye.localScale = Vector3.Lerp(m_rightEye.localScale, m_rightEyeScaleT, t);
            m_leftPupil.localPosition = Vector3.Lerp(m_leftPupil.localPosition, m_leftPupilPosT, t);
            m_rightPupil.localPosition = Vector3.Lerp(m_rightPupil.localPosition, m_rightPupilPosT, t);
            m_leftPupil.localScale = Vector3.Lerp(m_leftPupil.localScale, m_leftPupilScaleT, t);
            m_rightPupil.localScale = Vector3.Lerp(m_rightPupil.localScale, m_rightPupilScaleT, t);
            m_leftBrow.localPosition = Vector3.Lerp(m_leftBrow.localPosition, m_leftBrowPosT, t);
            m_rightBrow.localPosition = Vector3.Lerp(m_rightBrow.localPosition, m_rightBrowPosT, t);
            m_leftBrow.localRotation = Quaternion.Slerp(m_leftBrow.localRotation, m_leftBrowRotT, t);
            m_rightBrow.localRotation = Quaternion.Slerp(m_rightBrow.localRotation, m_rightBrowRotT, t);
            m_mouth.localScale = Vector3.Lerp(m_mouth.localScale, m_mouthScaleT, t);
        }

        private void SetIdleTargets(float blinkY)
        {
            m_leftEyeScaleT = new Vector3(EyeBaseScale.x, EyeBaseScale.y * blinkY, EyeBaseScale.z);
            m_rightEyeScaleT = new Vector3(RightEyeBaseScale.x, RightEyeBaseScale.y * blinkY, RightEyeBaseScale.z);
            m_leftPupilPosT = LeftEyePos + new Vector3(0f, 0f, PupilZ);
            m_rightPupilPosT = RightEyePos + new Vector3(0f, 0f, PupilZ);
            m_leftPupilScaleT = PupilBaseScale;
            m_rightPupilScaleT = PupilBaseScale;
            m_leftBrowPosT = LeftBrowPos;
            m_rightBrowPosT = RightBrowPos;
            m_leftBrowRotT = Quaternion.identity;
            m_rightBrowRotT = Quaternion.identity;
            m_mouthScaleT = MouthBaseScale;
        }

        private void SetHitTargets()
        {
            // Eyes pop big; pupils shrink and go cross-eyed.
            m_leftEyeScaleT = EyeBaseScale * 1.6f;
            m_rightEyeScaleT = RightEyeBaseScale * 1.6f;
            // With the eye at x1.6 its surface sits further out, so the pupils
            // ride out to z 0.11 to stay visible.
            m_leftPupilPosT = LeftEyePos + new Vector3(CrossShift, 0f, 0.11f);
            m_rightPupilPosT = RightEyePos + new Vector3(-CrossShift, 0f, 0.11f);
            m_leftPupilScaleT = PupilBaseScale * 0.5f;
            m_rightPupilScaleT = PupilBaseScale * 0.5f;
            // Brows raised and angled (inner ends down, like a dazed glare).
            m_leftBrowPosT = LeftBrowPos + new Vector3(0f, 0.045f, 0f);
            m_rightBrowPosT = RightBrowPos + new Vector3(0f, 0.045f, 0f);
            m_leftBrowRotT = Quaternion.Euler(0f, 0f, -25f);
            m_rightBrowRotT = Quaternion.Euler(0f, 0f, 25f);
            // Mouth stretches into a wide-open O.
            m_mouthScaleT = new Vector3(
                MouthBaseScale.x * 0.6f,
                MouthBaseScale.y * 3.0f,
                MouthBaseScale.z);
        }

        private void SetDizzyTargets()
        {
            // Pupils orbit the eye centres.
            float x = Mathf.Cos(m_dizzyAngle) * OrbitR;
            float y = Mathf.Sin(m_dizzyAngle) * OrbitR;
            m_leftPupilPosT = LeftEyePos + new Vector3(x, y, PupilZ);
            m_rightPupilPosT = RightEyePos + new Vector3(x, y, PupilZ);
            m_leftPupilScaleT = PupilBaseScale;
            m_rightPupilScaleT = PupilBaseScale;
            m_leftEyeScaleT = EyeBaseScale;
            m_rightEyeScaleT = RightEyeBaseScale;
            // Brows flat, mouth wobbles via y-scale oscillation.
            m_leftBrowPosT = LeftBrowPos;
            m_rightBrowPosT = RightBrowPos;
            m_leftBrowRotT = Quaternion.identity;
            m_rightBrowRotT = Quaternion.identity;
            m_mouthScaleT = new Vector3(
                MouthBaseScale.x * 1.2f,
                MouthBaseScale.y * (1.4f + 0.8f * Mathf.Sin(m_dizzyAngle * 1.3f)),
                MouthBaseScale.z);
        }

        private void SetRecoverTargets()
        {
            // Groggy and a bit sad: normal eyes, sad brows, small mouth.
            m_leftEyeScaleT = EyeBaseScale;
            m_rightEyeScaleT = RightEyeBaseScale;
            m_leftPupilPosT = LeftEyePos + new Vector3(0f, 0f, PupilZ);
            m_rightPupilPosT = RightEyePos + new Vector3(0f, 0f, PupilZ);
            m_leftPupilScaleT = PupilBaseScale;
            m_rightPupilScaleT = PupilBaseScale;
            // Inner brow ends lift for a sad look.
            m_leftBrowPosT = LeftBrowPos + new Vector3(0f, 0.02f, 0f);
            m_rightBrowPosT = RightBrowPos + new Vector3(0f, 0.02f, 0f);
            m_leftBrowRotT = Quaternion.Euler(0f, 0f, 15f);
            m_rightBrowRotT = Quaternion.Euler(0f, 0f, -15f);
            m_mouthScaleT = new Vector3(
                MouthBaseScale.x * 0.7f,
                MouthBaseScale.y * 0.6f,
                MouthBaseScale.z);
        }
    }
}
