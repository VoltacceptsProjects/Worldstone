using Diablerie.Engine.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Diablerie.Game.UI.ControlPanel
{
    public class ExperienceBar : MonoBehaviour
    {
        [SerializeField]
        private Image filler;

        [SerializeField]
        private Tooltip tooltip;

        // 0: player 1, 1: player 2 (set on the co-op copy of the panel)
        public int playerIndex;

        void Update()
        {
            var player = PanelPlayer.Get(playerIndex);
            if (player == null)
                return;
            int level = player.charStat.level;
            uint currExp = player.charStat.experience;
            uint currLevelExp = player.charStat.currentLevelExp;
            uint nextLevelExp = player.charStat.nextLevelExp;
            filler.fillAmount = (currExp - currLevelExp) / (float)(nextLevelExp - currLevelExp);
            tooltip.text = "Level " + level + "\nExperience: " + (currExp - currLevelExp) + " / " + (nextLevelExp - currLevelExp);
        }
    }
}
