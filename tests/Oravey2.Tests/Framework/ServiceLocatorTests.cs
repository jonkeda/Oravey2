using Oravey2.Core.Framework.Services;
using Xunit;

namespace Oravey2.Tests.Framework;

public class ServiceLocatorTests
{
    public ServiceLocatorTests()
    {
        ServiceLocator.Reset();
    }

    [Fact]
    public void Register_and_Get_returns_service()
    {
        var locator = ServiceLocator.Instance;
        var service = new TestService("hello");

        locator.Register<ITestService>(service);
        var result = locator.Get<ITestService>();

        Assert.Same(service, result);
    }

    [Fact]
    public void Get_unregistered_service_throws()
    {
        var locator = ServiceLocator.Instance;
        Assert.Throws<InvalidOperationException>(() => locator.Get<ITestService>());
    }

    [Fact]
    public void TryGet_returns_false_for_unregistered()
    {
        var locator = ServiceLocator.Instance;
        var found = locator.TryGet<ITestService>(out var service);

        Assert.False(found);
        Assert.Null(service);
    }

    [Fact]
    public void TryGet_returns_true_for_registered()
    {
        var locator = ServiceLocator.Instance;
        locator.Register<ITestService>(new TestService("test"));

        var found = locator.TryGet<ITestService>(out var service);

        Assert.True(found);
        Assert.NotNull(service);
    }

    [Fact]
    public void Register_overwrites_previous_service()
    {
        var locator = ServiceLocator.Instance;
        locator.Register<ITestService>(new TestService("first"));
        locator.Register<ITestService>(new TestService("second"));

        var result = locator.Get<ITestService>();
        Assert.Equal("second", result.Name);
    }

    [Fact]
    public void Reset_clears_all_services()
    {
        ServiceLocator.Instance.Register<ITestService>(new TestService("a"));
        ServiceLocator.Reset();

        Assert.Throws<InvalidOperationException>(() => ServiceLocator.Instance.Get<ITestService>());
    }

    private interface ITestService { string Name { get; } }
    private record TestService(string Name) : ITestService;
}
