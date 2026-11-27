#if UNITY_WEBGL && !UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using WhackySlappy.NPC;
using WhackySlappy.Player;
using WhackySlappy.Slap;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Photo mode for the headless WebGL CI browser: when the build is
    /// opened with ?shot=overview|slap|getup in its URL, player input is
    /// disabled, a fixed camera is set per scenario, one scripted action
    /// runs through the real gameplay paths (SlapHand swing /
    /// ISlappable.OnHit), and the exact "[shot] start / state / done"
    /// protocol is logged so the CI can sample frames at 0.25 s, 0.75 s,
    /// 1.5 s and 3.0 s after the start line.
    ///
    /// Without a shot query parameter the hook returns immediately and
    /// changes nothing; the whole file is compiled out outside WebGL
    /// players (#if UNITY_WEBGL && !UNITY_EDITOR), so neither the Editor
    /// nor normal play ever executes any of this.
    /// </summary>
    public static class PhotoMode
    {
        private const int kMaxWaitFrames = 600; // Same grace as the other bootstrap runners.
        private const float kSampleInterval = 0.25f;
        private const int kSampleCount = 14; // 14 * 0.25 s = 3.5 s of state lines.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            string shot = ReadShotParameter(Application.absoluteURL);
            if (string.IsNullOrEmpty(shot))
                return;

            if (shot != "overview" && shot != "slap" && shot != "getup")
            {
                Debug.Log("[shot] start {\"name\":\"" + shot + "\",\"error\":\"unknown shot\"}");
                Debug.Log("[shot] done");
                return;
            }

            var runner = new GameObject("PhotoModeRunner");
            runner.AddComponent<Runner>().m_shot = shot;
        }

        /// <summary>
        /// Reads the 'shot' value from the page URL. Returns null when the
        /// parameter is absent, empty, or contains any character outside
        /// [a-z0-9-], so odd input can never reach the log line.
        /// </summary>
        private static string ReadShotParameter(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            int q = url.IndexOf('?');
            if (q < 0)
                return null;

            foreach (string pair in url.Substring(q + 1).Split('&'))
            {
                if (!pair.StartsWith("shot=", System.StringComparison.Ordinal))
                    continue;

                string value = pair.Substring("shot=".Length);
                if (value.Length == 0)
                    return null;

                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
                    if (!ok)
                        return null;
                }
                return value;
            }
            return null;
        }

        /// <summary>
        /// Stages and plays one shot (same nested-runner pattern as
        /// SlapBootstrap: waits for the bootstrapped systems, then acts).
        /// </summary>
        private class Runner : MonoBehaviour
        {
            // Assigned by the outer PhotoMode class in Init; the WebGL build
            // rejects cross-class writes to a nested-class private field
            // (CS0122), so this field is internal. Same-assembly access.
            internal string m_shot = "";

            // Stable ids per NPC instance across the 3.5 s of state lines.
            private readonly Dictionary<NpcController, int> m_npcIds = new Dictionary<NpcController, int>();
            private int m_nextNpcId;
            private bool m_staged;
            private int m_waitFrames;

            private void Update()
            {
                if (m_staged)
                    return;

                var fps = Object.FindAnyObjectByType<FirstPersonController>();
                var spawner = Object.FindAnyObjectByType<NpcSpawner>();
                var hand = m_shot == "slap" ? Object.FindAnyObjectByType<SlapHand>() : null;

                bool ready = fps != null && spawner != null && (m_shot != "slap" || hand != null);
                if (!ready)
                {
                    m_waitFrames++;
                    if (m_waitFrames >= kMaxWaitFrames)
                    {
                        Debug.LogWarning("PhotoMode: bootstrapped systems never showed up after " +
                            kMaxWaitFrames + " frames; the runner gives up.");
                        Destroy(gameObject);
                    }
                    return;
                }

                m_staged = true;
                Stage(fps, spawner, hand);
            }

            private void Stage(FirstPersonController fps, NpcSpawner spawner, SlapHand hand)
            {
                // Headless CI: nothing should need a real mouse or keyboard.
                fps.enabled = false;
                var reader = fps.GetComponent<PlayerInputReader>();
                if (reader != null)
                    reader.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                NpcController scenarioNpc = null;

                if (m_shot == "overview")
                {
                    // Straight down over the whole FloorSize x FloorSize
                    // (60 x 60 m) arena at the camera's default fov 60:
                    // at FloorSize + 5 m the vertical span is ~75 m, so the
                    // edge strips stay on screen whatever the CI viewport.
                    // Normal spawning continues untouched.
                    SetFixedCamera(new Vector3(0f, GameBootstrap.FloorSize + 5f, 0f), Quaternion.Euler(90f, 0f, 0f));
                    // At FloorSize + 5 m (~65 m) the camera sits far past
                    // the linear fog start (25 m) and the shot washes out;
                    // fog is global (RenderSettings.fog), off for this
                    // throwaway WebGL photo session, never restored.
                    RenderSettings.fog = false;
                }
                else if (m_shot == "slap")
                {
                    ClearArenaForSingleNpc(spawner);
                    Vector3 fwd = fps.transform.forward;
                    fwd.y = 0f;
                    fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
                    scenarioNpc = NpcController.Create(fps.transform.position + fwd * 1.5f, spawner);
                    // Phase 5b: aggro off while staging so the slap
                    // telemetry stays comparable; RunProtocol's sampling
                    // loop re-arms it 2.5 s into the shot.
                    scenarioNpc.AggroEnabled = false;
                    // The Main Camera stays: it rides the now-motionless
                    // player rig (input is off) and already faces the NPC.
                }
                else // getup
                {
                    ClearArenaForSingleNpc(spawner);
                    scenarioNpc = NpcController.Create(new Vector3(0f, 1f, 4f), spawner);
                    // Phase 5b: aggro off for this shot's NPC and never
                    // re-armed, so getup telemetry stays as before.
                    scenarioNpc.AggroEnabled = false;
                    Vector3 focus = scenarioNpc.transform.position + Vector3.up * 0.9f;
                    Vector3 camPos = scenarioNpc.transform.position + Vector3.right * 4f + Vector3.up * 0.9f;
                    SetFixedCamera(camPos, Quaternion.LookRotation(focus - camPos));
                }

                StartCoroutine(RunProtocol(hand, scenarioNpc));
            }

            /// <summary>
            /// Parks the player camera (its AudioListener component stays
            /// enabled on the same GameObject) and renders through a fresh
            /// fixed camera with a higher depth. No materials are created
            /// here, so DefaultLitMaterial is not needed.
            /// </summary>
            private static void SetFixedCamera(Vector3 position, Quaternion rotation)
            {
                Camera playerCam = Camera.main != null
                    ? Camera.main
                    : Object.FindAnyObjectByType<Camera>();
                if (playerCam != null)
                    playerCam.enabled = false;

                var camGo = new GameObject("PhotoModeCamera");
                Camera cam = camGo.AddComponent<Camera>();
                cam.depth = 100f;
                camGo.transform.SetPositionAndRotation(position, rotation);
            }

            /// <summary>
            /// slap/getup frame exactly one NPC: despawn whatever already
            /// spawned and keep the spawner quiet for the shot.
            /// </summary>
            private static void ClearArenaForSingleNpc(NpcSpawner spawner)
            {
                spawner.enabled = false;
                NpcController[] npcs = Object.FindObjectsByType<NpcController>(FindObjectsSortMode.None);
                for (int i = 0; i < npcs.Length; i++)
                {
                    if (npcs[i] == null)
                        continue;
                    spawner.NotifyDespawn(npcs[i]);
                    Destroy(npcs[i].gameObject);
                }
            }

            private IEnumerator RunProtocol(SlapHand hand, NpcController scenarioNpc)
            {
                // One settling frame so the scenario NPC has run its Awake/
                // Start (cached body) before the action fires.
                yield return null;

                Debug.Log("[shot] start {\"name\":\"" + m_shot + "\"}");

                if (m_shot == "slap" && hand != null)
                {
                    // The same plain swing an Attack release with no drag
                    // performs; animation, velocity-based impulse and slap
                    // audio all come from the real code path.
                    hand.TriggerPlainSwing();
                }
                else if (m_shot == "getup" && scenarioNpc != null)
                {
                    // Knock it flat through the real hit path: OnSlapped
                    // only flips state (the slap hand normally applies the
                    // force), so shove sideways high on the torso first,
                    // then feed the same SlapHitInfo to ISlappable.
                    Rigidbody rb = scenarioNpc.GetComponent<Rigidbody>();
                    Vector3 impulse = Vector3.forward * 14f;
                    Vector3 point = scenarioNpc.transform.position + Vector3.up * 1.3f;
                    if (rb != null)
                        rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
                    ((ISlappable)scenarioNpc).OnHit(new SlapHitInfo(point, impulse, SlapKind.Slap, 1f));
                }

                for (int i = 0; i < kSampleCount; i++)
                {
                    // Phase 5b: re-arm the slap shot's NPC aggro 2.5 s
                    // after the loop starts (10 samples x 0.25 s), so the
                    // final samples show whether it turns aggressive and
                    // walks toward the player. Overview shots keep aggro.
                    if (m_shot == "slap" && scenarioNpc != null && i == 10)
                        scenarioNpc.AggroEnabled = true;

                    yield return new WaitForSecondsRealtime(kSampleInterval);
                    Debug.Log("[shot] state " + BuildStateJson());
                }

                Debug.Log("[shot] done");
                Destroy(gameObject);
            }

            private string BuildStateJson()
            {
                NpcController[] npcs = Object.FindObjectsByType<NpcController>(FindObjectsSortMode.None);
                System.Array.Sort(npcs, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

                var sb = new StringBuilder("{\"npcs\":[", 1024);
                bool first = true;

                for (int i = 0; i < npcs.Length; i++)
                {
                    NpcController npc = npcs[i];
                    if (npc == null)
                        continue;

                    if (!m_npcIds.TryGetValue(npc, out int id))
                    {
                        id = m_nextNpcId++;
                        m_npcIds[npc] = id;
                    }

                    if (!first)
                        sb.Append(',');
                    first = false;

                    Vector3 p = npc.transform.position;
                    sb.Append("{\"id\":").Append(id)
                      .Append(",\"x\":").Append(p.x.ToString("F2", CultureInfo.InvariantCulture))
                      .Append(",\"y\":").Append(p.y.ToString("F2", CultureInfo.InvariantCulture))
                      .Append(",\"z\":").Append(p.z.ToString("F2", CultureInfo.InvariantCulture))
                      .Append(",\"tilt\":").Append(npc.TiltDegrees.ToString("F1", CultureInfo.InvariantCulture))
                      .Append(",\"tumbling\":").Append(npc.IsTumbling ? "true" : "false")
                      .Append(",\"speed\":").Append(npc.Speed.ToString("F2", CultureInfo.InvariantCulture))
                      .Append(",\"walking\":").Append(npc.IsWalking ? "true" : "false")
                      .Append(",\"aggro\":").Append(npc.IsAggro ? "true" : "false")
                      .Append(",\"strike\":\"")
                      .Append(npc.StrikePhase.ToString()).Append('"')
                      .Append('}');

                    // Stay well under the CI's 2000-char line limit; with
                    // at most 13 NPCs this never fires, but guard it anyway.
                    if (sb.Length > 1800)
                        break;
                }

                sb.Append("]}");
                return sb.ToString();
            }
        }
    }
}

#endif
