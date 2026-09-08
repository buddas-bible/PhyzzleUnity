using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using Phyzzle.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>AttachHoldControllerPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class AttachHoldControllerPlayModeTests
    {
        private GameObject playerObject;
        private GameObject modelObject;
        private GameObject serviceObject;
        private GameObject targetObject;
        private GameObject rearObject;
        private GameObject inputObject;
        private GameObject transientBodyObject;
        private AttachSettings settings;
        private Rigidbody playerBody;
        private Rigidbody targetBody;
        private AttachmentService service;
        private AttachableObject target;
        private AttachHoldController controller;
        private Gamepad gamepad;
        private readonly InputTestFixture inputFixture = new();

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            inputFixture.Setup();
            settings = ScriptableObject.CreateInstance<AttachSettings>();
            settings.selectedMass = 0.1f;
            settings.selectedInertiaTensor = new Vector3(100f, 100f, 100f);
            settings.minTargetY = -4f;
            settings.maxTargetY = 10f;
            settings.minTargetZ = 1f;
            settings.maxTargetZ = 20f;
            settings.targetDepthStep = 2f;
            settings.rotationStepDegrees = 45f;
            settings.fineAdjustmentSpeed = 45f;

            gamepad = InputSystem.AddDevice<Gamepad>();

            playerObject = new GameObject("Player");
            modelObject = new GameObject("Model");
            modelObject.transform.SetParent(playerObject.transform, false);
            playerBody = playerObject.AddComponent<Rigidbody>();
            playerBody.isKinematic = true;

            serviceObject = new GameObject("AttachmentService");
            service = serviceObject.AddComponent<AttachmentService>();
            service.Configure(settings);

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.transform.position = new Vector3(0f, 0f, 5f);
            targetBody = targetObject.AddComponent<Rigidbody>();
            targetBody.mass = 3f;
            targetBody.useGravity = true;
            target = targetObject.AddComponent<AttachableObject>();
            target.Configure(service);

            controller = playerObject.AddComponent<AttachHoldController>();
            controller.Configure(modelObject.transform, playerBody, service, settings);
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
            Object.Destroy(rearObject);
            Object.Destroy(inputObject);
            Object.Destroy(transientBodyObject);
            Object.Destroy(serviceObject);
            Object.Destroy(settings);
            yield return null;
            inputFixture.TearDown();
        }

        /// <summary>
        /// <c>Begin_OffAxisTarget_SnapsAndClassifiesPostFacingLocalPose</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Begin_OffAxisTarget_SnapsAndClassifiesPostFacingLocalPose()
        {
            targetBody.position = new Vector3(5f, 0f, 0f);
            targetBody.rotation = Quaternion.AngleAxis(90f, Vector3.up) *
                                  Quaternion.AngleAxis(45f, Vector3.right);
            Physics.SyncTransforms();

            Assert.That(controller.Begin(target), Is.True);

            Assert.That(ReadSessionPoseType(controller), Is.EqualTo(AttachRotationPoseType.RotateX));
            Assert.That(Quaternion.Angle(ReadTargetRotation(controller),
                Quaternion.Euler(45f, 0f, 0f)), Is.LessThan(0.01f));
        }

        /// <summary>
        /// <c>TickUpdate_RotateHeldStepsInCppDirection</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(GamepadButton.DpadUp, (int)AttachRotationPoseType.RotateX, 45f, 1f, 0f)]
        [TestCase(GamepadButton.DpadDown, (int)AttachRotationPoseType.RotateX, -45f, 1f, 0f)]
        [TestCase(GamepadButton.DpadLeft, (int)AttachRotationPoseType.RotateY, 45f, 0f, 1f)]
        [TestCase(GamepadButton.DpadRight, (int)AttachRotationPoseType.RotateY, -45f, 0f, 1f)]
        public void TickUpdate_RotateHeldStepsInCppDirection(
            GamepadButton direction,
            int expectedTypeValue,
            float expectedDegrees,
            float axisX,
            float axisY)
        {
            AttachRotationPoseType expectedType = (AttachRotationPoseType)expectedTypeValue;
            Assert.That(controller.Begin(target), Is.True);
            PlayerInputReader input = CreateInput();
            SetGamepadState(0f, true, direction);
            input.Sample();

            controller.TickUpdate(input, 0.2f);

            Assert.That(ReadSessionPoseType(controller), Is.EqualTo(expectedType));
            Assert.That(Quaternion.Angle(ReadTargetRotation(controller),
                Quaternion.AngleAxis(expectedDegrees, new Vector3(axisX, axisY, 0f))),
                Is.LessThan(0.01f));
        }

        /// <summary>
        /// <c>TickUpdate_RotateHeldDiagonal_ProcessesVerticalBeforeHorizontal</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_RotateHeldDiagonal_ProcessesVerticalBeforeHorizontal()
        {
            Assert.That(controller.Begin(target), Is.True);
            PlayerInputReader input = CreateInput();
            SetGamepadState(0f, true, GamepadButton.DpadUp, GamepadButton.DpadLeft);
            input.Sample();

            controller.TickUpdate(input, 0.2f);

            Quaternion expected = Quaternion.AngleAxis(45f, Vector3.up) *
                                  Quaternion.AngleAxis(45f, Vector3.right);
            Assert.That(ReadSessionPoseType(controller), Is.EqualTo(AttachRotationPoseType.RotateXY));
            Assert.That(Quaternion.Angle(ReadTargetRotation(controller), expected), Is.LessThan(0.01f));
        }

        /// <summary>
        /// <c>TickUpdate_SpecialPoseUp_AppliesSixOperationCppTransition</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_SpecialPoseUp_AppliesSixOperationCppTransition()
        {
            Quaternion start = Quaternion.AngleAxis(-45f, Vector3.up) *
                               Quaternion.AngleAxis(45f, Vector3.right);
            targetBody.rotation = start;
            Physics.SyncTransforms();
            Assert.That(controller.Begin(target), Is.True);
            Assert.That(ReadSessionPoseType(controller), Is.EqualTo(AttachRotationPoseType.RotateX_Y));
            PlayerInputReader input = CreateInput();
            SetGamepadState(0f, true, GamepadButton.DpadUp);
            input.Sample();

            controller.TickUpdate(input, 0.2f);

            Assert.That(ReadSessionPoseType(controller), Is.EqualTo(AttachRotationPoseType.RotateXY));
            Assert.That(Quaternion.Angle(ReadTargetRotation(controller),
                ApplyLiteralOperations(start, "Y+ X+ Y- Y- X+ Y+", 45f)), Is.LessThan(0.01f));
        }

        /// <summary>
        /// <c>TickUpdate_LeftTriggerSuppressesStepAndOnlyFineAdjustsWithoutReclassification</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_LeftTriggerSuppressesStepAndOnlyFineAdjustsWithoutReclassification()
        {
            Quaternion start = Quaternion.AngleAxis(-45f, Vector3.up) *
                               Quaternion.AngleAxis(45f, Vector3.right);
            targetBody.rotation = start;
            Physics.SyncTransforms();
            Assert.That(controller.Begin(target), Is.True);
            PlayerInputReader input = CreateInput();
            SetGamepadState(1f, true, GamepadButton.DpadUp, GamepadButton.DpadLeft);
            input.Sample();

            controller.TickUpdate(input, 0.2f);

            Quaternion expected = Quaternion.AngleAxis(9f, Vector3.up) *
                                  Quaternion.AngleAxis(9f, Vector3.right) * start;
            Assert.That(ReadSessionPoseType(controller), Is.EqualTo(AttachRotationPoseType.RotateX_Y));
            Assert.That(Quaternion.Angle(ReadTargetRotation(controller), expected), Is.LessThan(0.01f));
        }

        /// <summary>
        /// <c>TickUpdate_LeftTriggerDepth_ExcludesOneShotDepthStep</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_LeftTriggerDepth_ExcludesOneShotDepthStep()
        {
            Assert.That(controller.Begin(target), Is.True);
            float initialDepth = ReadSession(controller).TargetLocalPosition.z;
            PlayerInputReader input = CreateInput();
            SetGamepadState(1f, false, GamepadButton.DpadUp);
            input.Sample();

            controller.TickUpdate(input, 0.2f);

            Assert.That(ReadSession(controller).TargetLocalPosition.z,
                Is.EqualTo(initialDepth + 1f).Within(0.001f));
        }

        /// <summary>
        /// <c>TickUpdate_ZeroLeftTriggerDepth_StepsOnceWithoutRepeatingWhileHeld</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_ZeroLeftTriggerDepth_StepsOnceWithoutRepeatingWhileHeld()
        {
            Assert.That(controller.Begin(target), Is.True);
            float initialDepth = ReadSession(controller).TargetLocalPosition.z;
            PlayerInputReader input = CreateInput();
            SetGamepadState(0f, false, GamepadButton.DpadUp);
            input.Sample();

            controller.TickUpdate(input, 0.2f);

            Assert.That(ReadSession(controller).TargetLocalPosition.z,
                Is.EqualTo(initialDepth + settings.targetDepthStep).Within(0.001f));

            input.Sample();
            controller.TickUpdate(input, 0.2f);

            Assert.That(ReadSession(controller).TargetLocalPosition.z,
                Is.EqualTo(initialDepth + settings.targetDepthStep).Within(0.001f));
        }

        /// <summary>
        /// <c>TickUpdate_LargeMouseDeltaRemainsUnclampedWhileHolding</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_LargeMouseDeltaRemainsUnclampedWhileHolding()
        {
            settings.targetVerticalSpeed = 0.4f;
            Assert.That(controller.Begin(target), Is.True);
            PlayerInputReader input = CreateInput();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(0f, 1800f));
            InputSystem.Update();
            input.Sample();

            controller.TickUpdate(input, 0.016f);

            Assert.That(ReadSession(controller).TargetLocalPosition.y,
                Is.EqualTo(0.8f).Within(0.001f));
        }

        /// <summary>
        /// <c>TickUpdate_GamepadLookRemainsRateInputWhileHolding</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_GamepadLookRemainsRateInputWhileHolding()
        {
            settings.targetVerticalSpeed = 10f;
            Assert.That(controller.Begin(target), Is.True);
            float initialHeight = ReadSession(controller).TargetLocalPosition.y;
            PlayerInputReader input = CreateInput();
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.up });
            InputSystem.Update();
            input.Sample();

            controller.TickUpdate(input, 0.02f);

            Assert.That(ReadSession(controller).TargetLocalPosition.y,
                Is.EqualTo(initialHeight + 0.2f).Within(0.001f));
        }

        /// <summary>
        /// <c>Release_ResetsRotationPoseAndTarget</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Release_ResetsRotationPoseAndTarget()
        {
            targetBody.rotation = Quaternion.Euler(45f, 0f, 0f);
            Physics.SyncTransforms();
            Assert.That(controller.Begin(target), Is.True);

            controller.Release();

            Assert.That(ReadSessionPoseType(controller), Is.EqualTo(AttachRotationPoseType.None));
            Assert.That(Quaternion.Angle(ReadTargetRotation(controller), Quaternion.identity),
                Is.LessThan(0.01f));
        }

        /// <summary>
        /// <c>BeginAndRelease_RestoresSelectedBodyProperties</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator BeginAndRelease_RestoresSelectedBodyProperties()
        {
            float originalMass = targetBody.mass;
            Vector3 originalInertia = targetBody.inertiaTensor;

            Assert.That(controller.Begin(target), Is.True);
            Assert.That(controller.IsHolding, Is.True);
            Assert.That(target.IsSelected, Is.True);
            Assert.That(targetBody.useGravity, Is.False);
            Assert.That(targetBody.mass, Is.EqualTo(settings.selectedMass).Within(0.0001f));

            controller.Release();
            yield return null;

            Assert.That(controller.IsHolding, Is.False);
            Assert.That(target.IsSelected, Is.False);
            Assert.That(targetBody.useGravity, Is.True);
            Assert.That(targetBody.mass, Is.EqualTo(originalMass).Within(0.0001f));
            Assert.That(Vector3.Distance(targetBody.inertiaTensor, originalInertia), Is.LessThan(0.0001f));
        }

        /// <summary>
        /// <c>TickFixed_TransfersPlayerLinearVelocityToHeldBody</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickFixed_TransfersPlayerLinearVelocityToHeldBody()
        {
            targetBody.useGravity = false;
            Assert.That(controller.Begin(target), Is.True);
            targetBody.linearVelocity = Vector3.zero;
            playerBody.isKinematic = false;
            playerBody.useGravity = false;
            playerBody.linearVelocity = Vector3.right * 2f;

            controller.TickFixed(0.02f);
            yield return new WaitForFixedUpdate();

            Assert.That(targetBody.linearVelocity.x, Is.GreaterThan(0f));
        }

        /// <summary>
        /// <c>Begin_AttachedIslandBehindPlayer_PushesTargetBeyondMinimumZ</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator Begin_AttachedIslandBehindPlayer_PushesTargetBeyondMinimumZ()
        {
            targetObject.transform.position = new Vector3(0f, 0f, 2f);
            rearObject = CreateAttachableCube("Rear", new Vector3(0f, 0f, 0f), out AttachableObject rear);
            Assert.That(service.Attach(target, rear, Vector3.forward), Is.True);
            Physics.SyncTransforms();

            Assert.That(controller.Begin(target), Is.True);
            targetBody.linearVelocity = Vector3.zero;
            controller.TickFixed(0.02f);
            yield return new WaitForFixedUpdate();

            Assert.That(targetBody.linearVelocity.z, Is.GreaterThan(0f));
        }

        /// <summary>
        /// <c>TickUpdate_HeldRigidbodyDestroyed_SkipsFrameAndKeepsHolding</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickUpdate_HeldRigidbodyDestroyed_SkipsFrameAndKeepsHolding()
        {
            inputObject = new GameObject("Input");
            PlayerInputReader input = inputObject.AddComponent<PlayerInputReader>();

            transientBodyObject = new GameObject("Transient Target Body");
            transientBodyObject.transform.SetPositionAndRotation(targetBody.position, targetBody.rotation);
            Rigidbody transientBody = transientBodyObject.AddComponent<Rigidbody>();
            FieldInfo bodyField = typeof(AttachableObject)
                .GetField("body", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bodyField, Is.Not.Null);
            bodyField.SetValue(target, transientBody);

            Assert.That(controller.Begin(target), Is.True);

            Object.Destroy(transientBody);
            yield return null;
            bodyField.SetValue(target, null);

            Assert.That(target.Body == null, Is.True);
            Assert.DoesNotThrow(() => controller.TickUpdate(input, 0.016f));
            Assert.That(controller.IsHolding, Is.True);
        }

        /// <summary>
        /// <c>Begin_SingleLargeColliderExtentBehindMinimum_PushesTargetForward</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator Begin_SingleLargeColliderExtentBehindMinimum_PushesTargetForward()
        {
            targetObject.transform.position = new Vector3(0f, 0f, 2f);
            targetObject.transform.localScale = new Vector3(1f, 1f, 4f);
            Physics.SyncTransforms();
            Assert.That(targetBody.position.z, Is.GreaterThan(settings.minTargetZ));

            Assert.That(controller.Begin(target), Is.True);
            targetBody.linearVelocity = Vector3.zero;
            controller.TickFixed(0.02f);
            yield return new WaitForFixedUpdate();

            Assert.That(targetBody.linearVelocity.z, Is.GreaterThan(0f));
        }

        /// <summary>
        /// <c>CreateAttachableCube</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private GameObject CreateAttachableCube(
            string name,
            Vector3 position,
            out AttachableObject attachable)
        {
            GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.name = name;
            created.transform.position = position;
            Rigidbody body = created.AddComponent<Rigidbody>();
            body.useGravity = false;
            attachable = created.AddComponent<AttachableObject>();
            attachable.Configure(service);
            return created;
        }

        /// <summary>
        /// <c>CreateInput</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private PlayerInputReader CreateInput()
        {
            inputObject = new GameObject("Input");
            return inputObject.AddComponent<PlayerInputReader>();
        }

        /// <summary>
        /// <c>SetGamepadState</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void SetGamepadState(float leftTrigger, bool rotateHeld, params GamepadButton[] buttons)
        {
            if (rotateHeld)
            {
                inputFixture.Press(gamepad.rightShoulder);
            }

            foreach (GamepadButton button in buttons)
            {
                inputFixture.Press(gamepad[button]);
            }

            inputFixture.Set(gamepad.leftTrigger, leftTrigger);
        }

        /// <summary>
        /// <c>ReadSession</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static AttachHoldSession ReadSession(AttachHoldController source)
        {
            FieldInfo sessionField = typeof(AttachHoldController)
                .GetField("session", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(sessionField, Is.Not.Null);
            return (AttachHoldSession)sessionField.GetValue(source);
        }

        /// <summary>
        /// <c>ReadTargetRotation</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static Quaternion ReadTargetRotation(AttachHoldController source)
            => ReadSession(source).TargetLocalRotation;

        /// <summary>
        /// <c>ReadSessionPoseType</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static AttachRotationPoseType ReadSessionPoseType(AttachHoldController source)
        {
            PropertyInfo poseProperty = typeof(AttachHoldSession)
                .GetProperty("RotationPoseType", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(poseProperty, Is.Not.Null);
            return (AttachRotationPoseType)poseProperty.GetValue(ReadSession(source));
        }

        /// <summary>
        /// <c>ApplyLiteralOperations</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static Quaternion ApplyLiteralOperations(
            Quaternion rotation,
            string operations,
            float degrees)
        {
            foreach (string operation in operations.Split(' '))
            {
                rotation = operation switch
                {
                    "X+" => Quaternion.AngleAxis(degrees, Vector3.right) * rotation,
                    "X-" => Quaternion.AngleAxis(-degrees, Vector3.right) * rotation,
                    "Y+" => Quaternion.AngleAxis(degrees, Vector3.up) * rotation,
                    "Y-" => Quaternion.AngleAxis(-degrees, Vector3.up) * rotation,
                    _ => throw new AssertionException($"Unknown literal operation: {operation}")
                };
            }

            return rotation;
        }
    }
}
