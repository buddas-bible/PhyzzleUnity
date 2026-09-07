using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 한 번에 하나의 되감기 대상을 제어하고 기록된 자세를 물리 속도로 재생한다.
    /// </summary>
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

        /// <summary>
        /// Unity 고정 프레임마다 현재 되감기 대상을 갱신한다.
        /// </summary>
        private void FixedUpdate()
        {
            TickFixed(Time.fixedDeltaTime);
        }

        /// <summary>
        /// 컴포넌트가 비활성화될 때 현재 되감기를 안전하게 종료한다.
        /// </summary>
        private void OnDisable()
        {
            EndRewind();
        }

        /// <summary>
        /// 되감기 재생에 사용할 설정을 구성한다.
        /// </summary>
        public void Configure(RewindSettings rewindSettings)
        {
            settings = rewindSettings;
        }

        /// <summary>
        /// 지정한 기록기가 현재 되감기 중인 대상인지 확인한다.
        /// </summary>
        public bool IsRewinding(RewindRecorder recorder)
        {
            return current == recorder;
        }

        /// <summary>
        /// 지정한 기록기가 현재 조건에서 되감기를 시작할 수 있는지 검사한다.
        /// </summary>
        public bool CanRewind(RewindRecorder recorder)
        {
            return recorder != null && recorder.isActiveAndEnabled && recorder.Body != null &&
                !recorder.Body.isKinematic && current == null && recorder.SnapshotCount > 1 &&
                settings != null;
        }

        /// <summary>
        /// 지정한 기록기를 현재 대상으로 설정하고 되감기 재생을 시작한다.
        /// </summary>
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

        /// <summary>
        /// 기록된 과거 자세를 샘플링해 현재 대상의 선형·각속도를 갱신한다.
        /// </summary>
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

        /// <summary>
        /// 현재 되감기를 종료하고 대상의 속도와 중력 상태를 복원한다.
        /// </summary>
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

        /// <summary>
        /// 대상이 사라진 경우 물리 접근 없이 내부 되감기 상태만 초기화한다.
        /// </summary>
        private void ClearMissingTarget()
        {
            current = null;
            rewindElapsed = 0f;
            finishAfterPhysicsStep = false;
        }
    }
}
