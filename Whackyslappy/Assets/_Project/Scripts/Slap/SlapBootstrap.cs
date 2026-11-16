using UnityEngine;
using WhackySlappy.Core;

namespace WhackySlappy.Slap
{
    /// <summary>
    /// Bootstrap: locates the FirstPersonController after scene load and
    /// wires up the SlapHand component to its camera pivot.
    /// Also attaches a HitFeedback component to the camera pivot for
    /// camera shake and hit-stop feedback.
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
                    // HitFeedback lives on the camera pivot so its localEulerAngles
                    // apply directly to pitch/roll without fighting the FPS look code.
                    var hitFeedback = fps.CameraPivot.gameObject.AddComponent<HitFeedback>();
                    hitFeedback.enabled = false; // start disabled; Trigger enables it

                    var hand = new GameObject("SlapHand");
                    hand.transform.SetParent(fps.CameraPivot, false);
                    var input = fps.GetComponent<WhackySlappy.Player.PlayerInputReader>();
                    var slapHand = hand.AddComponent<SlapHand>();
                    slapHand.InputReader = input;
                    slapHand.Player = fps;

                    // LegSweep rides the same pivot child as SlapHand. The
                    // pivot guard covers a second wiring pass (e.g. player
                    // surviving a scene reload). InputReader is assigned
                    // after AddComponent, so LegSweep must read it in
                    // Update, not Awake — which it does.
                    if (fps.CameraPivot.GetComponentInChildren<LegSweep>() == null)
                    {
                        var sweep = hand.AddComponent<LegSweep>();
                        sweep.InputReader = input;
                    }
                    Destroy(gameObject);
                }
            }
        }
    }
}
