using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    public sealed class PlayerMotorPlayModeTests
    {
        private readonly List<GameObject> extraObjects = new();
        private readonly List<Object> extraAssets = new();
        private GameObject ground;
        private GameObject playerObject;
        private GameObject cameraReferenceObject;
        private PlayerMovementSettings settings;
        private PlayerMotor motor;
        private Rigidbody body;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);

            playerObject = new GameObject("Test Player");
            playerObject.transform.position = new Vector3(0f, 0.05f, 0f);
            body = playerObject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            CapsuleCollider capsule = playerObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.5f;

            GameObject sensorObject = new("Ground Sensor");
            sensorObject.transform.SetParent(playerObject.transform, false);
            sensorObject.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            SphereCollider trigger = sensorObject.AddComponent<SphereCollider>();
            trigger.radius = 0.45f;
            trigger.isTrigger = true;
            PlayerGroundSensor sensor = sensorObject.AddComponent<PlayerGroundSensor>();

            cameraReferenceObject = new GameObject("Movement Reference");
            cameraReferenceObject.transform.forward = Vector3.forward;

            settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
            settings.moveSpeed = 5f;
            settings.jumpAcceleration = 10f;
            settings.maxVerticalSpeed = 30f;
            settings.slopeLimitDegrees = 45f;
            settings.groundProbeDistance = 1f;
            settings.groundProbeRadius = 0.15f;
            settings.groundMask = ~0;

            motor = playerObject.AddComponent<PlayerMotor>();
            motor.Configure(body, sensor, cameraReferenceObject.transform, playerObject.transform, settings);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            motor.TickFixed(Vector2.zero, false, true);
            Assert.That(motor.IsGrounded, Is.True, "Ground sensor did not reproduce the original trigger-based GroundCheck.");
            Assert.That(motor.IsOnStandableSlope, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject extraObject in extraObjects)
            {
                Object.Destroy(extraObject);
            }

            foreach (Object extraAsset in extraAssets)
            {
                Object.Destroy(extraAsset);
            }

            Object.Destroy(playerObject);
            Object.Destroy(cameraReferenceObject);
            Object.Destroy(ground);
            Object.Destroy(settings);
            extraObjects.Clear();
            extraAssets.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TickFixed_ForwardInputMovesAlongCameraForward()
        {
            float initialZ = body.position.z;

            for (int i = 0; i < 10; i++)
            {
                motor.TickFixed(Vector2.up, false, true);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(body.position.z, Is.GreaterThan(initialZ + 0.01f));
            Assert.That(motor.IsMoving, Is.True);
        }

        [UnityTest]
        public IEnumerator TickFixed_MovementFacingDisabled_MovesWithoutRotatingModel()
        {
            Quaternion initialRotation = Quaternion.Euler(0f, 37f, 0f);
            playerObject.transform.rotation = initialRotation;
            float initialX = body.position.x;
            motor.SetMovementFacingEnabled(false);

            for (int i = 0; i < 4; i++)
            {
                motor.TickFixed(Vector2.right, false, true);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(body.position.x, Is.GreaterThan(initialX + 0.01f));
            Assert.That(
                Quaternion.Angle(playerObject.transform.rotation, initialRotation),
                Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator TickFixed_MovementFacingReenabled_FacesMovementVelocity()
        {
            motor.SetMovementFacingEnabled(false);
            motor.SetMovementFacingEnabled(true);
            body.linearVelocity = Vector3.right;

            motor.TickFixed(Vector2.right, false, true);
            yield return null;

            Assert.That(
                Vector3.Angle(playerObject.transform.forward, Vector3.right),
                Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator TickFixed_JumpAddsUpwardVelocity()
        {
            motor.TickFixed(Vector2.zero, true, true);
            yield return new WaitForFixedUpdate();

            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator TickFixed_AfterSettlingOnFlatGround_AllowsJump()
        {
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            body.position = Vector3.zero;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();

            motor.TickFixed(Vector2.zero, false, true);
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(motor.IsOnStandableSlope, Is.True);

            motor.TickFixed(Vector2.zero, true, true);
            yield return new WaitForFixedUpdate();

            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator TickFixed_TranslatingRecalledPlatform_CarriesPlayer()
        {
            PhysicsMaterial zeroFriction = CreateZeroFrictionMaterial();
            GameObject platform = CreateRecalledPlatform(
                zeroFriction,
                out Rigidbody platformBody,
                out RewindCoordinator coordinator,
                out RewindRecorder recorder);
            ground.SetActive(false);
            playerObject.GetComponent<CapsuleCollider>().sharedMaterial = zeroFriction;

            for (int i = 0; i <= 10; i++)
            {
                platformBody.position = Vector3.forward * (i * 0.2f);
                recorder.CaptureNow();
            }

            body.position = platformBody.position + new Vector3(1.5f, 0.52f, 0f);
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            motor.TickFixed(Vector2.zero, false, true);
            Assert.That(motor.IsGrounded, Is.True);

            float initialLocalX = platform.transform.InverseTransformPoint(body.position).x;
            float initialPlayerZ = body.position.z;
            float initialPlatformZ = platformBody.position.z;
            recorder.enabled = true;
            Assert.That(coordinator.StartRewind(recorder), Is.True);

            for (int i = 0; i < 10; i++)
            {
                coordinator.TickFixed(Time.fixedDeltaTime);
                motor.TickFixed(Vector2.zero, false, true);
                yield return new WaitForFixedUpdate();
            }

            float playerWorldDisplacement = body.position.z - initialPlayerZ;
            float platformWorldDisplacement = platformBody.position.z - initialPlatformZ;
            float finalLocalX = platform.transform.InverseTransformPoint(body.position).x;
            Assert.That(Mathf.Abs(playerWorldDisplacement), Is.GreaterThan(0.01f));
            Assert.That(finalLocalX, Is.EqualTo(initialLocalX).Within(0.1f));
            TestContext.WriteLine(
                $"RecallTranslation playerDeltaZ={playerWorldDisplacement}; " +
                $"platformDeltaZ={platformWorldDisplacement}; localX={initialLocalX}->{finalLocalX}");
        }

        [UnityTest]
        public IEnumerator TickFixed_RotatingRecalledPlatform_CarriesOffCenterPlayerTangentially()
        {
            PhysicsMaterial zeroFriction = CreateZeroFrictionMaterial();
            GameObject platform = CreateRecalledPlatform(
                zeroFriction,
                out Rigidbody platformBody,
                out RewindCoordinator coordinator,
                out RewindRecorder recorder);
            ground.SetActive(false);
            playerObject.GetComponent<CapsuleCollider>().sharedMaterial = zeroFriction;

            for (int i = 0; i <= 10; i++)
            {
                platformBody.rotation = Quaternion.Euler(0f, i * 5f, 0f);
                recorder.CaptureNow();
            }

            body.position = platformBody.position +
                platformBody.rotation * (Vector3.right * 2f) + Vector3.up * 0.52f;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            motor.TickFixed(Vector2.zero, false, true);
            Assert.That(motor.IsGrounded, Is.True);

            Vector3 initialPlayerPosition = body.position;
            Vector3 initialPlatformPosition = platformBody.position;
            Vector3 initialLocalPosition = platform.transform.InverseTransformPoint(body.position);
            recorder.enabled = true;
            Assert.That(coordinator.StartRewind(recorder), Is.True);

            for (int i = 0; i < 10; i++)
            {
                coordinator.TickFixed(Time.fixedDeltaTime);
                motor.TickFixed(Vector2.zero, false, true);
                yield return new WaitForFixedUpdate();
            }

            float tangentialDisplacement = Vector3.Distance(
                Vector3.ProjectOnPlane(initialPlayerPosition, Vector3.up),
                Vector3.ProjectOnPlane(body.position, Vector3.up));
            float platformCenterDisplacement = Vector3.Distance(
                initialPlatformPosition,
                platformBody.position);
            Vector3 finalLocalPosition = platform.transform.InverseTransformPoint(body.position);
            float localDisplacement = Vector2.Distance(
                new Vector2(initialLocalPosition.x, initialLocalPosition.z),
                new Vector2(finalLocalPosition.x, finalLocalPosition.z));
            TestContext.WriteLine(
                $"RecallRotation playerTangentDelta={tangentialDisplacement}; " +
                $"platformCenterDelta={platformCenterDisplacement}; " +
                $"localXZ=({initialLocalPosition.x},{initialLocalPosition.z})->" +
                $"({finalLocalPosition.x},{finalLocalPosition.z}); localDelta={localDisplacement}");
            Assert.That(platformCenterDisplacement, Is.LessThan(0.01f));
            Assert.That(tangentialDisplacement, Is.GreaterThan(0.1f));
        }

        private PhysicsMaterial CreateZeroFrictionMaterial()
        {
            PhysicsMaterial material = new("Zero Friction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            extraAssets.Add(material);
            return material;
        }

        private GameObject CreateRecalledPlatform(
            PhysicsMaterial material,
            out Rigidbody platformBody,
            out RewindCoordinator coordinator,
            out RewindRecorder recorder)
        {
            RewindSettings rewindSettings = ScriptableObject.CreateInstance<RewindSettings>();
            rewindSettings.maxLinearSpeed = 50f;
            rewindSettings.maxAngularSpeed = 720f;
            extraAssets.Add(rewindSettings);

            GameObject coordinatorObject = new("Platform Rewind Coordinator");
            extraObjects.Add(coordinatorObject);
            coordinator = coordinatorObject.AddComponent<RewindCoordinator>();
            coordinator.Configure(rewindSettings);
            coordinator.enabled = false;

            GameObject platform = new("Recalled Platform");
            extraObjects.Add(platform);
            BoxCollider platformCollider = platform.AddComponent<BoxCollider>();
            platformCollider.size = new Vector3(8f, 1f, 8f);
            platformCollider.sharedMaterial = material;
            platformBody = platform.AddComponent<Rigidbody>();
            platformBody.useGravity = false;
            platformBody.mass = 1000f;
            platformBody.constraints = RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
            recorder = platform.AddComponent<RewindRecorder>();
            recorder.Configure(rewindSettings, coordinator);
            recorder.enabled = false;
            recorder.ClearHistory();
            return platform;
        }
    }
}
