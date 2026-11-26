using System.Collections.Generic;
using UnityEngine;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Keeps the NPC count under a hard cap: an initial burst of at most
    /// m_initialBurst, then at most one replacement spawn per m_spawnInterval
    /// tick while below m_maxAlive. The alive count is decremented on the
    /// (deduplicated) despawn notification from NpcController.
    /// </summary>
    public class NpcSpawner : MonoBehaviour
    {
        /// <summary>Hard cap: the spawner never exceeds this alive count.</summary>
        [SerializeField] private int m_maxAlive = 4;

        /// <summary> NPCs spawned at Start (clamped to m_maxAlive).</summary>
        [SerializeField] private int m_initialBurst = 4;

        /// <summary>Seconds between replacement spawn ticks; one spawn max per tick.</summary>
        [SerializeField] private float m_spawnInterval = 3f;

        /// <summary>Minimum spawn distance from the player (m).</summary>
        [SerializeField] private float m_minSpawnDistance = 4f;

        /// <summary>Dead bodies kept in the scene at most; when a newly
        /// registered corpse pushes the queue past this count, the oldest
        /// one is destroyed (Phase 2b corpse cap).</summary>
        [SerializeField] private int m_maxCorpses = 6;

        private int m_activeCount = 0;
        private float m_intervalTimer = 0f;

        // NPCs currently alive and spawned by this spawner. Doubles as the
        // dedupe set: NpcController.Despawn has no re-entry guard and its
        // kill-height check can fire twice in the same FixedUpdate tick
        // (CheckDespawn + CheckTransitionToGettingUp), so NotifyDespawn can
        // be called repeatedly for one death. A spawn-per-death coroutine
        // loop let the count grow unbounded; now only this set's removal
        // decrements the count, and the interval tick below re-spawns.
        private readonly HashSet<NpcController> m_alive = new HashSet<NpcController>();

        // Downed bodies still in the scene: NpcController.Die() keeps the
        // corpse as a passive ragdoll, so nothing else destroys it. FIFO,
        // capped by m_maxCorpses in RegisterCorpse. A corpse already
        // destroyed by the kill-height Despawn() reads as null through
        // Unity's overloaded == and is skipped when dequeued.
        private readonly Queue<NpcController> m_corpses = new Queue<NpcController>();

        private void Start()
        {
            int burst = Mathf.Min(m_initialBurst, m_maxAlive);
            for (int i = 0; i < burst; i++)
                SpawnRandom();
        }

        private void Update()
        {
            m_intervalTimer += Time.deltaTime;
            if (m_intervalTimer < m_spawnInterval)
                return;
            m_intervalTimer = 0f;
            // One spawn at most per interval tick, never past the hard cap.
            if (m_activeCount < m_maxAlive)
                SpawnRandom();
        }

        /// <summary>
        /// Called by NpcController when it dies (Phase 2b, from Die) and
        /// again if its corpse later falls below the kill height.
        /// Only the first notification per NPC counts; replacements come
        /// from the interval tick in Update, not from a per-death coroutine.
        /// </summary>
        public void NotifyDespawn(NpcController npc)
        {
            if (npc == null || !m_alive.Remove(npc))
                return;
            m_activeCount = Mathf.Max(0, m_activeCount - 1);
        }

        /// <summary>
        /// Called by NpcController.Die() right after NotifyDespawn: the
        /// corpse stays in the scene as a ragdoll, but only the most
        /// recent m_maxCorpses bodies are kept — when the queue exceeds
        /// that cap, the oldest corpse is destroyed (bodies already
        /// gone via Despawn() read as null and are just dropped).
        /// </summary>
        public void RegisterCorpse(NpcController npc)
        {
            if (npc == null)
                return;
            m_corpses.Enqueue(npc);
            while (m_corpses.Count > m_maxCorpses)
            {
                NpcController oldest = m_corpses.Dequeue();
                if (oldest != null)
                    Destroy(oldest.gameObject);
            }
        }

        private void SpawnRandom()
        {
            if (m_activeCount >= m_maxAlive)
                return;

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

            NpcController npc = NpcController.Create(spawnPos, this);
            m_alive.Add(npc);
            m_activeCount++;
        }
    }
}
