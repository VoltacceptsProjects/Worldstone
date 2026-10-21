using UnityEngine;

namespace Diablerie.Game.UI
{
    public class CharstatPanel : MonoBehaviour
    {
        public static CharstatPanel instance;

        public GameObject panel;

        void Awake()
        {
            instance = this;
        }

        public void ToggleVisibility()
        {
            if (Diablerie.Game.Coop.CoopUi.GuestOwns)
                return;
            visible ^= true;
        }

        public bool visible
        {
            set { panel.SetActive(value); }
            get { return panel.activeSelf; }
        }
    }
}
