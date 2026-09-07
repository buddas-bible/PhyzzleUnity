using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 하나의 기록 시점에 해당하는 이동 틱, 위치와 회전 정보를 보관한다.
    /// </summary>
    internal readonly struct RewindPoseSample
    {
        /// <summary>
        /// 기록 틱과 월드 위치·회전으로 되감기 자세 샘플을 생성한다.
        /// </summary>
        public RewindPoseSample(ulong motionTick, Vector3 position, Quaternion rotation)
        {
            MotionTick = motionTick;
            Position = position;
            Rotation = rotation;
        }

        public ulong MotionTick { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }
}
