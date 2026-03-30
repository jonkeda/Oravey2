using Oravey2.Core.AI.Group;

namespace Oravey2.Tests.AI;

public class AIGroupCoordinatorTests
{
    private readonly AIGroupCoordinator _coordinator = new();

    [Fact]
    public void EmptyGroup_ReturnsEmpty()
    {
        var result = _coordinator.UpdateGroup([], 0, 0, "target");
        Assert.Empty(result);
    }

    [Fact]
    public void AllDead_ReturnsEmpty()
    {
        var group = new[]
        {
            new GroupMember("a", 0, 0, 0f, false),
            new GroupMember("b", 1, 0, 0f, false),
        };
        var result = _coordinator.UpdateGroup(group, 5, 5, "target");
        Assert.Empty(result);
    }

    [Fact]
    public void RetreatThreshold_HalfDead()
    {
        var group = new[]
        {
            new GroupMember("a", 0, 0, 1f, true),
            new GroupMember("b", 1, 0, 1f, true),
            new GroupMember("c", 2, 0, 0f, false),
            new GroupMember("d", 3, 0, 0f, false),
        };
        var result = _coordinator.UpdateGroup(group, 10, 10, "target");
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(GroupRole.Retreat, a.Role));
    }

    [Fact]
    public void RetreatThreshold_BelowHalf_NoRetreat()
    {
        var group = new[]
        {
            new GroupMember("a", 0, 0, 1f, true),
            new GroupMember("b", 1, 0, 1f, true),
            new GroupMember("c", 2, 0, 1f, true),
            new GroupMember("d", 3, 0, 0f, false), // 1 of 4 = 25%
        };
        var result = _coordinator.UpdateGroup(group, 10, 10, "target");
        Assert.Equal(3, result.Count);
        Assert.True(result.All(a => a.Role != GroupRole.Retreat));
    }

    [Fact]
    public void FocusFire_ThreeOrMore_AssignsTarget()
    {
        var group = new[]
        {
            new GroupMember("a", 0, 0, 1f, true),
            new GroupMember("b", 1, 0, 1f, true),
            new GroupMember("c", 2, 0, 1f, true),
        };
        var result = _coordinator.UpdateGroup(group, 10, 0, "enemy1");
        Assert.All(result, a => Assert.Equal("enemy1", a.FocusTargetId));
    }

    [Fact]
    public void FocusFire_LessThanThree_NoFocus()
    {
        var group = new[]
        {
            new GroupMember("a", 0, 0, 1f, true),
            new GroupMember("b", 1, 0, 1f, true),
        };
        var result = _coordinator.UpdateGroup(group, 10, 0, "enemy1");
        Assert.All(result, a => Assert.Null(a.FocusTargetId));
    }

    [Fact]
    public void FlankAssignment_SpreadMembers()
    {
        // Members spread at wide angle from centroid→target line
        var group = new[]
        {
            new GroupMember("front", 0, 0, 1f, true),    // directly behind target line
            new GroupMember("flank", 0, 15, 1f, true),   // far off to the side
            new GroupMember("flank2", 0, -15, 1f, true),  // far off other side
        };
        // Target at (10, 0) — centroid is at (0, 0)
        var result = _coordinator.UpdateGroup(group, 10, 0, "target");
        Assert.Equal(3, result.Count);
        // At least one should be flanking (the ones at wide angles)
        Assert.True(result.Any(a => a.Role == GroupRole.Flank),
            "At least one spread member should be assigned Flank");
    }

    [Fact]
    public void SingleMember_Attack()
    {
        var group = new[]
        {
            new GroupMember("alone", 0, 0, 1f, true),
        };
        var result = _coordinator.UpdateGroup(group, 10, 0, "target");
        Assert.Single(result);
        Assert.Equal(GroupRole.Attack, result[0].Role);
    }

    [Fact]
    public void AllAlive_NoRetreat()
    {
        var group = new[]
        {
            new GroupMember("a", 0, 0, 1f, true),
            new GroupMember("b", 1, 0, 1f, true),
            new GroupMember("c", 2, 0, 1f, true),
            new GroupMember("d", 3, 0, 1f, true),
        };
        var result = _coordinator.UpdateGroup(group, 10, 0, "target");
        Assert.True(result.All(a => a.Role != GroupRole.Retreat));
    }
}
