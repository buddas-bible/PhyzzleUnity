using System;
using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RewindRecorder : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private RewindSettings settings;
        [SerializeField] private RewindCoordinator coordinator;

        private FixedRingBuffer<RewindPoseSample> history;
        private ulong motionTick;
        private float recordedFixedDeltaTime;
        private bool wasMoving;

        public Rigidbody Body => body;
        public int SnapshotCount => history?.Count ?? 0;
        public float AvailableHistoryDuration => Mathf.Max(0f, (SnapshotCount - 1) * recordedFixedDeltaTime);
        public bool IsRewinding => coordinator?.IsRewinding(this) == true;

        private void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
            coordinator ??= FindAnyObjectByType<RewindCoordinator>();
            EnsureHistory();
        }

        private void FixedUpdate()
        {
            TickRecord(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            if (coordinator != null && coordinator.IsRewinding(this))
            {
                coordinator.EndRewind();
            }
        }

        public void Configure(RewindSettings rewindSettings, RewindCoordinator rewindCoordinator)
        {
            settings = rewindSettings;
            coordinator = rewindCoordinator;
            EnsureHistory(true);
            CaptureNow();
        }

        public void CaptureNow()
        {
            if (body == null || settings == null || IsRewinding)
            {
                return;
            }

            EnsureHistory();
            recordedFixedDeltaTime = Time.fixedDeltaTime;
            RecordPose();
        }

        public void TickRecord(float fixedDeltaTime)
        {
            if (body == null || settings == null || IsRewinding)
            {
                return;
            }

            EnsureHistory();
            recordedFixedDeltaTime = Mathf.Max(0.000001f, fixedDeltaTime);
            if (history.Count == 0)
            {
                RecordPose();
                return;
            }

            RewindPoseSample newest = history.GetFromOldest(history.Count - 1);
            bool moved = Vector3.Distance(body.position, newest.Position) >= settings.recordPositionThreshold ||
                RotationDeltaDegrees(body.rotation, newest.Rotation) >= settings.recordRotationThreshold;
            if (moved)
            {
                RecordPose();
                wasMoving = true;
            }
            else if (wasMoving)
            {
                RecordPose();
                wasMoving = false;
            }
        }

        public void ClearHistory()
        {
            history?.Clear();
            motionTick = 0;
            wasMoving = false;
        }

        internal bool TrySampleReverse(float historicalAge, out RewindPoseSample sample)
        {
            if (history == null || history.Count == 0)
            {
                sample = default;
                return false;
            }

            float age = Mathf.Clamp(historicalAge, 0f, AvailableHistoryDuration);
            float oldestBasedIndex = Mathf.Clamp(
                history.Count - 1 - age / recordedFixedDeltaTime,
                0f,
                history.Count - 1);
            int lowerIndex = Mathf.FloorToInt(oldestBasedIndex);
            int upperIndex = Mathf.Min(lowerIndex + 1, history.Count - 1);
            float interpolation = oldestBasedIndex - lowerIndex;
            RewindPoseSample lower = history.GetFromOldest(lowerIndex);
            RewindPoseSample upper = history.GetFromOldest(upperIndex);
            sample = new RewindPoseSample(
                lower.MotionTick,
                Vector3.Lerp(lower.Position, upper.Position, interpolation),
                Quaternion.Slerp(lower.Rotation, upper.Rotation, interpolation));
            return true;
        }

        internal void CopyHistoryNewestFirst(List<RewindPoseSample> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            if (history == null)
            {
                return;
            }

            for (int i = history.Count - 1; i >= 0; i--)
            {
                destination.Add(history.GetFromOldest(i));
            }
        }

        private void EnsureHistory(bool forceRecreate = false)
        {
            if (settings == null)
            {
                return;
            }

            int capacity = settings.GetHistoryCapacity(Time.fixedDeltaTime);
            if (forceRecreate || history == null || history.Capacity != capacity)
            {
                history = new FixedRingBuffer<RewindPoseSample>(capacity);
                motionTick = 0;
                wasMoving = false;
            }
        }

        private void RecordPose()
        {
            history.Add(new RewindPoseSample(motionTick++, body.position, body.rotation));
        }

        private static float RotationDeltaDegrees(Quaternion first, Quaternion second)
        {
            Quaternion delta = second * Quaternion.Inverse(first);
            double vectorMagnitude = System.Math.Sqrt(
                (double)delta.x * delta.x +
                (double)delta.y * delta.y +
                (double)delta.z * delta.z);
            return (float)(
                2d * System.Math.Atan2(vectorMagnitude, System.Math.Abs((double)delta.w)) *
                (180d / System.Math.PI));
        }
    }
}
