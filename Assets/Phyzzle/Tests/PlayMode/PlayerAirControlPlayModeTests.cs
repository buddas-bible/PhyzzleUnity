using System.Collections;
using NUnit.Framework;
using Phyzzle.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    public sealed class PlayerAirControlPlayModeTests
    {
        private GameObject ground;
        private GameObject playerObject;
        private GameObject cameraReferenceObject;
        private PlayerMovementSettings settings;
        private PlayerGroundSensor groundSensor;
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
            groundSensor = sensorObject.AddComponent<PlayerGroundSensor>();

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
            motor.Configure(
                body,
                groundSensor,
                cameraReferenceObject.transform,
                playerObject.transform,
                settings);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(playerObject);
            Object.Destroy(cameraReferenceObject);
            Object.Destroy(ground);
            Object.Destroy(settings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TickFixed_AirborneNoInput_PreservesExistingHorizontalMomentum()
        {
            yield return PrepareAirborne(Vector3.right * 10f);

            const int physicsSteps = 25;
            for (int i = 0; i < physicsSteps; i++)
            {
                motor.TickFixed(Vector2.zero, false, true);
                yield return new WaitForFixedUpdate();
            }

            float actualSpeed = body.linearVelocity.x;
            TestContext.WriteLine(
                $"DesiredAirNoInput initialX=10; steps={physicsSteps}; actualX={actualSpeed}");

            Assert.That(actualSpeed, Is.EqualTo(10f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator TickFixed_AirborneLateralInput_AddsControlWithoutBrakingExistingForwardMomentum()
        {
            yield return PrepareAirborne(Vector3.forward * 10f);

            const int physicsSteps = 10;
            for (int i = 0; i < physicsSteps; i++)
            {
                motor.TickFixed(Vector2.right, false, true);
                yield return new WaitForFixedUpdate();
            }

            Vector3 velocity = body.linearVelocity;
            TestContext.WriteLine(
                $"DesiredAirLateral initialZ=10; steps={physicsSteps}; " +
                $"actualX={velocity.x}; actualZ={velocity.z}");

            Assert.That(velocity.x, Is.GreaterThan(0.1f),
                "Air input should add limited lateral control.");
            Assert.That(velocity.z, Is.EqualTo(10f).Within(0.1f),
                "Air control should not erase existing forward momentum on an unrelated axis.");
        }

        [UnityTest]
        public IEnumerator TickFixed_AirborneSameDirectionInput_DoesNotClampFasterExistingMomentumToMoveSpeed()
        {
            yield return PrepareAirborne(Vector3.forward * 10f);

            const int physicsSteps = 25;
            for (int i = 0; i < physicsSteps; i++)
            {
                motor.TickFixed(Vector2.up, false, true);
                yield return new WaitForFixedUpdate();
            }

            float actualSpeed = body.linearVelocity.z;
            TestContext.WriteLine(
                $"DesiredAirSameDirection initialZ=10; moveSpeed={settings.moveSpeed}; " +
                $"steps={physicsSteps}; actualZ={actualSpeed}");

            Assert.That(actualSpeed, Is.EqualTo(10f).Within(0.1f),
                "Air input must not clamp externally acquired speed down to locomotion moveSpeed.");
        }

        private IEnumerator PrepareAirborne(Vector3 initialVelocity)
        {
            groundSensor.enabled = false;
            ground.SetActive(false);

            body.useGravity = false;
            body.position = Vector3.up * 10f;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();

            groundSensor.enabled = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            motor.TickFixed(Vector2.zero, false, false);
            Assert.That(motor.IsGrounded, Is.False,
                "Player should be airborne for air-control tests.");

            body.linearVelocity = initialVelocity;
        }
    }
}
