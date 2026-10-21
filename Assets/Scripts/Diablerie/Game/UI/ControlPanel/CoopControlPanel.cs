using System.Collections.Generic;
using Diablerie.Engine.World;
using Diablerie.Game.Coop;
using UnityEngine;
using UnityEngine.UI;
using SkillBar = Diablerie.Game.UI.SkillPanel.SkillPanel;

namespace Diablerie.Game.UI.ControlPanel
{
    // Playing alone there is one control panel at the bottom of the screen. In co-op there are two, standing
    // on the sides of the view:
    //   player 1: the panel turned 90 degrees clockwise, on the left side
    //   player 2: a clone of the panel turned 90 degrees counterclockwise, on the right side
    // The skill bar (the six hot skills) belongs to the panel: player 1's bar is moved into his panel and turned
    // with it, player 2 gets a copy that shows the four skills of his gamepad buttons.
    // When player 2 leaves, the original panel and skill bar are put back at the bottom. Added to the game scene
    // by CoopManager.
    public class CoopControlPanel : MonoBehaviour
    {
        private class Saved
        {
            public RectTransform rect;
            public Vector2 anchorMin, anchorMax, pivot, position, size;
            public Quaternion rotation;
            public Vector3 scale;
        }

        private RectTransform _panel;
        private GameObject _guestPanel;
        private Saved _original;
        private class SavedText
        {
            public RectTransform rect;
            public Quaternion rotation;
            public Vector3 scale;
        }

        private readonly List<SavedText> _texts = new List<SavedText>();
        private class SavedFill
        {
            public Image image;
            public Image.FillMethod method;
            public int origin;
        }

        private SkillBar _skillBar;
        private Transform _skillBarParent;
        private int _skillBarSibling;
        private readonly List<SavedFill> _fills = new List<SavedFill>();
        private bool _coop;

        private void Update()
        {
            var world = WorldState.instance;
            bool coop = world != null && world.Player != null && world.Player2 != null;
            if (coop == _coop)
                return;

            if (coop)
                _coop = Enter();
            else
            {
                Leave();
                _coop = false;
            }
        }

        private RectTransform FindPanel()
        {
            var bulb = FindObjectOfType<LifeBulb>();
            if (bulb == null)
                return null;
            for (var t = bulb.transform; t != null; t = t.parent)
            {
                if (t.name == "ControlPanel")
                    return t as RectTransform;
            }
            return null;
        }

        private bool Enter()
        {
            if (_panel == null)
                _panel = FindPanel();
            if (_panel == null)
                return false;   // not there yet, try again next frame

            var world = WorldState.instance;
            var guestInput = world.Player2 != null ? world.Player2.gameObject.GetComponent<CoopPlayerInput>() : null;
            if (guestInput == null)
                return false;   // player 2 is not set up yet, try again next frame

            // The copy for player 2 is made from the untouched panel
            var copy = Instantiate(_panel.gameObject, _panel.parent, false);
            copy.name = "ControlPanel (Player 2)";
            var guest = (RectTransform) copy.transform;
            guest.SetSiblingIndex(_panel.GetSiblingIndex() + 1);

            foreach (var bulb in copy.GetComponentsInChildren<LifeBulb>(true))
                bulb.playerIndex = 1;
            foreach (var bulb in copy.GetComponentsInChildren<ManaBulb>(true))
                bulb.playerIndex = 1;
            foreach (var bar in copy.GetComponentsInChildren<ExperienceBar>(true))
                bar.playerIndex = 1;

            // Player 2 plays with a gamepad: the menu buttons and skill slots of his panel are not used
            foreach (var button in copy.GetComponentsInChildren<Button>(true))
                button.interactable = false;
            var minipanel = guest.Find("ImageMinipanel");
            if (minipanel != null)
                minipanel.gameObject.SetActive(false);

            _original = Save(_panel);
            _guestPanel = copy;

            // The skill bar: a copy for player 2, the original moves into the panel of player 1. Both go into
            // the panels before they are turned, so they are turned with them.
            _skillBar = SkillBar.instance;
            if (_skillBar != null)
            {
                var barCopy = Instantiate(_skillBar.gameObject, guest, false);
                barCopy.name = "SkillPanel (Player 2)";
                SkillBar.instance = _skillBar;   // the Awake of the copy has taken the instance over, give it back
                FillGuestSkillBar(barCopy, guestInput);

                _skillBarParent = _skillBar.transform.parent;
                _skillBarSibling = _skillBar.transform.GetSiblingIndex();
                _skillBar.transform.SetParent(_panel, false);   // anchors stay: the panel is as big as the canvas
            }

            Turn(_panel, -90f, false, true, true);   // clockwise, left side, flipped over the X axis
            Turn(guest, 90f, true, false, false);    // counterclockwise, right side
            return true;
        }

        // Player 2 has no mouse: the copy only shows his four skills with the buttons that cast them
        private static void FillGuestSkillBar(GameObject barCopy, CoopPlayerInput input)
        {
            string[] buttons = { "A", "X", "B", "Y", "RB", "RT" };
            var bar = barCopy.GetComponent<SkillBar>();
            for (int i = 0; i < bar.hotSkills.Length; ++i)
            {
                bar.SetHotSkill(i, input.GuestSkill(i));
                if (i < buttons.Length)
                    bar.SetHotKey(i, buttons[i]);
            }
            foreach (var button in barCopy.GetComponentsInChildren<Button>(true))
                button.interactable = false;
        }

        private void Leave()
        {
            if (_skillBar != null && _skillBarParent != null)
            {
                _skillBar.transform.SetParent(_skillBarParent, false);
                _skillBar.transform.SetSiblingIndex(_skillBarSibling);
            }
            _skillBar = null;
            _skillBarParent = null;

            if (_guestPanel != null)
                Destroy(_guestPanel);
            _guestPanel = null;

            if (_original != null && _original.rect != null)
            {
                var r = _original.rect;
                r.anchorMin = _original.anchorMin;
                r.anchorMax = _original.anchorMax;
                r.pivot = _original.pivot;
                r.sizeDelta = _original.size;
                r.anchoredPosition = _original.position;
                r.localRotation = _original.rotation;
                r.localScale = _original.scale;
            }
            _original = null;

            for (int i = 0; i < _texts.Count; ++i)
            {
                if (_texts[i].rect == null)
                    continue;
                _texts[i].rect.localRotation = _texts[i].rotation;
                _texts[i].rect.localScale = _texts[i].scale;
            }
            _texts.Clear();

            for (int i = 0; i < _fills.Count; ++i)
            {
                var f = _fills[i];
                if (f.image == null)
                    continue;
                f.image.fillMethod = f.method;
                f.image.fillOrigin = f.origin;
            }
            _fills.Clear();
        }

        private static Saved Save(RectTransform rect)
        {
            return new Saved
            {
                rect = rect,
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                position = rect.anchoredPosition,
                size = rect.sizeDelta,
                rotation = rect.localRotation,
                scale = rect.localScale
            };
        }

        // Stands the panel on one side of the view. The panel is as wide as the view and its parts sit on the
        // bottom edge. Turned by 90 degrees its bottom edge becomes the left (clockwise) or the right
        // (counterclockwise) edge of the view. The width of the view becomes the height, so the panel is also
        // scaled down by height / width to fit between the top and the bottom of the screen.
        private void Turn(RectTransform root, float angle, bool right, bool flip, bool remember)
        {
            Vector2 view = ((RectTransform) root.parent).rect.size;
            float scale = view.y / view.x;

            root.anchorMin = new Vector2(right ? 1f : 0f, 0.5f);
            root.anchorMax = root.anchorMin;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = view;
            // The turned panel is view.y * scale pixels wide, its middle is half of that from the edge
            root.anchoredPosition = new Vector2((right ? -1f : 1f) * view.y * scale / 2f, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, angle);
            // Flipped over the X axis of the screen (top and bottom swap, the panel stays on its side). The panel is
            // turned by 90 degrees, so the X axis of the screen is the local X axis of the panel.
            root.localScale = new Vector3(flip ? -scale : scale, scale, 1f);

            // Flipped, the local X axis of the panel points up (as on the right panel), so both fill from the left
            OrientBulbs(root, right || flip, remember);

            // Texts turn (and flip) with everything else, undo it so they can be read. Undoing a turn is turning
            // back; undoing a flip and a turn together is flipping the text again and turning it the other way.
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                var t = text.rectTransform;
                if (remember)
                    _texts.Add(new SavedText { rect = t, rotation = t.localRotation, scale = t.localScale });
                if (flip)
                {
                    t.localRotation = Quaternion.Euler(0f, 0f, angle) * t.localRotation;
                    var ts = t.localScale;
                    t.localScale = new Vector3(-ts.x, ts.y, ts.z);
                }
                else
                {
                    t.localRotation = t.localRotation * Quaternion.Euler(0f, 0f, -angle);
                }
            }
        }

        // The bulbs fill along their own bottom to top direction, which turns with the panel. Both bulbs of both
        // players should still fill from the bottom of the screen to the top, so the fill runs sideways inside
        // the turned panel: from the local left side when the local X axis of the panel points up (the right panel,
        // and the left panel since it is flipped), from the local right side when it points down.
        private void OrientBulbs(RectTransform root, bool fromLeft, bool remember)
        {
            var images = new List<Image>();
            foreach (var bulb in root.GetComponentsInChildren<LifeBulb>(true))
                AddImage(images, bulb.GetComponent<Image>());
            foreach (var bulb in root.GetComponentsInChildren<ManaBulb>(true))
                AddImage(images, bulb.GetComponent<Image>());

            for (int i = 0; i < images.Count; ++i)
            {
                var image = images[i];
                if (remember)
                    _fills.Add(new SavedFill { image = image, method = image.fillMethod, origin = image.fillOrigin });
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int) (fromLeft ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right);
            }
        }

        private static void AddImage(List<Image> images, Image image)
        {
            if (image != null && !images.Contains(image))
                images.Add(image);
        }
    }
}
