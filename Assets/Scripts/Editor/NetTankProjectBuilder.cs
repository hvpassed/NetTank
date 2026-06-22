#if UNITY_EDITOR
using System;
using System.IO;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace NetTank.EditorTools
{
    /// <summary>
    /// Editor-only helper that creates the demo scene, prefabs, materials and UI.
    /// </summary>
    public static class NetTankProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainScene.unity";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string MaterialFolder = "Assets/Materials";

        [MenuItem("Tools/NetTank Arena/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            EnsureFolders();

            Material tankBlue = GetOrCreateMaterial("TankBlue", new Color(0.12f, 0.36f, 0.8f));
            Material tankDark = GetOrCreateMaterial("TankDark", new Color(0.08f, 0.1f, 0.12f));
            Material bulletYellow = GetOrCreateMaterial("BulletYellow", new Color(1f, 0.78f, 0.18f));
            Material groundGreen = GetOrCreateMaterial("GroundGreen", new Color(0.22f, 0.35f, 0.22f));
            Material wallGray = GetOrCreateMaterial("WallGray", new Color(0.32f, 0.32f, 0.34f));

            GameObject explosionPrefab = CreateExplosionPrefab();
            GameObject muzzleFlashPrefab = CreateMuzzleFlashPrefab();
            GameObject bulletPrefab = CreateBulletPrefab(bulletYellow);
            GameObject tankPrefab = CreateTankPrefab(tankBlue, tankDark, bulletPrefab, explosionPrefab, muzzleFlashPrefab);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainScene";

            CreateLightingAndCamera();
            SpawnPointGroup spawnPointGroup = CreateArena(groundGreen, wallGray);
            CreateNetworkRig(tankPrefab, bulletPrefab, spawnPointGroup);
            CreateUi();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("NetTank Arena demo scene built at Assets/Scenes/MainScene.unity");
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory("Assets/Scenes");
        }

        private static Material GetOrCreateMaterial(string materialName, Color color)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                material.color = color;
                EditorUtility.SetDirty(material);
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreateTankPrefab(Material bodyMaterial, Material accentMaterial, GameObject bulletPrefab, GameObject explosionPrefab, GameObject muzzleFlashPrefab)
        {
            string path = $"{PrefabFolder}/PlayerTank.prefab";

            GameObject root = new GameObject("PlayerTank");
            root.transform.position = Vector3.up * 0.5f;
            root.AddComponent<NetworkIdentity>();

            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 6f;
            rigidbody.linearDamping = 1f;
            rigidbody.angularDamping = 4f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(1.6f, 0.8f, 2.4f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            body.transform.localScale = new Vector3(1.6f, 0.55f, 2.2f);
            SetMaterial(body, bodyMaterial);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            turret.name = "Turret";
            turret.transform.SetParent(root.transform, false);
            turret.transform.localPosition = new Vector3(0f, 0.85f, 0.15f);
            turret.transform.localScale = new Vector3(0.55f, 0.2f, 0.55f);
            SetMaterial(turret, accentMaterial);
            UnityEngine.Object.DestroyImmediate(turret.GetComponent<Collider>());

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Barrel";
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.85f, 0.95f);
            barrel.transform.localScale = new Vector3(0.22f, 0.2f, 1.25f);
            SetMaterial(barrel, accentMaterial);
            UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());

            GameObject firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(root.transform, false);
            firePoint.transform.localPosition = new Vector3(0f, 0.85f, 1.65f);
            firePoint.transform.localRotation = Quaternion.identity;

            root.AddComponent<TankIdentity>();
            root.AddComponent<TankController>();
            TankHealth health = root.AddComponent<TankHealth>();
            TankShooting shooting = root.AddComponent<TankShooting>();

            SetObjectReference(health, "explosionPrefab", explosionPrefab);
            SetObjectReference(shooting, "bulletPrefab", bulletPrefab);
            SetObjectReference(shooting, "firePoint", firePoint.transform);
            SetObjectReference(shooting, "muzzleFlashPrefab", muzzleFlashPrefab);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBulletPrefab(Material bulletMaterial)
        {
            string path = $"{PrefabFolder}/Bullet.prefab";

            GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bullet.name = "Bullet";
            bullet.transform.localScale = Vector3.one * 0.32f;
            SetMaterial(bullet, bulletMaterial);

            SphereCollider collider = bullet.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            Rigidbody rigidbody = bullet.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            bullet.AddComponent<NetworkIdentity>();
            bullet.AddComponent<Bullet>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(bullet, path);
            UnityEngine.Object.DestroyImmediate(bullet);
            return prefab;
        }

        private static GameObject CreateExplosionPrefab()
        {
            string path = $"{PrefabFolder}/Explosion.prefab";

            GameObject explosion = new GameObject("Explosion");
            ParticleSystem particleSystem = explosion.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 0.7f;
            main.loop = false;
            main.startLifetime = 0.65f;
            main.startSpeed = 5f;
            main.startSize = 0.55f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.48f, 0.1f), new Color(1f, 0.9f, 0.25f));

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(explosion, path);
            UnityEngine.Object.DestroyImmediate(explosion);
            return prefab;
        }

        private static GameObject CreateMuzzleFlashPrefab()
        {
            string path = $"{PrefabFolder}/MuzzleFlash.prefab";

            GameObject flash = new GameObject("MuzzleFlash");
            ParticleSystem particleSystem = flash.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 0.18f;
            main.loop = false;
            main.startLifetime = 0.12f;
            main.startSpeed = 2.5f;
            main.startSize = 0.22f;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.yellow, new Color(1f, 0.35f, 0.05f));

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(flash, path);
            UnityEngine.Object.DestroyImmediate(flash);
            return prefab;
        }

        private static void CreateLightingAndCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 17f, -15f);
            camera.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            camera.fieldOfView = 55f;
            cameraObject.AddComponent<AudioListener>();

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        private static SpawnPointGroup CreateArena(Material groundMaterial, Material wallMaterial)
        {
            GameObject arena = new GameObject("Arena");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(arena.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            ground.transform.localScale = new Vector3(24f, 1f, 24f);
            SetMaterial(ground, groundMaterial);

            CreateWall("North Wall", new Vector3(0f, 0.7f, 12f), new Vector3(24f, 1.4f, 0.5f), wallMaterial, arena.transform);
            CreateWall("South Wall", new Vector3(0f, 0.7f, -12f), new Vector3(24f, 1.4f, 0.5f), wallMaterial, arena.transform);
            CreateWall("East Wall", new Vector3(12f, 0.7f, 0f), new Vector3(0.5f, 1.4f, 24f), wallMaterial, arena.transform);
            CreateWall("West Wall", new Vector3(-12f, 0.7f, 0f), new Vector3(0.5f, 1.4f, 24f), wallMaterial, arena.transform);

            CreateWall("Center Block A", new Vector3(-3.5f, 0.6f, 0f), new Vector3(2.2f, 1.2f, 2.2f), wallMaterial, arena.transform);
            CreateWall("Center Block B", new Vector3(4.2f, 0.6f, 1.8f), new Vector3(2.8f, 1.2f, 1.2f), wallMaterial, arena.transform);
            CreateWall("Center Block C", new Vector3(1.2f, 0.6f, -4.2f), new Vector3(1.2f, 1.2f, 3.2f), wallMaterial, arena.transform);

            GameObject spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(arena.transform, false);

            CreateSpawnPoint("SpawnPoint 1", new Vector3(-8f, 0.55f, -8f), Quaternion.Euler(0f, 45f, 0f), spawnRoot.transform);
            CreateSpawnPoint("SpawnPoint 2", new Vector3(8f, 0.55f, 8f), Quaternion.Euler(0f, 225f, 0f), spawnRoot.transform);
            CreateSpawnPoint("SpawnPoint 3", new Vector3(-8f, 0.55f, 8f), Quaternion.Euler(0f, 135f, 0f), spawnRoot.transform);
            CreateSpawnPoint("SpawnPoint 4", new Vector3(8f, 0.55f, -8f), Quaternion.Euler(0f, 315f, 0f), spawnRoot.transform);

            return spawnRoot.AddComponent<SpawnPointGroup>();
        }

        private static void CreateNetworkRig(GameObject tankPrefab, GameObject bulletPrefab, SpawnPointGroup spawnPointGroup)
        {
            GameObject networkRoot = new GameObject("NetworkManager");
            Transport transport = CreateTransport(networkRoot);
            NetTankNetworkManager manager = networkRoot.AddComponent<NetTankNetworkManager>();

            manager.playerPrefab = tankPrefab;
            manager.autoCreatePlayer = true;
            manager.maxConnections = 4;
            manager.spawnPrefabs.Clear();
            manager.spawnPrefabs.Add(bulletPrefab);

            if (transport != null)
            {
                manager.transport = transport;
                Transport.active = transport;
            }

            SetObjectReference(manager, "spawnPointGroup", spawnPointGroup);
        }

        private static Transport CreateTransport(GameObject target)
        {
            Type transportType = FindType("Mirror.TelepathyTransport");
            if (transportType == null)
            {
                Debug.LogWarning("Could not find Mirror.TelepathyTransport. Add a Transport component to NetworkManager manually.");
                return null;
            }

            return target.AddComponent(transportType) as Transport;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void CreateUi()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule uiInputModule = eventSystem.AddComponent<InputSystemUIInputModule>();
            InputActionAsset actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (actionsAsset != null)
            {
                uiInputModule.actionsAsset = actionsAsset;
            }
#else
            eventSystem.AddComponent<StandaloneInputModule>();
#endif

            GameObject canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            ConnectUI connectUi = canvasObject.AddComponent<ConnectUI>();
            PlayerHUD hud = canvasObject.AddComponent<PlayerHUD>();
            ScoreboardUI scoreboard = canvasObject.AddComponent<ScoreboardUI>();

            Text statusText = CreateText("StatusText", canvasObject.transform, "Status: Disconnected", 16, TextAnchor.MiddleLeft);
            SetAnchored(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -18f), new Vector2(260f, 28f));

            InputField ipInput = CreateInputField("IpInput", canvasObject.transform, "localhost");
            SetAnchored(ipInput.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -56f), new Vector2(180f, 30f));

            Button hostButton = CreateButton("HostButton", canvasObject.transform, "Host");
            SetAnchored(hostButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -56f), new Vector2(74f, 30f));

            Button clientButton = CreateButton("ClientButton", canvasObject.transform, "Client");
            SetAnchored(clientButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(292f, -56f), new Vector2(74f, 30f));

            Button serverButton = CreateButton("ServerButton", canvasObject.transform, "Server");
            SetAnchored(serverButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(374f, -56f), new Vector2(74f, 30f));

            Button stopButton = CreateButton("StopButton", canvasObject.transform, "Stop");
            SetAnchored(stopButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(456f, -56f), new Vector2(74f, 30f));

            Text healthText = CreateText("HealthText", canvasObject.transform, "HP: --", 18, TextAnchor.MiddleLeft);
            SetAnchored(healthText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 48f), new Vector2(220f, 30f));

            Text scoreText = CreateText("LocalScoreText", canvasObject.transform, "Score: --", 18, TextAnchor.MiddleLeft);
            SetAnchored(scoreText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 18f), new Vector2(220f, 30f));

            Text respawnText = CreateText("RespawnText", canvasObject.transform, "Respawning...", 24, TextAnchor.MiddleCenter);
            SetAnchored(respawnText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 44f));
            respawnText.enabled = false;

            Text scoreboardText = CreateText("ScoreboardText", canvasObject.transform, "Scoreboard\nWaiting for players...", 16, TextAnchor.UpperLeft);
            SetAnchored(scoreboardText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-286f, -16f), new Vector2(270f, 150f));

            SetObjectReference(connectUi, "hostButton", hostButton);
            SetObjectReference(connectUi, "clientButton", clientButton);
            SetObjectReference(connectUi, "serverButton", serverButton);
            SetObjectReference(connectUi, "stopButton", stopButton);
            SetObjectReference(connectUi, "ipInputField", ipInput);
            SetObjectReference(connectUi, "statusText", statusText);

            SetObjectReference(hud, "healthText", healthText);
            SetObjectReference(hud, "scoreText", scoreText);
            SetObjectReference(hud, "respawnText", respawnText);

            SetObjectReference(scoreboard, "scoreboardText", scoreboardText);
        }

        private static void CreateWall(string objectName, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = objectName;
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            SetMaterial(wall, material);
        }

        private static void CreateSpawnPoint(string objectName, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject point = new GameObject(objectName);
            point.transform.SetParent(parent, false);
            point.transform.SetPositionAndRotation(position, rotation);
        }

        private static Button CreateButton(string objectName, Transform parent, string label)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);
            Button button = buttonObject.AddComponent<Button>();

            Text labelText = CreateText("Label", buttonObject.transform, label, 15, TextAnchor.MiddleCenter);
            labelText.color = Color.white;
            SetAnchored(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            return button;
        }

        private static InputField CreateInputField(string objectName, Transform parent, string initialValue)
        {
            GameObject inputObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
            inputObject.transform.SetParent(parent, false);
            Image image = inputObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.9f);
            InputField inputField = inputObject.AddComponent<InputField>();

            Text text = CreateText("Text", inputObject.transform, initialValue, 15, TextAnchor.MiddleLeft);
            text.color = Color.black;
            SetAnchored(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, 0f));

            Text placeholder = CreateText("Placeholder", inputObject.transform, "IP", 15, TextAnchor.MiddleLeft);
            placeholder.color = new Color(0f, 0f, 0f, 0.45f);
            SetAnchored(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-16f, 0f));

            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.text = initialValue;

            return inputField;
        }

        private static Text CreateText(string objectName, Transform parent, string text, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(parent, false);
            Text uiText = textObject.AddComponent<Text>();
            uiText.text = text;
            uiText.font = GetBuiltinFont();
            uiText.fontSize = fontSize;
            uiText.alignment = alignment;
            uiText.color = Color.white;
            uiText.raycastTarget = false;
            return uiText;
        }

        private static Font GetBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static void SetAnchored(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Could not find serialized property {propertyName} on {target.name}");
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
