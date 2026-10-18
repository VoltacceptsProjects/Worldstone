using System.Collections;
using System.IO;
using Diablerie.Engine;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.SaveGame;
using Diablerie.Engine.UI;
using Diablerie.Game.UI.Menu.CharacterSelect;
using Diablerie.Game.UI.Menu.ClassSelect;
using Diablerie.Game.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Diablerie.Game.UI.Menu
{
    public class MainMenu : MonoBehaviour
    {
        public RectTransform logoPlaceholder;
        
        public ImageButton singlePlayerButton;
        public ImageButton multiPlayerButton;
        public ImageButton cinematicsButton;
        public ImageButton exitButton;
        
        public GameObject classSelectMenu;
        public GameObject mainMenu;

        private GameObject _logo;
        private CharacterSelectMenu _characterSelect;
        private GameObject _dialogCanvas;
        private Stream _musicStream;
        
        private void Awake()
        {
            classSelectMenu.SetActive(false);
            
            singlePlayerButton.OnClick += SinglePlayerButtonOnClick;
            multiPlayerButton.Disabled = true;
            cinematicsButton.Disabled = true;
            exitButton.OnClick += ExitButtonOnClick;

            var classSelect = classSelectMenu.GetComponent<ClassSelectMenu>();
            classSelect.exitButton.OnClick += ClassSelectMenuExitButtonOnClick;
            classSelect.OnClassChosen += OnClassChosen;

            _characterSelect = CharacterSelectMenu.Create(transform);
            _characterSelect.OnPlay += OnPlayCharacter;
            _characterSelect.OnCreateNew += OnCreateCharacter;
            _characterSelect.OnExit += OnCharacterSelectExit;

            _logo = CreateLogo();
            _logo.transform.position = UiHelper.ScreenToWorldPoint(logoPlaceholder.position);
            _logo.transform.parent = logoPlaceholder;
        }

        private void ClassSelectMenuExitButtonOnClick()
        {
            classSelectMenu.SetActive(false);
            _characterSelect.Show();
        }

        private void OnCharacterSelectExit()
        {
            mainMenu.SetActive(true);
        }

        private void OnCreateCharacter()
        {
            classSelectMenu.SetActive(true);
        }

        private void OnPlayCharacter(CharacterSave save)
        {
            StartGame(save.className, save.name, save);
        }

        private void OnClassChosen(string chosenClassName)
        {
            if (_dialogCanvas == null)
                _dialogCanvas = MenuWidgets.CreateCanvas("Dialog Canvas", transform, sortingOrder: 200);

            MenuDialog.ShowInput(_dialogCanvas.transform, "Enter character name", CharacterNames.MaxLength,
                charFilter: CharacterNames.IsAllowedChar,
                validate: name =>
                {
                    string error = CharacterNames.Validate(name);
                    if (error != null)
                        return error;
                    if (SaveManager.NameExists(name))
                        return "A character with this name already exists.";
                    return null;
                },
                onAccept: name =>
                {
                    classSelectMenu.SetActive(false);
                    StartGame(chosenClassName, name, null);
                });
        }

        private void StartGame(string startClassName, string characterName, CharacterSave save)
        {
            WorldBuilder.className = startClassName;
            WorldBuilder.characterName = characterName;
            WorldBuilder.pendingSave = save;
            StartCoroutine(LoadGame());
        }

        private IEnumerator LoadGame()
        {
            LoadingScreen.Show();
            yield return null;
            SceneManager.LoadScene("Game");
        }

        private void OnDestroy()
        {
            var classSelect = classSelectMenu.GetComponent<ClassSelectMenu>();
            classSelect.exitButton.OnClick -= ClassSelectMenuExitButtonOnClick;
            classSelect.OnClassChosen -= OnClassChosen;
            singlePlayerButton.OnClick -= SinglePlayerButtonOnClick;
            exitButton.OnClick -= ExitButtonOnClick;
        }

        private void SinglePlayerButtonOnClick()
        {
            mainMenu.SetActive(false);
            _characterSelect.Show();
        }
        
        private static void ExitButtonOnClick()
        {
            GameManager.QuitGame();
        }
        
        private static GameObject CreateLogo()
        {
            var logo = new GameObject("logo");

            var left = UiHelper.CreateAnimatedObject("leftLogo", @"data\global\ui\FrontEnd\d2LogoFireLeft", material: Materials.SoftAdditive, sortingOrder: 1);
            var right = UiHelper.CreateAnimatedObject("rightLogo", @"data\global\ui\FrontEnd\d2LogoFireRight", material: Materials.SoftAdditive, sortingOrder: 1);

            left.transform.SetParent(logo.transform, false);
            right.transform.SetParent(logo.transform, false);

            return logo;
        }
        
        private void OnEnable()
        {
            PlayMusic();
        }

        private void OnDisable()
        {
            StopMusic();
        }

        private void PlayMusic()
        {
            try
            {
                _musicStream = Mpq.fs.OpenFile(@"data\global\music\introedit.wav");
                var clip = Wav.Load("intro music", true, _musicStream);
                var musicObject = new GameObject("intro music");
                musicObject.transform.parent = transform;
                var audioSource = musicObject.AddComponent<AudioSource>();
                audioSource.clip = clip;
                audioSource.loop = true;
                audioSource.Play();
            }
            catch (FileNotFoundException)
            {
            }
        }

        private void StopMusic()
        {
            if (_musicStream == null)
            {
                return;
            }

            _musicStream.Close();
        }
    }
}
