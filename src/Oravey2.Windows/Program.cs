using Brinell.Automation;
using Microsoft.Extensions.Logging;
using Oravey2.Core.Bootstrap;
using Oravey2.Core.Input;
using Stride.CommunityToolkit.Engine;
using Stride.Engine;

using var game = new Game();

game.Run(start: (Scene rootScene) =>
{
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .SetMinimumLevel(LogLevel.Debug)
            .AddSimpleConsole(options =>
            {
                options.TimestampFormat = "HH:mm:ss.fff ";
                options.SingleLine = true;
            });
    });

    var config = new BootstrapConfig
    {
        InputProvider = new KeyboardMouseInputProvider(),
        LoggerFactory = loggerFactory,
        AutomationEnabled = StrideAutomationExtensions.IsAutomationEnabled(),
        Args = Environment.GetCommandLineArgs(),
    };

    new GameBootstrapper().Start(rootScene, game, config);
});
