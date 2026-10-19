using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Game.World;
using UnityEngine;

namespace Diablerie.Engine.Quest
{
    /// <summary>
    /// Makes sure the quest objects exist when their level is entered. The Act 1 levels bring their own
    /// objects from the DS1 presets (Gibbet, Inifuss, Cairn Stones, Malus), only Cain's cage gets a fallback.
    /// </summary>
    public class RuntimeQuestObjectSpawner : MonoBehaviour
    {
        private const int GibbetObjectId = 26;

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

            if (newLevel.id == 38) // Tristram
                EnsureCainsCage();
        }

        private void EnsureCainsCage()
        {
            foreach (var obj in FindObjectsOfType<StaticObject>())
            {
                if (obj.info != null && obj.info.id == GibbetObjectId)
                    return;
            }

            if (!Act1.HasTristram)
                return;

            Debug.LogWarning("Cain's cage not found in Tristram, spawning fallback");
            var objectInfo = ObjectInfo.sheet[GibbetObjectId];
            var position = Iso.MapTileToWorld(Act1.TristramEntry + new Diablerie.Engine.Utility.Vector2i(3, 3));
            var cage = WorldBuilder.SpawnObject(objectInfo, position, parent: transform);
            cage.gameObject.name = "Cain's Cage (Fallback)";
        }
    }
}
