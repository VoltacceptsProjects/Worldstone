using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.World;
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
        
        // cain1 is the caged Cain of Tristram. cain2 and up are the ones who stand in the towns.
        private static bool IsDeckardCain(Unit unit)
        {
            string id = unit.monStat.id;
            return id != null && id.StartsWith("cain", System.StringComparison.OrdinalIgnoreCase) &&
                   !id.Equals("cain1", System.StringComparison.OrdinalIgnoreCase);
        }

        // "I will identify items for you at no charge."
        private static void IdentifyItemsForPlayer(Unit initiator)
        {
            var player = WorldState.instance.Player;
            if (player == null || player.unit != initiator)
                return;

            int count = ItemIdentification.IdentifyAll(player);
            Debug.Log("Deckard Cain identified " + count + " item(s)");
            if (count > 0)
                AudioManager.instance.Play("cursor_identify_item");
        }

        private void OnInteract(Unit target, Unit initiator)
        {
            if (target.monStat == null || !target.monStat.npc)
                return;
            Debug.Log("Interact with " + target);
            if (IsDeckardCain(target))
                IdentifyItemsForPlayer(initiator);
            initiator.LookAt(target.iso.pos);
            var greetingSound = SoundInfo.Find(target.monStat.id + "_greeting_1");
            AudioManager.instance.Play(greetingSound, target.transform);
        }
    }
}
