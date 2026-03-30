using Brinell.Stride.Communication;
using Brinell.Stride.Interfaces;
using System.Text.Json;

namespace Oravey2.UITests;

/// <summary>
/// Helpers for querying Oravey2 game state through the automation pipe.
/// </summary>
public static class GameQueryHelpers
{
    public record Position(double X, double Y, double Z);

    public record CameraState(double X, double Y, double Z, double Yaw, double Pitch, double Zoom);

    public static Position GetPlayerPosition(IStrideTestContext context)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("GetPlayerPosition"));
        if (!response.Success)
            throw new InvalidOperationException($"GetPlayerPosition failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return new Position(
            je.GetProperty("x").GetDouble(),
            je.GetProperty("y").GetDouble(),
            je.GetProperty("z").GetDouble());
    }

    public static CameraState GetCameraState(IStrideTestContext context)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("GetCameraState"));
        if (!response.Success)
            throw new InvalidOperationException($"GetCameraState failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return new CameraState(
            je.GetProperty("x").GetDouble(),
            je.GetProperty("y").GetDouble(),
            je.GetProperty("z").GetDouble(),
            je.GetProperty("yaw").GetDouble(),
            je.GetProperty("pitch").GetDouble(),
            je.GetProperty("zoom").GetDouble());
    }

    public static string GetGameState(IStrideTestContext context)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("GetGameState"));
        if (!response.Success)
            throw new InvalidOperationException($"GetGameState failed: {response.Error}");

        return response.Result?.ToString() ?? "";
    }

    public record SceneDiagnostics(
        int TotalEntities,
        int ModelEntityCount,
        List<ModelEntityInfo> ModelEntitiesSample,
        CameraDiagnostics? Camera);

    public record ModelEntityInfo(string Name, double X, double Y, double Z, int MeshCount, int MaterialCount);

    public record CameraDiagnostics(
        Position Position,
        Position Forward,
        string Projection,
        double OrthoSize,
        double NearClip,
        double FarClip,
        string SlotId);

    public static SceneDiagnostics GetSceneDiagnostics(IStrideTestContext context)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("GetSceneDiagnostics"));
        if (!response.Success)
            throw new InvalidOperationException($"GetSceneDiagnostics failed: {response.Error}");

        var je = (JsonElement)response.Result!;

        var modelEntities = new List<ModelEntityInfo>();
        if (je.TryGetProperty("modelEntitiesSample", out var sample))
        {
            foreach (var item in sample.EnumerateArray())
            {
                modelEntities.Add(new ModelEntityInfo(
                    item.GetProperty("name").GetString() ?? "",
                    item.GetProperty("x").GetDouble(),
                    item.GetProperty("y").GetDouble(),
                    item.GetProperty("z").GetDouble(),
                    item.GetProperty("meshCount").GetInt32(),
                    item.GetProperty("materialCount").GetInt32()));
            }
        }

        CameraDiagnostics? camDiag = null;
        if (je.TryGetProperty("camera", out var cam) && cam.ValueKind != JsonValueKind.Null)
        {
            var pos = cam.GetProperty("position");
            var fwd = cam.GetProperty("forward");
            camDiag = new CameraDiagnostics(
                new Position(pos.GetProperty("x").GetDouble(), pos.GetProperty("y").GetDouble(), pos.GetProperty("z").GetDouble()),
                new Position(fwd.GetProperty("x").GetDouble(), fwd.GetProperty("y").GetDouble(), fwd.GetProperty("z").GetDouble()),
                cam.GetProperty("projection").GetString() ?? "",
                cam.GetProperty("orthoSize").GetDouble(),
                cam.GetProperty("nearClip").GetDouble(),
                cam.GetProperty("farClip").GetDouble(),
                cam.GetProperty("slotId").GetString() ?? "");
        }

        return new SceneDiagnostics(
            je.GetProperty("totalEntities").GetInt32(),
            je.GetProperty("modelEntityCount").GetInt32(),
            modelEntities,
            camDiag);
    }

    public static string TakeScreenshot(IStrideTestContext context)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("TakeScreenshot"));
        if (!response.Success)
            throw new InvalidOperationException($"TakeScreenshot failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return je.GetProperty("path").GetString() ?? "";
    }

    public record ScreenPosition(
        double ScreenX, double ScreenY,
        double NormX, double NormY,
        bool OnScreen,
        int ScreenWidth, int ScreenHeight);

    public static ScreenPosition WorldToScreen(IStrideTestContext context, double x, double y, double z)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("WorldToScreen", x, y, z));
        if (!response.Success)
            throw new InvalidOperationException($"WorldToScreen failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return new ScreenPosition(
            je.GetProperty("screenX").GetDouble(),
            je.GetProperty("screenY").GetDouble(),
            je.GetProperty("normX").GetDouble(),
            je.GetProperty("normY").GetDouble(),
            je.GetProperty("onScreen").GetBoolean(),
            je.GetProperty("screenWidth").GetInt32(),
            je.GetProperty("screenHeight").GetInt32());
    }

    public record PlayerScreenInfo(
        double WorldX, double WorldY, double WorldZ,
        double ScreenX, double ScreenY,
        double NormX, double NormY,
        bool OnScreen,
        int ScreenWidth, int ScreenHeight);

    public static PlayerScreenInfo GetPlayerScreenPosition(IStrideTestContext context)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("GetPlayerScreenPosition"));
        if (!response.Success)
            throw new InvalidOperationException($"GetPlayerScreenPosition failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return new PlayerScreenInfo(
            je.GetProperty("worldX").GetDouble(),
            je.GetProperty("worldY").GetDouble(),
            je.GetProperty("worldZ").GetDouble(),
            je.GetProperty("screenX").GetDouble(),
            je.GetProperty("screenY").GetDouble(),
            je.GetProperty("normX").GetDouble(),
            je.GetProperty("normY").GetDouble(),
            je.GetProperty("onScreen").GetBoolean(),
            je.GetProperty("screenWidth").GetInt32(),
            je.GetProperty("screenHeight").GetInt32());
    }

    public record TileInfo(int TileX, int TileZ, string TileType, int TileTypeId);

    public static TileInfo GetTileAtWorldPos(IStrideTestContext context, double worldX, double worldZ)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("GetTileAtWorldPos", worldX, worldZ));
        if (!response.Success)
            throw new InvalidOperationException($"GetTileAtWorldPos failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return new TileInfo(
            je.GetProperty("tileX").GetInt32(),
            je.GetProperty("tileZ").GetInt32(),
            je.GetProperty("tileType").GetString() ?? "",
            je.GetProperty("tileTypeId").GetInt32());
    }

    public static Position GetEntityPosition(IStrideTestContext context, string entityName)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("GetEntityPosition", entityName));
        if (!response.Success)
            throw new InvalidOperationException($"GetEntityPosition({entityName}) failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return new Position(
            je.GetProperty("x").GetDouble(),
            je.GetProperty("y").GetDouble(),
            je.GetProperty("z").GetDouble());
    }

    // --- Combat helpers ---

    public record EnemyState(
        string Id, int Hp, int MaxHp, int Ap, int MaxAp,
        bool IsAlive, double X, double Y, double Z);

    public record CombatState(
        bool InCombat, int EnemyCount,
        List<EnemyState> Enemies,
        int PlayerHp, int PlayerMaxHp,
        int PlayerAp, int PlayerMaxAp);

    public static CombatState GetCombatState(IStrideTestContext context)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("GetCombatState"));
        if (!response.Success)
            throw new InvalidOperationException($"GetCombatState failed: {response.Error}");

        var je = (JsonElement)response.Result!;

        var enemies = new List<EnemyState>();
        if (je.TryGetProperty("enemies", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                enemies.Add(new EnemyState(
                    item.GetProperty("id").GetString() ?? "",
                    item.GetProperty("hp").GetInt32(),
                    item.GetProperty("maxHp").GetInt32(),
                    item.GetProperty("ap").GetInt32(),
                    item.GetProperty("maxAp").GetInt32(),
                    item.GetProperty("isAlive").GetBoolean(),
                    item.GetProperty("x").GetDouble(),
                    item.GetProperty("y").GetDouble(),
                    item.GetProperty("z").GetDouble()));
            }
        }

        return new CombatState(
            je.GetProperty("inCombat").GetBoolean(),
            je.GetProperty("enemyCount").GetInt32(),
            enemies,
            je.GetProperty("playerHp").GetInt32(),
            je.GetProperty("playerMaxHp").GetInt32(),
            je.GetProperty("playerAp").GetInt32(),
            je.GetProperty("playerMaxAp").GetInt32());
    }

    public static Position TeleportPlayer(IStrideTestContext context, double x, double y, double z)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("TeleportPlayer", x, y, z));
        if (!response.Success)
            throw new InvalidOperationException($"TeleportPlayer failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return new Position(
            je.GetProperty("x").GetDouble(),
            je.GetProperty("y").GetDouble(),
            je.GetProperty("z").GetDouble());
    }

    public static (bool Killed, int RemainingAlive) KillEnemy(IStrideTestContext context, string enemyId)
    {
        var response = context.SendCommand(AutomationCommand.GameQuery("KillEnemy", enemyId));
        if (!response.Success)
            throw new InvalidOperationException($"KillEnemy({enemyId}) failed: {response.Error}");

        var je = (JsonElement)response.Result!;
        return (je.GetProperty("killed").GetBoolean(),
                je.GetProperty("remainingAlive").GetInt32());
    }
}
