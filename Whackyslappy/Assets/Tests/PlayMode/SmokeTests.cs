using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhackySlappy.NPC;

namespace WhackySlappy.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke tests: load SampleScene (the code-built arena) and
    /// confirm the runtime bootstraps come up. No player input, no physics
    /// values: the bootstraps (GameBootstrap, NpcBootstrap) run
    /// RuntimeInitializeOnLoadMethod AfterSceneLoad, so tests just load the
    /// scene and poll a bounded number of frames.
    /// </summary>
    public class SmokeTests
    {
        private const string TestScene = "SampleScene";

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator SampleSceneLoad_CreatesArenaRoot()
        {
            yield return LoadSampleScene();

            // GameBootstrap creates ArenaRoot after the scene loads; give it
            // a few frames to finish its build steps.
            for (int i = 0; i < 5; i++)
                yield return null;

            Assert.IsNotNull(GameObject.Find("ArenaRoot"),
                "GameBootstrap did not create ArenaRoot in scene " + TestScene);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator SampleSceneLoad_SpawnsAtLeastOneNpc()
        {
            yield return LoadSampleScene();

            // NpcBootstrap adds the NpcSpawner after scene load; its Start()
            // runs the initial spawn burst. Poll for up to ~5 s of frames
            // instead of relying on fixed physics timings.
            NpcController[] npcs = new NpcController[0];
            float deadline = Time.unscaledTime + 5f;
            while (Time.unscaledTime < deadline)
            {
                npcs = Object.FindObjectsByType<NpcController>(FindObjectsSortMode.None);
                if (npcs.Length >= 1)
                    break;
                yield return null;
            }

            Assert.GreaterOrEqual(npcs.Length, 1,
                "No NpcController spawned in scene " + TestScene);
        }

        /// <summary>
        /// Loads SampleScene (in the build list) and waits for the load to
        /// finish. Asserts the scene actually exists in build settings.
        /// </summary>
        private static IEnumerator LoadSampleScene()
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(TestScene, LoadSceneMode.Single);
            Assert.IsNotNull(loadOp, "Scene " + TestScene + " is not in the build settings.");
            while (!loadOp.isDone)
                yield return null;
        }
    }
}
