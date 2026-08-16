using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class AttachmentRotationSnapperTests
    {
        [Test]
        public void Snap_SelectsNearestLegacyCandidate()
        {
            Quaternion nearQuarterTurn = Quaternion.Euler(2f, 88f, -1f);
            Quaternion snapped = AttachmentRotationSnapper.Snap(nearQuarterTurn);

            Assert.That(Quaternion.Angle(snapped, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
        }
    }
}
