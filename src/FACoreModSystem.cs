using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace FACore;

public class FACoreModSystem : ModSystem
{
    private static readonly HashSet<string> GreenwichBaseMetals = new(StringComparer.Ordinal)
    {
        "iron",
        "meteoriciron",
        "steel"
    };

    private static readonly HashSet<string> GreenwichCovers = new(StringComparer.Ordinal)
    {
        "none",
        "bismuth",
        "bismuthbronze",
        "copper",
        "cupronickel",
        "brass",
        "zinc",
        "blackbronze",
        "lead",
        "silver",
        "tinbronze",
        "meteoriciron",
        "gold",
        "electrum",
        "uranium"
    };

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("FAStation", typeof(BlockFAStation));
        api.RegisterBlockEntityClass("FAStation", typeof(BlockEntityFACoverStation));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.ChatCommands
            .GetOrCreate("fac")
            .WithDescription("Forgotten Armory commands")
            .RequiresPrivilege(Privilege.gamemode)
            .BeginSubCommand("greenwich")
                .WithDescription("Spawn Greenwich armor: /fac greenwich <head|body|legs|all> <iron|meteoriciron|steel> [cover]")
                .WithArgs(
                    api.ChatCommands.Parsers.Word("piece"),
                    api.ChatCommands.Parsers.Word("metal"),
                    api.ChatCommands.Parsers.OptionalWord("cover"))
                .HandleWith(args => OnArmorCommand(api, args, "fagreenwich", "Greenwich"))
            .EndSubCommand()
            .BeginSubCommand("gothic")
                .WithDescription("Spawn Gothic armor: /fac gothic <head|body|legs|all> <iron|meteoriciron|steel> [cover]")
                .WithArgs(
                    api.ChatCommands.Parsers.Word("piece"),
                    api.ChatCommands.Parsers.Word("metal"),
                    api.ChatCommands.Parsers.OptionalWord("cover"))
                .HandleWith(args => OnArmorCommand(api, args, "fagothic", "Gothic"))
            .EndSubCommand();
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);

        foreach (Block block in api.World.Blocks)
        {
            if (block.Code?.Domain != "facore" || !block.Code.Path.StartsWith("fa-workstation-cover-"))
            {
                continue;
            }

            block.SelectionBoxes = [new Cuboidf(0f, 0f, 0f, 1f, 1.5f, 1f)];
            block.CollisionBoxes = [new Cuboidf(0f, 0f, 0f, 1f, 1.5f, 1f)];
            block.ParticleCollisionBoxes = block.CollisionBoxes;
        }
    }

    private static TextCommandResult OnArmorCommand(ICoreServerAPI api, TextCommandCallingArgs args, string domain, string familyName)
    {
        if (args.Caller.Player is not IServerPlayer player)
        {
            return TextCommandResult.Error("This command must be run by an in-game player.");
        }

        string pieceArg = NormalizeGreenwichToken((args[0] as string) ?? "");
        string metal = NormalizeGreenwichMetal((args[1] as string) ?? "");
        string cover = NormalizeGreenwichCover((args[2] as string) ?? "none");

        if (!GreenwichBaseMetals.Contains(metal))
        {
            return TextCommandResult.Error("Material must be iron, meteoriciron, or steel.");
        }

        if (!GreenwichCovers.Contains(cover))
        {
            return TextCommandResult.Error("Cover must be one of: " + string.Join(", ", GreenwichCovers));
        }

        string[] pieces = ResolveGreenwichPieces(pieceArg);
        if (pieces.Length == 0)
        {
            return TextCommandResult.Error("Piece must be head/helmet/armet, body/chest, legs, or all/set.");
        }

        var given = new List<string>();
        var missing = new List<string>();

        foreach (string piece in pieces)
        {
            ItemStack? stack = CreateFAArmorStack(api, domain, piece, metal, cover);
            if (stack == null)
            {
                missing.Add(piece);
                continue;
            }

            GiveOrDrop(player, stack);
            given.Add(piece);
        }

        if (given.Count == 0)
        {
            return TextCommandResult.Error($"No {familyName} armor items are registered. Is {domain} loaded/enabled?");
        }

        string message = $"Spawned {familyName} {string.Join(", ", given)} with base={metal}, cover={cover}.";
        if (missing.Count > 0)
        {
            message += " Missing item(s): " + string.Join(", ", missing) + ".";
        }

        return TextCommandResult.Success(message);
    }

    private static string[] ResolveGreenwichPieces(string piece)
    {
        if (piece.StartsWith("helmet", StringComparison.Ordinal)
            || piece.StartsWith("head", StringComparison.Ordinal)
            || piece.StartsWith("armet", StringComparison.Ordinal))
        {
            return ["head"];
        }

        return piece switch
        {
            "helm" => ["head"],
            "body" or "chest" or "chestplate" or "torso" => ["body"],
            "legs" or "leg" or "pants" or "leggings" => ["legs"],
            "all" or "set" or "sets" or "armor" or "armour" => ["head", "body", "legs"],
            _ => []
        };
    }

    private static string NormalizeGreenwichMetal(string value)
    {
        string token = NormalizeGreenwichToken(value);
        return token switch
        {
            "meteor" or "meteoric" or "meteoriron" or "meteoriciron" => "meteoriciron",
            _ => token
        };
    }

    private static string NormalizeGreenwichCover(string value)
    {
        string token = NormalizeGreenwichToken(value);
        return token switch
        {
            "" or "no" or "none" or "empty" or "uncovered" => "none",
            "meteor" or "meteoric" or "meteoriron" or "meteoriciron" => "meteoriciron",
            _ => token
        };
    }

    private static string NormalizeGreenwichToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        Span<char> buffer = stackalloc char[value.Length];
        int length = 0;

        foreach (char ch in value)
        {
            if (ch == '-' || ch == '_' || char.IsWhiteSpace(ch))
            {
                continue;
            }

            buffer[length++] = char.ToLowerInvariant(ch);
        }

        return new string(buffer[..length]);
    }

    private static ItemStack? CreateFAArmorStack(ICoreServerAPI api, string domain, string piece, string metal, string cover)
    {
        Item? item = FindFAArmorItem(api, domain, piece, metal);
        if (item == null)
        {
            return null;
        }

        var stack = new ItemStack(item);
        var types = new TreeAttribute();

        switch (piece)
        {
            case "head":
                types.SetString("formhead", "armet");
                types.SetString("basehead", metal);
                types.SetString("coverhead", cover);
                types.SetString("striphead", "none");
                types.SetString("decorationhead", "none");
                types.SetString("colorhead", "none");
                break;

            case "body":
                types.SetString("formbody", "guardbody");
                types.SetString("basebody", metal);
                types.SetString("coverbody", cover);
                types.SetString("stripbody", "none");
                types.SetString("decorationbody", "none");
                types.SetString("colorbody", "none");
                break;

            case "legs":
                types.SetString("formlegs", "guardlegs");
                types.SetString("baselegs", metal);
                types.SetString("coverlegs", cover);
                types.SetString("striplegs", "none");
                types.SetString("decorationlegs", "none");
                types.SetString("colorlegs", "none");
                break;
        }

        stack.Attributes["types"] = types;
        return stack;
    }

    private static Item? FindFAArmorItem(ICoreServerAPI api, string domain, string piece, string metal)
    {
        string prefix = piece switch
        {
            "head" => "platehead-",
            "body" => "platebody-",
            "legs" => "platelegs-",
            _ => ""
        };

        if (prefix.Length == 0)
        {
            return null;
        }

        string suffix = "-" + metal;
        foreach (Item item in api.World.Items)
        {
            AssetLocation? code = item?.Code;
            if (code?.Domain == domain
                && code.Path.StartsWith(prefix, StringComparison.Ordinal)
                && code.Path.EndsWith(suffix, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private static void GiveOrDrop(IServerPlayer player, ItemStack stack)
    {
        if (!player.InventoryManager.TryGiveItemstack(stack, true))
        {
            player.Entity.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ.Add(0, 0.5, 0));
        }
    }
}
