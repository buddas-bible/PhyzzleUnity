using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Rewind
{
    /// <summary>
    /// 되감기 기록에서 경로와 고스트 미리보기에 사용할 샘플을 균등 간격으로 추출한다.
    /// </summary>
    internal static class RewindPreviewSampler
    {
        /// <summary>
        /// 최신순 히스토리에서 경로용 샘플과 고스트용 샘플 목록을 생성한다.
        /// </summary>
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

        /// <summary>
        /// 원본 목록의 처음과 끝을 포함하도록 지정 개수의 샘플을 균등 간격으로 복사한다.
        /// </summary>
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
