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
            // Give up after ~10 s of frames if no player shows up.
            private const int MaxWaitFrames = 600;
            private int m_framesWaited;

            private void Update()
            {
                var fps = Object.FindAnyObjectByType<WhackySlappy.Player.FirstPersonController>();
                if (fps != null && fps.CameraPivot != null)
                {
                    // HitFeedback lives on the camera pivot so its localEulerAngles
                    // apply directly to pitch/roll without fighting the FPS look code.
                    // It stays enabled from the start so its Update can poll F1 for
                    // the hit-stop toggle before the first hit; it only writes the
                    // pivot while m_kickActive is set by a hit, so an idle
                    // HitFeedback never touches the camera.
                    var hitFeedback = fps.CameraPivot.gameObject.AddComponent<HitFeedback>();

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
                    return;
                }

                m_framesWaited++;
                if (m_framesWaited >= MaxWaitFrames)
                {
                    Debug.LogWarning("SlapBootstrap: no FirstPersonController with a camera pivot after " + MaxWaitFrames + " frames; the runner gives up.");
                    Destroy(gameObject);
                }
            }
        }
    }
}
