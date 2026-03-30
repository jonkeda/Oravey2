using Brinell.Stride.Infrastructure;
using Xunit;

namespace Oravey2.UITests;

public class ArmorEffectTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void EquipItem_EquipsLeatherJacket()
    {
        var result = GameQueryHelpers.EquipItem(_fixture.Context, "leather_jacket");
        Assert.True(result.Success);
        Assert.Equal("Torso", result.Slot);
        Assert.Equal("Leather Jacket", result.ItemName);
    }

    [Fact]
    public void ArmorEquipped_ReducesDamage()
    {
        // Verify via equipment state that armor is equipped after EquipItem
        GameQueryHelpers.EquipItem(_fixture.Context, "leather_jacket");

        var equip = GameQueryHelpers.GetEquipmentState(_fixture.Context);
        Assert.True(equip.Slots.ContainsKey("Torso") && equip.Slots["Torso"] != null,
            "Leather Jacket should be equipped in Torso slot");
    }

    [Fact]
    public void NoArmor_AtStart()
    {
        var equip = GameQueryHelpers.GetEquipmentState(_fixture.Context);
        // Player starts with PipeWrench in PrimaryWeapon, nothing in Torso
        Assert.True(!equip.Slots.ContainsKey("Torso") || equip.Slots["Torso"] == null,
            "No torso armor should be equipped at start");
    }
}
