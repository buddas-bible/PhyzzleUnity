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
            // 비활성 대상이나 기록이 부족한 대상은 되감기 상태로 진입시키지 않음
            if (recorder == null || !recorder.isActiveAndEnabled || !CanRewind(recorder))
            {
                return false;
            }

            current = recorder;
            rewindElapsed = 0f;
            finishAfterPhysicsStep = false;
            // 되감기 중에는 기록된 궤적만 따라가야 하므로 중력을 잠시 끄고 기존 상태는 종료 시 복원
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
            // 마지막 속도 명령이 실제 물리 스텝에 한 번 적용된 뒤 다음 프레임에서 종료
            if (finishAfterPhysicsStep)
            {
                EndRewind();
                return;
            }

            // 대상이 Destroy되어 참조 자체가 사라진 경우 Rigidbody를 만지지 않고 내부 상태만 정리
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

            // 설정이 사라지거나 대상이 비활성화되면 정상 종료 경로를 사용해 중력과 속도를 복원
            if (settings == null || !current.isActiveAndEnabled)
            {
                EndRewind();
                return;
            }

            // playbackRate를 곱한 누적 시간을 '현재에서 몇 초 과거인가'로 사용
            rewindElapsed += fixedDeltaTime * settings.playbackRate;
            float targetAge = Mathf.Min(rewindElapsed, current.AvailableHistoryDuration);
            if (!current.TrySampleReverse(targetAge, out RewindPoseSample target))
            {
                EndRewind();
                return;
            }

            // 직접 위치를 순간이동시키지 않고 목표 샘플까지 필요한 속도를 계산해 PhysX가 한 스텝 동안 이동하도록 함
            RewindDrive drive = RewindVelocityServo.Calculate(
                current.Body.position,
                current.Body.rotation,
                target,
                fixedDeltaTime,
                settings.maxLinearSpeed,
                settings.maxAngularSpeed);

            current.Body.linearVelocity = drive.LinearVelocity;
            current.Body.angularVelocity = drive.AngularVelocity;
            // 히스토리 끝에 도달한 프레임의 속도가 실제로 적용될 시간을 보장하기 위해 종료를 한 스텝 미룸
            finishAfterPhysicsStep = rewindElapsed >= current.AvailableHistoryDuration;
        }

        /// <summary>
        /// 현재 되감기를 종료하고 대상의 속도와 중력 상태를 복원한다.
        /// </summary>
        public void EndRewind()
        {
            // current를 먼저 비워도 마지막 Rigidbody는 정리할 수 있도록 로컬 변수에 보관
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
