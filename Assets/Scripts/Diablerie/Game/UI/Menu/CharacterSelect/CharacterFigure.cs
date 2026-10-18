using System;
using Diablerie.Engine;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.IO.D2Formats;
using Diablerie.Engine.SaveGame;
using UnityEngine;
using UnityEngine.UI;

namespace Diablerie.Game.UI.Menu.CharacterSelect
{
    // The little animated character next to a character select slot. Composes the class's town idle
    // animation from its COF layers (head, torso, legs, arms, weapons...) using plain UI images, so it
    // works on the overlay canvas. Equipment comes from the save, the same way Equipment does in game.
    public class CharacterFigure : MonoBehaviour
    {
        private const string BasePath = @"data\global\chars";
        private const float FramesPerSecond = 12;
        // Direction 0 faces the viewer (south). Unit.DirectionCount directions in total.
        private const int Direction = 0;

        private static readonly string[] ArmorTypes = { "LIT", "MED", "HVY" };
        private static readonly string[] DefaultEquip = { "LIT", "LIT", "LIT", "LIT", "LIT", "", "", "", "LIT", "LIT", "", "", "", "", "", "" };

        private class LayerView
        {
            public Image image;
            public Sprite[] sprites;
        }

        private COF _cof;
        private LayerView[] _layers;
        private float _time;

        // feet is the point on the ground the figure stands on, in the local space of parent (top left origin, y down)
        public static CharacterFigure Create(Transform parent, Vector2 feet, CharacterSave save)
        {
            if (save == null)
                return null;

            var charInfo = CharStatsInfo.Find(save.className);
            if (charInfo == null)
                return null;

            var go = new GameObject("Figure", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform) go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = new Vector2(feet.x, -feet.y);

            var figure = go.AddComponent<CharacterFigure>();
            try
            {
                if (!figure.Build(charInfo, save))
                {
                    Destroy(go);
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("CharacterFigure: could not build " + save.className + ": " + e.Message);
                Destroy(go);
                return null;
            }
            return figure;
        }

        private bool Build(CharStatsInfo charInfo, CharacterSave save)
        {
            var equip = new string[DefaultEquip.Length];
            Array.Copy(DefaultEquip, equip, DefaultEquip.Length);
            string weaponClass = charInfo.baseWClass ?? "HTH";

            foreach (var entry in save.equipment)
            {
                if (entry == null || entry.item == null || string.IsNullOrEmpty(entry.item.code))
                    continue;
                var info = ItemInfo.Find(entry.item.code);
                if (info == null)
                    continue;

                if (info.armor != null && info.armor.rArm != -1)
                {
                    equip[Array.IndexOf(COF.layerNames, "RA")] = ArmorTypes[info.armor.rArm];
                    equip[Array.IndexOf(COF.layerNames, "LA")] = ArmorTypes[info.armor.lArm];
                    equip[Array.IndexOf(COF.layerNames, "TR")] = ArmorTypes[info.armor.torso];
                    equip[Array.IndexOf(COF.layerNames, "LG")] = ArmorTypes[info.armor.legs];
                    equip[Array.IndexOf(COF.layerNames, "S1")] = ArmorTypes[info.armor.rSPad];
                    equip[Array.IndexOf(COF.layerNames, "S2")] = ArmorTypes[info.armor.lSPad];
                }
                else
                {
                    if (info.component >= 0 && info.component < equip.Length)
                        equip[info.component] = info.alternateGfx;
                    if (info.weapon != null)
                        weaponClass = info.weapon.twoHanded ? info.weapon.twoHandedWClass : info.weapon.wClass;
                }
            }

            _cof = LoadCof(charInfo.token, weaponClass) ?? LoadCof(charInfo.token, "HTH");
            if (_cof == null)
                return false;

            _layers = new LayerView[_cof.layerCount];
            for (int i = 0; i < _cof.layerCount; ++i)
            {
                var cofLayer = _cof.layers[i];
                string gear = equip[cofLayer.compositIndex];
                if (string.IsNullOrEmpty(gear))
                    continue;

                var sheet = Spritesheet.Load(_cof.GetSpritesheetFilename(cofLayer, gear));
                if (sheet == null)
                    continue;

                int sheetDirection = Direction * sheet.directionCount / Unit.DirectionCount;
                var sprites = sheet.GetSprites(sheetDirection);
                if (sprites == null || sprites.Length == 0)
                    continue;
                foreach (var sprite in sprites)
                {
                    if (sprite != null && sprite.texture != null)
                        sprite.texture.filterMode = FilterMode.Point;
                }

                var rect = MenuWidgets.NewRect(cofLayer.name, transform);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                var image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                _layers[i] = new LayerView { image = image, sprites = sprites };
            }

            ShowFrame(0);
            return true;
        }

        // Town idle first, the dungeon idle if the class has no town animation
        private static COF LoadCof(string token, string weaponClass)
        {
            foreach (var mode in new[] { "TN", "NU" })
            {
                try
                {
                    return COF.Load(BasePath, token, weaponClass, mode);
                }
                catch (Exception)
                {
                    // no such animation, try the next mode
                }
            }
            return null;
        }

        private void Update()
        {
            if (_cof == null)
                return;
            _time += Time.unscaledDeltaTime;
            ShowFrame(Mathf.FloorToInt(_time * FramesPerSecond));
        }

        private void ShowFrame(int frame)
        {
            frame %= Mathf.Max(1, _cof.framesPerDirection);
            int cofDirection = Direction * _cof.directionCount / Unit.DirectionCount;
            int priority = cofDirection * _cof.framesPerDirection * _cof.layerCount + frame * _cof.layerCount;

            // Later layers in the priority list are drawn on top, so they become later siblings
            int order = 0;
            for (int i = 0; i < _cof.layerCount; ++i)
            {
                var cofLayer = _cof.compositLayers[_cof.priority[priority + i]];
                var layer = _layers[cofLayer.index];
                if (layer == null)
                    continue;

                var sprite = frame < layer.sprites.Length ? layer.sprites[frame] : null;
                layer.image.enabled = sprite != null;
                if (sprite == null)
                    continue;

                layer.image.sprite = sprite;
                var rt = layer.image.rectTransform;
                var size = sprite.rect.size;
                rt.sizeDelta = size;
                rt.pivot = new Vector2(sprite.pivot.x / size.x, sprite.pivot.y / size.y);
                rt.anchoredPosition = Vector2.zero;
                rt.SetSiblingIndex(order++);
            }
        }
    }
}