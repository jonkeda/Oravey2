using Oravey2.Core.Combat;

namespace Oravey2.Tests.Combat;

public class DamageResolverTests
{
    private static AttackContext MakeContext(
        float accuracy = 0.90f, int damage = 20, float range = 15f,
        float critMult = 2.0f, int skill = 50, int luck = 5,
        int armorDR = 0, CoverLevel cover = CoverLevel.None, float distance = 1f)
        => new(accuracy, damage, range, critMult, skill, luck, armorDR, cover, distance);

    [Fact]
    public void Resolve_GuaranteedHit_ReturnsDamage()
    {
        // Seed 42: first NextDouble() ≈ 0.745 — need high hit chance to guarantee hit
        var resolver = new DamageResolver(new Random(42));
        var ctx = MakeContext(accuracy: 0.90f, skill: 80); // ~1.26 clamped to 0.95
        var result = resolver.Resolve(ctx);
        Assert.True(result.Hit);
        Assert.True(result.Damage > 0);
    }

    [Fact]
    public void Resolve_GuaranteedMiss_ReturnsZero()
    {
        // Very low accuracy → almost certain miss
        var resolver = new DamageResolver(new Random(42));
        var ctx = MakeContext(accuracy: 0.01f, skill: 0, distance: 100f, range: 10f);
        var result = resolver.Resolve(ctx);
        Assert.False(result.Hit);
        Assert.Equal(0, result.Damage);
    }

    [Fact]
    public void Resolve_Headshot_HigherDamage()
    {
        // We need a seed where hit roll < hitChance, and location roll is in 0.40–0.50 range
        // Test by running many seeds and finding a headshot
        DamageResult? headshot = null;
        DamageResult? torso = null;

        for (int seed = 0; seed < 1000; seed++)
        {
            var resolver = new DamageResolver(new Random(seed));
            var ctx = MakeContext(accuracy: 0.90f, skill: 50, luck: 0);
            var result = resolver.Resolve(ctx);
            if (result.Hit && result.Location == HitLocation.Head)
                headshot = result;
            if (result.Hit && result.Location == HitLocation.Torso)
                torso = result;
            if (headshot != null && torso != null) break;
        }

        Assert.NotNull(headshot);
        Assert.NotNull(torso);
        Assert.True(headshot.Damage > torso.Damage);
    }

    [Fact]
    public void Resolve_CriticalHit_AppliesMultiplier()
    {
        // High luck → very high crit chance
        DamageResult? crit = null;
        DamageResult? noCrit = null;

        for (int seed = 0; seed < 1000; seed++)
        {
            var resolver = new DamageResolver(new Random(seed));
            var ctx = MakeContext(accuracy: 0.90f, skill: 50, luck: 99);
            var result = resolver.Resolve(ctx);
            if (result.Hit && result.Critical && result.Location == HitLocation.Torso)
                crit = result;

            var resolver2 = new DamageResolver(new Random(seed));
            var ctx2 = MakeContext(accuracy: 0.90f, skill: 50, luck: 0);
            var result2 = resolver2.Resolve(ctx2);
            if (result2.Hit && !result2.Critical && result2.Location == HitLocation.Torso)
                noCrit = result2;

            if (crit != null && noCrit != null) break;
        }

        Assert.NotNull(crit);
        Assert.NotNull(noCrit);
        Assert.True(crit.Damage > noCrit.Damage);
    }

    [Fact]
    public void Resolve_NoCrit_LuckZero()
    {
        // Luck=0 → crit chance = 0 → never critical
        for (int seed = 0; seed < 100; seed++)
        {
            var resolver = new DamageResolver(new Random(seed));
            var ctx = MakeContext(luck: 0);
            var result = resolver.Resolve(ctx);
            Assert.False(result.Critical);
        }
    }

    [Fact]
    public void Resolve_ArmorReducesDamage()
    {
        DamageResult? armored = null;
        DamageResult? unarmored = null;

        for (int seed = 0; seed < 1000; seed++)
        {
            var resolver1 = new DamageResolver(new Random(seed));
            var ctx1 = MakeContext(armorDR: 10, luck: 0);
            var r1 = resolver1.Resolve(ctx1);

            var resolver2 = new DamageResolver(new Random(seed));
            var ctx2 = MakeContext(armorDR: 0, luck: 0);
            var r2 = resolver2.Resolve(ctx2);

            if (r1.Hit && r2.Hit && r1.Location == r2.Location)
            {
                armored = r1;
                unarmored = r2;
                break;
            }
        }

        Assert.NotNull(armored);
        Assert.NotNull(unarmored);
        Assert.True(armored.Damage < unarmored.Damage);
    }

    [Fact]
    public void Resolve_MinimumDamageOne()
    {
        // Very high armor, very low weapon → still at least 1 on hit
        for (int seed = 0; seed < 1000; seed++)
        {
            var resolver = new DamageResolver(new Random(seed));
            var ctx = MakeContext(damage: 1, skill: 0, armorDR: 100, luck: 0);
            var result = resolver.Resolve(ctx);
            if (result.Hit)
            {
                Assert.True(result.Damage >= 1);
                return;
            }
        }

        // If we never hit in 1000 seeds, the test is still valid
        // (accuracy 0.90 at close range should hit often)
        Assert.Fail("Expected at least one hit in 1000 seeds");
    }

    [Fact]
    public void Resolve_FullCover_LowersHitChance()
    {
        int hitsNoCover = 0;
        int hitsFullCover = 0;
        const int trials = 500;

        for (int seed = 0; seed < trials; seed++)
        {
            var r1 = new DamageResolver(new Random(seed));
            if (r1.Resolve(MakeContext(cover: CoverLevel.None)).Hit) hitsNoCover++;

            var r2 = new DamageResolver(new Random(seed));
            if (r2.Resolve(MakeContext(cover: CoverLevel.Full)).Hit) hitsFullCover++;
        }

        Assert.True(hitsFullCover < hitsNoCover);
    }
}
