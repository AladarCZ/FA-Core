using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FACore.ArmorCommands;

internal sealed class LegacyArmorPurgeCommand(ICoreServerAPI api)
{
    internal void Register(IChatCommand root) => root.BeginSubCommand("purge")
        .WithDescription(FaText.Get("Migrate your legacy Greenwich, Gothic and Dynasties armor"))
        .RequiresPlayer()
        .HandleWith(Handle)
        .EndSubCommand();

    private TextCommandResult Handle(TextCommandCallingArgs args)
    {
        using var languageScope = FaText.ForPlayer(args.Caller.Player);
        if (args.Caller.Player is not IServerPlayer player)
            return TextCommandResult.Error(FaText.Get("This command must be run by an in-game player."));

        int migrated = 0, skipped = 0, failed = 0;
        foreach (ItemSlot slot in PlayerSlots(player))
        {
            ItemStack? original = slot.Itemstack;
            if (original == null) continue;
            try
            {
                LegacyMigrationStatus status = LegacyArmorMigration.TryReconstruct(original, api.World,
                    out ItemStack? replacement, out string reason);
                if (status == LegacyMigrationStatus.Unrelated) continue;
                if (status == LegacyMigrationStatus.Migrated && replacement != null)
                {
                    if (replacement.StackSize > slot.MaxSlotStackSize || !slot.CanHold(new DummySlot(replacement)))
                    {
                        reason = FaText.Get("The inventory slot cannot hold the reconstructed armor.");
                    }
                    else
                    {
                        // Construct and validate first. MarkDirty also triggers inventory/bag/equipment
                        // callbacks. Roll back if a callback throws, retaining the original stack.
                        slot.Itemstack = replacement;
                        slot.MarkDirty();
                        migrated++;
                        continue;
                    }
                }
                skipped++;
                api.Logger.Warning("[FACore] purge skipped {0}: {1}", LegacyArmorMigration.Describe(original), reason);
            }
            catch (Exception exception)
            {
                if (!ReferenceEquals(slot.Itemstack, original))
                {
                    slot.Itemstack = original;
                    try { slot.MarkDirty(); }
                    catch (Exception rollbackError) { api.Logger.Error("[FACore] purge rollback notification failed: {0}", rollbackError); }
                }
                failed++;
                api.Logger.Error("[FACore] purge failed {0}: {1}", LegacyArmorMigration.Describe(original), exception);
            }
        }
        return TextCommandResult.Success(FaText.Get("FA purge complete: {0} migrated, {1} skipped, {2} failed.", migrated, skipped, failed));
    }

    internal static IEnumerable<ItemSlot> PlayerSlots(IPlayer player)
    {
        var seen = new HashSet<ItemSlot>(ReferenceEqualityComparer.Instance);
        // InventoryBasePlayer is VS's persisted, on-player inventory contract. Include custom
        // personal inventories, but exclude creative catalog templates and opened world chests.
        // Backpack enumeration exposes its bag-content slots through the inventory API.
        foreach (InventoryBase inventory in player.InventoryManager.InventoriesOrdered.ToArray())
        {
            if (inventory is not InventoryBasePlayer personal || personal.Player?.PlayerUID != player.PlayerUID
                || inventory.ClassName == GlobalConstants.creativeInvClassName) continue;
            foreach (ItemSlot slot in inventory)
                if (slot != null && seen.Add(slot)) yield return slot;
        }
        ItemSlot? mouse = player.InventoryManager.MouseItemSlot;
        if (mouse != null && seen.Add(mouse)) yield return mouse;
    }
}
