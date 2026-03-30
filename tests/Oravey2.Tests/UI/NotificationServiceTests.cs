using Oravey2.Core.UI;

namespace Oravey2.Tests.UI;

public class NotificationServiceTests
{
    [Fact]
    public void Add_AppearsInActive()
    {
        var svc = new NotificationService();
        svc.Add("hello");
        Assert.Single(svc.GetActive());
    }

    [Fact]
    public void Add_EmptyMessage_Ignored()
    {
        var svc = new NotificationService();
        svc.Add("");
        Assert.Empty(svc.GetActive());
    }

    [Fact]
    public void Update_ExpiresNotification()
    {
        var svc = new NotificationService();
        svc.Add("x", 1.0f);
        svc.Update(2.0f);
        Assert.Empty(svc.GetActive());
    }

    [Fact]
    public void Update_DecrementsTimeRemaining()
    {
        var svc = new NotificationService();
        svc.Add("x", 5.0f);
        svc.Update(2.0f);
        var active = svc.GetActive();
        Assert.Single(active);
        Assert.Equal(3.0f, active[0].TimeRemaining, 0.01f);
    }

    [Fact]
    public void MaxVisible_ExcessQueued()
    {
        var svc = new NotificationService(maxVisible: 2);
        svc.Add("a");
        svc.Add("b");
        svc.Add("c");
        Assert.Equal(2, svc.GetActive().Count);
        Assert.Equal(1, svc.PendingCount);
    }

    [Fact]
    public void Update_PromotesPending()
    {
        var svc = new NotificationService(maxVisible: 1);
        svc.Add("first", 1.0f);
        svc.Add("second", 3.0f);

        Assert.Equal(1, svc.PendingCount);

        // Expire the first
        svc.Update(2.0f);
        Assert.Single(svc.GetActive());
        Assert.Equal("second", svc.GetActive()[0].Message);
        Assert.Equal(0, svc.PendingCount);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var svc = new NotificationService(maxVisible: 1);
        svc.Add("a");
        svc.Add("b");
        svc.Add("c");
        svc.Clear();
        Assert.Empty(svc.GetActive());
        Assert.Equal(0, svc.PendingCount);
    }

    [Fact]
    public void Add_NegativeDuration_DefaultsTo3()
    {
        var svc = new NotificationService();
        svc.Add("x", -1f);
        Assert.Equal(3.0f, svc.GetActive()[0].DurationSeconds, 0.01f);
    }

    [Fact]
    public void Update_ZeroDelta_NoChange()
    {
        var svc = new NotificationService();
        svc.Add("x", 1.0f);
        svc.Update(0f);
        Assert.Equal(1.0f, svc.GetActive()[0].TimeRemaining, 0.01f);
    }
}
