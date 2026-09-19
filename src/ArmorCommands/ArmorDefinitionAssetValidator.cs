using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace FACore.ArmorCommands;

internal static class ArmorDefinitionAssetValidator
{
    private static readonly string[] ThreeColorShapeVariants = ["none", "fancy"];

    public static void Validate(ICoreAPI api)
    {
        foreach (ArmorDefinition definition in ArmorDefinitions.All.Values)
        {
            if (!api.ModLoader.IsModEnabled(definition.Domain)) continue;
            ValidateDefinition(api, definition);
        }
    }

    private static void ValidateDefinition(ICoreAPI api, ArmorDefinition definition)
    {
        var checkedItems = new HashSet<AssetLocation>();
        var checkedAssets = new HashSet<AssetLocation>();

        foreach (string style in definition.Styles)
        {
            if (!definition.PiecesByStyle.TryGetValue(style, out IReadOnlySet<string>? supportedPieces))
            {
                api.Logger.Error("[FACore] Armor definition '{0}' has no piece rules for declared style '{1}'.", definition.Identifier, style);
                continue;
            }

            foreach (string pieceName in supportedPieces)
            {
                if (!definition.Pieces.TryGetValue(pieceName, out ArmorPieceDefinition? piece))
                {
                    api.Logger.Error("[FACore] Armor definition '{0}' style '{1}' references undeclared piece '{2}'.", definition.Identifier, style, pieceName);
                    continue;
                }

                foreach (string color in definition.Colors)
                {
                    ValidateAsset(api, definition, ArmorRenderAssets.TextureFile(ArmorRenderAssets.DecorationTexture(piece.Piece, color)),
                        FaText.Get("decoration texture"), checkedAssets);
                }

                if (piece.Decorations.Contains("bear"))
                {
                    foreach (string color in ArmorDefinitions.BearColors)
                    {
                        ValidateAsset(api, definition, ArmorRenderAssets.TextureFile(ArmorRenderAssets.BearTexture(color)),
                            FaText.Get("bear texture"), checkedAssets);
                    }
                }

                if (piece.Decorations.Contains("wolf"))
                {
                    foreach (string color in ArmorDefinitions.BearColors)
                    {
                        ValidateAsset(api, definition, ArmorRenderAssets.TextureFile(ArmorRenderAssets.WolfTexture(color)),
                            FaText.Get("wolf texture"), checkedAssets);
                    }
                }

                foreach (string baseMetal in definition.BaseMetals)
                {
                    ValidateItem(api, definition, piece, style, baseMetal, checkedItems);
                    ValidateTexture(api, definition, piece, style, baseMetal, ArmorCompatibility.LayeredNeutral,
                        definition.Rendering.BaseTextureTemplate, FaText.Get("base texture"), checkedAssets);

                    if (definition.Rendering.CoverTextureTemplate != null
                        && definition.CoversByBaseMetal.TryGetValue(baseMetal, out IReadOnlySet<string>? covers))
                    {
                        foreach (string cover in covers)
                        {
                            if (cover == ArmorCompatibility.LayeredNeutral) continue;
                            ValidateTexture(api, definition, piece, style, baseMetal, cover,
                                definition.Rendering.CoverTextureTemplate, FaText.Get("cover texture"), checkedAssets);
                        }
                    }

                    if (definition.TrimsByBaseMetal.TryGetValue(baseMetal, out IReadOnlySet<string>? trims))
                    {
                        foreach (string trim in trims)
                        {
                            ValidateAsset(api, definition, ArmorRenderAssets.TextureFile(ArmorRenderAssets.TrimTexture(trim)),
                                FaText.Get("trim texture"), checkedAssets);
                        }
                    }

                    IEnumerable<string> shapeVariants = definition.Schema == ArmorAttributeSchema.Layered
                        ? piece.Decorations
                        : ThreeColorShapeVariants;
                    foreach (string variant in shapeVariants)
                    {
                        string shapePath = Expand(definition.Rendering.ShapeTemplate, piece, style, baseMetal, variant, ArmorCompatibility.LayeredNeutral);
                        ValidateAsset(api, definition, new AssetLocation(definition.Domain, shapePath), "shape", checkedAssets);
                    }
                }
            }
        }
    }

    private static void ValidateItem(
        ICoreAPI api,
        ArmorDefinition definition,
        ArmorPieceDefinition piece,
        string style,
        string baseMetal,
        HashSet<AssetLocation> checkedItems)
    {
        string path = piece.ItemCodeTemplate
            .Replace("{style}", style, StringComparison.Ordinal)
            .Replace("{metal}", baseMetal, StringComparison.Ordinal);
        var code = new AssetLocation(definition.Domain, path);
        if (!checkedItems.Add(code)) return;

        Item? item = api.World.GetItem(code);
        if (item?.Code?.Equals(code) != true)
        {
            api.Logger.Error("[FACore] Armor definition '{0}' references missing item '{1}'.", definition.Identifier, code);
        }
    }

    private static void ValidateTexture(
        ICoreAPI api,
        ArmorDefinition definition,
        ArmorPieceDefinition piece,
        string style,
        string baseMetal,
        string cover,
        string template,
        string assetKind,
        HashSet<AssetLocation> checkedAssets)
    {
        string texturePath = Expand(template, piece, style, baseMetal, "none", cover);
        ValidateAsset(api, definition, new AssetLocation(definition.Domain, $"textures/{texturePath}.png"), assetKind, checkedAssets);
    }

    private static void ValidateAsset(
        ICoreAPI api,
        ArmorDefinition definition,
        AssetLocation location,
        string assetKind,
        HashSet<AssetLocation> checkedAssets)
    {
        if (!checkedAssets.Add(location) || api.Assets.TryGet(location) != null) return;
        api.Logger.Error("[FACore] Armor definition '{0}' references missing {1} asset '{2}'.", definition.Identifier, assetKind, location);
    }

    private static string Expand(
        string template,
        ArmorPieceDefinition piece,
        string style,
        string baseMetal,
        string variant,
        string cover) =>
        template
            .Replace("{slot}", piece.SlotPrefix, StringComparison.Ordinal)
            .Replace("{style}", style, StringComparison.Ordinal)
            .Replace("{piece}", piece.Piece, StringComparison.Ordinal)
            .Replace("{metal}", baseMetal, StringComparison.Ordinal)
            .Replace("{cover}", cover, StringComparison.Ordinal)
            .Replace("{variant}", variant, StringComparison.Ordinal);
}
