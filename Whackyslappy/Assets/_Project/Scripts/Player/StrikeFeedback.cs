using UnityEngine;
using WhackySlappy.Core;
using WhackySlappy.NPC;

namespace WhackySlappy.Player
{
    /// <summary>
    /// Player-side feedback for NPC strikes: when a nearby NPC begins a
    /// swing (NpcController.AnyStrikeLanded), kick the camera view and
    /// play the kit Whoosh through SlapAudio's pooled sources, so the
    /// strike is felt even without looking at the NPC. Sits on the camera
    /// rig next to HitFeedback; subscribes once per enable, so recycled
    /// NPCs can never cause double subscriptions.
    /// </summary>
    public class StrikeFeedback : MonoBehaviour
    {
        [SerializeField] private float m_maxFlatDistance = 3f;
        [SerializeField] private float m_bumpPitch = -2.5f;
        [SerializeField] private float m_bumpRoll = 1f;
        [SerializeField] private float m_bumpDuration = 0.12f;
        [SerializeField] private float m_whooshVolume = 0.5f;

        private FirstPersonController m_player;

        private void Awake()
        {
            m_player = GetComponentInParent<FirstPersonController>();
        }

        private void OnEnable()
        {
            NpcController.AnyStrikeLanded += OnStrikeLanded;
        }

        private void OnDisable()
        {
            NpcController.AnyStrikeLanded -= OnStrikeLanded;
        }

        // Fires for every NPC's swing start; only reacts to NPCs within
        // m_maxFlat metres, measured flat (height difference ignored).
        private void OnStrikeLanded(NpcController npc)
        {
            if (npc == null || m_player == null)
                return;

            Vector3 delta = npc.transform.position - m_player.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > m_maxFlatDistance * m_maxFlatDistance)
                return;

            float roll = m_bumpRoll * (Random.value * 2f - 1f);
            m_player.AddCameraBump(m_bumpPitch, roll, m_bumpDuration);
            SlapAudio.Play(SlapAudio.SoundKind.Whoosh, npc.transform.position, m_whooshVolume);
        }
    }
}
