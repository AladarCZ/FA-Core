using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace FACore.ArmorCommands;

public sealed record ResolvedArmor
{
    public ResolvedArmor(ArmorDefinition definition, ArmorPieceDefinition pieceDefinition, string piece, string style, string baseMetal)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(pieceDefinition);

        if (!definition.Pieces.TryGetValue(piece, out ArmorPieceDefinition? canonicalPiece)
            || !ReferenceEquals(canonicalPiece, pieceDefinition)
            || !string.Equals(pieceDefinition.Piece, piece, StringComparison.Ordinal))
        {
            throw new ArgumentException("The piece and piece definition must be the canonical entry from the supplied armor definition.", nameof(pieceDefinition));
        }

        if (!definition.Styles.Contains(style)
            || !definition.PiecesByStyle.TryGetValue(style, out IReadOnlySet<string>? supportedPieces)
            || !supportedPieces.Contains(piece))
        {
            throw new ArgumentException("The style does not support this armor piece.", nameof(style));
        }

        if (!definition.BaseMetals.Contains(baseMetal))
        {
            throw new ArgumentException("The base metal is not supported by this armor definition.", nameof(baseMetal));
        }

        Definition = definition;
        PieceDefinition = canonicalPiece;
        Style = style;
        BaseMetal = baseMetal;
    }

    public ArmorDefinition Definition { get; }
    public ArmorPieceDefinition PieceDefinition { get; }
    public string Piece => PieceDefinition.Piece;
    public string Style { get; }
    public string BaseMetal { get; }
    public string Domain => Definition.Domain;
    public string Family => Definition.Identifier;
    public string SlotPrefix => PieceDefinition.SlotPrefix;
    public string TextureKind => SlotPrefix.StartsWith("plate", StringComparison.Ordinal) ? "plate" : SlotPrefix;

    public void Deconstruct(
        out ArmorDefinition definition,
        out ArmorPieceDefinition pieceDefinition,
        out string piece,
        out string style,
        out string baseMetal)
    {
        definition = Definition;
        pieceDefinition = PieceDefinition;
        piece = Piece;
        style = Style;
        baseMetal = BaseMetal;
    }
}

public enum ArmorCompatibilityIssue
{
    None,
    UnsupportedArmor,
    AmbiguousArmor,
    MalformedArmorState,
    InvalidStyleOrPiece,
    InvalidCover,
    InvalidTrim,
    InvalidDecoration,
    InvalidDecorationColor,
    InvalidDecorationColorPairing,
    InvalidThreeColorValue,
    SchemaMismatch
}

public readonly record struct ArmorCompatibilityResult(ArmorCompatibilityIssue Issue, string Value = "")
{
    public bool Allowed => Issue == ArmorCompatibilityIssue.None;
    public static ArmorCompatibilityResult Success => new(ArmorCompatibilityIssue.None);
}

public readonly record struct LayeredArmorState(string Cover, string Trim, string Decoration, string Color);
public readonly record struct ThreeColorArmorState(string Color1, string Color2, string Color3);

public static class ArmorResolver
{
    private readonly record struct TemplateMatcher(
        ArmorDefinition Definition,
        ArmorPieceDefinition Piece,
        Regex Pattern);

    private static readonly IReadOnlyList<TemplateMatcher> TemplateMatchers = CreateTemplateMatchers();

    public static ArmorCompatibilityResult Resolve(ItemStack? stack, out ResolvedArmor? armor)
    {
        armor = null;
        AssetLocation? code = stack?.Collectible?.Code;
        return code == null ? new(ArmorCompatibilityIssue.UnsupportedArmor) : Resolve(code, out armor);
    }

    public static ArmorCompatibilityResult Resolve(AssetLocation code, out ResolvedArmor? armor)
    {
        armor = null;
        ResolvedArmor? match = null;
        foreach (TemplateMatcher matcher in TemplateMatchers)
        {
            ArmorDefinition definition = matcher.Definition;
            if (!string.Equals(code.Domain, definition.Domain, StringComparison.Ordinal)) continue;
            if (!TryMatchTemplate(matcher.Pattern, code.Path, out string style, out string metal)) continue;
            ArmorPieceDefinition piece = matcher.Piece;
            if (!definition.Styles.Contains(style)
                || !definition.PiecesByStyle.TryGetValue(style, out IReadOnlySet<string>? supportedPieces)
                || !supportedPieces.Contains(piece.Piece)
                || !definition.BaseMetals.Contains(metal)) continue;
            var candidate = new ResolvedArmor(definition, piece, piece.Piece, style, metal);
            if (match != null) return new(ArmorCompatibilityIssue.AmbiguousArmor);
            match = candidate;
        }
        if (match == null) return new(ArmorCompatibilityIssue.UnsupportedArmor);
        armor = match;
        return ArmorCompatibilityResult.Success;
    }

    public static string CreateItemPath(ResolvedArmor armor) => CreateItemPath(armor.PieceDefinition, armor.Style, armor.BaseMetal);

    public static string CreateItemPath(ArmorPieceDefinition piece, string style, string metal) =>
        piece.ItemCodeTemplate.Replace("{style}", style, StringComparison.Ordinal).Replace("{metal}", metal, StringComparison.Ordinal);

    private static IReadOnlyList<TemplateMatcher> CreateTemplateMatchers()
    {
        var matchers = new List<TemplateMatcher>();
        foreach (ArmorDefinition definition in ArmorDefinitions.All.Values)
        {
            foreach (ArmorPieceDefinition piece in definition.Pieces.Values)
            {
                string pattern = "^" + Regex.Escape(piece.ItemCodeTemplate)
                    .Replace("\\{style}", "(?<style>[^/]+)", StringComparison.Ordinal)
                    .Replace("\\{metal}", "(?<metal>[^/]+)", StringComparison.Ordinal) + "$";
                matchers.Add(new TemplateMatcher(definition, piece, new Regex(pattern, RegexOptions.CultureInvariant)));
            }
        }
        return matchers;
    }

    private static bool TryMatchTemplate(Regex pattern, string path, out string style, out string metal)
    {
        Match match = pattern.Match(path);
        style = match.Success ? match.Groups["style"].Value : "";
        metal = match.Success ? match.Groups["metal"].Value : "";
        return match.Success;
    }
}

public static class ArmorCompatibility
{
    public const string LayeredNeutral = "none";
    public const string ThreeColorNeutral = "plain";

    public static string Normalize(string? value, string defaultValue = "") =>
        string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim().ToLowerInvariant();

    public static string NormalizeMetal(string? value, string defaultValue = "") => Normalize(value, defaultValue) switch
    {
        "meteor" or "meteoric" or "meteoriron" or "meteoric-iron" => "meteoriciron",
        string token => token
    };

    public static ArmorCompatibilityResult ValidateStateContainer(ItemStack stack, ResolvedArmor armor, out ITreeAttribute? types)
    {
        types = stack.Attributes?.GetTreeAttribute("types");
        if (types == null) return new(ArmorCompatibilityIssue.MalformedArmorState);
        if (armor.Definition.Schema == ArmorAttributeSchema.Layered)
        {
            return TryReadLayered(types, armor.Piece, out LayeredArmorState layered)
                ? ValidateFinalLayered(armor, layered)
                : new(ArmorCompatibilityIssue.MalformedArmorState);
        }
        return TryReadThreeColor(types, armor.Piece, out ThreeColorArmorState threeColor)
            ? ValidateFinalThreeColor(armor, threeColor)
            : new(ArmorCompatibilityIssue.MalformedArmorState);
    }

    public static ArmorCompatibilityResult ValidateCover(ResolvedArmor armor, string cover) =>
        armor.Definition.Schema != ArmorAttributeSchema.Layered ? new(ArmorCompatibilityIssue.SchemaMismatch) :
        armor.Definition.CoversByBaseMetal.TryGetValue(armor.BaseMetal, out IReadOnlySet<string>? covers) && covers.Contains(cover)
            ? ArmorCompatibilityResult.Success
            : new(ArmorCompatibilityIssue.InvalidCover, cover);

    public static ArmorCompatibilityResult ValidateTrim(ResolvedArmor armor, string trim) =>
        armor.Definition.Schema != ArmorAttributeSchema.Layered ? new(ArmorCompatibilityIssue.SchemaMismatch) :
        armor.Definition.TrimsByBaseMetal.TryGetValue(armor.BaseMetal, out IReadOnlySet<string>? trims) && trims.Contains(trim)
            ? ArmorCompatibilityResult.Success
            : new(ArmorCompatibilityIssue.InvalidTrim, trim);

    public static ArmorCompatibilityResult ValidateDecoration(ResolvedArmor armor, string decoration) =>
        armor.Definition.Schema != ArmorAttributeSchema.Layered ? new(ArmorCompatibilityIssue.SchemaMismatch) :
        armor.PieceDefinition.Decorations.Contains(decoration) ? ArmorCompatibilityResult.Success : new(ArmorCompatibilityIssue.InvalidDecoration, decoration);

    public static ArmorCompatibilityResult ValidateDecorationColor(ResolvedArmor armor, string decoration, string color)
    {
        if (armor.Definition.Schema != ArmorAttributeSchema.Layered) return new(ArmorCompatibilityIssue.SchemaMismatch);
        return IsValidDecorationColor(armor, decoration, color)
            ? ArmorCompatibilityResult.Success
            : new(ArmorCompatibilityIssue.InvalidDecorationColor, color);
    }

    public static IEnumerable<string> GetValidDecorationColors(ResolvedArmor armor, string decoration)
    {
        foreach (string color in armor.Definition.Colors)
        {
            if (ValidateDecorationColor(armor, decoration, color).Allowed
                && HasValidDecorationColorPairing(decoration, color))
            {
                yield return color;
            }
        }
    }

    private static bool IsValidDecorationColor(ResolvedArmor armor, string decoration, string color) =>
        armor.Definition.Colors.Contains(color)
        && (decoration switch
        {
            "bear" or "wolf" => ArmorDefinitions.BearColors.Contains(color),
            _ when decoration.StartsWith("metallicus-", StringComparison.Ordinal) => color == "plain",
            _ => true
        });

    private static bool HasValidDecorationColorPairing(string decoration, string color) =>
        string.Equals(decoration, LayeredNeutral, StringComparison.Ordinal)
        == string.Equals(color, LayeredNeutral, StringComparison.Ordinal);

    public static ArmorCompatibilityResult ValidateThreeColor(ResolvedArmor armor, string color) =>
        armor.Definition.Schema != ArmorAttributeSchema.ThreeColor ? new(ArmorCompatibilityIssue.SchemaMismatch) :
        armor.Definition.Colors.Contains(color) ? ArmorCompatibilityResult.Success : new(ArmorCompatibilityIssue.InvalidThreeColorValue, color);

    public static ArmorCompatibilityResult ValidateFinalLayered(ResolvedArmor armor, LayeredArmorState state)
    {
        ArmorCompatibilityResult result = ValidateCover(armor, state.Cover);
        if (!result.Allowed) return result;
        result = ValidateTrim(armor, state.Trim);
        if (!result.Allowed) return result;
        result = ValidateDecoration(armor, state.Decoration);
        if (!result.Allowed) return result;
        result = ValidateDecorationColor(armor, state.Decoration, state.Color);
        if (!result.Allowed) return result;
        return HasValidDecorationColorPairing(state.Decoration, state.Color)
            ? ArmorCompatibilityResult.Success
            : new(ArmorCompatibilityIssue.InvalidDecorationColorPairing);
    }

    public static ArmorCompatibilityResult ValidateFinalThreeColor(ResolvedArmor armor, ThreeColorArmorState state)
    {
        foreach (string color in new[] { state.Color1, state.Color2, state.Color3 })
        {
            ArmorCompatibilityResult result = ValidateThreeColor(armor, color);
            if (!result.Allowed) return result;
        }
        return ArmorCompatibilityResult.Success;
    }

    public static bool TryReadLayered(ITreeAttribute types, string piece, out LayeredArmorState state)
    {
        string? cover = types.GetString(Key("cover", piece));
        string? trim = types.GetString(Key("strip", piece));
        string? decoration = types.GetString(Key("decoration", piece));
        string? color = types.GetString(Key("color", piece));
        state = new(cover ?? "", trim ?? "", decoration ?? "", color ?? "");
        return cover != null && trim != null && decoration != null && color != null;
    }

    public static bool TryReadThreeColor(ITreeAttribute types, string piece, out ThreeColorArmorState state)
    {
        string? color1 = types.GetString(Key("color1", piece));
        string? color2 = types.GetString(Key("color2", piece));
        string? color3 = types.GetString(Key("color3", piece));
        state = new(color1 ?? "", color2 ?? "", color3 ?? "");
        return color1 != null && color2 != null && color3 != null;
    }

    public static TreeAttribute CreateLayeredTypes(string piece, LayeredArmorState state)
    {
        var types = new TreeAttribute();
        WriteLayered(types, piece, state);
        return types;
    }

    public static TreeAttribute CreateThreeColorTypes(string piece, ThreeColorArmorState state)
    {
        var types = new TreeAttribute();
        WriteThreeColor(types, piece, state);
        return types;
    }

    public static void WriteLayered(ITreeAttribute types, string piece, LayeredArmorState state)
    {
        types.SetString(Key("cover", piece), state.Cover);
        types.SetString(Key("strip", piece), state.Trim);
        types.SetString(Key("decoration", piece), state.Decoration);
        types.SetString(Key("color", piece), state.Color);
    }

    public static void WriteThreeColor(ITreeAttribute types, string piece, ThreeColorArmorState state)
    {
        types.SetString(Key("color1", piece), state.Color1);
        types.SetString(Key("color2", piece), state.Color2);
        types.SetString(Key("color3", piece), state.Color3);
    }

    public static void ResetDecorations(ITreeAttribute types, ResolvedArmor armor)
    {
        if (armor.Definition.Schema == ArmorAttributeSchema.Layered)
        {
            types.SetString(Key("decoration", armor.Piece), LayeredNeutral);
            types.SetString(Key("color", armor.Piece), LayeredNeutral);
            return;
        }

        WriteThreeColor(types, armor.Piece, new ThreeColorArmorState(ThreeColorNeutral, ThreeColorNeutral, ThreeColorNeutral));
    }

    private static string Key(string value, string piece) => value + piece;
}
