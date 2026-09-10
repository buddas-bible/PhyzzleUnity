using System;
using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 리지드바디의 이동 자세를 순환 버퍼에 기록하고 과거 자세 샘플을 제공한다.
    /// </summary>
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

        /// <summary>
        /// 인스펙터 초기화 시 동일 오브젝트의 Rigidbody 참조를 자동으로 연결한다.
        /// </summary>
        private void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 런타임 시작 시 필요한 참조와 기록 버퍼를 초기화한다.
        /// </summary>
        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
            coordinator ??= FindAnyObjectByType<RewindCoordinator>();
            EnsureHistory();
        }

        /// <summary>
        /// Unity 고정 프레임마다 현재 자세 기록 여부를 갱신한다.
        /// </summary>
        private void FixedUpdate()
        {
            TickRecord(Time.fixedDeltaTime);
        }

        /// <summary>
        /// 비활성화될 때 자신이 되감기 중이면 해당 재생을 종료한다.
        /// </summary>
        private void OnDisable()
        {
            if (coordinator != null && coordinator.IsRewinding(this))
            {
                coordinator.EndRewind();
            }
        }

        /// <summary>
        /// 기록 설정과 되감기 조율자를 구성하고 초기 자세를 기록한다.
        /// </summary>
        public void Configure(RewindSettings rewindSettings, RewindCoordinator rewindCoordinator)
        {
            settings = rewindSettings;
            coordinator = rewindCoordinator;
            EnsureHistory(true);
            CaptureNow();
        }

        /// <summary>
        /// 현재 리지드바디 자세를 즉시 기록 버퍼에 추가한다.
        /// </summary>
        public void CaptureNow()
        {
            // 되감기 중인 자세를 다시 기록하면 히스토리가 오염되므로 기록하지 않음
            if (body == null || settings == null || IsRewinding)
            {
                return;
            }

            EnsureHistory();
            recordedFixedDeltaTime = Time.fixedDeltaTime;
            RecordPose();
        }

        /// <summary>
        /// 이동 임계값을 기준으로 현재 자세를 기록할지 판단하고 히스토리를 갱신한다.
        /// </summary>
        public void TickRecord(float fixedDeltaTime)
        {
            // 물리 대상이나 설정이 없거나 현재 되감기 재생 중이면 새 스냅샷을 만들지 않음
            if (body == null || settings == null || IsRewinding)
            {
                return;
            }

            EnsureHistory();
            recordedFixedDeltaTime = Mathf.Max(0.000001f, fixedDeltaTime);
            // 첫 프레임은 비교 대상이 없으므로 현재 자세를 기준 샘플로 기록
            if (history.Count == 0)
            {
                RecordPose();
                return;
            }

            RewindPoseSample newest = history.GetFromOldest(history.Count - 1);
            // 위치 또는 회전 중 하나라도 설정 임계값 이상 변하면 새로운 모션 샘플로 판단
            bool moved = Vector3.Distance(body.position, newest.Position) >= settings.recordPositionThreshold ||
                RotationDeltaDegrees(body.rotation, newest.Rotation) >= settings.recordRotationThreshold;
            if (moved)
            {
                RecordPose();
                wasMoving = true;
            }
            // 움직이다 멈춘 순간의 자세도 한 번 기록해야 재생 끝에서 정지 위치가 유지됨
            else if (wasMoving)
            {
                RecordPose();
                wasMoving = false;
            }
        }

        /// <summary>
        /// 저장된 자세 히스토리와 기록 상태를 모두 초기화한다.
        /// </summary>
        public void ClearHistory()
        {
            history?.Clear();
            motionTick = 0;
            wasMoving = false;
        }

        /// <summary>
        /// 현재 시점에서 지정한 과거 시간만큼 되돌린 자세를 보간해 반환한다.
        /// </summary>
        internal bool TrySampleReverse(float historicalAge, out RewindPoseSample sample)
        {
            // 저장된 자세가 하나도 없으면 시간에 대응하는 샘플을 계산할 수 없음
            if (history == null || history.Count == 0)
            {
                sample = default;
                return false;
            }

            float age = Mathf.Clamp(historicalAge, 0f, AvailableHistoryDuration);
            // 최신 인덱스에서 age / dt 만큼 역방향으로 이동한 실수 인덱스를 계산
            // 예: Count=10, age=1.5dt라면 newest(9)에서 1.5칸 과거인 7.5를 샘플링
            float oldestBasedIndex = Mathf.Clamp(
                history.Count - 1 - age / recordedFixedDeltaTime,
                0f,
                history.Count - 1);
            int lowerIndex = Mathf.FloorToInt(oldestBasedIndex);
            int upperIndex = Mathf.Min(lowerIndex + 1, history.Count - 1);
            float interpolation = oldestBasedIndex - lowerIndex;
            RewindPoseSample lower = history.GetFromOldest(lowerIndex);
            RewindPoseSample upper = history.GetFromOldest(upperIndex);

            // 두 기록 사이의 소수 시간은 위치는 Lerp, 회전은 Slerp로 보간해 연속적인 궤적으로 복원
            sample = new RewindPoseSample(
                lower.MotionTick,
                Vector3.Lerp(lower.Position, upper.Position, interpolation),
                Quaternion.Slerp(lower.Rotation, upper.Rotation, interpolation));
            return true;
        }

        /// <summary>
        /// 저장된 자세를 최신 항목부터 순서대로 지정한 목록에 복사한다.
        /// </summary>
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

        /// <summary>
        /// 설정과 고정 프레임 간격에 맞는 용량의 기록 버퍼를 준비한다.
        /// </summary>
        private void EnsureHistory(bool forceRecreate = false)
        {
            if (settings == null)
            {
                return;
            }

            // 저장 시간 / FixedDeltaTime을 기준으로 필요한 스냅샷 수를 계산해 메모리를 고정 크기로 유지
            int capacity = settings.GetHistoryCapacity(Time.fixedDeltaTime);
            if (forceRecreate || history == null || history.Capacity != capacity)
            {
                history = new FixedRingBuffer<RewindPoseSample>(capacity);
                motionTick = 0;
                wasMoving = false;
            }
        }

        /// <summary>
        /// 현재 위치와 회전을 새로운 자세 샘플로 기록한다.
        /// </summary>
        private void RecordPose()
        {
            history.Add(new RewindPoseSample(motionTick++, body.position, body.rotation));
        }

        /// <summary>
        /// 두 쿼터니언 사이의 회전 차이를 각도 단위로 계산한다.
        /// </summary>
        private static float RotationDeltaDegrees(Quaternion first, Quaternion second)
        {
            // 상대 회전 q = second * inverse(first)을 구한 뒤 q = [axis*sin(θ/2), cos(θ/2)] 관계를 이용
            Quaternion delta = second * Quaternion.Inverse(first);
            double vectorMagnitude = System.Math.Sqrt(
                (double)delta.x * delta.x +
                (double)delta.y * delta.y +
                (double)delta.z * delta.z);
            // θ = 2 * atan2(|xyz|, |w|), |w|를 사용해 q/-q 중 짧은 회전 각도를 선택하고 degree로 변환
            return (float)(
                2d * System.Math.Atan2(vectorMagnitude, System.Math.Abs((double)delta.w)) *
                (180d / System.Math.PI));
        }
    }
}
