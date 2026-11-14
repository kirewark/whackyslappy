using UnityEngine;
using WhackySlappy.Player;

namespace WhackySlappy.Core
{
    /// <summary>
    /// Builds the test arena and player in code when the scene loads.
    /// If no GameObject named "ArenaRoot" exists, this class creates
    /// the floor, walls, lighting, and the player controller.
    /// </summary>
    public static class GameBootstrap
    {
        private const float FloorSize = 20f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void BuildArena()
        {
            ApplyLayerMatrix();

            if (GameObject.Find("ArenaRoot") != null)
                return; // Already built.

            var arenaRoot = new GameObject("ArenaRoot");

            CreateFloor(arenaRoot);
            CreatePlayerBlockerWalls(arenaRoot);
            EnsureDirectionalLight();
            CreatePlayer(arenaRoot);
            EnsureCrosshairOnRoot(arenaRoot);
        }

        /// <summary>
        /// Creates a 20x20 m floor at y=0.
        /// </summary>
        private static void CreateFloor(GameObject parent)
        {
            var floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObj.name = "Floor";
            floorObj.transform.SetParent(parent.transform, false);
            floorObj.layer = GameLayers.Environment;
            floorObj.transform.localScale = new Vector3(FloorSize, 0.2f, FloorSize);
            floorObj.transform.position = new Vector3(0, -0.1f, 0);

            // Checker texture: 2x2 pixels, point filter, repeat wrap, tiled 10x10.
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.SetPixel(0, 0, new Color(0.8f, 0.8f, 0.8f));
            tex.SetPixel(1, 0, new Color(0.55f, 0.55f, 0.55f));
            tex.SetPixel(0, 1, new Color(0.55f, 0.55f, 0.55f));
            tex.SetPixel(1, 1, new Color(0.8f, 0.8f, 0.8f));
            tex.Apply();

            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            var mat = new Material(shader);
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(10f, 10f);

            var renderer = floorObj.GetComponent<MeshRenderer>();
            renderer.material = mat;
        }

        /// <summary>
        /// Creates four invisible PlayerBlocker walls around the arena edge.
        /// </summary>
        private static void CreatePlayerBlockerWalls(GameObject parent)
        {
            const float WallHeight = 3f;
            var half = FloorSize * 0.5f;
            var wallThickness = 0.3f;
            var wallLength = FloorSize + wallThickness;

            Vector3[] positions =
            {
                new Vector3(0, WallHeight * 0.5f, -half - wallThickness * 0.5f), // North
                new Vector3(0, WallHeight * 0.5f,  half + wallThickness * 0.5f), // South
                new Vector3(-half - wallThickness * 0.5f, WallHeight * 0.5f, 0), // West
                new Vector3( half + wallThickness * 0.5f, WallHeight * 0.5f, 0), // East
            };

            Vector3[] sizes =
            {
                new Vector3(wallLength, WallHeight, wallThickness),
                new Vector3(wallLength, WallHeight, wallThickness),
                new Vector3(wallThickness, WallHeight, wallLength),
                new Vector3(wallThickness, WallHeight, wallLength),
            };

            for (int i = 0; i < 4; i++)
            {
                var wallObj = new GameObject("BlockerWall_" + "NWSE"[i]);
                wallObj.layer = GameLayers.PlayerBlocker;

                var collider = wallObj.AddComponent<BoxCollider>();
                collider.isTrigger = false;
                collider.center = Vector3.zero;
                collider.size = sizes[i];

                wallObj.transform.SetParent(parent.transform, false);
                wallObj.transform.position = positions[i];
            }
        }

        /// <summary>
        /// Ensures there is at least one directional light in the scene.
        /// </summary>
        private static void EnsureDirectionalLight()
        {
            var dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight == null || dirLight.type != LightType.Directional)
            {
                var go = new GameObject("DirectionalLight");
                go.AddComponent<Light>().type = LightType.Directional;
                go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                go.transform.position = new Vector3(0, 3, 0);
            }
        }

        /// <summary>
        /// Creates the player: CharacterController + CameraRig + input reader.
        /// </summary>
        private static void CreatePlayer(GameObject parent)
        {
            var playerObj = new GameObject("Player");
            playerObj.layer = GameLayers.Player;
            playerObj.transform.SetParent(parent.transform, false);
            playerObj.transform.position = new Vector3(0, 0.1f, 0);

            // CharacterController: height 1.8, radius 0.35, bottom at y=0.
            var cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0); // bottom at y=0
            cc.skinWidth = 0.01f;

            // PlayerInputReader creates and enables input actions.
            playerObj.AddComponent<PlayerInputReader>();

            // FirstPersonController gets input reader via GetComponent.
            var fps = playerObj.AddComponent<FirstPersonController>();

            // Camera rig at eye height (1.6). Pitch the rig, not the player.
            var cameraRig = new GameObject("CameraRig");
            cameraRig.transform.SetParent(playerObj.transform, false);
            cameraRig.transform.localPosition = new Vector3(0, 1.6f, 0);

            // Set CameraPivot BEFORE AddComponent runs Awake (which reads it).
            fps.CameraPivot = cameraRig.transform;

            // Use the existing Main Camera if it has the MainCamera tag;
            // otherwise create a new one.
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            // Re-parent the camera under the rig so it can pitch.
            camera.transform.SetParent(cameraRig.transform, false);
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localEulerAngles = Vector3.zero;

            // Disable any extra AudioListener — only the camera's listener should be active.
            var allListeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var listener in allListeners)
            {
                if (listener.gameObject != camera.gameObject)
                    listener.enabled = false;
            }
            // Ensure the camera has an AudioListener.
            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
        }

        /// <summary>
        /// Places the crosshair on the ArenaRoot (not a separate child).
        /// </summary>
        private static void EnsureCrosshairOnRoot(GameObject arenaRoot)
        {
            // Remove the crosshair child if the bootstrap is re-run (shouldn't happen after the early return, but be safe).
            var existing = arenaRoot.GetComponent<Crosshair>();
            if (existing == null)
            {
                arenaRoot.AddComponent<Crosshair>();
            }
        }

        /// <summary>
        /// Applies the collision matrix from Design.md section 6.
        /// PlayerHand ignores everything. PlayerBlocker collides with Player only.
        /// </summary>
        private static void ApplyLayerMatrix()
        {
            // PlayerHand ignores everything.
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(GameLayers.PlayerHand, i, true);

            // PlayerBlocker collides with Player only.
            for (int i = 0; i < 32; i++)
            {
                if (i == GameLayers.Player)
                    Physics.IgnoreLayerCollision(GameLayers.PlayerBlocker, i, false);
                else
                    Physics.IgnoreLayerCollision(GameLayers.PlayerBlocker, i, true);
            }

            // Player collides with Environment, PlayerBlocker, NPC.
            for (int i = 0; i < 32; i++)
            {
                bool collides = (i == GameLayers.Environment || i == GameLayers.PlayerBlocker || i == GameLayers.NPC);
                Physics.IgnoreLayerCollision(GameLayers.Player, i, !collides);
            }

            // NPC collides with Environment, NPC, Player.
            for (int i = 0; i < 32; i++)
            {
                bool collides = (i == GameLayers.Environment || i == GameLayers.NPC || i == GameLayers.Player);
                Physics.IgnoreLayerCollision(GameLayers.NPC, i, !collides);
            }

            // Environment collides with everything except PlayerBlocker.
            for (int i = 0; i < 32; i++)
            {
                bool collides = (i != GameLayers.PlayerBlocker);
                Physics.IgnoreLayerCollision(GameLayers.Environment, i, !collides);
            }
        }
    }
}
