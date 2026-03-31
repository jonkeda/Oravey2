using Microsoft.Extensions.Logging;
using Oravey2.Core.Input;

namespace Oravey2.Core.Bootstrap;

/// <summary>
/// Platform-provided configuration for game startup.
/// </summary>
public sealed class BootstrapConfig
{
    /// <summary>Platform input provider (keyboard, touch, gamepad).</summary>
    public required IInputProvider InputProvider { get; init; }

    /// <summary>Logger factory for platform-specific logging.</summary>
    public required ILoggerFactory LoggerFactory { get; init; }

    /// <summary>Whether to enable the automation server (for UI tests).</summary>
    public bool AutomationEnabled { get; init; }

    /// <summary>CLI arguments (Windows) or intent extras (Android).</summary>
    public string[] Args { get; init; } = [];
}
