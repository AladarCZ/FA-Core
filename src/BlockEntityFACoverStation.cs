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
    private static readonly AssetLocation ItemInsertSound = new("game", "sounds/player/build");
    private static readonly AssetLocation ItemPickupSound = new("game", "sounds/player/buildhigh");
    private static readonly AssetLocation CeramicPlaceSound = new("game", "sounds/block/ceramicplace");
    private static readonly AssetLocation ScrapeSound = new("game", "sounds/player/scrape");
    private static readonly AssetLocation SolderSound = new("game", "sounds/effect/moltenmetal");
    private static readonly AssetLocation[] MetalHitSounds =
    [
        new("game", "sounds/effect/anvilhit1"),
        new("game", "sounds/effect/anvilhit2"),
        new("game", "sounds/effect/anvilhit3")
    ];
    private static readonly AssetLocation[] SawSounds =
    [
        new("game", "sounds/tool/groundcrafting/saw1"),
        new("game", "sounds/tool/groundcrafting/saw2"),
        new("game", "sounds/tool/groundcrafting/saw3"),
        new("game", "sounds/tool/groundcrafting/saw4")
    ];
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
        ["copper"] = "orange",
        ["cupronickel"] = "gray",
        ["electrum"] = "yellow",
        ["gold"] = "yellow",
        ["lead"] = "gray",
        ["meteoriciron"] = "gray",
        ["silver"] = "white",
        ["tinbronze"] = "brown",
        ["uranium"] = "green",
        ["zinc"] = "white"
    };
    private static readonly AssetLocation CharcoalCode = new("game:charcoal");
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
    private const double ArmorDissolveHours = 2.0;
    private const string ProcessPlateResting = "plate";
    private const string ProcessArmorCoating = "armor";
    private const string ProcessArmorDissolving = "dissolve";
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
    private const float TableMinX = 0f / 16f;
    private const float TableMaxX = 14f / 16f;
    private const float TableMinZ = 0f / 16f;
    private const float TableMaxZ = 16f / 16f;
    private const float TableTopY = 18f / 16f + 0.003f;
    private const float TableItemMaxDimension = 0.44f;
    private const int DecorationClothCapacity = 12;
    private const float DecorationShelfItemScale = 0.8f;
    private const float DecorationShelfItemYawDegrees = 45f;
    private const float TrimRivetsBowlItemScale = 0.70f;
    private const float TrimCrucibleItemScale = 0.8925f;
    private const float TrimCrucibleItemYOffset = 1f / 16f;
    private const float TrimCrucibleItemYawDegrees = 45f;
    private const float TrimSolderingIronItemScale = 0.7f;
    private const int TrimSolderWeldAmount = 100;
    private const float TrimSolderLeadMinTemperature = 327f;
    private const float TrimSolderSilverMinTemperature = 961f;
    private const int DecorationVisibleClothStacks = 6;
    private const float DecorationClothStackScale = 0.8f;
    private const float DecorationClothLayerYOffset = 1f / 64f;
    // Use the player seraph as the armor frame: it lives in the reliably-loaded "game" domain (the survival
    // armorstand shape is not retrievable via Shape.TryGet at tesselation time) and carries the exact joints
    // the worn armor step-parents onto. Its own body renders transparent so only the armor shows.
    private static readonly AssetLocation ArmorStandShapeLocation = new("game", "shapes/entity/humanoid/seraph.json");
    private static readonly AssetLocation TransparentTextureLocation = new("game", "block/transparent");
    // Texture codes of the base frame body that we hide so only the armor is visible.
    private static readonly string[] ArmorStandHiddenTextureCodes = ["seraph", "hair"];
    // Yaw applied to the assembled figure for a north-facing station; other sides add 90 deg steps.
    // +90 deg (a 180 turn from the previous -90) faces the figure out of the stand.
    private static readonly float ArmorStandBaseFacingRadians = GameMath.PIHALF;
    // Small nudge of the figure away from the main block part (toward the proxy), in blocks.
    private const float ArmorStandPushFromMain = 1.5f / 16f;
    // Small nudge in the direction the displayed armor is facing, in blocks.
    private const float ArmorStandForwardNudge = 4f / 16f;
    // Lowers the displayed armor slightly so the feet sit into the stand.
    private const float ArmorStandDropY = 2f / 16f;
    private const float TrimArmorScale = 0.95f;
    private const float TrimArmorExtraDropY = 0.5f;

    private bool lidOpen;
    private bool fuelOpen;
    private bool fuelLit;
    private ItemStack? liquidStack;
    private ItemStack? immersedStack;
    private ItemStack? fuelStack;
    private ItemStack? tableStack;
    private ItemStack? decorationKitHeadStack;
    private ItemStack? ornamentsStack;
    private ItemStack? decorationKitBodyStack;
    private ItemStack? bracketsStack;
    private ItemStack? decorationKitLegsStack;
    private ItemStack? fastenersStack;
    private readonly List<ItemStack> decorationClothStacks = [];
    private ItemStack? decorationHelmetStack;
    private ItemStack? decorationBodyStack;
    private ItemStack? decorationLegsStack;
    private ItemStack? pendingHeadDecorationStack;
    private ItemStack? pendingHeadColorStack;
    private ItemStack? pendingBodyDecorationStack;
    private ItemStack? pendingBodyColorStack;
    private ItemStack? pendingLegsDecorationStack;
    private ItemStack? pendingLegsColorStack;
    private string pendingHeadOriginalDecoration = "";
    private string pendingHeadOriginalColor = "";
    private string pendingBodyOriginalDecoration = "";
    private string pendingBodyOriginalColor = "";
    private string pendingLegsOriginalDecoration = "";
    private string pendingLegsOriginalColor = "";
    private string armorStandRenderDebug = "idle";
    private ItemStack? trimArmorStack;
    private ItemStack? trimRivetsStack;
    private ItemStack? trimCrucibleStack;
    private ItemStack? trimSolderingIronStack;
    private ItemStack? pendingTrimRivetsStack;
    private string pendingTrimOriginalStrip = "";
    private string trimArmorRenderDebug = "idle";
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

    private bool IsCoverStationBlock()
    {
        string[] parts = Block?.Code?.Path.Split('-') ?? [];
        if (parts.Length < 3 || parts[0] != "fa" || parts[1] != "workstation")
        {
            return true;
        }

        return parts[2] == "cover";
    }

    private bool IsDecorationStationBlock()
    {
        string[] parts = Block?.Code?.Path.Split('-') ?? [];
        return parts.Length >= 3
            && parts[0] == "fa"
            && parts[1] == "workstation"
            && parts[2] == "decoration";
    }

    private bool IsTrimStationBlock()
    {
        string[] parts = Block?.Code?.Path.Split('-') ?? [];
        return parts.Length >= 3
            && parts[0] == "fa"
            && parts[1] == "workstation"
            && parts[2] == "trim";
    }

    public float GetAnimationProgress(string code, bool openFallback)
    {
        RunningAnimation? runningAnimation = AnimUtil?.animator?.GetAnimationState(code);
        return runningAnimation == null ? openFallback ? 1f : 0f : GameMath.Clamp(runningAnimation.AnimProgress, 0f, 1f);
    }

    public void HandleElementInteraction(IPlayer byPlayer, string actionName)
    {
        if (!IsCoverStationBlock())
        {
            if (IsDecorationStationBlock())
            {
                HandleDecorationElementInteraction(byPlayer, actionName);
                return;
            }

            if (IsTrimStationBlock())
            {
                HandleTrimElementInteraction(byPlayer, actionName);
            }

            return;
        }

        switch (actionName)
        {
            case "LidOpen":
                UpdateProcess();
                if (fuelLit || processMode == ProcessArmorCoating)
                {
                    Notify(byPlayer, "Wait for the cauldron to cool before opening it.");
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

            case "TableStorage":
                TryInteractTable(byPlayer);
                return;

            default:
                Notify(byPlayer, "This part of the station cannot be used right now.");
                return;
        }
    }

    public string DescribeState()
    {
        if (!IsCoverStationBlock())
        {
            return "";
        }

        var builder = new StringBuilder();
        AppendStationDisplayInfo(builder);
        return builder.ToString();
    }

    public string DescribeElementState(string actionName)
    {
        if (IsDecorationStationBlock())
        {
            return DescribeDecorationElementState(actionName);
        }

        if (IsTrimStationBlock())
        {
            return DescribeTrimElementState(actionName);
        }

        return DescribeState();
    }

    private string DescribeDecorationElementState(string actionName)
    {
        return actionName switch
        {
            "HelmetDeco" => DescribeDecorationArmorState("Helmet", "head", decorationHelmetStack),
            "BodyDeco" => DescribeDecorationArmorState("Chestplate", "body", decorationBodyStack),
            "LegsDeco" => DescribeDecorationArmorState("Leggings", "legs", decorationLegsStack),
            "CupboardSlot1" or "CupboardSlot2" or "CupboardSlot3" or "CupboardSlot4" or "CupboardSlot5" or "CupboardSlot6"
                => DescribeStorageSlotState(GetDecorationSlotName(actionName), GetDecorationShelfStack(actionName)),
            "MordantLinenStack" => DescribeDecorationClothState(),
            _ => ""
        };
    }

    private string DescribeDecorationArmorState(string label, string piece, ItemStack? armorStack)
    {
        var builder = new StringBuilder();
        builder.Append(label);
        builder.Append(": ");
        builder.Append(armorStack?.GetName() ?? "empty");

        if (armorStack == null)
        {
            return builder.ToString();
        }

        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        string decoration = FormatStationValue(types?.GetString("decoration" + piece, "none") ?? "none");
        string color = FormatStationValue(types?.GetString("color" + piece, "none") ?? "none");
        bool hasPending = HasPendingDecorationEdits(piece);
        bool hasBaked = !hasPending && (decoration != "none" || color != "none");

        builder.AppendLine();
        builder.Append("Decoration: ");
        builder.Append(decoration);
        builder.AppendLine();
        builder.Append("Color: ");
        builder.Append(color);
        builder.AppendLine();
        builder.Append(hasBaked ? "Finished" : "Unfinished");
        return builder.ToString();
    }

    private string DescribeDecorationClothState()
    {
        var builder = new StringBuilder();
        builder.Append("Cloth: ");
        builder.Append(decorationClothStacks.Count);
        builder.Append("/");
        builder.Append(DecorationClothCapacity);

        if (decorationClothStacks.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Top: ");
            builder.Append(decorationClothStacks[^1].GetName());
        }

        return builder.ToString();
    }

    private string DescribeTrimElementState(string actionName)
    {
        return actionName switch
        {
            "Armor" => DescribeTrimArmorState(),
            "RivetsPlace" => DescribeTrimRivetsState(),
            "CruciblePlace" => DescribeTrimCrucibleState(),
            "SolderHolder" => DescribeStorageSlotState("Soldering iron", trimSolderingIronStack),
            _ => ""
        };
    }

    private string DescribeTrimArmorState()
    {
        var builder = new StringBuilder();
        builder.Append("Mounted: ");
        builder.Append(trimArmorStack?.GetName() ?? "empty");

        if (trimArmorStack == null)
        {
            return builder.ToString();
        }

        string trim = "none";
        if (TryGetFAArmorInfo(trimArmorStack, out FAArmorInfo armorInfo))
        {
            ITreeAttribute? types = trimArmorStack.Attributes?.GetTreeAttribute("types");
            trim = FormatStationValue(types?.GetString("strip" + armorInfo.Piece, "none") ?? "none");
        }

        bool hasPending = pendingTrimRivetsStack != null;
        bool hasBaked = !hasPending && trim != "none";

        builder.AppendLine();
        builder.Append("Rivets: ");
        builder.Append(trim);
        builder.AppendLine();
        builder.Append("Status: ");
        builder.Append(hasBaked ? "Finished" : "Unfinished");
        return builder.ToString();
    }

    private string DescribeTrimRivetsState()
    {
        var builder = new StringBuilder();
        builder.Append("Rims and rivets: ");
        builder.Append(trimRivetsStack?.GetName() ?? "empty");

        if (pendingTrimRivetsStack != null)
        {
            builder.AppendLine();
            builder.Append("Staged: ");
            builder.Append(pendingTrimRivetsStack.GetName());
            builder.Append(" (Unfinished)");
        }

        return builder.ToString();
    }

    private string DescribeTrimCrucibleState()
    {
        var builder = new StringBuilder();
        builder.Append("Crucible: ");
        builder.Append(trimCrucibleStack?.GetName() ?? "empty");

        if (trimCrucibleStack == null)
        {
            builder.AppendLine();
            builder.Append("Solder: 0 ml / ");
            builder.Append(TrimSolderWeldAmount);
            builder.Append(" ml");
            builder.AppendLine();
            builder.Append("Status: Not Ok");
            return builder.ToString();
        }

        if (!TryGetSolderContent(trimCrucibleStack, out SolderContent solderContent, logDebug: false))
        {
            builder.AppendLine();
            builder.Append("Solder: unreadable / ");
            builder.Append(TrimSolderWeldAmount);
            builder.Append(" ml");
            builder.AppendLine();
            builder.Append("Status: Not Ok");
            return builder.ToString();
        }

        float requiredTemperature = solderContent.Metal == "silver" ? TrimSolderSilverMinTemperature : TrimSolderLeadMinTemperature;
        bool ready = solderContent.Amount >= TrimSolderWeldAmount && solderContent.Temperature >= requiredTemperature;

        builder.AppendLine();
        builder.Append("Solder: ");
        builder.Append(solderContent.Amount);
        builder.Append(" ml / ");
        builder.Append(TrimSolderWeldAmount);
        builder.Append(" ml");
        builder.AppendLine();
        builder.Append("Metal: ");
        builder.Append(FormatStationValue(solderContent.Metal));
        builder.AppendLine();
        builder.Append("Temperature: ");
        builder.Append(solderContent.Temperature.ToString("0"));
        builder.Append("C");
        builder.AppendLine();
        builder.Append("Status: ");
        builder.Append(ready ? "Ok" : "Not Ok");
        return builder.ToString();
    }

    private static string DescribeStorageSlotState(string label, ItemStack? stack)
    {
        return label + ": " + (stack?.GetName() ?? "empty");
    }

    private static string FormatStationValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }

        return value switch
        {
            "bismuthbronze" => "bismuth bronze",
            "blackbronze" => "black bronze",
            "tinbronze" => "tin bronze",
            "meteoriciron" => "meteoric iron",
            "blistersteel" => "blister steel",
            _ => value
        };
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
    public bool HasTableItem => tableStack != null;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (api.Side == EnumAppSide.Server && Block is BlockFAStation station)
        {
            station.EnsureStationStructure(api.World, Pos);
        }

        if (!IsCoverStationBlock())
        {
            return;
        }

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
        if (IsDecorationStationBlock())
        {
            WriteDecorationTreeAttributes(tree);
            return;
        }

        if (IsTrimStationBlock())
        {
            WriteTrimTreeAttributes(tree);
            return;
        }

        if (!IsCoverStationBlock())
        {
            return;
        }

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
        if (IsDecorationStationBlock())
        {
            ReadDecorationTreeAttributes(tree, worldForResolving);
            return;
        }

        if (IsTrimStationBlock())
        {
            ReadTrimTreeAttributes(tree, worldForResolving);
            return;
        }

        if (!IsCoverStationBlock())
        {
            return;
        }

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
        if (IsDecorationStationBlock())
        {
            AddDecorationStationMeshes(mesher, tessThreadTesselator);
            return skipDefaultMesh;
        }

        if (IsTrimStationBlock())
        {
            AddTrimStationMeshes(mesher, tessThreadTesselator);
            return skipDefaultMesh;
        }

        if (!IsCoverStationBlock())
        {
            return skipDefaultMesh;
        }

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

        if (tableStack != null)
        {
            MeshData? tableMesh = TryCreateTableItemMesh(tessThreadTesselator);
            if (tableMesh != null)
            {
                mesher.AddMeshData(tableMesh, 1);
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
        if (IsDecorationStationBlock())
        {
            base.OnBlockBroken(byPlayer);
            DropDecorationInventory();
            return;
        }

        if (IsTrimStationBlock())
        {
            base.OnBlockBroken(byPlayer);
            DropTrimInventory();
            return;
        }

        if (!IsCoverStationBlock())
        {
            base.OnBlockBroken(byPlayer);
            return;
        }

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
        if (!IsCoverStationBlock())
        {
            base.OnBlockRemoved();
            return;
        }

        StopProcessLoopSound(immediate: true);
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        if (!IsCoverStationBlock())
        {
            base.OnBlockUnloaded();
            return;
        }

        StopProcessLoopSound(immediate: true);
        base.OnBlockUnloaded();
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        if (IsDecorationStationBlock())
        {
            dsc.Clear();
            if (TryAppendSelectedElementInfo(forPlayer, dsc))
            {
                return;
            }

            AppendDecorationStationDisplayInfo(dsc);
            return;
        }

        if (IsTrimStationBlock())
        {
            dsc.Clear();
            if (TryAppendSelectedElementInfo(forPlayer, dsc))
            {
                return;
            }

            AppendTrimStationDisplayInfo(dsc);
            return;
        }

        base.GetBlockInfo(forPlayer, dsc);

        if (!IsCoverStationBlock())
        {
            return;
        }

        if (dsc.Length > 0)
        {
            dsc.AppendLine();
        }

        AppendStationDisplayInfo(dsc);
    }

    private bool TryAppendSelectedElementInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        BlockSelection? selection = forPlayer.CurrentBlockSelection;
        if (selection == null || selection.SelectionBoxIndex < 0)
        {
            return false;
        }

        if (!TryResolveSelectedElementAction(selection, out string actionName))
        {
            return false;
        }

        string info = DescribeElementState(actionName);
        if (string.IsNullOrWhiteSpace(info))
        {
            return false;
        }

        dsc.Append(info);
        return true;
    }

    private bool TryResolveSelectedElementAction(BlockSelection selection, out string actionName)
    {
        actionName = "";
        List<StationElementZone> zones = StationShapeElementReader.LoadElementZones(Api, Block);
        if (zones.Count == 0)
        {
            return false;
        }

        Vec3i partOffset = new(
            selection.Position.X - Pos.X,
            selection.Position.Y - Pos.Y,
            selection.Position.Z - Pos.Z
        );

        if (selection.HitPosition != null)
        {
            StationElementZone? bestZone = null;
            double bestDistance = double.MaxValue;
            foreach (StationElementZone zone in zones)
            {
                double distance = DistanceToBoxCenter(selection.HitPosition, ToPartBox(zone.StationBox, partOffset));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestZone = zone;
                }
            }

            if (bestZone != null)
            {
                actionName = bestZone.ActionName;
                return true;
            }
        }

        int zoneIndex = selection.SelectionBoxIndex;
        if (zoneIndex < 0 || zoneIndex >= zones.Count)
        {
            return false;
        }

        actionName = zones[zoneIndex].ActionName;
        return true;
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

    private static double DistanceToBoxCenter(Vec3d point, Cuboidf box)
    {
        double centerX = (box.X1 + box.X2) * 0.5;
        double centerY = (box.Y1 + box.Y2) * 0.5;
        double centerZ = (box.Z1 + box.Z2) * 0.5;
        double dx = point.X - centerX;
        double dy = point.Y - centerY;
        double dz = point.Z - centerZ;
        return dx * dx + dy * dy + dz * dz;
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
            Notify(byPlayer, "Hold coal or charcoal to fuel the station.");
            return;
        }

        if (fuelLit)
        {
            Notify(byPlayer, "The fuel is already burning.");
            return;
        }

        if (fuelStack != null && !fuelStack.Equals(Api.World, heldStack, GlobalConstants.IgnoredStackAttributes))
        {
            Notify(byPlayer, "Remove the current fuel before adding a different fuel type.");
            return;
        }

        int room = MaxCharcoalPieces - (fuelStack?.StackSize ?? 0);
        if (room <= 0)
        {
            Notify(byPlayer, "The fuel tray is already full.");
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
        NotifyInfo(byPlayer, $"Added {inserted.StackSize}x {inserted.GetName()} to the fuel tray.");
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
            Notify(byPlayer, "The fuel is burning and cannot be removed.");
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
        NotifyInfo(byPlayer, $"Removed 1x {takeName} from the fuel tray.");
    }

    private void TryInteractTable(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (tableStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, "The table is already holding an item.");
                return;
            }

            ItemStack takeStack = tableStack;
            tableStack = null;
            string takeName = takeStack.GetName();
            if (!byPlayer.InventoryManager.TryGiveItemstack(takeStack, true))
            {
                Api.World.SpawnItemEntity(takeStack, Pos.ToVec3d().Add(0.5, 1.1, 0.5));
            }

            MarkStationDirty();
            NotifyInfo(byPlayer, $"Picked up {takeName} from the table.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, "The table is empty.");
            return;
        }

        tableStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {tableStack.GetName()} on the table.");
    }

    private void HandleDecorationElementInteraction(IPlayer byPlayer, string actionName)
    {
        switch (actionName)
        {
            case "CupboardSlot1":
            case "CupboardSlot2":
            case "CupboardSlot3":
            case "CupboardSlot4":
            case "CupboardSlot5":
            case "CupboardSlot6":
                TryInteractDecorationShelfSlot(byPlayer, actionName);
                return;

            case "MordantLinenStack":
                TryInteractDecorationClothStorage(byPlayer);
                return;

            case "HelmetDeco":
                TryInteractDecorationArmorSlot(byPlayer, actionName, "head", "helmet");
                return;

            case "BodyDeco":
                TryInteractDecorationArmorSlot(byPlayer, actionName, "body", "chestplate");
                return;

            case "LegsDeco":
                TryInteractDecorationArmorSlot(byPlayer, actionName, "legs", "leggings");
                return;

            default:
                Notify(byPlayer, "This part of the decoration station cannot be used right now.");
                return;
        }
    }

    private void TryInteractDecorationShelfSlot(IPlayer byPlayer, string actionName)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;
        ItemStack? storedStack = GetDecorationShelfStack(actionName);
        string slotName = GetDecorationSlotName(actionName);

        if (storedStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, $"The {slotName} is already holding an item.");
                return;
            }

            SetDecorationShelfStack(actionName, null);
            GiveOrDrop(byPlayer, storedStack, 1.1);
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, $"Picked up {storedStack.GetName()} from the {slotName}.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, $"The {slotName} is empty.");
            return;
        }

        if (!IsDecorationSmallItem(heldStack))
        {
            Notify(byPlayer, "Only Forgotten Armory fittings (rivets, brackets, fasteners, ornaments, decoration kits) belong in the cupboard.");
            return;
        }

        ItemStack inserted = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        SetDecorationShelfStack(actionName, inserted);
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {inserted.GetName()} in the {slotName}.");
    }

    private void TryInteractDecorationClothStorage(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (heldStack == null)
        {
            if (decorationClothStacks.Count == 0)
            {
                Notify(byPlayer, "The cloth cabinet is empty.");
                return;
            }

            ItemStack takeStack = decorationClothStacks[^1];
            decorationClothStacks.RemoveAt(decorationClothStacks.Count - 1);
            GiveOrDrop(byPlayer, takeStack, 0.65);
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, $"Picked up {takeStack.GetName()} from the cloth cabinet ({decorationClothStacks.Count}/{DecorationClothCapacity}).");
            return;
        }

        if (!IsDecorationCloth(heldStack))
        {
            Notify(byPlayer, "Only cloth can be stored in the cabinet.");
            return;
        }

        if (decorationClothStacks.Count >= DecorationClothCapacity)
        {
            Notify(byPlayer, "The cloth cabinet is full.");
            return;
        }

        ItemStack inserted = activeSlot!.TakeOut(1);
        activeSlot.MarkDirty();
        decorationClothStacks.Add(inserted);
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Stored {inserted.GetName()} in the cloth cabinet ({decorationClothStacks.Count}/{DecorationClothCapacity}).");
    }

    private void TryInteractDecorationArmorSlot(IPlayer byPlayer, string actionName, string expectedPiece, string slotName)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;
        ItemStack? storedStack = GetDecorationArmorStack(actionName);

        if (storedStack != null)
        {
            if (IsIronOrBetterTool(heldStack, "hammer"))
            {
                TryBakeDecorationArmor(byPlayer, storedStack, expectedPiece, slotName);
                return;
            }

            if (IsIronOrBetterTool(heldStack, "saw"))
            {
                if (!TryRemoveBakedDecorationWithSaw(byPlayer, storedStack, expectedPiece, slotName))
                {
                    Notify(byPlayer, $"There is no baked decoration to remove from this {slotName}.");
                }

                return;
            }

            if (heldStack != null)
            {
                TryStageDecorationMaterial(byPlayer, activeSlot!, heldStack, storedStack, expectedPiece, slotName);
                return;
            }

            if (HasPendingDecorationEdits(expectedPiece))
            {
                TryRemovePendingDecorationMaterial(byPlayer, storedStack, expectedPiece);
                return;
            }

            SetDecorationArmorStack(actionName, null);
            GiveOrDrop(byPlayer, storedStack, 1.1);
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, $"Picked up {storedStack.GetName()} from the armor stand.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, $"The armor stand has no {slotName}.");
            return;
        }

        if (!TryGetFAArmorPiece(heldStack, out string piece) || piece != expectedPiece)
        {
            Notify(byPlayer, $"Place a Forgotten Armory {slotName} here.");
            return;
        }

        ItemStack inserted = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        SetDecorationArmorStack(actionName, inserted);
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {inserted.GetName()} on the armor stand.");
    }

    private void TryStageDecorationMaterial(IPlayer byPlayer, ItemSlot activeSlot, ItemStack heldStack, ItemStack armorStack, string expectedPiece, string slotName)
    {
        if (!TryParseDecorationMaterial(heldStack, expectedPiece, out string editKind, out string value, out string failure))
        {
            Notify(byPlayer, failure.Length > 0 ? failure : $"That material does not belong on this {slotName}.");
            return;
        }

        if (!TryGetFAArmorInfo(armorStack, out FAArmorInfo armorInfo) || armorInfo.Piece != expectedPiece)
        {
            Notify(byPlayer, $"Place a Forgotten Armory {slotName} here first.");
            return;
        }

        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            Notify(byPlayer, "This armor piece cannot be decorated.");
            return;
        }

        if (GetPendingDecorationMaterial(expectedPiece, editKind) != null)
        {
            Notify(byPlayer, $"This {slotName} already has a staged {GetDecorationEditName(editKind)}. Remove it first.");
            return;
        }

        string attrKey = editKind + expectedPiece;
        string currentValue = types.GetString(attrKey) ?? "none";
        if (!string.Equals(currentValue, "none", StringComparison.Ordinal))
        {
            Notify(byPlayer, $"This {slotName} already has {GetDecorationEditName(editKind)} baked in.");
            return;
        }

        if (editKind == "color")
        {
            string currentDecoration = types.GetString("decoration" + expectedPiece) ?? "none";
            if (string.Equals(currentDecoration, "none", StringComparison.Ordinal))
            {
                Notify(byPlayer, $"Add a decoration to the {slotName} before adding a color kit.");
                return;
            }
        }

        if (editKind == "decoration"
            && !TryResolveFAArmorShape(armorInfo, value, types.GetString("form" + expectedPiece, "") ?? "", out _, out _))
        {
            Notify(byPlayer, $"This decoration does not fit the {slotName}.");
            return;
        }

        ItemStack inserted = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        SetPendingOriginalValue(expectedPiece, editKind, currentValue);
        SetPendingDecorationMaterial(expectedPiece, editKind, inserted);
        types.SetString(attrKey, value);
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Previewing {inserted.GetName()} on the {slotName}. Right-click with an iron or better hammer to bake it.");
    }

    private bool TryRemoveBakedDecorationWithSaw(IPlayer byPlayer, ItemStack armorStack, string piece, string slotName)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            Notify(byPlayer, "This armor piece cannot be decorated.");
            return true;
        }

        string decorationKey = "decoration" + piece;
        string colorKey = "color" + piece;
        if (HasPendingDecorationEdits(piece))
        {
            Notify(byPlayer, $"Only staged decoration is on this {slotName}. Use an empty hand to remove the preview.");
            return true;
        }

        bool hadDecoration = !string.Equals(types.GetString(decorationKey, "none"), "none", StringComparison.Ordinal);
        bool hadColor = !string.Equals(types.GetString(colorKey, "none"), "none", StringComparison.Ordinal);

        if (!hadDecoration && !hadColor)
        {
            return false;
        }

        types.SetString(decorationKey, "none");
        types.SetString(colorKey, "none");
        PlaySawSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Unbaked and removed the {slotName} decoration.");
        return true;
    }

    private bool TryRemovePendingDecorationMaterial(IPlayer byPlayer, ItemStack armorStack, string piece)
    {
        if (TryRemovePendingDecorationMaterial(byPlayer, armorStack, piece, "color"))
        {
            return true;
        }

        return TryRemovePendingDecorationMaterial(byPlayer, armorStack, piece, "decoration");
    }

    private bool TryRemovePendingDecorationMaterial(IPlayer byPlayer, ItemStack armorStack, string piece, string editKind)
    {
        ItemStack? pendingStack = GetPendingDecorationMaterial(piece, editKind);
        if (pendingStack == null)
        {
            return false;
        }

        RestorePendingDecorationValue(armorStack, piece, editKind);
        SetPendingDecorationMaterial(piece, editKind, null);
        SetPendingOriginalValue(piece, editKind, "");
        GiveOrDrop(byPlayer, pendingStack, 1.1);
        PlayItemPickupSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Removed staged {pendingStack.GetName()}.");
        return true;
    }

    private void TryBakeDecorationArmor(IPlayer byPlayer, ItemStack armorStack, string piece, string slotName)
    {
        if (!HasPendingDecorationEdits(piece))
        {
            Notify(byPlayer, $"There are no staged decorations to bake on this {slotName}.");
            return;
        }

        SetPendingDecorationMaterial(piece, "decoration", null);
        SetPendingDecorationMaterial(piece, "color", null);
        SetPendingOriginalValue(piece, "decoration", "");
        SetPendingOriginalValue(piece, "color", "");
        PlayMetalHitSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Baked the staged {slotName} decoration.");
    }

    private void HandleTrimElementInteraction(IPlayer byPlayer, string actionName)
    {
        switch (actionName)
        {
            case "Armor":
                TryInteractTrimArmorSlot(byPlayer);
                return;

            case "RivetsPlace":
                TryInteractTrimRivetsPlace(byPlayer);
                return;

            case "CruciblePlace":
                TryInteractTrimCruciblePlace(byPlayer);
                return;

            case "SolderHolder":
                TryInteractTrimSolderHolder(byPlayer);
                return;

            default:
                Notify(byPlayer, "This part of the hemming crane cannot be used right now.");
                return;
        }
    }

    private void TryInteractTrimArmorSlot(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (trimArmorStack != null)
        {
            if (IsIronOrBetterTongs(heldStack))
            {
                TryRemoveBakedTrimWithTongs(byPlayer);
                return;
            }

            if (IsSneaking(byPlayer))
            {
                if (!IsSolderingIron(heldStack))
                {
                    Notify(byPlayer, "Hold a soldering iron to bake the staged trim.");
                    return;
                }

                TryBakeTrimArmor(byPlayer);
                return;
            }

            if (heldStack != null)
            {
                TryStageTrimMaterial(byPlayer, activeSlot!, heldStack);
                return;
            }

            if (TryRemovePendingTrimMaterial(byPlayer))
            {
                return;
            }

            GiveOrDrop(byPlayer, trimArmorStack, 1.1);
            trimArmorStack = null;
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, "Picked up armor from the hemming crane.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, "The hemming crane has no armor.");
            return;
        }

        if (!TryGetFAArmorPiece(heldStack, out _))
        {
            Notify(byPlayer, "Place a Forgotten Armory armor piece here.");
            return;
        }

        trimArmorStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {trimArmorStack.GetName()} on the hemming crane.");
    }

    private void TryInteractTrimRivetsPlace(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (trimArmorStack != null && IsIronOrBetterTongs(heldStack))
        {
            TryRemoveBakedTrimWithTongs(byPlayer);
            return;
        }

        if (trimArmorStack != null && heldStack != null)
        {
            TryStageTrimMaterial(byPlayer, activeSlot!, heldStack);
            return;
        }

        if (trimArmorStack != null && heldStack == null && TryRemovePendingTrimMaterial(byPlayer))
        {
            return;
        }

        if (trimRivetsStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, "The rims and rivets bowl is already occupied.");
                return;
            }

            GiveOrDrop(byPlayer, trimRivetsStack, 0.9);
            trimRivetsStack = null;
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, "Picked up rims and rivets from the bowl.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, "The rims and rivets bowl is empty.");
            return;
        }

        if (!TryGetRimsAndRivetsMetal(heldStack, out _))
        {
            Notify(byPlayer, "Only rims and rivets belong in this bowl.");
            return;
        }

        trimRivetsStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {trimRivetsStack.GetName()} in the bowl.");
    }

    private void TryInteractTrimCruciblePlace(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;
        TrimDebugLog($"CruciblePlace interact held={FormatStackDebug(heldStack)} stored={FormatStackDebug(trimCrucibleStack)} heldAttrs={FormatTreeDebug(heldStack?.Attributes)}");

        if (trimCrucibleStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, "The crucible stand is already occupied.");
                return;
            }

            GiveOrDrop(byPlayer, trimCrucibleStack, 0.9);
            trimCrucibleStack = null;
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, "Picked up the solder crucible.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, "The crucible stand is empty.");
            return;
        }

        if (!IsLeadOrSilverSolderCrucible(heldStack, requireHeat: false, out string solderMetal, out string failure))
        {
            TrimDebugLog($"CruciblePlace rejected held={FormatStackDebug(heldStack)} failure={failure} attrs={FormatTreeDebug(heldStack.Attributes)}");
            Notify(byPlayer, failure);
            return;
        }

        trimCrucibleStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        TrimDebugLog($"CruciblePlace accepted metal={solderMetal} stored={FormatStackDebug(trimCrucibleStack)} attrs={FormatTreeDebug(trimCrucibleStack?.Attributes)}");
        PlayCeramicPlaceSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed hot {solderMetal} solder crucible on the stand.");
    }

    private void TryInteractTrimSolderHolder(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (trimSolderingIronStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, "The soldering iron holder is already occupied.");
                return;
            }

            GiveOrDrop(byPlayer, trimSolderingIronStack, 0.9);
            trimSolderingIronStack = null;
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, "Picked up the soldering iron.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, "The soldering iron holder is empty.");
            return;
        }

        if (!IsSolderingIron(heldStack))
        {
            Notify(byPlayer, "Only a soldering iron belongs in this holder.");
            return;
        }

        trimSolderingIronStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {trimSolderingIronStack.GetName()} in the holder.");
    }

    private void TryStageTrimMaterial(IPlayer byPlayer, ItemSlot activeSlot, ItemStack heldStack)
    {
        if (trimArmorStack == null)
        {
            Notify(byPlayer, "Place armor on the hemming crane first.");
            return;
        }

        if (!TryGetRimsAndRivetsMetal(heldStack, out string metal))
        {
            Notify(byPlayer, "Use rims and rivets to preview trim.");
            return;
        }

        if (pendingTrimRivetsStack != null)
        {
            Notify(byPlayer, "This armor already has staged trim. Remove it first.");
            return;
        }

        if (!TryGetFAArmorInfo(trimArmorStack, out FAArmorInfo armorInfo))
        {
            Notify(byPlayer, "This armor piece cannot be trimmed.");
            return;
        }

        ITreeAttribute? types = trimArmorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            Notify(byPlayer, "This armor piece cannot be trimmed.");
            return;
        }

        string stripKey = "strip" + armorInfo.Piece;
        string currentStrip = types.GetString(stripKey) ?? "none";

        if (!TextureAssetExists(new AssetLocation("facore", $"armor/entity/trim/{metal}")))
        {
            Notify(byPlayer, $"No trim texture exists for {metal}.");
            return;
        }

        pendingTrimOriginalStrip = currentStrip;
        pendingTrimRivetsStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        types.SetString(stripKey, metal);
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        if (IsTrimValueSet(currentStrip))
        {
            NotifyInfo(byPlayer, $"Previewing {pendingTrimRivetsStack.GetName()} trim. Remove the existing rivets with iron or better tongs before baking.");
            return;
        }

        NotifyInfo(byPlayer, $"Previewing {pendingTrimRivetsStack.GetName()} trim. Hold a soldering iron and sneak-right-click the armor to bake it.");
    }

    private bool TryRemovePendingTrimMaterial(IPlayer byPlayer)
    {
        if (trimArmorStack == null || pendingTrimRivetsStack == null)
        {
            return false;
        }

        RestorePendingTrimPreview();
        GiveOrDrop(byPlayer, pendingTrimRivetsStack, 0.9);
        pendingTrimRivetsStack = null;
        pendingTrimOriginalStrip = "";
        PlayItemPickupSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, "Removed staged trim.");
        return true;
    }

    private void TryBakeTrimArmor(IPlayer byPlayer)
    {
        if (trimArmorStack == null)
        {
            Notify(byPlayer, "The hemming crane has no armor.");
            return;
        }

        if (pendingTrimRivetsStack == null)
        {
            Notify(byPlayer, "There is no staged trim to bake.");
            return;
        }

        if (IsTrimValueSet(pendingTrimOriginalStrip))
        {
            Notify(byPlayer, "This armor already has baked rivets. Remove them with iron or better tongs before baking new rivets.");
            return;
        }

        if (!TryConsumeTrimSolder(out string solderFailure))
        {
            Notify(byPlayer, solderFailure);
            return;
        }

        pendingTrimRivetsStack = null;
        pendingTrimOriginalStrip = "";
        PlaySolderSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, "Welded and baked the staged trim.");
    }

    private void TryRemoveBakedTrimWithTongs(IPlayer byPlayer)
    {
        if (trimArmorStack == null || !TryGetFAArmorInfo(trimArmorStack, out FAArmorInfo armorInfo))
        {
            Notify(byPlayer, "The hemming crane has no armor.");
            return;
        }

        ITreeAttribute? types = trimArmorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            Notify(byPlayer, "This armor piece cannot be trimmed.");
            return;
        }

        string stripKey = "strip" + armorInfo.Piece;
        if (pendingTrimRivetsStack != null && !IsTrimValueSet(pendingTrimOriginalStrip))
        {
            Notify(byPlayer, "Only staged rivets are on this armor. Use an empty hand to remove the preview.");
            return;
        }

        string bakedStrip = IsTrimValueSet(pendingTrimOriginalStrip)
            ? pendingTrimOriginalStrip
            : types.GetString(stripKey, "none");

        if (!IsTrimValueSet(bakedStrip))
        {
            Notify(byPlayer, "This armor has no baked rivets to remove.");
            return;
        }

        ItemStack? removedRivets = CreateRimsAndRivetsStack(bakedStrip);
        if (removedRivets == null)
        {
            Notify(byPlayer, $"Could not find rims and rivets item for {bakedStrip}.");
            return;
        }

        if (pendingTrimRivetsStack != null)
        {
            pendingTrimOriginalStrip = "none";
        }
        else
        {
            types.SetString(stripKey, "none");
        }

        GiveOrDrop(byPlayer, removedRivets, 0.9);
        PlayScrapeSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Removed {removedRivets.GetName()} from the armor.");
    }

    private void RestorePendingTrimPreview()
    {
        if (trimArmorStack == null || pendingTrimRivetsStack == null || !TryGetFAArmorInfo(trimArmorStack, out FAArmorInfo armorInfo))
        {
            return;
        }

        ITreeAttribute? types = trimArmorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return;
        }

        types.SetString("strip" + armorInfo.Piece, string.IsNullOrEmpty(pendingTrimOriginalStrip) ? "none" : pendingTrimOriginalStrip);
    }

    private void TryLightFuel(IPlayer byPlayer)
    {
        UpdateProcess();

        ItemStack? heldStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (!CanLightFuel(heldStack))
        {
            Notify(byPlayer, "Hold an igniter to light the fuel.");
            return;
        }

        if (!CanStartFuelIgnition(out string failure, out string coatingMetal, out FAArmorInfo armorInfo))
        {
            Notify(byPlayer, failure);
            return;
        }

        fuelLit = true;
        StartProcess(ProcessArmorCoating, coatingMetal);
        PlayStationSound(IgniteSound, byPlayer);
        PlayBubblingSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Fuel lit. Coating will finish in {ArmorCoatingHours:0.#} in-game hours.");
    }

    public bool CanStartFuelIgnition(IPlayer byPlayer)
    {
        UpdateProcess();

        ItemStack? heldStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        return CanLightFuel(heldStack) && CanStartFuelIgnition(out _, out _, out _);
    }

    public void CompleteFuelIgnition(IPlayer byPlayer)
    {
        TryLightFuel(byPlayer);
    }

    private bool CanStartFuelIgnition(out string failure, out string coatingMetal, out FAArmorInfo armorInfo)
    {
        failure = "";
        coatingMetal = "";
        armorInfo = null!;

        if (fuelStack == null)
        {
            failure = "Add fuel before lighting the station.";
            return false;
        }

        if (fuelStack.StackSize < MaxCharcoalPieces)
        {
            failure = $"Fill the fuel tray before lighting it ({MaxCharcoalPieces}/{MaxCharcoalPieces} fuel).";
            return false;
        }

        if (fuelLit)
        {
            failure = "The fuel is already burning.";
            return false;
        }

        if (lidOpen)
        {
            failure = "Close the cauldron lid before lighting the fuel.";
            return false;
        }

        if (processMode == ProcessPlateResting || processMode == ProcessArmorDissolving)
        {
            failure = "Wait for the acid reaction to finish before lighting the fuel.";
            return false;
        }

        if (!TryGetCoatingMetal(liquidStack, out coatingMetal))
        {
            failure = "Prepare coating liquid before lighting the fuel.";
            return false;
        }

        if (!TryGetFAArmorInfo(immersedStack, out armorInfo))
        {
            failure = "Place a coatable armor piece in the cauldron first.";
            return false;
        }

        if (!CanApplyArmorCoating(immersedStack, armorInfo, coatingMetal, out failure))
        {
            return false;
        }

        return true;
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
            NotifyInfo(byPlayer, liquidStack == null ? "The cauldron is empty." : $"Cauldron: {FormatLitres(liquidStack.StackSize)}/{LiquidCapacityLitres}L {GetLiquidName(liquidStack)}.");
            return;
        }

        if (CanTakeCauldronLiquid(activeSlot))
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
                ? "Only sulfuric acid or coating liquid can be poured into this cauldron."
                : $"That container cannot interact with {GetLiquidName(liquidStack)}.");
            return;
        }

        TryInsertImmersedItem(byPlayer, activeSlot);
    }

    private void TryPourIntoCauldron(IPlayer byPlayer, ItemSlot activeSlot)
    {
        if (HasActiveProcess() || fuelLit)
        {
            Notify(byPlayer, "The cauldron is already processing.");
            return;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "Remove the immersed item before changing the liquid.");
            return;
        }

        int missing = LiquidCapacityItems - (liquidStack?.StackSize ?? 0);
        if (missing <= 0)
        {
            Notify(byPlayer, liquidStack == null
                ? "The cauldron is already full."
                : $"The cauldron already contains {LiquidCapacityLitres}L of {GetLiquidName(liquidStack)}.");
            return;
        }

        if (!TryTakeCauldronLiquid(activeSlot, missing, out ItemStack pourStack, out string failure))
        {
            Notify(byPlayer, failure);
            return;
        }

        int inserted = InsertLiquid(pourStack);
        activeSlot.MarkDirty();
        PlayStationSound(WaterPourSound, byPlayer);
        MarkStationDirty();
        DebugLiquidLog($"TryPourIntoCauldron inserted={inserted}, liquidStack={FormatStackDebug(liquidStack)}, heldAfter={FormatStackDebug(activeSlot.Itemstack)}");
        NotifyInfo(byPlayer, $"Added {FormatLitres(inserted)}L {GetLiquidName(pourStack)} ({FormatLitres(liquidStack?.StackSize ?? 0)}/{LiquidCapacityLitres}L).");
    }

    private void TryTakeFromCauldron(IPlayer byPlayer, ItemSlot activeSlot)
    {
        UpdateProcess();

        if (HasActiveProcess() || fuelLit)
        {
            Notify(byPlayer, "The cauldron is already processing.");
            return;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "Remove the immersed item before draining the cauldron.");
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
            Notify(byPlayer, $"That container cannot hold {GetLiquidName(liquidStack)}.");
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
        NotifyInfo(byPlayer, $"Removed {FormatLitres(removed)}L {GetLiquidName(moveStack)} ({FormatLitres(liquidStack?.StackSize ?? 0)}/{LiquidCapacityLitres}L remaining).");
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
            NotifyInfo(byPlayer, GetProcessStatusText());
            return;
        }

        if (fuelLit)
        {
            Notify(byPlayer, "Wait for the cauldron to cool first.");
            return;
        }

        if (TryStartPlateRest(byPlayer, activeSlot))
        {
            return;
        }

        if (TryStartArmorDissolve(byPlayer, activeSlot))
        {
            return;
        }

        if (TryInsertArmorForCoating(byPlayer, activeSlot))
        {
            return;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "The cauldron already contains an item.");
            return;
        }

        Notify(byPlayer, "Use sulfuric acid with a metal plate or coated armor, or use coating liquid with uncoated armor.");
    }

    private void TryTakeImmersedItem(IPlayer byPlayer)
    {
        UpdateProcess();

        if (HasActiveProcess() || fuelLit)
        {
            Notify(byPlayer, "Wait for the cauldron to cool first.");
            return;
        }

        if (immersedStack == null)
        {
            Notify(byPlayer, "There is no item in the cauldron.");
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
        NotifyInfo(byPlayer, $"Removed {takeStack.GetName()} from the cauldron.");
    }

    private bool TryTakeCauldronLiquid(ItemSlot activeSlot, int maxItems, out ItemStack pourStack, out string failure)
    {
        pourStack = null!;
        failure = "Hold sulfuric acid or coating liquid over the cauldron.";

        ItemStack? heldStack = activeSlot.Itemstack;
        if (heldStack == null)
        {
            return false;
        }

        if (IsCauldronLiquid(heldStack))
        {
            if (!CanAddLiquidToCauldron(heldStack, out failure))
            {
                return false;
            }

            int amount = Math.Min(maxItems, heldStack.StackSize);
            pourStack = heldStack.Clone();
            pourStack.StackSize = amount;
            activeSlot.TakeOut(amount);
            return true;
        }

        if (heldStack.Collectible is not ILiquidSource source || heldStack.Collectible is not ILiquidInterface liquidInterface)
        {
            return false;
        }

        ItemStack? contentStack = liquidInterface.GetContent(heldStack);
        if (!IsCauldronLiquid(contentStack))
        {
            failure = "Only sulfuric acid or coating liquid can be poured into this cauldron.";
            return false;
        }

        if (!CanAddLiquidToCauldron(contentStack!, out failure))
        {
            return false;
        }

        int amountToTake = Math.Min(maxItems, contentStack!.StackSize);
        if (amountToTake <= 0)
        {
            return false;
        }

        ItemStack? takenStack = source.TryTakeContent(heldStack, amountToTake);
        if (!IsCauldronLiquid(takenStack))
        {
            failure = "That liquid could not be poured from the container.";
            return false;
        }

        pourStack = takenStack;
        return true;
    }

    private bool CanTakeCauldronLiquid(ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (IsCauldronLiquid(heldStack))
        {
            return true;
        }

        return heldStack?.Collectible is ILiquidInterface liquidInterface && IsCauldronLiquid(liquidInterface.GetContent(heldStack));
    }

    private bool CanAddLiquidToCauldron(ItemStack pourStack, out string failure)
    {
        failure = "";
        if (liquidStack == null)
        {
            return true;
        }

        if (pourStack.Equals(Api.World, liquidStack, GlobalConstants.IgnoredStackAttributes))
        {
            return true;
        }

        failure = $"The cauldron already contains {GetLiquidName(liquidStack)}.";
        return false;
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

    private int InsertLiquid(ItemStack pourStack)
    {
        int amount = Math.Min(LiquidCapacityItems - (liquidStack?.StackSize ?? 0), pourStack.StackSize);
        if (liquidStack == null)
        {
            liquidStack = pourStack.Clone();
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
            Notify(byPlayer, "Remove the immersed item before adding a plate.");
            return true;
        }

        if ((liquidStack?.StackSize ?? 0) < LiquidCapacityItems)
        {
            Notify(byPlayer, $"Fill the cauldron with {LiquidCapacityLitres}L of sulfuric acid before adding a metal plate.");
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
            Notify(byPlayer, $"This metal cannot be made into coating liquid: {metal}.");
            return true;
        }

        if (!HasAnyFAArmorCoverTexture(metal))
        {
            Notify(byPlayer, $"No loaded armor set supports {metal} coating.");
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
        NotifyInfo(byPlayer, $"{immersedStack.GetName()} added. Reaction time: {PlateRestHours:0.#} in-game hours.");
        return true;
    }

    private bool TryInsertArmorForCoating(IPlayer byPlayer, ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (!TryGetFAArmorInfo(heldStack, out FAArmorInfo armorInfo))
        {
            return false;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "The cauldron already contains an item.");
            return true;
        }

        if (!TryGetCoatingMetal(liquidStack, out string metal))
        {
            Notify(byPlayer, "Prepare coating liquid before adding armor.");
            return true;
        }

        if (!CanApplyArmorCoating(heldStack, armorInfo, metal, out string failure))
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
        NotifyInfo(byPlayer, $"{immersedStack.GetName()} added. Close the lid and light a full fuel tray.");
        return true;
    }

    private bool TryStartArmorDissolve(IPlayer byPlayer, ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (!TryGetFAArmorInfo(heldStack, out FAArmorInfo armorInfo))
        {
            return false;
        }

        if (!TryGetArmorCover(heldStack, armorInfo, out string coverMetal) || coverMetal == "none")
        {
            return false;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, "The cauldron already contains an item.");
            return true;
        }

        if ((liquidStack?.StackSize ?? 0) < LiquidCapacityItems)
        {
            Notify(byPlayer, $"Fill the cauldron with {LiquidCapacityLitres}L of sulfuric acid before dissolving coating.");
            return true;
        }

        if (!IsSulfuricAcid(liquidStack))
        {
            Notify(byPlayer, $"The cauldron already contains {GetLiquidName(liquidStack)}.");
            return true;
        }

        Item? coatingItem = Api.World.GetItem(CoatingLiquidCode(coverMetal));
        if (coatingItem == null)
        {
            Notify(byPlayer, $"This coating cannot be recovered as liquid: {coverMetal}.");
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
        StartProcess(ProcessArmorDissolving, coverMetal);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Coated armor added. Dissolving {coverMetal} coating takes {ArmorDissolveHours:0.#} in-game hours.");
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
        if (TryGetFAArmorInfo(stack, out FAArmorInfo armorInfo))
        {
            piece = armorInfo.Piece;
            return true;
        }

        piece = "";
        return false;
    }

    private static bool TryGetFAArmorInfo(ItemStack? stack, out FAArmorInfo armorInfo)
    {
        armorInfo = null!;

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

        string path = code.Path;
        if (TryParseFAArmorCodePath(path, out string piece, out string slotPrefix, out string style, out string baseMetal))
        {
            string typedBase = types.GetString("base" + piece) ?? "";
            if (!string.IsNullOrEmpty(typedBase))
            {
                baseMetal = typedBase;
            }

            armorInfo = new FAArmorInfo(code.Domain, GetArmorFamily(code.Domain), piece, slotPrefix, style, baseMetal);
            return true;
        }

        if (TryInferFAArmorPieceFromTypes(types, out piece, out baseMetal))
        {
            style = types.GetString("form" + piece, "") ?? "";
            armorInfo = new FAArmorInfo(code.Domain, GetArmorFamily(code.Domain), piece, "plate" + piece, style, baseMetal);
            return true;
        }

        return false;
    }

    private static bool TryParseFAArmorCodePath(string path, out string piece, out string slotPrefix, out string style, out string baseMetal)
    {
        piece = "";
        slotPrefix = "";
        style = "";
        baseMetal = "";

        if (TryParseFAArmorCodePath(path, "platehead", "head", out piece, out slotPrefix, out style, out baseMetal)
            || TryParseFAArmorCodePath(path, "platebody", "body", out piece, out slotPrefix, out style, out baseMetal)
            || TryParseFAArmorCodePath(path, "platelegs", "legs", out piece, out slotPrefix, out style, out baseMetal))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseFAArmorCodePath(string path, string expectedPrefix, string expectedPiece, out string piece, out string slotPrefix, out string style, out string baseMetal)
    {
        piece = "";
        slotPrefix = "";
        style = "";
        baseMetal = "";

        string prefix = expectedPrefix + "-";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string remainder = path[prefix.Length..];
        int lastDash = remainder.LastIndexOf('-');
        if (lastDash <= 0 || lastDash >= remainder.Length - 1)
        {
            return false;
        }

        piece = expectedPiece;
        slotPrefix = expectedPrefix;
        style = remainder[..lastDash];
        baseMetal = remainder[(lastDash + 1)..];
        return true;
    }

    private static bool TryInferFAArmorPieceFromTypes(ITreeAttribute types, out string piece, out string baseMetal)
    {
        piece = "";
        baseMetal = "";

        if (!string.IsNullOrEmpty(types.GetString("basehead")))
        {
            piece = "head";
            baseMetal = types.GetString("basehead") ?? "";
            return true;
        }

        if (!string.IsNullOrEmpty(types.GetString("basebody")))
        {
            piece = "body";
            baseMetal = types.GetString("basebody") ?? "";
            return true;
        }

        if (!string.IsNullOrEmpty(types.GetString("baselegs")))
        {
            piece = "legs";
            baseMetal = types.GetString("baselegs") ?? "";
            return true;
        }

        return false;
    }

    private static string GetArmorFamily(string domain)
    {
        return domain.StartsWith("fa", StringComparison.Ordinal) && domain.Length > 2
            ? domain[2..]
            : domain;
    }

    private bool CanApplyArmorCoating(ItemStack? armorStack, FAArmorInfo armorInfo, string metal, out string failure)
    {
        failure = "";

        ITreeAttribute? types = armorStack?.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            failure = "This armor piece cannot be coated.";
            return false;
        }

        string coverKey = "cover" + armorInfo.Piece;
        string currentCover = types.GetString(coverKey) ?? "none";
        if (!string.Equals(currentCover, "none", StringComparison.Ordinal))
        {
            failure = $"This armor already has {currentCover} coating.";
            return false;
        }

        if (!TryResolveFAArmorPlateTexture(armorInfo, metal, out _))
        {
            failure = $"This armor set does not support {metal} coating.";
            return false;
        }

        return true;
    }

    private static void ApplyArmorCoating(ItemStack armorStack, FAArmorInfo armorInfo, string metal)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return;
        }

        types.SetString("cover" + armorInfo.Piece, metal);
    }

    private static bool TryGetArmorCover(ItemStack? armorStack, FAArmorInfo armorInfo, out string coverMetal)
    {
        coverMetal = "";
        ITreeAttribute? types = armorStack?.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return false;
        }

        coverMetal = types.GetString("cover" + armorInfo.Piece) ?? "none";
        return !string.IsNullOrEmpty(coverMetal);
    }

    private static void RemoveArmorCoating(ItemStack armorStack, FAArmorInfo armorInfo)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return;
        }

        types.SetString("cover" + armorInfo.Piece, "none");
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
            return;
        }

        if (processMode == ProcessArmorDissolving)
        {
            CompleteArmorDissolve();
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
            ProcessArmorDissolving => ArmorDissolveHours,
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
        string label = processMode switch
        {
            ProcessPlateResting => "Plate reacting",
            ProcessArmorDissolving => "Armor coating dissolving",
            _ => "Armor coating"
        };
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
            || !TryGetFAArmorInfo(immersedStack, out FAArmorInfo armorInfo)
            || !CanApplyArmorCoating(immersedStack, armorInfo, metal, out _))
        {
            fuelLit = false;
            ClearProcess();
            MarkStationDirty();
            return;
        }

        ApplyArmorCoating(immersedStack, armorInfo, metal);
        liquidStack = null;
        fuelStack = null;
        fuelLit = false;
        ClearProcess();
        MarkStationDirty();
    }

    private void CompleteArmorDissolve()
    {
        if (immersedStack == null
            || !TryGetFAArmorInfo(immersedStack, out FAArmorInfo armorInfo)
            || !TryGetArmorCover(immersedStack, armorInfo, out string coverMetal)
            || coverMetal != processMetal
            || coverMetal == "none"
            || liquidStack == null
            || !IsSulfuricAcid(liquidStack))
        {
            ClearProcess();
            MarkStationDirty();
            return;
        }

        Item? coatingItem = Api.World.GetItem(CoatingLiquidCode(coverMetal));
        if (coatingItem == null)
        {
            ClearProcess();
            MarkStationDirty();
            return;
        }

        int liquidAmount = liquidStack.StackSize;
        RemoveArmorCoating(immersedStack, armorInfo);
        liquidStack = new ItemStack(coatingItem, liquidAmount);
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

    private MeshData? TryCreateTableItemMesh(ITesselatorAPI tessThreadTesselator)
    {
        try
        {
            return CreateTableItemMesh(tessThreadTesselator);
        }
        catch (Exception exception)
        {
            Api?.Logger.Warning("[FACore CoverStation] Could not render table item {0}: {1}", FormatStackDebug(tableStack), exception);
            return null;
        }
    }

    private MeshData? CreateTableItemMesh(ITesselatorAPI tessThreadTesselator)
    {
        if (tableStack?.Collectible == null)
        {
            return null;
        }

        MeshData? mesh = CreateItemStackMesh(tessThreadTesselator, tableStack, "facore-coverstation-tableitem");
        if (mesh == null)
        {
            return null;
        }

        AlignTableItemMesh(mesh, ShouldRotateTableItem(tableStack));
        return mesh;
    }

    private MeshData? CreateItemStackMesh(ITesselatorAPI tessThreadTesselator, ItemStack stack, string shapeName, bool applyBlockRotation = true)
    {
        if (Api is not ICoreClientAPI capi || stack.Collectible == null)
        {
            return null;
        }

        MeshData mesh;
        if (stack.Item != null)
        {
            ITexPositionSource fallbackTextureSource = capi.Tesselator.GetTextureSource(Block, 0, false);
            if (!TryResolveImmersedShapeAndTextures(
                    capi,
                    stack.Item,
                    stack,
                    fallbackTextureSource,
                    out Shape? shape,
                    out ITexPositionSource textureSource,
                    out _
                ))
            {
                return null;
            }

            tessThreadTesselator.TesselateShape(
                shapeName,
                shape,
                out mesh,
                textureSource,
                applyBlockRotation ? new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ) : new Vec3f()
            );
        }
        else if (stack.Block != null)
        {
            tessThreadTesselator.TesselateBlock(stack.Block, out mesh);
        }
        else
        {
            return null;
        }

        if (mesh == null || mesh.VerticesCount <= 0)
        {
            return null;
        }

        ForceOpaqueRenderPass(mesh);
        return mesh;
    }

    private void AddDecorationStationMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        List<StationElementZone> zones = StationShapeElementReader.LoadElementZones(Api, Block);

        AddDecorationSlotMesh(mesher, tessThreadTesselator, zones, "CupboardSlot1", decorationKitHeadStack, scaleFactor: DecorationShelfItemScale, yawDegrees: DecorationShelfItemYawDegrees);
        AddDecorationSlotMesh(mesher, tessThreadTesselator, zones, "CupboardSlot2", ornamentsStack, scaleFactor: DecorationShelfItemScale, yawDegrees: DecorationShelfItemYawDegrees);
        AddDecorationSlotMesh(mesher, tessThreadTesselator, zones, "CupboardSlot3", decorationKitBodyStack, scaleFactor: DecorationShelfItemScale, yawDegrees: DecorationShelfItemYawDegrees);
        AddDecorationSlotMesh(mesher, tessThreadTesselator, zones, "CupboardSlot4", bracketsStack, scaleFactor: DecorationShelfItemScale, yawDegrees: DecorationShelfItemYawDegrees);
        AddDecorationSlotMesh(mesher, tessThreadTesselator, zones, "CupboardSlot5", decorationKitLegsStack, scaleFactor: DecorationShelfItemScale, yawDegrees: DecorationShelfItemYawDegrees);
        AddDecorationSlotMesh(mesher, tessThreadTesselator, zones, "CupboardSlot6", fastenersStack, scaleFactor: DecorationShelfItemScale, yawDegrees: DecorationShelfItemYawDegrees);

        AddArmorStandMeshes(mesher, tessThreadTesselator, zones);
        AddDecorationClothMeshes(mesher, tessThreadTesselator, zones);
    }

    private void AddDecorationClothMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, List<StationElementZone> zones)
    {
        if (decorationClothStacks.Count == 0 || !TryGetDecorationZoneBox(zones, "MordantLinenStack", out Cuboidf zoneBox))
        {
            return;
        }

        int visibleClothStacks = Math.Min(DecorationVisibleClothStacks, decorationClothStacks.Count);
        for (int i = 0; i < visibleClothStacks; i++)
        {
            ItemStack clothStack = decorationClothStacks[i];
            MeshData? mesh;
            try
            {
                mesh = CreateItemStackMesh(
                    tessThreadTesselator,
                    clothStack,
                    $"facore-decorationstation-cloth-{i}"
                );
            }
            catch (Exception exception)
            {
                Api?.Logger.Warning("[FACore DecorationStation] Could not render cloth {0}: {1}", FormatStackDebug(clothStack), exception);
                continue;
            }

            if (mesh == null)
            {
                continue;
            }

            ApplyGroundTransformOrientation(mesh, clothStack.Collectible.GroundTransform);
            AlignDecorationClothMesh(mesh, zoneBox, i);
            mesher.AddMeshData(mesh, 1);
        }
    }

    private void AddTrimStationMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        List<StationElementZone> zones = StationShapeElementReader.LoadElementZones(Api, Block);

        if (trimArmorStack != null)
        {
            AddArmorStandMeshes(
                mesher,
                tessThreadTesselator,
                zones,
                [trimArmorStack],
                ["Armor"],
                "facore-trimstation-armor",
                value => trimArmorRenderDebug = value,
                TrimArmorExtraDropY,
                TrimArmorScale,
                centerLateralToTarget: true
            );
        }
        else
        {
            trimArmorRenderDebug = "no armor";
        }

        ItemStack? visibleRivets = pendingTrimRivetsStack ?? trimRivetsStack;
        TrimDebugLog($"Render RivetsPlace stack={FormatStackDebug(visibleRivets)} scale={TrimRivetsBowlItemScale}");
        AddDecorationSlotMesh(mesher, tessThreadTesselator, zones, "RivetsPlace", visibleRivets, scaleFactor: TrimRivetsBowlItemScale);
        AddDecorationSlotMesh(
            mesher,
            tessThreadTesselator,
            zones,
            "CruciblePlace",
            trimCrucibleStack,
            yOffset: TrimCrucibleItemYOffset,
            scaleFactor: TrimCrucibleItemScale,
            yawDegrees: TrimCrucibleItemYawDegrees
        );
        AddSolderingIronHolderMesh(mesher, tessThreadTesselator, zones);
    }

    private void AddSolderingIronHolderMesh(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, List<StationElementZone> zones)
    {
        if (trimSolderingIronStack?.Collectible == null || !TryGetDecorationZoneBox(zones, "SolderHolder", out Cuboidf zoneBox))
        {
            return;
        }

        MeshData? mesh;
        try
        {
            mesh = CreateItemStackMesh(tessThreadTesselator, trimSolderingIronStack, "facore-trimstation-solderholder");
        }
        catch (Exception exception)
        {
            Api?.Logger.Warning("[FACore TrimStation] Could not render soldering iron holder item {0}: {1}", FormatStackDebug(trimSolderingIronStack), exception);
            return;
        }

        if (mesh == null)
        {
            return;
        }

        ApplyGroundTransformOrientation(mesh, trimSolderingIronStack.Collectible.GroundTransform);
        AlignVerticalHolderMesh(mesh, zoneBox, TrimSolderingIronItemScale);
        mesher.AddMeshData(mesh, 1);
    }

    private void AddDecorationSlotMesh(
        ITerrainMeshPool mesher,
        ITesselatorAPI tessThreadTesselator,
        List<StationElementZone> zones,
        string actionName,
        ItemStack? stack,
        float yOffset = 0f,
        float scaleFactor = 1f,
        float yawDegrees = 0f)
    {
        if (stack?.Collectible == null || !TryGetDecorationZoneBox(zones, actionName, out Cuboidf zoneBox))
        {
            return;
        }

        MeshData? mesh;
        try
        {
            mesh = CreateItemStackMesh(
                tessThreadTesselator,
                stack,
                "facore-decorationstation-" + actionName.ToLowerInvariant()
            );
        }
        catch (Exception exception)
        {
            Api?.Logger.Warning("[FACore DecorationStation] Could not render {0} in {1}: {2}", FormatStackDebug(stack), actionName, exception);
            return;
        }

        if (mesh == null)
        {
            return;
        }

        ApplyGroundTransformOrientation(mesh, stack.Collectible.GroundTransform);
        AlignDecorationSlotMesh(mesh, zoneBox, yOffset, scaleFactor, yawDegrees);
        mesher.AddMeshData(mesh, 1);
    }

    private static void ApplyGroundTransformOrientation(MeshData mesh, ModelTransform? transform)
    {
        if (transform == null)
        {
            return;
        }

        var origin = new Vec3f(transform.Origin.X, transform.Origin.Y, transform.Origin.Z);
        mesh.Rotate(
            origin,
            transform.Rotation.X * GameMath.DEG2RAD,
            transform.Rotation.Y * GameMath.DEG2RAD,
            transform.Rotation.Z * GameMath.DEG2RAD
        );
    }

    private static bool TryGetDecorationZoneBox(List<StationElementZone> zones, string actionName, out Cuboidf zoneBox)
    {
        foreach (StationElementZone zone in zones)
        {
            if (zone.ActionName == actionName)
            {
                zoneBox = zone.StationBox;
                return true;
            }
        }

        zoneBox = Cuboidf.Default();
        return false;
    }

    private void AlignDecorationSlotMesh(MeshData mesh, Cuboidf zoneBox, float yOffset, float scaleFactor = 1f, float yawDegrees = 0f)
    {
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ))
        {
            return;
        }

        if (scaleFactor > 0f && Math.Abs(scaleFactor - 1f) > 0.001f)
        {
            var scaleOrigin = new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            mesh.Scale(scaleOrigin, scaleFactor, scaleFactor, scaleFactor);

            if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
            {
                return;
            }
        }

        // Yaw each item so longer pieces sit diagonally and don't clip into neighbouring slots.
        if (Math.Abs(yawDegrees) > 0.001f)
        {
            var yawOrigin = new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            mesh.Rotate(yawOrigin, 0f, yawDegrees * GameMath.DEG2RAD, 0f);

            if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
            {
                return;
            }
        }

        // Rest the item on the bottom of its selection box (shelf surface) instead of floating it centered.
        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;
        float targetCenterX = (zoneBox.X1 + zoneBox.X2) * 0.5f;
        float targetCenterZ = (zoneBox.Z1 + zoneBox.Z2) * 0.5f;
        float targetBottomY = zoneBox.Y1 + yOffset;

        mesh.Translate(
            targetCenterX - currentCenterX,
            targetBottomY - minY,
            targetCenterZ - currentCenterZ
        );
    }

    private static void AlignVerticalHolderMesh(MeshData mesh, Cuboidf zoneBox, float scaleFactor)
    {
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ))
        {
            return;
        }

        var rotateOrigin = new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        if (sizeX >= sizeZ)
        {
            mesh.Rotate(rotateOrigin, 0f, 0f, GameMath.PIHALF);
        }
        else
        {
            mesh.Rotate(rotateOrigin, GameMath.PIHALF, 0f, 0f);
        }

        if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
        {
            return;
        }

        float height = maxY - minY;
        float targetHeight = Math.Max(0.01f, (zoneBox.Y2 - zoneBox.Y1) * 0.9f);
        float fitScale = height > targetHeight ? targetHeight / height : 1f;
        float finalScale = Math.Min(scaleFactor, fitScale);
        if (finalScale > 0f && Math.Abs(finalScale - 1f) > 0.001f)
        {
            var scaleOrigin = new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            mesh.Scale(scaleOrigin, finalScale, finalScale, finalScale);

            if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
            {
                return;
            }
        }

        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterY = (minY + maxY) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;
        float targetCenterX = (zoneBox.X1 + zoneBox.X2) * 0.5f;
        float targetCenterY = (zoneBox.Y1 + zoneBox.Y2) * 0.5f;
        float targetCenterZ = (zoneBox.Z1 + zoneBox.Z2) * 0.5f;

        mesh.Translate(
            targetCenterX - currentCenterX,
            targetCenterY - currentCenterY,
            targetCenterZ - currentCenterZ
        );
    }

    private void AlignDecorationClothMesh(MeshData mesh, Cuboidf zoneBox, int layer)
    {
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ))
        {
            return;
        }

        var scaleOrigin = new Vec3f((minX + maxX) * 0.5f, minY, (minZ + maxZ) * 0.5f);
        mesh.Scale(scaleOrigin, DecorationClothStackScale, DecorationClothStackScale, DecorationClothStackScale);

        if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
        {
            return;
        }

        float yawDegrees = layer switch
        {
            1 => 7f,
            2 => -8f,
            3 => 13f,
            4 => -14f,
            5 => 4f,
            _ => 0f
        };

        if (Math.Abs(yawDegrees) > 0.001f)
        {
            var yawOrigin = new Vec3f((minX + maxX) * 0.5f, minY, (minZ + maxZ) * 0.5f);
            mesh.Rotate(yawOrigin, 0f, yawDegrees * GameMath.DEG2RAD, 0f);

            if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
            {
                return;
            }
        }

        (float offsetX, float offsetZ) = layer switch
        {
            1 => (0.008f, -0.006f),
            2 => (-0.007f, 0.007f),
            3 => (0.006f, 0.009f),
            4 => (-0.009f, -0.004f),
            5 => (0.004f, -0.009f),
            _ => (0f, 0f)
        };

        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;
        float targetCenterX = (zoneBox.X1 + zoneBox.X2) * 0.5f + offsetX;
        float targetCenterZ = (zoneBox.Z1 + zoneBox.Z2) * 0.5f + offsetZ;
        float targetBottomY = zoneBox.Y1 + layer * DecorationClothLayerYOffset;

        mesh.Translate(
            targetCenterX - currentCenterX,
            targetBottomY - minY,
            targetCenterZ - currentCenterZ
        );
    }

    private void AddArmorStandMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, List<StationElementZone> zones)
    {
        var pieces = new List<ItemStack>();
        if (decorationHelmetStack != null) pieces.Add(decorationHelmetStack);
        if (decorationBodyStack != null) pieces.Add(decorationBodyStack);
        if (decorationLegsStack != null) pieces.Add(decorationLegsStack);
        AddArmorStandMeshes(mesher, tessThreadTesselator, zones, pieces, ["HelmetDeco", "BodyDeco", "LegsDeco"], "facore-decorationstation-armorstand", value => armorStandRenderDebug = value);
    }

    private void AddArmorStandMeshes(
        ITerrainMeshPool mesher,
        ITesselatorAPI tessThreadTesselator,
        List<StationElementZone> zones,
        List<ItemStack> pieces,
        string[] targetZoneNames,
        string shapeName,
        Action<string> setRenderDebug,
        float extraDropY = 0f,
        float armorScale = 1f,
        bool centerLateralToTarget = false)
    {
        if (Api is not ICoreClientAPI capi)
        {
            return;
        }

        if (pieces.Count == 0)
        {
            setRenderDebug("no pieces");
            return;
        }

        Shape? standShape = Shape.TryGet(Api, ArmorStandShapeLocation)?.Clone();
        if (standShape == null)
        {
            setRenderDebug("BASE SHAPE NOT FOUND");
            Api?.Logger.Warning("[FACore ArmorStand] Base shape not found: {0}", ArmorStandShapeLocation);
            return;
        }

        // The vanilla armor stand shape carries both the wooden stand and a seraph skeleton; keep only the
        // seraph joints (the decoration station draws its own stand) so the armor frames cleanly.
        KeepOnlySeraphJoints(standShape);
        standShape.ResolveReferences(Api.Logger, ArmorStandShapeLocation.ToShortString());
        if (!TryGetArmorStandFrameBounds(tessThreadTesselator, capi, standShape, out MeshBounds frameBounds, out MeshData frameMesh))
        {
            setRenderDebug("BASE FRAME EMPTY");
            Api?.Logger.Warning("[FACore ArmorStand] Base frame tesselated empty: {0}", ArmorStandShapeLocation);
            return;
        }

        var armorTextures = new Dictionary<string, CompositeTexture>(StringComparer.Ordinal);
        foreach (string hiddenCode in ArmorStandHiddenTextureCodes)
        {
            armorTextures[hiddenCode] = TransparentComposite();
        }

        int mergedCount = 0;
        foreach (ItemStack stack in pieces)
        {
            if (TryMergeArmorStandPiece(capi, stack, standShape, armorTextures))
            {
                mergedCount++;
            }
        }

        if (mergedCount == 0)
        {
            setRenderDebug($"{pieces.Count} stored, 0 merged");
            Api?.Logger.Notification("[FACore ArmorStand] {0} piece(s) stored but none merged onto the stand.", pieces.Count);
            return;
        }

        ITexPositionSource fallback = capi.Tesselator.GetTextureSource(Block, 0, false);
        var textureSource = new CompositeBlockAtlasTextureSource(capi, new TransparentTextureSource(capi, fallback.AtlasSize), armorTextures);

        try
        {
            tessThreadTesselator.TesselateShape(
                shapeName,
                standShape,
                out MeshData mesh,
                textureSource,
                new Vec3f()
            );

            if (mesh == null || mesh.VerticesCount <= 0)
            {
                setRenderDebug($"merged={mergedCount}, EMPTY MESH");
                Api?.Logger.Notification("[FACore ArmorStand] Tesselated mesh was empty (merged={0}).", mergedCount);
                return;
            }

            ForceOpaqueRenderPass(mesh);
            string preBounds = FormatMeshBounds(mesh);
            Api?.Logger.Notification("[FACore ArmorStand] merged={0}, meshVerts={1}, bounds={2}, frameBounds={3}", mergedCount, mesh.VerticesCount, preBounds, frameBounds);
            AlignArmorStandMesh(mesh, zones, frameBounds, frameMesh, targetZoneNames, extraDropY, armorScale, centerLateralToTarget);
            mesher.AddMeshData(mesh, 1);
            setRenderDebug($"merged={mergedCount} verts={mesh.VerticesCount} placed={FormatMeshBounds(mesh)}");
        }
        catch (Exception exception)
        {
            setRenderDebug("EXCEPTION: " + exception.Message);
            Api?.Logger.Warning("[FACore ArmorStand] Could not render armor stand: {0}", exception);
        }
    }

    private bool TryGetArmorStandFrameBounds(ITesselatorAPI tessThreadTesselator, ICoreClientAPI capi, Shape standShape, out MeshBounds bounds, out MeshData frameMesh)
    {
        bounds = default;
        frameMesh = null!;

        ITexPositionSource fallback = capi.Tesselator.GetTextureSource(Block, 0, false);
        var textureSource = new TransparentTextureSource(capi, fallback.AtlasSize);

        tessThreadTesselator.TesselateShape(
            "facore-decorationstation-armorstand-frame",
            standShape,
            out frameMesh,
            textureSource,
            new Vec3f()
        );

        return TryGetMeshBounds(frameMesh, out bounds);
    }

    private static void KeepOnlySeraphJoints(Shape standShape)
    {
        ShapeElement[]? roots = standShape.Elements;
        if (roots == null)
        {
            return;
        }

        foreach (ShapeElement root in roots)
        {
            if (root.Children == null)
            {
                continue;
            }

            var kept = new List<ShapeElement>();
            foreach (ShapeElement child in root.Children)
            {
                if (child.Name == "LowerTorso")
                {
                    kept.Add(child);
                }
            }

            if (kept.Count > 0 && kept.Count != root.Children.Length)
            {
                root.Children = kept.ToArray();
            }
        }
    }

    private bool TryMergeArmorStandPiece(ICoreClientAPI capi, ItemStack stack, Shape standShape, Dictionary<string, CompositeTexture> armorTextures)
    {
        if (!TryGetFAArmorInfo(stack, out FAArmorInfo armorInfo))
        {
            return false;
        }

        ITreeAttribute? types = stack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return false;
        }

        string piece = armorInfo.Piece;
        string decoration = types.GetString("decoration" + piece, "none");
        string coverMetal = types.GetString("cover" + piece, "none");
        string stripMetal = types.GetString("strip" + piece, "none");
        string color = types.GetString("color" + piece, "none");

        if (!TryResolveFAArmorShape(armorInfo, decoration, types.GetString("form" + piece, "") ?? "", out Shape? armorShape, out AssetLocation shapeLocation)
            || armorShape == null)
        {
            return false;
        }

        if (!TryResolveFAArmorPlateTexture(armorInfo, coverMetal, out AssetLocation plateTexture))
        {
            return false;
        }

        armorTextures["base" + piece] = new CompositeTexture(plateTexture) { Alpha = 255 };
        armorTextures["strip" + piece] = new CompositeTexture(new AssetLocation("facore", $"armor/entity/trim/{stripMetal}")) { Alpha = 255 };
        armorTextures["color" + piece] = GetFAArmorDecorationTexture(piece, decoration, color);

        // Reparent the worn-armor entity shape onto the vanilla armor stand's seraph joints so the
        // pieces assemble into a standing figure instead of piling up at the origin. Mirror the engine's
        // gear-attach recipe (VSEssentials addGearToShape): subclass + resolve the child before merging,
        // otherwise its faces never resolve and it tesselates to nothing.
        Shape childShape = armorShape.Clone();
        bool merged;
        try
        {
            childShape.SubclassForStepParenting("", 0f);
            childShape.ResolveReferences(Api.Logger, shapeLocation.ToShortString());
            merged = standShape.StepParentShape(
                childShape,
                shapeLocation.ToShortString(),
                ArmorStandShapeLocation.ToShortString(),
                Api.Logger,
                (textureCode, textureLocation) =>
                {
                    if (!armorTextures.ContainsKey(textureCode))
                    {
                        armorTextures[textureCode] = new CompositeTexture(textureLocation) { Alpha = 255 };
                    }
                }
            );
        }
        catch (Exception exception)
        {
            Api?.Logger.Warning("[FACore ArmorStand] StepParentShape failed for {0} ({1}): {2}", piece, shapeLocation, exception);
            return false;
        }

        Api?.Logger.Notification("[FACore ArmorStand] merge piece={0} shape={1} stepParented={2}", piece, shapeLocation, merged);
        return merged;
    }

    private void AlignArmorStandMesh(
        MeshData mesh,
        List<StationElementZone> zones,
        MeshBounds frameBounds,
        MeshData frameMesh,
        string[] targetZoneNames,
        float extraDropY,
        float armorScale,
        bool centerLateralToTarget)
    {
        if (!frameBounds.IsValid)
        {
            return;
        }

        if (!TryGetArmorStandTargetRegion(zones, targetZoneNames, out float targetCenterX, out float targetCenterZ, out float targetBottomY, out float targetTopY))
        {
            Api?.Logger.Notification("[FACore ArmorStand] No target zones found ({0}); skipping placement.", string.Join(",", targetZoneNames));
            return;
        }

        Api?.Logger.Notification("[FACore ArmorStand] target center=({0:0.##},{1:0.##}) y={2:0.##}..{3:0.##} side={4}; frameBounds={5}; figureBounds={6}", targetCenterX, targetCenterZ, targetBottomY, targetTopY, GetStationSideCode(), frameBounds, FormatMeshBounds(mesh));

        var origin = new Vec3f(frameBounds.CenterX, frameBounds.CenterY, frameBounds.CenterZ);

        if (armorScale > 0f && Math.Abs(armorScale - 1f) > 0.001f)
        {
            mesh.Scale(origin, armorScale, armorScale, armorScale);
            frameMesh.Scale(origin, armorScale, armorScale, armorScale);
        }

        mesh.Rotate(origin, 0f, GetArmorStandFacingRadians(), 0f);
        frameMesh.Rotate(origin, 0f, GetArmorStandFacingRadians(), 0f);
        if (!TryGetMeshBounds(frameMesh, out MeshBounds transformedFrameBounds))
        {
            return;
        }

        if (!TryGetMeshBounds(mesh, out MeshBounds transformedMeshBounds))
        {
            return;
        }

        (float proxyOffsetX, float proxyOffsetZ) = GetArmorStandProxyNudge();
        (float forwardOffsetX, float forwardOffsetZ) = GetArmorStandForwardNudge();
        float placementCenterX = proxyOffsetX == 0f ? 0.5f : targetCenterX + proxyOffsetX;
        float placementCenterZ = proxyOffsetZ == 0f ? 0.5f : targetCenterZ + proxyOffsetZ;
        float translateX = placementCenterX + forwardOffsetX - transformedFrameBounds.CenterX;
        float translateZ = placementCenterZ + forwardOffsetZ - transformedFrameBounds.CenterZ;
        if (centerLateralToTarget)
        {
            if (Math.Abs(forwardOffsetX) > Math.Abs(forwardOffsetZ))
            {
                translateZ = targetCenterZ - transformedMeshBounds.CenterZ;
            }
            else
            {
                translateX = targetCenterX - transformedMeshBounds.CenterX;
            }
        }

        mesh.Translate(translateX, targetBottomY - ArmorStandDropY - extraDropY - transformedFrameBounds.MinY, translateZ);
    }

    private static bool TryGetArmorStandTargetRegion(List<StationElementZone> zones, string[] targetZoneNames, out float centerX, out float centerZ, out float bottomY, out float topY)
    {
        centerX = centerZ = bottomY = topY = 0f;

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        bool any = false;

        foreach (string name in targetZoneNames)
        {
            if (!TryGetDecorationZoneBox(zones, name, out Cuboidf box))
            {
                continue;
            }

            any = true;
            minY = Math.Min(minY, box.Y1);
            maxY = Math.Max(maxY, box.Y2);
        }

        if (!any)
        {
            return false;
        }

        // Anchor the standing figure horizontally on the legs/feet marker (the standing axis) so the bulky
        // chest zone doesn't drag it sideways. Fall back to body, then helmet.
        Cuboidf anchor;
        if (Array.IndexOf(targetZoneNames, "LegsDeco") >= 0 && TryGetDecorationZoneBox(zones, "LegsDeco", out Cuboidf legs)) anchor = legs;
        else if (Array.IndexOf(targetZoneNames, "BodyDeco") >= 0 && TryGetDecorationZoneBox(zones, "BodyDeco", out Cuboidf body)) anchor = body;
        else _ = TryGetDecorationZoneBox(zones, targetZoneNames[0], out anchor);

        centerX = (anchor.X1 + anchor.X2) * 0.5f;
        centerZ = (anchor.Z1 + anchor.Z2) * 0.5f;
        bottomY = minY;
        topY = maxY;
        return true;
    }

    private float GetArmorStandFacingRadians()
    {
        int quarterTurns = GetStationSideCode() switch
        {
            "east" => 1,
            "south" => 2,
            "west" => 3,
            _ => 0
        };

        return ArmorStandBaseFacingRadians + quarterTurns * GameMath.PIHALF;
    }

    private (float X, float Z) GetArmorStandProxyNudge()
    {
        return GetStationSideCode() switch
        {
            "east" => (0f, ArmorStandPushFromMain),
            "south" => (-ArmorStandPushFromMain, 0f),
            "west" => (0f, -ArmorStandPushFromMain),
            _ => (ArmorStandPushFromMain, 0f)
        };
    }

    private (float X, float Z) GetArmorStandForwardNudge()
    {
        return GetStationSideCode() switch
        {
            "east" => (ArmorStandForwardNudge, 0f),
            "south" => (0f, ArmorStandForwardNudge),
            "west" => (-ArmorStandForwardNudge, 0f),
            _ => (0f, -ArmorStandForwardNudge)
        };
    }

    private static CompositeTexture TransparentComposite()
    {
        return new CompositeTexture(TransparentTextureLocation) { Alpha = 0 };
    }

    private static bool ShouldRotateTableItem(ItemStack stack)
    {
        return TryGetFAArmorInfo(stack, out FAArmorInfo armorInfo)
            && (armorInfo.Piece == "body" || armorInfo.Piece == "legs");
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

        if (!TryResolveFAArmorPreview(capi, stack, fallbackTextureSource, out shape, out textureSource, out shapeLocation))
        {
            return false;
        }

        return true;
    }

    private bool TryResolveFAArmorPreview(
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

        if (!TryGetFAArmorInfo(stack, out FAArmorInfo armorInfo))
        {
            return false;
        }

        ITreeAttribute? types = stack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return false;
        }

        string piece = armorInfo.Piece;
        string decoration = types.GetString("decoration" + piece, "none");
        string coverMetal = types.GetString("cover" + piece, "none");
        string stripMetal = types.GetString("strip" + piece, "none");
        string color = types.GetString("color" + piece, "none");

        if (!TryResolveFAArmorShape(armorInfo, decoration, types.GetString("form" + piece, "") ?? "", out shape, out shapeLocation))
        {
            DebugLiquidLog($"TryResolveFAArmorPreview shape not found stack={FormatStackDebug(stack)}, family={armorInfo.Family}, piece={piece}, style={armorInfo.Style}, decoration={decoration}");
            return false;
        }

        if (!TryResolveFAArmorPlateTexture(armorInfo, coverMetal, out AssetLocation plateTexture))
        {
            DebugLiquidLog($"TryResolveFAArmorPreview plate texture not found stack={FormatStackDebug(stack)}, family={armorInfo.Family}, piece={piece}, base={armorInfo.BaseMetal}, cover={coverMetal}");
            return false;
        }

        var textures = new Dictionary<string, CompositeTexture>(StringComparer.Ordinal);
        textures["base" + piece] = new CompositeTexture(plateTexture) { Alpha = 255 };
        textures["strip" + piece] = new CompositeTexture(new AssetLocation("facore", $"armor/entity/trim/{stripMetal}")) { Alpha = 255 };
        textures["color" + piece] = GetFAArmorDecorationTexture(piece, decoration, color);
        textures["seraph"] = new CompositeTexture(new AssetLocation("game", "block/transparent")) { Alpha = 0 };

        textureSource = new CompositeBlockAtlasTextureSource(capi, fallbackTextureSource, textures);
        DebugLiquidLog($"TryResolveFAArmorPreview stack={FormatStackDebug(stack)}, piece={piece}, shape={shapeLocation}, plate={plateTexture}, strip={stripMetal}, decoration={decoration}, color={color}");
        return true;
    }

    private bool TryResolveFAArmorShape(FAArmorInfo armorInfo, string decoration, string form, out Shape? shape, out AssetLocation shapeLocation)
    {
        shape = null;
        shapeLocation = null!;

        var candidates = new List<AssetLocation>();
        AddShapeCandidate(candidates, armorInfo.Domain, armorInfo.Family, form, decoration, armorInfo.Piece);
        AddShapeCandidate(candidates, armorInfo.Domain, armorInfo.Family, $"{armorInfo.SlotPrefix}/{armorInfo.Style}", decoration, armorInfo.Piece);
        AddShapeCandidate(candidates, armorInfo.Domain, armorInfo.Family, armorInfo.SlotPrefix, decoration, armorInfo.Piece);
        AddShapeCandidate(candidates, armorInfo.Domain, armorInfo.Family, armorInfo.Style, decoration, armorInfo.Piece);

        foreach (AssetLocation candidate in candidates)
        {
            shape = Shape.TryGet(Api, candidate);
            if (shape != null)
            {
                shapeLocation = candidate;
                return true;
            }
        }

        shapeLocation = candidates.Count > 0 ? candidates[0] : new AssetLocation(armorInfo.Domain, "shapes/entity/armor");
        return false;
    }

    private static void AddShapeCandidate(List<AssetLocation> candidates, string domain, string family, string folder, string decoration, string piece)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        string normalizedFolder = folder.Replace('\\', '/').Trim('/');
        if (normalizedFolder.Length == 0)
        {
            return;
        }

        AddUniqueAssetLocation(candidates, new AssetLocation(domain, $"shapes/entity/armor/{family}/{normalizedFolder}/{decoration}{piece}.json"));
    }

    private bool TryResolveFAArmorPlateTexture(FAArmorInfo armorInfo, string coverMetal, out AssetLocation textureBase)
    {
        textureBase = null!;

        var candidates = new List<AssetLocation>();
        if (string.Equals(coverMetal, "none", StringComparison.Ordinal))
        {
            AddBaseTextureCandidates(candidates, armorInfo);
        }
        else
        {
            AddCoverTextureCandidates(candidates, armorInfo, coverMetal);
        }

        foreach (AssetLocation candidate in candidates)
        {
            if (TextureAssetExists(candidate))
            {
                textureBase = candidate;
                return true;
            }
        }

        textureBase = candidates.Count > 0 ? candidates[0] : new AssetLocation(armorInfo.Domain, $"armor/entity/{armorInfo.Family}");
        return false;
    }

    private static void AddBaseTextureCandidates(List<AssetLocation> candidates, FAArmorInfo armorInfo)
    {
        string textureKind = armorInfo.TextureKind;

        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/base/{textureKind}/{armorInfo.Style}/{armorInfo.BaseMetal}");
        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/base/{textureKind}/noble/{armorInfo.BaseMetal}");
        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/base/{textureKind}/{armorInfo.BaseMetal}");
        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/base/{armorInfo.BaseMetal}");
    }

    private static void AddCoverTextureCandidates(List<AssetLocation> candidates, FAArmorInfo armorInfo, string coverMetal)
    {
        string textureKind = armorInfo.TextureKind;

        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/cover/{armorInfo.BaseMetal}/{textureKind}/{armorInfo.Style}/{coverMetal}");
        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/cover/{armorInfo.BaseMetal}/{textureKind}/noble/{coverMetal}");
        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/cover/{armorInfo.BaseMetal}/{textureKind}/{coverMetal}");
        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/cover/{armorInfo.BaseMetal}/{coverMetal}");
        AddTextureCandidate(candidates, armorInfo.Domain, $"armor/entity/{armorInfo.Family}/cover/{coverMetal}");
    }

    private static void AddTextureCandidate(List<AssetLocation> candidates, string domain, string path)
    {
        string normalizedPath = path.Replace('\\', '/').Replace("//", "/").Trim('/');
        if (normalizedPath.Contains("//", StringComparison.Ordinal))
        {
            return;
        }

        AddUniqueAssetLocation(candidates, new AssetLocation(domain, normalizedPath));
    }

    private bool TextureAssetExists(AssetLocation textureBase)
    {
        return Api?.Assets.TryGet(new AssetLocation(textureBase.Domain, $"textures/{textureBase.Path}.png")) != null;
    }

    private bool HasAnyFAArmorCoverTexture(string coverMetal)
    {
        if (string.Equals(coverMetal, "none", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (Item item in Api.World.Items)
        {
            AssetLocation? code = item?.Code;
            if (code == null || !code.Domain.StartsWith("fa", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParseFAArmorCodePath(code.Path, out string piece, out string slotPrefix, out string style, out string baseMetal))
            {
                continue;
            }

            var armorInfo = new FAArmorInfo(code.Domain, GetArmorFamily(code.Domain), piece, slotPrefix, style, baseMetal);
            if (TryResolveFAArmorPlateTexture(armorInfo, coverMetal, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddUniqueAssetLocation(List<AssetLocation> candidates, AssetLocation candidate)
    {
        foreach (AssetLocation existing in candidates)
        {
            if (existing.Equals(candidate))
            {
                return;
            }
        }

        candidates.Add(candidate);
    }

    private static CompositeTexture GetFAArmorDecorationTexture(string piece, string decoration, string color)
    {
        if (decoration == "bear")
        {
            return new CompositeTexture(new AssetLocation("facore", $"armor/entity/bear/{color}")) { Alpha = 255 };
        }

        string textureColor = decoration != "none" && color == "none" ? "plain" : color;
        return new CompositeTexture(new AssetLocation("facore", $"block/decorations/{piece}/{textureColor}")) { Alpha = 255 };
    }

    private void AlignTableItemMesh(MeshData mesh, bool layFlat)
    {
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ))
        {
            return;
        }

        var meshOrigin = new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
        float maxDimension = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
        if (maxDimension > 0)
        {
            float scale = TableItemMaxDimension / maxDimension;
            mesh.Scale(meshOrigin, scale, scale, scale);
        }

        if (layFlat)
        {
            mesh.Rotate(meshOrigin, GameMath.PIHALF, 0f, GameMath.PI * 0.08f);
        }

        if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out _, out maxZ))
        {
            return;
        }

        (float targetCenterX, float targetCenterZ) = GetTableTargetCenter();
        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;

        mesh.Translate(
            targetCenterX - currentCenterX,
            TableTopY - minY,
            targetCenterZ - currentCenterZ
        );
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

    private (float X, float Z) GetTableTargetCenter()
    {
        float northCenterX = (TableMinX + TableMaxX) * 0.5f;
        float northCenterZ = (TableMinZ + TableMaxZ) * 0.5f;
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

    private static bool TryGetMeshBounds(MeshData mesh, out MeshBounds bounds)
    {
        bounds = default;
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ))
        {
            return false;
        }

        bounds = new MeshBounds(minX, minY, minZ, maxX, maxY, maxZ);
        return true;
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

    private void WriteDecorationTreeAttributes(ITreeAttribute tree)
    {
        SetOrRemoveItemstack(tree, "decorationKitHeadStack", decorationKitHeadStack);
        SetOrRemoveItemstack(tree, "ornamentsStack", ornamentsStack);
        SetOrRemoveItemstack(tree, "decorationKitBodyStack", decorationKitBodyStack);
        SetOrRemoveItemstack(tree, "bracketsStack", bracketsStack);
        SetOrRemoveItemstack(tree, "decorationKitLegsStack", decorationKitLegsStack);
        SetOrRemoveItemstack(tree, "fastenersStack", fastenersStack);
        SetOrRemoveItemstack(tree, "decorationHelmetStack", decorationHelmetStack);
        SetOrRemoveItemstack(tree, "decorationBodyStack", decorationBodyStack);
        SetOrRemoveItemstack(tree, "decorationLegsStack", decorationLegsStack);
        SetOrRemoveItemstack(tree, "pendingHeadDecorationStack", pendingHeadDecorationStack);
        SetOrRemoveItemstack(tree, "pendingHeadColorStack", pendingHeadColorStack);
        SetOrRemoveItemstack(tree, "pendingBodyDecorationStack", pendingBodyDecorationStack);
        SetOrRemoveItemstack(tree, "pendingBodyColorStack", pendingBodyColorStack);
        SetOrRemoveItemstack(tree, "pendingLegsDecorationStack", pendingLegsDecorationStack);
        SetOrRemoveItemstack(tree, "pendingLegsColorStack", pendingLegsColorStack);
        SetOrRemoveString(tree, "pendingHeadOriginalDecoration", pendingHeadOriginalDecoration);
        SetOrRemoveString(tree, "pendingHeadOriginalColor", pendingHeadOriginalColor);
        SetOrRemoveString(tree, "pendingBodyOriginalDecoration", pendingBodyOriginalDecoration);
        SetOrRemoveString(tree, "pendingBodyOriginalColor", pendingBodyOriginalColor);
        SetOrRemoveString(tree, "pendingLegsOriginalDecoration", pendingLegsOriginalDecoration);
        SetOrRemoveString(tree, "pendingLegsOriginalColor", pendingLegsOriginalColor);

        for (int i = 0; i < DecorationClothCapacity; i++)
        {
            ItemStack? clothStack = i < decorationClothStacks.Count ? decorationClothStacks[i] : null;
            SetOrRemoveItemstack(tree, $"decorationClothStack{i}", clothStack);
        }
    }

    private void ReadDecorationTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        decorationKitHeadStack = ResolveItemstack(tree.GetItemstack("decorationKitHeadStack"), worldForResolving);
        ornamentsStack = ResolveItemstack(tree.GetItemstack("ornamentsStack"), worldForResolving);
        decorationKitBodyStack = ResolveItemstack(tree.GetItemstack("decorationKitBodyStack"), worldForResolving);
        bracketsStack = ResolveItemstack(tree.GetItemstack("bracketsStack"), worldForResolving);
        decorationKitLegsStack = ResolveItemstack(tree.GetItemstack("decorationKitLegsStack"), worldForResolving);
        fastenersStack = ResolveItemstack(tree.GetItemstack("fastenersStack"), worldForResolving);
        decorationHelmetStack = ResolveItemstack(tree.GetItemstack("decorationHelmetStack"), worldForResolving);
        decorationBodyStack = ResolveItemstack(tree.GetItemstack("decorationBodyStack"), worldForResolving);
        decorationLegsStack = ResolveItemstack(tree.GetItemstack("decorationLegsStack"), worldForResolving);
        pendingHeadDecorationStack = ResolveItemstack(tree.GetItemstack("pendingHeadDecorationStack"), worldForResolving);
        pendingHeadColorStack = ResolveItemstack(tree.GetItemstack("pendingHeadColorStack"), worldForResolving);
        pendingBodyDecorationStack = ResolveItemstack(tree.GetItemstack("pendingBodyDecorationStack"), worldForResolving);
        pendingBodyColorStack = ResolveItemstack(tree.GetItemstack("pendingBodyColorStack"), worldForResolving);
        pendingLegsDecorationStack = ResolveItemstack(tree.GetItemstack("pendingLegsDecorationStack"), worldForResolving);
        pendingLegsColorStack = ResolveItemstack(tree.GetItemstack("pendingLegsColorStack"), worldForResolving);
        pendingHeadOriginalDecoration = tree.GetString("pendingHeadOriginalDecoration") ?? "";
        pendingHeadOriginalColor = tree.GetString("pendingHeadOriginalColor") ?? "";
        pendingBodyOriginalDecoration = tree.GetString("pendingBodyOriginalDecoration") ?? "";
        pendingBodyOriginalColor = tree.GetString("pendingBodyOriginalColor") ?? "";
        pendingLegsOriginalDecoration = tree.GetString("pendingLegsOriginalDecoration") ?? "";
        pendingLegsOriginalColor = tree.GetString("pendingLegsOriginalColor") ?? "";

        decorationClothStacks.Clear();
        for (int i = 0; i < DecorationClothCapacity; i++)
        {
            ItemStack? clothStack = ResolveItemstack(tree.GetItemstack($"decorationClothStack{i}"), worldForResolving);
            if (clothStack != null)
            {
                decorationClothStacks.Add(clothStack);
            }
        }
    }

    private void DropDecorationInventory()
    {
        RestoreAllPendingDecorationPreviews();
        DropDecorationStack(ref pendingHeadDecorationStack);
        DropDecorationStack(ref pendingHeadColorStack);
        DropDecorationStack(ref pendingBodyDecorationStack);
        DropDecorationStack(ref pendingBodyColorStack);
        DropDecorationStack(ref pendingLegsDecorationStack);
        DropDecorationStack(ref pendingLegsColorStack);
        DropDecorationStack(ref decorationKitHeadStack);
        DropDecorationStack(ref ornamentsStack);
        DropDecorationStack(ref decorationKitBodyStack);
        DropDecorationStack(ref bracketsStack);
        DropDecorationStack(ref decorationKitLegsStack);
        DropDecorationStack(ref fastenersStack);
        DropDecorationStack(ref decorationHelmetStack);
        DropDecorationStack(ref decorationBodyStack);
        DropDecorationStack(ref decorationLegsStack);

        foreach (ItemStack clothStack in decorationClothStacks)
        {
            Api.World.SpawnItemEntity(clothStack, Pos.ToVec3d().Add(0.5, 0.8, 0.5));
        }

        decorationClothStacks.Clear();
    }

    private void DropDecorationStack(ref ItemStack? stack)
    {
        if (stack == null)
        {
            return;
        }

        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.8, 0.5));
        stack = null;
    }

    private void WriteTrimTreeAttributes(ITreeAttribute tree)
    {
        SetOrRemoveItemstack(tree, "trimArmorStack", trimArmorStack);
        SetOrRemoveItemstack(tree, "trimRivetsStack", trimRivetsStack);
        SetOrRemoveItemstack(tree, "trimCrucibleStack", trimCrucibleStack);
        SetOrRemoveItemstack(tree, "trimSolderingIronStack", trimSolderingIronStack);
        SetOrRemoveItemstack(tree, "pendingTrimRivetsStack", pendingTrimRivetsStack);
        SetOrRemoveString(tree, "pendingTrimOriginalStrip", pendingTrimOriginalStrip);
    }

    private void ReadTrimTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        trimArmorStack = ResolveItemstack(tree.GetItemstack("trimArmorStack"), worldForResolving);
        trimRivetsStack = ResolveItemstack(tree.GetItemstack("trimRivetsStack"), worldForResolving);
        trimCrucibleStack = ResolveItemstack(tree.GetItemstack("trimCrucibleStack"), worldForResolving);
        trimSolderingIronStack = ResolveItemstack(tree.GetItemstack("trimSolderingIronStack"), worldForResolving);
        pendingTrimRivetsStack = ResolveItemstack(tree.GetItemstack("pendingTrimRivetsStack"), worldForResolving);
        pendingTrimOriginalStrip = tree.GetString("pendingTrimOriginalStrip") ?? "";
    }

    private void DropTrimInventory()
    {
        RestorePendingTrimPreview();
        DropDecorationStack(ref pendingTrimRivetsStack);
        DropDecorationStack(ref trimRivetsStack);
        DropDecorationStack(ref trimCrucibleStack);
        DropDecorationStack(ref trimSolderingIronStack);
        DropDecorationStack(ref trimArmorStack);
        pendingTrimOriginalStrip = "";
    }

    private void AppendTrimStationDisplayInfo(StringBuilder builder)
    {
        AppendDecorationLine(builder, "Armor", trimArmorStack);
        AppendDecorationLine(builder, "Rims and rivets", trimRivetsStack);
        AppendDecorationLine(builder, "Solder crucible", trimCrucibleStack);
        AppendDecorationLine(builder, "Soldering iron", trimSolderingIronStack);
        AppendDecorationLine(builder, "Staged trim", pendingTrimRivetsStack);

        if (Api?.Side == EnumAppSide.Client)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("[trim armor: ");
            builder.Append(trimArmorRenderDebug);
            builder.Append("]");
        }
    }

    private void AppendDecorationStationDisplayInfo(StringBuilder builder)
    {
        AppendDecorationLine(builder, "Helmet", decorationHelmetStack);
        AppendDecorationLine(builder, "Chestplate", decorationBodyStack);
        AppendDecorationLine(builder, "Leggings", decorationLegsStack);
        AppendDecorationLine(builder, "Helmet kit", decorationKitHeadStack);
        AppendDecorationLine(builder, "Ornaments", ornamentsStack);
        AppendDecorationLine(builder, "Chest kit", decorationKitBodyStack);
        AppendDecorationLine(builder, "Brackets", bracketsStack);
        AppendDecorationLine(builder, "Legs kit", decorationKitLegsStack);
        AppendDecorationLine(builder, "Fasteners", fastenersStack);
        AppendDecorationLine(builder, "Helmet staged decoration", pendingHeadDecorationStack);
        AppendDecorationLine(builder, "Helmet staged color", pendingHeadColorStack);
        AppendDecorationLine(builder, "Chest staged decoration", pendingBodyDecorationStack);
        AppendDecorationLine(builder, "Chest staged color", pendingBodyColorStack);
        AppendDecorationLine(builder, "Legs staged decoration", pendingLegsDecorationStack);
        AppendDecorationLine(builder, "Legs staged color", pendingLegsColorStack);

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append("Cloth: ");
        builder.Append(decorationClothStacks.Count);
        builder.Append("/");
        builder.Append(DecorationClothCapacity);

        if (Api?.Side == EnumAppSide.Client)
        {
            builder.AppendLine();
            builder.Append("[armorstand: ");
            builder.Append(armorStandRenderDebug);
            builder.Append("]");
        }
    }

    private static void AppendDecorationLine(StringBuilder builder, string label, ItemStack? stack)
    {
        if (stack == null)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(label);
        builder.Append(": ");
        builder.Append(stack.GetName());
    }

    private ItemStack? GetDecorationShelfStack(string actionName)
    {
        return actionName switch
        {
            "CupboardSlot1" => decorationKitHeadStack,
            "CupboardSlot2" => ornamentsStack,
            "CupboardSlot3" => decorationKitBodyStack,
            "CupboardSlot4" => bracketsStack,
            "CupboardSlot5" => decorationKitLegsStack,
            "CupboardSlot6" => fastenersStack,
            _ => null
        };
    }

    private void SetDecorationShelfStack(string actionName, ItemStack? stack)
    {
        switch (actionName)
        {
            case "CupboardSlot1":
                decorationKitHeadStack = stack;
                return;
            case "CupboardSlot2":
                ornamentsStack = stack;
                return;
            case "CupboardSlot3":
                decorationKitBodyStack = stack;
                return;
            case "CupboardSlot4":
                bracketsStack = stack;
                return;
            case "CupboardSlot5":
                decorationKitLegsStack = stack;
                return;
            case "CupboardSlot6":
                fastenersStack = stack;
                return;
        }
    }

    private ItemStack? GetDecorationArmorStack(string actionName)
    {
        return actionName switch
        {
            "HelmetDeco" => decorationHelmetStack,
            "BodyDeco" => decorationBodyStack,
            "LegsDeco" => decorationLegsStack,
            _ => null
        };
    }

    private void SetDecorationArmorStack(string actionName, ItemStack? stack)
    {
        switch (actionName)
        {
            case "HelmetDeco":
                decorationHelmetStack = stack;
                return;
            case "BodyDeco":
                decorationBodyStack = stack;
                return;
            case "LegsDeco":
                decorationLegsStack = stack;
                return;
        }
    }

    private static string GetDecorationSlotName(string actionName)
    {
        return actionName switch
        {
            "CupboardSlot1" => "cupboard slot 1",
            "CupboardSlot2" => "cupboard slot 2",
            "CupboardSlot3" => "cupboard slot 3",
            "CupboardSlot4" => "cupboard slot 4",
            "CupboardSlot5" => "cupboard slot 5",
            "CupboardSlot6" => "cupboard slot 6",
            _ => "cupboard"
        };
    }

    private static bool IsDecorationCloth(ItemStack stack)
    {
        string path = stack.Collectible?.Code?.Path ?? "";
        return path.Contains("cloth", StringComparison.OrdinalIgnoreCase)
            || path.Contains("linen", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDecorationSmallItem(ItemStack stack)
    {
        AssetLocation? code = stack.Collectible?.Code;
        if (code == null || code.Domain != "facore")
        {
            return false;
        }

        string baseCode = code.Path.Split('-')[0];
        return baseCode is "rimsandrivets" or "brackets" or "fasteners" or "ornaments" or "decorationkit";
    }

    private static bool TryGetRimsAndRivetsMetal(ItemStack? stack, out string metal)
    {
        metal = "";
        AssetLocation? code = stack?.Collectible?.Code;
        if (code?.Domain != "facore" || !code.Path.StartsWith("rimsandrivets-", StringComparison.Ordinal))
        {
            return false;
        }

        metal = code.Path["rimsandrivets-".Length..];
        return metal.Length > 0;
    }

    private static bool IsIronOrBetterTongs(ItemStack? stack)
    {
        string path = stack?.Collectible?.Code?.Path ?? "";
        if (!path.Contains("tongs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string metal = "";
        int separator = path.LastIndexOf('-');
        if (separator >= 0 && separator < path.Length - 1)
        {
            metal = path[(separator + 1)..];
        }

        return metal is "iron" or "meteoriciron" or "steel" or "blistersteel";
    }

    private static bool IsIronOrBetterTool(ItemStack? stack, string toolName)
    {
        string path = stack?.Collectible?.Code?.Path ?? "";
        if (!path.Contains(toolName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string metal = "";
        int separator = path.LastIndexOf('-');
        if (separator >= 0 && separator < path.Length - 1)
        {
            metal = path[(separator + 1)..];
        }

        return metal is "iron" or "meteoriciron" or "steel" or "blistersteel";
    }

    private static bool IsSolderingIron(ItemStack? stack)
    {
        string path = stack?.Collectible?.Code?.Path ?? "";
        return path.Contains("solder", StringComparison.OrdinalIgnoreCase)
            && path.Contains("iron", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrimValueSet(string? trim)
    {
        return !string.IsNullOrEmpty(trim) && !string.Equals(trim, "none", StringComparison.Ordinal);
    }

    private ItemStack? CreateRimsAndRivetsStack(string metal)
    {
        Item? item = Api?.World.GetItem(new AssetLocation("facore", "rimsandrivets-" + metal));
        return item == null ? null : new ItemStack(item);
    }

    private bool HasUsableTrimSolderCrucible(out string failure)
    {
        if (trimCrucibleStack == null)
        {
            failure = $"Place a hot crucible with at least {TrimSolderWeldAmount} lead or silver solder on the crucible stand before baking rivets.";
            return false;
        }

        return IsLeadOrSilverSolderCrucible(trimCrucibleStack, requireHeat: true, out _, out failure);
    }

    private bool TryConsumeTrimSolder(out string failure)
    {
        if (!HasUsableTrimSolderCrucible(out failure))
        {
            return false;
        }

        if (!TryGetSolderContent(trimCrucibleStack, out SolderContent solderContent))
        {
            failure = "The solder crucible content could not be read.";
            return false;
        }

        if (solderContent.Amount < TrimSolderWeldAmount)
        {
            failure = $"The solder crucible needs at least {TrimSolderWeldAmount} lead or silver solder to weld rivets.";
            return false;
        }

        if (trimCrucibleStack?.Collectible is ILiquidSource liquidSource && solderContent.Stack != null)
        {
            ItemStack? takenStack = liquidSource.TryTakeContent(trimCrucibleStack, TrimSolderWeldAmount);
            if (takenStack != null && takenStack.StackSize >= TrimSolderWeldAmount && TryGetSolderMetal(takenStack, out _))
            {
                return true;
            }

            failure = "The solder could not be removed from the crucible.";
            return false;
        }

        if (solderContent.Tree != null && !string.IsNullOrEmpty(solderContent.AmountKey))
        {
            int remaining = solderContent.Amount - TrimSolderWeldAmount;
            if (remaining > 0)
            {
                solderContent.Tree.SetInt(solderContent.AmountKey, remaining);
            }
            else
            {
                solderContent.Tree.RemoveAttribute(solderContent.AmountKey);
                if (!string.IsNullOrEmpty(solderContent.ContentKey))
                {
                    solderContent.Tree.RemoveAttribute(solderContent.ContentKey);
                }
            }

            return true;
        }

        if (solderContent.Stack == null || string.IsNullOrEmpty(solderContent.ContentKey))
        {
            failure = "The solder crucible content cannot be consumed from this container.";
            return false;
        }

        solderContent.Stack.StackSize -= TrimSolderWeldAmount;
        SetOrRemoveItemstack(trimCrucibleStack!.Attributes, solderContent.ContentKey, solderContent.Stack.StackSize > 0 ? solderContent.Stack : null);
        return true;
    }

    private bool IsLeadOrSilverSolderCrucible(ItemStack? stack, bool requireHeat, out string solderMetal, out string failure)
    {
        solderMetal = "";
        failure = "";

        if (!IsCrucible(stack))
        {
            failure = "Place a crucible here.";
            TrimDebugLog($"SolderCrucible check failed: not crucible stack={FormatStackDebug(stack)}");
            return false;
        }

        if (!TryGetSolderContent(stack, out SolderContent solderContent))
        {
            failure = "The crucible must contain lead or silver solder.";
            TrimDebugLog($"SolderCrucible check failed: no solder content stack={FormatStackDebug(stack)} attrs={FormatTreeDebug(stack?.Attributes)} requireHeat={requireHeat}");
            return false;
        }

        solderMetal = solderContent.Metal;
        TrimDebugLog($"SolderCrucible content metal={solderContent.Metal} amount={solderContent.Amount} temp={solderContent.Temperature:0.#} stack={FormatStackDebug(solderContent.Stack)} contentKey={solderContent.ContentKey} amountKey={solderContent.AmountKey} requireHeat={requireHeat}");
        if (requireHeat && solderContent.Amount < TrimSolderWeldAmount)
        {
            failure = $"The solder crucible needs at least {TrimSolderWeldAmount} lead or silver solder to weld rivets.";
            return false;
        }

        if (requireHeat)
        {
            float temperature = solderContent.Temperature;
            float requiredTemperature = solderMetal == "silver" ? TrimSolderSilverMinTemperature : TrimSolderLeadMinTemperature;
            if (temperature < requiredTemperature)
            {
                failure = $"The {solderMetal} solder is too cold. Heat it to at least {requiredTemperature:0}C.";
                return false;
            }
        }

        return true;
    }

    private static bool IsCrucible(ItemStack? stack)
    {
        string path = stack?.Collectible?.Code?.Path ?? "";
        return path.Contains("crucible", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetSolderMetal(ItemStack? stack, out string metal)
    {
        metal = "";
        if (TryGetSolderMetalFromStackCode(stack, out metal))
        {
            return true;
        }

        if (stack?.Collectible is ILiquidInterface liquidInterface
            && TryGetSolderMetalFromStackCode(liquidInterface.GetContent(stack), out metal))
        {
            return true;
        }

        ITreeAttribute? attrs = stack?.Attributes;
        if (attrs == null)
        {
            return false;
        }

        foreach (string key in new[] { "contents", "content", "liquid", "metalContent", "output" })
        {
            if (TryGetSolderMetalFromStackCode(attrs.GetItemstack(key), out metal))
            {
                return true;
            }

            ITreeAttribute? contentTree = attrs.GetTreeAttribute(key);
            if (contentTree != null && TryGetSolderMetalFromContentTree(contentTree, out metal))
            {
                return true;
            }
        }

        foreach (string key in new[] { "metal", "solderMetal", "contentMetal" })
        {
            string value = attrs.GetString(key) ?? "";
            if (TryNormalizeSolderMetal(value, out metal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSolderContentStack(ItemStack? stack, out ItemStack? contentStack, out string contentKey)
    {
        contentStack = null;
        contentKey = "";

        if (stack?.Collectible is ILiquidInterface liquidInterface)
        {
            ItemStack? liquidContent = liquidInterface.GetContent(stack);
            if (TryGetSolderMetal(liquidContent, out _))
            {
                contentStack = liquidContent;
                contentKey = "";
                return true;
            }
        }

        ITreeAttribute? attrs = stack?.Attributes;
        if (attrs == null)
        {
            return false;
        }

        foreach (string key in new[] { "contents", "content", "liquid", "metalContent" })
        {
            ItemStack? attrStack = attrs.GetItemstack(key);
            if (TryGetSolderMetal(attrStack, out _))
            {
                contentStack = attrStack;
                contentKey = key;
                return true;
            }
        }

        return false;
    }

    private bool TryGetSolderContent(ItemStack? stack, out SolderContent solderContent, bool logDebug = true)
    {
        solderContent = default;
        float containerTemperature = stack?.Collectible?.GetTemperature(Api.World, stack) ?? 0f;
        if (logDebug)
        {
            TrimDebugLog($"TryGetSolderContent stack={FormatStackDebug(stack)} containerTemp={containerTemperature:0.#} attrs={FormatTreeDebug(stack?.Attributes)}");
        }

        if (TryGetSolderContentStack(stack, out ItemStack? contentStack, out string contentKey)
            && contentStack != null
            && TryGetSolderMetal(contentStack, out string stackMetal))
        {
            float contentTemperature = contentStack.Collectible.GetTemperature(Api.World, contentStack);
            if (logDebug)
            {
                TrimDebugLog($"TryGetSolderContent matched content stack key={contentKey} content={FormatStackDebug(contentStack)} metal={stackMetal} contentTemp={contentTemperature:0.#}");
            }
            solderContent = new SolderContent(
                stackMetal,
                contentStack.StackSize,
                Math.Max(contentTemperature, containerTemperature),
                contentStack,
                contentKey,
                null,
                ""
            );
            return true;
        }

        ITreeAttribute? attrs = stack?.Attributes;
        if (attrs == null)
        {
            if (logDebug)
            {
                TrimDebugLog("TryGetSolderContent no attrs and no content stack.");
            }
            return false;
        }

        foreach (string key in new[] { "contents", "content", "liquid", "metalContent" })
        {
            ITreeAttribute? contentTree = attrs.GetTreeAttribute(key);
            if (contentTree != null && TryGetSolderContentFromTree(contentTree, containerTemperature, out solderContent))
            {
                if (logDebug)
                {
                    TrimDebugLog($"TryGetSolderContent matched tree key={key} metal={solderContent.Metal} amount={solderContent.Amount} temp={solderContent.Temperature:0.#} tree={FormatTreeDebug(contentTree)}");
                }
                return true;
            }
        }

        bool matchedRoot = TryGetSolderContentFromTree(attrs, containerTemperature, out solderContent);
        if (logDebug)
        {
            TrimDebugLog($"TryGetSolderContent root match={matchedRoot} metal={solderContent.Metal} amount={solderContent.Amount} temp={solderContent.Temperature:0.#}");
        }
        return matchedRoot;
    }

    private static bool TryGetSolderContentFromTree(ITreeAttribute tree, float fallbackTemperature, out SolderContent solderContent)
    {
        solderContent = default;
        if (!TryGetSolderMetalFromContentTree(tree, out string metal, out string contentKey))
        {
            return false;
        }

        if (!TryGetTreeInt(tree, out string amountKey, out int amount, "units", "quantity", "stackSize", "stacksize", "amount", "size"))
        {
            amount = 0;
        }

        float temperature = fallbackTemperature;
        if (TryGetTreeFloat(tree, out _, out float contentTemperature, "temperature", "temp"))
        {
            temperature = Math.Max(temperature, contentTemperature);
        }

        solderContent = new SolderContent(metal, amount, temperature, null, contentKey, tree, amountKey);
        return true;
    }

    private static bool TryGetTreeInt(ITreeAttribute tree, out string foundKey, out int value, params string[] keys)
    {
        foreach (string key in keys)
        {
            value = tree.GetInt(key, int.MinValue);
            if (value != int.MinValue)
            {
                foundKey = key;
                return true;
            }
        }

        foundKey = "";
        value = 0;
        return false;
    }

    private static bool TryGetTreeFloat(ITreeAttribute tree, out string foundKey, out float value, params string[] keys)
    {
        foreach (string key in keys)
        {
            value = tree.GetFloat(key, float.MinValue);
            if (value > float.MinValue)
            {
                foundKey = key;
                return true;
            }
        }

        foundKey = "";
        value = 0f;
        return false;
    }

    private static bool TryGetSolderMetalFromContentTree(ITreeAttribute tree, out string metal)
    {
        return TryGetSolderMetalFromContentTree(tree, out metal, out _);
    }

    private static bool TryGetSolderMetalFromContentTree(ITreeAttribute tree, out string metal, out string contentKey)
    {
        foreach (string key in new[] { "stack", "itemstack", "itemStack", "content", "output" })
        {
            if (TryGetSolderMetalFromStackCode(tree.GetItemstack(key), out metal))
            {
                contentKey = key;
                return true;
            }
        }

        foreach (string key in new[] { "code", "path", "metal", "variant", "solderMetal" })
        {
            string value = tree.GetString(key) ?? "";
            if (TryNormalizeSolderMetal(value, out metal))
            {
                contentKey = key;
                return true;
            }
        }

        metal = "";
        contentKey = "";
        return false;
    }

    private static bool TryGetSolderMetalFromStackCode(ItemStack? stack, out string metal)
    {
        string path = stack?.Collectible?.Code?.Path ?? "";
        return TryNormalizeSolderMetal(path, out metal);
    }

    private static bool TryNormalizeSolderMetal(string value, out string metal)
    {
        metal = "";
        if (value.Contains("lead", StringComparison.OrdinalIgnoreCase))
        {
            metal = "lead";
            return true;
        }

        if (value.Contains("silver", StringComparison.OrdinalIgnoreCase))
        {
            metal = "silver";
            return true;
        }

        return false;
    }

    private static bool IsSneaking(IPlayer player)
    {
        return player.Entity?.Controls?.Sneak == true;
    }

    private static bool TryParseDecorationMaterial(ItemStack stack, string expectedPiece, out string editKind, out string value, out string failure)
    {
        editKind = "";
        value = "";
        failure = "";

        AssetLocation? code = stack.Collectible?.Code;
        if (code?.Domain != "facore")
        {
            failure = "Use Forgotten Armory decoration materials here.";
            return false;
        }

        string path = code.Path;
        if (path.StartsWith("decorationkit-", StringComparison.Ordinal))
        {
            editKind = "color";
            value = path["decorationkit-".Length..];
            return value.Length > 0;
        }

        string requiredPrefix = expectedPiece switch
        {
            "head" => "ornaments-",
            "body" => "brackets-",
            "legs" => "fasteners-",
            _ => ""
        };

        if (requiredPrefix.Length == 0)
        {
            return false;
        }

        if (!path.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            failure = expectedPiece switch
            {
                "head" => "Use ornaments on helmets.",
                "body" => "Use brackets on chestplates.",
                "legs" => "Use fasteners on leggings.",
                _ => "That decoration does not fit this armor piece."
            };
            return false;
        }

        editKind = "decoration";
        value = path[requiredPrefix.Length..];
        return value.Length > 0;
    }

    private static string GetDecorationEditName(string editKind)
    {
        return editKind == "color" ? "color kit" : "decoration";
    }

    private bool HasPendingDecorationEdits(string piece)
    {
        return GetPendingDecorationMaterial(piece, "decoration") != null
            || GetPendingDecorationMaterial(piece, "color") != null;
    }

    private ItemStack? GetPendingDecorationMaterial(string piece, string editKind)
    {
        return (piece, editKind) switch
        {
            ("head", "decoration") => pendingHeadDecorationStack,
            ("head", "color") => pendingHeadColorStack,
            ("body", "decoration") => pendingBodyDecorationStack,
            ("body", "color") => pendingBodyColorStack,
            ("legs", "decoration") => pendingLegsDecorationStack,
            ("legs", "color") => pendingLegsColorStack,
            _ => null
        };
    }

    private void SetPendingDecorationMaterial(string piece, string editKind, ItemStack? stack)
    {
        switch (piece, editKind)
        {
            case ("head", "decoration"):
                pendingHeadDecorationStack = stack;
                return;
            case ("head", "color"):
                pendingHeadColorStack = stack;
                return;
            case ("body", "decoration"):
                pendingBodyDecorationStack = stack;
                return;
            case ("body", "color"):
                pendingBodyColorStack = stack;
                return;
            case ("legs", "decoration"):
                pendingLegsDecorationStack = stack;
                return;
            case ("legs", "color"):
                pendingLegsColorStack = stack;
                return;
        }
    }

    private string GetPendingOriginalValue(string piece, string editKind)
    {
        return (piece, editKind) switch
        {
            ("head", "decoration") => pendingHeadOriginalDecoration,
            ("head", "color") => pendingHeadOriginalColor,
            ("body", "decoration") => pendingBodyOriginalDecoration,
            ("body", "color") => pendingBodyOriginalColor,
            ("legs", "decoration") => pendingLegsOriginalDecoration,
            ("legs", "color") => pendingLegsOriginalColor,
            _ => ""
        };
    }

    private void SetPendingOriginalValue(string piece, string editKind, string value)
    {
        switch (piece, editKind)
        {
            case ("head", "decoration"):
                pendingHeadOriginalDecoration = value;
                return;
            case ("head", "color"):
                pendingHeadOriginalColor = value;
                return;
            case ("body", "decoration"):
                pendingBodyOriginalDecoration = value;
                return;
            case ("body", "color"):
                pendingBodyOriginalColor = value;
                return;
            case ("legs", "decoration"):
                pendingLegsOriginalDecoration = value;
                return;
            case ("legs", "color"):
                pendingLegsOriginalColor = value;
                return;
        }
    }

    private void RestorePendingDecorationValue(ItemStack armorStack, string piece, string editKind)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return;
        }

        string originalValue = GetPendingOriginalValue(piece, editKind);
        types.SetString(editKind + piece, string.IsNullOrEmpty(originalValue) ? "none" : originalValue);
    }

    private void RestoreAllPendingDecorationPreviews()
    {
        RestoreAllPendingDecorationPreviews(decorationHelmetStack, "head");
        RestoreAllPendingDecorationPreviews(decorationBodyStack, "body");
        RestoreAllPendingDecorationPreviews(decorationLegsStack, "legs");
    }

    private void RestoreAllPendingDecorationPreviews(ItemStack? armorStack, string piece)
    {
        if (armorStack == null)
        {
            return;
        }

        if (GetPendingDecorationMaterial(piece, "color") != null)
        {
            RestorePendingDecorationValue(armorStack, piece, "color");
        }

        if (GetPendingDecorationMaterial(piece, "decoration") != null)
        {
            RestorePendingDecorationValue(armorStack, piece, "decoration");
        }
    }

    private void GiveOrDrop(IPlayer byPlayer, ItemStack stack, double yOffset)
    {
        if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, yOffset, 0.5));
        }
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

    private static void SetOrRemoveString(ITreeAttribute tree, string key, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            tree.RemoveAttribute(key);
            return;
        }

        tree.SetString(key, value);
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

    private void PlayItemInsertSound(IPlayer byPlayer)
    {
        PlayStationSound(ItemInsertSound, byPlayer, 0.65f);
    }

    private void PlayItemPickupSound(IPlayer byPlayer)
    {
        PlayStationSound(ItemPickupSound, byPlayer, 0.55f);
    }

    private void PlayCeramicPlaceSound(IPlayer byPlayer)
    {
        PlayStationSound(CeramicPlaceSound, byPlayer, 0.8f);
    }

    private void PlayScrapeSound(IPlayer byPlayer)
    {
        PlayStationSound(ScrapeSound, byPlayer, 0.65f);
    }

    private void PlayMetalHitSound(IPlayer byPlayer)
    {
        AssetLocation sound = MetalHitSounds[Api.World.Rand.Next(MetalHitSounds.Length)];
        PlayStationSound(sound, byPlayer, 0.85f);
    }

    private void PlaySawSound(IPlayer byPlayer)
    {
        AssetLocation sound = SawSounds[Api.World.Rand.Next(SawSounds.Length)];
        PlayStationSound(sound, byPlayer, 0.7f);
    }

    private void PlaySolderSound(IPlayer byPlayer)
    {
        PlayStationSound(SolderSound, byPlayer, 0.8f);
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

    private void Notify(IPlayer player, string text)
    {
        if (player is IServerPlayer serverPlayer)
        {
            serverPlayer.SendIngameError("facore-coverstation", text);
        }
    }

    private void NotifyInfo(IPlayer player, string text)
    {
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

    private void TrimDebugLog(string message)
    {
        Api?.Logger.Notification("[FACore TrimStation] {0}: {1}", Pos, message);
    }

    private static string FormatStackDebug(ItemStack? stack)
    {
        return stack == null ? "null" : $"{stack.StackSize}x {stack.Collectible?.Code}";
    }

    private static string FormatTreeDebug(ITreeAttribute? tree)
    {
        if (tree == null)
        {
            return "null";
        }

        var parts = new List<string>();
        foreach ((string key, IAttribute value) in tree)
        {
            parts.Add(key + "=" + FormatAttributeDebug(value));
        }

        return parts.Count == 0 ? "{}" : "{" + string.Join(", ", parts) + "}";
    }

    private static string FormatAttributeDebug(IAttribute? attribute)
    {
        if (attribute == null)
        {
            return "null";
        }

        if (attribute is ITreeAttribute tree)
        {
            return FormatTreeDebug(tree);
        }

        if (attribute is ItemstackAttribute itemstackAttribute)
        {
            return FormatStackDebug(itemstackAttribute.value);
        }

        return attribute.GetType().Name + ":" + attribute;
    }

    private sealed class FAArmorInfo
    {
        public FAArmorInfo(string domain, string family, string piece, string slotPrefix, string style, string baseMetal)
        {
            Domain = domain;
            Family = family;
            Piece = piece;
            SlotPrefix = slotPrefix;
            Style = style;
            BaseMetal = baseMetal;
        }

        public string Domain { get; }
        public string Family { get; }
        public string Piece { get; }
        public string SlotPrefix { get; }
        public string Style { get; }
        public string BaseMetal { get; }
        public string TextureKind => SlotPrefix.StartsWith("plate", StringComparison.Ordinal) ? "plate" : SlotPrefix;
    }

    private readonly struct SolderContent
    {
        public SolderContent(string metal, int amount, float temperature, ItemStack? stack, string contentKey, ITreeAttribute? tree, string amountKey)
        {
            Metal = metal;
            Amount = amount;
            Temperature = temperature;
            Stack = stack;
            ContentKey = contentKey;
            Tree = tree;
            AmountKey = amountKey;
        }

        public string Metal { get; }
        public int Amount { get; }
        public float Temperature { get; }
        public ItemStack? Stack { get; }
        public string ContentKey { get; }
        public ITreeAttribute? Tree { get; }
        public string AmountKey { get; }
    }

    private readonly struct MeshBounds
    {
        public MeshBounds(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
            IsValid = true;
        }

        public float MinX { get; }
        public float MinY { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public float MaxZ { get; }
        public bool IsValid { get; }
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterY => (MinY + MaxY) * 0.5f;
        public float CenterZ => (MinZ + MaxZ) * 0.5f;
        public float Height => MaxY - MinY;

        public override string ToString()
        {
            return $"[{MinX:0.###},{MinY:0.###},{MinZ:0.###}]..[{MaxX:0.###},{MaxY:0.###},{MaxZ:0.###}]";
        }
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

    private sealed class TransparentTextureSource : ITexPositionSource
    {
        private readonly TextureAtlasPosition position;

        public TransparentTextureSource(ICoreClientAPI capi, Size2i? atlasSize)
        {
            var texture = new CompositeTexture(TransparentTextureLocation);
            texture.Bake(capi.Assets);
            capi.BlockTextureAtlas.GetOrInsertTexture(texture, out _, out position, 0.005f);
            AtlasSize = atlasSize;
        }

        public TextureAtlasPosition this[string textureCode] => position;

        public Size2i? AtlasSize { get; }
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
