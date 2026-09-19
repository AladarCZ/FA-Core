using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using FACore.ArmorCommands;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace FACore;

public class BlockFAStation : Block, IMultiBlockColSelBoxes, IMultiBlockInteract
{
    private const float FuelIgnitionSeconds = 2f;
    private const float HeldStationActionSeconds = 2f;
    private const float HeldStationClientCompletionGraceSeconds = 0.25f;
    private const float DecodingClientCompletionGraceSeconds = 0.25f;
    private const int StationProxyRows = 3;
    private Cuboidf[]? selectionBoxes;
    private IReadOnlyList<StationElementZone> elementZones = Array.Empty<StationElementZone>();
    private List<StationElementZone> selectableZones = [];
    private readonly Dictionary<string, HeldStationOperation> heldStationOperations = [];
    private readonly Dictionary<string, DecodingHeldOperation> decodingOperations = [];
    private readonly Dictionary<string, long> heldActionSoundTimes = [];
    private ICoreServerAPI? serverApi;
    private ICoreClientAPI? clientApi;
    private ItemStack[]? trimSolderingIronHelpStacks;
    private ItemStack[]? trimTongsHelpStacks;
    private ItemStack[]? trimRivetsHelpStacks;
    private ItemStack[]? trimCrucibleHelpStacks;
    private ItemStack[]? decorationKitHelpStacks;
    private ItemStack[]? decorationClothHelpStacks;
    private ItemStack[]? decorationHammerHelpStacks;
    private ItemStack[]? decorationSawHelpStacks;
    private ItemStack[]? decorationShearsHelpStacks;
    private ItemStack[]? coverMetalPlateHelpStacks;
    private ItemStack[]? coverAcidMetalPlateHelpStacks;
    private ItemStack[]? coverSulfurBucketHelpStacks;
    private ItemStack[]? coverFuelHelpStacks;
    private ItemStack[]? coverIgniterHelpStacks;
    private ItemStack[]? coatingArmorHelpCandidates;
    private readonly Dictionary<string, ItemStack[]> coverCoatingArmorHelpStacks = [];
    private readonly Dictionary<string, ItemStack[]> decorationFittingHelpStacks = [];

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api is ICoreServerAPI loadedServerApi)
        {
            serverApi = loadedServerApi;
            serverApi.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        if (api is ICoreClientAPI loadedClientApi)
        {
            clientApi = loadedClientApi;
            clientApi.Event.ReloadShapes += OnReloadShapes;
        }

        if (!UsesModelDrivenStation()) return;

        SideSolid[BlockFacing.UP.Index] = true;

        ReloadElementZones(api);
        Vec3i partOffset = GetPartOffset();
        bool includeAllSelectionOwners = IncludeAllSelectionOwners(partOffset);
        StationDebugLog(api.Logger,
            "[FACore Station] {0}: purpose={1}, legacyParts={2}, includeAllSelectionOwners={3}, elementZones={4}, selectableZones={5}, selectionBoxes={6}",
            Code,
            GetPurpose(),
            UsesLegacyParts(),
            includeAllSelectionOwners,
            elementZones.Count,
            selectableZones.Count,
            selectionBoxes?.Length ?? 0
        );
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        if (serverApi != null)
        {
            serverApi.Event.PlayerDisconnect -= OnPlayerDisconnect;
            serverApi = null;
        }


        if (clientApi != null)
        {
            clientApi.Event.ReloadShapes -= OnReloadShapes;
            clientApi = null;
        }

        heldStationOperations.Clear();
        foreach ((string playerUid, DecodingHeldOperation operation) in decodingOperations)
        {
            operation.Station.Cancel(playerUid, operation.Token);
        }
        decodingOperations.Clear();
        heldActionSoundTimes.Clear();
        coatingArmorHelpCandidates = null;
        coverCoatingArmorHelpStacks.Clear();
        elementZones = Array.Empty<StationElementZone>();
        selectableZones.Clear();
        selectionBoxes = null;
        base.OnUnloaded(api);
    }

    internal IReadOnlyList<StationElementZone> GetElementZones() => elementZones;

    private void OnReloadShapes()
    {
        if (clientApi != null && UsesModelDrivenStation()) ReloadElementZones(clientApi);
    }

    private void ReloadElementZones(ICoreAPI api)
    {
        Vec3i partOffset = GetPartOffset();
        elementZones = StationShapeElementReader.LoadElementZones(api, this);
        if (IsDecodingStation())
        {
            var invalidActions = new HashSet<string>(StringComparer.Ordinal);
            foreach (string requiredName in StationElementZone.DecodingElementNames)
            {
                int count = elementZones.Count(zone => string.Equals(zone.ElementName, requiredName, StringComparison.Ordinal));
                if (count == 1) continue;
                invalidActions.Add(StationElementZone.ActionNameFor(requiredName));
                api.Logger.Error("[FACore DecodingTable] Shape {0} requires exactly one '{1}' anchor, but found {2}. Its interaction is disabled.", Shape?.Base, requiredName, count);
            }
            if (invalidActions.Count > 0)
            {
                elementZones = elementZones.Where(zone => !invalidActions.Contains(zone.ActionName)).ToArray();
            }
        }
        selectableZones = BuildSelectableZones(elementZones, partOffset, null, IncludeAllSelectionOwners(partOffset));
        selectionBoxes = BuildSelectionBoxes(selectableZones, partOffset);
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        heldStationOperations.Remove(player.PlayerUID);
        if (decodingOperations.Remove(player.PlayerUID, out DecodingHeldOperation? operation))
        {
            operation.Station.Cancel(player.PlayerUID, operation.Token);
        }
        heldActionSoundTimes.Remove(player.PlayerUID);
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        if (!UsesModelDrivenStation())
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

            if (!CanPlaceStationProxies(world, byPlayer, placeBlock, blockSel.Position, placementSide, out _))
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

            if (!placeBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
            {
                return false;
            }
            if (!EnsureStationProxies(world, blockSel.Position, placementSide))
            {
                RemoveStationProxies(world, blockSel.Position, placementSide);
                if (world.BlockAccessor.GetBlock(blockSel.Position) == placeBlock)
                {
                    world.BlockAccessor.SetBlock(0, blockSel.Position);
                }
                failureCode = "notenoughspace";
                return false;
            }
            placeStation.EnsureStationController(world, blockSel.Position);
            RefreshPlacedStation(world, blockSel.Position, placementSide);
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
        if (!UsesModelDrivenStation() || !UsesLegacyParts())
        {
            if (UsesModelDrivenStation() && !UsesLegacyParts())
            {
                RemoveStationProxies(world, pos, GetSide());
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
        if (world.BlockAccessor.GetBlock(otherPos) is BlockFAStation otherStation && otherStation.UsesModelDrivenStation() && otherStation.GetPart() != GetPart())
        {
            world.BlockAccessor.SetBlock(0, otherPos);
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        if (UsesModelDrivenStation() && !UsesLegacyParts())
        {
            RemoveStationProxies(world, pos, GetSide());
        }

        if (UsesModelDrivenStation() && UsesLegacyParts())
        {
            BlockPos otherPos = GetOtherPartPos(pos);
            Block otherBlock = world.BlockAccessor.GetBlock(otherPos);
            if (otherBlock is BlockFAStation otherStation && otherStation.UsesModelDrivenStation() && otherStation.GetPart() != GetPart())
            {
                world.BlockAccessor.SetBlock(0, otherPos);
            }
        }

        base.OnBlockRemoved(world, pos);
    }

    public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack? byItemStack = null)
    {
        base.OnBlockPlaced(world, blockPos, byItemStack);

        if (!UsesModelDrivenStation() || UsesLegacyParts())
        {
            return;
        }

        EnsureStationProxies(world, blockPos, GetSide());
        EnsureStationController(world, blockPos);
        RefreshPlacedStation(world, blockPos, GetSide());
    }

    public void EnsureStationStructure(IWorldAccessor world, BlockPos blockPos)
    {
        if (!UsesModelDrivenStation() || UsesLegacyParts())
        {
            return;
        }

        EnsureStationProxies(world, blockPos, GetSide());
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (!UsesModelDrivenStation())
        {
            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }

        if (!UsesLegacyParts())
        {
            ItemStack[] drops = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
            if (IsDecodingStation() && GetDecodingController(world.BlockAccessor, pos) is BlockEntityFADecodingTable decoding)
            {
                foreach (ItemStack drop in drops)
                {
                    if (drop.Collectible?.Code?.Equals(Code) == true) decoding.WriteDropState(drop);
                }
            }
            return drops;
        }

        if (GetPart() == "proxy")
        {
            return [];
        }

        return [new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("main", "north")), 1)];
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        if (UsesModelDrivenStation() && !UsesLegacyParts())
        {
            return base.OnPickBlock(world, pos);
        }

        return UsesModelDrivenStation()
            ? new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("main", "north")), 1)
            : base.OnPickBlock(world, pos);
    }

    public override AssetLocation GetRotatedBlockCode(int angle)
    {
        if (!UsesModelDrivenStation())
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
        return UsesModelDrivenStation() || base.DoPartialSelection(world, pos);
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (!UsesModelDrivenStation())
        {
            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        BlockEntityFAWorkStation? be = IsCoverStation() ? GetStationController(blockAccessor, GetMainPos(pos)) : null;
        Vec3i partOffset = GetPartOffset();
        List<StationElementZone> zones = BuildSelectableZones(elementZones, partOffset, be, IncludeAllSelectionOwners(partOffset));
        return BuildSelectionBoxes(zones, partOffset, be) ?? selectionBoxes ?? base.GetSelectionBoxes(blockAccessor, pos);
    }

    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        return UsesModelDrivenStation()
            ? StationBounds.CreatePhysicalCollisionBoxes()
            : base.GetCollisionBoxes(blockAccessor, pos);
    }

    public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea)
    {
        return UsesModelDrivenStation() && blockFace == BlockFacing.UP
            || base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!UsesModelDrivenStation()) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        if (TryHandleStationInteractStart(world, byPlayer, blockSel, GetPartOffset(), GetMainPos(blockSel.Position)))
        {
            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (TryHandleStationInteractStep(secondsUsed, world, byPlayer, blockSel, GetPartOffset(), GetMainPos(blockSel.Position), out bool result))
        {
            return result;
        }

        return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!TryHandleStationInteractStop(secondsUsed, world, byPlayer, blockSel, GetPartOffset(), GetMainPos(blockSel.Position)))
        {
            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }
    }

    public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
    {
        if (TryHandleStationInteractCancel(world, byPlayer, blockSel, GetPartOffset(), GetMainPos(blockSel.Position)))
        {
            return true;
        }

        return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        if (!UsesModelDrivenStation())
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        BlockPos mainPos = GetMainPos(pos);
        if (IsDecodingStation())
        {
            BlockEntityFADecodingTable? decoding = GetDecodingController(world.BlockAccessor, mainPos);
            if (decoding == null) return base.GetPlacedBlockInfo(world, pos, forPlayer);
            BlockSelection? currentSelection = forPlayer.CurrentBlockSelection;
            if (currentSelection != null)
            {
                Vec3i selectionOffset = ResolveSelectionPartOffset(currentSelection, pos);
                StationElementZone? decodingZone = GetZoneFromSelection(
                    currentSelection,
                    selectionOffset,
                    BuildSelectableZones(elementZones, selectionOffset, null, IncludeAllSelectionOwners(selectionOffset)),
                    null);
                if (decodingZone != null) return decoding.DescribeElementState(decodingZone.ActionName);
            }
            return decoding.DescribeState();
        }

        BlockEntityFAWorkStation? be = GetStationController(world.BlockAccessor, mainPos);
        if (be == null)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        BlockSelection? selection = forPlayer.CurrentBlockSelection;
        if (selection != null)
        {
            // When the player looks at a multiblock proxy part, vanilla BlockMultiblock forwards this
            // call to the controller (pos) but leaves SelectionBoxIndex pointing into the proxy part's
            // own selection-box array. Resolve against the part offset of the block actually being
            // looked at so the index lines up with the boxes the player sees (otherwise every proxy box
            // resolves to zone 0, e.g. the soldering iron holder, and slots read each other's state).
            Vec3i partOffset = ResolveSelectionPartOffset(selection, pos);
            BlockEntityFAWorkStation? zoneBe = IsCoverStation() ? be : null;
            StationElementZone? zone = GetZoneFromSelection(
                selection,
                partOffset,
                BuildSelectableZones(elementZones, partOffset, zoneBe, IncludeAllSelectionOwners(partOffset)),
                zoneBe
            );

            if (zone != null)
            {
                string zoneInfo = be.DescribeElementState(zone.ActionName);
                if (!string.IsNullOrWhiteSpace(zoneInfo))
                {
                    return zoneInfo;
                }
            }
        }

        string info = be.DescribeState();
        return string.IsNullOrWhiteSpace(info) ? base.GetPlacedBlockInfo(world, pos, forPlayer) : info;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        if (!UsesModelDrivenStation())
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        if (IsDecodingStation())
        {
            Vec3i decodingOffset = GetPartOffset();
            StationElementZone? decodingZone = GetZoneFromSelection(
                selection,
                decodingOffset,
                BuildSelectableZones(elementZones, decodingOffset, null, IncludeAllSelectionOwners(decodingOffset)),
                null);
            BlockEntityFADecodingTable? decoding = GetDecodingController(world.BlockAccessor, GetMainPos(selection.Position));
            return decodingZone != null && decoding != null
                ? decoding.GetInteractionHelp(decodingZone.ActionName)
                : base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

        BlockEntityFAWorkStation? be = GetStationController(world.BlockAccessor, GetMainPos(selection.Position));
        StationElementZone? zone = GetZoneFromSelection(
            selection,
            GetPartOffset(),
            BuildSelectableZones(elementZones, GetPartOffset(), be, IncludeAllSelectionOwners(GetPartOffset())),
            be
        );
        if (zone != null)
        {
            return GetStationZoneInteractions(world, zone, be, forPlayer);
        }

        return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
    }

    public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
    {
        Vec3i partOffset = ToPartOffset(offset);
        BlockEntityFAWorkStation? be = IsCoverStation() ? GetStationController(blockAccessor, pos.AddCopy(offset)) : null;
        List<StationElementZone> zones = BuildSelectableZones(elementZones, partOffset, be);
        return BuildSelectionBoxes(zones, partOffset, be) ?? [];
    }

    public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
    {
        if (ToPartOffset(offset).Y > 0)
        {
            return [];
        }

        return StationBounds.CreatePhysicalCollisionBoxes();
    }

    public bool MBDoPartialSelection(IWorldAccessor world, BlockPos pos, Vec3i offset)
    {
        return UsesModelDrivenStation();
    }

    public bool MBOnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
    {
        if (!UsesModelDrivenStation()) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        if (TryHandleStationInteractStart(world, byPlayer, blockSel, ToPartOffset(offset), blockSel.Position.AddCopy(offset)))
        {
            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public bool MBOnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
    {
        if (TryHandleStationInteractStep(secondsUsed, world, byPlayer, blockSel, ToPartOffset(offset), blockSel.Position.AddCopy(offset), out bool result))
        {
            return result;
        }

        return false;
    }

    public void MBOnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i offset)
    {
        TryHandleStationInteractStop(secondsUsed, world, byPlayer, blockSel, ToPartOffset(offset), blockSel.Position.AddCopy(offset));
    }

    public bool MBOnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason, Vec3i offset)
    {
        return TryHandleStationInteractCancel(world, byPlayer, blockSel, ToPartOffset(offset), blockSel.Position.AddCopy(offset));
    }

    private bool TryHandleStationInteractStart(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        Vec3i partOffset,
        BlockPos controllerPos)
    {
        using var languageScope = FaText.ForPlayer(byPlayer);
        BlockEntityFAWorkStation? currentBe = IsCoverStation() ? GetStationController(world.BlockAccessor, controllerPos) : null;
        StationElementZone? zone = GetZoneFromSelection(
            blockSel,
            partOffset,
            BuildSelectableZones(elementZones, partOffset, currentBe, IncludeAllSelectionOwners(partOffset)),
            currentBe
        );
        if (zone == null)
        {
            StationDebugLog(world.Logger,
                "[FACore Station] Interaction at {0} had no zone. purpose={1}, controller={2}, partOffset={3}/{4}/{5}, selectionBox={6}, currentBe={7}",
                blockSel.Position,
                GetPurpose(),
                controllerPos,
                partOffset.X,
                partOffset.Y,
                partOffset.Z,
                blockSel.SelectionBoxIndex,
                currentBe?.GetType().FullName ?? "null"
            );
            return false;
        }

        if (IsDecodingStation())
        {
            CancelDecodingOperation(byPlayer);
            BlockEntityFADecodingTable? decoding = GetDecodingController(world.BlockAccessor, controllerPos);
            if (decoding == null)
            {
                if (world.Side == EnumAppSide.Server) Notify(byPlayer, FaText.Get("The Deciphering Table is not ready yet."));
                return true;
            }

            if (zone.ActionName == "Decoding" && BlockEntityFADecodingTable.IsInkAndQuill(byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack))
            {
                if (world.Side == EnumAppSide.Client)
                {
                    if (decoding.CanClientBegin(byPlayer, zone.ActionName)) StartDecodingOperation(byPlayer, decoding, controllerPos, 0);
                    return true;
                }

                if (decoding.TryBegin(byPlayer, zone.ActionName, out long token, out string failure))
                {
                    StartDecodingOperation(byPlayer, decoding, controllerPos, token);
                }
                else if (!string.IsNullOrWhiteSpace(failure))
                {
                    Notify(byPlayer, failure);
                }
                return true;
            }

            if (world.Side == EnumAppSide.Server) decoding.HandleElementInteraction(byPlayer, zone.ActionName);
            return true;
        }

        if (!IsCoverStation())
        {
            heldStationOperations.Remove(byPlayer.PlayerUID);
            if (world.Side == EnumAppSide.Client)
            {
                BlockEntityFAWorkStation? clientStationBe = GetStationController(world.BlockAccessor, controllerPos);
                if (clientStationBe != null && CanStartHeldStationInteraction(clientStationBe, byPlayer, zone.ActionName))
                {
                    StartHeldStationOperation(byPlayer, clientStationBe, controllerPos, zone.ActionName);
                    if (ShouldPlayHeldActionStartSound(byPlayer)) clientStationBe.PlayHeldActionStartSound(byPlayer);
                }
                return true;
            }

            BlockEntityFAWorkStation? stationBe = GetOrCreateStationController(world, controllerPos);
            if (stationBe == null)
            {
                Notify(byPlayer, FaText.Get("The station is not ready yet."));
                return true;
            }

            if (CanStartHeldStationInteraction(stationBe, byPlayer, zone.ActionName))
            {
                StartHeldStationOperation(byPlayer, stationBe, controllerPos, zone.ActionName);
                return true;
            }

            stationBe.HandleElementInteraction(byPlayer, zone.ActionName);
            return true;
        }

        if (zone.ActionName == "TableStorage")
        {
            if (world.Side == EnumAppSide.Server)
            {
                BlockEntityFAWorkStation? tableBe = GetOrCreateStationController(world, controllerPos);
                if (tableBe == null)
                {
                    Notify(byPlayer, FaText.Get("The cover station is not ready yet."));
                }
                else
                {
                    tableBe.HandleElementInteraction(byPlayer, zone.ActionName);
                }
            }
            return true;
        }

        if (world.Side == EnumAppSide.Client)
        {
            StationDebugLog(world.Logger,
                "[FACore CoverStation AnimDebug] Client interaction accepted at {0}. controller={1}, zone={2}, selectionBox={3}, currentBe={4}",
                blockSel.Position,
                controllerPos,
                zone.ActionName,
                blockSel.SelectionBoxIndex,
                currentBe?.GetType().FullName ?? "null"
            );
            return true;
        }

        BlockEntityFAWorkStation? be = GetOrCreateStationController(world, controllerPos);
        if (be == null)
        {
            Notify(byPlayer, FaText.Get("The cover station is not ready yet."));
            return true;
        }

        if (zone.ActionName != "Fuel"
            || !be.IsHoldingIgniter(byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack)
            || !be.CanStartFuelIgnition(byPlayer))
        {
            be.HandleElementInteraction(byPlayer, zone.ActionName);
        }

        return true;
    }

    private bool TryHandleStationInteractStep(
        float secondsUsed,
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        Vec3i partOffset,
        BlockPos controllerPos,
        out bool result)
    {
        using var languageScope = FaText.ForPlayer(byPlayer);
        if (IsDecodingStation())
        {
            if (TryGetDecodingTarget(world, byPlayer, blockSel, partOffset, controllerPos, out BlockEntityFADecodingTable? decoding, out string actionName)
                && actionName == "Decoding"
                && decodingOperations.TryGetValue(byPlayer.PlayerUID, out DecodingHeldOperation? operation)
                && operation.Matches(byPlayer, decoding, controllerPos)
                && (world.Side == EnumAppSide.Client || decoding.CanContinue(byPlayer, operation.Token)))
            {
                double requiredSeconds = BlockEntityFADecodingTable.HoldSeconds
                    + (world.Side == EnumAppSide.Client ? DecodingClientCompletionGraceSeconds : 0f);
                result = operation.ElapsedSeconds < requiredSeconds;
                return true;
            }

            CancelDecodingOperation(byPlayer);
            result = false;
            return true;
        }

        if (!IsCoverStation())
        {
            if (TryGetHeldStationTarget(world, byPlayer, blockSel, partOffset, controllerPos, out BlockEntityFAWorkStation? stationBe, out string actionName)
                && heldStationOperations.TryGetValue(byPlayer.PlayerUID, out HeldStationOperation? operation)
                && operation.Matches(byPlayer, stationBe, controllerPos, actionName))
            {
                // Stop and step must use the same clock. Keep the client holding slightly
                // longer so its stop packet does not race the server completion threshold.
                double requiredSeconds = HeldStationActionSeconds
                    + (world.Side == EnumAppSide.Client ? HeldStationClientCompletionGraceSeconds : 0f);
                result = operation.ElapsedSeconds < requiredSeconds;
                return true;
            }

            heldStationOperations.Remove(byPlayer.PlayerUID);
            result = false;
            return false;
        }

        result = secondsUsed < FuelIgnitionSeconds
            && TryGetFuelIgnitionTarget(world, byPlayer, blockSel, partOffset, controllerPos, out BlockEntityFAWorkStation? be)
            && be.CanStartFuelIgnition(byPlayer);
        return true;
    }

    private bool TryHandleStationInteractStop(
        float secondsUsed,
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        Vec3i partOffset,
        BlockPos controllerPos)
    {
        using var languageScope = FaText.ForPlayer(byPlayer);
        if (IsDecodingStation())
        {
            DecodingHeldOperation? operation = TakeDecodingOperation(byPlayer);
            if (operation == null) return true;
            if (world.Side == EnumAppSide.Server
                && TryGetDecodingTarget(world, byPlayer, blockSel, partOffset, controllerPos, out BlockEntityFADecodingTable? decoding, out string actionName)
                && actionName == "Decoding"
                && operation.Matches(byPlayer, decoding, controllerPos)
                && operation.ElapsedSeconds >= BlockEntityFADecodingTable.HoldSeconds)
            {
                decoding.TryComplete(byPlayer, operation.Token, operation.ElapsedSeconds);
            }
            else if (world.Side == EnumAppSide.Server)
            {
                operation.Station.Cancel(byPlayer.PlayerUID, operation.Token);
            }
            return true;
        }

        if (!IsCoverStation())
        {
            HeldStationOperation? operation = TakeHeldStationOperation(byPlayer);
            if (!TryGetHeldStationTarget(world, byPlayer, blockSel, partOffset, controllerPos, out BlockEntityFAWorkStation? stationBe, out string actionName))
            {
                return false;
            }

            if (world.Side == EnumAppSide.Server
                && operation != null
                && operation.Matches(byPlayer, stationBe, controllerPos, actionName)
                && operation.ElapsedSeconds >= HeldStationActionSeconds)
            {
                stationBe.HandleElementInteraction(byPlayer, actionName);
            }
            else if (world.Side == EnumAppSide.Server && operation != null)
            {
                Notify(byPlayer, FaText.Get("Keep holding the tool against the armor until the operation completes."));
            }
            return true;
        }

        if (secondsUsed >= FuelIgnitionSeconds
            && world.Side == EnumAppSide.Server
            && TryGetFuelIgnitionTarget(world, byPlayer, blockSel, partOffset, controllerPos, out BlockEntityFAWorkStation? be))
        {
            be.CompleteFuelIgnition(byPlayer);
        }
        return true;
    }

    private bool TryHandleStationInteractCancel(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        Vec3i partOffset,
        BlockPos controllerPos)
    {
        if (IsDecodingStation())
        {
            CancelDecodingOperation(byPlayer);
            return true;
        }

        return !IsCoverStation()
            ? heldStationOperations.Remove(byPlayer.PlayerUID)
            : TryGetFuelIgnitionTarget(world, byPlayer, blockSel, partOffset, controllerPos, out _);
    }

    public ItemStack MBOnPickBlock(IWorldAccessor world, BlockPos pos, Vec3i offset)
    {
        return OnPickBlock(world, pos.AddCopy(offset));
    }

    public WorldInteraction[] MBGetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer, Vec3i offset)
    {
        Vec3i partOffset = ToPartOffset(offset);
        if (IsDecodingStation())
        {
            StationElementZone? decodingZone = GetZoneFromSelection(blockSel, partOffset, BuildSelectableZones(elementZones, partOffset, null), null);
            BlockEntityFADecodingTable? decoding = GetDecodingController(world.BlockAccessor, blockSel.Position.AddCopy(offset));
            return decodingZone != null && decoding != null
                ? decoding.GetInteractionHelp(decodingZone.ActionName)
                : base.GetPlacedBlockInteractionHelp(world, blockSel, forPlayer);
        }

        BlockEntityFAWorkStation? be = GetStationController(world.BlockAccessor, blockSel.Position.AddCopy(offset));
        List<StationElementZone> zones = BuildSelectableZones(elementZones, partOffset, be);
        StationElementZone? zone = GetZoneFromSelection(blockSel, partOffset, zones, be);
        if (zone != null)
        {
            return GetStationZoneInteractions(world, zone, be, forPlayer);
        }

        return base.GetPlacedBlockInteractionHelp(world, blockSel, forPlayer);
    }

    public BlockSounds MBGetSounds(IBlockAccessor blockAccessor, BlockSelection blockSel, ItemStack stack, Vec3i offset)
    {
        return GetSounds(blockAccessor, blockSel, stack);
    }

    private StationElementZone? GetZoneFromSelection(BlockSelection selection, Vec3i partOffset, List<StationElementZone> zones, BlockEntityFAWorkStation? be)
    {
        int zoneIndex = selection.SelectionBoxIndex;
        if (zoneIndex >= 0 && zoneIndex < zones.Count)
        {
            return zones[zoneIndex];
        }
        return null;
    }

    internal bool TryResolveSelectionAction(IBlockAccessor blockAccessor, BlockPos controllerPos, BlockSelection selection, out string actionName)
    {
        actionName = "";
        Vec3i partOffset = ResolveSelectionPartOffset(selection, controllerPos);
        BlockEntityFAWorkStation? be = GetStationController(blockAccessor, controllerPos);
        StationElementZone? zone = GetZoneFromSelection(
            selection,
            partOffset,
            BuildSelectableZones(elementZones, partOffset, be, IncludeAllSelectionOwners(partOffset)),
            be
        );
        if (zone == null) return false;
        actionName = zone.ActionName;
        return true;
    }

    private bool TryGetDecodingTarget(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        Vec3i partOffset,
        BlockPos controllerPos,
        out BlockEntityFADecodingTable decoding,
        out string actionName)
    {
        decoding = null!;
        actionName = "";
        BlockEntityFADecodingTable? current = GetDecodingController(world.BlockAccessor, controllerPos);
        if (current == null) return false;
        StationElementZone? zone = GetZoneFromSelection(
            blockSel,
            partOffset,
            BuildSelectableZones(elementZones, partOffset, null, IncludeAllSelectionOwners(partOffset)),
            null);
        if (zone == null) return false;
        decoding = current;
        actionName = zone.ActionName;
        return true;
    }

    private void StartDecodingOperation(IPlayer player, BlockEntityFADecodingTable station, BlockPos controllerPos, long token)
    {
        decodingOperations[player.PlayerUID] = new DecodingHeldOperation(
            station,
            token,
            controllerPos.Copy(),
            player.InventoryManager?.ActiveHotbarSlot?.Itemstack,
            Environment.TickCount64);
    }

    private void CancelDecodingOperation(IPlayer player)
    {
        if (decodingOperations.Remove(player.PlayerUID, out DecodingHeldOperation? operation))
        {
            operation.Station.Cancel(player.PlayerUID, operation.Token);
        }
    }

    private DecodingHeldOperation? TakeDecodingOperation(IPlayer player)
    {
        return decodingOperations.Remove(player.PlayerUID, out DecodingHeldOperation? operation) ? operation : null;
    }

    private bool TryGetFuelIgnitionTarget(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        Vec3i partOffset,
        BlockPos controllerPos,
        out BlockEntityFAWorkStation be
    )
    {
        be = null!;
        BlockEntityFAWorkStation? stationBe = GetStationController(world.BlockAccessor, controllerPos);
        if (stationBe == null)
        {
            return false;
        }

        StationElementZone? zone = GetZoneFromSelection(
            blockSel,
            partOffset,
            BuildSelectableZones(elementZones, partOffset, stationBe, IncludeAllSelectionOwners(partOffset)),
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

    private bool TryGetHeldStationTarget(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        Vec3i partOffset,
        BlockPos controllerPos,
        out BlockEntityFAWorkStation be,
        out string actionName
    )
    {
        be = null!;
        actionName = "";
        if (GetPurpose() is not ("trim" or "decoration"))
        {
            return false;
        }

        BlockEntityFAWorkStation? stationBe = GetStationController(world.BlockAccessor, controllerPos);
        if (stationBe == null)
        {
            return false;
        }

        StationElementZone? zone = GetZoneFromSelection(
            blockSel,
            partOffset,
            BuildSelectableZones(elementZones, partOffset, stationBe, IncludeAllSelectionOwners(partOffset)),
            stationBe
        );
        if (zone == null || !CanStartHeldStationInteraction(stationBe, byPlayer, zone.ActionName))
        {
            return false;
        }

        be = stationBe;
        actionName = zone.ActionName;
        return true;
    }

    private bool CanStartHeldStationInteraction(BlockEntityFAWorkStation stationBe, IPlayer byPlayer, string actionName)
    {
        return GetPurpose() == "trim"
            ? actionName == "Armor" && stationBe.CanStartHeldTrimArmorInteraction(byPlayer)
            : GetPurpose() == "decoration" && actionName is "HeadPlace" or "BodyPlace" or "LegsPlace"
                && stationBe.CanStartHeldDecorationInteraction(byPlayer, actionName);
    }

    private void StartHeldStationOperation(IPlayer byPlayer, BlockEntityFAWorkStation stationBe, BlockPos controllerPos, string actionName)
    {
        heldStationOperations[byPlayer.PlayerUID] = new HeldStationOperation(
            stationBe,
            stationBe.GetHeldInteractionRevision(actionName),
            controllerPos.Copy(),
            actionName,
            byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack,
            Environment.TickCount64
        );
    }

    private bool ShouldPlayHeldActionStartSound(IPlayer byPlayer)
    {
        long now = Environment.TickCount64;
        if (heldActionSoundTimes.TryGetValue(byPlayer.PlayerUID, out long previous) && now - previous < 3500)
        {
            return false;
        }

        heldActionSoundTimes[byPlayer.PlayerUID] = now;
        return true;
    }

    private HeldStationOperation? TakeHeldStationOperation(IPlayer byPlayer)
    {
        if (!heldStationOperations.Remove(byPlayer.PlayerUID, out HeldStationOperation? operation))
        {
            return null;
        }

        return operation;
    }

    private bool IsCoverStation()
    {
        return GetPurpose() == "cover";
    }

    private bool IsDecodingStation()
    {
        return GetPurpose() == "decoding";
    }

    private bool UsesModelDrivenStation()
    {
        return GetPurpose() is "cover" or "decoration" or "trim" or "decoding";
    }

    private string FormatStationPurpose()
    {
        string purpose = GetPurpose();
        return purpose.Length == 0 ? "Unknown" : char.ToUpperInvariant(purpose[0]) + purpose[1..];
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

    private BlockEntityFAWorkStation? GetStationController(IWorldAccessor world, BlockPos mainPos)
    {
        return world.BlockAccessor.GetBlockEntity(mainPos) as BlockEntityFAWorkStation;
    }

    private BlockEntityFAWorkStation? GetOrCreateStationController(IWorldAccessor world, BlockPos mainPos)
    {
        BlockEntityFAWorkStation? be = GetStationController(world, mainPos);
        if (be != null || world.Side != EnumAppSide.Server)
        {
            return be;
        }

        world.BlockAccessor.SpawnBlockEntity("FAStation", mainPos);
        return GetStationController(world, mainPos);
    }

    private static BlockEntityFAWorkStation? GetStationController(IBlockAccessor blockAccessor, BlockPos mainPos)
    {
        return blockAccessor.GetBlockEntity(mainPos) as BlockEntityFAWorkStation;
    }

    private static BlockEntityFADecodingTable? GetDecodingController(IBlockAccessor blockAccessor, BlockPos mainPos)
    {
        return blockAccessor.GetBlockEntity(mainPos) as BlockEntityFADecodingTable;
    }

    private void EnsureStationController(IWorldAccessor world, BlockPos mainPos)
    {
        if (world.Side != EnumAppSide.Server)
        {
            return;
        }

        BlockEntity? existing = world.BlockAccessor.GetBlockEntity(mainPos);
        if (IsDecodingStation())
        {
            if (existing is BlockEntityFADecodingTable) return;
            if (existing != null)
            {
                world.Logger.Error("[FACore DecodingTable] Refusing to replace unexpected controller {0} at {1}.", existing.GetType().FullName, mainPos);
                return;
            }
            world.BlockAccessor.SpawnBlockEntity("FADecodingTable", mainPos);
            return;
        }

        if (existing is BlockEntityFAWorkStation)
        {
            return;
        }

        if (existing != null) return;

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

    private void RefreshPlacedStation(IWorldAccessor world, BlockPos mainPos, BlockFacing side)
    {
        if (world.Side != EnumAppSide.Server)
        {
            return;
        }

        world.BlockAccessor.MarkBlockEntityDirty(mainPos);
        world.BlockAccessor.MarkBlockDirty(mainPos, () => { });

        foreach (Vec3i offset in GetStationProxyOffsets(side))
        {
            BlockPos proxyPos = mainPos.AddCopy(offset.X, offset.Y, offset.Z);
            world.BlockAccessor.MarkBlockDirty(proxyPos, () => { });
        }
    }

    private bool EnsureStationProxies(IWorldAccessor world, BlockPos mainPos, BlockFacing side)
    {
        if (world.Side != EnumAppSide.Server)
        {
            return true;
        }

        if (IsDecodingStation()) RemoveLegacyDecodingProxies(world, mainPos, side);
        Block stationBlock = world.BlockAccessor.GetBlock(mainPos);
        foreach (Vec3i offset in GetStationProxyOffsets(side))
        {
            if (!EnsureStationProxy(world, mainPos, stationBlock, offset)) return false;
        }
        return true;
    }

    private static void RemoveLegacyDecodingProxies(IWorldAccessor world, BlockPos mainPos, BlockFacing side)
    {
        Vec3i horizontal = GetProxyOffset(side.Code);
        foreach (Vec3i offset in new[] { horizontal, new Vec3i(horizontal.X, 1, horizontal.Z) })
        {
            BlockPos proxyPos = mainPos.AddCopy(offset.X, offset.Y, offset.Z);
            Block block = world.BlockAccessor.GetBlock(proxyPos);
            if (IsOwnedStationProxy(block, proxyPos, mainPos, GetStationProxyCode(offset)))
            {
                world.BlockAccessor.SetBlock(0, proxyPos);
            }
        }
    }

    private static bool EnsureStationProxy(IWorldAccessor world, BlockPos mainPos, Block stationBlock, Vec3i offset)
    {
        BlockPos proxyPos = mainPos.AddCopy(offset.X, offset.Y, offset.Z);
        Block currentBlock = world.BlockAccessor.GetBlock(proxyPos);
        AssetLocation proxyCode = GetStationProxyCode(offset);
        if (IsOwnedStationProxy(currentBlock, proxyPos, mainPos, proxyCode))
        {
            return true;
        }
        if (!currentBlock.IsReplacableBy(stationBlock))
        {
            world.Logger.Warning("[FACore Station] Cannot restore proxy at {0} for controller {1}: occupied by {2}", proxyPos, mainPos, currentBlock.Code);
            return false;
        }

        Block? proxyBlock = world.GetBlock(proxyCode);
        if (proxyBlock == null)
        {
            world.Logger.Warning("[FACore Station] Missing multiblock proxy block {0}", proxyCode);
            return false;
        }

        world.BlockAccessor.SetBlock(proxyBlock.Id, proxyPos);
        StationDebugLog(world.Logger, "[FACore Station] Placed proxy {0} at {1} for controller {2}", proxyBlock.Code, proxyPos, mainPos);
        return true;
    }

    private bool CanPlaceStationProxies(IWorldAccessor world, IPlayer byPlayer, Block stationBlock, BlockPos mainPos, BlockFacing side, out BlockPos blockedPos)
    {
        foreach (Vec3i offset in GetStationProxyOffsets(side))
        {
            BlockPos proxyPos = mainPos.AddCopy(offset.X, offset.Y, offset.Z);
            if (world.GetBlock(GetStationProxyCode(offset)) == null
                || !world.Claims.TryAccess(byPlayer, proxyPos, EnumBlockAccessFlags.BuildOrBreak))
            {
                blockedPos = proxyPos;
                return false;
            }
            Block currentBlock = world.BlockAccessor.GetBlock(proxyPos);
            if (!currentBlock.IsReplacableBy(stationBlock))
            {
                blockedPos = proxyPos;
                return false;
            }
        }

        blockedPos = mainPos;
        return true;
    }

    private void RemoveStationProxies(IWorldAccessor world, BlockPos mainPos, BlockFacing side)
    {
        if (world.Side != EnumAppSide.Server)
        {
            return;
        }

        foreach (Vec3i offset in GetStationProxyOffsets(side))
        {
            BlockPos proxyPos = mainPos.AddCopy(offset.X, offset.Y, offset.Z);
            Block currentBlock = world.BlockAccessor.GetBlock(proxyPos);
            if (IsOwnedStationProxy(currentBlock, proxyPos, mainPos, GetStationProxyCode(offset)))
            {
                world.BlockAccessor.SetBlock(0, proxyPos);
            }
        }
    }

    private static bool IsOwnedStationProxy(Block block, BlockPos proxyPos, BlockPos mainPos, AssetLocation expectedCode)
    {
        return block is BlockMultiblock proxy
            && block.Code?.Equals(expectedCode) == true
            && proxy.GetControlBlockPos(proxyPos).Equals(mainPos);
    }

    private static AssetLocation GetStationProxyCode(Vec3i offset)
    {
        return new AssetLocation("game", $"multiblock-monolithic-{OffsetCode(offset.X)}-{OffsetCode(offset.Y)}-{OffsetCode(offset.Z)}");
    }

    private IEnumerable<Vec3i> GetStationProxyOffsets(BlockFacing side)
    {
        if (IsDecodingStation())
        {
            yield return new Vec3i(0, 1, 0);
            yield break;
        }

        Vec3i horizontalOffset = GetProxyOffset(side.Code);
        yield return horizontalOffset;

        for (int y = 1; y < StationProxyRows; y++)
        {
            yield return new Vec3i(0, y, 0);
            yield return new Vec3i(horizontalOffset.X, y, horizontalOffset.Z);
        }
    }

    internal IEnumerable<BlockPos> GetStationProxyPositions(BlockPos mainPos)
    {
        foreach (Vec3i offset in GetStationProxyOffsets(GetSide()))
        {
            yield return mainPos.AddCopy(offset.X, offset.Y, offset.Z);
        }
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

    // Offset of the block the player is actually looking at, relative to this controller block.
    // For legacy "main"/"proxy" parts the selection always targets this block, so fall back to the
    // static part offset. For the vanilla-multiblock layout the controller handles both its own
    // selection (offset 0) and proxy parts forwarded by BlockMultiblock (offset = proxy - main).
    private Vec3i ResolveSelectionPartOffset(BlockSelection selection, BlockPos controllerPos)
    {
        if (UsesLegacyParts())
        {
            return GetPartOffset();
        }

        return new Vec3i(
            selection.Position.X - controllerPos.X,
            selection.Position.Y - controllerPos.Y,
            selection.Position.Z - controllerPos.Z
        );
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

    private BlockFacing GetPlacementSide(IPlayer byPlayer)
    {
        BlockFacing playerFacing = BlockFacing.HorizontalFromYaw(byPlayer.Entity.Pos.Yaw);
        return IsDecodingStation() ? playerFacing : playerFacing.Opposite;
    }

    private static Cuboidf[] BuildSelectionBoxes(IReadOnlyList<StationElementZone> zones, Vec3i partOffset)
    {
        return BuildSelectionBoxes(zones, partOffset, null)!;
    }

    private static Cuboidf[]? BuildSelectionBoxes(IReadOnlyList<StationElementZone> zones, Vec3i partOffset, BlockEntityFAWorkStation? be)
    {
        if (zones.Count == 0)
        {
            return null;
        }

        var boxes = new List<Cuboidf>(zones.Count);

        foreach (StationElementZone zone in zones)
        {
            boxes.Add(ToPartBox(GetCurrentStationBox(zone, be), partOffset));
        }

        return boxes.ToArray();
    }

    private static Cuboidf GetCurrentStationBox(StationElementZone zone, BlockEntityFAWorkStation? be)
    {
        // Keep the large lid control envelope stationary so it remains usable while the lid moves.
        if (zone.ActionName == "LidOpen" || zone.AnimatedStationBox == null || be == null)
        {
            return zone.StationBox;
        }

        float progress = zone.ActionName switch
        {
            "FuelDoor" => be.GetAnimationProgress("fuelopen", be.FuelOpen),
            _ => 0f
        };

        return GetAnimationBox(zone, progress);
    }

    private static Cuboidf GetAnimationBox(StationElementZone zone, float progress)
    {
        if (zone.TryGetAnimationBox(progress, out Cuboidf exactBox))
        {
            return exactBox;
        }

        IReadOnlyList<StationElementKeyframe>? boxes = zone.AnimationBoxes;
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

    private bool IncludeAllSelectionOwners(Vec3i partOffset)
    {
        return UsesModelDrivenStation()
            && !UsesLegacyParts()
            && !IsDecodingStation()
            && partOffset.X == 0
            && partOffset.Y == 0
            && partOffset.Z == 0;
    }

    private static List<StationElementZone> BuildSelectableZones(IReadOnlyList<StationElementZone> zones, Vec3i partOffset, BlockEntityFAWorkStation? be, bool includeAllOwners = false)
    {
        var result = new List<StationElementZone>();
        foreach (StationElementZone zone in zones)
        {
            if (zone.ActionName == "ArmorPlace")
            {
                continue;
            }

            if (be != null && !IsZoneSelectableForState(zone, be))
            {
                continue;
            }

            if (includeAllOwners || GetOwnerPartOffset(zone.StationBox, partOffset) == partOffset)
            {
                result.Add(zone);
            }
        }

        return result;
    }

    private static bool IsZoneSelectableForState(StationElementZone zone, BlockEntityFAWorkStation be)
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

    private static Vec3i GetOwnerPartOffset(Cuboidf stationBox, Vec3i partOffset)
    {
        int x = (int)Math.Floor((stationBox.X1 + stationBox.X2) * 0.5f);
        int y = (int)Math.Floor((stationBox.Y1 + stationBox.Y2) * 0.5f);
        int z = (int)Math.Floor((stationBox.Z1 + stationBox.Z2) * 0.5f);

        if (partOffset.X != 0)
        {
            x = GameMath.Clamp(x, Math.Min(0, partOffset.X), Math.Max(0, partOffset.X));
        }

        if (partOffset.Y != 0)
        {
            y = GameMath.Clamp(y, Math.Min(0, partOffset.Y), Math.Max(0, partOffset.Y));
        }

        if (partOffset.Z != 0)
        {
            z = GameMath.Clamp(z, Math.Min(0, partOffset.Z), Math.Max(0, partOffset.Z));
        }

        return new Vec3i(x, y, z);
    }

    private static string GetZoneInteractionText(StationElementZone zone, BlockEntityFAWorkStation? be, IPlayer forPlayer)
    {
        ItemStack? heldStack = forPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;

        return zone.ActionName switch
        {
            "LidOpen" => be?.LidOpen == true ? FaText.Get("Close lid") : FaText.Get("Open lid"),
            "FuelDoor" => be?.FuelOpen == true ? FaText.Get("Close fuel door") : FaText.Get("Open fuel door"),
            "Fuel" => GetFuelInteractionText(be, heldStack),
            "LiquidPour" => GetLiquidInteractionText(be, heldStack),
            "TableStorage" => GetTableInteractionText(be, heldStack),
            _ => FaText.Get("Use")
        };
    }

    private WorldInteraction[] GetStationZoneInteractions(IWorldAccessor world, StationElementZone zone, BlockEntityFAWorkStation? be, IPlayer forPlayer)
    {
        if (GetPurpose() == "cover" && zone.ActionName == "LiquidPour" && be?.HasLiquid != true)
        {
            coverSulfurBucketHelpStacks ??= GetFilledLiquidContainerStack(world, "game:woodbucket", "game:acid-full-sulfuric", 10f);
            return
            [
                new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Fill cauldron with sulfuric acid"),
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = coverSulfurBucketHelpStacks
                }
            ];
        }

        if (GetPurpose() == "cover" && zone.ActionName == "LiquidPour" && be?.CanGuideMetalPlateInsertion == true)
        {
            coverAcidMetalPlateHelpStacks ??= GetMatchingItemStacks(world, item => be.CanAcceptMetalPlateForAcid(new ItemStack(item)));
            return
            [
                new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Insert metal plate"),
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = coverAcidMetalPlateHelpStacks
                }
            ];
        }

        if (GetPurpose() == "cover" && zone.ActionName == "LiquidPour" && be?.CanGuideArmorInsertion == true)
        {
            string coatingMetal = be.CoatingMetal;
            if (!coverCoatingArmorHelpStacks.TryGetValue(coatingMetal, out ItemStack[]? armorStacks))
            {
                coatingArmorHelpCandidates ??= GetMatchingCreativeItemStacks(world, stack =>
                    ArmorResolver.Resolve(stack, out ResolvedArmor? armor).Allowed
                    && armor?.Definition.Schema == ArmorAttributeSchema.Layered);
                armorStacks = GetMatchingCachedItemStacks(coatingArmorHelpCandidates, be.CanAcceptArmorForCoating);
                coverCoatingArmorHelpStacks[coatingMetal] = armorStacks;
            }
            return
            [
                new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Insert armor"),
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = armorStacks
                }
            ];
        }

        if (GetPurpose() == "cover" && zone.ActionName == "Fuel")
        {
            var interactions = new List<WorldInteraction>();
            if (be?.FuelCount < be?.FuelCapacity)
            {
                coverFuelHelpStacks ??= GetMatchingItemStacks(world, item => be?.IsHoldingStationFuel(new ItemStack(item)) == true);
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Add coal or charcoal"),
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = coverFuelHelpStacks
                });
            }

            if (be?.CanIgnitePreparedFuel == true)
            {
                coverIgniterHelpStacks ??= world.Collectibles
                    .Where(collectible => be.IsHoldingIgniter(new ItemStack(collectible)))
                    .Select(collectible => new ItemStack(collectible)).ToArray();
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Hold: Light prepared fuel"),
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = coverIgniterHelpStacks
                });
            }

            if (be?.HasFuel == true)
            {
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Remove fuel"),
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true
                });
            }

            return interactions.ToArray();
        }

        if (GetPurpose() == "cover" && zone.ActionName is "Plate1" or "Plate2" or "Plate3" or "Plate4")
        {
            coverMetalPlateHelpStacks ??= GetMatchingItemStacks(world, item => item.Code?.Path.StartsWith("metalplate-", StringComparison.Ordinal) == true);
            return
            [
                new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Store or remove metal plate"),
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = coverMetalPlateHelpStacks
                }
            ];
        }

        if (GetPurpose() == "decoration" && zone.ActionName is "HeadDecorations" or "BodyDecorations" or "LegsDecorations")
        {
            string piece = zone.ActionName.StartsWith("Head", StringComparison.Ordinal) ? "head"
                : zone.ActionName.StartsWith("Body", StringComparison.Ordinal) ? "body"
                : "legs";
            string prefix = piece == "head" ? "ornaments-" : piece == "body" ? "brackets-" : "fasteners-";
            string fittingName = piece == "head" ? "ornament" : piece == "body" ? "bracket" : "fastener";
            if (!decorationFittingHelpStacks.TryGetValue(piece, out ItemStack[]? fittingStacks))
            {
                fittingStacks = GetMatchingItemStacks(world, item => item.Code?.Domain == "facore"
                    && (item.Code.Path.StartsWith(prefix, StringComparison.Ordinal)
                        || item.Code.Path.StartsWith("wolfdec-" + piece + "-", StringComparison.Ordinal)));
                decorationFittingHelpStacks[piece] = fittingStacks;
            }
            decorationKitHelpStacks ??= GetLoadedCollectibleStacks(world, "facore:decorationkit");
            decorationClothHelpStacks ??= GetMatchingCreativeItemStacks(world, stack =>
                BlockEntityFAWorkStation.TryGetClothColor(stack, out _))
                .DistinctBy(stack => stack.Collectible.Code).ToArray();
            ArmorAttributeSchema? schema = be?.GetDecorationArmorSchema(piece);
            var interactions = new List<WorldInteraction>();
            if (schema is null or ArmorAttributeSchema.Layered)
            {
                interactions.Add(new WorldInteraction { ActionLangCode = FaText.Get("Insert {0}", FaText.Get(fittingName)), MouseButton = EnumMouseButton.Right, Itemstacks = fittingStacks });
                if (be?.DecorationSuppliesOwnColor(piece) != true)
                {
                    interactions.Add(new WorldInteraction { ActionLangCode = FaText.Get("Insert filled decoration kit"), MouseButton = EnumMouseButton.Right, Itemstacks = decorationKitHelpStacks });
                }
            }
            if (schema is null or ArmorAttributeSchema.ThreeColor)
            {
                int pileCount = be?.GetDecorationClothPileCount(piece) ?? 0;
                if (pileCount < 3)
                {
                    interactions.Add(new WorldInteraction
                    {
                        ActionLangCode = FaText.Get("Insert cloth pile {0}/3 (3 matching cloth needed)", pileCount + 1),
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = decorationClothHelpStacks
                    });
                }
            }
            return interactions.ToArray();
        }

        if (GetPurpose() == "decoration" && zone.ActionName is "HeadPlace" or "BodyPlace" or "LegsPlace")
        {
            decorationHammerHelpStacks ??= GetMatchingItemStacks(world, item => item.Code?.Path.StartsWith("hammer-", StringComparison.Ordinal) == true);
            decorationSawHelpStacks ??= GetMatchingItemStacks(world, item => item.Code?.Path.StartsWith("saw-", StringComparison.Ordinal) == true);
            decorationShearsHelpStacks ??= GetMatchingItemStacks(world, item =>
                item.Code?.Path.StartsWith("shears-", StringComparison.Ordinal) == true
                || item.Code?.Path.StartsWith("scissors-", StringComparison.Ordinal) == true);
            var interactions = new List<WorldInteraction>();
            switch (be?.GetDecorationToolGuide(zone.ActionName))
            {
                case "hammer":
                    interactions.Add(new WorldInteraction { ActionLangCode = FaText.Get("Hold: Apply decoration"), MouseButton = EnumMouseButton.Right, Itemstacks = decorationHammerHelpStacks });
                    break;
                case "saw":
                    interactions.Add(new WorldInteraction { ActionLangCode = FaText.Get("Hold: Remove decoration"), MouseButton = EnumMouseButton.Right, Itemstacks = decorationSawHelpStacks });
                    break;
                case "shears":
                    interactions.Add(new WorldInteraction { ActionLangCode = FaText.Get("Hold: Apply cloth colors"), MouseButton = EnumMouseButton.Right, Itemstacks = decorationShearsHelpStacks });
                    break;
            }
            interactions.Add(new WorldInteraction { ActionLangCode = FaText.Get("Place or remove armor"), MouseButton = EnumMouseButton.Right });
            return interactions.ToArray();
        }

        if (GetPurpose() == "trim" && zone.ActionName == "Armor")
        {
            if (be?.HasTrimArmor != true)
            {
                return
                [
                    new WorldInteraction
                    {
                        ActionLangCode = FaText.Get("Hang armor"),
                        MouseButton = EnumMouseButton.Right
                    }
                ];
            }

            List<WorldInteraction> interactions =
            [
                new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Remove armor"),
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true
                }
            ];

            if (be.HasStagedTrim)
            {
                trimSolderingIronHelpStacks ??= GetLoadedItemStacks(world, "game:solderingiron");
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Hold: Solder rivets"),
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = trimSolderingIronHelpStacks
                });
            }

            if (be.HasBakedTrim)
            {
                trimTongsHelpStacks ??= GetMatchingItemStacks(world, item =>
                    item.Code?.Path.Contains("tongs", StringComparison.OrdinalIgnoreCase) == true);
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Hold: Remove soldered rivets"),
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = trimTongsHelpStacks
                });
            }

            return interactions.ToArray();
        }

        if (GetPurpose() == "trim" && zone.ActionName == "RivetsPlace")
        {
            if (be?.HasTrimRivets != true)
            {
                trimRivetsHelpStacks ??= GetLoadedItemStacks(world,
                    "facore:rimsandrivets-bismuth",
                    "facore:rimsandrivets-blackbronze",
                    "facore:rimsandrivets-brass",
                    "facore:rimsandrivets-copper",
                    "facore:rimsandrivets-cupronickel",
                    "facore:rimsandrivets-electrum",
                    "facore:rimsandrivets-gold",
                    "facore:rimsandrivets-lead",
                    "facore:rimsandrivets-meteoriciron",
                    "facore:rimsandrivets-silver",
                    "facore:rimsandrivets-uranium",
                    "facore:rimsandrivets-zinc");
                return
                [
                    new WorldInteraction
                    {
                        ActionLangCode = FaText.Get("Insert rims and rivets"),
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = trimRivetsHelpStacks
                    }
                ];
            }

            return
            [
                new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Remove rims and rivets"),
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true
                }
            ];
        }

        if (GetPurpose() == "trim" && zone.ActionName == "CruciblePlace")
        {
            if (be?.HasTrimCrucible != true)
            {
                trimCrucibleHelpStacks ??= GetLoadedCollectibleStacks(world, "game:crucible");
                return
                [
                    new WorldInteraction
                    {
                        ActionLangCode = FaText.Get("Insert crucible with silver solder"),
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = trimCrucibleHelpStacks
                    }
                ];
            }

            return
            [
                new WorldInteraction
                {
                    ActionLangCode = FaText.Get("Remove crucible"),
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true
                }
            ];
        }

        return
        [
            new WorldInteraction
            {
                ActionLangCode = IsCoverStation() ? GetZoneInteractionText(zone, be, forPlayer) : GetStationZoneInteractionText(zone),
                MouseButton = EnumMouseButton.Right
            }
        ];
    }

    private static ItemStack[] GetLoadedItemStacks(IWorldAccessor world, params string[] codes)
    {
        List<ItemStack> stacks = [];
        foreach (string code in codes)
        {
            Item? item = world.GetItem(new AssetLocation(code));
            if (item != null)
            {
                stacks.Add(new ItemStack(item));
            }
        }

        return stacks.ToArray();
    }

    private static ItemStack[] GetLoadedCollectibleStacks(IWorldAccessor world, params string[] codes)
    {
        List<ItemStack> stacks = [];
        foreach (string code in codes)
        {
            AssetLocation location = new(code);
            CollectibleObject? collectible = world.GetItem(location) ?? (CollectibleObject?)world.GetBlock(location);
            if (collectible != null) stacks.Add(new ItemStack(collectible));
        }
        return stacks.ToArray();
    }

    private static ItemStack[] GetFilledLiquidContainerStack(IWorldAccessor world, string containerCode, string liquidCode, float litres)
    {
        CollectibleObject? container = world.GetBlock(new AssetLocation(containerCode)) ?? (CollectibleObject?)world.GetItem(new AssetLocation(containerCode));
        Item? liquid = world.GetItem(new AssetLocation(liquidCode));
        if (container == null || liquid == null) return [];

        ItemStack containerStack = new(container);
        if (containerStack.Collectible is not ILiquidSink sink) return [];

        ItemStack liquidStack = new(liquid, 10000);
        return sink.TryPutLiquid(containerStack, liquidStack, litres) > 0 ? [containerStack] : [];
    }

    private static ItemStack[] GetMatchingItemStacks(IWorldAccessor world, System.Func<Item, bool> predicate)
    {
        List<ItemStack> stacks = [];
        foreach (Item item in world.Items)
        {
            if (item.Code != null && predicate(item)) stacks.Add(new ItemStack(item));
        }
        return stacks.ToArray();
    }

    private static ItemStack[] GetMatchingCreativeItemStacks(IWorldAccessor world, System.Func<ItemStack, bool> predicate)
    {
        List<ItemStack> stacks = [];
        foreach (Item item in world.Items)
        {
            if (item.Code == null || item.CreativeInventoryStacks == null) continue;
            foreach (CreativeTabAndStackList group in item.CreativeInventoryStacks)
            {
                foreach (JsonItemStack jsonStack in group.Stacks)
                {
                    ItemStack? stack = jsonStack.ResolvedItemstack;
                    if (stack?.Item != null && stack.Collectible.Code != null && predicate(stack)) stacks.Add(stack.Clone());
                }
            }
        }
        return stacks.ToArray();
    }

    private static ItemStack[] GetMatchingCachedItemStacks(IEnumerable<ItemStack> candidates, System.Func<ItemStack, bool> predicate)
    {
        List<ItemStack> stacks = [];
        foreach (ItemStack stack in candidates)
        {
            if (predicate(stack)) stacks.Add(stack);
        }
        return stacks.ToArray();
    }

    private static string GetStationZoneInteractionText(StationElementZone zone)
    {
        return zone.ActionName switch
        {
            "HeadPlace" => FaText.Get("Use helmet place"),
            "BodyPlace" => FaText.Get("Use chestplate place"),
            "LegsPlace" => FaText.Get("Use leggings place"),
            "HeadDecorations" => FaText.Get("Use helmet decoration materials"),
            "BodyDecorations" => FaText.Get("Use chestplate decoration materials"),
            "LegsDecorations" => FaText.Get("Use leggings decoration materials"),
            "SolderHolder" => FaText.Get("Use soldering iron holder"),
            "CruciblePlace" => FaText.Get("Use crucible stand"),
            "Armor" => FaText.Get("Use hanging armor"),
            "RivetsPlace" => FaText.Get("Use rims and rivets bowl"),
            _ => FaText.Get("Use ") + zone.ActionName
        };
    }

    private static string GetTableInteractionText(BlockEntityFAWorkStation? be, ItemStack? heldStack)
    {
        if (be?.HasTableItem == true)
        {
            return heldStack == null ? FaText.Get("Take item") : FaText.Get("Table occupied");
        }

        return heldStack == null ? FaText.Get("Table top") : FaText.Get("Place item");
    }

    private static string GetFuelInteractionText(BlockEntityFAWorkStation? be, ItemStack? heldStack)
    {
        if (be == null)
        {
            return FaText.Get("Use fuel tray");
        }

        if (heldStack == null)
        {
            return be.HasFuel ? FaText.Get("Take fuel") : FaText.Get("Use fuel tray");
        }

        if (be.IsHoldingIgniter(heldStack))
        {
            return FaText.Get("Light fuel");
        }

        if (be.IsHoldingStationFuel(heldStack))
        {
            return FaText.Get("Add fuel");
        }

        return FaText.Get("Use fuel tray");
    }

    private static string GetLiquidInteractionText(BlockEntityFAWorkStation? be, ItemStack? heldStack)
    {
        if (be == null)
        {
            return FaText.Get("Use cauldron");
        }

        if (heldStack == null)
        {
            return be.HasImmersedItem ? FaText.Get("Take item") : FaText.Get("Use cauldron");
        }

        if (be.IsHoldingLiquidContainer(heldStack))
        {
            return be.HasLiquid ? FaText.Get("Take liquid") : FaText.Get("Pour liquid");
        }

        if (be.IsHoldingCauldronItem(heldStack))
        {
            return FaText.Get("Insert item");
        }

        return FaText.Get("Use cauldron");
    }

    private static string GetCauldronItemInteractionText(BlockEntityFAWorkStation? be, ItemStack? heldStack)
    {
        if (heldStack == null)
        {
            return be?.HasImmersedItem == true ? FaText.Get("Take item") : FaText.Get("Use cauldron");
        }

        return be?.IsHoldingCauldronItem(heldStack) == true ? FaText.Get("Insert item") : FaText.Get("Use cauldron");
    }

    private static void Notify(IPlayer player, string text)
    {
        if (player is IServerPlayer serverPlayer)
        {
            serverPlayer.SendIngameError("facore-coverstation", text);
        }
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private static void StationDebugLog(ILogger logger, string format, params object?[] args)
    {
        logger.Notification(format, args!);
    }

    private sealed class HeldStationOperation(
        BlockEntityFAWorkStation targetStation,
        long targetRevision,
        BlockPos controllerPos,
        string actionName,
        ItemStack? toolStack,
        long startedAtMilliseconds)
    {
        public double ElapsedSeconds => (Environment.TickCount64 - startedAtMilliseconds) / 1000.0;

        public bool Matches(IPlayer player, BlockEntityFAWorkStation station, BlockPos position, string action) =>
            ReferenceEquals(targetStation, station)
            && targetRevision == station.GetHeldInteractionRevision(action)
            && controllerPos.Equals(position)
            && string.Equals(actionName, action, StringComparison.Ordinal)
            && ReferenceEquals(toolStack, player.InventoryManager?.ActiveHotbarSlot?.Itemstack);
    }

    private sealed class DecodingHeldOperation
    {
        private readonly BlockPos controllerPos;
        private readonly ItemStack? toolStack;
        private readonly long startedAtMilliseconds;

        public DecodingHeldOperation(BlockEntityFADecodingTable station, long token, BlockPos controllerPos, ItemStack? toolStack, long startedAtMilliseconds)
        {
            Station = station;
            Token = token;
            this.controllerPos = controllerPos;
            this.toolStack = toolStack;
            this.startedAtMilliseconds = startedAtMilliseconds;
        }

        public BlockEntityFADecodingTable Station { get; }
        public long Token { get; }
        public double ElapsedSeconds => (Environment.TickCount64 - startedAtMilliseconds) / 1000.0;

        public bool Matches(IPlayer player, BlockEntityFADecodingTable currentStation, BlockPos position) =>
            ReferenceEquals(Station, currentStation)
            && controllerPos.Equals(position)
            && ReferenceEquals(toolStack, player.InventoryManager?.ActiveHotbarSlot?.Itemstack);
    }
}
