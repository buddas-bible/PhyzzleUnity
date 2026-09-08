using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>AttachmentRotationSnapperTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class AttachmentRotationSnapperTests
    {
        /// <summary>
        /// <c>Snap_SelectsNearestLegacyCandidate</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Snap_SelectsNearestLegacyCandidate()
        {
            Quaternion nearQuarterTurn = Quaternion.Euler(2f, 88f, -1f);
            Quaternion snapped = AttachmentRotationSnapper.Snap(nearQuarterTurn);

            Assert.That(Quaternion.Angle(snapped, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
        }
    }
}
