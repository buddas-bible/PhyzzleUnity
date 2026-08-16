using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    public static class AttachmentRotationSnapper
    {
        public static Quaternion Snap(Quaternion localRotation)
        {
            return AttachmentRotationCatalog.FindNearest(localRotation).Rotation;
        }
    }
}
