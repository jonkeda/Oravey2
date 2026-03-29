using Brinell.Stride.Controls;
using Brinell.Stride.Interfaces;
using Brinell.Stride.Pages;

namespace Oravey2.UITests.Pages;

/// <summary>
/// Page object for the main game world (exploring state).
/// Currently has no Stride UI elements — this is the 3D scene with
/// player, tile map, and camera. Will gain UI controls as HUD/menus are added.
/// </summary>
public class GameWorldPage : PageObjectBase<GameWorldPage>
{
    public override string Name => "GameWorld";

    // No root automation ID — the 3D scene has no UIElement root yet
    public override string AutomationId => string.Empty;

    public GameWorldPage(IStrideTestContext context) : base(context)
    {
    }

    /// <summary>
    /// The game world is "loaded" once the game is ready and not busy.
    /// </summary>
    public override bool IsLoaded(int? timeoutMs = null)
    {
        return StrideContext.IsGameReady;
    }

    public override bool IsReady(int? timeoutMs = null)
    {
        return IsLoaded(timeoutMs) && !StrideContext.IsGameBusy();
    }
}
