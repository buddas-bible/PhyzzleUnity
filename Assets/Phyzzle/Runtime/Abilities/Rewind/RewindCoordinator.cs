using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    [DisallowMultipleComponent]
    public sealed class RewindCoordinator : MonoBehaviour
    {
        [SerializeField] private RewindSettings settings;

        private RewindRecorder current;
        private float rewindElapsed;
        private bool finishAfterPhysicsStep;
        private bool previousUseGravity;

        public bool IsRewindingAny => current != null;
        public RewindRecorder Current => current;

        private void FixedUpdate()
        {
            TickFixed(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            EndRewind();
        }

        public void Configure(RewindSettings rewindSettings)
        {
            settings = rewindSettings;
        }

        public bool IsRewinding(RewindRecorder recorder)
        {
            return current == recorder;
        }

        public bool CanRewind(RewindRecorder recorder)
        {
            return recorder != null && recorder.isActiveAndEnabled && recorder.Body != null &&
                !recorder.Body.isKinematic && current == null && recorder.SnapshotCount > 1 &&
                settings != null;
        }

        public bool StartRewind(RewindRecorder recorder)
        {
            if (recorder == null || !recorder.isActiveAndEnabled || !CanRewind(recorder))
            {
                return false;
            }

            current = recorder;
            rewindElapsed = 0f;
            finishAfterPhysicsStep = false;
            previousUseGravity = recorder.Body.useGravity;
            recorder.Body.useGravity = false;
            recorder.Body.linearVelocity = Vector3.zero;
            recorder.Body.angularVelocity = Vector3.zero;
            return true;
        }

        public void TickFixed(float fixedDeltaTime)
        {
            if (finishAfterPhysicsStep)
            {
                EndRewind();
                return;
            }

            if (current == null)
            {
                ClearMissingTarget();
                return;
            }

            if (current.Body == null)
            {
                ClearMissingTarget();
                return;
            }

            if (settings == null || !current.isActiveAndEnabled)
            {
                EndRewind();
                return;
            }

            rewindElapsed += fixedDeltaTime * settings.playbackRate;
            float targetAge = Mathf.Min(rewindElapsed, current.AvailableHistoryDuration);
            if (!current.TrySampleReverse(targetAge, out RewindPoseSample target))
            {
                EndRewind();
                return;
            }

            RewindDrive drive = RewindVelocityServo.Calculate(
                current.Body.position,
                current.Body.rotation,
                target,
                fixedDeltaTime,
                settings.maxLinearSpeed,
                settings.maxAngularSpeed);

            current.Body.linearVelocity = drive.LinearVelocity;
            current.Body.angularVelocity = drive.AngularVelocity;
            finishAfterPhysicsStep = rewindElapsed >= current.AvailableHistoryDuration;
        }

        public void EndRewind()
        {
            Rigidbody body = current != null ? current.Body : null;
            current = null;
            rewindElapsed = 0f;
            finishAfterPhysicsStep = false;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = previousUseGravity;
            }
        }

        private void ClearMissingTarget()
        {
            current = null;
            rewindElapsed = 0f;
            finishAfterPhysicsStep = false;
        }
    }
}
