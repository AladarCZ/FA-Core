using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace FACore.ArmorCommands;

internal enum LegacyMigrationStatus { Unrelated, Migrated, Skipped }

internal sealed record LegacyArmorMapping(
    ResolvedArmor Armor,
    IReadOnlyDictionary<string, string> Variants,
    LayeredArmorState Layered,
    ThreeColorArmorState ThreeColor,
    string? UnsupportedReason = null);

/// <summary>
/// Exact code/variant schemas from Greenwich 1.4.6, Gothic 1.3.4 and Dynasties 1.3.3.
/// Does not depend on the old content mods being installed.
/// </summary>
internal static class LegacyArmorCatalog
{
    private enum Finish { Greenwich, Gothic, Ming, Song, Qing }

    private sealed record Source(
        string Code, string Piece, string Style, string StyleKey, string ColorKey,
        ArmorDefinition Target, string TargetStyle, string Decoration, Finish Finish,
        bool RequiresColor = false, bool AllowsBear = true, string? UnsupportedReason = null);

    private static readonly string[] Colors =
        ["red", "green", "black", "blue", "brown", "white", "purple", "pink", "orange", "yellow", "gray", "plain"];
    private static readonly string[] Visuals = [.. Colors, "none", "bear-black", "bear-brown", "bear-polar"];

    // Explicit family/type mappings. Decoration identities were checked against old and new shapes:
    // Gothic sallet uses a crest; Greenwich armet uses a plume; Ming uses wings and a cloak;
    // old Song uses a feather and insignia. The old Gothic wreath has no current equivalent.
    private static readonly Source[] Sources =
    [
        new("fa-greenwich-head", "head", "armet", "style", "plume", ArmorDefinitions.Greenwich, "armet", "communis-pluma", Finish.Greenwich),
        new("fa-greenwich-head", "head", "armetwing", "style", "plume", ArmorDefinitions.Greenwich, "armet", "communis-pennae", Finish.Greenwich, true, false),
        new("fa-greenwich-head", "head", "armetcrista", "style", "plume", ArmorDefinitions.Greenwich, "armet", "communis-crista", Finish.Greenwich, true, false),
        new("fa-greenwich-body", "body", "", "", "textile", ArmorDefinitions.Greenwich, "noble", "insignia", Finish.Greenwich),
        new("fa-greenwich-legs", "legs", "", "", "line", ArmorDefinitions.Greenwich, "noble", "lacinia", Finish.Greenwich),
        new("fa-gothic-head", "head", "sallet", "style", "plume", ArmorDefinitions.Gothic, "sallet", "communis-crista", Finish.Gothic),
        new("fa-gothic-head", "head", "salletwing", "style", "plume", ArmorDefinitions.Gothic, "sallet", "communis-pennae", Finish.Gothic, true, false),
        new("fa-gothic-head", "head", "salletwreath", "style", "plume", ArmorDefinitions.Gothic, "sallet", "none", Finish.Gothic, true, false,
            "Legacy salletwreath decoration has no current equivalent."),
        new("fa-gothic-body", "body", "", "", "textile", ArmorDefinitions.Gothic, "noble", "insignia", Finish.Gothic),
        new("fa-gothic-legs", "legs", "", "", "line", ArmorDefinitions.Gothic, "noble", "lacinia", Finish.Gothic),
        new("fa-dynasties-head", "head", "ming", "type", "plume", ArmorDefinitions.Scale, "song", "communis-pennae", Finish.Ming),
        new("fa-dynasties-head", "head", "maskedming", "type", "plume", ArmorDefinitions.Scale, "songmasked", "communis-pennae", Finish.Ming, false, false),
        new("fa-dynasties-body", "body", "ming", "style", "textile", ArmorDefinitions.Scale, "song", "mantellum", Finish.Ming),
        new("fa-dynasties-legs", "legs", "ming", "version", "line", ArmorDefinitions.Scale, "song", "lacinia", Finish.Ming),
        new("fa-dynasties-head", "head", "song", "type", "plume", ArmorDefinitions.Scale, "jin", "communis-cauda", Finish.Song),
        new("fa-dynasties-body", "body", "song", "style", "textile", ArmorDefinitions.Scale, "jin", "insignia", Finish.Song),
        new("fa-dynasties-legs", "legs", "song", "version", "line", ArmorDefinitions.Scale, "jin", "lacinia", Finish.Song),
        new("fa-dynasties-head", "head", "qing", "type", "plume", ArmorDefinitions.Brigandine, "qing", "none", Finish.Qing, true, false),
        new("fa-dynasties-body", "body", "qing", "style", "textile", ArmorDefinitions.Brigandine, "qing", "none", Finish.Qing, true, false),
        new("fa-dynasties-legs", "legs", "qing", "version", "line", ArmorDefinitions.Brigandine, "qing", "none", Finish.Qing, true, false)
    ];

    internal static readonly IReadOnlyDictionary<string, LegacyArmorMapping> Mappings = BuildMappings();

    internal static bool IsCandidate(AssetLocation code) => code.Domain switch
    {
        "fagreenwich" => code.Path.StartsWith("fa-greenwich-", StringComparison.Ordinal)
            && !code.Path.StartsWith("fa-greenwich-schematic", StringComparison.Ordinal)
            && !code.Path.StartsWith("fa-greenwich-rolled-schematic", StringComparison.Ordinal),
        "fagothic" => code.Path.StartsWith("fa-gothic-", StringComparison.Ordinal)
            && !code.Path.StartsWith("fa-gothic-schematic", StringComparison.Ordinal)
            && !code.Path.StartsWith("fa-gothic-rolled-schematic", StringComparison.Ordinal),
        "fadynasties" => code.Path.StartsWith("fa-dynasties-", StringComparison.Ordinal)
            && !code.Path.StartsWith("fa-dynasties-schematic", StringComparison.Ordinal)
            && !code.Path.StartsWith("fa-dynasties-rolled-schematic", StringComparison.Ordinal),
        _ => false
    };

    private static IReadOnlyDictionary<string, LegacyArmorMapping> BuildMappings()
    {
        var result = new Dictionary<string, LegacyArmorMapping>(StringComparer.Ordinal);
        foreach (Source source in Sources)
        {
            string[] metals = source.Finish == Finish.Greenwich
                ? ["iron", "meteoriciron", "steel", "steelplain"] : ["iron", "meteoriciron", "steel"];
            foreach (string metal in metals)
            foreach (string visual in Visuals)
            {
                bool bear = visual.StartsWith("bear-", StringComparison.Ordinal);
                if ((source.RequiresColor && visual == "none") || (!source.AllowsBear && bear)) continue;
                var variants = new Dictionary<string, string>(StringComparer.Ordinal);
                // Variant order is significant: European helmets use style-metal-plume;
                // Dynasties uses metal-(type/style/version)-(plume/textile/line).
                if (source.StyleKey.Length > 0 && source.Target.Domain != "fadynasties") variants.Add(source.StyleKey, source.Style);
                variants.Add("metal" + source.Piece, metal);
                if (source.Target.Domain == "fadynasties") variants.Add(source.StyleKey, source.Style);
                variants.Add(source.ColorKey, visual);
                string code = source.Target.Domain + ":" + source.Code + "-" + string.Join("-", variants.Values);
                var armor = new ResolvedArmor(source.Target, source.Target.Pieces[source.Piece], source.Piece,
                    source.TargetStyle, metal == "steelplain" ? "steel" : metal);
                (string cover, string trim) = MaterialFinish(source.Finish, metal);
                string decoration = visual == "none" ? "none" : bear ? "bear" : source.Decoration;
                string color = visual switch
                {
                    "bear-black" => "black", "bear-brown" => "brown", "bear-polar" => "white", _ => visual
                };
                // 1.3.3 Qing has ONE color variant per item, not three. Preserve it across
                // the three new regions; never read colors from unrelated equipped pieces.
                result.Add(code, new(armor, variants, new(cover, trim, decoration, color),
                    new(color, color, color), source.UnsupportedReason));
            }
        }
        return result;
    }

    private static (string Cover, string Trim) MaterialFinish(Finish finish, string metal) => finish switch
    {
        Finish.Greenwich => metal switch
        {
            "steel" => ("blackbronze", "gold"), "steelplain" => ("none", "brass"),
            "meteoriciron" => ("none", "copper"), _ => ("none", "none")
        },
        Finish.Gothic => (metal == "steel" ? "silver" : "none", "none"),
        Finish.Ming => metal switch
        {
            "steel" => ("lead", "gold"), "meteoriciron" => ("none", "copper"), _ => ("none", "none")
        },
        Finish.Song => ("none", metal switch { "steel" => "gold", "meteoriciron" => "copper", _ => "none" }),
        _ => ("none", "none")
    };
}

internal static class LegacyArmorMigration
{
    /// <summary>
    /// Never mutates source. The installed VS ServerSystemItemIdRemapper.RemapItems creates
    /// an Item with IsMissing=true and the ORIGINAL Code for each missing saved item ID.
    /// Such placeholders lose Variant and old collectible properties, but retain stack
    /// attributes. The exact code catalog above recovers their variant values safely.
    ///
    /// Truly unresolved ItemStacks only serialize Class, Id, StackSize and Attributes
    /// (ItemStack.ToBytes/FromBytes). There is no original-code field on those stacks.
    /// InventoryBase.ResolveBlocksOrItems can clear them; FixMapping can also reject them.
    /// Without a code-bearing collectible, or after deletion, a player command cannot
    /// recover their identity. Do not interpret numeric IDs or arbitrary attributes as codes.
    /// </summary>
    internal static LegacyMigrationStatus TryReconstruct(
        ItemStack source, IWorldAccessor world, out ItemStack? replacement, out string reason)
    {
        replacement = null;
        reason = "";
        AssetLocation? code = source.Collectible?.Code;
        if (code == null)
        {
            reason = FaText.Get("Unresolved stack has no recoverable original code; legacy origin cannot be determined.");
            return LegacyMigrationStatus.Skipped;
        }
        if (source.Class != EnumItemClass.Item || !LegacyArmorCatalog.IsCandidate(code)) return LegacyMigrationStatus.Unrelated;
        if (!LegacyArmorCatalog.Mappings.TryGetValue(code.ToString(), out LegacyArmorMapping? mapping))
        {
            reason = FaText.Get("Code/type/material/visual combination is not in the supported legacy assets.");
            return LegacyMigrationStatus.Skipped;
        }
        if (mapping.UnsupportedReason != null)
        {
            reason = mapping.UnsupportedReason;
            return LegacyMigrationStatus.Skipped;
        }
        // A populated live Variant must agree with the historical code. Missing placeholders
        // have no variants; this is why detection must not require collectible.Variant.
        if (source.Collectible!.Variant != null && source.Collectible.Variant.Any(pair =>
            !mapping.Variants.TryGetValue(pair.Key, out string? value) || value != pair.Value))
        {
            reason = FaText.Get("Registered variants conflict with the legacy code schema.");
            return LegacyMigrationStatus.Skipped;
        }
        if (source.Attributes?.HasAttribute("types") == true)
        {
            reason = FaText.Get("Legacy code already carries ARL types; refusing to overwrite unknown visual state.");
            return LegacyMigrationStatus.Skipped;
        }
        ResolvedArmor armor = mapping.Armor;
        var targetCode = new AssetLocation(armor.Domain, ArmorResolver.CreateItemPath(armor));
        Item? item = world.GetItem(targetCode);
        if (item?.Code?.Equals(targetCode) != true || item.IsMissing)
        {
            reason = FaText.Get("Current armor '{0}' is unavailable or is itself a missing-item placeholder.", targetCode);
            return LegacyMigrationStatus.Skipped;
        }
        // No compile-time ARL dependency, matching FA-Core's existing integration.
        if (item.CollectibleBehaviors?.Any(behavior => IsArlWearable(behavior.GetType())) != true)
        {
            reason = FaText.Get("Current armor '{0}' has no Attribute Rendering Library wearable behavior.", targetCode);
            return LegacyMigrationStatus.Skipped;
        }
        if (source.StackSize < 1 || source.StackSize > item.MaxStackSize)
        {
            reason = FaText.Get("Stack size {0} exceeds the current armor limit {1}; split the legacy stack first.", source.StackSize, item.MaxStackSize);
            return LegacyMigrationStatus.Skipped;
        }
        var stack = new ItemStack(item, source.StackSize);
        stack.Attributes = source.Attributes?.Clone() ?? new TreeAttribute();
        stack.Attributes["types"] = armor.Definition.Schema == ArmorAttributeSchema.Layered
            ? ArmorCompatibility.CreateLayeredTypes(armor.Piece, mapping.Layered)
            : ArmorCompatibility.CreateThreeColorTypes(armor.Piece, mapping.ThreeColor);
        ArmorCompatibilityResult state = ArmorCompatibility.ValidateStateContainer(stack, armor, out _);
        if (!ArmorResolver.Resolve(stack, out ResolvedArmor? resolved).Allowed || resolved == null || !state.Allowed)
        {
            reason = FaText.Get("Current armor state is invalid: {0} ({1}).", state.Issue, state.Value);
            return LegacyMigrationStatus.Skipped;
        }
        if (stack.Attributes.HasAttribute("durability"))
        {
            if (stack.Attributes["durability"] is not (IntAttribute or LongAttribute or FloatAttribute or DoubleAttribute))
            {
                reason = FaText.Get("Durability attribute is not numeric.");
                return LegacyMigrationStatus.Skipped;
            }
            double remaining = stack.Attributes.GetDecimal("durability", double.NaN);
            int maximum = item.GetMaxDurability(stack);
            if (!double.IsFinite(remaining) || remaining < 0 || remaining > int.MaxValue || maximum <= 0)
            {
                reason = FaText.Get("Durability cannot be safely preserved.");
                return LegacyMigrationStatus.Skipped;
            }
            // Keep absolute remaining durability, capped at the new maximum. Missing
            // placeholders have no old max durability, so a percentage would be guessed.
            stack.Attributes.SetInt("durability", Math.Min((int)remaining, maximum));
        }
        replacement = stack;
        return LegacyMigrationStatus.Migrated;
    }

    private static bool IsArlWearable(Type? type)
    {
        for (; type != null; type = type.BaseType)
            if (type.FullName == "AttributeRenderingLibrary.CollectibleBehaviorWearable") return true;
        return false;
    }

    internal static string Describe(ItemStack stack)
    {
        string code = stack.Collectible?.Code?.ToString() ?? FaText.Get("<unresolved {0} id={1}>", stack.Class, stack.Id);
        string variants = LegacyArmorCatalog.Mappings.TryGetValue(code, out LegacyArmorMapping? mapping)
            ? string.Join(", ", mapping.Variants.Select(pair => pair.Key + "=" + pair.Value)) : "unrecognized code suffix";
        string registered = stack.Collectible?.Variant == null ? "" :
            string.Join(", ", stack.Collectible.Variant.Select(pair => pair.Key + "=" + pair.Value));
        return $"{code}; decoded variants=[{variants}]; registered variants=[{registered}]";
    }
}
