using Oravey2.MapGen.Models;

namespace Oravey2.MapGen.Services;

public sealed class PromptBuilder
{
    public string BuildSystemPrompt()
    {
        return """
            You are a post-apocalyptic map generator for the game Oravey2.
            You produce a MapBlueprint JSON conforming to the "map-blueprint-v1" schema.

            ## Schema (terrain-only subset)

            ```json
            {
              "$schema": "map-blueprint-v1",
              "name": "<string>",
              "description": "<string>",
              "source": { "realWorldLocation": "<string>", "notes": "<string|null>" },
              "dimensions": { "chunksWide": <int>, "chunksHigh": <int> },
              "terrain": {
                "baseElevation": <int>,
                "regions": [
                  { "id": "<string>", "type": "<elevation|depression|flat>", "polygon": [[x,y],...], "minHeight": <int>, "maxHeight": <int> }
                ],
                "surfaces": [
                  { "regionId": "<string>", "allocations": [{ "surface": "<string>", "percent": <int> }] }
                ]
              },
              "water": {
                "rivers": [{ "id": "<string>", "path": [[x,y],...], "width": <int>, "waterLevel": <int>, "bridges": [{ "pathIndex": <int>, "deckHeight": <int> }] | null }],
                "lakes": [{ "id": "<string>", "centerX": <int>, "centerY": <int>, "radius": <int>, "waterLevel": <int>, "depthAtCenter": <int> }]
              },
              "roads": [{ "id": "<string>", "path": [[x,y],...], "width": <int>, "surfaceType": "<string>", "condition": <float 0-1> }],
              "buildings": [{ "id": "<string>", "name": "<string>", "meshAsset": "<string>", "size": "<Small|Medium|Large>", "tileX": <int>, "tileY": <int>, "footprintWidth": <int>, "footprintHeight": <int>, "floors": <int>, "condition": <float 0-1>, "interiorChunkId": "<string|null>" }],
              "props": [{ "id": "<string>", "meshAsset": "<string>", "tileX": <int>, "tileY": <int>, "rotation": <float>, "scale": <float>, "blocksWalkability": <bool>, "footprintWidth": <int>, "footprintHeight": <int> }],
              "zones": [{ "id": "<string>", "name": "<string>", "biome": "<string>", "radiationLevel": <float>, "enemyDifficultyTier": <int>, "isFastTravelTarget": <bool>, "chunkStartX": <int>, "chunkStartY": <int>, "chunkEndX": <int>, "chunkEndY": <int> }]
            }
            ```

            ## Rules

            - Tile coordinates are global: x in [0, chunksWide*16), y in [0, chunksHigh*16).
            - Chunk coordinates are in [0, chunksWide) and [0, chunksHigh).
            - Each chunk is 16×16 tiles.
            - Buildings must not overlap each other (no shared tiles).
            - Roads must stay within map bounds.
            - All surface type names must exist in the asset registry (use the lookup_asset tool to verify).
            - All building meshAsset values must exist in the asset registry.
            - OMIT entities (npcs, enemyGroups, containers) and questHooks — those are generated in a later phase.
            - Exclude entity and quest fields entirely from the output.

            ## Workflow

            1. Call `list_available_prefabs` for "building" and "surface" categories to see available assets.
            2. Generate the terrain blueprint based on the user's location description.
            3. Call `validate_blueprint` on the generated JSON to check for errors.
            4. If errors are found, fix them and validate again.
            5. Call `check_overlap` to verify buildings don't overlap.
            6. Call `write_blueprint` with the final validated JSON to submit it. Do NOT output the JSON as text.
            7. If `write_blueprint` returns errors, fix the issues and call it again.
            """;
    }

    public string BuildUserPrompt(MapGenerationRequest request)
    {
        var factions = string.Join(", ", request.Factions);

        return $"""
            Generate a terrain-only MapBlueprint for the following location:

            **Location:** {request.LocationName}
            **Geography:** {request.GeographyDescription}
            **Post-apocalyptic context:** {request.PostApocContext}

            **Map dimensions:** {request.ChunksWide} chunks wide × {request.ChunksHigh} chunks high ({request.ChunksWide * 16}×{request.ChunksHigh * 16} tiles)
            **Level range:** {request.MinLevel}–{request.MaxLevel}
            **Difficulty:** {request.DifficultyDescription}
            **Factions present:** {factions}
            **Time of day:** {request.TimeOfDay}
            **Default weather:** {request.WeatherDefault}

            Create realistic terrain that reflects the real-world geography of {request.LocationName}, adapted for a post-apocalyptic setting. Include appropriate roads, buildings, water features, and zones based on the geography description. Use only assets from the asset registry.
            """;
    }
}
