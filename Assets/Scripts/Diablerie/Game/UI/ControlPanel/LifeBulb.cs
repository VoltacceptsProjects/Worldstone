using UnityEngine;
using UnityEngine.UI;

namespace Diablerie.Game.UI.ControlPanel
{
    public class LifeBulb : MonoBehaviour
    {
        [SerializeField]
        private Text label;
        private Image imgHealthBar;

        // 0: player 1, 1: player 2 (set on the co-op copy of the panel)
        public int playerIndex;

        void Awake()
        {
            imgHealthBar = GetComponent<Image>();
        }

        void Update()
        {
            var player = PanelPlayer.Get(playerIndex);
            if (player == null || player.unit == null)
                return;
            float currHealth = player.unit.health;
            float maxHealth = player.unit.maxHealth;
            imgHealthBar.fillAmount = currHealth / maxHealth;
            label.text = "Life: " + currHealth + "/" + maxHealth;
        }
    }
}
