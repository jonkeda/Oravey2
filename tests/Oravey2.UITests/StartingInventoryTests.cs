using Xunit;

namespace Oravey2.UITests;

/// <summary>
/// Verifies the player's initial inventory and equipment state immediately after game start.
/// </summary>
public class StartingInventoryTests : IAsyncLifetime
{
    private readonly OraveyTestFixture _fixture = new();

    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public void PlayerHas_PipeWrenchEquipped()
    {
        var equipment = GameQueryHelpers.GetEquipmentState(_fixture.Context);
        Assert.NotNull(equipment.Slots["PrimaryWeapon"]);
        Assert.Equal("pipe_wrench", equipment.Slots["PrimaryWeapon"]!.Id);
    }

    [Fact]
    public void PlayerHas_MedkitsInInventory()
    {
        var inventory = GameQueryHelpers.GetInventoryState(_fixture.Context);
        var medkit = inventory.Items.FirstOrDefault(i => i.Id == "medkit");
        Assert.NotNull(medkit);
        Assert.Equal(2, medkit.Count);
    }

    [Fact]
    public void StartingWeight_IsCorrect()
    {
        var inventory = GameQueryHelpers.GetInventoryState(_fixture.Context);
        // 2 Medkits × 0.5 = 1.0; wrench is equipped, not in bag
        Assert.Equal(1.0, inventory.CurrentWeight, 0.1);
    }

    [Fact]
    public void NotOverweight_AtStart()
    {
        var inventory = GameQueryHelpers.GetInventoryState(_fixture.Context);
        Assert.False(inventory.IsOverweight);
    }

    [Fact]
    public void EquipmentSlots_MostlyEmpty()
    {
        var equipment = GameQueryHelpers.GetEquipmentState(_fixture.Context);
        Assert.NotNull(equipment.Slots["PrimaryWeapon"]);

        var emptySlots = new[] { "Head", "Torso", "Legs", "Feet", "SecondaryWeapon", "Accessory1", "Accessory2" };
        foreach (var slot in emptySlots)
        {
            Assert.Null(equipment.Slots[slot]);
        }
    }
}
