using System.Collections.Generic;
using Diablerie.Engine.Datasheets;
using UnityEngine;

namespace Diablerie.Engine.SaveGame
{
    public static class ItemSerializer
    {
        public static ItemSave ToSave(Item item)
        {
            var save = new ItemSave
            {
                code = item.info.code,
                name = item.name,
                quality = (int)item.quality,
                level = item.level,
                levelReq = item.levelReq,
                identified = item.identified,
                quantity = item.quantity,
                invFileIndex = item.InvFileIndex,
                uniqueName = item.unique?.nameStr,
                setItemId = item.setItem?.id,
            };

            foreach (var prop in item.properties)
                save.properties.Add(ToSave(prop));

            foreach (var group in item.setItemProperties)
            {
                var groupSave = new PropertyGroupSave();
                foreach (var prop in group)
                    groupSave.properties.Add(ToSave(prop));
                save.setItemProperties.Add(groupSave);
            }

            return save;
        }

        // Returns null (and logs) if the item can't be reconstructed, e.g. datasheets changed.
        public static Item FromSave(ItemSave save)
        {
            if (save == null || string.IsNullOrEmpty(save.code))
                return null;

            var info = ItemInfo.Find(save.code);
            if (info == null)
            {
                Debug.LogWarning("Save: unknown item code '" + save.code + "', item skipped");
                return null;
            }

            var item = new Item(info)
            {
                name = string.IsNullOrEmpty(save.name) ? info.name : save.name,
                quality = (Item.Quality)save.quality,
                level = save.level,
                levelReq = save.levelReq,
                identified = save.identified,
                quantity = save.quantity,
                InvFileIndex = save.invFileIndex,
            };

            if (!string.IsNullOrEmpty(save.uniqueName))
            {
                item.unique = UniqueItem.sheet.Find(u => u.nameStr == save.uniqueName);
                if (item.unique == null)
                    Debug.LogWarning("Save: unknown unique '" + save.uniqueName + "'");
            }

            if (!string.IsNullOrEmpty(save.setItemId))
            {
                item.setItem = SetItem.sheet.Find(s => s.id == save.setItemId);
                if (item.setItem == null)
                    Debug.LogWarning("Save: unknown set item '" + save.setItemId + "'");
            }

            foreach (var propSave in save.properties)
            {
                if (TryFromSave(propSave, out var prop))
                    item.properties.Add(prop);
            }

            foreach (var groupSave in save.setItemProperties)
            {
                var group = new List<Item.Property>();
                foreach (var propSave in groupSave.properties)
                {
                    if (TryFromSave(propSave, out var prop))
                        group.Add(prop);
                }
                item.setItemProperties.Add(group);
            }

            return item;
        }

        private static PropertySave ToSave(Item.Property prop)
        {
            return new PropertySave
            {
                code = prop.info.code,
                param = prop.param,
                value = prop.value,
                min = prop.min,
                max = prop.max,
                classSpecific = prop.classSpecific,
            };
        }

        private static bool TryFromSave(PropertySave save, out Item.Property prop)
        {
            prop = new Item.Property();
            var info = string.IsNullOrEmpty(save.code) ? null : ItemPropertyInfo.Find(save.code);
            if (info == null)
            {
                Debug.LogWarning("Save: unknown item property '" + save.code + "', skipped");
                return false;
            }

            prop.info = info;
            // JsonUtility turns null strings into "", the item code expects null for "no param"
            prop.param = string.IsNullOrEmpty(save.param) ? null : save.param;
            prop.value = save.value;
            prop.min = save.min;
            prop.max = save.max;
            prop.classSpecific = string.IsNullOrEmpty(save.classSpecific) ? null : save.classSpecific;
            return true;
        }
    }
}
