using UnityEngine;
using UnityEngine.UI;

namespace Diablerie.Game.UI.ControlPanel
{
    public class ManaBulb : MonoBehaviour
    {
        [SerializeField]
        private Text label;
        private Image imgManaBar;

        // 0: player 1, 1: player 2 (set on the co-op copy of the panel)
        public int playerIndex;

        void Awake()
        {
            imgManaBar = GetComponent<Image>();
        }

        void Update()
        {
            var player = PanelPlayer.Get(playerIndex);
            if (player == null || player.unit == null)
                return;
            float currentMana = player.unit.mana;
            float maxMana = player.unit.maxMana;
            imgManaBar.fillAmount = currentMana / maxMana;
            label.text = "Mana: " + currentMana + "/" + maxMana;
        }
    }
}
