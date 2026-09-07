using UnityEngine;

namespace Phyzzle.UI
{
    /// <summary>
    /// HUD RectTransform을 현재 디스플레이의 안전 영역 안에 맞춰 자동 조정한다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PlayerHudSafeArea : MonoBehaviour
    {
        private Rect lastSafeArea = new(-1f, -1f, -1f, -1f);
        private Vector2 lastScreenSize = new(-1f, -1f);

        /// <summary>
        /// 활성화될 때 현재 화면 안전 영역을 즉시 적용한다.
        /// </summary>
        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>
        /// 화면 크기나 안전 영역이 변경되었을 때만 HUD 앵커를 다시 적용한다.
        /// </summary>
        private void Update()
        {
            Rect safeArea = Screen.safeArea;
            Vector2 screenSize = new(Screen.width, Screen.height);
            if (safeArea != lastSafeArea || screenSize != lastScreenSize)
            {
                Apply(safeArea, screenSize);
            }
        }

        /// <summary>
        /// 현재 화면 크기와 안전 영역을 기준으로 HUD 배치를 강제로 갱신한다.
        /// </summary>
        public void Refresh()
        {
            Apply(Screen.safeArea, new Vector2(Screen.width, Screen.height));
        }

        /// <summary>
        /// 픽셀 단위 안전 영역을 0~1 범위의 RectTransform 앵커 값으로 변환한다.
        /// </summary>
        public static void CalculateAnchors(
            Rect safeArea,
            Vector2 screenSize,
            out Vector2 anchorMin,
            out Vector2 anchorMax)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            anchorMin = new Vector2(
                Mathf.Clamp01(safeArea.xMin / screenSize.x),
                Mathf.Clamp01(safeArea.yMin / screenSize.y));
            anchorMax = new Vector2(
                Mathf.Clamp01(safeArea.xMax / screenSize.x),
                Mathf.Clamp01(safeArea.yMax / screenSize.y));
            anchorMax = Vector2.Max(anchorMin, anchorMax);
        }

        /// <summary>
        /// 계산된 안전 영역 앵커를 RectTransform에 적용하고 마지막 상태를 캐시한다.
        /// </summary>
        private void Apply(Rect safeArea, Vector2 screenSize)
        {
            CalculateAnchors(safeArea, screenSize, out Vector2 min, out Vector2 max);
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            lastSafeArea = safeArea;
            lastScreenSize = screenSize;
        }
    }
}
