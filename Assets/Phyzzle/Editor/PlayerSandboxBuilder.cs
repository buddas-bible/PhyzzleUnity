using Phyzzle.Player;
using Phyzzle.Abilities;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Phyzzle.Editor
{
    public static class PlayerSandboxBuilder
    {
        private const string RootFolder = "Assets/Phyzzle";
        private const string SettingsFolder = RootFolder + "/Settings";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string MovementSettingsPath = SettingsFolder + "/PlayerMovementSettings.asset";
        private const string CameraSettingsPath = SettingsFolder + "/PlayerCameraSettings.asset";
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";
        private const string AttachSettingsPath = SettingsFolder + "/AttachSettings.asset";
        private const string RewindSettingsPath = SettingsFolder + "/RewindSettings.asset";
        private const string SelectedPhysicsMaterialPath = SettingsFolder + "/SelectedObjectPhysicsMaterial.asset";
        private const string ScenePath = ScenesFolder + "/PlayerMigrationSandbox.unity";

        [MenuItem("Phyzzle/Migration/Create Player Sandbox")]
        public static void CreateSandbox()
        {
            EnsureFolder(RootFolder, "Settings");
            EnsureFolder(RootFolder, "Scenes");

            PlayerMovementSettings movementSettings = LoadOrCreate<PlayerMovementSettings>(MovementSettingsPath);
            PlayerCameraSettings cameraSettings = LoadOrCreate<PlayerCameraSettings>(CameraSettingsPath);
            AttachSettings attachSettings = LoadOrCreate<AttachSettings>(AttachSettingsPath);
            RewindSettings rewindSettings = LoadOrCreate<RewindSettings>(RewindSettingsPath);
            RewindVisualAssets rewindVisualAssets = RewindVisualAssetBuilder.EnsureMaterials();
            RewindVisualAssetBuilder.EnsurePcRendererFeature(rewindVisualAssets);
            attachSettings.selectedPhysicsMaterial = LoadOrCreateSelectedPhysicsMaterial();

            movementSettings.groundMask = UnityEngine.Physics.DefaultRaycastLayers;
            cameraSettings.collisionMask = UnityEngine.Physics.DefaultRaycastLayers;
            attachSettings.nearbyMask = UnityEngine.Physics.DefaultRaycastLayers;
            attachSettings.targetMask = UnityEngine.Physics.DefaultRaycastLayers;
            rewindSettings.targetMask = UnityEngine.Physics.DefaultRaycastLayers;
            EditorUtility.SetDirty(movementSettings);
            EditorUtility.SetDirty(cameraSettings);
            EditorUtility.SetDirty(attachSettings);
            EditorUtility.SetDirty(rewindSettings);

            Physics.gravity = new Vector3(0f, -22f, 0f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEnvironment();
            AttachmentService attachmentService = CreateAttachmentService(attachSettings);
            RewindCoordinator rewindCoordinator = CreateRewindCoordinator(rewindSettings);
            CreatePlayer(
                movementSettings,
                cameraSettings,
                attachSettings,
                rewindSettings,
                attachmentService,
                rewindCoordinator,
                rewindVisualAssets.Preview);
            CreateAttachable(
                "Attachable_A",
                new Vector3(0f, 5f, 6f),
                attachmentService,
                rewindSettings,
                rewindCoordinator);
            CreateAttachable(
                "Attachable_B",
                new Vector3(2.1f, 8f, 6f),
                attachmentService,
                rewindSettings,
                rewindCoordinator);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = GameObject.Find("PhyzzlePlayer");

            Debug.Log($"Created Phyzzle player migration sandbox at {ScenePath}");
        }

        public static void CreateSandboxFromCommandLine()
        {
            CreateSandbox();
        }

        private static void CreateEnvironment()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Slope_20_Degrees";
            ramp.transform.SetPositionAndRotation(new Vector3(6f, 1f, 2f), Quaternion.Euler(0f, 0f, 20f));
            ramp.transform.localScale = new Vector3(6f, 0.5f, 5f);

            GameObject lightObject = new("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreatePlayer(
            PlayerMovementSettings movementSettings,
            PlayerCameraSettings cameraSettings,
            AttachSettings attachSettings,
            RewindSettings rewindSettings,
            AttachmentService attachmentService,
            RewindCoordinator rewindCoordinator,
            Material rewindPreviewMaterial)
        {
            GameObject root = new("PhyzzlePlayer");
            root.transform.position = new Vector3(0f, 0.1f, 0f);
            root.layer = 2;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.radius = 0.5f;
            capsule.height = 2f;

            Transform modelRoot = CreateModel(root.transform);
            PlayerGroundSensor sensor = CreateGroundSensor(root.transform);

            GameObject cameraArmObject = new("CameraArm");
            cameraArmObject.transform.SetParent(root.transform, false);
            cameraArmObject.transform.localPosition = new Vector3(0f, 2f, 0f);
            cameraArmObject.transform.localRotation = Quaternion.Euler(1f, 0f, 0f);

            GameObject cameraCoreObject = new("CameraCore");
            cameraCoreObject.transform.SetParent(cameraArmObject.transform, false);
            cameraCoreObject.transform.localPosition = new Vector3(0f, 0f, -4f);
            cameraCoreObject.AddComponent<Camera>();
            cameraCoreObject.AddComponent<AudioListener>();

            PlayerInputReader input = root.AddComponent<PlayerInputReader>();
            PlayerMotor motor = root.AddComponent<PlayerMotor>();
            PlayerCameraRig cameraRig = root.AddComponent<PlayerCameraRig>();
            PlayerAnimationDriver animationDriver = root.AddComponent<PlayerAnimationDriver>();
            AttachTargeting targeting = root.AddComponent<AttachTargeting>();
            AttachHoldController holdController = root.AddComponent<AttachHoldController>();
            AttachAbilityController attachAbility = root.AddComponent<AttachAbilityController>();
            RewindTargeting rewindTargeting = root.AddComponent<RewindTargeting>();
            RewindAbilityController rewindAbility = root.AddComponent<RewindAbilityController>();
            PlayerAbilityController abilityController = root.AddComponent<PlayerAbilityController>();
            PhyzzlePlayer player = root.AddComponent<PhyzzlePlayer>();

            motor.Configure(body, sensor, cameraArmObject.transform, modelRoot, movementSettings);
            cameraRig.Configure(cameraArmObject.transform, cameraCoreObject.transform, modelRoot, cameraSettings);
            targeting.Configure(cameraArmObject.transform, cameraCoreObject.transform, attachSettings);
            holdController.Configure(modelRoot, body, attachmentService, attachSettings);
            attachAbility.Configure(motor, cameraRig, targeting, holdController, attachSettings);
            rewindTargeting.Configure(
                cameraArmObject.transform,
                cameraCoreObject.transform,
                rewindSettings,
                rewindCoordinator);
            rewindAbility.Configure(cameraRig, rewindTargeting, rewindCoordinator, rewindSettings);
            EnsureRewindVisualController(
                root,
                rewindAbility,
                rewindTargeting,
                rewindSettings,
                rewindPreviewMaterial);
            abilityController.Configure(motor, attachAbility, rewindAbility);
            player.Configure(input, motor, cameraRig, animationDriver, abilityController);
            PlayerHudBuilder.CreateHud(
                input,
                attachAbility,
                holdController,
                attachmentService,
                rewindAbility);
        }

        public static RewindVisualController EnsureRewindVisualController(
            GameObject player,
            RewindAbilityController rewindAbility,
            RewindTargeting rewindTargeting,
            RewindSettings rewindSettings,
            Material rewindPreviewMaterial)
        {
            RewindVisualController controller =
                player.GetComponent<RewindVisualController>() ??
                player.AddComponent<RewindVisualController>();
            controller.Configure(
                rewindAbility,
                rewindTargeting,
                player.transform,
                rewindSettings,
                rewindPreviewMaterial,
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcPipelinePath));
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AttachmentService CreateAttachmentService(AttachSettings settings)
        {
            GameObject serviceObject = new("AttachmentService");
            AttachmentService service = serviceObject.AddComponent<AttachmentService>();
            service.Configure(settings);
            return service;
        }

        private static RewindCoordinator CreateRewindCoordinator(RewindSettings settings)
        {
            GameObject serviceObject = new("RewindCoordinator");
            RewindCoordinator coordinator = serviceObject.AddComponent<RewindCoordinator>();
            coordinator.Configure(settings);
            return coordinator;
        }

        private static void CreateAttachable(
            string name,
            Vector3 position,
            AttachmentService service,
            RewindSettings rewindSettings,
            RewindCoordinator rewindCoordinator)
        {
            GameObject attachableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            attachableObject.name = name;
            attachableObject.transform.position = position;
            attachableObject.transform.localScale = new Vector3(2f, 2f, 2f);

            Rigidbody body = attachableObject.AddComponent<Rigidbody>();
            body.mass = 10f;
            body.useGravity = true;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;

            AttachableObject attachable = attachableObject.AddComponent<AttachableObject>();
            attachable.Configure(service);
            RewindRecorder recorder = attachableObject.AddComponent<RewindRecorder>();
            recorder.Configure(rewindSettings, rewindCoordinator);
        }

        private static Transform CreateModel(Transform parent)
        {
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            return model.transform;
        }

        private static PlayerGroundSensor CreateGroundSensor(Transform parent)
        {
            GameObject sensorObject = new("GroundCheck");
            sensorObject.transform.SetParent(parent, false);
            sensorObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            SphereCollider trigger = sensorObject.AddComponent<SphereCollider>();
            trigger.radius = 0.45f;
            trigger.isTrigger = true;
            return sensorObject.AddComponent<PlayerGroundSensor>();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static PhysicsMaterial LoadOrCreateSelectedPhysicsMaterial()
        {
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(SelectedPhysicsMaterialPath);
            if (material != null)
            {
                return material;
            }

            material = new PhysicsMaterial("SelectedObject")
            {
                staticFriction = 0f,
                dynamicFriction = 0.05f,
                bounciness = 1f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(material, SelectedPhysicsMaterialPath);
            return material;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
