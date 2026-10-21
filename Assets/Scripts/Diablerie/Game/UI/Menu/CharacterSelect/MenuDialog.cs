using System;
using UnityEngine;
using UnityEngine.UI;

namespace Diablerie.Game.UI.Menu.CharacterSelect
{
    // Modal dialog: either yes/no confirmation or a single line text input.
    // Enter accepts, Escape cancels. Destroys itself when closed.
    public class MenuDialog : MonoBehaviour
    {
        private Action<string> _onAccept;
        private Action _onClosed;
        private Func<string, string> _validate;
        private InputField _input;
        private Text _error;
        private bool _closed;

        public static MenuDialog ShowConfirm(Transform canvas, string message, Action onYes, Action onClosed = null)
        {
            var dialog = Build(canvas, message, withInput: false, maxLength: 0, acceptLabel: "YES", cancelLabel: "NO");
            dialog._onAccept = _ => onYes?.Invoke();
            dialog._onClosed = onClosed;
            return dialog;
        }

        // validate returns an error message (shown in the dialog) or null if the input is acceptable.
        public static MenuDialog ShowInput(Transform canvas, string message, int maxLength,
            Func<char, bool> charFilter, Func<string, string> validate, Action<string> onAccept, Action onClosed = null)
        {
            var dialog = Build(canvas, message, withInput: true, maxLength: maxLength, acceptLabel: "OK", cancelLabel: "CANCEL");
            dialog._validate = validate;
            dialog._onAccept = onAccept;
            dialog._onClosed = onClosed;
            dialog._input.onValidateInput = (text, index, c) => charFilter(c) ? c : '\0';
            return dialog;
        }

        private static MenuDialog Build(Transform canvas, string message, bool withInput, int maxLength, string acceptLabel, string cancelLabel)
        {
            // Full screen dim layer, also swallows clicks aimed at the screen below
            var dim = MenuWidgets.Box("Dialog", canvas, new Color(0, 0, 0, 0.7f));
            MenuWidgets.Stretch(dim.rectTransform);
            var dialog = dim.gameObject.AddComponent<MenuDialog>();

            float height = withInput ? 230 : 170;
            var box = MenuWidgets.Box("Box", dim.transform, new Color(0.06f, 0.06f, 0.06f, 1));
            MenuWidgets.Center(box.rectTransform, Vector2.zero, new Vector2(480, height));
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = MenuWidgets.Gold;
            outline.effectDistance = new Vector2(2, -2);

            float top = height / 2;
            var text = MenuWidgets.Label("Message", box.transform, message, Fonts.GetFont30(), Color.white, TextAnchor.MiddleCenter);
            MenuWidgets.PlaceText(text, new Vector2(0, top - 45), new Vector2(440, 40));

            if (withInput)
            {
                var inputImage = MenuWidgets.Box("Input", box.transform, new Color(0.14f, 0.14f, 0.14f, 1));
                MenuWidgets.Center(inputImage.rectTransform, new Vector2(0, top - 100), new Vector2(300, 38));
                var inputText = MenuWidgets.Label("Text", inputImage.transform, "", Fonts.GetFont24(), Color.white, TextAnchor.MiddleLeft);
                inputText.supportRichText = false;
                MenuWidgets.StretchText(inputText, horizontalPadding: 10);

                dialog._input = inputImage.gameObject.AddComponent<InputField>();
                dialog._input.targetGraphic = inputImage;
                dialog._input.textComponent = inputText;
                dialog._input.lineType = InputField.LineType.SingleLine;
                dialog._input.characterLimit = maxLength;

                dialog._error = MenuWidgets.Label("Error", box.transform, "", Fonts.GetFont16(), MenuWidgets.Red, TextAnchor.MiddleCenter);
                MenuWidgets.PlaceText(dialog._error, new Vector2(0, top - 140), new Vector2(440, 24));
            }

            float buttonY = -top + 36;
            MenuWidgets.TextButton(box.transform, acceptLabel, new Vector2(-80, buttonY), new Vector2(130, 40), dialog.Accept);
            MenuWidgets.TextButton(box.transform, cancelLabel, new Vector2(80, buttonY), new Vector2(130, 40), dialog.Cancel);

            if (withInput)
            {
                dialog._input.Select();
                dialog._input.ActivateInputField();
            }

            return dialog;
        }

        private void Update()
        {
            if (_closed)
                return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Diablerie.Engine.UI.GamepadCursor.ConfirmDown)
                Accept();
            else if (Input.GetKeyDown(KeyCode.Escape) || Diablerie.Engine.UI.GamepadCursor.CancelDown)
                Cancel();
        }

        private void Accept()
        {
            if (_closed)
                return;

            string value = _input != null ? _input.text : null;
            if (_validate != null)
            {
                string error = _validate(value);
                if (error != null)
                {
                    _error.text = error;
                    _input.ActivateInputField();
                    return;
                }
            }

            MenuWidgets.PlayClick();
            Close();
            _onAccept?.Invoke(value);
        }

        private void Cancel()
        {
            if (_closed)
                return;

            MenuWidgets.PlayClick();
            Close();
        }

        private void Close()
        {
            _closed = true;
            Destroy(gameObject);
            _onClosed?.Invoke();
        }
    }
}
