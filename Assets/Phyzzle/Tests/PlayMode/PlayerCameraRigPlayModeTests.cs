using System.Collections;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using Phyzzle.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerCameraRigPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerCameraRigPlayModeTests
    {
        private GameObject playerObject;
        private GameObject modelObject;
        private GameObject armObject;
        private GameObject coreObject;
        private GameObject targetObject;
        private PlayerCameraSettings settings;
        private PlayerCameraRig rig;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("Player");
            modelObject = new GameObject("Model");
            armObject = new GameObject("CameraArm");
            coreObject = new GameObject("CameraCore");
            targetObject = new GameObject("HeldTarget");

            modelObject.transform.SetParent(playerObject.transform, false);
            armObject.transform.SetParent(playerObject.transform, false);
            coreObject.transform.SetParent(armObject.transform, false);
            armObject.transform.localPosition = new Vector3(0f, 2f, 0f);
            coreObject.transform.localPosition = new Vector3(0f, 0f, -4f);
            targetObject.transform.position = new Vector3(0f, 0f, 5f);

            settings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
            settings.collisionMask = 0;
            settings.hideModelDistance = 0f;
            rig = playerObject.AddComponent<PlayerCameraRig>();
            rig.Configure(
                armObject.transform,
                coreObject.transform,
                modelObject.transform,
                settings);
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerObject);
            Object.Destroy(targetObject);
            Object.Destroy(settings);
            yield return null;
        }

        /// <summary>
        /// <c>EnterHoldingCamera_PreservesCurrentWorldPose</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void EnterHoldingCamera_PreservesCurrentWorldPose()
        {
            armObject.transform.localRotation = Quaternion.Euler(12f, 35f, 0f);
            modelObject.transform.localRotation = Quaternion.Euler(0f, 100f, 0f);
            Vector3 expectedPosition = coreObject.transform.position;
            Quaternion expectedRotation = coreObject.transform.rotation;

            rig.EnterHoldingCamera(targetObject.transform);

            Assert.That(Vector3.Distance(coreObject.transform.position, expectedPosition),
                Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(coreObject.transform.rotation, expectedRotation),
                Is.LessThan(0.001f));
        }

        /// <summary>
        /// <c>HoldingCamera_CopiesModelRotationBeforeApplyingPose</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator HoldingCamera_CopiesModelRotationBeforeApplyingPose()
        {
            rig.EnterHoldingCamera(targetObject.transform);
            modelObject.transform.localRotation = Quaternion.Euler(0f, 75f, 0f);
            yield return null;

            rig.TickLate(Vector2.zero, false);

            Assert.That(Quaternion.Angle(
                    armObject.transform.localRotation,
                    modelObject.transform.localRotation),
                Is.LessThan(0.001f));
        }

        /// <summary>
        /// <c>ExitHoldingCamera_MovesBackTowardDefaultPose</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator ExitHoldingCamera_MovesBackTowardDefaultPose()
        {
            rig.EnterHoldingCamera(targetObject.transform);
            coreObject.transform.localPosition = new Vector3(0f, 8f, -12f);
            float distanceBeforeExit = Vector3.Distance(
                coreObject.transform.localPosition,
                new Vector3(0f, 0f, -4f));

            rig.ExitHoldingCamera();
            rig.SetAbilityCamera(false);
            yield return null;
            rig.TickLate(Vector2.zero, false);

            float distanceAfterExit = Vector3.Distance(
                coreObject.transform.localPosition,
                new Vector3(0f, 0f, -4f));
            Assert.That(distanceAfterExit, Is.LessThan(distanceBeforeExit));
        }

        /// <summary>
        /// <c>TickLate_PointerDeltaAppliesPerFrameWithoutDeltaTimeScaling</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickLate_PointerDeltaAppliesPerFrameWithoutDeltaTimeScaling()
        {
            settings.sensitivity = 90f;
            System.Reflection.MethodInfo overload = typeof(PlayerCameraRig).GetMethod(
                "TickLate",
                new[] { typeof(Vector2), typeof(bool), typeof(bool) });
            Assert.That(overload, Is.Not.Null, "PlayerCameraRig must expose the source-aware overload.");

            overload.Invoke(rig, new object[] { new Vector2(0.02f, 0f), true, true });

            Assert.That(Quaternion.Angle(armObject.transform.localRotation, Quaternion.identity),
                Is.EqualTo(1.8f).Within(0.01f));
        }

        /// <summary>
        /// <c>TickLate_StickLookRemainsDegreesPerSecond</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickLate_StickLookRemainsDegreesPerSecond()
        {
            settings.sensitivity = 90f;
            Time.captureDeltaTime = 0.02f;
            yield return null;
            try
            {
                float frameDeltaTime = Time.deltaTime;
                rig.TickLate(new Vector2(0.5f, 0f), true);

                Assert.That(Quaternion.Angle(armObject.transform.localRotation, Quaternion.identity),
                    Is.EqualTo(45f * frameDeltaTime).Within(0.01f));
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }
        }
    }

    /// <summary>
    /// <c>AttachAbilityCameraPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class AttachAbilityCameraPlayModeTests
    {
        private GameObject playerObject;
        private GameObject targetObject;
        private GameObject serviceObject;
        private GameObject coreObject;
        private PlayerCameraSettings cameraSettings;
        private AttachSettings attachSettings;
        private AttachAbilityController ability;
        private PlayerMotor motor;
        private PlayerCameraRig rig;
        private Rigidbody targetBody;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("Player");
            GameObject modelObject = new GameObject("Model");
            GameObject armObject = new GameObject("CameraArm");
            coreObject = new GameObject("CameraCore");
            modelObject.transform.SetParent(playerObject.transform, false);
            armObject.transform.SetParent(playerObject.transform, false);
            coreObject.transform.SetParent(armObject.transform, false);
            armObject.transform.localPosition = new Vector3(0f, 2f, 0f);
            coreObject.transform.localPosition = new Vector3(0f, 0f, -4f);

            Rigidbody playerBody = playerObject.AddComponent<Rigidbody>();
            playerBody.isKinematic = true;
            motor = playerObject.AddComponent<PlayerMotor>();

            cameraSettings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
            cameraSettings.positionLerpTime = 0.001f;
            cameraSettings.collisionMask = 0;
            cameraSettings.hideModelDistance = 0f;
            attachSettings = ScriptableObject.CreateInstance<AttachSettings>();

            rig = playerObject.AddComponent<PlayerCameraRig>();
            rig.Configure(
                armObject.transform,
                coreObject.transform,
                modelObject.transform,
                cameraSettings);

            serviceObject = new GameObject("AttachmentService");
            AttachmentService service = serviceObject.AddComponent<AttachmentService>();
            service.Configure(attachSettings);

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "AttachTarget";
            targetObject.transform.position = new Vector3(0f, 2f, 5f);
            targetBody = targetObject.AddComponent<Rigidbody>();
            targetBody.useGravity = false;
            AttachableObject attachable = targetObject.AddComponent<AttachableObject>();
            attachable.Configure(service);

            AttachTargeting targeting = playerObject.AddComponent<AttachTargeting>();
            targeting.Configure(armObject.transform, coreObject.transform, attachSettings);
            AttachHoldController holdController = playerObject.AddComponent<AttachHoldController>();
            holdController.Configure(modelObject.transform, null, service, attachSettings);
            ability = playerObject.AddComponent<AttachAbilityController>();
            ability.Configure(motor, rig, targeting, holdController, attachSettings);
            Physics.SyncTransforms();
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerObject);
            Object.Destroy(targetObject);
            Object.Destroy(serviceObject);
            Object.Destroy(cameraSettings);
            Object.Destroy(attachSettings);
            yield return null;
        }

        /// <summary>
        /// <c>TryBeginHolding_MakesCameraFollowHeldTargetHeight</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TryBeginHolding_MakesCameraFollowHeldTargetHeight()
        {
            ability.EnterSelecting();
            Assert.That(ability.TryBeginHolding(), Is.True);
            float initialY = coreObject.transform.localPosition.y;
            float initialZ = coreObject.transform.localPosition.z;
            targetBody.position = new Vector3(0f, 10f, 5f);
            Physics.SyncTransforms();
            yield return null;

            rig.TickLate(Vector2.zero, false);

            Assert.That(coreObject.transform.localPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(coreObject.transform.localPosition.y, Is.GreaterThan(initialY));
            Assert.That(coreObject.transform.localPosition.z, Is.LessThan(initialZ));
        }

        /// <summary>
        /// <c>TryBeginHolding_DisablesMovementFacingUntilReturnToDefault</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TryBeginHolding_DisablesMovementFacingUntilReturnToDefault()
        {
            Assert.That(motor.MovementFacingEnabled, Is.True);
            ability.EnterSelecting();
            Assert.That(ability.TryBeginHolding(), Is.True);
            Assert.That(motor.MovementFacingEnabled, Is.False);

            ability.ReturnToDefault();
            Assert.That(motor.MovementFacingEnabled, Is.True);
            yield return null;
        }
    }
}
