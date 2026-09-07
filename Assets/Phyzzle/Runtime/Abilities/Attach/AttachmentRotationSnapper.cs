using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 임의의 로컬 회전을 가장 가까운 부착 기준 회전으로 스냅한다.
    /// </summary>
    public static class AttachmentRotationSnapper
    {
        /// <summary>
        /// 입력 회전과 가장 가까운 기준 자세의 회전을 반환한다.
        /// </summary>
        public static Quaternion Snap(Quaternion localRotation)
        {
            return AttachmentRotationCatalog.FindNearest(localRotation).Rotation;
        }
    }
}
