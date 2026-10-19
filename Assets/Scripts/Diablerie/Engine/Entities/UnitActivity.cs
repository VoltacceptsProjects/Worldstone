using System.Collections.Generic;
using Diablerie.Engine.World;
using UnityEngine;

namespace Diablerie.Engine.Entities
{
    // Streamed levels stay in the world, so their monsters would otherwise keep thinking, pathfinding and
    // animating forever. Monsters farther from the player than SleepDistance are deactivated and woken up
    // again when the player comes within WakeDistance (iso units, a screen is about 25 wide).
    public class UnitActivity : MonoBehaviour
    {
        public const float WakeDistance = 45f;
        public const float SleepDistance = 60f;
        private const float CheckInterval = 0.5f;

        private static readonly List<Unit> units = new List<Unit>();
        private float nextCheck;

        public static void Register(Unit unit)
        {
            unit.canSleep = true;
            units.Add(unit);
        }

        public static bool IsFar(Vector2 isoPos)
        {
            var player = WorldState.instance != null ? WorldState.instance.Player : null;
            if (player == null || player.unit == null)
                return false;
            return Vector2.Distance(player.unit.iso.pos, isoPos) > WakeDistance;
        }

        // Every registered monster, including sleeping ones that have not been awake yet
        public static List<Unit> All => units;

        private void OnDestroy()
        {
            units.Clear();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + CheckInterval;

            var player = WorldState.instance != null ? WorldState.instance.Player : null;
            if (player == null || player.unit == null)
                return;
            Vector2 center = player.unit.iso.pos;
            var hot = MouseSelection.instance != null ? MouseSelection.instance.HotEntity : null;

            for (int i = units.Count - 1; i >= 0; --i)
            {
                var unit = units[i];
                if (unit == null)
                {
                    units[i] = units[units.Count - 1];
                    units.RemoveAt(units.Count - 1);
                    continue;
                }

                var go = unit.gameObject;
                // Position is kept in the transform, which stays valid for inactive units
                Vector2 pos = Iso.MapToIso(unit.transform.position);
                float distance = Vector2.Distance(center, pos);
                if (go.activeSelf)
                {
                    if (distance > SleepDistance && unit != hot)
                        go.SetActive(false);
                }
                else if (distance < WakeDistance)
                {
                    go.SetActive(true);
                }
            }
        }
    }
}
