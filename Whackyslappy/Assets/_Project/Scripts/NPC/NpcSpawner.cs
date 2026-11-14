using System.Collections;
using UnityEngine;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Maintains the Design.md NPC count alive. Spawns NPCs on start and respawns
    /// them after they are destroyed (fall off the platform).
    /// </summary>
    public class NpcSpawner : MonoBehaviour
    {
        [SerializeField] private int m_targetCount = 5;
        [SerializeField] private float m_respawnMinDelay = 2f;
        [SerializeField] private float m_respawnMaxDelay = 4f;
        [SerializeField] private float m_minSpawnDistance = 4f;

        private int m_activeCount = 0;

        private void Start()
        {
            SpawnInitial();
        }

        /// <summary>
        /// Called by NpcController when it falls below the kill height.
        /// Waits a random delay and then spawns a replacement.
        /// </summary>
        public void NotifyDespawn(NpcController npc)
        {
            m_activeCount = Mathf.Max(0, m_activeCount - 1);
            float delay = Random.Range(m_respawnMinDelay, m_respawnMaxDelay);
            StartCoroutine(SpawnAfterDelay(npc, delay));
        }

        private void SpawnInitial()
        {
            for (int i = 0; i < m_targetCount; i++)
            {
                SpawnRandom();
            }
        }

        private IEnumerator SpawnAfterDelay(NpcController npc, float delay)
        {
            yield return new WaitForSeconds(delay);
            SpawnRandom();
        }

        private void SpawnRandom()
        {
            var player = Object.FindAnyObjectByType<WhackySlappy.Player.FirstPersonController>();
            Vector3 spawnPos;
            int attempts = 0;
            do
            {
                float x = Random.Range(-9f, 9f);
                float z = Random.Range(-9f, 9f);
                spawnPos = new Vector3(x, 1.0f, z);
                attempts++;
            } while (player != null &&
                     Vector3.Distance(spawnPos, player.transform.position) < m_minSpawnDistance
                     && attempts < 20);

            NpcController.Create(spawnPos, this);
            m_activeCount++;
        }
    }
}
