using UnityEngine;
using UnityEngine.EventSystems;

namespace Diablerie.Engine.UI
{
    public class Ui : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static Ui instance;

        private RectTransform rectTransform;
        private ScreenLabel screenLabel;
        private SoftwareCursor _softwareCursor;
        private GameObject currentHover;

        void Awake()
        {
            instance = this;
            rectTransform = transform as RectTransform;
            screenLabel = new ScreenLabel(rectTransform);
            screenLabel.Hide();
            _softwareCursor = new SoftwareCursor(rectTransform);
        }
        
        public void OnPointerEnter(PointerEventData eventData) {
            if (eventData.pointerCurrentRaycast.gameObject != null) {
                currentHover = eventData.pointerCurrentRaycast.gameObject;
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            currentHover = null;
        }
        
        public static bool Hover => instance.currentHover != null;

        public RectTransform RectTransform => rectTransform;

        // Converts a position in screen pixels (like the mouse position) to the canvas coordinates of the UI,
        // measured from the bottom left corner. The canvas is scaled to the game view, so they are not the same.
        public static Vector2 ScreenToCanvasPoint(Vector2 screenPoint)
        {
            var rect = instance.rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, Camera.main, out var local);
            return local - rect.rect.min;
        }

        // Position is in screen pixels
        public static void ShowScreenLabel(Vector2 position, string text)
        {
            instance.screenLabel.Show(ScreenToCanvasPoint(position), text);
        }

        public static void HideScreenLabel()
        {
            instance.screenLabel.Hide();
        }
    }
}
