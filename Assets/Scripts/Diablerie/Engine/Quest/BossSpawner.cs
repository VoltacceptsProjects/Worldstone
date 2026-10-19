using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.World;
using Diablerie.Game.World;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Spawns quest bosses at runtime when their level is entered, once per world, through the regular
    /// monster pipeline (renderer, collision, controllers). The old version created bare Unit objects
    /// without a renderer on every level change, which threw errors every frame and froze the game.
    /// </summary>
    public class BossSpawner : MonoBehaviour
    {
        private bool bloodRavenSpawned;
        private bool corpsefireSpawned;

        private void Start()
        {
            Events.LevelChanged += OnLevelChanged;
        }

        private void OnDestroy()
        {
            Events.LevelChanged -= OnLevelChanged;
        }

        private void OnLevelChanged(LevelInfo newLevel, LevelInfo previousLevel)
        {
            if (newLevel == null)
                return;

            switch (newLevel.id)
            {
                case 17: // Act 1 - Graveyard (Burial Grounds)
                    if (!bloodRavenSpawned)
                        bloodRavenSpawned = Spawn(MonStat.Find("bloodraven"), null, 3);
                    break;
                case 2: // Act 1 - Wilderness 1 (Blood Moor)
                    if (!corpsefireSpawned)
                    {
                        var superUnique = SuperUnique.Find("corpsefire");
                        var monStat = superUnique != null ? superUnique.monStat : MonStat.Find("corpsefire");
                        corpsefireSpawned = Spawn(monStat, superUnique, 2);
                    }
                    break;
            }
        }

        private static bool Exists(MonStat monStat, SuperUnique superUnique)
        {
            foreach (var entity in WorldState.instance.Entities)
            {
                if (!(entity is Unit unit) || unit.monStat == null)
                    continue;
                if (superUnique != null ? unit.title == superUnique.name : unit.monStat.id == monStat.id)
                    return true;
            }
            return false;
        }

        // Returns true when the boss exists afterwards (already there or spawned now)
        private bool Spawn(MonStat monStat, SuperUnique superUnique, int healthMultiplier)
        {
            if (monStat == null)
                return true; // nothing to spawn, do not retry
            if (Exists(monStat, superUnique))
                return true;

            var player = WorldState.instance.Player;
            if (player == null || player.unit == null)
                return false;

            var isoPos = player.unit.iso.pos + new Vector2(12, 12);
            var unit = WorldBuilder.SpawnMonster(monStat, Iso.MapToWorld(isoPos), transform);
            if (unit == null)
                return false;

            unit.party = Party.Evil;
            unit.killable = true;
            if (superUnique != null)
            {
                unit.title = superUnique.name;
                unit.gameObject.name = superUnique.nameStr;
            }
            unit.maxHealth = unit.maxHealth * healthMultiplier;
            unit.health = unit.maxHealth;
            return true;
        }
    }
}
