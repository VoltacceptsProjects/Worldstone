using Diablerie.Engine;
using Diablerie.Engine.World;

namespace Diablerie.Game.UI.ControlPanel
{
    // Which player a control panel shows: 0 is player 1 (the original panel), 1 is player 2 (the co-op panel)
    public static class PanelPlayer
    {
        public static Player Get(int index)
        {
            var world = WorldState.instance;
            if (world == null)
                return null;
            var players = world.Players;
            return index >= 0 && index < players.Count ? players[index] : null;
        }
    }
}
