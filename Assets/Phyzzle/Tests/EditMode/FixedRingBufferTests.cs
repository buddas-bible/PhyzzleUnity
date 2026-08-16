using NUnit.Framework;
using Phyzzle.Abilities.Rewind;

namespace Phyzzle.Tests
{
    public sealed class FixedRingBufferTests
    {
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
