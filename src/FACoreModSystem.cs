using System;
using System.Collections.Generic;
using System.Linq;
using FACore.ArmorCommands;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FACore;

public class FACoreModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("FAStation", typeof(BlockFAStation));
        api.RegisterBlockEntityClass("FAStation", typeof(BlockEntityFAWorkStation));
        api.RegisterBlockEntityClass("FADecodingTable", typeof(BlockEntityFADecodingTable));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        var armorCommands = new ArmorCommandService(api);
        var rootCommand = api.ChatCommands
            .GetOrCreate("fac")
            .WithDescription(FaText.Get("Forgotten Armory commands"))
            .RequiresPrivilege(Privilege.gamemode);

        new LegacyArmorPurgeCommand(api).Register(rootCommand);

        foreach (ArmorDefinition definition in ArmorDefinitions.All.Values)
        {
            if (!api.ModLoader.IsModEnabled(definition.Domain)) continue;
            rootCommand.BeginSubCommand(definition.CommandName)
                .WithDescription(FaText.Get("Spawn {0} armor", definition.DisplayName))
                .WithArgs(CreateParsers(definition))
                .HandleWith(args => armorCommands.Handle(args, definition))
                .EndSubCommand();
        }
    }

    private static ICommandArgumentParser[] CreateParsers(ArmorDefinition definition)
    {
        string[] metals = definition.BaseMetals
            .Concat(new[] { "meteor", "meteoric", "meteoriron", "meteoric-iron" })
            .Distinct(StringComparer.Ordinal).ToArray();
        if (definition.Schema == ArmorAttributeSchema.ThreeColor)
        {
            return
            [
                RequiredRange(ArmorCommandSyntax.StyleName, Sorted(definition.Styles)),
                RequiredRange(ArmorCommandSyntax.PieceName, Sorted(ArmorCommandSyntax.PieceOptions)),
                RequiredRange(ArmorCommandSyntax.MetalName, Sorted(metals)),
                OptionalRange(ArmorCommandSyntax.Color1Name, Sorted(definition.Colors)),
                OptionalRange(ArmorCommandSyntax.Color2Name, Sorted(definition.Colors)),
                OptionalRange(ArmorCommandSyntax.Color3Name, Sorted(definition.Colors))
            ];
        }

        string[] decorations = definition.Pieces.Values
            .SelectMany(piece => piece.Decorations)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return
        [
            RequiredRange(ArmorCommandSyntax.StyleName, Sorted(definition.Styles)),
            RequiredRange(ArmorCommandSyntax.PieceName, Sorted(ArmorCommandSyntax.PieceOptions)),
            RequiredRange(ArmorCommandSyntax.MetalName, Sorted(metals)),
            OptionalRange(ArmorCommandSyntax.CoverName, Sorted(definition.CoversByBaseMetal.Values.SelectMany(values => values).Distinct(StringComparer.Ordinal))),
            OptionalRange(ArmorCommandSyntax.TrimName, Sorted(definition.TrimsByBaseMetal.Values.SelectMany(values => values).Distinct(StringComparer.Ordinal))),
            OptionalRange(ArmorCommandSyntax.DecorationName, decorations),
            OptionalRange(ArmorCommandSyntax.ColorName, Sorted(definition.Colors))
        ];
    }

    private static ICommandArgumentParser RequiredRange(string name, params string[] values) =>
        new ConciseWordRangeArgParser(name, true, values);

    private static ICommandArgumentParser OptionalRange(string name, params string[] values) =>
        new ConciseWordRangeArgParser(name, false, values);

    private static string[] Sorted(IEnumerable<string> values) =>
        values.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private sealed class ConciseWordRangeArgParser : WordRangeArgParser
    {
        public ConciseWordRangeArgParser(string argumentName, bool isMandatory, params string[] values)
            : base(argumentName, isMandatory, values)
        {
        }

        public override string GetSyntax() =>
            IsMandatoryArg ? $"<i>&lt;{ArgumentName}&gt;</i>" : $"<i>[{ArgumentName}]</i>";

        public override string GetSyntaxUnformatted() =>
            IsMandatoryArg ? $"&lt;{ArgumentName}&gt;" : $"[{ArgumentName}]";

        public override string GetSyntaxExplanation(string indent) => "";
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        ArmorDefinitionAssetValidator.Validate(api);
        DecodingSchematicRegistry.Build(api);

        foreach (Block block in api.World.Blocks)
        {
            if (!IsOwnedWorkstationBlock(block))
            {
                continue;
            }

            // Keep these raw fields populated after asset resolution: placement gates on the raw
            // collision field, while base selection fallbacks and particle paths consume these fields.
            block.SelectionBoxes = StationBounds.CreateStaticSelectionEnvelopeBoxes();
            block.CollisionBoxes = StationBounds.CreatePhysicalCollisionBoxes();
            block.ParticleCollisionBoxes = block.CollisionBoxes;
        }
    }

    private static bool IsOwnedWorkstationBlock(Block block)
    {
        if (block is not BlockFAStation || block.Code?.Domain != "facore")
        {
            return false;
        }

        string? purpose = block.Variant["purpose"];
        string? side = block.Variant["side"];
        return purpose is "decoration" or "cover" or "trim" or "decoding"
            && side is "north" or "east" or "south" or "west"
            && block.Code.Path == $"fa-workstation-{purpose}-{side}";
    }

}
