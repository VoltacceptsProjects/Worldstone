using Diablerie.Engine;
using NUnit.Framework;

public class ItemStatsTest
{
    private static StatTotals Totals(params object[] pairs)
    {
        var totals = new StatTotals();
        for (int i = 0; i < pairs.Length; i += 2)
            totals.Add((string)pairs[i], (int)pairs[i + 1]);
        return totals;
    }

    [Test]
    public void NoItemsKeepsTheBaseValues()
    {
        var totals = Totals();
        Assert.AreEqual(1000, StatFormulas.MaxLife(1000, totals, 3));
        Assert.AreEqual(100, StatFormulas.MaxMana(100, totals, 2f));
        Assert.AreEqual(7f, StatFormulas.Speed(7f, totals));
    }

    [Test]
    public void FlatLifeRaisesMaxLife()
    {
        Assert.AreEqual(1040, StatFormulas.MaxLife(1000, Totals("maxhp", 40), 3));
    }

    [Test]
    public void VitalityGivesLifePerPointOfTheClass()
    {
        Assert.AreEqual(1040, StatFormulas.MaxLife(1000, Totals("vitality", 10), 4));
    }

    [Test]
    public void PercentLifeComesAfterTheFlatBonuses()
    {
        // (1000 + 100) * 120 / 100
        Assert.AreEqual(1320, StatFormulas.MaxLife(1000, Totals("maxhp", 100, "item_maxhp_percent", 20), 3));
    }

    [Test]
    public void ManaFromEnergyAndPercent()
    {
        // (100 + 20 + 10 * 1.5) * 110 / 100
        Assert.AreEqual(148, StatFormulas.MaxMana(100, Totals("maxmana", 20, "energy", 10, "item_maxmana_percent", 10), 1.5f));
    }

    [Test]
    public void FasterRunWalkScalesSpeed()
    {
        Assert.AreEqual(18f, StatFormulas.Speed(15f, Totals("item_fastermovevelocity", 20)), 0.001f);
    }

    [Test]
    public void LifeNeverDropsBelowOne()
    {
        Assert.AreEqual(1, StatFormulas.MaxLife(1000, Totals("maxhp", -5000), 3));
    }
}
