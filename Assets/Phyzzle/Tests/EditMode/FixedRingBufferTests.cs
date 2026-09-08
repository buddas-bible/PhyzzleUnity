using NUnit.Framework;
using Phyzzle.Abilities.Rewind;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>FixedRingBufferTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class FixedRingBufferTests
    {
        /// <summary>
        /// <c>Add_WhenFull_DiscardsOldestValue</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Add_WhenFull_DiscardsOldestValue()
        {
            FixedRingBuffer<int> buffer = new(3);

            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);
            buffer.Add(4);

            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer.GetFromOldest(0), Is.EqualTo(2));
            Assert.That(buffer.GetFromOldest(1), Is.EqualTo(3));
            Assert.That(buffer.GetFromOldest(2), Is.EqualTo(4));
        }

        /// <summary>
        /// <c>Clear_ResetsCountAndWritePosition</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Clear_ResetsCountAndWritePosition()
        {
            FixedRingBuffer<int> buffer = new(2);
            buffer.Add(1);
            buffer.Add(2);

            buffer.Clear();
            buffer.Add(9);

            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.GetFromOldest(0), Is.EqualTo(9));
        }
    }
}
