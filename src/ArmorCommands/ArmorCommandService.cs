using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace FACore.ArmorCommands;

public sealed class ArmorCommandService
{
    private static readonly string[] PieceOrder = ["head", "body", "legs"];

    private readonly ICoreServerAPI api;

    public ArmorCommandService(ICoreServerAPI api)
    {
        this.api = api;
    }

    public TextCommandResult Handle(TextCommandCallingArgs args, ArmorDefinition definition)
    {
        using var languageScope = FaText.ForPlayer(args.Caller.Player);
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error(FaText.Get("This command must be run by an in-game player."));
        }

        if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
        {
            return TextCommandResult.Error(FaText.Get("Forgotten Armory armor commands can only be used in Creative mode. Switch to Creative mode and try again."));
        }

        string style = ArmorCompatibility.Normalize(args[ArmorCommandSyntax.StyleIndex] as string);
        string pieceToken = ArmorCompatibility.Normalize(args[ArmorCommandSyntax.PieceIndex] as string);
        string metal = ArmorCompatibility.NormalizeMetal(args[ArmorCommandSyntax.MetalIndex] as string);

        if (!definition.Styles.Contains(style))
        {
            return Invalid("Style", style, definition.Styles);
        }

        if (!definition.BaseMetals.Contains(metal))
        {
            return Invalid("Metal", metal, definition.BaseMetals);
        }

        if (!TryResolvePieces(pieceToken, definition, style, out string[] pieces, out string pieceError))
        {
            return TextCommandResult.Error(pieceError);
        }

        return definition.Schema == ArmorAttributeSchema.Layered
            ? HandleLayered(player, definition, style, pieces, metal, ReadLayeredState(args))
            : HandleThreeColor(player, definition, style, pieces, metal, ReadThreeColorState(args));
    }

    private TextCommandResult HandleLayered(
        IServerPlayer player,
        ArmorDefinition definition,
        string style,
        string[] pieces,
        string metal,
        LayeredArmorState requestedState)
    {
        foreach (string piece in pieces)
        {
            var resolved = new ResolvedArmor(definition, definition.Pieces[piece], piece, style, metal);
            ArmorCompatibilityResult result = ArmorCompatibility.ValidateFinalLayered(resolved, requestedState);
            if (!result.Allowed)
            {
                return CompatibilityError(result, resolved, requestedState.Decoration);
            }
        }

        return ResolveConstructAndGive(player, definition, style, pieces, metal,
            piece => ArmorCompatibility.CreateLayeredTypes(piece, requestedState),
            FaText.Get("metal={0}, cover={1}, trim={2}, decoration={3}, color={4}", metal, requestedState.Cover, requestedState.Trim, requestedState.Decoration, requestedState.Color),
            requestedState.Decoration);
    }

    private TextCommandResult HandleThreeColor(
        IServerPlayer player,
        ArmorDefinition definition,
        string style,
        string[] pieces,
        string metal,
        ThreeColorArmorState requestedState)
    {
        foreach (string piece in pieces)
        {
            var resolved = new ResolvedArmor(definition, definition.Pieces[piece], piece, style, metal);
            ArmorCompatibilityResult result = ArmorCompatibility.ValidateFinalThreeColor(resolved, requestedState);
            if (!result.Allowed) return CompatibilityError(result, resolved);
        }

        return ResolveConstructAndGive(player, definition, style, pieces, metal,
            piece => ArmorCompatibility.CreateThreeColorTypes(piece, requestedState),
            FaText.Get("metal={0}, colors={1}/{2}/{3}", metal, requestedState.Color1, requestedState.Color2, requestedState.Color3),
            null);
    }

    private static LayeredArmorState ReadLayeredState(TextCommandCallingArgs args)
    {
        return new LayeredArmorState(
            ArmorCompatibility.NormalizeMetal(args[ArmorCommandSyntax.LayeredCoverIndex] as string, ArmorCompatibility.LayeredNeutral),
            ArmorCompatibility.NormalizeMetal(args[ArmorCommandSyntax.LayeredTrimIndex] as string, ArmorCompatibility.LayeredNeutral),
            ArmorCompatibility.Normalize(args[ArmorCommandSyntax.LayeredDecorationIndex] as string, ArmorCompatibility.LayeredNeutral),
            ArmorCompatibility.Normalize(args[ArmorCommandSyntax.LayeredColorIndex] as string, ArmorCompatibility.LayeredNeutral)
        );
    }

    private static ThreeColorArmorState ReadThreeColorState(TextCommandCallingArgs args)
    {
        return new ThreeColorArmorState(
            ArmorCompatibility.Normalize(args[ArmorCommandSyntax.ThreeColorFirstIndex] as string, ArmorCompatibility.ThreeColorNeutral),
            ArmorCompatibility.Normalize(args[ArmorCommandSyntax.ThreeColorSecondIndex] as string, ArmorCompatibility.ThreeColorNeutral),
            ArmorCompatibility.Normalize(args[ArmorCommandSyntax.ThreeColorThirdIndex] as string, ArmorCompatibility.ThreeColorNeutral)
        );
    }

    private TextCommandResult ResolveConstructAndGive(
        IServerPlayer player,
        ArmorDefinition definition,
        string style,
        string[] pieces,
        string metal,
        System.Func<string, TreeAttribute> createTypes,
        string description,
        string? decoration)
    {
        var stacks = new List<ItemStack>(pieces.Length);
        foreach (string piece in pieces)
        {
            string path = ArmorResolver.CreateItemPath(definition.Pieces[piece], style, metal);
            var code = new AssetLocation(definition.Domain, path);
            Item? item = api.World.GetItem(code);
            if (item == null || item.Code == null || !item.Code.Equals(code))
            {
                return TextCommandResult.Error(
                    FaText.Get("{0} expected item '{1}' is unavailable. The external armor mod may be missing, disabled, incompatible with this manual definition, or changed so the manual item definition is stale.", definition.DisplayName, code));
            }

            var stack = new ItemStack(item);
            stack.Attributes["types"] = createTypes(piece);
            ArmorCompatibilityResult resolution = ArmorResolver.Resolve(stack, out ResolvedArmor? resolved);
            if (!resolution.Allowed || resolved == null)
            {
                return TextCommandResult.Error(FaText.Get("Expected item '{0}' is not uniquely recognized by the canonical armor definitions.", code));
            }
            ArmorCompatibilityResult stateResult = ArmorCompatibility.ValidateStateContainer(stack, resolved, out _);
            if (!stateResult.Allowed) return CompatibilityError(stateResult, resolved, decoration);
            stacks.Add(stack);
        }

        // Giving begins only after every requested piece has validated, resolved, and constructed.
        foreach (ItemStack stack in stacks)
        {
            GiveOrDrop(player, stack);
        }

        return TextCommandResult.Success(FaText.Get("Spawned {0} {1} with {2}.", definition.DisplayName, string.Join(", ", pieces), description));
    }

    private static bool TryResolvePieces(
        string token,
        ArmorDefinition definition,
        string style,
        out string[] pieces,
        out string error)
    {
        if (!ArmorCommandSyntax.TryResolvePiece(token, out string piece))
        {
            pieces = [];
            error = FaText.Get("Piece must be head/helmet/armet/helm, body/chest/chestplate/torso, legs/leg/pants/leggings, or all/set.");
            return false;
        }

        IReadOnlySet<string> supported = definition.PiecesByStyle[style];
        pieces = string.Equals(piece, "all", StringComparison.Ordinal)
            ? PieceOrder.Where(supported.Contains).ToArray()
            : [piece];

        if (pieces.Length == 0 || pieces.Any(candidate => !supported.Contains(candidate)))
        {
            error = FaText.Get("Piece '{0}' is not supported by {1} style '{2}'. Valid pieces: {3}.", piece, definition.DisplayName, style, Join(supported));
            return false;
        }

        error = "";
        return true;
    }

    private static TextCommandResult CompatibilityError(ArmorCompatibilityResult result, ResolvedArmor armor, string? decoration = null) => result.Issue switch
    {
        ArmorCompatibilityIssue.InvalidCover => Invalid("Cover", result.Value, GetMetalValues(armor.Definition.CoversByBaseMetal, armor.BaseMetal)),
        ArmorCompatibilityIssue.InvalidTrim => Invalid("Trim", result.Value, GetMetalValues(armor.Definition.TrimsByBaseMetal, armor.BaseMetal)),
        ArmorCompatibilityIssue.InvalidDecoration => Invalid("Decoration", result.Value, armor.PieceDefinition.Decorations),
        ArmorCompatibilityIssue.InvalidDecorationColor when decoration != null =>
            InvalidDecorationColor(result.Value, armor, decoration),
        ArmorCompatibilityIssue.InvalidDecorationColor => TextCommandResult.Error(FaText.Get("Decoration color '{0}' is invalid.", result.Value)),
        ArmorCompatibilityIssue.InvalidDecorationColorPairing => TextCommandResult.Error(FaText.Get("Decoration 'none' requires color 'none', and a real decoration requires a real color.")),
        ArmorCompatibilityIssue.InvalidThreeColorValue => Invalid("Color", result.Value, armor.Definition.Colors),
        _ => TextCommandResult.Error(FaText.Get("The requested {0} {1} state is invalid ({2}).", armor.Definition.DisplayName, armor.Piece, result.Issue))
    };

    private static TextCommandResult Invalid(string label, string value, IEnumerable<string> valid) =>
        TextCommandResult.Error(FaText.Get("{0} '{1}' is invalid. Valid values: {2}.", FaText.Get(label), value, Join(valid)));

    private static TextCommandResult InvalidDecorationColor(string value, ResolvedArmor armor, string decoration)
    {
        string[] valid = ArmorCompatibility.GetValidDecorationColors(armor, decoration).ToArray();
        return valid.Length > 0
            ? Invalid(FaText.Get("Decoration color"), value, valid)
            : TextCommandResult.Error(FaText.Get("Decoration color '{0}' is invalid; no colors are valid for decoration '{1}'.", value, decoration));
    }

    private static IEnumerable<string> GetMetalValues(IReadOnlyDictionary<string, IReadOnlySet<string>> rules, string metal) =>
        rules.TryGetValue(metal, out IReadOnlySet<string>? values) ? values : Array.Empty<string>();

    private static string Join(IEnumerable<string> values) =>
        string.Join(", ", values.OrderBy(value => value, StringComparer.Ordinal));

    private static void GiveOrDrop(IServerPlayer player, ItemStack stack)
    {
        if (!player.InventoryManager.TryGiveItemstack(stack, true))
        {
            player.Entity.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ.Add(0, 0.5, 0));
        }
    }
}
