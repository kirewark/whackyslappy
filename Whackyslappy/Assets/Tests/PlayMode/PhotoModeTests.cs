using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhackySlappy.NPC;
using WhackySlappy.Player;

namespace WhackySlappy.Tests.PlayMode
{
    /// <summary>
    /// PhotoMode only exists inside a WebGL player build
    /// (#if UNITY_WEBGL && !UNITY_EDITOR) and only acts when the page URL
    /// carries a ?shot= query parameter. Normal play - the Editor test
    /// runner included - must therefore be completely unaffected: after
    /// the code-built arena comes up, the real player input components
    /// (PlayerInputReader, FirstPersonController) stay enabled.
    /// </summary>
    public class PhotoModeTests
    {
        private const string TestScene = "SampleScene";

        private static IEnumerator LoadSampleScene()
        {
            var loadOp = SceneManager.LoadSceneAsync(TestScene, LoadSceneMode.Single);
            Assert.IsNotNull(loadOp, "Scene " + TestScene + " is not in the build settings.");
            while (!loadOp.isDone)
                yield return null;

            // One frame after the load: the AfterSceneLoad bootstrap hooks
            // fire only once per play session (already spent on the startup
            // scene), so drive them explicitly. Both are idempotent, same
            // as SmokeTests.
            yield return null;
            WhackySlappy.Core.GameBootstrap.BuildArena();
            NpcBootstrap.Init();
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator NormalPlay_KeepsPlayerInputEnabled()
        {
            yield return LoadSampleScene();

            // Let the bootstrapped components run a few frames, like
            // SmokeTests does before touching the result.
            for (int i = 0; i < 5; i++)
                yield return null;

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "GameBootstrap did not create the Player GameObject.");

            var reader = player.GetComponent<PlayerInputReader>();
            Assert.IsNotNull(reader, "Player is missing its PlayerInputReader.");
            Assert.IsTrue(reader.enabled,
                "PlayerInputReader must stay enabled in normal play (no ?shot= parameter).");
            Assert.IsTrue(reader.enabledInHierarchy,
                "PlayerInputReader must stay active in the hierarchy in normal play.");

            var controller = player.GetComponent<FirstPersonController>();
            Assert.IsNotNull(controller, "Player is missing its FirstPersonController.");
            Assert.IsTrue(controller.enabled,
                "FirstPersonController must stay enabled in normal play (no ?shot= parameter).");
        }
    }
}
