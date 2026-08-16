using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    internal readonly struct RewindPoseSample
    {
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
