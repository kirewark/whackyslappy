using UnityEngine;

namespace WhackySlappy.NPC
{
    /// <summary>
    /// Ensures exactly one NpcSpawner exists in the scene.
    /// Called automatically after every scene loads.
    /// </summary>
    public static class NpcBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            if (Object.FindAnyObjectByType<NpcSpawner>() == null)
            {
                var go = new GameObject("NpcSpawner");
                go.AddComponent<NpcSpawner>();
            }
        }
    }
}
