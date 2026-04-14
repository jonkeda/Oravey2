using System.Text.Json;
using System.Text.Json.Serialization;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Combat;
using Oravey2.Core.Dialogue;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.NPC;
using Oravey2.Core.Quests;

namespace Oravey2.Core.Content;

/// <summary>
/// Loads game content from a content pack directory on disk.
/// </summary>
public sealed class ContentPackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private readonly string _rootDir;

    public ContentPackLoader(string rootDir)
    {
        if (!Directory.Exists(rootDir))
            throw new DirectoryNotFoundException($"Content pack not found: {rootDir}");
        _rootDir = rootDir;
    }

    public string RootDir => _rootDir;

    // --- Items ---

    public ItemDefinition[] LoadItems()
    {
        var path = Path.Combine(_rootDir, "data", "items.json");
        if (!File.Exists(path)) return [];

        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<ItemCatalogJson>(json, JsonOptions);
        return catalog?.Items?.Select(MapItem).ToArray() ?? [];
    }

    private static ItemDefinition MapItem(ItemJson j) => new(
        Id: j.Id,
        Name: j.Name,
        Description: j.Description,
        Category: Enum.Parse<ItemCategory>(j.Category, ignoreCase: true),
        Weight: j.Weight,
        Stackable: j.Stackable,
        Value: j.Value,
        MaxStack: j.MaxStack ?? 1,
        Slot: j.Slot != null ? Enum.Parse<EquipmentSlot>(j.Slot, ignoreCase: true) : null,
        Effects: j.Effects,
        Weapon: j.Weapon != null ? new WeaponData(
            Damage: j.Weapon.Damage,
            Range: j.Weapon.Range,
            ApCost: j.Weapon.ApCost,
            Accuracy: j.Weapon.Accuracy,
            SkillType: j.Weapon.SkillType,
            CritMultiplier: j.Weapon.CritMultiplier ?? 2.0f) : null,
        Armor: j.Armor != null ? new ArmorData(
            DamageReduction: j.Armor.DamageReduction,
            CoverageZones: j.Armor.CoverageZones) : null);

    // --- NPCs ---

    public NpcDataJson[] LoadNpcs()
    {
        var path = Path.Combine(_rootDir, "data", "npcs.json");
        if (!File.Exists(path)) return [];

        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<NpcCatalogJson>(json, JsonOptions);
        return catalog?.Npcs ?? [];
    }

    // --- Enemies ---

    public EnemySpawnPoint[] LoadEnemies()
    {
        var path = Path.Combine(_rootDir, "data", "enemies.json");
        if (!File.Exists(path)) return [];

        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<EnemyCatalogJson>(json, JsonOptions);
        return catalog?.Enemies?.Select(e => new EnemySpawnPoint(
            GroupId: e.GroupId,
            X: e.X,
            Z: e.Z,
            Count: e.Count,
            Endurance: e.Endurance,
            Luck: e.Luck,
            WeaponDamage: e.WeaponDamage,
            WeaponAccuracy: e.WeaponAccuracy,
            Tag: e.Tag,
            RequiredQuestId: e.RequiredQuestId)).ToArray() ?? [];
    }

    // --- Dialogues ---

    public DialogueTree[] LoadDialogues()
    {
        var dir = Path.Combine(_rootDir, "data", "dialogues");
        if (!Directory.Exists(dir)) return [];

        var trees = new List<DialogueTree>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var json = File.ReadAllText(file);
            var treeJson = JsonSerializer.Deserialize<DialogueTreeJson>(json, JsonOptions);
            if (treeJson != null)
                trees.Add(MapDialogueTree(treeJson));
        }
        return trees.ToArray();
    }

    private static DialogueTree MapDialogueTree(DialogueTreeJson j)
    {
        var nodes = new Dictionary<string, DialogueNode>();
        foreach (var (nodeId, nodeJson) in j.Nodes)
        {
            nodes[nodeId] = new DialogueNode {
                Id = nodeId,
                Speaker = nodeJson.Speaker,
                Text = nodeJson.Text,
                Portrait = nodeJson.Portrait,
                Choices = nodeJson.Choices.Select(MapChoice).ToArray() };
        }
        return new DialogueTree { Id = j.Id, StartNodeId = j.StartNodeId, Nodes = nodes };
    }

    private static DialogueChoice MapChoice(DialogueChoiceJson j) => new DialogueChoice {
        Text = j.Text,
        NextNodeId = j.NextNodeId,
        Condition = j.Condition != null ? MapCondition(j.Condition) : null,
        Consequences = j.Consequences?.Select(MapConsequence).ToArray() ?? []
    };

    private static IDialogueCondition MapCondition(ConditionJson j) => j.Type switch
    {
        "flag" => new FlagCondition(j.Flag!, j.Expected ?? true),
        "and" => new AndCondition(j.Conditions!.Select(MapCondition).ToArray()),
        "counter" => new CounterCondition(j.Counter!, j.Target ?? 0),
        "item" => new ItemCondition(j.ItemId!, j.Count ?? 1),
        "level" => new LevelCondition(j.MinLevel ?? 1),
        "skill" => new SkillCheckCondition(Enum.Parse<SkillType>(j.Skill!, ignoreCase: true), j.MinValue ?? 1),
        _ => throw new JsonException($"Unknown condition type: {j.Type}")
    };

    private static IConsequenceAction MapConsequence(ConsequenceJson j) => j.Type switch
    {
        "setFlag" => new SetFlagAction(j.Flag!),
        "giveCaps" => new GiveCapsAction(j.Amount ?? 0),
        "startQuest" => new StartQuestAction(j.QuestId!),
        "buyItem" => new BuyItemAction(j.ItemId!, j.Price ?? 0),
        "sellItem" => new SellItemAction(j.ItemId!, j.Price ?? 0),
        "giveItem" => new GiveItemAction(j.ItemId!, j.Count ?? 1),
        "giveXP" => new GiveXPAction(j.Amount ?? 0),
        _ => throw new JsonException($"Unknown consequence type: {j.Type}")
    };

    // --- Quests ---

    public QuestDefinition[] LoadQuests()
    {
        var dir = Path.Combine(_rootDir, "data", "quests");
        if (!Directory.Exists(dir)) return [];

        var quests = new List<QuestDefinition>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var json = File.ReadAllText(file);
            var catalog = JsonSerializer.Deserialize<QuestCatalogJson>(json, JsonOptions);
            if (catalog?.Quests != null)
                quests.AddRange(catalog.Quests.Select(MapQuest));
        }
        return quests.ToArray();
    }

    private static QuestDefinition MapQuest(QuestJson j) => new QuestDefinition {
        Id = j.Id,
        Title = j.Title,
        Description = j.Description,
        Type = Enum.Parse<QuestType>(j.Type, ignoreCase: true),
        FirstStageId = j.FirstStageId,
        Stages = j.Stages.ToDictionary(
            kvp => kvp.Key,
            kvp => MapQuestStage(kvp.Key, kvp.Value)),
        XPReward = j.XpReward ?? 0
    };

    private static QuestStage MapQuestStage(string id, QuestStageJson j) => new QuestStage {
        Id = id,
        Description = j.Description,
        Conditions = j.Conditions?.Select(MapQuestCondition).ToArray() ?? [],
        OnCompleteActions = j.OnCompleteActions?.Select(MapQuestAction).ToArray() ?? [],
        NextStageId = j.NextStageId,
        FailConditions = j.FailConditions?.Select(MapQuestCondition).ToArray() ?? [],
        OnFailActions = j.OnFailActions?.Select(MapQuestAction).ToArray() ?? []
    };

    private static IQuestCondition MapQuestCondition(QuestConditionJson j) => j.Type switch
    {
        "flag" => new QuestFlagCondition(j.Flag!, j.Expected ?? true),
        "counter" => new QuestCounterCondition(j.Counter!, j.Target ?? 0),
        "level" => new QuestLevelCondition(j.MinLevel ?? 1),
        "hasItem" => new HasItemCondition(j.ItemId!, j.Count ?? 1),
        "questComplete" => new QuestCompleteCondition(j.QuestId!),
        _ => throw new JsonException($"Unknown quest condition type: {j.Type}")
    };

    private static IQuestAction MapQuestAction(QuestActionJson j) => j.Type switch
    {
        "setFlag" => new QuestSetFlagAction(j.Flag!, j.Value ?? true),
        "giveXP" => new QuestGiveXPAction(j.Amount ?? 0),
        "startQuest" => new QuestStartQuestAction(j.QuestId!),
        _ => throw new JsonException($"Unknown quest action type: {j.Type}")
    };
}
