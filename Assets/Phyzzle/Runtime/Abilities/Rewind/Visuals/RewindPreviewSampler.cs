using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    internal static class RewindPreviewSampler
    {
        internal static void Build(
            IReadOnlyList<RewindPoseSample> newestFirst,
            int maxPathPoints,
            int desiredGhostCount,
            List<RewindPoseSample> path,
            List<RewindPoseSample> ghosts)
        {
            path.Clear();
            ghosts.Clear();
            int sourceCount = newestFirst.Count;
            if (sourceCount < 2)
            {
                return;
            }

            CopyUniform(
                newestFirst,
                Mathf.Min(sourceCount, Mathf.Max(2, maxPathPoints)),
                path);
            CopyUniform(
                newestFirst,
                Mathf.Min(sourceCount, Mathf.Clamp(desiredGhostCount, 2, 12)),
                ghosts);
        }

        private static void CopyUniform(
            IReadOnlyList<RewindPoseSample> source,
            int count,
            List<RewindPoseSample> destination)
        {
            for (int i = 0; i < count; i++)
            {
                int sourceIndex = Mathf.RoundToInt(i * (source.Count - 1f) / (count - 1f));
                destination.Add(source[sourceIndex]);
            }
        }
    }
}
