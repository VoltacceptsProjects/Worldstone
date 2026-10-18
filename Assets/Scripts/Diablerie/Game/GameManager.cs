using Diablerie.Engine;
using Diablerie.Engine.UI;
using Diablerie.Game.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Diablerie.Game
{
    public class GameManager
    {
        public static void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }

        // Like Diablo II's "Save and Exit Game": saves the character and returns to the main menu.
        public static void SaveAndExitToMenu()
        {
            WorldBuilder.SaveCurrentPlayer();

            // Undo anything the game scene changed globally, the next game starts from a clean slate
            Time.timeScale = 1;
            SoftwareCursor.SetCursor(null);
            Cursor.visible = true;
            if (AudioManager.instance != null)
                AudioManager.instance.ResetWorldAudio();
            Level.ResetCurrent();
            Diablerie.Engine.Gambling.Reset();
            WorldBuilder.pendingSave = null;

            SceneManager.LoadScene("MainMenu");
        }
    }
}