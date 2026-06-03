using System;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace FACore;

public class BlockEntityFACoverStation : BlockEntity
{
    private const bool DebugLiquid = true;
    private static readonly AssetLocation LidOpenSound = new("facore", "sounds/coverstation/lidopen");
    private static readonly AssetLocation LidCloseSound = new("facore", "sounds/coverstation/lidclose");
    private static readonly AssetLocation FireplaceOpenSound = new("facore", "sounds/coverstation/fireplaceopen");
    private static readonly AssetLocation FireplaceCloseSound = new("facore", "sounds/coverstation/fireplaceclose");
    private static readonly AssetLocation[] BubblingSounds =
    [
        new("facore", "sounds/coverstation/bubbling01"),
        new("facore", "sounds/coverstation/bubbling02"),
        new("facore", "sounds/coverstation/bubbling03")
    ];
    private static readonly AssetLocation[] CharcoalPlaceSounds =
    [
        new("game", "sounds/block/charcoal1"),
        new("game", "sounds/block/charcoal2"),
        new("game", "sounds/block/charcoal3")
    ];
    private static readonly AssetLocation IgniteSound = new("game", "sounds/torch-ignite");
    private static readonly AssetLocation WaterPourSound = new("game", "sounds/effect/water-pour");
    private static readonly AssetLocation[] ItemSplashSounds =
    [
        new("game", "sounds/environment/smallsplash"),
        new("game", "sounds/environment/mediumsplash")
    ];
    private static readonly AssetLocation SulfuricAcidCode = new("game:acid-full-sulfuric");
    private static readonly AssetLocation CoatingLiquidCodePrefix = new("facore", "coating-full-");
    private static readonly AssetLocation SulfuricAcidTexture = new("survival:block/liquid/dye/yellow");
    private static readonly Dictionary<string, string> CoatingDyeColors = new(StringComparer.Ordinal)
    {
        ["bismuth"] = "purple",
        ["bismuthbronze"] = "brown",
        ["blackbronze"] = "black",
        ["brass"] = "yellow",
        ["chromium"] = "white",
        ["copper"] = "orange",
        ["cupronickel"] = "gray",
        ["electrum"] = "yellow",
        ["gold"] = "yellow",
        ["iron"] = "gray",
        ["lead"] = "gray",
        ["meteoriciron"] = "gray",
        ["molybdochalkos"] = "orange",
        ["nickel"] = "gray",
        ["platinum"] = "white",
        ["silver"] = "white",
        ["stainlesssteel"] = "gray",
        ["steel"] = "gray",
        ["tin"] = "white",
        ["tinbronze"] = "brown",
        ["titanium"] = "white",
        ["uranium"] = "green",
        ["zinc"] = "white"
    };
    private static readonly AssetLocation CharcoalCode = new("game:charcoal");
    private static readonly HashSet<string> SupportedArmorCoatingMetals = new(StringComparer.Ordinal)
    {
        "copper",
        "cupronickel",
        "brass",
        "zinc",
        "blackbronze",
        "lead",
        "silver",
        "meteoriciron",
        "gold",
        "electrum"
    };
    private static readonly AssetLocation[] FallbackForgeCoalShapes =
    [
        new("survival", "shapes/block/stone/forge/coal"),
        new("survival", "shapes/block/stone/forge/coal.json"),
        new("survival", "block/stone/forge/coal"),
        new("game", "shapes/block/stone/forge/coal"),
        new("game", "shapes/block/stone/forge/coal.json"),
        new("game", "block/stone/forge/coal")
    ];
    private const int ItemsPerLitre = 100;
    private const int LiquidCapacityLitres = 10;
    private const int LiquidCapacityItems = LiquidCapacityLitres * ItemsPerLitre;
    private const int MaxCharcoalPieces = 5;
    private const double PlateRestHours = 2.0;
    private const double ArmorCoatingHours = 2.0;
    private const string ProcessPlateResting = "plate";
    private const string ProcessArmorCoating = "armor";
    private const float LiquidParentOffsetY = 7.5f / 16f;
    private const float LiquidMinY = 0.36f + LiquidParentOffsetY;
    private const float LiquidMaxY = 0.55f + LiquidParentOffsetY;
    private const float LiquidMinX = 14f / 16f;
    private const float LiquidMaxX = 30f / 16f;
    private const float LiquidMinZ = 2f / 16f;
    private const float LiquidMaxZ = 13.8f / 16f;
    private const float ImmersedPlateMaxDimension = 0.42f;
    private const float ImmersedArmorMaxDimension = 0.55f;
    private const float ImmersedPlateTopY = LiquidMaxY + 0.055f;
    private const float ImmersedArmorTopY = LiquidMaxY + 0.075f;
    private static readonly Vec3f FuelMeshRotationOrigin = new(0.5f, 0.5f, 0.5f);
    private const float FuelMeshScale = 1.2f;
    private const float FuelMinX = 16f / 16f;
    private const float FuelMaxX = 28f / 16f;
    private const float FuelMinY = 1f / 16f;
    private const float FuelMinZ = 1.5f / 16f;
    private const float FuelMaxZ = 13.5f / 16f;

    private bool lidOpen;
    private bool fuelOpen;
    private bool fuelLit;
    private ItemStack? liquidStack;
    private ItemStack? immersedStack;
    private ItemStack? fuelStack;
    private ItemStack? tableStack;
    private string processMode = "";
    private string processMetal = "";
    private double processStartHours = -1;
    private bool animationStateKnown;
    private bool lastSyncedLidOpen;
    private bool lastSyncedFuelOpen;
    private TextureAtlasPosition? cachedSulfurTexturePosition;
    private int cachedSulfurTextureSubId;
    private readonly Dictionary<string, TextureAtlasPosition> cachedLiquidTexturePositions = new();
    private readonly Dictionary<string, int> cachedLiquidTextureSubIds = new();
    private TextureAtlasPosition? cachedCharcoalTexturePosition;
    private int cachedCharcoalTextureSubId;
    private ILoadedSound? processLoopSound;

    public bool LidOpen => lidOpen;
    public bool FuelOpen => fuelOpen;

    public float GetAnimationProgress(string code, bool openFallback)
    {
        RunningAnimation? runningAnimation = AnimUtil?.animator?.GetAnimationState(code);
        return runningAnimation == null ? openFallback ? 1f : 0f : GameMath.Clamp(runningAnimation.AnimProgress, 0f, 1f);
    }

    public void HandleElementInteraction(IPlayer byPlayer, string actionName)
    {
        switch (actionName)
        {
            case "LidOpen":
                UpdateProcess();
                if (fuelLit || processMode == ProcessArmorCoating)
                {
                    Notify(byPlayer, "The cauldron is too hot to open.");
                    return;
                }

                AnimDebugLog($"HandleElementInteraction LidOpen before={lidOpen}");
                lidOpen = !lidOpen;
                AnimDebugLog($"HandleElementInteraction LidOpen after={lidOpen}");
                PlayStationSound(lidOpen ? LidOpenSound : LidCloseSound, byPlayer);
                MarkStationDirty(redrawOnClient: false);
                return;

            case "FuelDoor":
                UpdateProcess();

                AnimDebugLog($"HandleElementInteraction FuelDoor before={fuelOpen}");
                fuelOpen = !fuelOpen;
                AnimDebugLog($"HandleElementInteraction FuelDoor after={fuelOpen}");
                PlayStationSound(fuelOpen ? FireplaceOpenSound : FireplaceCloseSound, byPlayer);
                MarkStationDirty(redrawOnClient: false);
                return;

            case "Fuel":
                TryInteractFuel(byPlayer);
                return;

            case "LiquidPour":
                TryInteractLiquid(byPlayer);
                return;

            default:
                Notify(byPlayer, $"{actionName} zone is detected.");
                return;
        }
    }

    public string DescribeState()
    {
        var builder = new StringBuilder();
        AppendStationDisplayInfo(builder);
        return builder.ToString();
    }

    private void AppendStationDisplayInfo(StringBuilder builder)
    {
        if (liquidStack != null)
        {
            builder.Append(IsSulfuricAcid(liquidStack) ? "Sulfur" : GetLiquidName(liquidStack));
        }

        if (immersedStack != null)
        {
            if (builder.Length > 0) builder.AppendLine();
            builder.Append(immersedStack.GetName());
        }

        if (builder.Length > 0) builder.AppendLine();
        builder.Append("Fuel: ");
        builder.Append(fuelStack?.StackSize ?? 0);
        builder.Append("/");
        builder.Append(MaxCharcoalPieces);

        if (fuelLit)
        {
            builder.Append(" lit");
        }

        if (HasActiveProcess())
        {
            builder.AppendLine();
            builder.Append(processMode == ProcessPlateResting ? "Plate: " : "Armor: ");
            builder.Append(FormatRemainingProcessTime());
        }
    }

    public bool IsHoldingStationFuel(ItemStack? stack)
    {
        return IsStationFuel(stack);
    }

    public bool IsHoldingIgniter(ItemStack? stack)
    {
        return CanLightFuel(stack);
    }

    public bool IsHoldingLiquidContainer(ItemStack? stack)
    {
        return stack?.Collectible is ILiquidInterface or ILiquidSource or ILiquidSink;
    }

    public bool IsHoldingCauldronItem(ItemStack? stack)
    {
        return TryGetMetalPlateMetal(stack, out _) || TryGetFAArmorPiece(stack, out _);
    }
    public bool HasImmersedItem => immersedStack != null;
    public bool HasLiquid => liquidStack != null && liquidStack.StackSize > 0;
    public bool HasFuel => fuelStack != null && fuelStack.StackSize > 0;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        AnimDebugLog($"Initialize side={api.Side}, block={Block?.Code}, behaviors={FormatBehaviors()}");

        if (api.Side == EnumAppSide.Client)
        {
            CacheSulfurTexture();
            CacheCharcoalTexture();
            EnsureAnimator();
            SyncAnimations();
            SyncProcessLoopSound();
        }

        if (api.Side == EnumAppSide.Server)
        {
            RegisterGameTickListener(OnProcessTick, 3000);
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        AnimDebugLog($"ToTreeAttributes lidOpen={lidOpen}, fuelOpen={fuelOpen}, fuelLit={fuelLit}");
        tree.SetBool("lidOpen", lidOpen);
        tree.SetBool("fuelOpen", fuelOpen);
        tree.SetBool("fuelLit", fuelLit);
        tree.SetString("processMode", processMode);
        tree.SetString("processMetal", processMetal);
        tree.SetDouble("processStartHours", processStartHours);
        SetOrRemoveItemstack(tree, "liquidStack", liquidStack);
        SetOrRemoveItemstack(tree, "immersedStack", immersedStack);
        SetOrRemoveItemstack(tree, "fuelStack", fuelStack);
        SetOrRemoveItemstack(tree, "tableStack", tableStack);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        lidOpen = tree.GetBool("lidOpen");
        fuelOpen = tree.GetBool("fuelOpen");
        fuelLit = tree.GetBool("fuelLit");
        processMode = tree.GetString("processMode") ?? "";
        processMetal = tree.GetString("processMetal") ?? "";
        processStartHours = tree.GetDouble("processStartHours", -1);
        AnimDebugLog($"FromTreeAttributes side={Api?.Side.ToString() ?? "no-api"}, lidOpen={lidOpen}, fuelOpen={fuelOpen}, fuelLit={fuelLit}");
        liquidStack = ResolveItemstack(tree.GetItemstack("liquidStack"), worldForResolving);
        immersedStack = ResolveItemstack(tree.GetItemstack("immersedStack"), worldForResolving);
        fuelStack = ResolveItemstack(tree.GetItemstack("fuelStack"), worldForResolving);
        tableStack = ResolveItemstack(tree.GetItemstack("tableStack"), worldForResolving);

        if (Api?.Side == EnumAppSide.Client)
        {
            CacheSulfurTexture();
            CacheCharcoalTexture();
            EnsureAnimator();
            SyncAnimations();
            SyncProcessLoopSound();
        }
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        bool skipDefaultMesh = base.OnTesselation(mesher, tessThreadTesselator);
        if (lidOpen || fuelOpen)
        {
            skipDefaultMesh = true;
        }

        if (IsStationFuel(fuelStack))
        {
            MeshData? fuelMesh = CreateFuelMesh(tessThreadTesselator);
            if (fuelMesh != null)
            {
                DebugFuelLog($"OnTesselation adding fuel mesh. vertices={fuelMesh.VerticesCount}, indices={fuelMesh.IndicesCount}");
                mesher.AddMeshData(fuelMesh, 1);
            }
        }

        bool hasVisibleLiquid = liquidStack != null && IsCauldronLiquid(liquidStack) && liquidStack.StackSize > 0;
        if (!hasVisibleLiquid)
        {
            DebugLiquidLog($"OnTesselation no visible liquid. skipDefaultMesh={skipDefaultMesh}, liquidStack={FormatStackDebug(liquidStack)}");
            MeshData? dryImmersedMesh = CreateImmersedItemMesh(tessThreadTesselator, hasVisibleLiquid: false);
            if (dryImmersedMesh != null)
            {
                DebugLiquidLog($"OnTesselation adding dry immersed item mesh. vertices={dryImmersedMesh.VerticesCount}, indices={dryImmersedMesh.IndicesCount}, bounds={FormatMeshBounds(dryImmersedMesh)}");
                mesher.AddMeshData(dryImmersedMesh, 1);
            }

            return skipDefaultMesh;
        }

        DebugLiquidLog($"OnTesselation liquid present. skipDefaultMesh={skipDefaultMesh}, liquidStack={FormatStackDebug(liquidStack)}");
        MeshData? liquidMesh = CreateLiquidMesh(tessThreadTesselator);
        if (liquidMesh == null)
        {
            DebugLiquidLog("OnTesselation liquid mesh is null.");
            return skipDefaultMesh;
        }

        DebugLiquidLog($"OnTesselation adding liquid mesh. vertices={liquidMesh.VerticesCount}, indices={liquidMesh.IndicesCount}, renderPasses={liquidMesh.RenderPassCount}, needsLiquid={liquidMesh.NeedsRenderPass(EnumChunkRenderPass.Liquid)}, needsTransparent={liquidMesh.NeedsRenderPass(EnumChunkRenderPass.Transparent)}");
        mesher.AddMeshData(liquidMesh, 1);
        DebugLiquidLog("OnTesselation liquid mesh added to terrain pool.");

        MeshData? immersedMesh = CreateImmersedItemMesh(tessThreadTesselator, hasVisibleLiquid: true);
        if (immersedMesh != null)
        {
            DebugLiquidLog($"OnTesselation adding immersed item mesh after liquid. vertices={immersedMesh.VerticesCount}, indices={immersedMesh.IndicesCount}, bounds={FormatMeshBounds(immersedMesh)}");
            mesher.AddMeshData(immersedMesh, 1);
        }
        return skipDefaultMesh;
    }

    public override void OnBlockBroken(IPlayer byPlayer)
    {
        StopProcessLoopSound(immediate: true);
        base.OnBlockBroken(byPlayer);

        if (immersedStack != null)
        {
            Api.World.SpawnItemEntity(immersedStack, Pos.ToVec3d().Add(0.5, 0.75, 0.5));
            immersedStack = null;
        }

        if (fuelStack != null)
        {
            Api.World.SpawnItemEntity(fuelStack, Pos.ToVec3d().Add(0.5, 0.35, 0.5));
            fuelStack = null;
        }

        if (tableStack != null)
        {
            Api.World.SpawnItemEntity(tableStack, Pos.ToVec3d().Add(0.5, 1.05, 0.5));
            tableStack = null;
        }
    }

    public override void OnBlockRemoved()
    {
        StopProcessLoopSound(immediate: true);
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        StopProcessLoopSound(immediate: true);
        base.OnBlockUnloaded();
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        if (dsc.Length > 0)
        {
            dsc.AppendLine();
        }

        AppendStationDisplayInfo(dsc);
    }

    private void TryInteractFuel(IPlayer byPlayer)
    {
        UpdateProcess();

        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot == null || activeSlot.Empty)
        {
            TryTakeFuel(byPlayer);
            return;
        }

        ItemStack? heldStack = activeSlot.Itemstack;
        if (CanLightFuel(heldStack))
        {
            TryLightFuel(byPlayer);
            return;
        }

        TryInsertFuel(byPlayer, activeSlot);
    }

    private void TryInsertFuel(IPlayer byPlayer, ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (!IsStationFuel(heldStack))
        {
            Notify(byPlayer, "Hold charcoal or coal.");
            return;
        }

        if (fuelLit)
        {
            Notify(byPlayer, "The fuel is already lit.");
            return;
        }

        if (fuelStack != null && !fuelStack.Equals(Api.World, heldStack, GlobalConstants.IgnoredStackAttributes))
        {
            Notify(byPlayer, "Take out the current fuel before adding a different one.");
            return;
        }

        int room = MaxCharcoalPieces - (fuelStack?.StackSize ?? 0);
        if (room <= 0)
        {
            Notify(byPlayer, $"The fuel tray is full ({MaxCharcoalPieces}/{MaxCharcoalPieces}).");
            return;
        }

        ItemStack inserted = activeSlot.TakeOut(Math.Min(1, Math.Min(room, heldStack!.StackSize)));
        if (fuelStack == null)
        {
            fuelStack = inserted;
        }
        else
        {
            fuelStack.StackSize += inserted.StackSize;
        }

        activeSlot.MarkDirty();
        PlayCharcoalPlaceSound(byPlayer);
        MarkStationDirty();
        Notify(byPlayer, $"Added {inserted.StackSize}x {inserted.GetName()}.");
    }

    private void TryTakeFuel(IPlayer byPlayer)
    {
        if (fuelStack == null)
        {
            Notify(byPlayer, "The fuel tray is empty.");
            return;
        }

        if (fuelLit)
        {
            Notify(byPlayer, "The fuel is lit.");
            return;
        }

        ItemStack takeStack = fuelStack.Clone();
        takeStack.StackSize = 1;
        string takeName = takeStack.GetName();
        fuelStack.StackSize -= 1;
        if (fuelStack.StackSize <= 0)
        {
            fuelStack = null;
        }

        if (!byPlayer.InventoryManager.TryGiveItemstack(takeStack, true))
        {
            Api.World.SpawnItemEntity(takeStack, Pos.ToVec3d().Add(0.5, 0.35, 0.5));
        }

        MarkStationDirty();
        PlayCharcoalPlaceSound(byPlayer);
        Notify(byPlayer, $"Took 1x {takeName}.");
    }

    private void TryLightFuel(IPlayer byPlayer)
    {
        UpdateProcess();

        if (fuelStack == null)
        {
            Notify(byPlayer, "Add fuel first.");
            return;
        }

        if (fuelStack.StackSize < MaxCharcoalPieces)
        {
            Notify(byPlayer, $"Load the fuel tray fully first ({MaxCharcoalPieces}/{MaxCharcoalPieces} fuel).");
            return;
        }

        if (fuelLit)
        {
            Notify(byPlayer, "The fuel is already lit.");
            return;
        }

        if (lidOpen)
        {
            Notify(byPlayer, "Close the cauldron lid before lighting the fuel.");
            return;
        }

        if (processMode == ProcessPlateResting)
        {
            Notify(byPlayer, "Let the metal plate finish reacting before lighting the fuel.");
            return;
        }

        if (!TryGetCoatingMetal(liquidStack, out string coatingMetal))
        {
            Notify(byPlayer, "Prepare coating liquid before lighting the fuel.");
            return;
        }

        if (!TryGetFAArmorPiece(immersedStack, out string piece))
        {
            Notify(byPlayer, "Put a coatable FA armor piece into the cauldron first.");
            return;
        }

        if (!CanApplyArmorCoating(immersedStack, piece, coatingMetal, out string failure))
        {
            Notify(byPlayer, failure);
            return;
        }

        fuelLit = true;
        StartProcess(ProcessArmorCoating, coatingMetal);
        PlayStationSound(IgniteSound, byPlayer);
        PlayBubblingSound(byPlayer);
        MarkStationDirty();
        Notify(byPlayer, $"Lit the fuel. Coating will finish in {ArmorCoatingHours:0.#} in-game hours.");
    }

    private void TryInteractLiquid(IPlayer byPlayer)
    {
        UpdateProcess();

        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot == null || activeSlot.Empty)
        {
            TryTakeImmersedItem(byPlayer);
            return;
        }

        if (activeSlot.Itemstack == null)
        {
            Notify(byPlayer, liquidStack == null ? "The cauldron is empty." : $"The cauldron contains {FormatLitres(liquidStack.StackSize)}/{LiquidCapacityLitres}L {GetLiquidName(liquidStack)}.");
            return;
        }

        if (CanTakeSulfuricAcid(activeSlot))
        {
            TryPourIntoCauldron(byPlayer, activeSlot);
            return;
        }

        if (CanReceiveCauldronLiquid(activeSlot))
        {
            TryTakeFromCauldron(byPlayer, activeSlot);
            return;
        }

        ItemStack? heldStack = activeSlot.Itemstack;
        if (heldStack?.Collectible is ILiquidInterface or ILiquidSource or ILiquidSink)
        {
            Notify(byPlayer, liquidStack == null
                ? "Only sulfuric acid can be poured into this cauldron."
                : $"That container cannot interact with {GetLiquidName(liquidStack)}.");
            return;
        }

        TryInsertImmersedItem(byPlayer, activeSlot);
    }

    private void TryPourIntoCauldron(IPlayer byPlayer, ItemSlot activeSlot)
    {
        if (HasActiveProcess() || fuelLit)
        {
            Notify(byPlayer, "The cauldron is busy.");
            return;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "Take out the immersed item before changing the liquid.");
            return;
        }

        if (liquidStack != null && !IsSulfuricAcid(liquidStack))
        {
            Notify(byPlayer, $"The cauldron already contains {GetLiquidName(liquidStack)}.");
            return;
        }

        int missing = LiquidCapacityItems - (liquidStack?.StackSize ?? 0);
        if (missing <= 0)
        {
            Notify(byPlayer, $"The cauldron already has {LiquidCapacityLitres}L of sulfuric acid.");
            return;
        }

        if (!TryTakeSulfuricAcid(activeSlot, missing, out ItemStack acidStack, out string failure))
        {
            Notify(byPlayer, failure);
            return;
        }

        int inserted = InsertLiquid(acidStack);
        activeSlot.MarkDirty();
        PlayStationSound(WaterPourSound, byPlayer);
        MarkStationDirty();
        DebugLiquidLog($"TryPourIntoCauldron inserted={inserted}, liquidStack={FormatStackDebug(liquidStack)}, heldAfter={FormatStackDebug(activeSlot.Itemstack)}");
        Notify(byPlayer, $"Added {FormatLitres(inserted)}L sulfuric acid: {FormatLitres(liquidStack?.StackSize ?? 0)}/{LiquidCapacityLitres}L.");
    }

    private void TryTakeFromCauldron(IPlayer byPlayer, ItemSlot activeSlot)
    {
        UpdateProcess();

        if (HasActiveProcess() || fuelLit)
        {
            Notify(byPlayer, "The cauldron is busy.");
            return;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "Take out the immersed item before draining the cauldron.");
            return;
        }

        if (liquidStack == null || liquidStack.StackSize <= 0)
        {
            Notify(byPlayer, "The cauldron is empty.");
            return;
        }

        ItemStack? heldStack = activeSlot.Itemstack;
        if (heldStack?.Collectible is not ILiquidSink sink)
        {
            Notify(byPlayer, $"Hold a liquid container to take {GetLiquidName(liquidStack)} from the cauldron.");
            return;
        }

        ItemStack moveStack = liquidStack.Clone();
        float litres = Math.Min(LiquidCapacityLitres, liquidStack.StackSize / (float)ItemsPerLitre);
        int moved = sink.TryPutLiquid(heldStack, moveStack, litres);
        if (moved <= 0)
        {
            Notify(byPlayer, $"That container cannot take {GetLiquidName(liquidStack)}.");
            return;
        }

        int removed = Math.Min(liquidStack.StackSize, moved);
        liquidStack.StackSize -= removed;
        if (liquidStack.StackSize <= 0)
        {
            liquidStack = null;
        }

        activeSlot.MarkDirty();
        PlayStationSound(WaterPourSound, byPlayer);
        MarkStationDirty();
        DebugLiquidLog($"TryTakeFromCauldron removed={removed}, liquidStack={FormatStackDebug(liquidStack)}, heldAfter={FormatStackDebug(activeSlot.Itemstack)}");
        Notify(byPlayer, $"Took {FormatLitres(removed)}L {GetLiquidName(moveStack)}: {FormatLitres(liquidStack?.StackSize ?? 0)}/{LiquidCapacityLitres}L.");
    }

    private void TryInteractCauldronItem(IPlayer byPlayer)
    {
        UpdateProcess();

        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (activeSlot == null || activeSlot.Empty)
        {
            TryTakeImmersedItem(byPlayer);
            return;
        }

        TryInsertImmersedItem(byPlayer, activeSlot);
    }

    private void TryInsertImmersedItem(IPlayer byPlayer, ItemSlot activeSlot)
    {
        UpdateProcess();

        if (HasActiveProcess())
        {
            Notify(byPlayer, GetProcessStatusText());
            return;
        }

        if (fuelLit)
        {
            Notify(byPlayer, "The cauldron is too hot.");
            return;
        }

        if (TryStartPlateRest(byPlayer, activeSlot))
        {
            return;
        }

        if (TryInsertArmorForCoating(byPlayer, activeSlot))
        {
            return;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "The cauldron already holds an item.");
            return;
        }

        Notify(byPlayer, "Use a metal plate in sulfuric acid, or a FA armor piece in finished coating liquid.");
    }

    private void TryTakeImmersedItem(IPlayer byPlayer)
    {
        UpdateProcess();

        if (HasActiveProcess() || fuelLit)
        {
            Notify(byPlayer, "The cauldron is too hot.");
            return;
        }

        if (immersedStack == null)
        {
            Notify(byPlayer, "There is no immersed item in the cauldron.");
            return;
        }

        ItemStack takeStack = immersedStack;
        immersedStack = null;

        if (!byPlayer.InventoryManager.TryGiveItemstack(takeStack, true))
        {
            Api.World.SpawnItemEntity(takeStack, Pos.ToVec3d().Add(0.5, 0.75, 0.5));
        }

        MarkStationDirty();
        PlayItemSplashSound(byPlayer);
        Notify(byPlayer, "Took the immersed item.");
    }

    private bool TryTakeSulfuricAcid(ItemSlot activeSlot, int maxItems, out ItemStack acidStack, out string failure)
    {
        acidStack = null!;
        failure = "Hold sulfuric acid over the cauldron.";

        ItemStack? heldStack = activeSlot.Itemstack;
        if (heldStack == null)
        {
            return false;
        }

        if (IsSulfuricAcid(heldStack))
        {
            int amount = Math.Min(maxItems, heldStack.StackSize);
            acidStack = heldStack.Clone();
            acidStack.StackSize = amount;
            activeSlot.TakeOut(amount);
            return true;
        }

        if (heldStack.Collectible is not ILiquidSource source || heldStack.Collectible is not ILiquidInterface liquidInterface)
        {
            return false;
        }

        ItemStack? contentStack = liquidInterface.GetContent(heldStack);
        if (!IsSulfuricAcid(contentStack))
        {
            failure = "Only sulfuric acid can be poured into this cauldron right now.";
            return false;
        }

        int amountToTake = Math.Min(maxItems, contentStack!.StackSize);
        if (amountToTake <= 0)
        {
            return false;
        }

        ItemStack? takenStack = source.TryTakeContent(heldStack, amountToTake);
        if (!IsSulfuricAcid(takenStack))
        {
            failure = "Could not pour sulfuric acid from that container.";
            return false;
        }

        acidStack = takenStack;
        return true;
    }

    private bool CanTakeSulfuricAcid(ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (IsSulfuricAcid(heldStack))
        {
            return true;
        }

        return heldStack?.Collectible is ILiquidInterface liquidInterface && IsSulfuricAcid(liquidInterface.GetContent(heldStack));
    }

    private bool CanReceiveCauldronLiquid(ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (heldStack?.Collectible is not ILiquidSink)
        {
            return false;
        }

        if (heldStack.Collectible is not ILiquidInterface liquidInterface)
        {
            return true;
        }

        ItemStack? contentStack = liquidInterface.GetContent(heldStack);
        return contentStack == null || liquidStack != null && contentStack.Equals(Api.World, liquidStack, GlobalConstants.IgnoredStackAttributes);
    }

    private int InsertLiquid(ItemStack acidStack)
    {
        int amount = Math.Min(LiquidCapacityItems - (liquidStack?.StackSize ?? 0), acidStack.StackSize);
        if (liquidStack == null)
        {
            liquidStack = acidStack.Clone();
            liquidStack.StackSize = amount;
            return amount;
        }

        liquidStack.StackSize += amount;
        return amount;
    }

    private static bool IsSulfuricAcid(ItemStack? stack)
    {
        bool result = stack?.Collectible?.Code == SulfuricAcidCode;
        if (DebugLiquid)
        {
            Console.WriteLine(
                "[FACore CoverStation Liquid] IsSulfuricAcid stack={0}, code={1}, expected={2}, result={3}",
                FormatStackDebug(stack),
                stack?.Collectible?.Code,
                SulfuricAcidCode,
                result
            );
        }

        return result;
    }

    private bool TryStartPlateRest(IPlayer byPlayer, ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (!TryGetMetalPlateMetal(heldStack, out string metal))
        {
            return false;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "Take out the immersed item before adding a plate.");
            return true;
        }

        if ((liquidStack?.StackSize ?? 0) < LiquidCapacityItems)
        {
            Notify(byPlayer, $"Fill the cauldron with {LiquidCapacityLitres}L of sulfuric acid before inserting a metal plate.");
            return true;
        }

        if (!IsSulfuricAcid(liquidStack))
        {
            Notify(byPlayer, $"The cauldron already contains {GetLiquidName(liquidStack)}.");
            return true;
        }

        Item? coatingItem = Api.World.GetItem(CoatingLiquidCode(metal));
        if (coatingItem == null)
        {
            Notify(byPlayer, $"That metal plate cannot be turned into coating liquid yet: {metal}.");
            return true;
        }

        ItemStack? inserted = activeSlot.TakeOut(1);
        if (inserted == null)
        {
            return true;
        }

        immersedStack = inserted;
        activeSlot.MarkDirty();
        PlayItemSplashSound(byPlayer);
        PlayBubblingSound(byPlayer);
        StartProcess(ProcessPlateResting, metal);
        MarkStationDirty();
        Notify(byPlayer, $"Added {immersedStack.GetName()}. Let it react for {PlateRestHours:0.#} in-game hours.");
        return true;
    }

    private bool TryInsertArmorForCoating(IPlayer byPlayer, ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (!TryGetFAArmorPiece(heldStack, out string piece))
        {
            return false;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "The cauldron already holds an item.");
            return true;
        }

        if (!TryGetCoatingMetal(liquidStack, out string metal))
        {
            Notify(byPlayer, "Prepare coating liquid before inserting armor.");
            return true;
        }

        if (!CanApplyArmorCoating(heldStack, piece, metal, out string failure))
        {
            Notify(byPlayer, failure);
            return true;
        }

        ItemStack? inserted = activeSlot.TakeOut(1);
        if (inserted == null)
        {
            return true;
        }

        immersedStack = inserted;
        activeSlot.MarkDirty();
        PlayItemSplashSound(byPlayer);
        MarkStationDirty();
        Notify(byPlayer, $"Immersed {immersedStack.GetName()}. Close the lid and light a full charcoal stack.");
        return true;
    }

    private static bool TryGetMetalPlateMetal(ItemStack? stack, out string metal)
    {
        metal = "";
        string? path = stack?.Collectible?.Code?.Path;
        if (path == null || !path.StartsWith("metalplate-", StringComparison.Ordinal))
        {
            return false;
        }

        metal = path["metalplate-".Length..];
        return metal.Length > 0;
    }

    private static bool TryGetFAArmorPiece(ItemStack? stack, out string piece)
    {
        piece = "";
        AssetLocation? code = stack?.Collectible?.Code;
        if (code == null || !code.Domain.StartsWith("fa", StringComparison.Ordinal))
        {
            return false;
        }

        ITreeAttribute? types = stack!.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(types.GetString("basehead")))
        {
            piece = "head";
            return true;
        }

        if (!string.IsNullOrEmpty(types.GetString("basebody")))
        {
            piece = "body";
            return true;
        }

        if (!string.IsNullOrEmpty(types.GetString("baselegs")))
        {
            piece = "legs";
            return true;
        }

        return false;
    }

    private static bool CanApplyArmorCoating(ItemStack? armorStack, string piece, string metal, out string failure)
    {
        failure = "";

        if (!SupportedArmorCoatingMetals.Contains(metal))
        {
            failure = $"This armor set has no ARL coating texture for {metal}.";
            return false;
        }

        ITreeAttribute? types = armorStack?.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            failure = "That armor piece has no ARL type data.";
            return false;
        }

        string coverKey = "cover" + piece;
        string currentCover = types.GetString(coverKey) ?? "none";
        if (!string.Equals(currentCover, "none", StringComparison.Ordinal))
        {
            failure = $"That armor already has {currentCover} coating.";
            return false;
        }

        return true;
    }

    private static void ApplyArmorCoating(ItemStack armorStack, string piece, string metal)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return;
        }

        types.SetString("cover" + piece, metal);
    }

    private static AssetLocation CoatingLiquidCode(string metal)
    {
        return new AssetLocation(CoatingLiquidCodePrefix.Domain, CoatingLiquidCodePrefix.Path + metal);
    }

    private static bool IsCoatingLiquid(ItemStack? stack)
    {
        return TryGetCoatingMetal(stack, out _);
    }

    private static bool TryGetCoatingMetal(ItemStack? stack, out string metal)
    {
        metal = "";
        AssetLocation? code = stack?.Collectible?.Code;
        if (code?.Domain != CoatingLiquidCodePrefix.Domain
            || !code.Path.StartsWith(CoatingLiquidCodePrefix.Path, StringComparison.Ordinal))
        {
            return false;
        }

        metal = code.Path[CoatingLiquidCodePrefix.Path.Length..];
        return metal.Length > 0;
    }

    private static bool IsCauldronLiquid(ItemStack? stack)
    {
        return IsSulfuricAcid(stack) || IsCoatingLiquid(stack);
    }

    private void OnProcessTick(float dt)
    {
        UpdateProcess();
    }

    private void UpdateProcess()
    {
        if (!HasActiveProcess())
        {
            return;
        }

        if (Api?.Side != EnumAppSide.Server)
        {
            return;
        }

        double duration = GetProcessDurationHours();
        if (duration <= 0 || Api.World.Calendar.TotalHours - processStartHours < duration)
        {
            return;
        }

        if (processMode == ProcessPlateResting)
        {
            CompletePlateRest();
            return;
        }

        if (processMode == ProcessArmorCoating)
        {
            CompleteArmorCoating();
        }
    }

    private void StartProcess(string mode, string metal)
    {
        processMode = mode;
        processMetal = metal;
        processStartHours = Api.World.Calendar.TotalHours;
    }

    private void ClearProcess()
    {
        processMode = "";
        processMetal = "";
        processStartHours = -1;
    }

    private bool HasActiveProcess()
    {
        return processMode.Length > 0 && processStartHours >= 0;
    }

    private double GetProcessDurationHours()
    {
        return processMode switch
        {
            ProcessPlateResting => PlateRestHours,
            ProcessArmorCoating => ArmorCoatingHours,
            _ => 0
        };
    }

    private string GetProcessStatusText()
    {
        if (!HasActiveProcess())
        {
            return "Idle";
        }

        double remaining = Math.Max(0, GetProcessDurationHours() - (Api.World.Calendar.TotalHours - processStartHours));
        string label = processMode == ProcessPlateResting ? "Plate reacting" : "Armor coating";
        return $"{label}: {remaining:0.#} in-game hours remaining";
    }

    private string FormatRemainingProcessTime()
    {
        if (!HasActiveProcess())
        {
            return "";
        }

        double remainingHours = Math.Max(0, GetProcessDurationHours() - (Api.World.Calendar.TotalHours - processStartHours));
        int wholeHours = (int)Math.Floor(remainingHours);
        int minutes = (int)Math.Ceiling((remainingHours - wholeHours) * 60);
        if (minutes >= 60)
        {
            wholeHours++;
            minutes = 0;
        }

        if (wholeHours <= 0)
        {
            return $"{minutes}m";
        }

        return minutes <= 0 ? $"{wholeHours}h" : $"{wholeHours}h {minutes}m";
    }

    private void CompletePlateRest()
    {
        if (immersedStack == null
            || !TryGetMetalPlateMetal(immersedStack, out string metal)
            || metal != processMetal
            || liquidStack == null
            || !IsSulfuricAcid(liquidStack))
        {
            ClearProcess();
            MarkStationDirty();
            return;
        }

        Item? coatingItem = Api.World.GetItem(CoatingLiquidCode(metal));
        if (coatingItem == null)
        {
            ClearProcess();
            MarkStationDirty();
            return;
        }

        int liquidAmount = liquidStack.StackSize;
        immersedStack = null;
        liquidStack = new ItemStack(coatingItem, liquidAmount);
        ClearProcess();
        MarkStationDirty();
    }

    private void CompleteArmorCoating()
    {
        if (immersedStack == null
            || !TryGetCoatingMetal(liquidStack, out string metal)
            || metal != processMetal
            || !TryGetFAArmorPiece(immersedStack, out string piece)
            || !CanApplyArmorCoating(immersedStack, piece, metal, out _))
        {
            fuelLit = false;
            ClearProcess();
            MarkStationDirty();
            return;
        }

        ApplyArmorCoating(immersedStack, piece, metal);
        liquidStack = null;
        fuelStack = null;
        fuelLit = false;
        ClearProcess();
        MarkStationDirty();
    }

    private static string GetLiquidName(ItemStack? stack)
    {
        if (stack == null)
        {
            return "liquid";
        }

        return stack.GetName();
    }

    private bool CanLightFuel(ItemStack? stack)
    {
        return stack?.Collectible.HasBehavior("CanIgnite", Api.ClassRegistry) == true;
    }

    private static bool IsCharcoalFuel(ItemStack? stack)
    {
        return stack?.Collectible?.Code?.Equals(CharcoalCode) == true;
    }

    private static bool IsStationFuel(ItemStack? stack)
    {
        AssetLocation? code = stack?.Collectible?.Code;
        if (code == null || code.Domain != "game")
        {
            return false;
        }

        return code.Path == "charcoal"
            || code.Path == "coal"
            || code.Path.StartsWith("coal-", StringComparison.Ordinal)
            || code.Path == "ore-lignite"
            || code.Path == "ore-bituminouscoal"
            || code.Path == "ore-anthracite";
    }

    private static string FormatLitres(int itemCount)
    {
        return (itemCount / (float)ItemsPerLitre).ToString("0.##");
    }

    private MeshData? CreateLiquidMesh(ITesselatorAPI tessThreadTesselator)
    {
        if (Api is not ICoreClientAPI capi || liquidStack == null)
        {
            DebugLiquidLog($"CreateLiquidMesh skipped. ApiIsClient={Api is ICoreClientAPI}, liquidStack={FormatStackDebug(liquidStack)}");
            return null;
        }

        TextureAtlasPosition? liquidTexturePosition = GetLiquidTexturePosition(liquidStack, out int textureSubId);
        DebugLiquidLog($"CreateLiquidMesh texturePosition={(liquidTexturePosition == null ? "null" : $"x1={liquidTexturePosition.x1}, y1={liquidTexturePosition.y1}, x2={liquidTexturePosition.x2}, y2={liquidTexturePosition.y2}")}");
        if (liquidTexturePosition == null)
        {
            return null;
        }

        MeshData? liquidMesh = CreateLiquidSurfaceMesh(tessThreadTesselator, capi, liquidTexturePosition, textureSubId);
        if (liquidMesh == null)
        {
            DebugLiquidLog("CreateLiquidMesh tesselated shape returned null.");
            return null;
        }

        DebugLiquidLog($"CreateLiquidMesh tesselated. vertices={liquidMesh.VerticesCount}, indices={liquidMesh.IndicesCount}, renderPasses={liquidMesh.RenderPassCount}");
        return liquidMesh;
    }

    private MeshData? CreateFuelMesh(ITesselatorAPI tessThreadTesselator)
    {
        if (Api is not ICoreClientAPI capi || !IsStationFuel(fuelStack))
        {
            DebugFuelLog($"CreateFuelMesh skipped. ApiIsClient={Api is ICoreClientAPI}, fuelStack={FormatStackDebug(fuelStack)}");
            return null;
        }

        CacheCharcoalTexture();
        if (cachedCharcoalTexturePosition == null)
        {
            DebugFuelLog("CreateFuelMesh missing cached charcoal texture.");
            return null;
        }

        Shape? shape = null;
        AssetLocation? resolvedShape = null;
        AssetLocation[] candidates = GetForgeCoalShapeCandidates();
        foreach (AssetLocation candidate in candidates)
        {
            IAsset? asset = Api.Assets.TryGet(candidate, true);
            if (asset == null)
            {
                continue;
            }

            shape = TryReadShapeAsset(asset);
            if (shape != null)
            {
                resolvedShape = candidate;
                DebugFuelLog($"CreateFuelMesh loaded asset {candidate}");
                break;
            }
        }

        if (shape == null)
        {
            DebugFuelLog($"CreateFuelMesh shape not found. candidates=[{string.Join(", ", candidates)}]");
            return null;
        }

        ITexPositionSource textureSource;
        if (TryApplyForgeFuelTexture(shape, fuelLit, out string fuelTextureDebug))
        {
            textureSource = new ShapeTextureSource(capi, shape, resolvedShape);
        }
        else
        {
            textureSource = new MappedTextureSource(
                capi.Tesselator.GetTextureSource(Block, 0, false),
                "coal",
                cachedCharcoalTexturePosition
            );
            fuelTextureDebug = "fallback-charcoal";
        }

        tessThreadTesselator.TesselateShape(
            "facore-coverstation-fuel",
            shape,
            out MeshData mesh,
            textureSource,
            new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ)
        );

        ForceOpaqueRenderPass(mesh);
        mesh.Scale(FuelMeshRotationOrigin, FuelMeshScale, FuelMeshScale, FuelMeshScale);
        mesh.Rotate(FuelMeshRotationOrigin, 0f, GameMath.PI, 0f);
        AlignFuelMesh(mesh);
        DebugFuelLog($"CreateFuelMesh tesselated. vertices={mesh.VerticesCount}, indices={mesh.IndicesCount}, needsOpaque={mesh.NeedsRenderPass(EnumChunkRenderPass.Opaque)}, lit={fuelLit}, fuelTexture={fuelTextureDebug}, shape={resolvedShape}, bounds={FormatMeshBounds(mesh)}");
        return mesh;
    }

    private bool TryApplyForgeFuelTexture(Shape shape, bool lit, out string textureDebug)
    {
        textureDebug = "missing-forge-texture";
        string textureCode = lit ? "fuel-ember" : "fuel-coal";
        Block? forgeBlock = Api.World.GetBlock(new AssetLocation("game", "forge"))
            ?? Api.World.GetBlock(new AssetLocation("survival", "forge"))
            ?? Api.World.GetBlock(new AssetLocation("forge"));

        if (forgeBlock?.Textures == null || !forgeBlock.Textures.TryGetValue(textureCode, out CompositeTexture? fuelTexture) || fuelTexture?.Base == null)
        {
            DebugFuelLog($"TryApplyForgeFuelTexture missing textureCode={textureCode}, forgeBlock={forgeBlock?.Code}");
            return false;
        }

        shape.Textures ??= new Dictionary<string, AssetLocation>(StringComparer.Ordinal);
        shape.Textures["coal"] = fuelTexture.Base;
        textureDebug = $"{forgeBlock.Code}:{textureCode}->{fuelTexture.Base}";
        return true;
    }

    private MeshData? CreateImmersedItemMesh(ITesselatorAPI tessThreadTesselator, bool hasVisibleLiquid)
    {
        if (Api is not ICoreClientAPI capi || immersedStack?.Item == null)
        {
            return null;
        }

        bool isPlate = TryGetMetalPlateMetal(immersedStack, out _);
        bool isArmor = TryGetFAArmorPiece(immersedStack, out _);
        if (!isPlate && !isArmor)
        {
            DebugLiquidLog($"CreateImmersedItemMesh skipped unsupported stack={FormatStackDebug(immersedStack)}");
            return null;
        }

        Item item = immersedStack.Item;
        ITexPositionSource fallbackTextureSource = capi.Tesselator.GetTextureSource(Block, 0, false);
        if (!TryResolveImmersedShapeAndTextures(
                capi,
                item,
                immersedStack,
                fallbackTextureSource,
                out Shape? shape,
                out ITexPositionSource textureSource,
                out AssetLocation shapeLocation
            ))
        {
            DebugLiquidLog($"CreateImmersedItemMesh skipped missing preview shape stack={FormatStackDebug(immersedStack)}");
            return null;
        }

        tessThreadTesselator.TesselateShape(
            "facore-coverstation-immersed",
            shape,
            out MeshData mesh,
            textureSource,
            new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ)
        );

        if (mesh == null || mesh.VerticesCount <= 0)
        {
            DebugLiquidLog($"CreateImmersedItemMesh empty stack={FormatStackDebug(immersedStack)}");
            return null;
        }

        ForceOpaqueRenderPass(mesh);
        AlignImmersedItemMesh(mesh, isPlate, hasVisibleLiquid);
        DebugLiquidLog($"CreateImmersedItemMesh stack={FormatStackDebug(immersedStack)}, isPlate={isPlate}, isArmor={isArmor}, vertices={mesh.VerticesCount}, bounds={FormatMeshBounds(mesh)}");
        return mesh;
    }

    private bool TryResolveImmersedShapeAndTextures(
        ICoreClientAPI capi,
        Item item,
        ItemStack stack,
        ITexPositionSource fallbackTextureSource,
        out Shape? shape,
        out ITexPositionSource textureSource,
        out AssetLocation shapeLocation
    )
    {
        shape = null;
        textureSource = fallbackTextureSource;
        shapeLocation = null!;

        if (item.Shape?.Base != null)
        {
            shapeLocation = item.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            shape = Shape.TryGet(Api, shapeLocation);
            if (shape != null)
            {
                textureSource = new ItemBlockAtlasTextureSource(capi, fallbackTextureSource, item);
                return true;
            }

            DebugLiquidLog($"TryResolveImmersedShapeAndTextures item shape not found stack={FormatStackDebug(stack)}, shape={shapeLocation}");
        }

        if (!TryResolveGreenwichArmorPreview(capi, stack, fallbackTextureSource, out shape, out textureSource, out shapeLocation))
        {
            return false;
        }

        return true;
    }

    private bool TryResolveGreenwichArmorPreview(
        ICoreClientAPI capi,
        ItemStack stack,
        ITexPositionSource fallbackTextureSource,
        out Shape? shape,
        out ITexPositionSource textureSource,
        out AssetLocation shapeLocation
    )
    {
        shape = null;
        textureSource = fallbackTextureSource;
        shapeLocation = null!;

        if (stack.Collectible?.Code?.Domain != "fagreenwich" || !TryGetFAArmorPiece(stack, out string piece))
        {
            return false;
        }

        ITreeAttribute? types = stack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return false;
        }

        string form = types.GetString("form" + piece, GetDefaultGreenwichForm(piece));
        string decoration = types.GetString("decoration" + piece, "none");
        string baseMetal = types.GetString("base" + piece, "iron");
        string coverMetal = types.GetString("cover" + piece, "none");
        string stripMetal = types.GetString("strip" + piece, "none");
        string color = types.GetString("color" + piece, "none");

        shapeLocation = new AssetLocation("fagreenwich", $"shapes/entity/armor/greenwich/{form}/{decoration}{piece}.json");
        shape = Shape.TryGet(Api, shapeLocation);
        if (shape == null)
        {
            DebugLiquidLog($"TryResolveGreenwichArmorPreview shape not found stack={FormatStackDebug(stack)}, shape={shapeLocation}");
            return false;
        }

        var textures = new Dictionary<string, CompositeTexture>(StringComparer.Ordinal);
        string baseTextureMetal = coverMetal == "none" ? baseMetal : coverMetal;
        string baseTextureKind = coverMetal == "none" ? "base" : "cover";
        textures["base" + piece] = new CompositeTexture(new AssetLocation("fagreenwich", $"armor/entity/greenwich/{baseTextureKind}/{baseTextureMetal}")) { Alpha = 255 };
        textures["strip" + piece] = new CompositeTexture(new AssetLocation("facore", $"armor/entity/trim/{stripMetal}")) { Alpha = 255 };
        textures["color" + piece] = GetGreenwichDecorationTexture(piece, decoration, color);
        textures["seraph"] = new CompositeTexture(new AssetLocation("game", "block/transparent")) { Alpha = 0 };

        textureSource = new CompositeBlockAtlasTextureSource(capi, fallbackTextureSource, textures);
        DebugLiquidLog($"TryResolveGreenwichArmorPreview stack={FormatStackDebug(stack)}, piece={piece}, shape={shapeLocation}, base={baseTextureKind}/{baseTextureMetal}, strip={stripMetal}, decoration={decoration}, color={color}");
        return true;
    }

    private static string GetDefaultGreenwichForm(string piece)
    {
        return piece switch
        {
            "head" => "armet",
            "body" => "guardbody",
            "legs" => "guardlegs",
            _ => ""
        };
    }

    private static CompositeTexture GetGreenwichDecorationTexture(string piece, string decoration, string color)
    {
        if (decoration == "bear")
        {
            return new CompositeTexture(new AssetLocation("facore", $"armor/entity/bear/{color}")) { Alpha = 255 };
        }

        return new CompositeTexture(new AssetLocation("facore", $"block/decorations/{piece}/{color}")) { Alpha = 255 };
    }

    private void AlignImmersedItemMesh(MeshData mesh, bool isPlate, bool hasVisibleLiquid)
    {
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ))
        {
            return;
        }

        var meshOrigin = new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
        float maxDimension = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
        if (maxDimension > 0)
        {
            float targetDimension = isPlate ? ImmersedPlateMaxDimension : ImmersedArmorMaxDimension;
            float scale = targetDimension / maxDimension;
            mesh.Scale(meshOrigin, scale, scale, scale);
        }

        mesh.Rotate(meshOrigin, isPlate ? GameMath.PIHALF : GameMath.PI * 0.58f, 0f, isPlate ? GameMath.PI * 0.08f : GameMath.PI * 0.18f);

        if (!TryGetMeshBounds(mesh, out minX, out _, out minZ, out maxX, out maxY, out maxZ))
        {
            return;
        }

        (float targetCenterX, float targetCenterZ) = GetLiquidTargetCenter();
        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;
        float targetTopY = hasVisibleLiquid
            ? isPlate ? ImmersedPlateTopY : ImmersedArmorTopY
            : LiquidMinY - 0.08f;

        mesh.Translate(
            targetCenterX - currentCenterX,
            targetTopY - maxY,
            targetCenterZ - currentCenterZ
        );
    }

    private void AlignFuelMesh(MeshData mesh)
    {
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out _, out float maxZ))
        {
            return;
        }

        (float targetCenterX, float targetCenterZ) = GetFuelTargetCenter();
        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;

        mesh.Translate(
            targetCenterX - currentCenterX,
            FuelMinY - minY,
            targetCenterZ - currentCenterZ
        );
    }

    private (float X, float Z) GetFuelTargetCenter()
    {
        float northCenterX = (FuelMinX + FuelMaxX) * 0.5f;
        float northCenterZ = (FuelMinZ + FuelMaxZ) * 0.5f;
        float offsetX = northCenterX - 0.5f;
        float offsetZ = northCenterZ - 0.5f;

        return GetStationSideCode() switch
        {
            "east" => (0.5f - offsetZ, 0.5f + offsetX),
            "south" => (0.5f - offsetX, 0.5f - offsetZ),
            "west" => (0.5f + offsetZ, 0.5f - offsetX),
            _ => (northCenterX, northCenterZ)
        };
    }

    private AssetLocation[] GetForgeCoalShapeCandidates()
    {
        var candidates = new List<AssetLocation>();

        foreach (AssetLocation forgeCode in new[] { new AssetLocation("game", "forge"), new AssetLocation("survival", "forge"), new AssetLocation("forge") })
        {
            Block? forgeBlock = Api.World.GetBlock(forgeCode);
            string? shapePath = forgeBlock?.Attributes?["coalshapeloc"]?.AsString(null);
            if (string.IsNullOrWhiteSpace(shapePath))
            {
                continue;
            }

            string normalizedPath = shapePath.Replace('\\', '/');
            candidates.Add(new AssetLocation(forgeBlock!.Code.Domain, normalizedPath));
            if (!normalizedPath.EndsWith(".json", StringComparison.Ordinal))
            {
                candidates.Add(new AssetLocation(forgeBlock.Code.Domain, normalizedPath + ".json"));
            }
        }

        foreach (AssetLocation fallback in FallbackForgeCoalShapes)
        {
            if (!candidates.Contains(fallback))
            {
                candidates.Add(fallback);
            }
        }

        DebugFuelLog($"GetForgeCoalShapeCandidates -> [{string.Join(", ", candidates)}]");
        return candidates.ToArray();
    }

    private MeshData? CreateLiquidSurfaceMesh(ITesselatorAPI tessThreadTesselator, ICoreClientAPI capi, TextureAtlasPosition texturePosition, int textureSubId)
    {
        AssetLocation shapeLocation = ResolveShapeLocation();
        Shape? shape = Shape.TryGet(Api, shapeLocation);
        if (shape == null)
        {
            DebugLiquidLog($"CreateLiquidSurfaceMesh shape not found: {shapeLocation}");
            return null;
        }

        Shape? liquidShape = BuildLiquidOnlyShape(shape);
        if (liquidShape == null)
        {
            DebugLiquidLog($"CreateLiquidSurfaceMesh LiquidSurface not found: {shapeLocation}");
            return null;
        }

        ITexPositionSource textureSource = new MappedTextureSource(
            capi.Tesselator.GetTextureSource(Block, 0, false),
            "liquid",
            texturePosition
        );
        tessThreadTesselator.TesselateShape(
            "facore-coverstation-liquid",
            liquidShape,
            out MeshData mesh,
            textureSource,
            new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ)
        );

        float fill = GameMath.Clamp((liquidStack?.StackSize ?? 0) / (float)LiquidCapacityItems, 0f, 1f);
        float y = LiquidMinY + (LiquidMaxY - LiquidMinY) * fill;
        AlignLiquidMesh(mesh, y);
        ApplyLiquidMeshStyle(mesh);
        DebugLiquidLog($"CreateLiquidSurfaceMesh fill={fill:0.###}, y={y:0.###}, atlasTextureId={texturePosition.atlasTextureId}, subId={textureSubId}, vertices={mesh.VerticesCount}, indices={mesh.IndicesCount}, renderPasses={mesh.RenderPassCount}, textureIds={mesh.TextureIds?.Length ?? 0}, needsOpaque={mesh.NeedsRenderPass(EnumChunkRenderPass.Opaque)}, needsTransparent={mesh.NeedsRenderPass(EnumChunkRenderPass.Transparent)}, bounds={FormatMeshBounds(mesh)}");
        return mesh;
    }

    private void AlignLiquidMesh(MeshData mesh, float targetY)
    {
        if (!TryGetMeshBounds(
                    mesh,
                    out float minX,
                    out float minY,
                    out float minZ,
                    out float maxX,
                    out _,
                    out float maxZ
                ))
        {
            return;
        }

        (float targetCenterX, float targetCenterZ) = GetLiquidTargetCenter();
        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;

        mesh.Translate(
            targetCenterX - currentCenterX,
            targetY - minY,
            targetCenterZ - currentCenterZ
        );
    }

    private (float X, float Z) GetLiquidTargetCenter()
    {
        float northCenterX = (LiquidMinX + LiquidMaxX) * 0.5f;
        float northCenterZ = (LiquidMinZ + LiquidMaxZ) * 0.5f;
        float offsetX = northCenterX - 0.5f;
        float offsetZ = northCenterZ - 0.5f;

        return GetStationSideCode() switch
        {
            "east" => (0.5f - offsetZ, 0.5f + offsetX),
            "south" => (0.5f - offsetX, 0.5f - offsetZ),
            "west" => (0.5f + offsetZ, 0.5f - offsetX),
            _ => (northCenterX, northCenterZ)
        };
    }

private static void ApplyLiquidMeshStyle(MeshData mesh)
{
    mesh.WithRenderpasses();

    if (mesh.RenderPassesAndExtraBits != null)
    {
        Array.Fill(mesh.RenderPassesAndExtraBits, (short)EnumChunkRenderPass.Transparent);
    }
}

    private static Shape? BuildLiquidOnlyShape(Shape sourceShape)
    {
        Shape shape = sourceShape.Clone();
        ShapeElement? liquidElement = shape.GetElementByName("LiquidSurface", StringComparison.OrdinalIgnoreCase);
        if (liquidElement == null)
        {
            return null;
        }

        liquidElement.ParentElement = null;
        liquidElement.StepParentName = null;
        shape.Elements = [liquidElement];
        shape.Animations = null;
        return shape;
    }

    private static void ForceOpaqueRenderPass(MeshData mesh)
    {
        if (mesh.RenderPassesAndExtraBits == null || mesh.RenderPassesAndExtraBits.Length == 0)
        {
            mesh.WithRenderpasses();
        }

        if (mesh.RenderPassesAndExtraBits == null)
        {
            return;
        }

        Array.Fill(mesh.RenderPassesAndExtraBits, (short)EnumChunkRenderPass.Opaque);
    }

    private static Shape? TryReadShapeAsset(IAsset asset)
    {
        MethodInfo? toObjectMethod = asset.GetType().GetMethod("ToObject");
        if (toObjectMethod == null)
        {
            return null;
        }

        MethodInfo genericMethod = toObjectMethod.MakeGenericMethod(typeof(Shape));
        object?[] parameters = genericMethod.GetParameters().Length == 0 ? [] : [null];
        return genericMethod.Invoke(asset, parameters) as Shape;
    }

    private static string FormatMeshBounds(MeshData mesh)
    {
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ))
        {
            return "empty";
        }

        return $"[{minX:0.###},{minY:0.###},{minZ:0.###}]..[{maxX:0.###},{maxY:0.###},{maxZ:0.###}]";
    }

    private static bool TryGetMeshBounds(MeshData mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ)
    {
        minX = minY = minZ = 0f;
        maxX = maxY = maxZ = 0f;

        if (mesh.xyz == null || mesh.VerticesCount <= 0)
        {
            return false;
        }

        minX = minY = minZ = float.MaxValue;
        maxX = maxY = maxZ = float.MinValue;

        for (int i = 0; i < mesh.VerticesCount * 3; i += 3)
        {
            float x = mesh.xyz[i];
            float y = mesh.xyz[i + 1];
            float z = mesh.xyz[i + 2];

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);
        }

        return true;
    }

    private TextureAtlasPosition? GetLiquidTexturePosition(ItemStack liquid, out int textureSubId)
    {
        if (TryCacheLiquidTexture(liquid, out TextureAtlasPosition? texturePosition, out textureSubId))
        {
                    Console.WriteLine("ATLAS Tried finding a liquid texture: " + texturePosition);
            return texturePosition;
        }

        textureSubId = 0;
        return null;
    }

    private void CacheSulfurTexture()
    {
        if (cachedSulfurTexturePosition != null)
        {
            DebugLiquidLog($"CacheSulfurTexture skipped. cached={cachedSulfurTexturePosition != null}");
            return;
        }

        Item? sulfurItem = Api.World.GetItem(SulfuricAcidCode);
        if (sulfurItem == null)
        {
            DebugLiquidLog($"CacheSulfurTexture failed: item not found for {SulfuricAcidCode}");
            return;
        }

        DebugLiquidLog($"CacheSulfurTexture found item: {sulfurItem.Code}");
        if (!TryCacheLiquidTexture(new ItemStack(sulfurItem), out TextureAtlasPosition? texturePosition, out int textureSubId) || texturePosition == null)
        {
            DebugLiquidLog("CacheSulfurTexture failed: sulfur texture was null.");
            return;
        }

        cachedSulfurTextureSubId = textureSubId;
        cachedSulfurTexturePosition = texturePosition;
        DebugLiquidLog($"CacheSulfurTexture cached subId={cachedSulfurTextureSubId}, tex=({texturePosition.x1},{texturePosition.y1})-({texturePosition.x2},{texturePosition.y2})");
    }

    private bool TryCacheLiquidTexture(ItemStack liquid, out TextureAtlasPosition? texturePosition, out int textureSubId)
    {
        texturePosition = null;
        textureSubId = 0;
        if (Api is not ICoreClientAPI capi || !IsCauldronLiquid(liquid))
        {
            DebugLiquidLog($"TryCacheLiquidTexture skipped. apiIsClient={Api is ICoreClientAPI}, liquid={FormatStackDebug(liquid)}");
            return false;
        }

        string cacheKey = liquid.Collectible.Code.ToString();
        if (cachedLiquidTexturePositions.TryGetValue(cacheKey, out TextureAtlasPosition? cachedTexturePosition)
            && cachedLiquidTextureSubIds.TryGetValue(cacheKey, out int cachedTextureSubId))
        {
            texturePosition = cachedTexturePosition;
            textureSubId = cachedTextureSubId;
            return true;
        }

        CompositeTexture? texture = GetLiquidTexture(liquid);
        if (texture == null)
        {
            DebugLiquidLog($"TryCacheLiquidTexture failed: no texture for {FormatStackDebug(liquid)}");
            return false;
        }

        texture.Bake(capi.Assets);
        capi.BlockTextureAtlas.GetOrInsertTexture(texture, out textureSubId, out TextureAtlasPosition insertedTexturePosition, 0.005f);
        cachedLiquidTexturePositions[cacheKey] = insertedTexturePosition;
        cachedLiquidTextureSubIds[cacheKey] = textureSubId;
        texturePosition = insertedTexturePosition;
        DebugLiquidLog($"TryCacheLiquidTexture cached {cacheKey} subId={textureSubId}, tex=({insertedTexturePosition.x1},{insertedTexturePosition.y1})-({insertedTexturePosition.x2},{insertedTexturePosition.y2})");
        return true;
    }

    private void CacheCharcoalTexture()
    {
        if (cachedCharcoalTexturePosition != null || Api is not ICoreClientAPI capi)
        {
            DebugFuelLog($"CacheCharcoalTexture skipped. cached={cachedCharcoalTexturePosition != null}, apiIsClient={Api is ICoreClientAPI}");
            return;
        }

        Item? charcoalItem = Api.World.GetItem(CharcoalCode);
        if (charcoalItem == null)
        {
            DebugFuelLog($"CacheCharcoalTexture failed: item not found for {CharcoalCode}");
            return;
        }

        CompositeTexture? texture = GetItemTexture(new ItemStack(charcoalItem));
        if (texture == null)
        {
            DebugFuelLog("CacheCharcoalTexture failed: charcoal texture was null.");
            return;
        }

        texture.Alpha = 255;
        texture.Bake(capi.Assets);
        capi.BlockTextureAtlas.GetOrInsertTexture(texture, out cachedCharcoalTextureSubId, out TextureAtlasPosition texturePosition, 0.005f);
        cachedCharcoalTexturePosition = texturePosition;
        DebugFuelLog($"CacheCharcoalTexture cached subId={cachedCharcoalTextureSubId}, tex=({texturePosition.x1},{texturePosition.y1})-({texturePosition.x2},{texturePosition.y2})");
    }

    private static CompositeTexture? GetLiquidTexture(ItemStack liquid)
{
    if (TryGetCoatingMetal(liquid, out string metal))
    {
        string dyeColor = CoatingDyeColors.TryGetValue(metal, out string? mappedColor)
            ? mappedColor
            : "gray";

        return new CompositeTexture(new AssetLocation("game", "block/liquid/dye/" + dyeColor))
        {
            Alpha = 255
        };
    }

    WaterTightContainableProps? props = BlockLiquidContainerBase.GetContainableProps(liquid);
    if (props?.Texture != null)
    {
        CompositeTexture texture = props.Texture.Clone();
        texture.Alpha = 255;
        return texture;
    }

    return IsSulfuricAcid(liquid)
        ? new CompositeTexture(new AssetLocation("game", "block/liquid/dye/yellow")) { Alpha = 255 }
        : null;
}

    private static CompositeTexture? GetItemTexture(ItemStack stack)
    {
        if (stack.Collectible is not Item item || item.Textures == null)
        {
            return null;
        }

        if (item.Textures.TryGetValue("all", out CompositeTexture? allTexture))
        {
            return allTexture.Clone();
        }

        foreach (CompositeTexture texture in item.Textures.Values)
        {
            return texture.Clone();
        }

        return null;
    }

    private static void SetOrRemoveItemstack(ITreeAttribute tree, string key, ItemStack? stack)
    {
        if (stack == null)
        {
            tree.RemoveAttribute(key);
            return;
        }

        tree.SetItemstack(key, stack);
    }

    private static ItemStack? ResolveItemstack(ItemStack? stack, IWorldAccessor world)
    {
        stack?.ResolveBlockOrItem(world);
        return stack;
    }

    private void PlayBubblingSound(IPlayer byPlayer)
    {
        AssetLocation sound = BubblingSounds[Api.World.Rand.Next(BubblingSounds.Length)];
        PlayStationSound(sound, byPlayer);
    }

    private void SyncProcessLoopSound()
    {
        if (Api is not ICoreClientAPI capi)
        {
            return;
        }

        if (!HasActiveProcess())
        {
            StopProcessLoopSound();
            return;
        }

        if (processLoopSound != null && !processLoopSound.HasStopped)
        {
            processLoopSound.SetPosition(Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f));
            return;
        }

        AssetLocation sound = BubblingSounds[Api.World.Rand.Next(BubblingSounds.Length)];
        processLoopSound = capi.World.LoadSound(new SoundParams
        {
            Location = sound,
            Position = Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f),
            ShouldLoop = true,
            DisposeOnFinish = false,
            Volume = 0.55f,
            Range = 14f,
            SoundType = EnumSoundType.Sound
        });

        processLoopSound?.Start();
        AnimDebugLog($"SyncProcessLoopSound started sound={sound}, process={processMode}");
    }

    private void StopProcessLoopSound(bool immediate = false)
    {
        if (processLoopSound == null)
        {
            return;
        }

        ILoadedSound sound = processLoopSound;
        processLoopSound = null;

        if (immediate)
        {
            sound.Stop();
            sound.Dispose();
            return;
        }

        sound.FadeOut(0.35f, loadedSound =>
        {
            loadedSound.Stop();
            loadedSound.Dispose();
        });
    }

    private void PlayCharcoalPlaceSound(IPlayer byPlayer)
    {
        AssetLocation sound = CharcoalPlaceSounds[Api.World.Rand.Next(CharcoalPlaceSounds.Length)];
        PlayStationSound(sound, byPlayer, 0.85f);
    }

    private void PlayItemSplashSound(IPlayer byPlayer)
    {
        AssetLocation sound = ItemSplashSounds[Api.World.Rand.Next(ItemSplashSounds.Length)];
        PlayStationSound(sound, byPlayer, 0.8f);
    }

    private void PlayStationSound(AssetLocation sound, IPlayer byPlayer, float volume = 1f)
    {
        AnimDebugLog($"PlayStationSound sound={sound}, pos={Pos}, volume={volume}");
        Api.World.PlaySoundAt(sound, Pos, 0.5, null, true, 16f, volume);
    }

    private void MarkStationDirty(bool redrawOnClient = true)
    {
        AnimDebugLog($"MarkStationDirty redraw={redrawOnClient}, lidOpen={lidOpen}, fuelOpen={fuelOpen}");
        MarkDirty(redrawOnClient);
        if (redrawOnClient)
        {
            Api?.World.BlockAccessor.MarkBlockDirty(Pos);

            BlockPos? proxyPos = GetProxyPartPos();
            if (proxyPos != null)
            {
                Api?.World.BlockAccessor.MarkBlockDirty(proxyPos);
            }
        }
    }

    private BlockPos? GetProxyPartPos()
    {
        string? side = GetStationSideCode();
        if (side == null)
        {
            return null;
        }

        Vec3i offset = side switch
        {
            "east" => new Vec3i(0, 0, 1),
            "south" => new Vec3i(-1, 0, 0),
            "west" => new Vec3i(0, 0, -1),
            _ => new Vec3i(1, 0, 0)
        };

        return Pos.AddCopy(offset.X, offset.Y, offset.Z);
    }

    private string? GetStationSideCode()
    {
        if (Block?.Code == null)
        {
            return null;
        }

        string[] parts = Block.Code.Path.Split('-');
        return parts.Length == 0 ? null : parts[^1];
    }

    private bool IsAnimationVisible(string code)
    {
        RunningAnimation? runningAnimation = AnimUtil?.animator?.GetAnimationState(code);
        if (runningAnimation == null)
        {
            return false;
        }

        if (runningAnimation.AnimProgress > 0.001f)
        {
            return true;
        }

        if (runningAnimation.Active)
        {
            return true;
        }

        return runningAnimation.Running;
    }

    private void EnsureAnimator()
    {
        if (Api is not ICoreClientAPI)
        {
            return;
        }

        var capi = (ICoreClientAPI)Api;
        BlockEntityAnimationUtil? animUtil = AnimUtil;
        if (animUtil == null)
        {
            AnimDebugLog($"EnsureAnimator skipped. animUtil={(animUtil == null ? "null" : animUtil.GetType().FullName)}, renderer={FormatObject(animUtil?.renderer)}");
            return;
        }

        if (animUtil.renderer != null)
        {
            AnimDebugLog($"EnsureAnimator skipped. animUtil={animUtil.GetType().FullName}, renderer={FormatObject(animUtil.renderer)}");
            return;
        }

        AssetLocation shapeLocation = ResolveShapeLocation();
        Shape? shape = Shape.TryGet(Api, shapeLocation);
        if (shape == null)
        {
            AnimDebugLog($"EnsureAnimator shape not found: {shapeLocation}");
            return;
        }

        MeshData result = animUtil.InitializeAnimator(
            "facore-coverstation",
            shape,
            capi.Tesselator.GetTextureSource(Block, 0, false),
            new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ)
        );
        AnimDebugLog($"EnsureAnimator InitializeAnimator result={FormatObject(result)}, shape={shapeLocation}, animUtil={animUtil.GetType().FullName}, renderer={FormatObject(animUtil.renderer)}");
    }

    private AssetLocation ResolveShapeLocation()
    {
        AssetLocation baseLoc = Block.Shape.Base;
        string domain = string.IsNullOrEmpty(baseLoc.Domain) ? Block.Code.Domain : baseLoc.Domain;
        string path = baseLoc.Path;
        if (!path.StartsWith("shapes/")) path = "shapes/" + path;
        if (!path.EndsWith(".json")) path += ".json";
        return new AssetLocation(domain, path);
    }

    private void SyncAnimations()
    {
        bool hasPreviousState = animationStateKnown;
        AnimDebugLog($"SyncAnimations hasPrevious={hasPreviousState}, lidOpen={lidOpen}, fuelOpen={fuelOpen}, lastLid={lastSyncedLidOpen}, lastFuel={lastSyncedFuelOpen}");
        SetAnimationState("lidopen", lidOpen, 3f, hasPreviousState && lastSyncedLidOpen != lidOpen);
        SetAnimationState("fuelopen", fuelOpen, 3f, hasPreviousState && lastSyncedFuelOpen != fuelOpen);

        lastSyncedLidOpen = lidOpen;
        lastSyncedFuelOpen = fuelOpen;
        animationStateKnown = true;
    }

    private void SetAnimationState(string code, bool open, float speed, bool stateChanged)
    {
        BlockEntityAnimationUtil? animUtil = AnimUtil;
        if (animUtil == null)
        {
            AnimDebugLog($"SetAnimationState {code} aborted: animUtil null. open={open}, stateChanged={stateChanged}, behaviors={FormatBehaviors()}");
            return;
        }

        animUtil.activeAnimationsByAnimCode.TryGetValue(code, out AnimationMetaData? activeAnimation);
        AnimDebugLog($"SetAnimationState {code} begin open={open}, speed={speed}, stateChanged={stateChanged}, animUtil={animUtil.GetType().FullName}, active={FormatObject(activeAnimation)}, activeKeys={FormatActiveAnimationKeys(animUtil)}");
        if (!open)
        {
            if (activeAnimation == null)
            {
                AnimDebugLog($"SetAnimationState {code} skipped closed unchanged with no active animation");
                return;
            }

            animUtil.StopAnimation(code);
            AnimDebugLog($"SetAnimationState {code} requested rewind, activeKeys={FormatActiveAnimationKeys(animUtil)}");
            return;
        }

        if (activeAnimation == null)
        {
            float startFrame = stateChanged ? 0f : GetLastAnimationFrame(animUtil, code);
            bool startResult = animUtil.StartAnimation(CreateAnimationMeta(code, speed, startFrame));
            animUtil.activeAnimationsByAnimCode.TryGetValue(code, out activeAnimation);
            AnimDebugLog($"SetAnimationState {code} StartAnimation result={FormatObject(startResult)}, startFrame={startFrame}, activeAfter={FormatObject(activeAnimation)}, activeKeys={FormatActiveAnimationKeys(animUtil)}");
        }

        RunningAnimation? runningAnimation = animUtil.animator?.GetAnimationState(code);
        if (runningAnimation == null)
        {
            AnimDebugLog($"SetAnimationState {code} aborted: runningAnimation null. animator={FormatObject(animUtil.animator)}");
            return;
        }

        float progress = GetAnimationProgress(runningAnimation);
        if (activeAnimation != null)
        {
            activeAnimation.AnimationSpeed = speed;
        }

        AnimDebugLog($"SetAnimationState {code} open active. progress={progress}, running={runningAnimation.Running}, active={runningAnimation.Active}, currentFrame={runningAnimation.CurrentFrame}, shouldRewind={runningAnimation.ShouldRewind}");
    }

    private static AnimationMetaData CreateAnimationMeta(string code, float speed, float startFrame = 0f)
    {
        return new AnimationMetaData
        {
            Animation = code,
            Code = code,
            AnimationSpeed = speed,
            EaseOutSpeed = 1f,
            EaseInSpeed = 2f,
            StartFrameOnce = startFrame
        };
    }

    private BlockEntityAnimationUtil? AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil;

    private static float GetLastAnimationFrame(BlockEntityAnimationUtil animUtil, string code)
    {
        RunningAnimation? runningAnimation = animUtil.animator?.GetAnimationState(code);
        return runningAnimation == null ? 0f : GetLastAnimationFrame(runningAnimation);
    }

    private static float GetAnimationProgress(RunningAnimation runningAnimation)
    {
        return GameMath.Clamp(runningAnimation.AnimProgress, 0f, 1f);
    }

    private static float GetLastAnimationFrame(RunningAnimation runningAnimation)
    {
        return Math.Max(0f, runningAnimation.Animation.QuantityFrames - 1f);
    }

    private void AnimDebugLog(string message)
    {
        Api?.Logger.Notification(
            "[FACore CoverStation AnimDebug] {0}: {1}",
            Pos,
            message
        );
    }

    private string FormatBehaviors()
    {
        var names = new List<string>();
        foreach (BlockEntityBehavior behavior in Behaviors)
        {
            names.Add(behavior.GetType().FullName ?? behavior.GetType().Name);
        }

        return names.Count == 0 ? "[]" : string.Join(", ", names);
    }

    private static string FormatActiveAnimationKeys(BlockEntityAnimationUtil animUtil)
    {
        if (animUtil.activeAnimationsByAnimCode.Count == 0)
        {
            return "[]";
        }

        return string.Join(", ", animUtil.activeAnimationsByAnimCode.Keys);
    }

    private static string FormatObject(object? value)
    {
        return value == null ? "null" : $"{value.GetType().FullName}={value}";
    }

    private static void Notify(IPlayer player, string text)
    {
        if (player is IServerPlayer serverPlayer)
        {
            serverPlayer.SendMessage(GlobalConstants.CurrentChatGroup, text, EnumChatType.Notification);
        }
    }

    private void DebugLiquidLog(string message)
    {
        if (DebugLiquid)
        {
            Api?.Logger.Notification("[FACore CoverStation Liquid] {0}: {1}", Pos, message);
        }
    }

    private static void DebugLiquidStaticLog(string message)
    {
        if (DebugLiquid)
        {
            Console.WriteLine("[FACore CoverStation Liquid] " + message);
        }
    }

    private void DebugFuelLog(string message)
    {
        if (DebugLiquid)
        {
            Api?.Logger.Notification("[FACore CoverStation Fuel] {0}: {1}", Pos, message);
        }
    }

    private static string FormatStackDebug(ItemStack? stack)
    {
        return stack == null ? "null" : $"{stack.StackSize}x {stack.Collectible?.Code}";
    }

    private sealed class MappedTextureSource : ITexPositionSource
    {
        private readonly ITexPositionSource fallback;
        private readonly string textureCode;
        private readonly TextureAtlasPosition? texture;

        public MappedTextureSource(ITexPositionSource fallback, string textureCode, TextureAtlasPosition? texture)
        {
            this.fallback = fallback;
            this.textureCode = textureCode;
            this.texture = texture;
        }

        public TextureAtlasPosition? this[string requestedTextureCode] => requestedTextureCode == textureCode && texture != null
            ? texture
            : fallback[requestedTextureCode];

        public Size2i? AtlasSize => fallback.AtlasSize;
    }

    private sealed class ItemBlockAtlasTextureSource : ITexPositionSource
    {
        private readonly ITexPositionSource fallback;
        private readonly Dictionary<string, TextureAtlasPosition> textures = new(StringComparer.Ordinal);

        public ItemBlockAtlasTextureSource(ICoreClientAPI capi, ITexPositionSource fallback, Item item)
        {
            this.fallback = fallback;

            if (item.Textures == null)
            {
                return;
            }

            foreach ((string code, CompositeTexture itemTexture) in item.Textures)
            {
                CompositeTexture texture = itemTexture.Clone();
                texture.Alpha = 255;
                texture.Bake(capi.Assets);
                capi.BlockTextureAtlas.GetOrInsertTexture(texture, out _, out TextureAtlasPosition texturePosition, 0.005f);
                textures[code] = texturePosition;
            }
        }

        public TextureAtlasPosition? this[string textureCode]
        {
            get
            {
                if (textures.TryGetValue(textureCode, out TextureAtlasPosition? texture))
                {
                    return texture;
                }

                if (textures.TryGetValue("all", out TextureAtlasPosition? allTexture))
                {
                    return allTexture;
                }

                return fallback[textureCode];
            }
        }

        public Size2i? AtlasSize => fallback.AtlasSize;
    }

    private sealed class CompositeBlockAtlasTextureSource : ITexPositionSource
    {
        private readonly ITexPositionSource fallback;
        private readonly Dictionary<string, TextureAtlasPosition> textures = new(StringComparer.Ordinal);

        public CompositeBlockAtlasTextureSource(ICoreClientAPI capi, ITexPositionSource fallback, Dictionary<string, CompositeTexture> sourceTextures)
        {
            this.fallback = fallback;

            foreach ((string code, CompositeTexture sourceTexture) in sourceTextures)
            {
                CompositeTexture texture = sourceTexture.Clone();
                texture.Bake(capi.Assets);
                capi.BlockTextureAtlas.GetOrInsertTexture(texture, out _, out TextureAtlasPosition texturePosition, 0.005f);
                textures[code] = texturePosition;
            }
        }

        public TextureAtlasPosition? this[string textureCode] => textures.TryGetValue(textureCode, out TextureAtlasPosition? texture)
            ? texture
            : fallback[textureCode];

        public Size2i? AtlasSize => fallback.AtlasSize;
    }
}
