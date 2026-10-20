using Diablerie.Engine;
using Diablerie.Engine.AI;
using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using Diablerie.Engine.World;
using Diablerie.Game.UI;
using UnityEngine;

namespace Diablerie.Game.Coop
{
    // Drives the second player. There is no mouse for him, so the controls work differently from the main player:
    //
    //   Left stick (or WASD / arrows)   walk
    //   A / X / B / Y  (J / K / L / U)  the four skills of the class. Auto aims at the closest enemy, otherwise
    //                                   fires the way the stick points
    //   RB (E)                          pick up / open the closest item or object
    //   LB (1)                          drink a life potion
    //   Back (2)                        drink a mana potion
    //
    // Gold is picked up by walking over it. Button numbers are the Xbox layout on Windows.
    public class CoopPlayerInput : MonoBehaviour
    {
        private const float DeadZone = 0.3f;
        private const float MoveReach = 8f;
        private const float MoveReissueInterval = 0.15f;
        private const float MoveReissueAngle = 12f;
        private const float AutoPickupRadius = 3.5f;
        private const float AutoPickupInterval = 0.25f;
        private const float InteractRadius = 12f;
        private const float MeleeTargetRadius = 14f;
        private const float RangedTargetRadius = 22f;

        private static readonly KeyCode[] SkillButtons =
            { KeyCode.JoystickButton0, KeyCode.JoystickButton2, KeyCode.JoystickButton1, KeyCode.JoystickButton3 };
        private static readonly KeyCode[] SkillKeys = { KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.U };

        private Player _player;
        private Unit _unit;
        private readonly SkillInfo[] _skills = new SkillInfo[4];

        private bool _moving;
        private float _nextMoveIssue;
        private Vector2 _lastMoveDirection;
        private Vector2 _aimWorld = new Vector2(0f, -1f); // the way the character looks, in screen directions
        private float _nextAutoPickup;
        private bool _axesMissing;

        public void Init(Player player, string[] loadout)
        {
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

            // WASD and arrows are also the keys of the console prompt
            if (CommandPrompt.instance != null && CommandPrompt.instance.visible)
            {
                StopWalking();
                return;
            }

            Vector2 stick = ReadStick();
            if (stick != Vector2.zero)
                _aimWorld = stick.normalized;

            if (Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.Alpha1))
                DrinkPotion(health: true);
            if (Input.GetKeyDown(KeyCode.JoystickButton6) || Input.GetKeyDown(KeyCode.Alpha2))
                DrinkPotion(health: false);

            int slot = PressedSkillSlot();
            if (slot >= 0)
            {
                StopWalking();
                UseSkill(_skills[slot]);
                return;
            }

            if (Input.GetKeyDown(KeyCode.JoystickButton5) || Input.GetKeyDown(KeyCode.E))
                Interact();

            if (CoopManager.Instance != null)
                stick = CoopManager.Instance.ClampMovementToLeash(stick);

            if (stick.magnitude >= DeadZone)
                Walk(stick);
            else
                StopWalking();

            PickUpGoldNearby();
        }

        #region Input

        private Vector2 ReadStick()
        {
            if (_axesMissing)
                return Vector2.zero;

            try
            {
                // Unity's default axes: left stick of any gamepad, plus WASD and the arrow keys
                var value = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                if (value.magnitude < DeadZone)
                    return Vector2.zero;
                return value.normalized * Mathf.Min(1f, value.magnitude);
            }
            catch (System.ArgumentException)
            {
                _axesMissing = true;
                Debug.LogWarning("Co-op: the Horizontal and Vertical input axes are missing in the Input Manager");
                return Vector2.zero;
            }
        }

        private int PressedSkillSlot()
        {
            for (int i = 0; i < _skills.Length; ++i)
            {
                if (_skills[i] != null && (Input.GetKeyDown(SkillButtons[i]) || Input.GetKeyDown(SkillKeys[i])))
                    return i;
            }

            for (int i = 0; i < _skills.Length; ++i)
            {
                if (_skills[i] != null && (Input.GetKey(SkillButtons[i]) || Input.GetKey(SkillKeys[i])))
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

        private void Interact()
        {
            Entity best = null;
            float bestScore = float.MaxValue;
            Vector2 position = _unit.iso.pos;

            foreach (var entity in WorldState.instance.Entities)
            {
                bool loot = entity is Loot;
                if (!loot && !(entity is StaticObject))
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

            if (best != null)
                _unit.Use(best);
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
                if (loot == null || loot.item == null || loot.item.info.code != "gld")
                    continue;
                if (Vector2.Distance(position, Iso.MapToIso(loot.transform.position)) > AutoPickupRadius)
                    continue;

                _unit.Use(loot);
                return;
            }
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
