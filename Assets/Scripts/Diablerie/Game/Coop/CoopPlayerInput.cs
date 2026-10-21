using System.Collections.Generic;
using Diablerie.Engine;
using Diablerie.Engine.AI;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.World;
using Diablerie.Game.UI;
using UnityEngine;

namespace Diablerie.Game.Coop
{
    // Controller layout of Diablo II Resurrected, for player 1 (first gamepad) and player 2 (second gamepad):
    //
    //   Left stick                 walk, click: run / walk toggle
    //   A                          skill 1, or interact with the closest item, object or townsperson
    //   X, B, Y, RB, RT            skills 2, 3, 4, 5, 6
    //   LT (hold)                  second skill bar: A / X = the left / right mouse skill of the skill bar (player 1)
    //   LB (hold)                  show item labels (player 1)
    //   D-pad left, up, down, right   belt slots 1, 2, 3, 4: drinks a potion
    //   View (Back)                automap (player 1)
    //   Menu (Start)               game menu (player 1)
    //
    // Player 1 takes the skills from the skill bar (F1..F6), player 2 plays four fixed skills of the class
    // (A, X, B, Y). Skills auto aim at the closest enemy, otherwise they go the way the stick points.
    // Player 2 also plays on the keyboard: WASD / arrows walk, J K L U skills, E interact, 1 / 2 life / mana potion.
    // There is no belt and no weapon swap in this game: the belt slots are the potion kinds in the inventory
    // (life, then mana, then rejuvenation), R3 (swap weapons) does nothing.
    //
    // The axes CoopJoy1X..CoopJoy8RT are created by CoopInputSetup in the editor.
    public class CoopPlayerInput : MonoBehaviour
    {
        private const float DeadZone = 0.3f;
        private const float MoveReach = 8f;
        private const float MoveReissueInterval = 0.15f;
        private const float MoveReissueAngle = 12f;
        private const float AutoPickupRadius = 3.5f;
        private const float ItemPickupRadius = 2f;
        private const float AutoPickupInterval = 0.25f;
        private const float InteractRadius = 12f;
        private const float MeleeTargetRadius = 14f;
        private const float RangedTargetRadius = 22f;

        private static readonly int[] SkillPadButtons = { 0, 2, 1, 3, 5, -1 }; // A X B Y RB RT, -1: right trigger
        private const float TriggerThreshold = 0.5f;
        private static readonly KeyCode[] SkillKeys = { KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.U };

        private int _pad = 1;
        private bool _main;
        private Player _player;
        private Unit _unit;
        private readonly SkillInfo[] _skills = new SkillInfo[6];

        private bool _moving;
        private float _nextMoveIssue;
        private Vector2 _lastMoveDirection;
        private Vector2 _aimWorld = new Vector2(0f, -1f); // the way the character looks, in screen directions
        private float _nextAutoPickup;
        // Set while skills are not allowed (menus, interactions). Stays set until every skill button is let go.
        private bool _skillLatch;
        private bool _rightTrigger;
        private bool _rightTriggerPrevious;
        private bool _leftTrigger;
        private readonly bool[] _dpad = new bool[4];         // left, up, down, right
        private readonly bool[] _dpadDown = new bool[4];

        public static bool PadButtonDown(int pad, int button)
        {
            return CoopPads.ButtonDown(pad, button);
        }

        private static KeyCode PadKey(int pad, int button)
        {
            return CoopPads.Key(pad, button);
        }

        // The main player: skills come from the skill bar of the player controller
        public void InitMain(Player player, int pad)
        {
            _main = true;
            _pad = pad;
            _player = player;
            _unit = player.unit;
        }

        // The guest: a fixed set of skills
        public void Init(Player player, int pad, string[] loadout)
        {
            _main = false;
            _pad = pad;
            _player = player;
            _unit = player.unit;
            for (int i = 0; i < _skills.Length; ++i)
                _skills[i] = i < loadout.Length ? SkillInfo.Find(loadout[i]) : null;
            if (_skills[0] == null)
                _skills[0] = SkillInfo.Attack;
        }

        private void Update()
        {
            if (_unit == null || _player == null)
                return;

            // Read before any early return so the latch sees the buttons while menus are open
            ReadTriggersAndDpad();
            UpdateSkillLatch();

            // The game menu pauses the game, the world is loading while the main controller is off
            if (Time.timeScale <= 0f)
                return;
            var controller = PlayerController.instance;
            if (controller != null && !controller.enabled)
                return;
            if (_unit.IsDead)
            {
                _moving = false;
                return;
            }

            // Player 1 uses the cursor while a window is open
            if (_main && Diablerie.Engine.UI.GamepadCursor.Active)
            {
                StopWalking();
                return;
            }

            // WASD and arrows are also the keys of the console prompt
            if (CommandPrompt.instance != null && CommandPrompt.instance.visible)
            {
                StopWalking();
                return;
            }

            Vector2 stick = ReadStick();
            if (stick != Vector2.zero)
                _aimWorld = stick.normalized;

            if (_main)
            {
                if (PadButtonDown(_pad, 7))
                {
                    GameMenu.Show();
                    return;
                }
                if (PadButtonDown(_pad, 6))
                    Automap.Toggle();
                if (PadButtonDown(_pad, 8) && controller != null)
                    controller.ToggleRun();
            }
            else
            {
                if (PadButtonDown(_pad, 8))
                    _unit.run = !_unit.run;
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    DrinkPotion(health: true);
                if (Input.GetKeyDown(KeyCode.Alpha2))
                    DrinkPotion(health: false);
            }

            for (int i = 0; i < _dpadDown.Length; ++i)
            {
                if (_dpadDown[i])
                    DrinkBeltSlot(i);
            }

            if (!_main && Input.GetKeyDown(KeyCode.E))
                Interact();

            int slot = _skillLatch ? -1 : PressedSkillSlot();
            if (slot >= 0)
            {
                // A is skill 1 and interact in one: items, objects and townsfolk first, as long as no enemy is close
                if (slot == 0 && SkillDown(0) && !_leftTrigger &&
                    AIUtils.GetNearestEnemy(_unit, MeleeTargetRadius) == null && Interact())
                {
                    // The press was an interaction, not a cast: A must be released before it casts again
                    _skillLatch = true;
                    StopWalking();
                    return;
                }

                StopWalking();
                UseSkill(SkillAt(slot));
                return;
            }

            if (CoopManager.Instance != null)
                stick = CoopManager.Instance.ClampMovementToLeash(_player, stick);

            if (stick.magnitude >= DeadZone)
                Walk(stick);
            else
                StopWalking();

            PickUpGoldNearby();
        }

        #region Input

        private Vector2 ReadStick()
        {
            Vector2 value = new Vector2(CoopPads.Axis(_pad, "X"), CoopPads.Axis(_pad, "Y"));

            if (value.magnitude < DeadZone && !_main)
                value = KeyboardDirection();

            if (value.magnitude < DeadZone)
                return Vector2.zero;
            return value.normalized * Mathf.Min(1f, value.magnitude);
        }

        private static Vector2 KeyboardDirection()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            return new Vector2(x, y);
        }

        private int SlotCount => _skills.Length;

        private SkillInfo SkillAt(int slot)
        {
            if (!_main)
                return _skills[slot];

            var controller = PlayerController.instance;
            if (controller == null)
                return null;

            // The second skill bar (LT held) holds the two mouse skills
            if (_leftTrigger)
                return slot == 0 ? controller.LeftSkill : slot == 1 ? controller.RightSkill : null;

            var skill = controller.GetHotSkill(slot);
            if (skill == null && slot == 0)
                skill = SkillInfo.Attack;
            return skill;
        }

        private void ReadTriggersAndDpad()
        {
            _rightTriggerPrevious = _rightTrigger;
            _rightTrigger = CoopPads.Axis(_pad, "RT") > TriggerThreshold;
            _leftTrigger = CoopPads.Axis(_pad, "LT") > TriggerThreshold;

            float x = CoopPads.Axis(_pad, "DX");
            float y = CoopPads.Axis(_pad, "DY");
            bool[] now = { x < -TriggerThreshold, y > TriggerThreshold, y < -TriggerThreshold, x > TriggerThreshold };
            for (int i = 0; i < now.Length; ++i)
            {
                _dpadDown[i] = now[i] && !_dpad[i];
                _dpad[i] = now[i];
            }
        }

        // Skills are off while a menu / window is open or just closed (player 1) or the game is paused (everybody).
        // After that they stay off until all skill buttons were released, so a button held through a menu,
        // a close or an interaction does not start casting by itself.
        private void UpdateSkillLatch()
        {
            bool blocked = _main ? SkillGate.Blocked : SkillGate.WorldBlocked;
            if (blocked)
            {
                _skillLatch = true;
                return;
            }

            if (_skillLatch && !AnySkillHeld())
                _skillLatch = false;
        }

        private bool AnySkillHeld()
        {
            for (int i = 0; i < SlotCount; ++i)
            {
                if (SkillHeld(i))
                    return true;
            }

            return false;
        }

        private bool SkillDown(int slot)
        {
            int button = SkillPadButtons[slot];
            bool pad = button < 0 ? _rightTrigger && !_rightTriggerPrevious : Input.GetKeyDown(PadKey(_pad, button));
            return pad || (!_main && slot < SkillKeys.Length && Input.GetKeyDown(SkillKeys[slot]));
        }

        private bool SkillHeld(int slot)
        {
            int button = SkillPadButtons[slot];
            bool pad = button < 0 ? _rightTrigger : Input.GetKey(PadKey(_pad, button));
            return pad || (!_main && slot < SkillKeys.Length && Input.GetKey(SkillKeys[slot]));
        }

        private int PressedSkillSlot()
        {
            for (int i = 0; i < SlotCount; ++i)
            {
                if (SkillAt(i) != null && SkillDown(i))
                    return i;
            }

            for (int i = 0; i < SlotCount; ++i)
            {
                if (SkillAt(i) != null && SkillHeld(i))
                    return i;
            }

            return -1;
        }

        // The screen is isometric, so a stick direction on the screen is another direction on the map
        private static Vector2 ToIsoDirection(Vector2 screenDirection)
        {
            Vector3 iso = Iso.MapToIso(new Vector3(screenDirection.x, screenDirection.y, 0f));
            return new Vector2(iso.x, iso.y).normalized;
        }

        #endregion

        #region Walking

        private void Walk(Vector2 stick)
        {
            Vector2 direction = stick.normalized;
            bool issue = !_moving ||
                         Time.time >= _nextMoveIssue ||
                         Vector2.Angle(direction, _lastMoveDirection) > MoveReissueAngle;
            if (!issue)
                return;

            _moving = true;
            _lastMoveDirection = direction;
            _nextMoveIssue = Time.time + MoveReissueInterval;
            _unit.GoTo(_unit.iso.pos + ToIsoDirection(direction) * MoveReach);
        }

        private void StopWalking()
        {
            if (!_moving)
                return;
            _moving = false;
            _unit.StopMoving();
        }

        #endregion

        #region Skills

        private void UseSkill(SkillInfo skill)
        {
            Vector2 aim = ToIsoDirection(_aimWorld);
            Vector2 position = _unit.iso.pos;

            // Summons appear in front of the character
            if (!string.IsNullOrEmpty(skill.summon))
            {
                _unit.UseSkill(skill, (Vector3)(position + aim * 4f));
                return;
            }

            // Teleport goes where the stick points
            if (skill.srvDoFunc == 27)
            {
                _unit.UseSkill(skill, (Vector3)(position + aim * 10f));
                return;
            }

            bool melee = skill.range == SkillInfo.Range.Melee || skill.range == SkillInfo.Range.Both;
            var weapon = _unit.equip != null ? _unit.equip.GetWeapon() : null;
            bool bow = weapon != null && weapon.info.type.shoots != null;
            bool closeCombat = melee && !bow;

            var enemy = AIUtils.GetNearestEnemy(_unit, closeCombat ? MeleeTargetRadius : RangedTargetRadius);
            if (enemy != null && _unit.CanUseSkill(skill, enemy))
            {
                // Melee skills walk up to the enemy by themselves
                _unit.UseSkill(skill, enemy);
                return;
            }

            // Nobody around: swing or shoot into the air the way the character looks
            float reach = closeCombat ? 1.5f : 10f;
            _unit.UseSkill(skill, (Vector3)(position + aim * reach));
        }

        #endregion

        #region Items and objects

        private bool Interact()
        {
            Entity best = null;
            float bestScore = float.MaxValue;
            Vector2 position = _unit.iso.pos;

            foreach (var entity in WorldState.instance.Entities)
            {
                bool loot = entity is Loot;
                bool townsfolk = _main && entity is Unit npc && npc != _unit && npc.monStat != null && npc.monStat.npc;
                if (!loot && !townsfolk && !(entity is StaticObject))
                    continue;
                if (!entity.selectable)
                    continue;

                float distance = Vector2.Distance(position, Iso.MapToIso(entity.transform.position));
                if (distance > InteractRadius)
                    continue;

                // Items first, then chests and the like
                float score = distance - (loot ? 4f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = entity;
                }
            }

            if (best == null)
                return false;
            _unit.Use(best);
            return true;
        }

        private void PickUpGoldNearby()
        {
            if (Time.time < _nextAutoPickup)
                return;
            _nextAutoPickup = Time.time + AutoPickupInterval;

            Vector2 position = _unit.iso.pos;
            foreach (var entity in WorldState.instance.Entities)
            {
                var loot = entity as Loot;
                if (loot == null || loot.item == null)
                    continue;
                // Gold always, items only for player 1 while walking (as in D2R: pick up by walking over them)
                if (loot.item.info.code != "gld" && !(_main && _moving))
                    continue;
                float radius = loot.item.info.code == "gld" ? AutoPickupRadius : ItemPickupRadius;
                if (Vector2.Distance(position, Iso.MapToIso(loot.transform.position)) > radius)
                    continue;

                _unit.Use(loot);
                return;
            }
        }

        // The belt: one slot per kind of potion in the inventory, life potions first, then mana, then rejuvenation
        private void DrinkBeltSlot(int slot)
        {
            var inventory = _player.inventory;
            var kinds = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < inventory.entries.Count; ++i)
            {
                var item = inventory.entries[i].item;
                var misc = item.info.misc;
                if (misc == null || !misc.useable || !item.identified)
                    continue;

                int order = PotionScore(misc, true) == 2 ? 0 : PotionScore(misc, false) == 2 ? 1 : PotionScore(misc, true) == 1 ? 2 : -1;
                if (order < 0)
                    continue;
                var kind = new KeyValuePair<int, string>(order, item.info.code);
                if (!kinds.Contains(kind))
                    kinds.Add(kind);
            }

            if (slot >= kinds.Count)
                return;
            kinds.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : string.CompareOrdinal(a.Value, b.Value));

            for (int i = 0; i < inventory.entries.Count; ++i)
            {
                if (inventory.entries[i].item.info.code != kinds[slot].Value)
                    continue;
                Consume(inventory.entries[i]);
                return;
            }
        }

        private void Consume(Diablerie.Engine.Inventory.Entry entry)
        {
            AudioManager.instance.Play(entry.item.useSound);
            _player.inventory.Take(entry.x, entry.y);
            _player.Use(entry.item.info.misc);
            _unit.health = Mathf.Min(_unit.health, _unit.maxHealth);
            _unit.mana = Mathf.Min(_unit.mana, _unit.maxMana);
        }

        private void DrinkPotion(bool health)
        {
            if (health && _unit.health >= _unit.maxHealth)
                return;
            if (!health && _unit.mana >= _unit.maxMana)
                return;

            var inventory = _player.inventory;
            int bestIndex = -1;
            int bestScore = 0;
            for (int i = 0; i < inventory.entries.Count; ++i)
            {
                var item = inventory.entries[i].item;
                var misc = item.info.misc;
                if (misc == null || !misc.useable || !item.identified)
                    continue;

                int score = PotionScore(misc, health);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                return;

            var entry = inventory.entries[bestIndex];
            AudioManager.instance.Play(entry.item.useSound);
            inventory.Take(entry.x, entry.y);
            _player.Use(entry.item.info.misc);
            _unit.health = Mathf.Min(_unit.health, _unit.maxHealth);
            _unit.mana = Mathf.Min(_unit.mana, _unit.maxMana);
        }

        // 2: the potion for the job, 1: a rejuvenation potion does both, 0: not a potion for this
        private static int PotionScore(MiscInfo misc, bool health)
        {
            string stat = health ? "hpregen" : "manarecovery";
            if (misc.useFunction == MiscInfo.UseFunction.Potion && misc.stat1 == stat)
                return 2;
            if (misc.useFunction == MiscInfo.UseFunction.RejuvPotion)
                return 1;
            return 0;
        }

        #endregion
    }
}
