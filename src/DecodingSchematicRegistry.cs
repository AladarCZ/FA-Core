using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace FACore;

internal static class DecodingSchematicRegistry
{
    private static readonly object syncRoot = new();
    private static ItemStack[] prototypes = [];

    public static int Count
    {
        get
        {
            lock (syncRoot) return prototypes.Length;
        }
    }

    public static void Build(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server)
        {
            return;
        }

        ItemStack[] discovered = api.World.Items
            .Where(item => item.Code != null
                && item.Code.Domain != "facore"
                && item.Code.Domain.StartsWith("fa", StringComparison.Ordinal)
                && item.Attributes?["faDecodingResult"].AsBool(false) == true)
            .GroupBy(item => item.Code.ToString(), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.Code.ToString(), StringComparer.Ordinal)
            .Select(item => new ItemStack(item, 1))
            .ToArray();

        lock (syncRoot)
        {
            prototypes = discovered;
        }

        api.Logger.Notification("[FACore DecodingTable] Registered {0} decoding schematic result(s): {1}",
            discovered.Length,
            string.Join(", ", discovered.Select(stack => stack.Collectible.Code)));
    }

    public static ItemStack[] GetSnapshot()
    {
        lock (syncRoot)
        {
            return prototypes.Select(stack => stack.Clone()).ToArray();
        }
    }
}
