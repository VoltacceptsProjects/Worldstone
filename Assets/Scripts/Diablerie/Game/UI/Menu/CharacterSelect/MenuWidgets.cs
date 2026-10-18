using System;
using UnityEngine;
using UnityEngine.UI;

namespace Diablerie.Game.UI.Menu.CharacterSelect
{
    // Tiny helpers for building menu UI from code (the menu scenes are not edited by hand).
    // Everything is positioned relative to the screen center with absolute pixel sizes.
    public static class MenuWidgets
    {
        // The bitmap fonts draw their glyphs about this many pixels higher than Unity's
        // "middle" alignment puts them. Tune this if text looks off-center with other fonts.
        public const float TextNudge = 10;

        public static readonly Color Gold = new Color(0.78f, 0.65f, 0.35f);
        public static readonly Color Red = new Color(0.85f, 0.25f, 0.2f);
        public static readonly Color Grey = new Color(0.55f, 0.55f, 0.55f);

        public static GameObject CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            go.AddComponent<GraphicRaycaster>();
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Center(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Position a label by its center, compensating for the font's vertical offset
        public static void PlaceText(Text text, Vector2 position, Vector2 size)
        {
            Center(text.rectTransform, new Vector2(position.x, position.y - TextNudge), size);
        }

        // Fill the parent, compensating for the font's vertical offset
        public static void StretchText(Text text, float horizontalPadding = 0)
        {
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(horizontalPadding, -TextNudge);
            text.rectTransform.offsetMax = new Vector2(-horizontalPadding, -TextNudge);
        }

        public static Image Box(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, Font font, Color color, TextAnchor anchor)
        {
            var rt = NewRect(name, parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button TextButton(Transform parent, string label, Vector2 position, Vector2 size, Action onClick)
        {
            var image = Box(label, parent, Color.white);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(0.14f, 0.14f, 0.14f, 0.95f);
            colors.highlightedColor = new Color(0.26f, 0.22f, 0.12f, 0.95f);
            colors.pressedColor = new Color(0.4f, 0.32f, 0.15f, 0.95f);
            colors.disabledColor = new Color(0.07f, 0.07f, 0.07f, 0.8f);
            colors.colorMultiplier = 1;
            button.colors = colors;

            var text = Label("Text", image.transform, label, Fonts.GetFont24(), Color.white, TextAnchor.MiddleCenter);
            StretchText(text);

            // A width of zero or less means "as wide as the label needs"
            float width = size.x > 0 ? size.x : Mathf.Ceil(text.preferredWidth) + 48;
            Center(image.rectTransform, position, new Vector2(width, size.y));

            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }

        // Centers buttons in a horizontal row at height y, keeping their own widths
        public static void LayoutRow(float y, float gap, params Button[] buttons)
        {
            float total = gap * (buttons.Length - 1);
            foreach (var button in buttons)
                total += ((RectTransform)button.transform).sizeDelta.x;

            float x = -total / 2;
            foreach (var button in buttons)
            {
                var rt = (RectTransform)button.transform;
                rt.anchoredPosition = new Vector2(x + rt.sizeDelta.x / 2, y);
                x += rt.sizeDelta.x + gap;
            }
        }

        public static void SetButtonEnabled(Button button, bool enabled)
        {
            button.interactable = enabled;
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
                text.color = enabled ? Color.white : Grey;
        }

        public static void PlayClick()
        {
            if (Diablerie.Engine.AudioManager.instance != null)
                Diablerie.Engine.AudioManager.instance.Play(Diablerie.Engine.Datasheets.SoundInfo.cursorButtonClick);
        }
    }
}
