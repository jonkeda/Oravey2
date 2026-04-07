using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Models.Meshy;

public sealed record MeshyBalance(
    [property: JsonPropertyName("balance")] int Balance
);
