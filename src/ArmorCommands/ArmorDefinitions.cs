using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace FACore.ArmorCommands;

public enum ArmorAttributeSchema
{
    Layered,
    ThreeColor
}

public sealed record ArmorPieceDefinition(
    string Piece,
    string ItemCodeTemplate,
    IReadOnlySet<string> Decorations)
{
    public string SlotPrefix
    {
        get
        {
            int marker = ItemCodeTemplate.IndexOf('{');
            string prefix = marker < 0 ? ItemCodeTemplate : ItemCodeTemplate[..marker];
            return prefix.TrimEnd('-');
        }
    }
}

public sealed record ArmorRenderingDefinition(
    string ShapeTemplate,
    string BaseTextureTemplate,
    string? CoverTextureTemplate);

public sealed record ArmorDefinition(
    string Identifier,
    string CommandName,
    string DisplayName,
    string Domain,
    ArmorAttributeSchema Schema,
    IReadOnlySet<string> Styles,
    IReadOnlyDictionary<string, ArmorPieceDefinition> Pieces,
    IReadOnlyDictionary<string, IReadOnlySet<string>> PiecesByStyle,
    IReadOnlyDictionary<string, IReadOnlySet<string>> CoversByBaseMetal,
    IReadOnlyDictionary<string, IReadOnlySet<string>> TrimsByBaseMetal,
    IReadOnlySet<string> Colors,
    ArmorRenderingDefinition Rendering)
{
    // Defaults to the former contract for source compatibility with external definitions.
    public IReadOnlySet<string> BaseMetals { get; init; } = CoversByBaseMetal.Keys.ToFrozenSet(StringComparer.Ordinal);
}

public static class ArmorDefinitions
{
    public static readonly IReadOnlySet<string> ArmorBaseCovers = Set(
        "bismuth", "brass", "copper", "gold", "lead", "none", "silver");

    public static readonly IReadOnlySet<string> ArmorFullCovers = Set(
        "bismuth", "blackbronze", "bismuthbronze", "tinbronze", "brass", "copper",
        "cupronickel", "electrum", "gold", "lead", "meteoriciron", "none", "silver",
        "uranium", "zinc");

    public static readonly IReadOnlySet<string> ArmorBaseTrims = Set(
        "bismuth", "brass", "copper", "gold", "lead", "none", "silver");

    public static readonly IReadOnlySet<string> ArmorFullTrims = Set(
        "bismuth", "blackbronze", "bismuthbronze", "tinbronze", "brass", "copper",
        "cupronickel", "electrum", "gold", "lead", "meteoriciron", "none", "silver",
        "uranium", "zinc");

    public static readonly IReadOnlySet<string> ArmorColors = Set(
        "none", "red", "rose", "green", "pine", "black", "blue", "navy", "brown",
        "white", "purple", "pink", "burgundy", "orange", "yellow", "gray", "plain");

    public static readonly IReadOnlySet<string> ThreeColorArmorColors = Set(
        "red", "rose", "green", "pine", "black", "blue", "navy", "brown", "white",
        "purple", "pink", "burgundy", "orange", "yellow", "gray", "plain");

    public static readonly IReadOnlySet<string> BearColors = Set("black", "brown", "white");

    private static readonly IReadOnlySet<string> HeadDecorations = Set(
        "none", "communis-pennae", "communis-cauda", "communis-pluma", "communis-crista",
        "communis-pinna", "communis-transversa", "regalis-crista", "metallicus-laurea", "bear", "wolf");
    private static readonly IReadOnlySet<string> ScaleHeadDecorations = Set(
        "none", "communis-pennae", "communis-cauda", "communis-pluma", "communis-crista",
        "communis-pinna", "communis-transversa", "regalis-crista", "metallicus-laurea", "bear", "wolf");
    private static readonly IReadOnlySet<string> BodyDecorations = Set("none", "insignia", "mantellum", "bear", "wolf");
    private static readonly IReadOnlySet<string> ScaleBodyDecorations = Set("none", "insignia", "mantellum", "bear", "wolf");
    private static readonly IReadOnlySet<string> LegsDecorations = Set("none", "lacinia", "bear", "wolf");
    private static readonly IReadOnlySet<string> NoDecorations = Set("none");
    private static readonly IReadOnlySet<string> AllPieces = Set("head", "body", "legs");
    private static readonly IReadOnlySet<string> SupportedBaseMetals = Set("iron", "meteoriciron", "steel");
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> LayeredCovers = MetalRules(
        ("iron", ArmorBaseCovers), ("meteoriciron", ArmorBaseCovers), ("steel", ArmorFullCovers));
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> LayeredTrims = MetalRules(
        ("iron", ArmorBaseTrims), ("meteoriciron", ArmorBaseTrims), ("steel", ArmorFullTrims));
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> NoLayeredMetalRules =
        Array.Empty<KeyValuePair<string, IReadOnlySet<string>>>().ToFrozenDictionary(StringComparer.Ordinal);
    private static readonly ArmorRenderingDefinition GreenwichRendering = new(
        "shapes/entity/armor/greenwich/{slot}/{style}/{variant}{piece}.json",
        "armor/entity/greenwich/base/plate/noble/{metal}",
        "armor/entity/greenwich/cover/{metal}/plate/noble/{cover}");
    private static readonly ArmorRenderingDefinition GothicRendering = new(
        "shapes/entity/armor/gothic/{slot}/{style}/{variant}{piece}.json",
        "armor/entity/gothic/base/plate/noble/{metal}",
        "armor/entity/gothic/cover/{metal}/plate/noble/{cover}");
    private static readonly ArmorRenderingDefinition ScaleRendering = new(
        "shapes/entity/armor/dynasties/{slot}/{style}/{variant}{piece}.json",
        "armor/entity/dynasties/base/scale/{metal}",
        "armor/entity/dynasties/cover/{metal}/scale/{cover}");
    private static readonly ArmorRenderingDefinition BrigandineRendering = new(
        "shapes/entity/armor/dynasties/{slot}/{style}/{variant}{piece}.json",
        "armor/entity/dynasties/base/brigandine/{style}/{metal}",
        null);

    public static readonly ArmorDefinition Greenwich = new(
        "greenwich", "greenwich_plate", "Greenwich", "fagreenwich", ArmorAttributeSchema.Layered,
        Set("armet", "noble"),
        Pieces(
            new("head", "platehead-{style}-{metal}", HeadDecorations),
            new("body", "platebody-{style}-{metal}", BodyDecorations),
            new("legs", "platelegs-{style}-{metal}", LegsDecorations)),
        PieceRules(
            ("armet", Set("head")),
            ("noble", Set("body", "legs"))),
        LayeredCovers, LayeredTrims, ArmorColors, GreenwichRendering)
    {
        BaseMetals = SupportedBaseMetals
    };

    public static readonly ArmorDefinition Gothic = new(
        "gothic", "gothic_plate", "Gothic", "fagothic", ArmorAttributeSchema.Layered,
        Set("sallet", "noble"),
        Pieces(
            new("head", "platehead-{style}-{metal}", HeadDecorations),
            new("body", "platebody-{style}-{metal}", BodyDecorations),
            new("legs", "platelegs-{style}-{metal}", LegsDecorations)),
        PieceRules(
            ("sallet", Set("head")),
            ("noble", Set("body", "legs"))),
        LayeredCovers, LayeredTrims, ArmorColors, GothicRendering)
    {
        BaseMetals = SupportedBaseMetals
    };

    public static readonly ArmorDefinition Scale = new(
        "scale", "dynasties_scale", "Chinese Dynasties scale", "fadynasties", ArmorAttributeSchema.Layered,
        Set("song", "songmasked", "jin"),
        Pieces(
            new("head", "scalehead-{style}-{metal}", ScaleHeadDecorations),
            new("body", "scalebody-{style}-{metal}", ScaleBodyDecorations),
            new("legs", "scalelegs-{style}-{metal}", LegsDecorations)),
        PieceRules(
            ("song", AllPieces),
            ("songmasked", Set("head")),
            ("jin", AllPieces)),
        LayeredCovers, LayeredTrims, ArmorColors, ScaleRendering)
    {
        BaseMetals = SupportedBaseMetals
    };

    public static readonly ArmorDefinition Brigandine = new(
        "brigandine", "dynasties_brigandine", "Chinese Dynasties brigandine", "fadynasties", ArmorAttributeSchema.ThreeColor,
        Set("qing"),
        Pieces(
            new("head", "brigandinehead-{style}-{metal}", NoDecorations),
            new("body", "brigandinebody-{style}-{metal}", NoDecorations),
            new("legs", "brigandinelegs-{style}-{metal}", NoDecorations)),
        PieceRules(("qing", AllPieces)),
        NoLayeredMetalRules, NoLayeredMetalRules, ThreeColorArmorColors, BrigandineRendering)
    {
        BaseMetals = SupportedBaseMetals
    };

    public static readonly ArmorDefinition VarangianPlate = new(
        "varangianplate", "varangian_plate", "Varangian plate", "favarangian", ArmorAttributeSchema.Layered,
        Set("captain", "captainmasked", "myrmidon", "veteran"),
        Pieces(
            new("head", "platehead-{style}-{metal}", HeadDecorations),
            new("body", "platebody-{style}-{metal}", BodyDecorations),
            new("legs", "platelegs-{style}-{metal}", LegsDecorations)),
        PieceRules(("captain", AllPieces), ("captainmasked", Set("head")),
            ("myrmidon", Set("body")), ("veteran", Set("body"))),
        LayeredCovers, LayeredTrims, ArmorColors,
        new("shapes/entity/armor/varangian/{slot}/{style}/{variant}{piece}.json",
            "armor/entity/varangian/base/{metal}", "armor/entity/varangian/cover/{metal}/{cover}"))
    {
        BaseMetals = SupportedBaseMetals
    };

    public static readonly ArmorDefinition VarangianScale = new(
        "varangianscale", "varangian_scale", "Varangian scale", "favarangian", ArmorAttributeSchema.Layered,
        Set("guard", "guardmasked"),
        Pieces(
            new("head", "scalehead-{style}-{metal}", HeadDecorations),
            new("body", "scalebody-{style}-{metal}", BodyDecorations),
            new("legs", "scalelegs-{style}-{metal}", LegsDecorations)),
        PieceRules(("guard", AllPieces), ("guardmasked", Set("head"))),
        LayeredCovers, LayeredTrims, ArmorColors,
        new("shapes/entity/armor/varangian/{slot}/{style}/{variant}{piece}.json",
            "armor/entity/varangian/base/{metal}", "armor/entity/varangian/cover/{metal}/{cover}"))
    {
        BaseMetals = SupportedBaseMetals
    };

    public static readonly IReadOnlyDictionary<string, ArmorDefinition> All =
        new Dictionary<string, ArmorDefinition>(StringComparer.Ordinal)
        {
            [Greenwich.Identifier] = Greenwich,
            [Gothic.Identifier] = Gothic,
            [Scale.Identifier] = Scale,
            [Brigandine.Identifier] = Brigandine,
            [VarangianPlate.Identifier] = VarangianPlate,
            [VarangianScale.Identifier] = VarangianScale
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static IReadOnlySet<string> Set(params string[] values) =>
        values.ToFrozenSet(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, ArmorPieceDefinition> Pieces(params ArmorPieceDefinition[] pieces)
    {
        var result = new Dictionary<string, ArmorPieceDefinition>(StringComparer.Ordinal);
        foreach (ArmorPieceDefinition piece in pieces)
        {
            result.Add(piece.Piece, piece);
        }

        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> PieceRules(
        params (string Style, IReadOnlySet<string> Pieces)[] rules)
    {
        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach ((string style, IReadOnlySet<string> pieces) in rules) result.Add(style, pieces);
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> MetalRules(
        params (string Metal, IReadOnlySet<string> Values)[] rules)
    {
        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach ((string metal, IReadOnlySet<string> values) in rules) result.Add(metal, values);
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
