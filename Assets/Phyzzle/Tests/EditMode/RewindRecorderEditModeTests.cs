using System;
using System.Collections.Generic;
using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>RewindRecorderEditModeTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class RewindRecorderEditModeTests
    {
        private GameObject coordinatorObject;
        private GameObject recorderObject;
        private RewindSettings settings;
        private RewindCoordinator coordinator;
        private RewindRecorder recorder;
        private Rigidbody body;
        private float previousFixedDeltaTime;

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            previousFixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 0.02f;
            settings = ScriptableObject.CreateInstance<RewindSettings>();

            coordinatorObject = new GameObject("Rewind Coordinator");
            coordinator = coordinatorObject.AddComponent<RewindCoordinator>();
            coordinator.Configure(settings);

            recorderObject = new GameObject("Rewind Recorder");
            body = recorderObject.AddComponent<Rigidbody>();
            recorder = recorderObject.AddComponent<RewindRecorder>();
            recorder.Configure(settings, coordinator);
        }

        /// <summary>
        /// <c>TearDown</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(recorderObject);
            Object.DestroyImmediate(coordinatorObject);
            Object.DestroyImmediate(settings);
            Time.fixedDeltaTime = previousFixedDeltaTime;
        }

        /// <summary>
        /// <c>TickRecord_StationaryTicksPreserveLastMotionWindow</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickRecord_StationaryTicksPreserveLastMotionWindow()
        {
            Assert.That(recorder.SnapshotCount, Is.EqualTo(1));
            body.position = new Vector3(1f, 0f, 0f);
            recorder.TickRecord(0.02f);
            recorder.TickRecord(0.02f); // one final resting pose
            int stoppedCount = recorder.SnapshotCount;

            for (int i = 0; i < 500; i++) recorder.TickRecord(0.02f);

            Assert.That(stoppedCount, Is.EqualTo(3));
            Assert.That(recorder.SnapshotCount, Is.EqualTo(stoppedCount));
        }

        /// <summary>
        /// <c>TickRecord_PositionExactlyAtConfiguredThreshold_RecordsPose</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickRecord_PositionExactlyAtConfiguredThreshold_RecordsPose()
        {
            recorder.ClearHistory();
            body.position = Vector3.zero;
            recorder.CaptureNow();
            Vector3 boundaryPosition = Vector3.right * settings.recordPositionThreshold;
            settings.recordPositionThreshold = Vector3.Distance(body.position, boundaryPosition);

            body.position = boundaryPosition;
            recorder.TickRecord(0.02f);

            Assert.That(settings.recordPositionThreshold, Is.EqualTo(0.002f));
            Assert.That(recorder.SnapshotCount, Is.EqualTo(2));
        }

        /// <summary>
        /// <c>TickRecord_RotationExactlyAtConfiguredThreshold_RecordsPose</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TickRecord_RotationExactlyAtConfiguredThreshold_RecordsPose()
        {
            recorder.ClearHistory();
            body.rotation = Quaternion.identity;
            recorder.CaptureNow();
            Quaternion boundaryRotation =
                Quaternion.AngleAxis(settings.recordRotationThreshold, Vector3.up);

            body.rotation = boundaryRotation;
            recorder.TickRecord(0.02f);

            Assert.That(settings.recordRotationThreshold, Is.EqualTo(0.0573f));
            Assert.That(recorder.SnapshotCount, Is.EqualTo(2));
        }

        /// <summary>
        /// <c>TrySampleReverse_InterpolatesPositionAndRotation</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TrySampleReverse_InterpolatesPositionAndRotation()
        {
            recorder.CaptureNow();
            body.position = new Vector3(2f, 0f, 0f);
            body.rotation = Quaternion.Euler(0f, 90f, 0f);
            recorder.CaptureNow();

            Assert.That(recorder.TrySampleReverse(0.01f, out RewindPoseSample sample), Is.True);
            Assert.That(sample.Position.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Quaternion.Angle(sample.Rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.01f));
        }

        /// <summary>
        /// <c>TrySampleReverse_ExactAvailableHistoryDuration_ReturnsOldestPose</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void TrySampleReverse_ExactAvailableHistoryDuration_ReturnsOldestPose()
        {
            recorder.ClearHistory();
            for (int i = 0; i <= 10; i++)
            {
                body.position = Vector3.right * i;
                recorder.CaptureNow();
            }

            bool success = false;
            RewindPoseSample sample = default;
            Assert.DoesNotThrow(() =>
                success = recorder.TrySampleReverse(recorder.AvailableHistoryDuration, out sample));

            Assert.That(success, Is.True);
            Assert.That(sample.Position, Is.EqualTo(Vector3.zero));
            TestContext.WriteLine(
                $"ExactBoundary age={recorder.AvailableHistoryDuration}; " +
                $"oldestPosition={sample.Position}; motionTick={sample.MotionTick}");
        }

        /// <summary>
        /// <c>CopyHistoryNewestFirst_ReturnsEveryPoseWithoutChangingRecorderOrBody</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void CopyHistoryNewestFirst_ReturnsEveryPoseWithoutChangingRecorderOrBody()
        {
            recorder.ClearHistory();
            for (int i = 0; i < 4; i++)
            {
                body.position = Vector3.right * i;
                body.rotation = Quaternion.Euler(0f, i * 10f, 0f);
                recorder.CaptureNow();
            }

            body.useGravity = false;
            body.linearVelocity = new Vector3(1f, 2f, 3f);
            body.angularVelocity = new Vector3(4f, 5f, 6f);
            int snapshotCount = recorder.SnapshotCount;
            float historyDuration = recorder.AvailableHistoryDuration;
            Vector3 position = body.position;
            Quaternion rotation = body.rotation;
            Vector3 linearVelocity = body.linearVelocity;
            Vector3 angularVelocity = body.angularVelocity;
            List<RewindPoseSample> destination = new()
            {
                new RewindPoseSample(99, Vector3.one * 99f, Quaternion.identity)
            };

            recorder.CopyHistoryNewestFirst(destination);

            Assert.That(destination, Has.Count.EqualTo(4));
            Assert.That(destination[0].MotionTick, Is.EqualTo(3ul));
            Assert.That(destination[0].Position, Is.EqualTo(Vector3.right * 3f));
            Assert.That(Quaternion.Angle(destination[0].Rotation, Quaternion.Euler(0f, 30f, 0f)),
                Is.LessThan(0.001f));
            Assert.That(destination[3].MotionTick, Is.EqualTo(0ul));
            Assert.That(destination[3].Position, Is.EqualTo(Vector3.zero));
            Assert.That(recorder.SnapshotCount, Is.EqualTo(snapshotCount));
            Assert.That(recorder.AvailableHistoryDuration, Is.EqualTo(historyDuration));
            Assert.That(recorder.IsRewinding, Is.False);
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(body.rotation, Is.EqualTo(rotation));
            Assert.That(body.linearVelocity, Is.EqualTo(linearVelocity));
            Assert.That(body.angularVelocity, Is.EqualTo(angularVelocity));
            Assert.That(body.useGravity, Is.False);
            Assert.That(body.isKinematic, Is.False);
        }

        /// <summary>
        /// <c>CopyHistoryNewestFirst_EmptyHistoryClearsDestination</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void CopyHistoryNewestFirst_EmptyHistoryClearsDestination()
        {
            recorder.ClearHistory();
            List<RewindPoseSample> destination = new()
            {
                new RewindPoseSample(99, Vector3.one, Quaternion.identity)
            };

            recorder.CopyHistoryNewestFirst(destination);

            Assert.That(destination, Is.Empty);
            Assert.That(recorder.SnapshotCount, Is.Zero);
        }

        /// <summary>
        /// <c>CopyHistoryNewestFirst_NullDestinationThrows</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void CopyHistoryNewestFirst_NullDestinationThrows()
        {
            Assert.Throws<ArgumentNullException>(() => recorder.CopyHistoryNewestFirst(null));
        }
    }
}
