using Diablerie.Engine.Entities;
using Diablerie.Engine.World;
using UnityEngine;

namespace Diablerie.Engine
{
    public class Experience : MonoBehaviour
    {
        void Start()
        {
            Events.UnitDied += UnitDeath;
        }

        void OnDestroy()
        {
            Events.UnitDied -= UnitDeath;
        }

        void UnitDeath(Unit target, Unit killer)
        {
            var world = WorldState.instance;

            // A kill by any player pays everybody who is still standing
            if (target.monStat == null || !world.IsPlayerUnit(killer))
                return;

            uint exp = target.monStat.stats[0].exp;
            var players = world.Players;
            for (int i = 0; i < players.Count; ++i)
            {
                var player = players[i];
                if (player.unit == killer || !player.unit.IsDead)
                    player.charStat.AddExp(exp);
            }
        }
    }
}
