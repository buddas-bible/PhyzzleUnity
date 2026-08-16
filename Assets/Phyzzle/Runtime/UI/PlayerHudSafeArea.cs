using UnityEngine;

namespace Phyzzle.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class PlayerHudSafeArea : MonoBehaviour
    {
        private Rect lastSafeArea = new(-1f, -1f, -1f, -1f);
        private Vector2 lastScreenSize = new(-1f, -1f);

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Rect safeArea = Screen.safeArea;
            Vector2 screenSize = new(Screen.width, Screen.height);
            if (safeArea != lastSafeArea || screenSize != lastScreenSize)
            {
                Apply(safeArea, screenSize);
            }
        }

        public void Refresh()
        {
            Apply(Screen.safeArea, new Vector2(Screen.width, Screen.height));
        }

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
