using UnityEngine;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// Bootstrap: locates the FirstPersonController after scene load and
    /// wires up the SlapHand component to its camera pivot.
    /// </summary>
    public static class SlapBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            var runner = new GameObject("SlapBootstrapRunner");
            runner.AddComponent<Runner>();
        }

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                var fps = Object.FindAnyObjectByType<WhackySlappy.Player.FirstPersonController>();
                if (fps != null && fps.CameraPivot != null)
                {
                    var hand = new GameObject("SlapHand");
                    hand.transform.SetParent(fps.CameraPivot, false);
                    var slapHand = hand.AddComponent<SlapHand>();
                    slapHand.InputReader = fps.GetComponent<WhackySlappy.Player.PlayerInputReader>();
                    slapHand.Player = fps;
                    Destroy(gameObject);
                }
            }
        }
    }
}
