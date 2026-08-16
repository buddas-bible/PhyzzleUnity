using NUnit.Framework;
using Phyzzle.Abilities.Attach;

namespace Phyzzle.Tests
{
    public sealed class AttachmentGraphTests
    {
        [Test]
        public void Connect_MergesComponents_AndDisconnectSplitsThem()
        {
            AttachmentGraph<string> graph = new();
            graph.Connect("A", "B");
            graph.Connect("C", "D");
            graph.Connect("B", "C");

            Assert.That(graph.GetComponent("A").Count, Is.EqualTo(4));
            Assert.That(graph.AreInSameComponent("A", "D"), Is.True);

            graph.Disconnect("B", "C");

            Assert.That(graph.GetComponent("A").Count, Is.EqualTo(2));
            Assert.That(graph.GetComponent("D").Count, Is.EqualTo(2));
            Assert.That(graph.AreInSameComponent("A", "D"), Is.False);
        }

        [Test]
        public void Remove_DisconnectsNodeFromEveryNeighbor()
        {
            AttachmentGraph<int> graph = new();
            graph.Connect(1, 2);
            graph.Connect(1, 3);

            Assert.That(graph.Remove(1), Is.True);
            Assert.That(graph.GetNeighbors(2), Is.Empty);
            Assert.That(graph.GetNeighbors(3), Is.Empty);
        }
    }
}
