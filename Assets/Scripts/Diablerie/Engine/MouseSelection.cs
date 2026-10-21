using System.Collections.Generic;
using Diablerie.Engine.Entities;
using Diablerie.Engine.UI;
using Diablerie.Engine.Utility;
using Diablerie.Engine.World;
using Diablerie.Game.UI;
using UnityEngine;

namespace Diablerie.Engine
{
    class MouseSelection : MonoBehaviour
    {
        public static MouseSelection instance;
        
        private Entity hotEntity;
        private bool highlightLoot;
        private LootHighlighter _lootHighlighter;
        private readonly HashSet<Loot> loots = new HashSet<Loot>();
        private LabelPool lootLabelPool;
        private Label label;

        void Awake()
        {
            instance = this;
            lootLabelPool = new LabelPool(transform);
            _lootHighlighter = new LootHighlighter(lootLabelPool);
            label = new Label(transform);
            label.Hide();
        }

        void Update()
        {
            bool updateHotEntity = !PlayerController.instance.FixedSelection();
            loots.Clear();
            if (highlightLoot)
            {
                FindVisibleLoots();
            }
            // Loot labels are never hovered with the mouse, the hot entity comes from the player's 3x3 tiles only
            _lootHighlighter.Show(loots, false);
            
            if (updateHotEntity)
            {
                if (Ui.Hover)
                    HotEntity = null;
                else
                    HotEntity = CalcHotEntity();
            }

            if (HotEntity != null && !highlightLoot)
            {
                var unit = HotEntity.GetComponent<Unit>();
                if (unit && unit.monStat != null)
                {
                    if (unit.monStat.interact)
                    {
                        ShowLabel(HotEntity);
                    }
                    else if (unit.monStat.killable)
                    {
                        ShowEnemyBar(unit);
                    }
                    else
                    {
                        ShowNothing();
                    }
                }
                else
                {
                    ShowLabel(HotEntity);
                }
            }
            else
            {
                ShowNothing();
            }
        }

        public Entity HotEntity
        {
            get { return hotEntity; }
            private set
            {
                if (hotEntity == value)
                    return;
                if (hotEntity != null)
                    hotEntity.selected = false;
                hotEntity = value;
                if (hotEntity != null)
                    hotEntity.selected = true;
            }
        }

        public void SetHighlightLoot(bool highlightLoot)
        {
            this.highlightLoot = highlightLoot;
        }

        private void FindVisibleLoots()
        {
            foreach (var entity in WorldState.instance.Entities)
            {
                if (entity is Loot loot)
                {
                    Bounds bounds = entity.bounds;
                    Vector2 minPosition = Camera.main.WorldToViewportPoint(bounds.min);
                    if (minPosition.x >= 1 || minPosition.y >= 1)
                        continue;
                    Vector2 maxPosition = Camera.main.WorldToViewportPoint(bounds.max);
                    if (maxPosition.x <= 0 || maxPosition.y <= 0)
                        continue;
                    loots.Add(loot);
                }
            }
        }

        private void ShowLabel(Entity entity)
        {
            EnemyBar.instance.unit = null;
            var labelPosition = entity.transform.position + (Vector3) entity.titleOffset / Iso.pixelsPerUnit;
            label.Show(labelPosition, entity.title);
        }

        private void ShowEnemyBar(Unit unit)
        {
            EnemyBar.instance.unit = unit;
            label.Hide();
        }

        private void ShowNothing()
        {
            EnemyBar.instance.unit = null;
            label.Hide();
        }

        // True when the entity stands in the 3x3 tiles around the tile the unit stands on (the center tile included)
        public static bool IsInGrid6x6(Unit origin, Entity entity)
        {
            var center = Iso.Snap((Vector3) origin.iso.pos);
            var tile = Iso.Snap(Iso.MapToIso(entity.transform.position));
            return Mathf.Abs(tile.x - center.x) <= 4 && Mathf.Abs(tile.y - center.y) <= 4;
        }

        // The mouse does not pick anything: the hot entity is the closest selectable entity in the 3x3 tiles around the player
        private Entity CalcHotEntity()
        {
            var origin = PlayerController.instance.PlayerUnit;
            if (origin == null)
                return null;

            Vector2 originPos = origin.iso.pos;
            Entity bestMatch = null;
            float bestDistance = float.MaxValue;
            foreach (var entity in WorldState.instance.Entities)
            {
                if (!entity.selectable)
                    continue;

                if (WorldState.instance.IsPlayerUnit(entity))
                    continue;

                if (!IsInGrid6x6(origin, entity))
                    continue;

                float distance = Vector2.Distance(originPos, Iso.MapToIso(entity.transform.position));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = entity;
                }
            }

            return bestMatch;
        }
    }
}
