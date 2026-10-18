using Diablerie.Engine.World;
using UnityEngine;
using UnityEngine.UI;

namespace Diablerie.Engine
{
    // Renders the world like the original game does: a fixed 800x600 pixel view. The view is scaled up to
    // fit the window and keeps its 4:3 shape, black bars fill the rest of the window.
    public class CameraController : MonoBehaviour
    {
        public const float VirtualWidth = 800;
        public const float VirtualHeight = 600;

        // How many screen pixels one pixel of the 800x600 view takes
        public static float UiScale { get; private set; } = 1;

        // Not used anymore, the view size is fixed. Kept so the serialized scene data stays valid.
        public float sizeChangeSpeed = 3f;
        public float horizontalShift = 0;

        new Camera camera;
        CanvasScaler canvasScaler;
        int screenWidth = -1;
        int screenHeight = -1;

        void Start()
        {
            camera = GetComponent<Camera>();
            CreateLetterboxCamera();
            UpdateViewport();
        }

        void LateUpdate()
        {
            if (Screen.width != screenWidth || Screen.height != screenHeight)
                UpdateViewport();
            else if (canvasScaler == null)
                ApplyUiScale();

            if (WorldState.instance == null || WorldState.instance.Player == null)
                return;

            transform.position = CalcTargetPos();
        }

        // A camera that only paints the window black, so the area around the 4:3 view is never left with garbage
        void CreateLetterboxCamera()
        {
            var letterboxObject = new GameObject("Letterbox Camera");
            letterboxObject.transform.SetParent(transform, false);
            var letterbox = letterboxObject.AddComponent<Camera>();
            letterbox.clearFlags = CameraClearFlags.SolidColor;
            letterbox.backgroundColor = Color.black;
            letterbox.cullingMask = 0;
            letterbox.depth = camera.depth - 1;
            letterbox.orthographic = true;
        }

        void UpdateViewport()
        {
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            if (screenWidth <= 0 || screenHeight <= 0)
                return;

            float targetAspect = VirtualWidth / VirtualHeight;
            float windowAspect = (float) screenWidth / screenHeight;
            Rect rect;
            if (windowAspect > targetAspect)
            {
                float width = targetAspect / windowAspect;
                rect = new Rect((1 - width) / 2, 0, width, 1);
            }
            else
            {
                float height = windowAspect / targetAspect;
                rect = new Rect(0, (1 - height) / 2, 1, height);
            }

            camera.rect = rect;
            camera.orthographicSize = VirtualHeight / Iso.pixelsPerUnit / 2;
            UiScale = screenHeight * rect.height / VirtualHeight;
            ApplyUiScale();
        }

        // The game canvas follows the camera viewport, so it has to be scaled the same way as the world
        void ApplyUiScale()
        {
            if (canvasScaler == null)
            {
                var canvasObject = GameObject.Find("Canvas");
                if (canvasObject != null)
                    canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            }

            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                canvasScaler.scaleFactor = UiScale;
            }
        }

        Vector3 CalcTargetPos()
        {
            Vector3 targetPos = WorldState.instance.Player.transform.position;
            targetPos.z = transform.position.z;

            targetPos.x += camera.orthographicSize * VirtualWidth / VirtualHeight * horizontalShift;

            return targetPos;
        }
    }
}