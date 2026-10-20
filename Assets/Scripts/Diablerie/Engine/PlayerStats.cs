using Diablerie.Engine.Datasheets;
using Diablerie.Engine.Entities;
using UnityEngine;

namespace Diablerie.Engine
{
    // Applies the stats of the worn items to the player: max life and mana, movement speed, weapon damage,
    // life and mana on kill and per hit, and extra gold. Totals are recalculated whenever the equipment or
    // the level changes.
    public class PlayerStats : MonoBehaviour
    {
        // What the character has without any items. This is what Player used to hard code.
        public const int BaseLife = 1000;
        public const int BaseMana = 100;
        public const float BaseWalkSpeed = 7f;
        public const float BaseRunSpeed = 15f;

        public Player player;
        public StatTotals totals = new StatTotals();

        private Unit _unit;
        private Equipment _equip;
        private CharStat _charStat;

        public static PlayerStats Create(Player player)
        {
            var stats = player.gameObject.AddComponent<PlayerStats>();
            stats.Init(player);
            return stats;
        }

        private void Init(Player owner)
        {
            player = owner;
            _unit = owner.unit;
            _equip = owner.equip;
            _charStat = owner.charStat;
            _equip.OnUpdate += Recalculate;
            _charStat.LeveledUp += Recalculate;
            Events.UnitDied += OnUnitDied;
            Recalculate();
        }

        private void OnDestroy()
        {
            if (_equip != null)
                _equip.OnUpdate -= Recalculate;
            if (_charStat != null)
                _charStat.LeveledUp -= Recalculate;
            Events.UnitDied -= OnUnitDied;
        }

        public int Get(string code)
        {
            return totals.Get(code);
        }

        public void Recalculate()
        {
            if (_unit == null)
                return;

            int level = _charStat != null ? _charStat.level : 1;
            totals = ItemStats.Collect(_equip, level);

            int maxLife = StatFormulas.MaxLife(BaseLife, totals, LifePerVitality(player.charStatInfo));
            int maxMana = StatFormulas.MaxMana(BaseMana, totals, ManaPerEnergy(player.charStatInfo));

            // Wearing something does not heal, taking it off can only cut what is above the new maximum
            _unit.maxHealth = maxLife;
            _unit.health = Mathf.Min(_unit.health, maxLife);
            _unit.maxMana = maxMana;
            _unit.mana = Mathf.Min(_unit.mana, maxMana);

            _unit.walkSpeed = StatFormulas.Speed(BaseWalkSpeed, totals);
            _unit.runSpeed = StatFormulas.Speed(BaseRunSpeed, totals);
        }

        // Life per point of vitality: charstats.txt has it as a whole number for every class
        private static int LifePerVitality(CharStatsInfo info)
        {
            if (info != null && info.lifePerVitality > 0)
                return info.lifePerVitality;
            return 2;
        }

        // Mana per point of energy. charstats.txt keeps fractions (1.5) that the integer column cannot hold,
        // so the values of the game are used.
        private static float ManaPerEnergy(CharStatsInfo info)
        {
            if (info == null)
                return 2f;
            switch (info.className)
            {
                case "Barbarian": return 1f;
                case "Amazon":
                case "Paladin":
                case "Assassin": return 1.5f;
                default: return 2f;
            }
        }

        // Melee and bow damage: weapon damage plus the flat damage of the items, then the enhanced damage percent
        public int ModifyWeaponDamage(int weaponDamage)
        {
            int min = totals.Get("mindamage");
            int max = Mathf.Max(min, totals.Get("maxdamage"));
            long damage = weaponDamage + Random.Range(min, max + 1);
            damage = damage * (100 + totals.Get("item_maxdamage_percent")) / 100;
            return (int)System.Math.Max(1L, System.Math.Min(damage, int.MaxValue));
        }

        // Life and mana stolen by a melee hit, a percent of the damage
        public void OnMeleeHit(int damage)
        {
            int lifePercent = Mathf.Max(totals.Get("lifedrainmindam"), totals.Get("lifedrainmaxdam"));
            int manaPercent = Mathf.Max(totals.Get("manadrainmindam"), totals.Get("manadrainmaxdam"));
            if (lifePercent > 0)
                _unit.health = Mathf.Min(_unit.maxHealth, _unit.health + damage * lifePercent / 100);
            if (manaPercent > 0)
                _unit.mana = Mathf.Min(_unit.maxMana, _unit.mana + damage * manaPercent / 100);
        }

        public int GoldWithBonus(int gold)
        {
            return (int)System.Math.Min((long)gold * (100 + Mathf.Max(-90, totals.Get("item_goldbonus"))) / 100, int.MaxValue);
        }

        private void OnUnitDied(Unit unit, Unit killer)
        {
            if (killer != _unit || unit == _unit)
                return;
            int life = totals.Get("item_healafterkill");
            int mana = totals.Get("item_manaafterkill");
            if (life > 0)
                _unit.health = Mathf.Min(_unit.maxHealth, _unit.health + life);
            if (mana > 0)
                _unit.mana = Mathf.Min(_unit.maxMana, _unit.mana + mana);
        }

        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Life " + _unit.health + "/" + _unit.maxHealth + ", Mana " + _unit.mana + "/" + _unit.maxMana);
            sb.Append(", walk " + _unit.walkSpeed.ToString("0.0") + ", run " + _unit.runSpeed.ToString("0.0"));
            foreach (var entry in totals.All)
                sb.Append("\n  " + entry.Key + " = " + entry.Value);
            return sb.ToString();
        }
    }
}
