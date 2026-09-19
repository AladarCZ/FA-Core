using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FACore.ArmorCommands;

internal static class ArmorCommandSyntax
{
    public const int StyleIndex = 0;
    public const int PieceIndex = 1;
    public const int MetalIndex = 2;
    public const int LayeredCoverIndex = 3;
    public const int LayeredTrimIndex = 4;
    public const int LayeredDecorationIndex = 5;
    public const int LayeredColorIndex = 6;
    public const int ThreeColorFirstIndex = 3;
    public const int ThreeColorSecondIndex = 4;
    public const int ThreeColorThirdIndex = 5;

    public const string StyleName = "style";
    public const string PieceName = "piece";
    public const string MetalName = "metal";
    public const string CoverName = "cover";
    public const string TrimName = "trim";
    public const string DecorationName = "decoration";
    public const string ColorName = "color";
    public const string Color1Name = "color1";
    public const string Color2Name = "color2";
    public const string Color3Name = "color3";

    private static readonly IReadOnlyDictionary<string, string> PieceAliases =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["head"] = "head",
            ["helmet"] = "head",
            ["armet"] = "head",
            ["helm"] = "head",
            ["body"] = "body",
            ["chest"] = "body",
            ["chestplate"] = "body",
            ["torso"] = "body",
            ["legs"] = "legs",
            ["leg"] = "legs",
            ["pants"] = "legs",
            ["leggings"] = "legs",
            ["all"] = "all",
            ["set"] = "all",
            ["sets"] = "all",
            ["armor"] = "all",
            ["armour"] = "all"
        });

    public static IEnumerable<string> PieceOptions => PieceAliases.Keys;

    public static bool TryResolvePiece(string alias, out string piece) =>
        PieceAliases.TryGetValue(alias, out piece!);
}
