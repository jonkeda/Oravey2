namespace Oravey2.Core.Framework.Services;

public sealed class ServiceLocator : IServiceLocator
{
    private static ServiceLocator? _instance;
    public static ServiceLocator Instance => _instance ??= new ServiceLocator();

    private readonly Dictionary<Type, object> _services = new();

    public void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    public T Get<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
            return (T)service;

        throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
    }

    public bool TryGet<T>(out T? service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj))
        {
            service = (T)obj;
            return true;
        }
        service = null;
        return false;
    }

    /// <summary>
    /// Reset for testing purposes.
    /// </summary>
    public static void Reset()
    {
        _instance = new ServiceLocator();
    }
}
