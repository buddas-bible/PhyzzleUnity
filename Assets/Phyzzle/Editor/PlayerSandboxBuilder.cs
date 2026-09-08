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
    /// <summary>
    /// 마이그레이션된 플레이어, 능력, HUD와 테스트 오브젝트를 포함한 검증용 샌드박스 씬을 생성한다.
    /// </summary>
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

        /// <summary>
        /// 필요한 설정·VFX 자산을 준비하고 플레이어 기능 검증용 샌드박스 씬 전체를 새로 생성한다.
        /// </summary>
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
            AttachVisualAssets attachVisualAssets = AttachVisualAssetBuilder.EnsureMaterials();
            AttachVisualAssetBuilder.EnsurePcRendererFeature(attachVisualAssets);
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
                rewindVisualAssets.Preview,
                attachVisualAssets.Projection,
                attachVisualAssets.Tether,
                attachVisualAssets.ContactPreview);
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

        /// <summary>
        /// 배치 모드나 CI에서 동일한 샌드박스 생성을 호출할 수 있는 진입점을 제공한다.
        /// </summary>
        public static void CreateSandboxFromCommandLine()
        {
            CreateSandbox();
        }

        /// <summary>
        /// 이동·경사 테스트용 지면과 램프, 기본 조명을 씬에 생성한다.
        /// </summary>
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

        /// <summary>
        /// 플레이어 물리, 모델, 카메라, 입력, 두 능력, VFX와 HUD를 생성하고 서로 연결한다.
        /// </summary>
        private static void CreatePlayer(
            PlayerMovementSettings movementSettings,
            PlayerCameraSettings cameraSettings,
            AttachSettings attachSettings,
            RewindSettings rewindSettings,
            AttachmentService attachmentService,
            RewindCoordinator rewindCoordinator,
            Material rewindPreviewMaterial,
            Material attachProjectionMaterial,
            Material attachTetherMaterial,
            Material attachContactPreviewMaterial)
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
            Camera gameplayCamera = cameraCoreObject.AddComponent<Camera>();
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
            EnsureAttachVisualComponents(
                root,
                gameplayCamera,
                attachAbility,
                targeting,
                holdController,
                attachmentService,
                attachSettings,
                attachProjectionMaterial,
                attachTetherMaterial,
                attachContactPreviewMaterial);
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

        /// <summary>
        /// 플레이어에 되감기 VisualController를 추가하거나 재사용하고 현재 자산 참조로 구성한다.
        /// </summary>
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

        /// <summary>
        /// 플레이어의 부착 투영·테더·접촉·글루 렌더러와 VisualController를 추가하거나 재사용해 연결한다.
        /// </summary>
        public static AttachVisualController EnsureAttachVisualComponents(
            GameObject player,
            Camera gameplayCamera,
            AttachAbilityController attachAbility,
            AttachTargeting targeting,
            AttachHoldController holdController,
            AttachmentService attachmentService,
            AttachSettings attachSettings,
            Material projectionMaterial,
            Material tetherMaterial,
            Material contactPreviewMaterial)
        {
            AttachProjectionRenderer projection =
                player.GetComponent<AttachProjectionRenderer>() ??
                player.AddComponent<AttachProjectionRenderer>();
            AttachVisualController visuals =
                player.GetComponent<AttachVisualController>() ??
                player.AddComponent<AttachVisualController>();
            AttachTetherRenderer tether =
                player.GetComponent<AttachTetherRenderer>() ??
                player.AddComponent<AttachTetherRenderer>();
            AttachContactPreviewRenderer contactPreview =
                player.GetComponent<AttachContactPreviewRenderer>() ??
                player.AddComponent<AttachContactPreviewRenderer>();
            AttachGlueRenderer glue =
                player.GetComponent<AttachGlueRenderer>() ??
                player.AddComponent<AttachGlueRenderer>();
            Transform model = player.transform.Find("Model") ?? player.transform;
            Transform handOrigin = model.Find("AttachHandOrigin");
            if (handOrigin == null)
            {
                handOrigin = new GameObject("AttachHandOrigin").transform;
                handOrigin.SetParent(model, false);
                // The sandbox has a capsule model; replace this anchor with a hand bone for a rigged character.
                handOrigin.localPosition = new Vector3(0.45f, 0.25f, 0.3f);
            }
            projection.Configure(gameplayCamera, attachSettings, projectionMaterial);
            tether.Configure(gameplayCamera, handOrigin, tetherMaterial, attachSettings);
            contactPreview.Configure(gameplayCamera, contactPreviewMaterial, attachSettings);
            glue.Configure(gameplayCamera, attachmentService, contactPreviewMaterial, attachSettings,
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcPipelinePath));
            visuals.Configure(
                attachAbility,
                targeting,
                holdController,
                attachmentService,
                projection,
                attachSettings,
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcPipelinePath),
                tether,
                contactPreview);
            EditorUtility.SetDirty(projection);
            EditorUtility.SetDirty(tether);
            EditorUtility.SetDirty(contactPreview);
            EditorUtility.SetDirty(glue);
            EditorUtility.SetDirty(visuals);
            return visuals;
        }

        /// <summary>
        /// 지정 설정을 사용하는 AttachmentService 오브젝트를 씬에 생성한다.
        /// </summary>
        private static AttachmentService CreateAttachmentService(AttachSettings settings)
        {
            GameObject serviceObject = new("AttachmentService");
            AttachmentService service = serviceObject.AddComponent<AttachmentService>();
            service.Configure(settings);
            return service;
        }

        /// <summary>
        /// 지정 설정을 사용하는 RewindCoordinator 오브젝트를 씬에 생성한다.
        /// </summary>
        private static RewindCoordinator CreateRewindCoordinator(RewindSettings settings)
        {
            GameObject serviceObject = new("RewindCoordinator");
            RewindCoordinator coordinator = serviceObject.AddComponent<RewindCoordinator>();
            coordinator.Configure(settings);
            return coordinator;
        }

        /// <summary>
        /// 부착과 되감기를 모두 시험할 수 있는 동적 큐브 오브젝트를 생성한다.
        /// </summary>
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

        /// <summary>
        /// 콜라이더를 제거한 캡슐 모델 오브젝트를 플레이어 자식으로 생성한다.
        /// </summary>
        private static Transform CreateModel(Transform parent)
        {
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Model";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(model.GetComponent<Collider>());
            return model.transform;
        }

        /// <summary>
        /// 플레이어 발밑에 트리거 SphereCollider와 PlayerGroundSensor를 생성한다.
        /// </summary>
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

        /// <summary>
        /// 지정 경로의 ScriptableObject 자산을 로드하고 없으면 새 자산을 생성한다.
        /// </summary>
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

        /// <summary>
        /// 선택 중 부착 오브젝트에 사용할 저마찰·고반발 PhysicsMaterial을 로드하거나 생성한다.
        /// </summary>
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

        /// <summary>
        /// 지정 부모 아래에 필요한 AssetDatabase 하위 폴더가 존재하도록 보장한다.
        /// </summary>
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