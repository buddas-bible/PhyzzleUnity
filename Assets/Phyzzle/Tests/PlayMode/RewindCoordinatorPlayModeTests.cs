using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Phyzzle.Abilities;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Player;
using Phyzzle.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>RewindCoordinatorPlayModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class RewindCoordinatorPlayModeTests
    {
        private readonly List<GameObject> extraObjects = new();
        private readonly List<ScriptableObject> extraAssets = new();
        private readonly InputTestFixture inputFixture = new();
        private GameObject serviceObject;
        private GameObject targetObject;
        private GameObject inputObject;
        private RewindSettings settings;
        private RewindCoordinator coordinator;
        private RewindRecorder recorder;
        private Rigidbody body;
        private PlayerInputReader reader;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private float initialTimeScale;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            initialTimeScale = Time.timeScale;
            inputFixture.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            inputObject = new GameObject("Input");
            reader = inputObject.AddComponent<PlayerInputReader>();

            settings = ScriptableObject.CreateInstance<RewindSettings>();

            serviceObject = new GameObject("Rewind Coordinator");
            coordinator = serviceObject.AddComponent<RewindCoordinator>();
            coordinator.Configure(settings);
            coordinator.enabled = false;

            targetObject = new GameObject("Rewind Target");
            targetObject.AddComponent<BoxCollider>();
            body = targetObject.AddComponent<Rigidbody>();
            body.useGravity = true;
            recorder = targetObject.AddComponent<RewindRecorder>();
            recorder.Configure(settings, coordinator);
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = initialTimeScale;
            foreach (GameObject extraObject in extraObjects)
            {
                Object.Destroy(extraObject);
            }

            foreach (ScriptableObject extraAsset in extraAssets)
            {
                Object.Destroy(extraAsset);
            }

            Object.Destroy(inputObject);
            Object.Destroy(targetObject);
            Object.Destroy(serviceObject);
            Object.Destroy(settings);
            extraObjects.Clear();
            extraAssets.Clear();
            yield return null;
            inputFixture.TearDown();
        }

        /// <summary>
        /// <c>StartRewind_KeepsDynamicBodyAndDisablesOnlyGravity</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void StartRewind_KeepsDynamicBodyAndDisablesOnlyGravity()
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;

            Assert.That(coordinator.StartRewind(recorder), Is.True);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        /// <summary>
        /// <c>EndRewind_ZerosVelocityAndRestoresOriginalGravity</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void EndRewind_ZerosVelocityAndRestoresOriginalGravity(bool initialUseGravity)
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            body.useGravity = initialUseGravity;
            coordinator.StartRewind(recorder);
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;

            coordinator.EndRewind();

            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.useGravity, Is.EqualTo(initialUseGravity));
            Assert.That(body.isKinematic, Is.False);
            TestContext.WriteLine(
                $"Release useGravity={body.useGravity}; isKinematic={body.isKinematic}; " +
                $"linearVelocity={body.linearVelocity}; angularVelocity={body.angularVelocity}");
        }

        /// <summary>
        /// <c>StartRewind_RejectsKinematicBody</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void StartRewind_RejectsKinematicBody()
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            body.isKinematic = true;

            Assert.That(coordinator.StartRewind(recorder), Is.False);
            Assert.That(coordinator.IsRewindingAny, Is.False);
            Assert.That(body.useGravity, Is.True);
        }

        /// <summary>
        /// <c>CanRewindAndStartRewind_RejectInactiveRecorder</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void CanRewindAndStartRewind_RejectInactiveRecorder()
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            recorder.enabled = false;

            Assert.That(recorder.isActiveAndEnabled, Is.False);
            Assert.That(coordinator.CanRewind(recorder), Is.False);
            Assert.That(coordinator.StartRewind(recorder), Is.False);
            Assert.That(coordinator.IsRewindingAny, Is.False);
            Assert.That(body.useGravity, Is.True);
        }

        /// <summary>
        /// <c>DisablingActiveRecorder_EndsRecallAndRestoresBody</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void DisablingActiveRecorder_EndsRecallAndRestoresBody(bool initialUseGravity)
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            body.useGravity = initialUseGravity;
            Assert.That(coordinator.StartRewind(recorder), Is.True);
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;

            recorder.enabled = false;

            Assert.That(coordinator.IsRewindingAny, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.useGravity, Is.EqualTo(initialUseGravity));
        }

        /// <summary>
        /// <c>TickFixed_MissingSettingsEndsRecallAndRestoresLiveBody</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void TickFixed_MissingSettingsEndsRecallAndRestoresLiveBody(bool initialUseGravity)
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            body.useGravity = initialUseGravity;
            Assert.That(coordinator.StartRewind(recorder), Is.True);
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;
            coordinator.Configure(null);

            coordinator.TickFixed(Time.fixedDeltaTime);

            Assert.That(coordinator.IsRewindingAny, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.useGravity, Is.EqualTo(initialUseGravity));
        }

        /// <summary>
        /// <c>StartRewind_RejectsSecondTargetWhileOneIsActive</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void StartRewind_RejectsSecondTargetWhileOneIsActive()
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            GameObject secondObject = new("Second Rewind Target");
            extraObjects.Add(secondObject);
            secondObject.transform.position = Vector3.right * 4f;
            secondObject.AddComponent<BoxCollider>();
            Rigidbody secondBody = secondObject.AddComponent<Rigidbody>();
            RewindRecorder secondRecorder = secondObject.AddComponent<RewindRecorder>();
            secondRecorder.Configure(settings, coordinator);
            secondBody.position = Vector3.right * 5f;
            secondRecorder.CaptureNow();

            Assert.That(coordinator.StartRewind(recorder), Is.True);
            Assert.That(coordinator.StartRewind(secondRecorder), Is.False);
            Assert.That(coordinator.Current, Is.SameAs(recorder));
            Assert.That(secondRecorder.IsRewinding, Is.False);
        }

        /// <summary>
        /// <c>TickFixed_DestroyedRigidbodyClearsOwnershipWithoutThrowing</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickFixed_DestroyedRigidbodyClearsOwnershipWithoutThrowing()
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            GameObject detachedBodyObject = new("Detached Recall Body");
            extraObjects.Add(detachedBodyObject);
            Rigidbody detachedBody = detachedBodyObject.AddComponent<Rigidbody>();
            System.Reflection.FieldInfo recorderBodyField = typeof(RewindRecorder).GetField(
                "body",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(recorderBodyField, Is.Not.Null);
            recorderBodyField.SetValue(recorder, detachedBody);
            Assert.That(coordinator.StartRewind(recorder), Is.True);

            Object.Destroy(detachedBody);
            yield return null;

            Assert.DoesNotThrow(() => coordinator.TickFixed(Time.fixedDeltaTime));
            Assert.That(ReferenceEquals(coordinator.Current, null), Is.True);
            Assert.That(coordinator.IsRewindingAny, Is.False);
        }

        /// <summary>
        /// <c>EndRewind_DestroyedTargetClearsOwnershipWithoutThrowing</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator EndRewind_DestroyedTargetClearsOwnershipWithoutThrowing()
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            Assert.That(coordinator.StartRewind(recorder), Is.True);

            Object.Destroy(targetObject);
            yield return null;

            Assert.DoesNotThrow(coordinator.EndRewind);
            Assert.That(ReferenceEquals(coordinator.Current, null), Is.True);
            Assert.That(coordinator.IsRewindingAny, Is.False);
        }

        /// <summary>
        /// <c>TickFixed_RotatedHistoryCommandsAngularVelocityBeforePhysicsRotatesBody</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickFixed_RotatedHistoryCommandsAngularVelocityBeforePhysicsRotatesBody()
        {
            settings.maxAngularSpeed = 180f;
            Capture(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            Capture(Vector3.right, Quaternion.Euler(0f, 45f, 0f));
            body.position = Vector3.right;
            body.rotation = Quaternion.identity;
            Quaternion rotationBeforeTick = body.rotation;
            Assert.That(coordinator.StartRewind(recorder), Is.True);

            coordinator.TickFixed(Time.fixedDeltaTime);
            Vector3 commandedAngularVelocity = body.angularVelocity;

            Assert.That(Mathf.Abs(commandedAngularVelocity.y), Is.GreaterThan(0.1f));
            Assert.That(Quaternion.Angle(body.rotation, rotationBeforeTick), Is.LessThan(0.001f));
            yield return new WaitForFixedUpdate();

            Assert.That(Quaternion.Angle(body.rotation, rotationBeforeTick), Is.GreaterThan(0.1f));
            TestContext.WriteLine(
                $"AngularDriveY={commandedAngularVelocity.y}; " +
                $"prePhysicsAngle=0; postPhysicsAngle={Quaternion.Angle(body.rotation, rotationBeforeTick)}");
        }

        /// <summary>
        /// <c>TickFixed_UnobstructedBodyStaysDynamicAndReleasesAfterFinalPhysicsStep</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickFixed_UnobstructedBodyStaysDynamicAndReleasesAfterFinalPhysicsStep()
        {
            settings.maxLinearSpeed = 5f;
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right * 2f, Quaternion.identity);
            body.position = Vector3.right * 2f;

            Assert.That(coordinator.StartRewind(recorder), Is.True);

            coordinator.TickFixed(Time.fixedDeltaTime);
            Assert.That(body.isKinematic, Is.False);
            yield return new WaitForFixedUpdate();

            coordinator.TickFixed(Time.fixedDeltaTime);
            float positionBeforeFinalStep = body.position.x;
            float finalTargetVelocity = body.linearVelocity.x;
            Assert.That(finalTargetVelocity, Is.LessThan(0f));
            Assert.That(coordinator.IsRewindingAny, Is.True);
            yield return new WaitForFixedUpdate();

            Assert.That(body.position.x, Is.LessThan(positionBeforeFinalStep));
            Assert.That(coordinator.IsRewindingAny, Is.True);
            coordinator.TickFixed(Time.fixedDeltaTime);

            Assert.That(coordinator.IsRewindingAny, Is.False);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            TestContext.WriteLine(
                $"FinalTargetVelocity={finalTargetVelocity}; positionBefore={positionBeforeFinalStep}; " +
                $"positionAfter={body.position.x}; released={coordinator.IsRewindingAny == false}");
        }

        /// <summary>
        /// <c>TickFixed_RecallReversesTargetWhileGameplayProbeKeepsUpdating</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickFixed_RecallReversesTargetWhileGameplayProbeKeepsUpdating()
        {
            settings.maxLinearSpeed = 10f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotation;
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right * 0.5f, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            body.position = Vector3.right;
            ForwardGameplayProbe probe = targetObject.AddComponent<ForwardGameplayProbe>();
            int probeTicksBeforeRecall = probe.FixedUpdateCount;
            float positionBeforeRecall = body.position.x;

            Assert.That(coordinator.StartRewind(recorder), Is.True);
            for (int step = 0; step < 2; step++)
            {
                coordinator.TickFixed(Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(body.position.x, Is.LessThan(positionBeforeRecall - 0.01f));
            Assert.That(probe.FixedUpdateCount, Is.GreaterThan(probeTicksBeforeRecall));
            Assert.That(coordinator.IsRewindingAny, Is.True);
            TestContext.WriteLine(
                $"GameplayProbeTicks={probe.FixedUpdateCount - probeTicksBeforeRecall}; " +
                $"RecallDisplacement={body.position.x - positionBeforeRecall:R}");
        }

        /// <summary>
        /// <c>TickFixed_StaticWallBlocksRecallWithoutTeleportCorrection</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickFixed_StaticWallBlocksRecallWithoutTeleportCorrection()
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right * 0.5f, Quaternion.identity);
            Capture(Vector3.right, Quaternion.identity);
            Capture(Vector3.right * 1.5f, Quaternion.identity);
            Capture(Vector3.right * 2f, Quaternion.identity);
            body.position = Vector3.right * 2f;

            GameObject wallObject = new("Recall Wall");
            extraObjects.Add(wallObject);
            wallObject.transform.position = Vector3.right;
            BoxCollider wallCollider = wallObject.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(0.5f, 4f, 4f);
            UnityEngine.Physics.SyncTransforms();

            Assert.That(coordinator.StartRewind(recorder), Is.True);

            RewindCollisionProbe collisionProbe = targetObject.AddComponent<RewindCollisionProbe>();
            for (int step = 0; step < 3; step++)
            {
                coordinator.TickFixed(Time.fixedDeltaTime);
                Assert.That(body.isKinematic, Is.False);
                yield return new WaitForFixedUpdate();
                Assert.That(body.position.x, Is.GreaterThanOrEqualTo(1.25f));
            }

            Assert.That(collisionProbe.ContactCount, Is.GreaterThan(0));
            float blockedPosition = body.position.x;
            Object.Destroy(wallObject);
            extraObjects.Remove(wallObject);
            yield return null;

            coordinator.TickFixed(Time.fixedDeltaTime);
            float postRemovalVelocity = body.linearVelocity.x;
            Assert.That(postRemovalVelocity, Is.LessThan(0f));
            yield return new WaitForFixedUpdate();

            Assert.That(body.position.x, Is.LessThan(blockedPosition));
            Assert.That(body.isKinematic, Is.False);
            TestContext.WriteLine(
                $"ObstacleContactCount={collisionProbe.ContactCount}; blockedX={blockedPosition}; " +
                $"postRemovalVelocityX={postRemovalVelocity}; postRemovalX={body.position.x}");
        }

        /// <summary>
        /// <c>TickFixed_FixedJointMovesConnectedDynamicBodyWithoutRecallingIt</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickFixed_FixedJointMovesConnectedDynamicBodyWithoutRecallingIt()
        {
            settings.maxLinearSpeed = 10f;
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.right * 2f, Quaternion.identity);
            body.position = Vector3.right * 2f;

            GameObject connectedObject = new("Joint Connected Body");
            extraObjects.Add(connectedObject);
            connectedObject.transform.position = Vector3.right * 3f;
            connectedObject.AddComponent<BoxCollider>();
            Rigidbody connectedBody = connectedObject.AddComponent<Rigidbody>();
            connectedBody.useGravity = false;
            RewindRecorder connectedRecorder = connectedObject.AddComponent<RewindRecorder>();
            connectedRecorder.Configure(settings, coordinator);
            FixedJoint joint = targetObject.AddComponent<FixedJoint>();
            joint.connectedBody = connectedBody;
            float connectedStartX = connectedBody.position.x;
            float targetStartX = body.position.x;
            UnityEngine.Physics.SyncTransforms();

            Assert.That(coordinator.StartRewind(recorder), Is.True);
            Assert.That(coordinator.IsRewinding(connectedRecorder), Is.False);

            coordinator.TickFixed(Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
            coordinator.TickFixed(Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();

            float targetDisplacement = body.position.x - targetStartX;
            float connectedDisplacement = connectedBody.position.x - connectedStartX;
            Assert.That(body.isKinematic, Is.False);
            Assert.That(connectedBody.isKinematic, Is.False);
            Assert.That(coordinator.IsRewinding(connectedRecorder), Is.False);
            Assert.That(Mathf.Abs(targetDisplacement), Is.GreaterThan(0.05f));
            Assert.That(Mathf.Abs(connectedDisplacement), Is.GreaterThan(0.05f));
            TestContext.WriteLine(
                $"JointTargetDisplacement={targetDisplacement}; " +
                $"JointConnectedDisplacement={connectedDisplacement}; " +
                $"connectedRewinding={coordinator.IsRewinding(connectedRecorder)}");
        }

        /// <summary>
        /// <c>TickFixed_AttachAuthoredRootRecallMovesConnectedMemberWithoutTargetingIt</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TickFixed_AttachAuthoredRootRecallMovesConnectedMemberWithoutTargetingIt()
        {
            settings.maxLinearSpeed = 10f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            recorder.enabled = false;
            recorder.ClearHistory();

            AttachSettings attachSettings = ScriptableObject.CreateInstance<AttachSettings>();
            extraAssets.Add(attachSettings);
            GameObject attachmentServiceObject = new("Attachment Service");
            extraObjects.Add(attachmentServiceObject);
            AttachmentService attachmentService =
                attachmentServiceObject.AddComponent<AttachmentService>();
            attachmentService.Configure(attachSettings);

            AttachableObject root = targetObject.AddComponent<AttachableObject>();
            root.Configure(attachmentService);
            GameObject memberObject = new("Attached Recall Member");
            extraObjects.Add(memberObject);
            memberObject.transform.position = Vector3.right;
            memberObject.AddComponent<BoxCollider>();
            Rigidbody memberBody = memberObject.AddComponent<Rigidbody>();
            memberBody.useGravity = false;
            memberBody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            AttachableObject member = memberObject.AddComponent<AttachableObject>();
            member.Configure(attachmentService);
            RewindRecorder memberRecorder = memberObject.AddComponent<RewindRecorder>();
            memberRecorder.Configure(settings, coordinator);
            memberRecorder.enabled = false;

            Assert.That(attachmentService.Attach(root, member, Vector3.right * 0.5f), Is.True);
            attachmentService.SelectIsland(root);
            Assert.That(root.IsSelected, Is.True);
            Assert.That(member.IsSelected, Is.True);

            for (int i = 0; i < 3; i++)
            {
                body.position = Vector3.right * i;
                memberBody.position = Vector3.right * (i + 1f);
                Physics.SyncTransforms();
                recorder.CaptureNow();
                Assert.That(root.IsSelected, Is.True);
            }

            attachmentService.DeselectIsland(root);
            Assert.That(root.IsSelected, Is.False);
            Assert.That(member.IsSelected, Is.False);
            recorder.enabled = true;
            float rootStartX = body.position.x;
            float memberStartX = memberBody.position.x;
            Assert.That(coordinator.StartRewind(recorder), Is.True);
            Assert.That(coordinator.Current, Is.SameAs(recorder));

            for (int i = 0; i < 3; i++)
            {
                coordinator.TickFixed(Time.fixedDeltaTime);
                Assert.That(coordinator.Current, Is.Not.SameAs(memberRecorder));
                yield return new WaitForFixedUpdate();
            }

            float rootDisplacement = body.position.x - rootStartX;
            float memberDisplacement = memberBody.position.x - memberStartX;
            Assert.That(rootDisplacement, Is.LessThan(-0.05f));
            Assert.That(Mathf.Abs(memberDisplacement), Is.GreaterThan(0.05f));
            Assert.That(coordinator.Current, Is.Not.SameAs(memberRecorder));
            TestContext.WriteLine(
                $"AttachRecall rootDisplacement={rootDisplacement}; " +
                $"memberDisplacement={memberDisplacement}; " +
                $"memberWasTarget={coordinator.Current == memberRecorder}");
        }

        /// <summary>
        /// <c>StartRewind_RejectsHistoryWithFewerThanTwoSnapshots</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void StartRewind_RejectsHistoryWithFewerThanTwoSnapshots()
        {
            Assert.That(coordinator.StartRewind(recorder), Is.False);
            Assert.That(coordinator.IsRewindingAny, Is.False);
        }

        /// <summary>
        /// <c>Targeting_FindsRecordedDynamicBodyAlongCameraRay</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Targeting_FindsRecordedDynamicBodyAlongCameraRay()
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.zero, Quaternion.identity);

            GameObject armObject = new("Camera Arm");
            GameObject coreObject = new("Camera Core");
            coreObject.transform.SetParent(armObject.transform, false);
            coreObject.transform.localPosition = new Vector3(0f, 0f, -4f);
            RewindTargeting targeting = armObject.AddComponent<RewindTargeting>();
            targeting.Configure(armObject.transform, coreObject.transform, settings, coordinator);
            UnityEngine.Physics.SyncTransforms();

            Assert.That(targeting.Refresh(), Is.SameAs(recorder));
            CollectionAssert.Contains((ICollection)targeting.Nearby, recorder);

            Object.DestroyImmediate(armObject);
        }

        /// <summary>
        /// <c>AbilitySelection_PausesAndRestoresWorldTime</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void AbilitySelection_PausesAndRestoresWorldTime()
        {
            GameObject abilityObject = new("Rewind Ability");
            RewindAbilityController ability = abilityObject.AddComponent<RewindAbilityController>();
            ability.Configure(null, null, coordinator, settings);

            try
            {
                ability.EnterSelecting();
                Assert.That(Time.timeScale, Is.Zero);

                ability.ReturnToDefault();
                Assert.That(Time.timeScale, Is.EqualTo(initialTimeScale));
            }
            finally
            {
                Time.timeScale = initialTimeScale;
                Object.DestroyImmediate(abilityObject);
            }
        }

        /// <summary>
        /// <c>TryStartRewind_EntersRewindingAndReleasesSelectionPause</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [UnityTest]
        public IEnumerator TryStartRewind_EntersRewindingAndReleasesSelectionPause()
        {
            const float deterministicDeltaTime = 0.02f;
            const int convergenceSteps = 40;
            RewindAbilityController ability = CreateAbility(cameraPositionLerpTime: 0.1f);
            PlayerCameraRig cameraRig = ability.GetComponent<PlayerCameraRig>();
            Transform cameraCore = ability.transform.Find("Camera Core");
            Vector3 defaultCameraPosition = cameraCore.localPosition;
            Time.captureDeltaTime = deterministicDeltaTime;
            yield return null;

            try
            {
                Assert.That(Time.deltaTime, Is.EqualTo(deterministicDeltaTime).Within(0.000001f));

                ability.EnterSelecting();
                cameraRig.TickLate(Vector2.zero, allowInput: true);
                float selectingDistance =
                    Vector3.Distance(cameraCore.localPosition, defaultCameraPosition);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(ability.BlocksJump, Is.True);
                Assert.That(selectingDistance, Is.GreaterThan(0.0001f));

                Assert.That(ability.TryStartRewind(), Is.True);
                cameraRig.TickLate(Vector2.zero, allowInput: true);
                float firstReturnDistance =
                    Vector3.Distance(cameraCore.localPosition, defaultCameraPosition);

                Assert.That(firstReturnDistance, Is.LessThan(selectingDistance));
                Assert.That(firstReturnDistance, Is.GreaterThan(0.0001f));
                for (int step = 1; step < convergenceSteps; step++)
                {
                    cameraRig.TickLate(Vector2.zero, allowInput: true);
                }

                float convergedDistance =
                    Vector3.Distance(cameraCore.localPosition, defaultCameraPosition);
                TestContext.WriteLine(
                    $"CAMERA_DELTA={deterministicDeltaTime:R}; " +
                    $"STEPS={convergenceSteps}; " +
                    $"INTERVAL={convergenceSteps * deterministicDeltaTime:R}; " +
                    $"SELECTING_DISTANCE={selectingDistance:R}; " +
                    $"FIRST_RETURN_DISTANCE={firstReturnDistance:R}; " +
                    $"CONVERGED_DISTANCE={convergedDistance:R}");
                Assert.That(convergedDistance, Is.LessThan(0.0001f));
                Assert.That(ability.State,
                    Is.EqualTo(RewindAbilityController.AbilityState.Rewinding));
                Assert.That(coordinator.IsRewindingAny, Is.True);
                Assert.That(ability.CurrentTarget, Is.Null);
                Assert.That(ability.BlocksJump, Is.False);
                Assert.That(ability.AllowDefaultCameraInput, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(initialTimeScale));

                PlayerHudState hud = PlayerHudPresenterState.Build(
                    AttachAbilityController.AbilityState.Default,
                    attachHasTarget: false,
                    ability.State,
                    rewindHasTarget: false,
                    rotateHeld: false,
                    touchingAttachable: false,
                    islandSize: 1);
                Assert.That(hud.Mode, Is.EqualTo(PlayerHudMode.Default));
                Assert.That(PlayerHudStateResolver.Resolve(hud).AnyVisible, Is.False);
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }
        }

        /// <summary>
        /// <c>TryStartRewind_FromDefaultWithTarget_ReturnsFalse</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TryStartRewind_FromDefaultWithTarget_ReturnsFalse()
        {
            RewindAbilityController ability = CreateAbility();
            RewindTargeting targeting = ability.GetComponent<RewindTargeting>();
            Assert.That(targeting.Refresh(), Is.SameAs(recorder));
            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Default));

            Assert.That(ability.TryStartRewind(), Is.False);

            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Default));
            Assert.That(coordinator.IsRewindingAny, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(initialTimeScale));
        }

        /// <summary>
        /// <c>ReturnToDefault_WhileRewinding_ReleasesBodyAndRestoresState</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void ReturnToDefault_WhileRewinding_ReleasesBodyAndRestoresState()
        {
            body.useGravity = false;
            RewindAbilityController ability = CreateAbility();
            ability.EnterSelecting();
            Assert.That(ability.TryStartRewind(), Is.True);
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;

            ability.ReturnToDefault();

            Assert.That(coordinator.IsRewindingAny, Is.False);
            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Default));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.useGravity, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(initialTimeScale));
        }

        /// <summary>
        /// <c>ConfirmInput_EntersRewinding</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(false, TestName = "ConfirmInput_KeyboardFEntersRewinding")]
        [TestCase(true, TestName = "ConfirmInput_GamepadEastEntersRewinding")]
        public void ConfirmInput_EntersRewinding(bool useGamepad)
        {
            RewindAbilityController ability = CreateAbility();
            ability.EnterSelecting();
            QueueConfirm(useGamepad);

            reader.Sample();
            ability.TickUpdate(reader);

            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Rewinding));
            Assert.That(coordinator.IsRewindingAny, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(initialTimeScale));
        }

        /// <summary>
        /// <c>CancelInput_EndsRewindAndRestoresBody</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(false, TestName = "CancelInput_KeyboardQEndsRewindAndRestoresBody")]
        [TestCase(true, TestName = "CancelInput_GamepadLeftShoulderEndsRewindAndRestoresBody")]
        public void CancelInput_EndsRewindAndRestoresBody(bool useGamepad)
        {
            RewindAbilityController ability = CreateAbility();
            ability.EnterSelecting();
            Assert.That(ability.TryStartRewind(), Is.True);
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;
            QueueCancel(useGamepad);

            reader.Sample();
            ability.TickUpdate(reader);

            Assert.That(coordinator.IsRewindingAny, Is.False);
            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Default));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.useGravity, Is.True);
        }

        /// <summary>
        /// <c>TickUpdate_AfterCoordinatorCompletes_ReturnsToDefault</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_AfterCoordinatorCompletes_ReturnsToDefault()
        {
            RewindAbilityController ability = CreateAbility();
            ability.EnterSelecting();
            Assert.That(ability.TryStartRewind(), Is.True);
            coordinator.EndRewind();

            ability.TickUpdate(reader);

            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Default));
            Assert.That(coordinator.IsRewindingAny, Is.False);
        }

        [TestCase(false, TestName =
            "CompletionFrameCancel_KeyboardQIsConsumedAndDoesNotReopenSelection")]
        [TestCase(true, TestName =
            "CompletionFrameCancel_GamepadLeftShoulderIsConsumedAndDoesNotReopenSelection")]
        /// <summary>
        /// <c>CompletionFrameCancel_IsConsumedAndDoesNotReopenSelection</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        public void CompletionFrameCancel_IsConsumedAndDoesNotReopenSelection(bool useGamepad)
        {
            RewindAbilityController ability = CreateAbility();
            PlayerAbilityController abilities =
                ability.gameObject.AddComponent<PlayerAbilityController>();
            abilities.Configure(null, null, ability);
            abilities.Select(PlayerAbilityController.AbilityKind.Rewind);
            ability.EnterSelecting();
            Assert.That(ability.TryStartRewind(), Is.True);
            QueueCancel(useGamepad);
            reader.Sample();
            coordinator.EndRewind();

            abilities.TickUpdate(reader);
            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Default));

            abilities.TickUpdate(reader);

            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Default));
            Assert.That(coordinator.IsRewindingAny, Is.False);
        }

        /// <summary>
        /// <c>TickUpdate_WhileRewinding_PreservesMovementJumpAndCameraInput</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickUpdate_WhileRewinding_PreservesMovementJumpAndCameraInput()
        {
            RewindAbilityController ability = CreateAbility();
            ability.EnterSelecting();
            Assert.That(ability.TryStartRewind(), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(900f, -450f));
            InputSystem.Update();

            reader.Sample();
            ability.TickUpdate(reader);

            Assert.That(ability.State,
                Is.EqualTo(RewindAbilityController.AbilityState.Rewinding));
            Assert.That(coordinator.IsRewindingAny, Is.True);
            Assert.That(reader.Move, Is.EqualTo(Vector2.up));
            Assert.That(reader.Look, Is.EqualTo(new Vector2(1f, -0.5f)));
            Assert.That(reader.LookIsPointerDelta, Is.True);
            Assert.That(reader.ConsumeJump(), Is.True);
            Assert.That(ability.AllowDefaultCameraInput, Is.True);
            Assert.That(ability.BlocksJump, Is.False);
        }

        /// <summary>
        /// <c>CreateAbility</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private RewindAbilityController CreateAbility(float cameraPositionLerpTime = 0.001f)
        {
            Capture(Vector3.zero, Quaternion.identity);
            Capture(Vector3.zero, Quaternion.identity);

            GameObject abilityObject = new("Rewind Ability");
            extraObjects.Add(abilityObject);
            GameObject cameraCoreObject = new("Camera Core");
            cameraCoreObject.transform.SetParent(abilityObject.transform, false);
            cameraCoreObject.transform.localPosition = new Vector3(0f, 0f, -4f);

            PlayerCameraSettings cameraSettings =
                ScriptableObject.CreateInstance<PlayerCameraSettings>();
            cameraSettings.collisionMask = 0;
            cameraSettings.positionLerpTime = cameraPositionLerpTime;
            extraAssets.Add(cameraSettings);
            PlayerCameraRig cameraRig = abilityObject.AddComponent<PlayerCameraRig>();
            cameraRig.Configure(
                abilityObject.transform,
                cameraCoreObject.transform,
                null,
                cameraSettings);
            RewindTargeting targeting = abilityObject.AddComponent<RewindTargeting>();
            targeting.Configure(
                abilityObject.transform,
                cameraCoreObject.transform,
                settings,
                coordinator);
            RewindAbilityController ability =
                abilityObject.AddComponent<RewindAbilityController>();
            ability.Configure(cameraRig, targeting, coordinator, settings);
            UnityEngine.Physics.SyncTransforms();
            return ability;
        }

        /// <summary>
        /// <c>QueueConfirm</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void QueueConfirm(bool useGamepad)
        {
            if (useGamepad)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState
                {
                    buttons = 1u << (int)GamepadButton.East
                });
            }
            else
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            }

            InputSystem.Update();
        }

        /// <summary>
        /// <c>QueueCancel</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void QueueCancel(bool useGamepad)
        {
            if (useGamepad)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState
                {
                    buttons = 1u << (int)GamepadButton.LeftShoulder
                });
            }
            else
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            }

            InputSystem.Update();
        }

        /// <summary>
        /// <c>Capture</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void Capture(Vector3 position, Quaternion rotation)
        {
            body.position = position;
            body.rotation = rotation;
            recorder.CaptureNow();
        }
    }

    /// <summary>
    /// 테스트에서 사용하는 <c>RewindCollisionProbe</c> 보조 타입이다.
    /// </summary>
    internal sealed class RewindCollisionProbe : MonoBehaviour
    {
        public int ContactCount { get; private set; }

        /// <summary>
        /// <c>OnCollisionEnter</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            ContactCount += collision.contactCount;
        }

        /// <summary>
        /// <c>OnCollisionStay</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void OnCollisionStay(Collision collision)
        {
            ContactCount += collision.contactCount;
        }
    }

    /// <summary>
    /// 테스트에서 사용하는 <c>ForwardGameplayProbe</c> 보조 타입이다.
    /// </summary>
    internal sealed class ForwardGameplayProbe : MonoBehaviour
    {
        public int FixedUpdateCount { get; private set; }

        /// <summary>
        /// <c>FixedUpdate</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private void FixedUpdate()
        {
            FixedUpdateCount++;
        }
    }
}
