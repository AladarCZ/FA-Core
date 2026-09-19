using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Vintagestory.API.Common;

namespace FACore;

internal readonly record struct DecorationColorSlot(string Piece, int Index)
{
    public string ArmorAttributeKey => $"color{Index}{Piece}";
    public string ClothTreeKey => $"decoration{Piece}Color{Index}ClothStack";
    public string OriginalTreeKey => $"decoration{Piece}OriginalColor{Index}";
}

internal sealed class DecorationThreeColorState
{
    public const int ColorsPerPiece = 3;

    private static readonly IReadOnlyList<DecorationColorSlot> slots = new ReadOnlyCollection<DecorationColorSlot>(
    [
        new("head", 1), new("head", 2), new("head", 3),
        new("body", 1), new("body", 2), new("body", 3),
        new("legs", 1), new("legs", 2), new("legs", 3)
    ]);

    private readonly Dictionary<string, PieceState> pieces = new(StringComparer.Ordinal)
    {
        ["head"] = new(),
        ["body"] = new(),
        ["legs"] = new()
    };

    public static IReadOnlyList<DecorationColorSlot> Slots => slots;

    public ItemStack? GetCloth(string piece, int index) =>
        TryGet(piece, index, out PieceState? state, out int arrayIndex) ? state.Cloth[arrayIndex] : null;

    public void SetCloth(string piece, int index, ItemStack? stack)
    {
        if (TryGet(piece, index, out PieceState? state, out int arrayIndex)) state.Cloth[arrayIndex] = stack;
    }

    public bool TryGetFirstEmptySlot(string piece, out int index)
    {
        index = 0;
        if (!pieces.TryGetValue(piece, out PieceState? state)) return false;

        for (int arrayIndex = 0; arrayIndex < state.Cloth.Length; arrayIndex++)
        {
            if (state.Cloth[arrayIndex] != null) continue;
            index = arrayIndex + 1;
            return true;
        }

        return false;
    }

    public bool TrySetClothIfEmpty(string piece, int index, ItemStack stack)
    {
        if (!TryGet(piece, index, out PieceState? state, out int arrayIndex)
            || state.Cloth[arrayIndex] != null)
        {
            return false;
        }

        state.Cloth[arrayIndex] = stack;
        return true;
    }

    public string GetOriginal(string piece, int index) =>
        TryGet(piece, index, out PieceState? state, out int arrayIndex) ? state.Original[arrayIndex] : "";

    public void SetOriginal(string piece, int index, string value)
    {
        if (TryGet(piece, index, out PieceState? state, out int arrayIndex)) state.Original[arrayIndex] = value;
    }

    public int CountCloth(string piece)
    {
        if (!pieces.TryGetValue(piece, out PieceState? state)) return 0;
        int count = 0;
        foreach (ItemStack? stack in state.Cloth)
        {
            if (stack != null) count++;
        }
        return count;
    }

    public void ClearOriginals(string piece)
    {
        if (!pieces.TryGetValue(piece, out PieceState? state)) return;
        Array.Fill(state.Original, "");
    }

    private bool TryGet(string piece, int index, out PieceState state, out int arrayIndex)
    {
        arrayIndex = index - 1;
        if (arrayIndex >= 0 && arrayIndex < ColorsPerPiece && pieces.TryGetValue(piece, out PieceState? found))
        {
            state = found;
            return true;
        }

        state = null!;
        return false;
    }

    private sealed class PieceState
    {
        public ItemStack?[] Cloth { get; } = new ItemStack?[ColorsPerPiece];
        public string[] Original { get; } = ["", "", ""];
    }
}
