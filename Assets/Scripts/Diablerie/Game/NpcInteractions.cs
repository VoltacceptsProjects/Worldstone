using Diablerie.Engine;
using Diablerie.Engine.Entities;
using Diablerie.Engine.World;
using Diablerie.Game.UI;
using UnityEngine;

namespace Diablerie.Game
{
    public class NpcInteractions : MonoBehaviour
    {
        public void Awake()
        {
            Events.UnitInteractionStarted += OnInteract;
        }

        private void OnDestroy()
        {
            Events.UnitInteractionStarted -= OnInteract;
        }

        private static bool IsDeckardCain(Unit unit)
        {
            string id = unit.monStat.id;
            return id != null && id.StartsWith("cain", System.StringComparison.OrdinalIgnoreCase) &&
                   !id.Equals("cain1", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void IdentifyItemsForPlayer(Player player)
        {
            int count = ItemIdentification.IdentifyAll(player);
            Debug.Log("Deckard Cain identified " + count + " item(s)");
            if (count > 0)
                AudioManager.instance.Play("cursor_identify_item");
        }

        private void OnInteract(Unit target, Unit initiator)
        {
            if (target.monStat == null || !target.monStat.npc || target.monStat.id == null)
                return;

            initiator.LookAt(target.iso.pos);

            var player = WorldState.instance.Player;
            bool byPlayer = player != null && player.unit == initiator;
            bool cain = IsDeckardCain(target);
            bool gambler = Gambling.IsGambler(target.monStat);
            bool vendor = VendorStock.IsVendor(target.monStat);

            if (!byPlayer || (!cain && !gambler && !vendor))
            {
                NpcVoice.PlayGreeting(target);
                return;
            }

            if (cain)
                IdentifyItemsForPlayer(player);
            NpcDialog.Show(player, target, vendor, gambler);
        }
    }
}
