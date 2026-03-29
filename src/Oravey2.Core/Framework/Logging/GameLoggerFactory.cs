using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Oravey2.Core.Framework.Logging;

public sealed class GameLoggerFactory
{
    private readonly ILoggerFactory _factory;

    public GameLoggerFactory(ILoggerFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates a logger for the given type.
    /// </summary>
    public ILogger<T> CreateLogger<T>() => _factory.CreateLogger<T>();

    /// <summary>
    /// Creates a logger with the given category name.
    /// </summary>
    public ILogger CreateLogger(string categoryName) => _factory.CreateLogger(categoryName);

    /// <summary>
    /// A no-op factory for testing or when logging is not configured.
    /// </summary>
    public static GameLoggerFactory Null { get; } = new(NullLoggerFactory.Instance);
}
