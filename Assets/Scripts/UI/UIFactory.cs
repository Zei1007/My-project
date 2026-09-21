using UnityEngine;
using UnityEngine.UI;

namespace ZombieShooter
{
    /// <summary>
    /// Builds the runtime UI in code. The HUD is prototype furniture - keeping it programmatic
    /// means no prefab wiring to maintain while the systems underneath are still moving. Swap this
    /// for authored prefabs once the layout settles.
    /// </summary>
    public static class UIFactory
    {
        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            return label;
        }

        /// <summary>
        /// A background bar with a left-anchored fill. Returns the fill RectTransform - drive it
        /// with SetFill. The fill is done by stretching the rect, not Image.fillAmount, because
        /// fillAmount is ignored on an Image with no sprite assigned.
        /// </summary>
        public static RectTransform Bar(Transform parent, string name, Color background, Color fill,
                                        Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var back = Panel(parent, name, background);
            back.anchorMin = anchor;
            back.anchorMax = anchor;
            back.pivot = anchor;
            back.anchoredPosition = anchoredPosition;
            back.sizeDelta = size;

            var fillRect = Panel(back, name + "_Fill", fill);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            return fillRect;
        }

        /// <summary>Sets a bar fill to a 0-1 fraction of its track.</summary>
        public static void SetFill(RectTransform fillRect, float normalized)
        {
            if (fillRect == null) return;

            normalized = Mathf.Clamp01(normalized);
            var max = fillRect.anchorMax;
            max.x = normalized;
            fillRect.anchorMax = max;

            // Re-assert the inset: changing anchors leaves offsets pointing at the old track.
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(normalized <= 0f ? 0f : -2f, -2f);
        }

        public static Button CardButton(Transform parent, Color background)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = background;
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            button.colors = colors;

            return button;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
