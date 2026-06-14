using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace FACore;

public class BlockFAStation : Block, IMultiBlockColSelBoxes, IMultiBlockInteract
{
    private const float FuelIgnitionSeconds = 2f;
    private Cuboidf[]? selectionBoxes;
    private List<StationElementZone> elementZones = [];
    private List<StationElementZone> selectableZones = [];

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (!IsCoverStation()) return;

        SideSolid[BlockFacing.UP.Index] = true;

        Vec3i partOffset = GetPartOffset();
        elementZones = StationShapeElementReader.LoadElementZones(api, this);
        selectableZones = BuildSelectableZones(elementZones, partOffset, null);
        selectionBoxes = BuildSelectionBoxes(selectableZones, partOffset);
        api.Logger.Notification(
            "[FACore Station] {0}: purpose={1}, legacyParts={2}, elementZones={3}, selectableZones={4}, selectionBoxes={5}",
            Code,
            GetPurpose(),
            UsesLegacyParts(),
            elementZones.Count,
            selectableZones.Count,
            selectionBoxes?.Length ?? 0
        );
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        if (!IsCoverStation())
        {
            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        if (!UsesLegacyParts())
        {
            BlockFacing placementSide = GetPlacementSide(byPlayer);
            Block placeBlock = world.BlockAccessor.GetBlock(CodeWithSide(placementSide.Code));
            if (placeBlock is not BlockFAStation placeStation)
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            BlockPos multiblockProxyPos = blockSel.Position.AddCopy(GetProxyOffset(placementSide.Code));
            Block proxyBlock = world.BlockAccessor.GetBlock(multiblockProxyPos);
            if (!proxyBlock.IsReplacableBy(placeBlock) && proxyBlock is not BlockMultiblock)
            {
                failureCode = "notenoughspace";
                return false;
            }

            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot?.MarkDirty();
                return false;
            }

            if (!placeBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            placeBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
            EnsureStationProxy(world, blockSel.Position, placementSide);
            placeStation.EnsureStationController(world, blockSel.Position);
            RefreshPlacedStation(world, blockSel.Position, multiblockProxyPos);
            return true;
        }

        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
        {
            byPlayer.InventoryManager.ActiveHotbarSlot?.MarkDirty();
            return false;
        }

        if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
        {
            return false;
        }

        BlockFacing side = GetPlacementSide(byPlayer);
        BlockPos proxyPos = blockSel.Position.AddCopy(GetProxyOffset(side.Code));
        var proxySelection = new BlockSelection
        {
            Position = proxyPos,
            Face = BlockFacing.UP
        };

        if (!CanPlaceBlock(world, byPlayer, proxySelection, ref failureCode))
        {
            return false;
        }

        world.BlockAccessor.GetBlock(CodeWithParts("main", side.Code)).DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        world.BlockAccessor.GetBlock(CodeWithParts("proxy", side.Code)).DoPlaceBlock(world, byPlayer, proxySelection, itemstack);
        EnsureStationController(world, blockSel.Position);
        RefreshPlacedStation(world, blockSel.Position, proxyPos);
        return true;
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (!IsCoverStation() || !UsesLegacyParts())
        {
            if (IsCoverStation() && !UsesLegacyParts())
            {
                BlockPos proxyPos = pos.AddCopy(GetProxyOffset(GetSide().Code));
                if (world.BlockAccessor.GetBlock(proxyPos) is BlockMultiblock)
                {
                    world.BlockAccessor.SetBlock(0, proxyPos);
                }
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }

        BlockPos mainPos = GetMainPos(pos);
        if (mainPos != pos && world.BlockAccessor.GetBlock(mainPos) is BlockFAStation mainBlock)
        {
            mainBlock.OnBlockBroken(world, mainPos, byPlayer, dropQuantityMultiplier);
            return;
        }

        BlockPos otherPos = GetOtherPartPos(pos);
        if (world.BlockAccessor.GetBlock(otherPos) is BlockFAStation otherStation && otherStation.IsCoverStation() && otherStation.GetPart() != GetPart())
        {
            world.BlockAccessor.SetBlock(0, otherPos);
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        if (IsCoverStation() && !UsesLegacyParts())
        {
            BlockPos proxyPos = pos.AddCopy(GetProxyOffset(GetSide().Code));
            if (world.BlockAccessor.GetBlock(proxyPos) is BlockMultiblock)
            {
                world.BlockAccessor.SetBlock(0, proxyPos);
            }
        }

        if (IsCoverStation() && UsesLegacyParts())
        {
            BlockPos otherPos = GetOtherPartPos(pos);
            Block otherBlock = world.BlockAccessor.GetBlock(otherPos);
            if (otherBlock is BlockFAStation otherStation && otherStation.IsCoverStation() && otherStation.GetPart() != GetPart())
            {
                world.BlockAccessor.SetBlock(0, otherPos);
            }
        }

        base.OnBlockRemoved(world, pos);
    }

    public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack? byItemStack = null)
    {
        base.OnBlockPlaced(world, blockPos, byItemStack);

        if (!IsCoverStation() || UsesLegacyParts())
        {
            return;
        }

        BlockPos proxyPos = blockPos.AddCopy(GetProxyOffset(GetSide().Code));
        EnsureStationProxy(world, blockPos, GetSide());
        EnsureStationController(world, blockPos);
        RefreshPlacedStation(world, blockPos, proxyPos);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (!IsCoverStation())
        {
            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        if (!UsesLegacyParts())
        {
            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        if (GetPart() == "proxy")
        {
            return [];
        }

        return [new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("main", "north")), 1)];
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        if (IsCoverStation() && !UsesLegacyParts())
        {
            return base.OnPickBlock(world, pos);
        }

        return IsCoverStation()
            ? new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("main", "north")), 1)
            : base.OnPickBlock(world, pos);
    }

    public override AssetLocation GetRotatedBlockCode(int angle)
    {
        if (!IsCoverStation())
        {
            return base.GetRotatedBlockCode(angle);
        }

        int index = GameMath.Mod(GetSide().HorizontalAngleIndex - angle / 90, 4);
        BlockFacing rotated = BlockFacing.HORIZONTALS_ANGLEORDER[index];
        return UsesLegacyParts()
            ? CodeWithParts(GetPart(), rotated.Code)
            : CodeWithSide(rotated.Code);
    }

    public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
    {
        return IsCoverStation() || base.DoPartialSelection(world, pos);
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (!IsCoverStation())
        {
            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        BlockEntityFACoverStation? be = GetStationController(blockAccessor, GetMainPos(pos));
        Vec3i partOffset = GetPartOffset();
        List<StationElementZone> zones = BuildSelectableZones(elementZones, partOffset, be);
        return BuildSelectionBoxes(zones, partOffset, be) ?? selectionBoxes ?? base.GetSelectionBoxes(blockAccessor, pos);
    }

    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        return IsCoverStation()
            ? [new Cuboidf(0f, 0f, 0f, 1f, 1.5f, 1f)]
            : base.GetCollisionBoxes(blockAccessor, pos);
    }

    public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea)
    {
        return IsCoverStation() && blockFace == BlockFacing.UP
            || base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsCoverStation()) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        BlockEntityFACoverStation? currentBe = GetStationController(world.BlockAccessor, GetMainPos(blockSel.Position));
        StationElementZone? zone = GetZoneFromSelection(
            blockSel,
            GetPartOffset(),
            BuildSelectableZones(elementZones, GetPartOffset(), currentBe),
            currentBe
        );
        if (zone == null)
        {
            world.Logger.Notification(
                "[FACore CoverStation AnimDebug] Interaction at {0} had no zone. selectionBox={1}, currentBe={2}",
                blockSel.Position,
                blockSel.SelectionBoxIndex,
                currentBe?.GetType().FullName ?? "null"
            );
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        if (zone.ActionName == "TableStorage")
        {
            if (world.Side == EnumAppSide.Client)
            {
                return true;
            }

            BlockPos tableMainPos = GetMainPos(blockSel.Position);
            BlockEntityFACoverStation? tableBe = GetOrCreateStationController(world, tableMainPos);
            if (tableBe == null)
            {
                Notify(byPlayer, "The cover station is not ready yet.");
                return true;
            }

            tableBe.HandleElementInteraction(byPlayer, zone.ActionName);
            return true;
        }

        if (world.Side == EnumAppSide.Client)
        {
            world.Logger.Notification(
                "[FACore CoverStation AnimDebug] Client interaction accepted at {0}. zone={1}, selectionBox={2}, currentBe={3}",
                blockSel.Position,
                zone.ActionName,
                blockSel.SelectionBoxIndex,
                currentBe?.GetType().FullName ?? "null"
            );
            return true;
        }

        BlockPos mainPos = GetMainPos(blockSel.Position);
        BlockEntityFACoverStation? be = GetOrCreateStationController(world, mainPos);
        if (be == null)
        {
            Notify(byPlayer, "The cover station is not ready yet.");
            return true;
        }

        if (zone.ActionName == "Fuel" && be.IsHoldingIgniter(byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack) && be.CanStartFuelIgnition(byPlayer))
        {
            return true;
        }

        world.Logger.Notification(
            "[FACore CoverStation AnimDebug] Server interaction at {0}. mainPos={1}, zone={2}, selectionBox={3}, be={4}",
            blockSel.Position,
            mainPos,
            zone.ActionName,
            blockSel.SelectionBoxIndex,
            be.GetType().FullName
        );
        be.HandleElementInteraction(byPlayer, zone.ActionName);
        return true;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsCoverStation())
        {
            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        return secondsUsed < FuelIgnitionSeconds
            && TryGetFuelIgnitionTarget(world, byPlayer, blockSel, GetPartOffset(), GetMainPos(blockSel.Position), out BlockEntityFACoverStation? be)
            && be.CanStartFuelIgnition(byPlayer);
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!IsCoverStation())
        {
            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
            return;
        }

        if (secondsUsed < FuelIgnitionSeconds || world.Side != EnumAppSide.Server)
        {
            return;
        }

        if (TryGetFuelIgnitionTarget(world, byPlayer, blockSel, GetPartOffset(), GetMainPos(blockSel.Position), out BlockEntityFACoverStation? be))
        {
            be.CompleteFuelIgnition(byPlayer);
        }
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        if (!IsCoverStation())
        {
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        return TryGetFuelIgnitionTarget(world, byPlayer, blockSel, GetPartOffset(), GetMainPos(blockSel.Position), out _)
            || base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        return base.GetPlacedBlockInfo(world, pos, forPlayer);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        if (!IsCoverStation())
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        BlockEntityFACoverStation? be = GetStationController(world.BlockAccessor, GetMainPos(selection.Position));
        StationElementZone? zone = GetZoneFromSelection(
            selection,
            GetPartOffset(),
            BuildSelectableZones(elementZones, GetPartOffset(), be),
            be
        );
        if (zone != null)
        {
            return
            [
                new WorldInteraction
                {
                    ActionLangCode = GetZoneInteractionText(zone, be, forPlayer),
                    MouseButton = EnumMouseButton.Right
                }
            ];
        }

        return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
    }

    public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
    {
        Vec3i partOffset = ToPartOffset(offset);
        BlockEntityFACoverStation? be = GetStationController(blockAccessor, pos.AddCopy(offset));
        List<StationElementZone> zones = BuildSelectableZones(elementZones, partOffset, be);
        return BuildSelectionBoxes(zones, partOffset, be) ?? [Cuboidf.Default()];
    }

    public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
    {
        return [new Cuboidf(0f, 0f, 0f, 1f, 1.5f, 1f)];
    }

    public bool MBDoPartialSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
    {
        return IsCoverStation();
    }

    public bool MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
    {
        if (!IsCoverStation()) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        Vec3i partOffset = ToPartOffset(offset);
        BlockEntityFACoverStation? currentBe = GetStationController(world.BlockAccessor, blockSel.Position.AddCopy(offset));
        List<StationElementZone> zones = BuildSelectableZones(elementZones, partOffset, currentBe);
        StationElementZone? zone = GetZoneFromSelection(blockSel, partOffset, zones, currentBe);
        if (zone == null)
        {
            world.Logger.Notification(
                "[FACore CoverStation AnimDebug] MB interaction at {0} had no zone. controller={1}, offset={2}/{3}/{4}, selectionBox={5}, currentBe={6}",
                blockSel.Position,
                blockSel.Position.AddCopy(offset),
                offset.X,
                offset.Y,
                offset.Z,
                blockSel.SelectionBoxIndex,
                currentBe?.GetType().FullName ?? "null"
            );
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        if (zone.ActionName == "TableStorage")
        {
            if (world.Side == EnumAppSide.Client)
            {
                return true;
            }

            BlockPos tableMainPos = blockSel.Position.AddCopy(offset);
            BlockEntityFACoverStation? tableBe = GetOrCreateStationController(world, tableMainPos);
            if (tableBe == null)
            {
                Notify(byPlayer, "The cover station is not ready yet.");
                return true;
            }

            tableBe.HandleElementInteraction(byPlayer, zone.ActionName);
            return true;
        }

        if (world.Side == EnumAppSide.Client)
        {
            world.Logger.Notification(
                "[FACore CoverStation AnimDebug] Client MB interaction accepted at {0}. controller={1}, offset={2}/{3}/{4}, zone={5}, selectionBox={6}, currentBe={7}",
                blockSel.Position,
                blockSel.Position.AddCopy(offset),
                offset.X,
                offset.Y,
                offset.Z,
                zone.ActionName,
                blockSel.SelectionBoxIndex,
                currentBe?.GetType().FullName ?? "null"
            );
            return true;
        }

        BlockPos mainPos = blockSel.Position.AddCopy(offset);
        BlockEntityFACoverStation? be = GetOrCreateStationController(world, mainPos);
        if (be == null)
        {
            Notify(byPlayer, "The cover station is not ready yet.");
            return true;
        }

        if (zone.ActionName == "Fuel" && be.IsHoldingIgniter(byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack) && be.CanStartFuelIgnition(byPlayer))
        {
            return true;
        }

        world.Logger.Notification(
            "[FACore CoverStation AnimDebug] Server MB interaction at {0}. mainPos={1}, offset={2}/{3}/{4}, zone={5}, selectionBox={6}, be={7}",
            blockSel.Position,
            mainPos,
            offset.X,
            offset.Y,
            offset.Z,
            zone.ActionName,
            blockSel.SelectionBoxIndex,
            be.GetType().FullName
        );
        be.HandleElementInteraction(byPlayer, zone.ActionName);
        return true;
    }

    public bool MBOnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
    {
        return secondsUsed < FuelIgnitionSeconds
            && TryGetFuelIgnitionTarget(world, byPlayer, blockSel, ToPartOffset(offset), blockSel.Position.AddCopy(offset), out BlockEntityFACoverStation? be)
            && be.CanStartFuelIgnition(byPlayer);
    }

    public void MBOnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
    {
        if (secondsUsed < FuelIgnitionSeconds || world.Side != EnumAppSide.Server)
        {
            return;
        }

        if (TryGetFuelIgnitionTarget(world, byPlayer, blockSel, ToPartOffset(offset), blockSel.Position.AddCopy(offset), out BlockEntityFACoverStation? be))
        {
            be.CompleteFuelIgnition(byPlayer);
        }
    }

    public bool MBOnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason, Vec3i offset)
    {
        return TryGetFuelIgnitionTarget(world, byPlayer, blockSel, ToPartOffset(offset), blockSel.Position.AddCopy(offset), out _);
    }

    public ItemStack MBOnPickBlock(IWorldAccessor world, BlockPos pos, Vec3i offset)
    {
        return OnPickBlock(world, pos.AddCopy(offset));
    }

    public WorldInteraction[] MBGetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer, Vec3i offset)
    {
        Vec3i partOffset = ToPartOffset(offset);
        BlockEntityFACoverStation? be = GetStationController(world.BlockAccessor, blockSel.Position.AddCopy(offset));
        List<StationElementZone> zones = BuildSelectableZones(elementZones, partOffset, be);
        StationElementZone? zone = GetZoneFromSelection(blockSel, partOffset, zones, be);
        if (zone != null)
        {
            return
            [
                new WorldInteraction
                {
                    ActionLangCode = GetZoneInteractionText(zone, be, forPlayer),
                    MouseButton = EnumMouseButton.Right
                }
            ];
        }

        return base.GetPlacedBlockInteractionHelp(world, blockSel, forPlayer);
    }

    public BlockSounds MBGetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack stack, Vec3i offset)
    {
        return GetSounds(blockAccessor, blockSel, stack);
    }

    private StationElementZone? GetZoneFromSelection(BlockSelection selection, Vec3i partOffset, List<StationElementZone> zones, BlockEntityFACoverStation? be)
    {
        int zoneIndex = selection.SelectionBoxIndex;
        if (zoneIndex >= 0 && zoneIndex < zones.Count)
        {
            return zones[zoneIndex];
        }

        if (zones.Count == 0 || selection.HitPosition == null)
        {
            return null;
        }

        StationElementZone? bestZone = null;
        double bestDistance = double.MaxValue;
        foreach (StationElementZone zone in zones)
        {
            double distance = DistanceToBoxCenter(selection.HitPosition, ToPartBox(GetCurrentStationBox(zone, be), partOffset));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestZone = zone;
            }
        }

        return bestZone;
    }

    private bool TryGetFuelIgnitionTarget(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        Vec3i partOffset,
        BlockPos controllerPos,
        out BlockEntityFACoverStation be
    )
    {
        be = null!;
        BlockEntityFACoverStation? stationBe = GetStationController(world.BlockAccessor, controllerPos);
        if (stationBe == null)
        {
            return false;
        }

        StationElementZone? zone = GetZoneFromSelection(
            blockSel,
            partOffset,
            BuildSelectableZones(elementZones, partOffset, stationBe),
            stationBe
        );
        if (zone?.ActionName != "Fuel")
        {
            return false;
        }

        ItemStack? heldStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (!stationBe.IsHoldingIgniter(heldStack))
        {
            return false;
        }

        be = stationBe;
        return true;
    }

    private bool IsCoverStation()
    {
        return GetPurpose() == "cover";
    }

    private string GetPurpose()
    {
        return UsesLegacyParts() ? LastCodePart(2) : LastCodePart(1);
    }

    private string GetPart()
    {
        return UsesLegacyParts() ? LastCodePart(1) : "main";
    }

    private BlockFacing GetSide()
    {
        return BlockFacing.FromCode(LastCodePart(0));
    }

    private AssetLocation CodeWithParts(string part, string side)
    {
        return new AssetLocation(Code.Domain, $"fa-workstation-{GetPurpose()}-{part}-{side}");
    }

    private AssetLocation CodeWithSide(string side)
    {
        return new AssetLocation(Code.Domain, $"fa-workstation-{GetPurpose()}-{side}");
    }

    private bool UsesLegacyParts()
    {
        string[] parts = Code.Path.Split('-');
        return parts.Length >= 4 && (LastCodePart(1) == "main" || LastCodePart(1) == "proxy");
    }

    private BlockPos GetMainPos(BlockPos pos)
    {
        if (GetPart() != "proxy")
        {
            return pos;
        }

        Vec3i offset = GetProxyOffset(GetSide().Code);
        return pos.AddCopy(-offset.X, -offset.Y, -offset.Z);
    }

    private BlockEntityFACoverStation? GetStationController(IWorldAccessor world, BlockPos mainPos)
    {
        return world.BlockAccessor.GetBlockEntity(mainPos) as BlockEntityFACoverStation;
    }

    private BlockEntityFACoverStation? GetOrCreateStationController(IWorldAccessor world, BlockPos mainPos)
    {
        BlockEntityFACoverStation? be = GetStationController(world, mainPos);
        if (be != null || world.Side != EnumAppSide.Server)
        {
            return be;
        }

        world.BlockAccessor.SpawnBlockEntity("FAStation", mainPos);
        return GetStationController(world, mainPos);
    }

    private static BlockEntityFACoverStation? GetStationController(IBlockAccessor blockAccessor, BlockPos mainPos)
    {
        return blockAccessor.GetBlockEntity(mainPos) as BlockEntityFACoverStation;
    }

    private void EnsureStationController(IWorldAccessor world, BlockPos mainPos)
    {
        if (world.Side != EnumAppSide.Server)
        {
            return;
        }

        if (GetStationController(world, mainPos) != null)
        {
            return;
        }

        world.BlockAccessor.SpawnBlockEntity("FAStation", mainPos);
    }

    private static void RefreshPlacedStation(IWorldAccessor world, BlockPos mainPos, BlockPos proxyPos)
    {
        if (world.Side != EnumAppSide.Server)
        {
            return;
        }

        world.BlockAccessor.MarkBlockEntityDirty(mainPos);
        world.BlockAccessor.MarkBlockDirty(mainPos, () => { });
        world.BlockAccessor.MarkBlockDirty(proxyPos, () => { });
    }

    private static void EnsureStationProxy(IWorldAccessor world, BlockPos mainPos, BlockFacing side)
    {
        if (world.Side != EnumAppSide.Server)
        {
            return;
        }

        Vec3i offset = GetProxyOffset(side.Code);
        BlockPos proxyPos = mainPos.AddCopy(offset.X, offset.Y, offset.Z);
        Block currentBlock = world.BlockAccessor.GetBlock(proxyPos);
        if (!currentBlock.IsReplacableBy(world.BlockAccessor.GetBlock(mainPos)) && currentBlock is not BlockMultiblock)
        {
            return;
        }

        string dx = OffsetCode(offset.X);
        string dy = OffsetCode(offset.Y);
        string dz = OffsetCode(offset.Z);
        Block? proxyBlock = world.GetBlock(new AssetLocation("game", $"multiblock-monolithic-{dx}-{dy}-{dz}"));
        if (proxyBlock == null)
        {
            world.Logger.Warning("[FACore Station] Missing multiblock proxy block game:multiblock-monolithic-{0}-{1}-{2}", dx, dy, dz);
            return;
        }

        world.BlockAccessor.SetBlock(proxyBlock.Id, proxyPos);
        world.Logger.Notification("[FACore Station] Placed proxy {0} at {1} for controller {2}", proxyBlock.Code, proxyPos, mainPos);
    }

    private static string OffsetCode(int value)
    {
        return value switch
        {
            < 0 => "n" + Math.Abs(value),
            > 0 => "p" + value,
            _ => "0"
        };
    }

    private BlockPos GetOtherPartPos(BlockPos pos)
    {
        if (GetPart() != "main")
        {
            return GetMainPos(pos);
        }

        Vec3i offset = GetProxyOffset(GetSide().Code);
        return pos.AddCopy(offset.X, offset.Y, offset.Z);
    }

    private Vec3i GetPartOffset()
    {
        return GetPart() == "proxy" ? GetProxyOffset(GetSide().Code) : new Vec3i(0, 0, 0);
    }

    private static Vec3i GetProxyOffset(string side)
    {
        return side switch
        {
            "east" => new Vec3i(0, 0, 1),
            "south" => new Vec3i(-1, 0, 0),
            "west" => new Vec3i(0, 0, -1),
            _ => new Vec3i(1, 0, 0)
        };
    }

    private static BlockFacing GetPlacementSide(IPlayer byPlayer)
    {
        return BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw).Opposite;
    }

    private static Cuboidf[] BuildSelectionBoxes(List<StationElementZone> zones, Vec3i partOffset)
    {
        return BuildSelectionBoxes(zones, partOffset, null)!;
    }

    private static Cuboidf[]? BuildSelectionBoxes(List<StationElementZone> zones, Vec3i partOffset, BlockEntityFACoverStation? be)
    {
        if (zones.Count == 0)
        {
            return null;
        }

        var boxes = new List<Cuboidf>(zones.Count + 1);

        foreach (StationElementZone zone in zones)
        {
            boxes.Add(ToPartBox(GetCurrentStationBox(zone, be), partOffset));
        }

        if (boxes.Count == 0)
        {
            boxes.Add(new Cuboidf(0f, 0f, 0f, 1f, 1.5f, 1f));
        }

        return boxes.ToArray();
    }

    private static Cuboidf GetCurrentStationBox(StationElementZone zone, BlockEntityFACoverStation? be)
    {
        if (zone.ActionName == "LidOpen" || zone.AnimatedStationBox == null || be == null)
        {
            return zone.StationBox;
        }

        float progress = zone.ActionName switch
        {
            "LidOpen" => be.GetAnimationProgress("lidopen", be.LidOpen),
            "FuelDoor" => be.GetAnimationProgress("fuelopen", be.FuelOpen),
            _ => 0f
        };

        return GetAnimationBox(zone, progress);
    }

    private static Cuboidf GetAnimationBox(StationElementZone zone, float progress)
    {
        List<StationElementKeyframe>? boxes = zone.AnimationBoxes;
        if (boxes == null || boxes.Count == 0)
        {
            return Lerp(zone.StationBox, zone.AnimatedStationBox!, progress);
        }

        if (progress <= boxes[0].Progress)
        {
            return boxes[0].Box;
        }

        for (int i = 1; i < boxes.Count; i++)
        {
            StationElementKeyframe previous = boxes[i - 1];
            StationElementKeyframe next = boxes[i];
            if (progress > next.Progress)
            {
                continue;
            }

            float range = next.Progress - previous.Progress;
            float segmentProgress = range <= 0f ? 1f : (progress - previous.Progress) / range;
            return Lerp(previous.Box, next.Box, segmentProgress);
        }

        return boxes[^1].Box;
    }

    private static Cuboidf Lerp(Cuboidf from, Cuboidf to, float progress)
    {
        progress = GameMath.Clamp(progress, 0f, 1f);
        return new Cuboidf(
            GameMath.Lerp(from.X1, to.X1, progress),
            GameMath.Lerp(from.Y1, to.Y1, progress),
            GameMath.Lerp(from.Z1, to.Z1, progress),
            GameMath.Lerp(from.X2, to.X2, progress),
            GameMath.Lerp(from.Y2, to.Y2, progress),
            GameMath.Lerp(from.Z2, to.Z2, progress)
        );
    }

    private static List<StationElementZone> BuildSelectableZones(List<StationElementZone> zones, Vec3i partOffset, BlockEntityFACoverStation? be)
    {
        var result = new List<StationElementZone>();
        foreach (StationElementZone zone in zones)
        {
            if (be != null && !IsZoneSelectableForState(zone, be))
            {
                continue;
            }

            if (GetOwnerPartOffset(zone.StationBox) == partOffset)
            {
                result.Add(zone);
            }
        }

        return result;
    }

    private static bool IsZoneSelectableForState(StationElementZone zone, BlockEntityFACoverStation be)
    {
        return zone.ActionName switch
        {
            "Fuel" => be.FuelOpen,
            "LiquidPour" => be.LidOpen,
            _ => true
        };
    }

    private static Vec3i ToPartOffset(Vec3i multiblockOffsetInv)
    {
        return new Vec3i(-multiblockOffsetInv.X, -multiblockOffsetInv.Y, -multiblockOffsetInv.Z);
    }

    private static Cuboidf ToPartBox(Cuboidf stationBox, Vec3i partOffset)
    {
        return new Cuboidf(
            stationBox.X1 - partOffset.X,
            stationBox.Y1 - partOffset.Y,
            stationBox.Z1 - partOffset.Z,
            stationBox.X2 - partOffset.X,
            stationBox.Y2 - partOffset.Y,
            stationBox.Z2 - partOffset.Z
        );
    }

    private static Vec3i GetOwnerPartOffset(Cuboidf stationBox)
    {
        int x = (int)Math.Floor((stationBox.X1 + stationBox.X2) * 0.5f);
        int z = (int)Math.Floor((stationBox.Z1 + stationBox.Z2) * 0.5f);
        return new Vec3i(x, 0, z);
    }

    private static string GetZoneInteractionText(StationElementZone zone, BlockEntityFACoverStation? be, IPlayer forPlayer)
    {
        ItemStack? heldStack = forPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;

        return zone.ActionName switch
        {
            "LidOpen" => be?.LidOpen == true ? "Close lid" : "Open lid",
            "FuelDoor" => be?.FuelOpen == true ? "Close fuel door" : "Open fuel door",
            "Fuel" => GetFuelInteractionText(be, heldStack),
            "LiquidPour" => GetLiquidInteractionText(be, heldStack),
            "TableStorage" => GetTableInteractionText(be, heldStack),
            _ => "Use"
        };
    }

    private static string GetTableInteractionText(BlockEntityFACoverStation? be, ItemStack? heldStack)
    {
        if (be?.HasTableItem == true)
        {
            return heldStack == null ? "Take item" : "Table occupied";
        }

        return heldStack == null ? "Table top" : "Place item";
    }

    private static string GetFuelInteractionText(BlockEntityFACoverStation? be, ItemStack? heldStack)
    {
        if (be == null)
        {
            return "Use fuel tray";
        }

        if (heldStack == null)
        {
            return be.HasFuel ? "Take fuel" : "Use fuel tray";
        }

        if (be.IsHoldingIgniter(heldStack))
        {
            return "Light fuel";
        }

        if (be.IsHoldingStationFuel(heldStack))
        {
            return "Add fuel";
        }

        return "Use fuel tray";
    }

    private static string GetLiquidInteractionText(BlockEntityFACoverStation? be, ItemStack? heldStack)
    {
        if (be == null)
        {
            return "Use cauldron";
        }

        if (heldStack == null)
        {
            return be.HasImmersedItem ? "Take item" : "Use cauldron";
        }

        if (be.IsHoldingLiquidContainer(heldStack))
        {
            return be.HasLiquid ? "Take liquid" : "Pour liquid";
        }

        if (be.IsHoldingCauldronItem(heldStack))
        {
            return "Insert item";
        }

        return "Use cauldron";
    }

    private static string GetCauldronItemInteractionText(BlockEntityFACoverStation? be, ItemStack? heldStack)
    {
        if (heldStack == null)
        {
            return be?.HasImmersedItem == true ? "Take item" : "Use cauldron";
        }

        return be?.IsHoldingCauldronItem(heldStack) == true ? "Insert item" : "Use cauldron";
    }

    private static double DistanceToBoxCenter(Vec3d point, Cuboidf box)
    {
        double dx = point.X - (box.X1 + box.X2) / 2.0;
        double dy = point.Y - (box.Y1 + box.Y2) / 2.0;
        double dz = point.Z - (box.Z1 + box.Z2) / 2.0;
        return dx * dx + dy * dy + dz * dz;
    }

    private static void Notify(IPlayer player, string text)
    {
        if (player is IServerPlayer serverPlayer)
        {
            serverPlayer.SendIngameError("facore-coverstation", text);
        }
    }
}
