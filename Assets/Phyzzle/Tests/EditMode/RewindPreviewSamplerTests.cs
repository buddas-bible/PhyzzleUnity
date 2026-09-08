using System.Collections.Generic;
using NUnit.Framework;
using Phyzzle.Abilities.Rewind;
using UnityEngine;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>RewindPreviewSamplerTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class RewindPreviewSamplerTests
    {
        private readonly List<RewindPoseSample> path = new();
        private readonly List<RewindPoseSample> ghosts = new();

        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            path.Clear();
            ghosts.Clear();
        }

        /// <summary>
        /// <c>Build_FewerThanTwoSamplesClearsBothOutputs</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        public void Build_FewerThanTwoSamplesClearsBothOutputs(int sourceCount)
        {
            List<RewindPoseSample> source = SequentialSamples(sourceCount);
            path.Add(SampleAt(99f));
            ghosts.Add(SampleAt(99f));

            RewindPreviewSampler.Build(source, 256, 8, path, ghosts);

            Assert.That(path, Is.Empty);
            Assert.That(ghosts, Is.Empty);
        }

        /// <summary>
        /// <c>Build_PathCapPreservesNewestAndOldestSamples</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Build_PathCapPreservesNewestAndOldestSamples()
        {
            List<RewindPoseSample> source = SequentialSamples(1001);

            RewindPreviewSampler.Build(source, 256, 8, path, ghosts);

            Assert.That(path, Has.Count.EqualTo(256));
            Assert.That(path[0].Position, Is.EqualTo(Vector3.zero));
            Assert.That(path[255].Position, Is.EqualTo(Vector3.right * 1000f));
        }

        /// <summary>
        /// <c>Build_DefaultGhostCountIncludesBothEndpoints</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Build_DefaultGhostCountIncludesBothEndpoints()
        {
            List<RewindPoseSample> source = SequentialSamples(20);

            RewindPreviewSampler.Build(source, 256, 8, path, ghosts);

            Assert.That(ghosts, Has.Count.EqualTo(8));
            Assert.That(ghosts[0].Position, Is.EqualTo(Vector3.zero));
            Assert.That(ghosts[7].Position, Is.EqualTo(Vector3.right * 19f));
        }

        /// <summary>
        /// <c>Build_GhostCountClampsToTwoTwelveAndAvailableSamples</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [TestCase(20, 1, 2)]
        [TestCase(20, 99, 12)]
        [TestCase(5, 8, 5)]
        public void Build_GhostCountClampsToTwoTwelveAndAvailableSamples(
            int sourceCount,
            int desiredCount,
            int expectedCount)
        {
            RewindPreviewSampler.Build(
                SequentialSamples(sourceCount),
                256,
                desiredCount,
                path,
                ghosts);

            Assert.That(ghosts, Has.Count.EqualTo(expectedCount));
        }

        /// <summary>
        /// <c>Build_UsesRoundedUniformIndicesWithoutMutatingSource</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Build_UsesRoundedUniformIndicesWithoutMutatingSource()
        {
            List<RewindPoseSample> source = SequentialSamples(10);
            RewindPoseSample[] original = source.ToArray();

            RewindPreviewSampler.Build(source, 4, 4, path, ghosts);

            Assert.That(path, Has.Count.EqualTo(4));
            Assert.That(path[0].Position.x, Is.EqualTo(0f));
            Assert.That(path[1].Position.x, Is.EqualTo(3f));
            Assert.That(path[2].Position.x, Is.EqualTo(6f));
            Assert.That(path[3].Position.x, Is.EqualTo(9f));
            Assert.That(source, Is.EqualTo(original));
        }

        /// <summary>
        /// <c>Build_EqualTimeGhostsCreateWiderGapAcrossFastMotion</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Build_EqualTimeGhostsCreateWiderGapAcrossFastMotion()
        {
            List<RewindPoseSample> source = new()
            {
                SampleAt(0f),
                SampleAt(1f),
                SampleAt(2f),
                SampleAt(3f),
                SampleAt(13f),
                SampleAt(23f),
                SampleAt(33f),
                SampleAt(43f)
            };

            RewindPreviewSampler.Build(source, 256, 4, path, ghosts);

            Assert.That(ghosts, Has.Count.EqualTo(4));
            Assert.That(ghosts[0].Position.x, Is.EqualTo(0f));
            Assert.That(ghosts[1].Position.x, Is.EqualTo(2f));
            Assert.That(ghosts[2].Position.x, Is.EqualTo(23f));
            Assert.That(ghosts[3].Position.x, Is.EqualTo(43f));
            Assert.That(Vector3.Distance(ghosts[1].Position, ghosts[2].Position),
                Is.GreaterThan(Vector3.Distance(ghosts[0].Position, ghosts[1].Position)));
        }

        /// <summary>
        /// <c>Build_PathLimitBelowTwoStillKeepsBothEndpoints</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void Build_PathLimitBelowTwoStillKeepsBothEndpoints()
        {
            RewindPreviewSampler.Build(SequentialSamples(5), 1, 8, path, ghosts);

            Assert.That(path, Has.Count.EqualTo(2));
            Assert.That(path[0].Position, Is.EqualTo(Vector3.zero));
            Assert.That(path[1].Position, Is.EqualTo(Vector3.right * 4f));
        }

        /// <summary>
        /// <c>SequentialSamples</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static List<RewindPoseSample> SequentialSamples(int count)
        {
            List<RewindPoseSample> samples = new(count);
            for (int i = 0; i < count; i++)
            {
                samples.Add(SampleAt(i));
            }

            return samples;
        }

        /// <summary>
        /// <c>SampleAt</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        private static RewindPoseSample SampleAt(float x) =>
            new((ulong)Mathf.Max(0, Mathf.RoundToInt(x)), Vector3.right * x, Quaternion.Euler(0f, x, 0f));
    }
}
