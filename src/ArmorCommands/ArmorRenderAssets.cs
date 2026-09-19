using Vintagestory.API.Common;

namespace FACore.ArmorCommands;

internal static class ArmorRenderAssets
{
    public static AssetLocation DecorationTexture(string piece, string color) =>
        new("facore", $"block/decorations/{piece}/{color}");

    public static AssetLocation BearTexture(string color) =>
        new("facore", $"armor/entity/bear/{color}");

    public static AssetLocation WolfTexture(string color) =>
        new("facore", $"armor/entity/wolf/{color}");

    public static AssetLocation TrimTexture(string metal) =>
        new("facore", $"armor/entity/trim/{metal}");

    public static AssetLocation TextureFile(AssetLocation texture) =>
        new(texture.Domain, $"textures/{texture.Path}.png");
}
