using System;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using FACore.ArmorCommands;

namespace FACore;

public class BlockEntityFAWorkStation : BlockEntity
{
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
    private static readonly AssetLocation TongsSound = new("facore", "sounds/trimstation/tongs");
    private static readonly AssetLocation SolderSound = new("facore", "sounds/trimstation/soldering");
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
    private static readonly AssetLocation FirestarterCode = new("game:firestarter");
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
    private const float ChemicalExposureDamage = 0.5f;
    private const int ChemicalExposureTicks = 3;
    private const int ChemicalExposureTickDelayMs = 1000;
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
    private const float DecorationItemOffsetX = 0.3f;
    private const float DecorationItemOffsetY = 0f;
    private const float DecorationItemOffsetZ = -0.05f;
    private const float DecorationItemScale = 0.9f;
    private static readonly Vec3f DecorationShelfRotationOrigin = new(0.5f, 0f, 0.5f);
    // Bear armor projections use independent transforms so every body part can be tuned in place.
    private const float BearHeadOffsetX = 0f;
    private const float BearHeadOffsetY = 0f;
    private const float BearHeadOffsetZ = 0f;
    private const float BearHeadRotationX = 0f;
    private const float BearHeadRotationY = 0f;
    private const float BearHeadRotationZ = 0f;
    private const float BearHeadScale = 0.65f;
    private const float BearBodyOffsetX = 0f;
    private const float BearBodyOffsetY = 0f;
    private const float BearBodyOffsetZ = 0f;
    private const float BearBodyRotationX = 0f;
    private const float BearBodyRotationY = 0f;
    private const float BearBodyRotationZ = 0f;
    private const float BearBodyScale = 0.5f;
    private const float BearLegsOffsetX = 0f;
    private const float BearLegsOffsetY = 0f;
    private const float BearLegsOffsetZ = 0f;
    private const float BearLegsRotationX = 0f;
    private const float BearLegsRotationY = 0f;
    private const float BearLegsRotationZ = 0f;
    private const float BearLegsScale = 0.5f;
    private const float DecorationKitOffsetX = -0.3f;
    private const float DecorationKitOffsetY = 0f;
    private const float DecorationKitOffsetZ = -0.05f;
    private const float DecorationKitScale = 0.85f;
    private const float DecorationClothPileScale = 0.85f;
    private const float DecorationClothShelfOffsetX = -1f / 16f;
    private const float DecorationClothPileSpacing = 0.28f;
    private const float DecorationClothPileOffsetZ = -0.1f;
    private const float DecorationClothRotationY = 90f;
    private const float DecorationClothPiece1OffsetX = -0.08f;
    private const float DecorationClothPiece1OffsetY = 0f;
    private const float DecorationClothPiece1OffsetZ = 0f;
    private const float DecorationClothPiece1RotationX = 0f;
    private const float DecorationClothPiece1RotationY = 10f;
    private const float DecorationClothPiece2OffsetX = -0.05f;
    private const float DecorationClothPiece2OffsetY = 1f / 4f - 1f / 64f;
    private const float DecorationClothPiece2OffsetZ = -0.03f;
    private const float DecorationClothPiece2RotationX = -0f;
    private const float DecorationClothPiece2RotationY = 10f;
    private const float DecorationClothPiece3OffsetX = -0.06f;
    private const float DecorationClothPiece3OffsetY = 1f / 8f;
    private const float DecorationClothPiece3OffsetZ = 0.05f;
    private const float DecorationClothPiece3RotationX = -0f;
    private const float DecorationClothPiece3RotationY = -8f;
    // Shared Cover Station metal-plate transform, tuned for west/east. Each Plate element
    // anchors the finished pose after it is turned onto the station's axis.
    private const float CoverPlateOffsetX = 0f;
    private const float CoverPlateOffsetY = 0f;
    private const float CoverPlateOffsetZ = 0f;
    private const float CoverPlateScale = 1f;
    private const float CoverPlateRotationX = 90f;
    private const float CoverPlateRotationY = 0f;
    private const float CoverPlateRotationZ = 0f;
    private const float TrimRivetsBowlItemScale = 0.70f;
    private const float TrimCrucibleItemScale = 0.8925f;
    private const float TrimCrucibleItemYOffset = 1f / 16f;
    private const float TrimCrucibleItemYawDegrees = 45f;
    private const float TrimSolderingIronItemScale = 0.7f;
    private const int TrimSolderWeldAmount = 100;
    private const float TrimSolderSilverMinTemperature = 961f;
    private const string MissingTrimSolderCrucibleMessage = "Missing crucible with silver solder";
    private static readonly AssetLocation SilverSolderIngotCode = new("game", "ingot-silversolder");
    // Use the player seraph as the armor frame: it lives in the reliably-loaded "game" domain (the survival
    // armorstand shape is not retrievable via Shape.TryGet at tesselation time) and carries the exact joints
    // the worn armor step-parents onto. Its own body renders transparent so only the armor shows.
    private static readonly AssetLocation ArmorStandShapeLocation = new("game", "shapes/entity/humanoid/seraph.json");
    private static readonly AssetLocation TransparentTextureLocation = new("game", "block/transparent");
    // Texture codes of the base frame body that we hide so only the armor is visible.
    private static readonly string[] ArmorStandHiddenTextureCodes = ["seraph", "hair"];
    // Reference yaw for assembling the figure. AlignArmorStandMesh preserves the west/east
    // poses, including custom pitch and roll, before turning onto the other station axis.
    private static readonly float ArmorStandBaseFacingRadians = GameMath.PIHALF;
    // Small nudge of the figure away from the main block part (toward the proxy), in blocks.
    private const float ArmorStandPushFromMain = 1.5f / 16f;
    // Small nudge in the direction the displayed armor is facing, in blocks.
    private const float ArmorStandForwardNudge = 4f / 16f;
    // Lowers the displayed armor slightly so the feet sit into the stand.
    private const float ArmorStandDropY = 2f / 16f;
    // One common translation keeps every Decoration Station armor piece on the same frame.
    private const float DecorationArmorOffsetX = 0f;
    private const float DecorationArmorOffsetY = -1.2f;
    private const float DecorationArmorOffsetZ = 0f;
    private const int DecorationPreviewGlowLevel = 24;
    // Hemming-crane display transforms are deliberately separate per armor piece so each can be tuned
    // without moving the others. Offsets are in block units; rotations are in degrees.
    private static readonly ArmorPieceTransform TrimHelmetTransform = new(0.75f, -0.95f, -0.075f, 0.95f, 0f, 0f, 40f);
    private static readonly ArmorPieceTransform TrimBodyTransform = new(0f, -0.5f, 0f, 0.95f, 0f, 0f, 0f);
    private static readonly ArmorPieceTransform TrimLegsTransform = new(0.075f, -0.15f, -0.075f, 0.95f, 0f, 0f, 0f);

    private bool lidOpen;
    private bool fuelOpen;
    private bool fuelLit;
    private ItemStack? liquidStack;
    private ItemStack? immersedStack;
    private ItemStack? fuelStack;
    private ItemStack? tableStack;
    private ItemStack? coverPlate1Stack;
    private ItemStack? coverPlate2Stack;
    private ItemStack? coverPlate3Stack;
    private ItemStack? coverPlate4Stack;
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
    private readonly DecorationThreeColorState threeColorState = new();
    private string armorStandRenderDebug = "idle";
    private ItemStack? trimArmorStack;
    private ItemStack? trimRivetsStack;
    private ItemStack? trimCrucibleStack;
    private ItemStack? trimSolderingIronStack;
    private ItemStack? pendingTrimRivetsStack;
    private bool previewParticleTickLogged;
    private bool previewParticleSpawnLogged;
    private string previewParticleDebugState = "";
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
    private readonly object liquidTextureCacheLock = new();
    private TextureAtlasPosition? cachedCharcoalTexturePosition;
    private int cachedCharcoalTextureSubId;
    private ILoadedSound? processLoopSound;
    private WorkStationRenderSnapshot renderSnapshot = WorkStationRenderSnapshot.Empty;
    private long headHeldInteractionRevision;
    private long bodyHeldInteractionRevision;
    private long legsHeldInteractionRevision;
    private long trimHeldInteractionRevision;

    private readonly record struct ArmorPieceTransform(
        float OffsetX,
        float OffsetY,
        float OffsetZ,
        float Scale,
        float RotationX,
        float RotationY,
        float RotationZ);

    private sealed class WorkStationRenderSnapshot
    {
        public static readonly WorkStationRenderSnapshot Empty = new();

        public bool LidOpen { get; init; }
        public bool FuelOpen { get; init; }
        public bool FuelLit { get; init; }
        public ItemStack? Liquid { get; init; }
        public ItemStack? Immersed { get; init; }
        public ItemStack? Fuel { get; init; }
        public ItemStack? Table { get; init; }
        public ItemStack?[] CoverPlates { get; init; } = new ItemStack?[4];
        public ItemStack?[] DecorationArmor { get; init; } = new ItemStack?[3];
        public ItemStack?[] DecorationMaterials { get; init; } = new ItemStack?[3];
        public ItemStack?[] DecorationKits { get; init; } = new ItemStack?[3];
        public ItemStack?[,] DecorationCloth { get; init; } = new ItemStack?[3, DecorationThreeColorState.ColorsPerPiece];
        public bool[] DecorationPreviews { get; init; } = new bool[3];
        public ItemStack? TrimArmor { get; init; }
        public ItemStack? TrimRivets { get; init; }
        public ItemStack? TrimCrucible { get; init; }
        public ItemStack? TrimSolderingIron { get; init; }
        public ItemStack? PendingTrimRivets { get; init; }

        public ItemStack? GetDecorationArmor(int pieceIndex) => DecorationArmor[pieceIndex];
        public ItemStack? GetDecorationMaterial(int pieceIndex) => DecorationMaterials[pieceIndex];
        public ItemStack? GetDecorationKit(int pieceIndex) => DecorationKits[pieceIndex];
        public ItemStack? GetDecorationCloth(int pieceIndex, int colorIndex) => DecorationCloth[pieceIndex, colorIndex - 1];
    }

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
        using var languageScope = FaText.ForPlayer(byPlayer);
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
                    Notify(byPlayer, FaText.Get("Wait for the cauldron to cool before opening it."));
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

            case "Plate1":
            case "Plate2":
            case "Plate3":
            case "Plate4":
                TryInteractCoverPlate(byPlayer, actionName);
                return;

            default:
                Notify(byPlayer, FaText.Get("This part of the station cannot be used right now."));
                return;
        }
    }

    public bool CanStartHeldTrimArmorInteraction(IPlayer byPlayer)
    {
        if (!IsTrimStationBlock() || trimArmorStack == null)
        {
            return false;
        }

        ItemStack? heldStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (IsTongs(heldStack))
        {
            return HasBakedTrim;
        }

        if (!IsSolderingIron(heldStack) || pendingTrimRivetsStack == null || IsTrimValueSet(pendingTrimOriginalStrip))
        {
            return false;
        }

        return heldStack!.Collectible.GetRemainingDurability(heldStack) > 0
            && HasUsableTrimSolderCrucible(out _);
    }

    public bool CanStartHeldDecorationInteraction(IPlayer byPlayer, string actionName)
    {
        ItemStack? armorStack = GetDecorationArmorStack(actionName);
        if (!IsDecorationStationBlock() || armorStack == null || !TryGetFAArmorInfo(armorStack, out ResolvedArmor armorInfo))
        {
            return false;
        }

        string piece = armorInfo.Piece;
        ItemStack? heldStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (IsToolType(heldStack, "hammer"))
        {
            return armorInfo.Definition.Schema == ArmorAttributeSchema.Layered
                && !HasThreeColorCloth(piece)
                && TryPrepareLayeredDecorationBake(armorStack, piece, out _, out _, out _, out _, out _, out _);
        }

        if (IsScissors(heldStack))
        {
            return armorInfo.Definition.Schema == ArmorAttributeSchema.ThreeColor
                && TryPrepareThreeColorBake(armorStack, piece, out _, out _);
        }

        if (!IsToolType(heldStack, "saw") || HasActiveDecorationPreview(piece))
        {
            return false;
        }

        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        return types != null
            && (!string.Equals(types.GetString("decoration" + piece, "none"), "none", StringComparison.Ordinal)
                || !string.Equals(types.GetString("color" + piece, "none"), "none", StringComparison.Ordinal));
    }

    public long GetHeldInteractionRevision(string actionName)
    {
        return actionName switch
        {
            "HeadPlace" => headHeldInteractionRevision,
            "BodyPlace" => bodyHeldInteractionRevision,
            "LegsPlace" => legsHeldInteractionRevision,
            "Armor" when IsTrimStationBlock() => trimHeldInteractionRevision,
            _ => 0
        };
    }

    private void InvalidateDecorationOperation(string piece)
    {
        switch (piece)
        {
            case "head": headHeldInteractionRevision++; break;
            case "body": bodyHeldInteractionRevision++; break;
            case "legs": legsHeldInteractionRevision++; break;
        }
    }

    private void InvalidateTrimOperation() => trimHeldInteractionRevision++;

    public void PlayHeldActionStartSound(IPlayer byPlayer)
    {
        ItemStack? heldStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        if (IsTongs(heldStack))
        {
            PlayTongsSound(byPlayer);
        }
        else if (IsSolderingIron(heldStack))
        {
            PlaySolderSound(byPlayer);
        }
        else if (IsToolType(heldStack, "hammer"))
        {
            PlayHammerSound(byPlayer);
        }
        else if (IsToolType(heldStack, "saw"))
        {
            PlaySawSound(byPlayer);
        }
        else if (IsScissors(heldStack))
        {
            PlayShearsSound(byPlayer);
        }
    }

    public bool HasTrimArmor => trimArmorStack != null;
    public bool HasTrimRivets => trimRivetsStack != null || pendingTrimRivetsStack != null;
    public bool HasTrimCrucible => trimCrucibleStack != null;

    public bool HasStagedTrim => pendingTrimRivetsStack != null;

    public ArmorAttributeSchema? GetDecorationArmorSchema(string pieceOrActionName)
    {
        ItemStack? armorStack = GetDecorationArmorStack(pieceOrActionName);
        return TryGetFAArmorInfo(armorStack, out ResolvedArmor armorInfo)
            ? armorInfo.Definition.Schema
            : null;
    }

    public string? GetDecorationToolGuide(string pieceOrActionName)
    {
        ItemStack? armorStack = GetDecorationArmorStack(pieceOrActionName);
        if (armorStack == null || !TryGetFAArmorInfo(armorStack, out ResolvedArmor armorInfo))
        {
            return null;
        }

        if (armorInfo.Definition.Schema == ArmorAttributeSchema.ThreeColor)
        {
            return "shears";
        }

        if (armorInfo.Definition.Schema != ArmorAttributeSchema.Layered)
        {
            return null;
        }

        return HasBakedDecorationIgnoringPreview(armorStack, armorInfo.Piece) ? "saw" : "hammer";
    }

    public int GetDecorationClothPileCount(string piece) => CountThreeColorPiles(piece);

    public bool HasBakedTrim
    {
        get
        {
            if (trimArmorStack == null || !TryGetFAArmorInfo(trimArmorStack, out ResolvedArmor armorInfo))
            {
                return false;
            }

            string strip = pendingTrimRivetsStack != null
                ? pendingTrimOriginalStrip
                : trimArmorStack.Attributes?.GetTreeAttribute("types")?.GetString("strip" + armorInfo.Piece, "none") ?? "none";
            return IsTrimValueSet(strip);
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
        if (IsCoverStationBlock())
        {
            return DescribeCoverElementState(actionName);
        }

        if (IsDecorationStationBlock())
        {
            return DescribeDecorationElementState(actionName);
        }

        if (IsTrimStationBlock())
        {
            return DescribeTrimElementState(actionName);
        }

        return "";
    }

    private string DescribeCoverElementState(string actionName)
    {
        if (actionName is "Plate1" or "Plate2" or "Plate3" or "Plate4")
        {
            int index = actionName[^1] - '0';
            ItemStack? plateStack = GetCoverPlateStack(index);
            return FaText.Get("Plate slot {0}: {1}", index, plateStack?.GetName() ?? FaText.Get("empty"));
        }

        return actionName switch
        {
            "LidOpen" => FaText.Get("Lid: {0}\n", (lidOpen ? FaText.Get("open") : FaText.Get("closed"))) + (lidOpen ? FaText.Get("Cauldron accessible.") : FaText.Get("Open to access cauldron.")),
            "FuelDoor" => FaText.Get("Fuel door: {0}\n", (fuelOpen ? FaText.Get("open") : FaText.Get("closed"))) + (fuelOpen ? FaText.Get("Fuel tray accessible.") : FaText.Get("Open to access fuel.")),
            "Fuel" => DescribeCoverFuelState(),
            "LiquidPour" => DescribeCoverCauldronState(),
            "TableStorage" => DescribeStorageSlotState(FaText.Get("Work table"), tableStack),
            _ => ""
        };
    }

    private string DescribeDecorationElementState(string actionName)
    {
        return actionName switch
        {
            "HeadPlace" => DescribeDecorationArmorState(FaText.Get("Helmet"), "head", decorationHelmetStack),
            "BodyPlace" => DescribeDecorationArmorState(FaText.Get("Chestplate"), "body", decorationBodyStack),
            "LegsPlace" => DescribeDecorationArmorState(FaText.Get("Leggings"), "legs", decorationLegsStack),
            "HeadDecorations" => DescribeDecorationWorkState(FaText.Get("Head"), "head"),
            "BodyDecorations" => DescribeDecorationWorkState(FaText.Get("Body"), "body"),
            "LegsDecorations" => DescribeDecorationWorkState(FaText.Get("Legs"), "legs"),
            _ => ""
        };
    }

    private string DescribeDecorationWorkState(string label, string piece)
    {
        if (HasThreeColorCloth(piece))
        {
            return FaText.Get("{0} cloth: {1}/3 piles staged", FaText.Get(label), CountThreeColorPiles(piece));
        }

        ItemStack? decoration = GetPendingDecorationMaterial(piece, "decoration");
        ItemStack? kit = GetPendingDecorationMaterial(piece, "color");
        return FaText.Get("{0} decoration: {1}; kit: {2}", FaText.Get(label), (decoration?.GetName() ?? FaText.Get("empty")), (kit?.GetName() ?? FaText.Get("empty")));
    }

    private string DescribeDecorationArmorState(string label, string piece, ItemStack? armorStack)
    {
        var builder = new StringBuilder();
        builder.Append(label);
        builder.Append(": ");
        builder.Append(armorStack?.GetName() ?? FaText.Get("empty"));

        if (armorStack == null)
        {
            return builder.ToString();
        }

        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null
            || !TryGetFAArmorInfo(armorStack, out ResolvedArmor armorInfo)
            || armorInfo.Piece != piece)
        {
            builder.AppendLine();
            builder.Append(FaText.Get("Customization state unavailable"));
            return builder.ToString();
        }

        if (armorInfo.Definition.Schema == ArmorAttributeSchema.ThreeColor)
        {
            if (!ArmorCompatibility.TryReadThreeColor(types, piece, out ThreeColorArmorState state))
            {
                builder.AppendLine();
                builder.Append(FaText.Get("Three-color state is malformed"));
                return builder.ToString();
            }

            builder.AppendLine();
            builder.Append(FaText.Get("Color 1: "));
            builder.Append(FormatStationValue(state.Color1));
            builder.AppendLine();
            builder.Append(FaText.Get("Color 2: "));
            builder.Append(FormatStationValue(state.Color2));
            builder.AppendLine();
            builder.Append(FaText.Get("Color 3: "));
            builder.Append(FormatStationValue(state.Color3));
            builder.AppendLine();
            int stagedPiles = CountThreeColorPiles(piece);
            builder.Append(stagedPiles switch
            {
                0 => FaText.Get("Finished"),
                3 => FaText.Get("PREVIEW - 3/3 cloth piles staged; use shears to finish"),
                _ => FaText.Get("PREVIEW - {0}/3 cloth piles staged; stage all three, then use shears to finish", stagedPiles)
            });
            return builder.ToString();
        }

        if (armorInfo.Definition.Schema != ArmorAttributeSchema.Layered
            || !ArmorCompatibility.TryReadLayered(types, piece, out LayeredArmorState layeredState))
        {
            builder.AppendLine();
            builder.Append(FaText.Get("Layered state is malformed"));
            return builder.ToString();
        }

        string decoration = FormatStationValue(layeredState.Decoration);
        string color = FormatStationValue(layeredState.Color);
        bool decorationPreview = HasAppliedLayeredPreview(piece, "decoration")
            && !string.Equals(layeredState.Decoration, GetPendingOriginalValue(piece, "decoration"), StringComparison.Ordinal);
        bool colorPreview = HasAppliedLayeredPreview(piece, "color")
            && !string.Equals(layeredState.Color, GetPendingOriginalValue(piece, "color"), StringComparison.Ordinal);
        bool hasPreview = decorationPreview || colorPreview;
        bool hasStoredMaterial = HasPendingDecorationEditsLayered(piece) && !hasPreview;
        bool hasBaked = !hasPreview
            && (!string.Equals(layeredState.Decoration, ArmorCompatibility.LayeredNeutral, StringComparison.Ordinal)
                || !string.Equals(layeredState.Color, ArmorCompatibility.LayeredNeutral, StringComparison.Ordinal));

        builder.AppendLine();
        builder.Append(FaText.Get("Decoration: "));
        builder.Append(decoration);
        builder.AppendLine();
        builder.Append(FaText.Get("Color: "));
        builder.Append(color);
        builder.AppendLine();
        builder.Append(hasPreview
            ? FaText.Get("PREVIEW - not baked; use a hammer to finish")
            : hasStoredMaterial
            ? hasBaked
                ? FaText.Get("Finished - staged material is stored separately and has not changed this armor")
                : FaText.Get("Unfinished - staged material is stored separately; add a decoration to preview it")
            : hasBaked ? FaText.Get("Finished") : FaText.Get("Unfinished"));
        return builder.ToString();
    }

    private string DescribeTrimElementState(string actionName)
    {
        return actionName switch
        {
            "Armor" => DescribeTrimArmorState(),
            "RivetsPlace" => DescribeTrimRivetsState(),
            "CruciblePlace" => DescribeTrimCrucibleState(),
            "SolderHolder" => DescribeTrimSolderingIronState(),
            _ => ""
        };
    }

    private string DescribeTrimArmorState()
    {
        var builder = new StringBuilder();
        builder.Append(FaText.Get("Mounted: "));
        builder.Append(trimArmorStack?.GetName() ?? FaText.Get("empty"));

        if (trimArmorStack == null)
        {
            return builder.ToString();
        }

        string trim = "none";
        if (TryGetFAArmorInfo(trimArmorStack, out ResolvedArmor armorInfo))
        {
            ITreeAttribute? types = trimArmorStack.Attributes?.GetTreeAttribute("types");
            trim = FormatStationValue(types?.GetString("strip" + armorInfo.Piece, "none") ?? "none");
        }

        bool hasPending = pendingTrimRivetsStack != null;
        bool hasBaked = !hasPending && trim != "none";

        builder.AppendLine();
        builder.Append(FaText.Get("Rivets: "));
        builder.Append(trim);
        builder.AppendLine();
        builder.Append(FaText.Get("Status: "));
        builder.Append(hasPending ? FaText.Get("PREVIEW - not baked; use a soldering iron to finish") : hasBaked ? FaText.Get("Finished") : FaText.Get("Unfinished"));
        return builder.ToString();
    }

    private string DescribeTrimRivetsState()
    {
        var builder = new StringBuilder();
        builder.Append(FaText.Get("Rims and rivets: "));
        builder.Append(trimRivetsStack?.GetName() ?? FaText.Get("empty"));

        if (pendingTrimRivetsStack != null)
        {
            builder.AppendLine();
            builder.Append(FaText.Get("Staged: "));
            builder.Append(pendingTrimRivetsStack.GetName());
            builder.Append(FaText.Get(" (PREVIEW - not baked; use a soldering iron to finish)"));
        }

        return builder.ToString();
    }

    private string DescribeTrimCrucibleState()
    {
        var builder = new StringBuilder();
        builder.Append(FaText.Get("Crucible: "));
        builder.Append(trimCrucibleStack?.GetName() ?? FaText.Get("empty"));

        if (trimCrucibleStack == null)
        {
            builder.AppendLine();
            builder.Append(FaText.Get("Silver Solder: 0 / "));
            builder.Append(TrimSolderWeldAmount);
            builder.Append(FaText.Get(" units"));
            builder.AppendLine();
            builder.Append(FaText.Get("Status: Not Ok"));
            return builder.ToString();
        }

        if (!TryGetSolderContent(trimCrucibleStack, out SolderContent solderContent, logDebug: false))
        {
            builder.AppendLine();
            builder.Append(FaText.Get("Silver Solder: unreadable / "));
            builder.Append(TrimSolderWeldAmount);
            builder.Append(FaText.Get(" units"));
            builder.AppendLine();
            builder.Append(FaText.Get("Status: Not Ok"));
            return builder.ToString();
        }

        bool ready = solderContent.Amount >= TrimSolderWeldAmount
            && (solderContent.Metal != "silver" || solderContent.Temperature >= TrimSolderSilverMinTemperature);

        builder.AppendLine();
        builder.Append(FaText.Get("Silver Solder: "));
            builder.Append(solderContent.Amount);
            builder.Append(" / ");
            builder.Append(TrimSolderWeldAmount);
            builder.Append(FaText.Get(" units"));
        builder.AppendLine();
        builder.Append(FaText.Get("Metal: "));
        builder.Append(FormatStationValue(solderContent.Metal));
        builder.AppendLine();
        builder.Append(FaText.Get("Temperature: "));
        builder.Append(solderContent.Temperature.ToString("0"));
        builder.Append("C");
        builder.AppendLine();
        builder.Append(FaText.Get("Status: "));
        builder.Append(ready ? FaText.Get("Ok") : FaText.Get("Not Ok"));
        return builder.ToString();
    }

    private string DescribeTrimSolderingIronState()
    {
        if (trimSolderingIronStack == null) return FaText.Get("Soldering iron: empty");

        int durability = trimSolderingIronStack.Collectible.GetRemainingDurability(trimSolderingIronStack);
        int maxDurability = trimSolderingIronStack.Collectible.GetMaxDurability(trimSolderingIronStack);
        string readiness = durability > 0 ? FaText.Get("Ready") : FaText.Get("Broken");

        return FaText.Get("Soldering iron: {0}\nDurability: {1}/{2}\nStatus: {3}", trimSolderingIronStack.GetName(), durability, maxDurability, readiness);
    }

    private static string DescribeStorageSlotState(string label, ItemStack? stack)
    {
        return label + ": " + (stack?.GetName() ?? FaText.Get("empty"));
    }

    private static string FormatStationValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return FaText.Get("none");
        }

        return value switch
        {
            "bismuthbronze" => FaText.Get("bismuth bronze"),
            "blackbronze" => FaText.Get("black bronze"),
            "tinbronze" => FaText.Get("tin bronze"),
            "meteoriciron" => FaText.Get("meteoric iron"),
            "blistersteel" => FaText.Get("blister steel"),
            _ => FaText.Value(value)
        };
    }

    private void AppendStationDisplayInfo(StringBuilder builder)
    {
        builder.Append(DescribeCoverCauldronState());
        builder.AppendLine();
        builder.Append(DescribeCoverFuelState());
    }

    private string DescribeCoverCauldronState()
    {
        var builder = new StringBuilder();
        if (liquidStack == null || liquidStack.StackSize <= 0)
        {
            builder.Append(FaText.Get("Cauldron: empty"));
            builder.AppendLine();
            builder.Append(FaText.Get("Add 10L sulfuric acid or coating liquid."));
        }
        else
        {
            builder.Append(FaText.Get("Cauldron: "));
            builder.Append(FormatLitres(liquidStack.StackSize));
            builder.Append("/");
            builder.Append(LiquidCapacityLitres);
            builder.Append(FaText.Get("L "));
            builder.Append(GetLiquidName(liquidStack));
            builder.AppendLine();
            builder.Append(FaText.Get("Item: "));
            builder.Append(immersedStack?.GetName() ?? "none");
            if (immersedStack == null)
            {
                builder.AppendLine();
                builder.Append(IsSulfuricAcid(liquidStack) ? FaText.Get("Add a metal plate or coated armor.") : FaText.Get("Add uncoated armor."));
            }
        }

        if (HasActiveProcess())
        {
            builder.AppendLine();
            builder.Append(processMode == ProcessPlateResting ? FaText.Get("Plate resting: ") : processMode == ProcessArmorDissolving ? FaText.Get("Coating removal: ") : FaText.Get("Armor coating: "));
            builder.Append(FormatRemainingProcessTime());
        }
        return builder.ToString();
    }

    private string DescribeCoverFuelState()
    {
        var builder = new StringBuilder();
        builder.Append(FaText.Get("Fuel: "));
        builder.Append(fuelStack?.StackSize ?? 0);
        builder.Append("/");
        builder.Append(MaxCharcoalPieces);
        builder.Append(fuelLit ? FaText.Get(" (lit)") : FaText.Get(" (unlit)"));
        builder.AppendLine();
        builder.Append(FaText.Get("Use coal or charcoal."));
        if (!fuelLit && (fuelStack?.StackSize ?? 0) >= MaxCharcoalPieces)
        {
            builder.AppendLine();
            builder.Append(CanIgnitePreparedFuel ? FaText.Get("Use a torch or firestarter.") : FaText.Get("Add coating liquid and armor; close lid."));
        }
        return builder.ToString();
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
    public int FuelCount => fuelStack?.StackSize ?? 0;
    public int FuelCapacity => MaxCharcoalPieces;
    public bool CanIgnitePreparedFuel => CanStartFuelIgnition(out _, out _, out _);
    public bool CanGuideMetalPlateInsertion => immersedStack == null
        && !HasActiveProcess()
        && IsSulfuricAcid(liquidStack)
        && (liquidStack?.StackSize ?? 0) >= LiquidCapacityItems;
    public bool CanGuideArmorInsertion => immersedStack == null
        && !HasActiveProcess()
        && TryGetCoatingDoseMetal(liquidStack, out _);
    public string CoatingMetal => TryGetCoatingMetal(liquidStack, out string metal) ? metal : "";

    public bool CanAcceptMetalPlateForAcid(ItemStack? stack)
    {
        return CanGuideMetalPlateInsertion
            && TryGetMetalPlateMetal(stack, out string metal)
            && Api.World.GetItem(CoatingLiquidCode(metal)) != null;
    }

    public bool CanAcceptArmorForCoating(ItemStack? stack)
    {
        return CanGuideArmorInsertion
            && TryGetCoatingDoseMetal(liquidStack, out string metal)
            && TryGetFAArmorInfo(stack, out ResolvedArmor armorInfo)
            && CanApplyArmorCoating(stack, armorInfo, metal, out _);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        RefreshRenderSnapshot();
        if (api.Side == EnumAppSide.Server && Block is BlockFAStation station)
        {
            station.EnsureStationStructure(api.World, Pos);
        }

        if (api.Side == EnumAppSide.Client && (IsDecorationStationBlock() || IsTrimStationBlock()))
        {
            RegisterGameTickListener(OnPreviewParticleTick, 25);
        }

        if (!IsCoverStationBlock())
        {
            return;
        }

        AnimDebugLog($"Initialize side={api.Side}, block={Block?.Code}, behaviors={FormatBehaviors()}");

        if (api.Side == EnumAppSide.Client)
        {
            CacheSulfurTexture();
            if (liquidStack != null) TryCacheLiquidTexture(liquidStack, out _, out _);
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
        for (int i = 1; i <= 4; i++) SetOrRemoveItemstack(tree, $"coverPlate{i}Stack", GetCoverPlateStack(i));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        if (IsDecorationStationBlock())
        {
            ReadDecorationTreeAttributes(tree, worldForResolving);
            RefreshRenderSnapshot();
            if (worldForResolving.Side == EnumAppSide.Client)
            {
                worldForResolving.BlockAccessor.MarkBlockDirty(Pos);
                BlockPos? proxyPos = GetProxyPartPos();
                if (proxyPos != null)
                {
                    worldForResolving.BlockAccessor.MarkBlockDirty(proxyPos);
                }
            }
            return;
        }

        if (IsTrimStationBlock())
        {
            ReadTrimTreeAttributes(tree, worldForResolving);
            RefreshRenderSnapshot();
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
        for (int i = 1; i <= 4; i++) SetCoverPlateStack(i, ResolveItemstack(tree.GetItemstack($"coverPlate{i}Stack"), worldForResolving));
        RefreshRenderSnapshot();

        if (Api?.Side == EnumAppSide.Client)
        {
            CacheSulfurTexture();
            if (liquidStack != null) TryCacheLiquidTexture(liquidStack, out _, out _);
            CacheCharcoalTexture();
            EnsureAnimator();
            SyncAnimations();
            SyncProcessLoopSound();
        }
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        bool skipDefaultMesh = base.OnTesselation(mesher, tessThreadTesselator);
        WorkStationRenderSnapshot snapshot = Volatile.Read(ref renderSnapshot);
        if (IsDecorationStationBlock())
        {
            AddDecorationStationMeshes(mesher, tessThreadTesselator, snapshot);
            return skipDefaultMesh;
        }

        if (IsTrimStationBlock())
        {
            AddTrimStationMeshes(mesher, tessThreadTesselator, snapshot);
            return skipDefaultMesh;
        }

        if (!IsCoverStationBlock())
        {
            return skipDefaultMesh;
        }

        if (snapshot.LidOpen || snapshot.FuelOpen)
        {
            skipDefaultMesh = true;
        }

        if (IsStationFuel(snapshot.Fuel))
        {
            MeshData? fuelMesh = CreateFuelMesh(tessThreadTesselator, snapshot.Fuel, snapshot.FuelLit);
            if (fuelMesh != null)
            {
                DebugFuelLog($"OnTesselation adding fuel mesh. vertices={fuelMesh.VerticesCount}, indices={fuelMesh.IndicesCount}");
                mesher.AddMeshData(fuelMesh, 1);
            }
        }

        if (snapshot.Table != null)
        {
            MeshData? tableMesh = TryCreateTableItemMesh(tessThreadTesselator, snapshot.Table);
            if (tableMesh != null)
            {
                mesher.AddMeshData(tableMesh, 1);
            }
        }

        AddCoverPlateMeshes(mesher, tessThreadTesselator, snapshot.CoverPlates);

        bool hasVisibleLiquid = snapshot.Liquid != null && IsCauldronLiquid(snapshot.Liquid) && snapshot.Liquid.StackSize > 0;
        if (!hasVisibleLiquid)
        {
            DebugLiquidLog($"OnTesselation no visible liquid. skipDefaultMesh={skipDefaultMesh}, liquidStack={FormatStackDebug(snapshot.Liquid)}");
            MeshData? dryImmersedMesh = CreateImmersedItemMesh(tessThreadTesselator, snapshot.Immersed, hasVisibleLiquid: false);
            if (dryImmersedMesh != null)
            {
                DebugLiquidLog($"OnTesselation adding dry immersed item mesh. vertices={dryImmersedMesh.VerticesCount}, indices={dryImmersedMesh.IndicesCount}, bounds={FormatMeshBounds(dryImmersedMesh)}");
                mesher.AddMeshData(dryImmersedMesh, 1);
            }

            return skipDefaultMesh;
        }

        DebugLiquidLog($"OnTesselation liquid present. skipDefaultMesh={skipDefaultMesh}, liquidStack={FormatStackDebug(snapshot.Liquid)}");
        MeshData? liquidMesh = CreateLiquidMesh(tessThreadTesselator, snapshot.Liquid);
        if (liquidMesh == null)
        {
            DebugLiquidLog("OnTesselation liquid mesh is null.");
            return skipDefaultMesh;
        }

        DebugLiquidLog($"OnTesselation adding liquid mesh. vertices={liquidMesh.VerticesCount}, indices={liquidMesh.IndicesCount}, renderPasses={liquidMesh.RenderPassCount}, needsLiquid={liquidMesh.NeedsRenderPass(EnumChunkRenderPass.Liquid)}, needsTransparent={liquidMesh.NeedsRenderPass(EnumChunkRenderPass.Transparent)}");
        mesher.AddMeshData(liquidMesh, 1);
        DebugLiquidLog("OnTesselation liquid mesh added to terrain pool.");

        MeshData? immersedMesh = CreateImmersedItemMesh(tessThreadTesselator, snapshot.Immersed, hasVisibleLiquid: true);
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

        for (int i = 1; i <= 4; i++)
        {
            ItemStack? plateStack = GetCoverPlateStack(i);
            if (plateStack != null) Api.World.SpawnItemEntity(plateStack, Pos.ToVec3d().Add(0.5, 1.05, 0.5));
            SetCoverPlateStack(i, null);
        }
    }

    public override void OnBlockRemoved()
    {
        Volatile.Write(ref renderSnapshot, WorkStationRenderSnapshot.Empty);
        if (!IsCoverStationBlock())
        {
            base.OnBlockRemoved();
            return;
        }

        StopProcessLoopSound(immediate: true);
        ClearRenderTextureCaches();
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        Volatile.Write(ref renderSnapshot, WorkStationRenderSnapshot.Empty);
        if (!IsCoverStationBlock())
        {
            base.OnBlockUnloaded();
            return;
        }

        StopProcessLoopSound(immediate: true);
        ClearRenderTextureCaches();
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

        if (IsCoverStationBlock())
        {
            dsc.Clear();
            if (TryAppendSelectedElementInfo(forPlayer, dsc))
            {
                return;
            }

            base.GetBlockInfo(forPlayer, dsc);
            if (dsc.Length > 0)
            {
                dsc.AppendLine();
            }
            AppendStationDisplayInfo(dsc);
            return;
        }

        base.GetBlockInfo(forPlayer, dsc);
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
        return Block is BlockFAStation station
            && station.TryResolveSelectionAction(Api.World.BlockAccessor, Pos, selection, out actionName);
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
            Notify(byPlayer, FaText.Get("Hold coal or charcoal to fuel the station."));
            return;
        }

        if (fuelLit)
        {
            Notify(byPlayer, FaText.Get("The fuel is already burning."));
            return;
        }

        if (fuelStack != null && !fuelStack.Equals(Api.World, heldStack, GlobalConstants.IgnoredStackAttributes))
        {
            Notify(byPlayer, FaText.Get("Remove the current fuel before adding a different fuel type."));
            return;
        }

        int room = MaxCharcoalPieces - (fuelStack?.StackSize ?? 0);
        if (room <= 0)
        {
            Notify(byPlayer, FaText.Get("The fuel tray is already full."));
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
            Notify(byPlayer, FaText.Get("The fuel tray is empty."));
            return;
        }

        if (fuelLit)
        {
            Notify(byPlayer, FaText.Get("The fuel is burning and cannot be removed."));
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
                Notify(byPlayer, FaText.Get("The table is already holding an item."));
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
            Notify(byPlayer, FaText.Get("The table is empty."));
            return;
        }

        tableStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {tableStack.GetName()} on the table.");
    }

    private void TryInteractCoverPlate(IPlayer byPlayer, string actionName)
    {
        int index = actionName[^1] - '0';
        ItemStack? storedStack = GetCoverPlateStack(index);
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (storedStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, FaText.Get("Plate slot {0} is already occupied.", index));
                return;
            }

            SetCoverPlateStack(index, null);
            GiveOrDrop(byPlayer, storedStack, 1.1);
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, $"Removed {storedStack.GetName()} from plate slot {index}.");
            return;
        }

        if (activeSlot == null || heldStack == null || !IsMetalPlate(heldStack))
        {
            Notify(byPlayer, FaText.Get("Place an in-game metal plate in this slot."));
            return;
        }

        ItemStack inserted = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        SetCoverPlateStack(index, inserted);
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {inserted.GetName()} in plate slot {index}.");
    }

    private static bool IsMetalPlate(ItemStack? stack) =>
        stack?.Collectible?.Code?.Path.StartsWith("metalplate-", StringComparison.Ordinal) == true;

    private ItemStack? GetCoverPlateStack(int index) => index switch
    {
        1 => coverPlate1Stack,
        2 => coverPlate2Stack,
        3 => coverPlate3Stack,
        4 => coverPlate4Stack,
        _ => null
    };

    private void SetCoverPlateStack(int index, ItemStack? stack)
    {
        switch (index)
        {
            case 1: coverPlate1Stack = stack; break;
            case 2: coverPlate2Stack = stack; break;
            case 3: coverPlate3Stack = stack; break;
            case 4: coverPlate4Stack = stack; break;
        }
    }

    private void HandleDecorationElementInteraction(IPlayer byPlayer, string actionName)
    {
        switch (actionName)
        {
            case "HeadPlace":
                TryInteractDecorationArmorSlot(byPlayer, actionName, "head", "helmet");
                return;
            case "BodyPlace":
                TryInteractDecorationArmorSlot(byPlayer, actionName, "body", "chestplate");
                return;
            case "LegsPlace":
                TryInteractDecorationArmorSlot(byPlayer, actionName, "legs", "leggings");
                return;
            case "HeadDecorations":
                TryInteractDecorationWorkArea(byPlayer, "head", "helmet");
                return;
            case "BodyDecorations":
                TryInteractDecorationWorkArea(byPlayer, "body", "chestplate");
                return;
            case "LegsDecorations":
                TryInteractDecorationWorkArea(byPlayer, "legs", "leggings");
                return;

            default:
                Notify(byPlayer, FaText.Get("This part of the decoration station cannot be used right now."));
                return;
        }
    }

    private void TryInteractDecorationWorkArea(IPlayer byPlayer, string piece, string slotName)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;
        ItemStack? armorStack = GetDecorationArmorStack(piece);

        if (heldStack == null)
        {
            if (armorStack == null)
            {
                if (!TryReturnOrphanedDecorationMaterial(byPlayer, piece))
                {
                    Notify(byPlayer, FaText.Get("The {0} decoration area has no staged material.", FaText.Get(slotName)));
                }
                return;
            }
            if (!TryRemovePendingDecorationMaterial(byPlayer, armorStack, piece))
            {
                Notify(byPlayer, FaText.Get("The {0} decoration area has no staged material.", FaText.Get(slotName)));
            }
            return;
        }

        if (TryGetClothColor(heldStack, out string clothColor))
        {
            if (armorStack == null) TryStoreThreeColorCloth(byPlayer, activeSlot!, heldStack, piece, clothColor);
            else TryStageThreeColorCloth(byPlayer, activeSlot!, heldStack, armorStack, piece, slotName, clothColor);
            return;
        }

        if (armorStack == null) TryStoreLayeredMaterial(byPlayer, activeSlot!, heldStack, piece, slotName);
        else TryStageDecorationMaterial(byPlayer, activeSlot!, heldStack, armorStack, piece, slotName);
    }

    public bool DecorationSuppliesOwnColor(string piece)
    {
        if (TryGetIntrinsicDecorationColor(GetPendingDecorationMaterial(piece, "decoration"), piece, out _)) return true;
        string decoration = GetDecorationArmorStack(piece)?.Attributes?.GetTreeAttribute("types")
            ?.GetString("decoration" + piece, "none") ?? "none";
        return decoration is "bear" or "wolf" || decoration.StartsWith("metallicus-", StringComparison.Ordinal);
    }

    private void TryStoreLayeredMaterial(IPlayer byPlayer, ItemSlot activeSlot, ItemStack heldStack, string piece, string slotName)
    {
        if (HasThreeColorCloth(piece))
        {
            Notify(byPlayer, FaText.Get("Remove the staged cloth piles before selecting Layered decoration materials."));
            return;
        }
        if (!TryParseDecorationMaterial(heldStack, piece, out string kind, out _, out string failure))
        {
            Notify(byPlayer, failure.Length > 0 ? failure : FaText.Get("That material does not belong in the {0} decoration area.", FaText.Get(slotName)));
            return;
        }
        if (kind == "color" && DecorationSuppliesOwnColor(piece))
        {
            Notify(byPlayer, FaText.Get("This decoration supplies its own color."));
            return;
        }
        if (GetPendingDecorationMaterial(piece, kind) != null)
        {
            Notify(byPlayer, FaText.Get("This area already has a staged {0}.", GetDecorationEditName(kind)));
            return;
        }
        if (TryGetIntrinsicDecorationColor(heldStack, piece, out _) && GetPendingDecorationMaterial(piece, "color") != null)
        {
            Notify(byPlayer, FaText.Get("Remove the staged color kit before inserting this decoration."));
            return;
        }

        ItemStack inserted = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        SetPendingDecorationMaterial(piece, kind, inserted);
        SetPendingOriginalValue(piece, kind, "");
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {inserted.GetName()} in the {slotName} decoration area.");
    }

    private void TryStoreThreeColorCloth(IPlayer byPlayer, ItemSlot activeSlot, ItemStack heldStack, string piece, string color)
    {
        if (HasPendingDecorationEditsLayered(piece))
        {
            Notify(byPlayer, FaText.Get("Remove the staged Layered materials before adding cloth."));
            return;
        }
        if (heldStack.StackSize < 3)
        {
            Notify(byPlayer, FaText.Get("A cloth pile requires three identical cloth items."));
            return;
        }
        if (!threeColorState.TryGetFirstEmptySlot(piece, out int index))
        {
            Notify(byPlayer, FaText.Get("All three cloth piles are already staged."));
            return;
        }
        ItemStack? inserted = activeSlot.TakeOut(3);
        activeSlot.MarkDirty();
        if (inserted == null || !threeColorState.TrySetClothIfEmpty(piece, index, inserted))
        {
            if (inserted != null) GiveOrDrop(byPlayer, inserted, 1.1);
            Notify(byPlayer, FaText.Get("The selected cloth slot changed before the material could be stored."));
            return;
        }
        InvalidateDecorationOperation(piece);
        SetThreeColorOriginal(piece, index, "");
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed a three-item {color} cloth pile in Color{index}.");
    }

    private bool TryReturnOrphanedDecorationMaterial(IPlayer byPlayer, string piece)
    {
        bool hasUnavailableCloth = false;
        for (int i = DecorationThreeColorState.ColorsPerPiece; i >= 1; i--)
        {
            ItemStack? cloth = GetThreeColorCloth(piece, i);
            if (cloth == null) continue;
            if (cloth.Collectible == null && !cloth.ResolveBlockOrItem(Api.World))
            {
                hasUnavailableCloth = true;
                continue;
            }
            SetThreeColorCloth(piece, i, null);
            SetThreeColorOriginal(piece, i, "");
            GiveOrDrop(byPlayer, cloth, 1.1);
            MarkStationDirty();
            return true;
        }
        if (hasUnavailableCloth)
        {
            Notify(byPlayer, FaText.Get("A stored cloth pile is unavailable. Restore its providing mod before retrieving it."));
            return true;
        }
        foreach (string kind in new[] { "color", "decoration" })
        {
            ItemStack? material = GetPendingDecorationMaterial(piece, kind);
            if (material == null) continue;
            SetPendingDecorationMaterial(piece, kind, null);
            SetPendingOriginalValue(piece, kind, "");
            GiveOrDrop(byPlayer, material, 1.1);
            MarkStationDirty();
            return true;
        }
        return false;
    }

    private void TryInteractDecorationArmorSlot(IPlayer byPlayer, string actionName, string expectedPiece, string slotName)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;
        ItemStack? storedStack = GetDecorationArmorStack(actionName);

        if (storedStack != null)
        {
            if (IsToolType(heldStack, "hammer"))
            {
                TryBakeLayeredDecoration(byPlayer, storedStack, expectedPiece, slotName);
                return;
            }

            if (IsScissors(heldStack))
            {
                TryBakeThreeColorDecoration(byPlayer, storedStack, expectedPiece, slotName);
                return;
            }

            if (IsToolType(heldStack, "saw"))
            {
                if (!TryRemoveBakedDecorationWithSaw(byPlayer, storedStack, expectedPiece, slotName))
                {
                    Notify(byPlayer, FaText.Get("There is no baked decoration to remove from this {0}.", FaText.Get(slotName)));
                }

                return;
            }

            if (heldStack != null)
            {
                Notify(byPlayer, FaText.Get("Use the {0} decoration area to stage materials.", FaText.Get(slotName)));
                return;
            }

            if (HasPendingDecorationEdits(expectedPiece))
            {
                RestoreAllPendingDecorationPreviews(storedStack, expectedPiece);
                ClearDecorationOriginalValues(expectedPiece);
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
            Notify(byPlayer, FaText.Get("The armor stand has no {0}.", FaText.Get(slotName)));
            return;
        }

        if (!TryGetFAArmorInfo(heldStack, out ResolvedArmor mountedArmor) || mountedArmor.Piece != expectedPiece)
        {
            Notify(byPlayer, FaText.Get("Place a Forgotten Armory {0} here.", FaText.Get(slotName)));
            return;
        }

        if (!ArmorCompatibility.ValidateStateContainer(heldStack, mountedArmor, out _).Allowed)
        {
            Notify(byPlayer, FaText.Get("This known {0} has a malformed customization state.", FaText.Get(slotName)));
            return;
        }

        if (!CanMountArmorWithStagedMaterials(heldStack, expectedPiece, out string stagedFailure))
        {
            Notify(byPlayer, stagedFailure);
            return;
        }

        ItemStack inserted = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        SetDecorationArmorStack(actionName, inserted);
        ApplyStoredDecorationPreviews(inserted, expectedPiece);
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {inserted.GetName()} on the armor stand.");
    }

    private bool CanMountArmorWithStagedMaterials(ItemStack armorStack, string piece, out string failure)
    {
        failure = "";
        if (HasBakedDecorationIgnoringPreview(armorStack, piece))
        {
            if (HasPendingDecorationEdits(piece))
            {
                failure = FaText.Get("Please remove all decorative ingredients from the shelves");
                return false;
            }

            return true;
        }

        if (HasThreeColorCloth(piece))
        {
            if (!UsesThreeColorSchema(armorStack, piece))
            {
                failure = FaText.Get("The staged cloth piles require ThreeColor armor. Remove them or mount compatible armor.");
                return false;
            }
            return true;
        }
        if (!HasPendingDecorationEditsLayered(piece)) return true;
        if (!UsesLayeredDecorationSchema(armorStack, piece)
            || !TryGetFAArmorInfo(armorStack, out ResolvedArmor info))
        {
            failure = FaText.Get("The staged decoration materials require compatible Layered armor.");
            return false;
        }
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (GetPendingDecorationMaterial(piece, "decoration") != null
            && !string.Equals(types?.GetString("decoration" + piece, "none"), "none", StringComparison.Ordinal))
        {
            failure = FaText.Get("Please remove all decorative ingredients from the shelves");
            return false;
        }
        if (GetPendingDecorationMaterial(piece, "color") != null
            && !string.Equals(types?.GetString("color" + piece, "none"), "none", StringComparison.Ordinal))
        {
            failure = FaText.Get("Please remove all decorative ingredients from the shelves");
            return false;
        }
        ItemStack? decorationStack = GetPendingDecorationMaterial(piece, "decoration");
        if (decorationStack != null
            && TryParseDecorationMaterial(decorationStack, piece, out _, out string value, out _)
            && !ArmorCompatibility.ValidateDecoration(info, value).Allowed)
        {
            failure = FaText.Get("The staged decoration does not fit this armor piece.");
            return false;
        }
        if (decorationStack != null && TryGetSelfColoredDecoration(decorationStack, piece, out string suppliedDecoration, out string suppliedColor))
        {
            if (GetPendingDecorationMaterial(piece, "color") != null)
            {
                failure = FaText.Get("This decoration supplies its own color.");
                return false;
            }
            if (!string.Equals(types?.GetString("color" + piece, "none"), "none", StringComparison.Ordinal))
            {
                failure = FaText.Get("Please remove all decorative ingredients from the shelves");
                return false;
            }
            if (!ArmorCompatibility.ValidateDecorationColor(info, suppliedDecoration, suppliedColor).Allowed)
            {
                failure = FaText.Get("This decoration color does not fit this armor piece.");
                return false;
            }
        }
        return true;
    }

    private void ApplyStoredDecorationPreviews(ItemStack armorStack, string piece)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null || HasBakedDecorationIgnoringPreview(armorStack, piece)) return;
        if (UsesThreeColorSchema(armorStack, piece))
        {
            for (int i = 1; i <= DecorationThreeColorState.ColorsPerPiece; i++)
            {
                ItemStack? cloth = GetThreeColorCloth(piece, i);
                if (cloth == null || !TryGetClothColor(cloth, out string color)) continue;
                string key = new DecorationColorSlot(piece, i).ArmorAttributeKey;
                SetThreeColorOriginal(piece, i, types.GetString(key, "plain") ?? "plain");
                types.SetString(key, color);
            }
            return;
        }
        foreach (string kind in new[] { "decoration", "color" })
        {
            if (kind == "color" && GetPendingDecorationMaterial(piece, "decoration") == null) continue;
            ItemStack? material = GetPendingDecorationMaterial(piece, kind);
            if (material == null || !TryParseDecorationMaterial(material, piece, out _, out string value, out _)) continue;
            string key = kind + piece;
            SetPendingOriginalValue(piece, kind, types.GetString(key, "none") ?? "none");
            types.SetString(key, value);
            if (kind == "decoration" && TryGetIntrinsicDecorationColor(material, piece, out string suppliedColor))
            {
                string colorKey = "color" + piece;
                SetPendingOriginalValue(piece, "color", types.GetString(colorKey, "none") ?? "none");
                types.SetString(colorKey, suppliedColor);
            }
        }
    }

    private void ClearDecorationOriginalValues(string piece)
    {
        SetPendingOriginalValue(piece, "decoration", "");
        SetPendingOriginalValue(piece, "color", "");
        threeColorState.ClearOriginals(piece);
    }

    private void TryStageDecorationMaterial(IPlayer byPlayer, ItemSlot activeSlot, ItemStack heldStack, ItemStack armorStack, string expectedPiece, string slotName)
    {
        if (HasThreeColorCloth(expectedPiece))
        {
            Notify(byPlayer, FaText.Get("Remove the staged cloth piles before selecting Layered decoration materials."));
            return;
        }

        if (!UsesLayeredDecorationSchema(armorStack, expectedPiece))
        {
            Notify(byPlayer, FaText.Get("This {0} uses the ThreeColor cloth workflow.", FaText.Get(slotName)));
            return;
        }

        if (!TryParseDecorationMaterial(heldStack, expectedPiece, out string editKind, out string value, out string failure))
        {
            Notify(byPlayer, failure.Length > 0 ? failure : FaText.Get("That material does not belong on this {0}.", FaText.Get(slotName)));
            return;
        }
        bool hasIntrinsicColor = TryGetIntrinsicDecorationColor(heldStack, expectedPiece, out string suppliedColor);
        ItemStack? pendingDecoration = GetPendingDecorationMaterial(expectedPiece, "decoration");
        if (editKind == "color" && pendingDecoration != null && TryGetIntrinsicDecorationColor(pendingDecoration, expectedPiece, out _))
        {
            Notify(byPlayer, FaText.Get("This decoration supplies its own color."));
            return;
        }

        if (!TryGetFAArmorInfo(armorStack, out ResolvedArmor armorInfo) || armorInfo.Piece != expectedPiece)
        {
            Notify(byPlayer, FaText.Get("Place a Forgotten Armory {0} here first.", FaText.Get(slotName)));
            return;
        }

        if (armorInfo.Definition.Schema != ArmorAttributeSchema.Layered)
        {
            Notify(byPlayer, FaText.Get("This {0} does not use Layered decorations.", FaText.Get(slotName)));
            return;
        }

        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            Notify(byPlayer, FaText.Get("This armor piece cannot be decorated."));
            return;
        }

        if (GetPendingDecorationMaterial(expectedPiece, editKind) != null)
        {
            Notify(byPlayer, FaText.Get("This {0} already has a staged {1}. Remove it first.", FaText.Get(slotName), GetDecorationEditName(editKind)));
            return;
        }
        if (hasIntrinsicColor && GetPendingDecorationMaterial(expectedPiece, "color") != null)
        {
            Notify(byPlayer, FaText.Get("Remove the staged color kit before using this decoration on this {0}.", FaText.Get(slotName)));
            return;
        }

        string attrKey = editKind + expectedPiece;
        string currentValue = types.GetString(attrKey) ?? "none";
        if (!string.Equals(currentValue, "none", StringComparison.Ordinal))
        {
            Notify(byPlayer, FaText.Get("This {0} already has {1} baked in.", FaText.Get(slotName), GetDecorationEditName(editKind)));
            return;
        }
        if (hasIntrinsicColor && !string.Equals(types.GetString("color" + expectedPiece, "none"), "none", StringComparison.Ordinal))
        {
            Notify(byPlayer, FaText.Get("This {0} already has a decoration color baked in.", FaText.Get(slotName)));
            return;
        }

        if (editKind == "color")
        {
            string currentDecoration = types.GetString("decoration" + expectedPiece, "none") ?? "none";
            if (!ArmorCompatibility.ValidateDecorationColor(armorInfo, currentDecoration, value).Allowed)
            {
                Notify(byPlayer, FaText.Get("That color is not compatible with the staged {0} decoration.", currentDecoration));
                return;
            }
        }
        else
        {
            ItemStack? stagedColor = GetPendingDecorationMaterial(expectedPiece, "color");
            if (stagedColor != null
                && TryParseDecorationMaterial(stagedColor, expectedPiece, out _, out string stagedColorValue, out _)
                && !ArmorCompatibility.ValidateDecorationColor(armorInfo, value, stagedColorValue).Allowed)
            {
                Notify(byPlayer, FaText.Get("The staged color is not compatible with that decoration."));
                return;
            }
        }

        if (editKind == "decoration"
            && !ArmorCompatibility.ValidateDecoration(armorInfo, value).Allowed)
        {
            Notify(byPlayer, FaText.Get("This decoration does not fit the {0}.", FaText.Get(slotName)));
            return;
        }
        if (hasIntrinsicColor && !ArmorCompatibility.ValidateDecorationColor(armorInfo, value, suppliedColor).Allowed)
        {
            Notify(byPlayer, FaText.Get("This decoration color does not fit the {0}.", FaText.Get(slotName)));
            return;
        }

        if (editKind == "decoration"
            && GetPendingDecorationMaterial(expectedPiece, "color") != null
            && !string.Equals(types.GetString("color" + expectedPiece, "none"), "none", StringComparison.Ordinal))
        {
            Notify(byPlayer, FaText.Get("This {0} already has a decoration color baked in.", FaText.Get(slotName)));
            return;
        }

        ItemStack inserted = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        SetPendingOriginalValue(expectedPiece, editKind, currentValue);
        SetPendingDecorationMaterial(expectedPiece, editKind, inserted);
        if (editKind == "decoration")
        {
            types.SetString(attrKey, value);
            if (hasIntrinsicColor)
            {
                string colorKey = "color" + expectedPiece;
                SetPendingOriginalValue(expectedPiece, "color", types.GetString(colorKey, "none") ?? "none");
                types.SetString(colorKey, suppliedColor);
            }
            else
            {
                ItemStack? stagedKit = GetPendingDecorationMaterial(expectedPiece, "color");
                if (stagedKit != null && TryParseDecorationMaterial(stagedKit, expectedPiece, out _, out string kitColor, out _))
                {
                    string colorKey = "color" + expectedPiece;
                    SetPendingOriginalValue(expectedPiece, "color", types.GetString(colorKey, "none") ?? "none");
                    types.SetString(colorKey, kitColor);
                }
            }
        }
        else if (GetPendingDecorationMaterial(expectedPiece, "decoration") != null)
        {
            types.SetString(attrKey, value);
        }
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Previewing {inserted.GetName()} on the {slotName}. Hold right mouse button with a hammer to bake it.");
    }

    private bool TryRemoveBakedDecorationWithSaw(IPlayer byPlayer, ItemStack armorStack, string piece, string slotName)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            Notify(byPlayer, FaText.Get("This armor piece cannot be decorated."));
            return true;
        }

        string decorationKey = "decoration" + piece;
        string colorKey = "color" + piece;
        if (HasActiveDecorationPreview(piece))
        {
            Notify(byPlayer, FaText.Get("Only staged decoration is on this {0}. Use an empty hand to remove the preview.", FaText.Get(slotName)));
            return true;
        }

        string bakedDecoration = types.GetString(decorationKey, "none") ?? "none";
        bool hadDecoration = !string.Equals(bakedDecoration, "none", StringComparison.Ordinal);
        bool hadColor = !string.Equals(types.GetString(colorKey, "none"), "none", StringComparison.Ordinal);

        if (!hadDecoration && !hadColor)
        {
            return false;
        }

        ItemStack? bearArmor = null;
        string? bearSourceKey = null;
        if (string.Equals(bakedDecoration, "bear", StringComparison.Ordinal))
        {
            bearSourceKey = GetBakedBearSourceKey(piece);
            if (!TryResolveBakedBearSource(armorStack, piece, Api.World, out bearArmor, out string sourceFailure))
            {
                Api.Logger.Warning("[FACore DecorationStation] Cannot remove baked bear decoration from {0}: {1}", armorStack.Collectible?.Code, sourceFailure);
                Notify(byPlayer, sourceFailure);
                return true;
            }
        }

        types.SetString(decorationKey, "none");
        types.SetString(colorKey, "none");
        if (bearArmor != null && bearSourceKey != null)
        {
            DamageRemovedBearArmor(bearArmor, piece);
            armorStack.Attributes?.RemoveAttribute(bearSourceKey);
            GiveOrDrop(byPlayer, bearArmor, 1.1);
        }
        // An emptied kit is stored material, not an active preview.
        ItemStack? storedKit = GetPendingDecorationMaterial(piece, "color");
        if (storedKit != null && !TryParseDecorationMaterial(storedKit, piece, out _, out _, out _))
        {
            SetPendingOriginalValue(piece, "color", "");
        }
        InvalidateDecorationOperation(piece);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Unbaked and removed the {slotName} decoration.");
        return true;
    }

    private bool TryRemovePendingDecorationMaterial(IPlayer byPlayer, ItemStack armorStack, string piece)
    {
        for (int colorIndex = DecorationThreeColorState.ColorsPerPiece; colorIndex >= 1; colorIndex--)
        {
            ItemStack? cloth = GetThreeColorCloth(piece, colorIndex);
            if (cloth == null) continue;
            RestoreThreeColorValue(armorStack, piece, colorIndex);
            SetThreeColorCloth(piece, colorIndex, null);
            SetThreeColorOriginal(piece, colorIndex, "");
            GiveOrDrop(byPlayer, cloth, 1.1);
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, $"Removed staged Color{colorIndex} cloth pile.");
            return true;
        }

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
        if (editKind == "decoration" && TryGetIntrinsicDecorationColor(pendingStack, piece, out _))
        {
            RestorePendingDecorationValue(armorStack, piece, "color");
            SetPendingOriginalValue(piece, "color", "");
        }
        SetPendingDecorationMaterial(piece, editKind, null);
        SetPendingOriginalValue(piece, editKind, "");
        GiveOrDrop(byPlayer, pendingStack, 1.1);
        PlayItemPickupSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Removed staged {pendingStack.GetName()}.");
        return true;
    }

    private void TryBakeLayeredDecoration(IPlayer byPlayer, ItemStack armorStack, string piece, string slotName)
    {
        if (HasThreeColorCloth(piece))
        {
            Notify(byPlayer, FaText.Get("Hold right mouse button with shears to bake the staged cloth on this {0}.", FaText.Get(slotName)));
            return;
        }

        if (!TryPrepareLayeredDecorationBake(
                armorStack,
                piece,
                out LayeredArmorState intendedState,
                out ItemStack decorationStack,
                out ItemStack? kitStack,
                out bool isBearArmor,
                out bool hasFilledKit,
                out string failure))
        {
            Notify(byPlayer, failure.Length > 0 ? failure : FaText.Get("The staged decoration cannot be baked into this {0}.", FaText.Get(slotName)));
            return;
        }

        ITreeAttribute types = armorStack.Attributes!.GetTreeAttribute("types")!;
        ArmorCompatibility.WriteLayered(types, piece, intendedState);

        if (isBearArmor)
        {
            armorStack.Attributes?.SetItemstack(GetBakedBearSourceKey(piece), decorationStack);
        }
        else
        {
            armorStack.Attributes?.RemoveAttribute(GetBakedBearSourceKey(piece));
        }

        SetPendingDecorationMaterial(piece, "decoration", null);
        SetPendingOriginalValue(piece, "decoration", "");
        if (TryGetIntrinsicDecorationColor(decorationStack, piece, out _))
        {
            SetPendingOriginalValue(piece, "color", "");
        }
        if (kitStack != null)
        {
            SetPendingOriginalValue(piece, "color", "");
            if (hasFilledKit) EmptyDecorationKit(kitStack);
        }
        MarkStationDirty();
        NotifyInfo(byPlayer, isBearArmor
            ? $"Baked the {intendedState.Color} bear decoration into the {slotName}."
            : hasFilledKit
            ? $"Baked the staged {slotName} decoration. The empty decoration kit remains on the station."
            : $"Baked the staged {slotName} decoration in {intendedState.Color} color.");
    }

    private bool TryPrepareLayeredDecorationBake(
        ItemStack armorStack,
        string piece,
        out LayeredArmorState intendedState,
        out ItemStack decorationStack,
        out ItemStack? kitStack,
        out bool isBearArmor,
        out bool hasFilledKit,
        out string failure)
    {
        intendedState = default;
        decorationStack = null!;
        kitStack = GetPendingDecorationMaterial(piece, "color");
        isBearArmor = false;
        hasFilledKit = false;
        failure = "";

        ItemStack? pendingDecoration = GetPendingDecorationMaterial(piece, "decoration");
        if (pendingDecoration == null)
        {
            failure = FaText.Get("Stage a decoration before baking this armor.");
            return false;
        }
        if (!HasAppliedLayeredPreview(piece, "decoration")
            || !TryParseDecorationMaterial(pendingDecoration, piece, out string kind, out string decorationValue, out _)
            || kind != "decoration")
        {
            failure = FaText.Get("The staged material is not an applied decoration preview for the mounted armor.");
            return false;
        }
        if (!TryGetFAArmorInfo(armorStack, out ResolvedArmor armorInfo)
            || armorInfo.Piece != piece
            || armorInfo.Definition.Schema != ArmorAttributeSchema.Layered
            || armorStack.Attributes?.GetTreeAttribute("types") is not ITreeAttribute types
            || !ArmorCompatibility.TryReadLayered(types, piece, out LayeredArmorState currentState))
        {
            failure = FaText.Get("This armor has a malformed Layered state.");
            return false;
        }
        if (!string.Equals(currentState.Decoration, decorationValue, StringComparison.Ordinal))
        {
            failure = FaText.Get("The mounted armor no longer contains the preview produced by the staged decoration.");
            return false;
        }

        isBearArmor = TryGetBearArmorColor(pendingDecoration, piece, out _);
        string bakedColor = "plain";
        if (TryGetIntrinsicDecorationColor(pendingDecoration, piece, out string suppliedColor))
        {
            if (kitStack != null)
            {
                failure = FaText.Get("This decoration supplies its own color.");
                return false;
            }
            bakedColor = suppliedColor;
            if (!HasAppliedLayeredPreview(piece, "color")
                || !string.Equals(currentState.Color, bakedColor, StringComparison.Ordinal))
            {
                failure = FaText.Get("The mounted armor no longer contains the color preview supplied by the staged decoration.");
                return false;
            }
        }
        else if (kitStack != null
            && TryParseDecorationMaterial(kitStack, piece, out string kitKind, out string kitColor, out _)
            && kitKind == "color")
        {
            hasFilledKit = true;
            bakedColor = kitColor;
            if (!HasAppliedLayeredPreview(piece, "color")
                || !string.Equals(currentState.Color, bakedColor, StringComparison.Ordinal))
            {
                failure = FaText.Get("The mounted armor no longer contains the color preview supplied by the staged kit.");
                return false;
            }
        }
        else if (HasAppliedLayeredPreview(piece, "color"))
        {
            failure = FaText.Get("The mounted armor has a stale color preview that does not match the staged materials.");
            return false;
        }

        intendedState = currentState with { Decoration = decorationValue, Color = bakedColor };
        if (!ArmorCompatibility.ValidateFinalLayered(armorInfo, intendedState).Allowed)
        {
            failure = FaText.Get("The complete result represented by the staged decoration and color is not valid for this armor.");
            return false;
        }

        decorationStack = pendingDecoration;
        return true;
    }

    private static void EmptyDecorationKit(ItemStack kitStack)
    {
        if (kitStack.Collectible is not ILiquidInterface liquidInterface
            || kitStack.Collectible is not ILiquidSource liquidSource)
        {
            return;
        }

        ItemStack? content = liquidInterface.GetContent(kitStack);
        if (content != null && content.StackSize > 0)
        {
            liquidSource.TryTakeContent(kitStack, content.StackSize);
        }
    }

    private void TryStageThreeColorCloth(IPlayer byPlayer, ItemSlot activeSlot, ItemStack heldStack, ItemStack armorStack, string piece, string slotName, string color)
    {
        if (HasPendingDecorationEditsLayered(piece))
        {
            Notify(byPlayer, FaText.Get("Remove the staged Layered decoration materials before adding cloth."));
            return;
        }

        if (!UsesThreeColorSchema(armorStack, piece))
        {
            Notify(byPlayer, FaText.Get("This {0} uses the Layered decoration workflow.", FaText.Get(slotName)));
            return;
        }
        if (!TryGetFAArmorInfo(armorStack, out ResolvedArmor armorInfo)
            || !ArmorCompatibility.ValidateThreeColor(armorInfo, color).Allowed)
        {
            Notify(byPlayer, FaText.Get("Color '{0}' is not supported by this armor.", color));
            return;
        }

        if (heldStack.StackSize < 3)
        {
            Notify(byPlayer, FaText.Get("A cloth pile requires three identical cloth items."));
            return;
        }

        if (!threeColorState.TryGetFirstEmptySlot(piece, out int colorIndex))
        {
            Notify(byPlayer, FaText.Get("All three cloth piles are already staged."));
            return;
        }

        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null) return;
        string key = new DecorationColorSlot(piece, colorIndex).ArmorAttributeKey;
        string original = types.GetString(key, "plain") ?? "plain";
        ItemStack? inserted = activeSlot.TakeOut(3);
        activeSlot.MarkDirty();
        if (inserted == null || !threeColorState.TrySetClothIfEmpty(piece, colorIndex, inserted))
        {
            if (inserted != null) GiveOrDrop(byPlayer, inserted, 1.1);
            Notify(byPlayer, FaText.Get("The selected cloth slot changed before the material could be staged."));
            return;
        }
        InvalidateDecorationOperation(piece);
        SetThreeColorOriginal(piece, colorIndex, original);
        types.SetString(key, color);
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Previewing {color} as Color{colorIndex} on the {slotName}.");
    }

    private void TryBakeThreeColorDecoration(IPlayer byPlayer, ItemStack armorStack, string piece, string slotName)
    {
        if (!UsesThreeColorSchema(armorStack, piece))
        {
            Notify(byPlayer, FaText.Get("This {0} does not use the ThreeColor cloth workflow.", FaText.Get(slotName)));
            return;
        }
        if (!TryPrepareThreeColorBake(armorStack, piece, out ThreeColorArmorState state, out string failure))
        {
            Notify(byPlayer, failure);
            return;
        }
        ITreeAttribute types = armorStack.Attributes!.GetTreeAttribute("types")!;
        for (int i = 1; i <= DecorationThreeColorState.ColorsPerPiece; i++)
        {
            SetThreeColorCloth(piece, i, null);
            SetThreeColorOriginal(piece, i, "");
        }
        ArmorCompatibility.WriteThreeColor(types, piece, state);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Baked the three staged colors into the {slotName}.");
    }

    private bool TryPrepareThreeColorBake(ItemStack armorStack, string piece, out ThreeColorArmorState intendedState, out string failure)
    {
        intendedState = default;
        failure = FaText.Get("Stage all three valid cloth piles before baking with shears.");
        if (!TryGetFAArmorInfo(armorStack, out ResolvedArmor armorInfo)
            || armorInfo.Piece != piece
            || armorInfo.Definition.Schema != ArmorAttributeSchema.ThreeColor
            || armorStack.Attributes?.GetTreeAttribute("types") is not ITreeAttribute types
            || !ArmorCompatibility.TryReadThreeColor(types, piece, out ThreeColorArmorState currentState))
        {
            failure = FaText.Get("The mounted armor has a malformed ThreeColor state.");
            return false;
        }

        string[] colors = new string[DecorationThreeColorState.ColorsPerPiece];
        for (int index = 1; index <= DecorationThreeColorState.ColorsPerPiece; index++)
        {
            ItemStack? cloth = GetThreeColorCloth(piece, index);
            if (cloth == null
                || !TryGetClothColor(cloth, out string color)
                || !HasAppliedThreeColorPreview(piece, index)
                || !string.Equals(types.GetString(new DecorationColorSlot(piece, index).ArmorAttributeKey, "plain"), color, StringComparison.Ordinal))
            {
                failure = FaText.Get("Cloth pile {0} no longer matches the applied armor preview.", index);
                return false;
            }
            colors[index - 1] = color;
        }

        intendedState = currentState with { Color1 = colors[0], Color2 = colors[1], Color3 = colors[2] };
        if (!ArmorCompatibility.ValidateFinalThreeColor(armorInfo, intendedState).Allowed)
        {
            failure = FaText.Get("The complete result represented by the staged cloth is not valid for this armor.");
            return false;
        }
        return true;
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
                Notify(byPlayer, FaText.Get("This part of the hemming crane cannot be used right now."));
                return;
        }
    }

    private void TryInteractTrimArmorSlot(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (trimArmorStack != null)
        {
            if (IsTongs(heldStack))
            {
                TryRemoveBakedTrimWithTongs(byPlayer);
                return;
            }

            if (IsSolderingIron(heldStack))
            {
                TryBakeTrimArmor(byPlayer);
                return;
            }

            if (heldStack != null)
            {
                bool missingCrucibleForTrim = trimCrucibleStack == null
                    && (pendingTrimRivetsStack != null || TryGetRimsAndRivetsMetal(heldStack, out _));
                Notify(byPlayer, missingCrucibleForTrim
                    ? MissingTrimSolderCrucibleMessage
                    : FaText.Get("Use an empty hand to pick up the armor, or use the appropriate tool here."));
                return;
            }

            ReturnPendingTrimToBowl();
            GiveOrDrop(byPlayer, trimArmorStack, 1.1);
            trimArmorStack = null;
            InvalidateTrimOperation();
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, "Picked up armor from the hemming crane.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, FaText.Get("The hemming crane has no armor."));
            return;
        }

        if (!TryGetFAArmorInfo(heldStack, out ResolvedArmor mountedArmor))
        {
            Notify(byPlayer, FaText.Get("Place a Forgotten Armory armor piece here."));
            return;
        }

        if (mountedArmor.Definition.Schema != ArmorAttributeSchema.Layered)
        {
            Notify(byPlayer, FaText.Get("This armor schema does not support trim."));
            return;
        }
        if (!ArmorCompatibility.ValidateStateContainer(heldStack, mountedArmor, out _).Allowed)
        {
            Notify(byPlayer, FaText.Get("This known armor has a malformed customization state."));
            return;
        }

        trimArmorStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        InvalidateTrimOperation();
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {trimArmorStack.GetName()} on the hemming crane.");
        TryStageStoredTrimMaterial(byPlayer);
    }

    private void TryInteractTrimRivetsPlace(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (pendingTrimRivetsStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, FaText.Get("The rims and rivets bowl is already occupied."));
                return;
            }

            TryRemovePendingTrimMaterial(byPlayer);
            return;
        }

        if (trimRivetsStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, FaText.Get("The rims and rivets bowl is already occupied."));
                return;
            }

            GiveOrDrop(byPlayer, trimRivetsStack, 0.9);
            trimRivetsStack = null;
            InvalidateTrimOperation();
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, "Picked up rims and rivets from the bowl.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, FaText.Get("The rims and rivets bowl is empty."));
            return;
        }

        if (!TryGetRimsAndRivetsMetal(heldStack, out _))
        {
            Notify(byPlayer, FaText.Get("Only rims and rivets belong in this bowl."));
            return;
        }

        trimRivetsStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        InvalidateTrimOperation();
        PlayItemInsertSound(byPlayer);
        if (trimArmorStack != null)
        {
            TryStageStoredTrimMaterial(byPlayer);
            MarkStationDirty();
            return;
        }

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
                Notify(byPlayer, FaText.Get("The crucible stand is already occupied."));
                return;
            }

            GiveOrDrop(byPlayer, trimCrucibleStack, 0.9);
            trimCrucibleStack = null;
            InvalidateTrimOperation();
            PlayItemPickupSound(byPlayer);
            MarkStationDirty();
            NotifyInfo(byPlayer, "Picked up the solder crucible.");
            return;
        }

        if (activeSlot == null || activeSlot.Empty || heldStack == null)
        {
            Notify(byPlayer, FaText.Get("The crucible stand is empty."));
            return;
        }

        if (!IsSilverSolderCrucible(heldStack, requireHeat: false, out string solderMetal, out string failure))
        {
            TrimDebugLog($"CruciblePlace rejected held={FormatStackDebug(heldStack)} failure={failure} attrs={FormatTreeDebug(heldStack.Attributes)}");
            Notify(byPlayer, failure);
            return;
        }

        trimCrucibleStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        InvalidateTrimOperation();
        TrimDebugLog($"CruciblePlace accepted metal={solderMetal} stored={FormatStackDebug(trimCrucibleStack)} attrs={FormatTreeDebug(trimCrucibleStack?.Attributes)}");
        PlayCeramicPlaceSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {solderMetal} solder crucible on the stand.");
    }

    private void TryInteractTrimSolderHolder(IPlayer byPlayer)
    {
        ItemSlot? activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? heldStack = activeSlot?.Itemstack;

        if (trimSolderingIronStack != null)
        {
            if (heldStack != null)
            {
                Notify(byPlayer, FaText.Get("The soldering iron holder is already occupied."));
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
            Notify(byPlayer, FaText.Get("The soldering iron holder is empty."));
            return;
        }

        if (!IsSolderingIron(heldStack))
        {
            Notify(byPlayer, FaText.Get("Only a soldering iron belongs in this holder."));
            return;
        }

        trimSolderingIronStack = activeSlot.TakeOut(1);
        activeSlot.MarkDirty();
        PlayItemInsertSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Placed {trimSolderingIronStack.GetName()} in the holder.");
    }

    private void TryStageStoredTrimMaterial(IPlayer byPlayer)
    {
        if (trimRivetsStack == null
            || !TryPrepareTrimPreview(byPlayer, trimRivetsStack, out ITreeAttribute types, out string stripKey, out string currentStrip))
        {
            return;
        }

        ItemStack stagedStack = trimRivetsStack;
        trimRivetsStack = null;
        ApplyTrimPreview(byPlayer, stagedStack, types, stripKey, currentStrip);
    }

    private void ReturnPendingTrimToBowl()
    {
        if (pendingTrimRivetsStack == null)
        {
            return;
        }

        RestorePendingTrimPreview();
        trimRivetsStack = pendingTrimRivetsStack;
        pendingTrimRivetsStack = null;
        pendingTrimOriginalStrip = "";
        InvalidateTrimOperation();
    }

    private bool TryPrepareTrimPreview(
        IPlayer byPlayer,
        ItemStack rivetsStack,
        out ITreeAttribute types,
        out string stripKey,
        out string currentStrip)
    {
        types = null!;
        stripKey = "";
        currentStrip = "none";

        if (trimArmorStack == null)
        {
            Notify(byPlayer, FaText.Get("Place armor on the hemming crane first."));
            return false;
        }

        if (!TryGetRimsAndRivetsMetal(rivetsStack, out string metal))
        {
            Notify(byPlayer, FaText.Get("Use rims and rivets to preview trim."));
            return false;
        }

        if (pendingTrimRivetsStack != null)
        {
            Notify(byPlayer, FaText.Get("This armor already has staged trim. Remove it first."));
            return false;
        }

        if (!TryGetFAArmorInfo(trimArmorStack, out ResolvedArmor armorInfo))
        {
            Notify(byPlayer, FaText.Get("This armor piece cannot be trimmed."));
            return false;
        }

        ArmorCompatibilityResult trimCompatibility = ArmorCompatibility.ValidateTrim(armorInfo, metal);
        if (!trimCompatibility.Allowed)
        {
            Notify(byPlayer, FaText.Get("This armor does not support {0} trim.", metal));
            return false;
        }

        ITreeAttribute? armorTypes = trimArmorStack.Attributes?.GetTreeAttribute("types");
        if (armorTypes == null)
        {
            Notify(byPlayer, FaText.Get("This armor piece cannot be trimmed."));
            return false;
        }

        if (!TextureAssetExists(new AssetLocation("facore", $"armor/entity/trim/{metal}")))
        {
            Notify(byPlayer, FaText.Get("No trim texture exists for {0}.", metal));
            return false;
        }

        types = armorTypes;
        stripKey = "strip" + armorInfo.Piece;
        currentStrip = types.GetString(stripKey) ?? "none";
        return true;
    }

    private void ApplyTrimPreview(IPlayer byPlayer, ItemStack stagedStack, ITreeAttribute types, string stripKey, string currentStrip)
    {
        TryGetRimsAndRivetsMetal(stagedStack, out string metal);
        pendingTrimOriginalStrip = currentStrip;
        pendingTrimRivetsStack = stagedStack;
        types.SetString(stripKey, metal);
        InvalidateTrimOperation();
        MarkStationDirty();
        if (IsTrimValueSet(currentStrip))
        {
            NotifyInfo(byPlayer, $"Previewing {pendingTrimRivetsStack.GetName()} trim. Remove the existing rivets with tongs before baking.");
            return;
        }

        NotifyInfo(byPlayer, $"Previewing {pendingTrimRivetsStack.GetName()} trim. Hold right mouse button with a soldering iron to bake it.");
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
        InvalidateTrimOperation();
        PlayItemPickupSound(byPlayer);
        MarkStationDirty();
        NotifyInfo(byPlayer, "Removed staged trim.");
        return true;
    }

    private void TryBakeTrimArmor(IPlayer byPlayer)
    {
        if (trimArmorStack == null)
        {
            Notify(byPlayer, FaText.Get("The hemming crane has no armor."));
            return;
        }

        if (pendingTrimRivetsStack == null)
        {
            Notify(byPlayer, FaText.Get("There is no staged trim to bake."));
            return;
        }

        if (IsTrimValueSet(pendingTrimOriginalStrip))
        {
            Notify(byPlayer, FaText.Get("This armor already has baked rivets. Remove them with tongs before baking new rivets."));
            return;
        }

        if (!TryGetFAArmorInfo(trimArmorStack, out ResolvedArmor armorInfo)
            || !TryGetRimsAndRivetsMetal(pendingTrimRivetsStack, out string stagedMetal)
            || !ArmorCompatibility.ValidateTrim(armorInfo, stagedMetal).Allowed
            || !ArmorCompatibility.ValidateStateContainer(trimArmorStack, armorInfo, out _).Allowed)
        {
            Notify(byPlayer, FaText.Get("The staged trim or mounted armor state is no longer compatible."));
            return;
        }

        if (!HasUsableTrimSolderCrucible(out string solderFailure))
        {
            Notify(byPlayer, solderFailure);
            return;
        }

        ItemSlot? solderingIronSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? solderingIron = solderingIronSlot?.Itemstack;
        if (!IsSolderingIron(solderingIron))
        {
            Notify(byPlayer, FaText.Get("Hold a soldering iron against the staged rivets."));
            return;
        }
        int remainingDurability = solderingIron!.Collectible.GetRemainingDurability(solderingIron);
        if (remainingDurability < 1)
        {
            Notify(byPlayer, FaText.Get("The soldering iron is too damaged to weld the rivets."));
            return;
        }
        if (!TryConsumeTrimSolder(out solderFailure))
        {
            Notify(byPlayer, solderFailure);
            return;
        }

        solderingIron.Collectible.DamageItem(Api.World, byPlayer.Entity, solderingIronSlot!, 1, true);
        pendingTrimRivetsStack = null;
        pendingTrimOriginalStrip = "";
        InvalidateTrimOperation();
        MarkStationDirty();
        NotifyInfo(byPlayer, "Welded and baked the staged trim.");
    }

    private void TryRemoveBakedTrimWithTongs(IPlayer byPlayer)
    {
        if (trimArmorStack == null || !TryGetFAArmorInfo(trimArmorStack, out ResolvedArmor armorInfo))
        {
            Notify(byPlayer, FaText.Get("The hemming crane has no armor."));
            return;
        }

        ITreeAttribute? types = trimArmorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            Notify(byPlayer, FaText.Get("This armor piece cannot be trimmed."));
            return;
        }

        string stripKey = "strip" + armorInfo.Piece;
        if (pendingTrimRivetsStack != null && !IsTrimValueSet(pendingTrimOriginalStrip))
        {
            Notify(byPlayer, FaText.Get("Only staged rivets are on this armor. Use an empty hand to remove the preview."));
            return;
        }

        string bakedStrip = IsTrimValueSet(pendingTrimOriginalStrip)
            ? pendingTrimOriginalStrip
            : types.GetString(stripKey, "none");

        if (!IsTrimValueSet(bakedStrip))
        {
            Notify(byPlayer, FaText.Get("This armor has no baked rivets to remove."));
            return;
        }

        ItemStack? removedRivets = CreateRimsAndRivetsStack(bakedStrip);
        if (removedRivets == null)
        {
            Notify(byPlayer, FaText.Get("Could not find rims and rivets item for {0}.", bakedStrip));
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
        InvalidateTrimOperation();
        MarkStationDirty();
        NotifyInfo(byPlayer, $"Removed {removedRivets.GetName()} from the armor.");
    }

    private void RestorePendingTrimPreview()
    {
        if (trimArmorStack == null || pendingTrimRivetsStack == null || !TryGetFAArmorInfo(trimArmorStack, out ResolvedArmor armorInfo))
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
            Notify(byPlayer, FaText.Get("Hold a torch or firestarter to light the fuel."));
            return;
        }

        if (!CanStartFuelIgnition(out string failure, out string coatingMetal, out ResolvedArmor armorInfo))
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
        using var languageScope = FaText.ForPlayer(byPlayer);
        UpdateProcess();

        ItemStack? heldStack = byPlayer.InventoryManager?.ActiveHotbarSlot?.Itemstack;
        return CanLightFuel(heldStack) && CanStartFuelIgnition(out _, out _, out _);
    }

    public void CompleteFuelIgnition(IPlayer byPlayer)
    {
        using var languageScope = FaText.ForPlayer(byPlayer);
        TryLightFuel(byPlayer);
    }

    private bool CanStartFuelIgnition(out string failure, out string coatingMetal, out ResolvedArmor armorInfo)
    {
        failure = "";
        coatingMetal = "";
        armorInfo = null!;

        if (fuelStack == null)
        {
            failure = FaText.Get("Add fuel before lighting the station.");
            return false;
        }

        if (fuelStack.StackSize < MaxCharcoalPieces)
        {
            failure = FaText.Get("Fill the fuel tray before lighting it ({0}/{1} fuel).", MaxCharcoalPieces, MaxCharcoalPieces);
            return false;
        }

        if (fuelLit)
        {
            failure = FaText.Get("The fuel is already burning.");
            return false;
        }

        if (lidOpen)
        {
            failure = FaText.Get("Close the cauldron lid before lighting the fuel.");
            return false;
        }

        if (processMode == ProcessPlateResting || processMode == ProcessArmorDissolving)
        {
            failure = FaText.Get("Wait for the acid reaction to finish before lighting the fuel.");
            return false;
        }

        if (!TryGetCoatingMetal(liquidStack, out coatingMetal))
        {
            failure = FaText.Get("Prepare coating liquid before lighting the fuel.");
            return false;
        }

        if (!TryGetCoatingDoseMetal(liquidStack, out coatingMetal))
        {
            failure = FaText.Get("Fill the cauldron with {0}L of coating liquid before lighting the fuel.", LiquidCapacityLitres);
            return false;
        }

        if (!TryGetFAArmorInfo(immersedStack, out armorInfo))
        {
            failure = FaText.Get("Place a coatable armor piece in the cauldron first.");
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

        if (IsTongs(activeSlot.Itemstack) && immersedStack != null)
        {
            TryTakeImmersedItem(byPlayer);
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
                ? FaText.Get("Only sulfuric acid or coating liquid can be poured into this cauldron.")
                : FaText.Get("That container cannot interact with {0}.", GetLiquidName(liquidStack)));
            return;
        }

        TryInsertImmersedItem(byPlayer, activeSlot);
    }

    private void TryPourIntoCauldron(IPlayer byPlayer, ItemSlot activeSlot)
    {
        if (HasActiveProcess() || fuelLit)
        {
            Notify(byPlayer, FaText.Get("The cauldron is already processing."));
            return;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, FaText.Get("Remove the immersed item before changing the liquid."));
            return;
        }

        int missing = LiquidCapacityItems - (liquidStack?.StackSize ?? 0);
        if (missing <= 0)
        {
            Notify(byPlayer, liquidStack == null
                ? FaText.Get("The cauldron is already full.")
                : FaText.Get("The cauldron already contains {0}L of {1}.", LiquidCapacityLitres, GetLiquidName(liquidStack)));
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
            Notify(byPlayer, FaText.Get("The cauldron is already processing."));
            return;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, FaText.Get("Remove the immersed item before draining the cauldron."));
            return;
        }

        if (liquidStack == null || liquidStack.StackSize <= 0)
        {
            Notify(byPlayer, FaText.Get("The cauldron is empty."));
            return;
        }

        ItemStack? heldStack = activeSlot.Itemstack;
        if (heldStack?.Collectible is not ILiquidSink sink)
        {
            Notify(byPlayer, FaText.Get("Hold a liquid container to take {0} from the cauldron.", GetLiquidName(liquidStack)));
            return;
        }

        ItemStack moveStack = liquidStack.Clone();
        int moved = FillContainerStack(sink, heldStack, moveStack);
        if (moved <= 0)
        {
            Notify(byPlayer, FaText.Get("That container cannot hold {0}.", GetLiquidName(liquidStack)));
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

        if (IsTongs(activeSlot.Itemstack) && immersedStack != null)
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
            Notify(byPlayer, FaText.Get("Wait for the cauldron to cool first."));
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
            Notify(byPlayer, FaText.Get("The cauldron already contains an item."));
            return;
        }

        Notify(byPlayer, FaText.Get("Use sulfuric acid with a metal plate or coated armor, or use coating liquid with uncoated armor."));
    }

    private void TryTakeImmersedItem(IPlayer byPlayer)
    {
        UpdateProcess();

        if (fuelLit)
        {
            Notify(byPlayer, FaText.Get("Wait for the cauldron to cool first."));
            return;
        }

        if (immersedStack == null)
        {
            Notify(byPlayer, FaText.Get("There is no item in the cauldron."));
            return;
        }

        ItemStack takeStack = immersedStack;
        immersedStack = null;
        ClearProcess();

        if (!byPlayer.InventoryManager.TryGiveItemstack(takeStack, true))
        {
            Api.World.SpawnItemEntity(takeStack, Pos.ToVec3d().Add(0.5, 0.75, 0.5));
        }

        MarkStationDirty();
        PlayItemSplashSound(byPlayer);
        NotifyInfo(byPlayer, $"Removed {takeStack.GetName()} from the cauldron.");
        StartChemicalExposure(byPlayer);
    }

    private bool TryTakeCauldronLiquid(ItemSlot activeSlot, int maxItems, out ItemStack pourStack, out string failure)
    {
        pourStack = null!;
        failure = FaText.Get("Hold sulfuric acid or coating liquid over the cauldron.");

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
            failure = FaText.Get("Only sulfuric acid or coating liquid can be poured into this cauldron.");
            return false;
        }

        if (!CanAddLiquidToCauldron(contentStack!, out failure))
        {
            return false;
        }

        ItemStack? takenStack = DrainContainerStack(source, heldStack, maxItems, contentStack!.StackSize);
        if (takenStack == null || !IsCauldronLiquid(takenStack))
        {
            failure = FaText.Get("That liquid could not be poured from the container.");
            return false;
        }

        pourStack = takenStack;
        return true;
    }

    internal static int FillContainerStack(ILiquidSink sink, ItemStack containers, ItemStack liquid)
    {
        int containerCount = containers.StackSize;
        int amountPerContainer = containerCount > 0 ? liquid.StackSize / containerCount : 0;
        if (amountPerContainer <= 0) return 0;

        // Container contents describe each bucket, whereas the cauldron stores the total.
        // Limit both arguments so integer remainders stay in the cauldron.
        ItemStack portion = liquid.Clone();
        portion.StackSize = amountPerContainer;
        int movedPerContainer = sink.TryPutLiquid(containers, portion, amountPerContainer / (float)ItemsPerLitre);
        return movedPerContainer * containerCount;
    }

    internal static ItemStack? DrainContainerStack(ILiquidSource source, ItemStack containers, int maxItems, int availablePerContainer)
    {
        int containerCount = containers.StackSize;
        if (containerCount <= 0) return null;
        int amountPerContainer = Math.Min(maxItems / containerCount, availablePerContainer);
        if (amountPerContainer <= 0) return null;

        ItemStack? taken = source.TryTakeContent(containers, amountPerContainer);
        if (taken == null) return null;

        // Clone before scaling: a container implementation may return its own content stack.
        ItemStack total = taken.Clone();
        total.StackSize *= containerCount;
        return total;
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

        failure = FaText.Get("The cauldron already contains {0}.", GetLiquidName(liquidStack));
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
        return stack?.Collectible?.Code == SulfuricAcidCode;
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
            Notify(byPlayer, FaText.Get("Remove the immersed item before adding a plate."));
            return true;
        }

        if ((liquidStack?.StackSize ?? 0) < LiquidCapacityItems)
        {
            Notify(byPlayer, FaText.Get("Fill the cauldron with {0}L of sulfuric acid before adding a metal plate.", LiquidCapacityLitres));
            return true;
        }

        if (!IsSulfuricAcid(liquidStack))
        {
            Notify(byPlayer, FaText.Get("The cauldron already contains {0}.", GetLiquidName(liquidStack)));
            return true;
        }

        Item? coatingItem = Api.World.GetItem(CoatingLiquidCode(metal));
        if (coatingItem == null)
        {
            Notify(byPlayer, FaText.Get("This metal cannot be made into coating liquid: {0}.", metal));
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
        StartChemicalExposure(byPlayer);
        return true;
    }

    private bool TryInsertArmorForCoating(IPlayer byPlayer, ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (!TryGetFAArmorInfo(heldStack, out ResolvedArmor armorInfo))
        {
            return false;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, FaText.Get("The cauldron already contains an item."));
            return true;
        }

        if (!TryGetCoatingMetal(liquidStack, out string metal))
        {
            Notify(byPlayer, FaText.Get("Prepare coating liquid before adding armor."));
            return true;
        }

        if (!TryGetCoatingDoseMetal(liquidStack, out metal))
        {
            Notify(byPlayer, FaText.Get("Fill the cauldron with {0}L of coating liquid before adding armor.", LiquidCapacityLitres));
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
        StartChemicalExposure(byPlayer);
        return true;
    }

    private bool TryStartArmorDissolve(IPlayer byPlayer, ItemSlot activeSlot)
    {
        ItemStack? heldStack = activeSlot.Itemstack;
        if (!TryGetFAArmorInfo(heldStack, out ResolvedArmor armorInfo))
        {
            return false;
        }

        if (!TryGetArmorCover(heldStack, armorInfo, out string coverMetal) || coverMetal == "none")
        {
            return false;
        }

        if (immersedStack != null)
        {
            Notify(byPlayer, FaText.Get("The cauldron already contains an item."));
            return true;
        }

        if ((liquidStack?.StackSize ?? 0) < LiquidCapacityItems)
        {
            Notify(byPlayer, FaText.Get("Fill the cauldron with {0}L of sulfuric acid before dissolving coating.", LiquidCapacityLitres));
            return true;
        }

        if (!IsSulfuricAcid(liquidStack))
        {
            Notify(byPlayer, FaText.Get("The cauldron already contains {0}.", GetLiquidName(liquidStack)));
            return true;
        }

        Item? coatingItem = Api.World.GetItem(CoatingLiquidCode(coverMetal));
        if (coatingItem == null)
        {
            Notify(byPlayer, FaText.Get("This coating cannot be recovered as liquid: {0}.", coverMetal));
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
        StartChemicalExposure(byPlayer);
        return true;
    }

    private void StartChemicalExposure(IPlayer byPlayer)
    {
        if (Api?.Side != EnumAppSide.Server || liquidStack == null || PlayerHasTongs(byPlayer))
        {
            return;
        }

        EntityPlayer playerEntity = byPlayer.Entity;
        Notify(byPlayer, FaText.Get("Chemical exposure! Use tongs to handle items in a filled cauldron."));

        for (int tick = 1; tick <= ChemicalExposureTicks; tick++)
        {
            Api.World.RegisterCallback(_ => ApplyNonFatalChemicalDamage(playerEntity), tick * ChemicalExposureTickDelayMs);
        }
    }

    private static bool PlayerHasTongs(IPlayer byPlayer)
    {
        return IsTongs(byPlayer.Entity.LeftHandItemSlot?.Itemstack);
    }

    private static void ApplyNonFatalChemicalDamage(EntityPlayer playerEntity)
    {
        if (!playerEntity.Alive)
        {
            return;
        }

        ITreeAttribute? health = playerEntity.WatchedAttributes.GetTreeAttribute("health");
        float currentHealth = health?.GetFloat("currenthealth") ?? 0f;
        if (currentHealth <= ChemicalExposureDamage)
        {
            return;
        }

        playerEntity.ReceiveDamage(new DamageSource
        {
            Source = EnumDamageSource.Internal,
            Type = EnumDamageType.Poison,
            IgnoreInvFrames = true
        }, ChemicalExposureDamage);
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
        bool resolved = ArmorResolver.Resolve(stack, out ResolvedArmor? armor).Allowed && armor != null;
        piece = resolved ? armor!.Piece : "";
        return resolved;
    }

    private static bool TryGetFAArmorInfo(ItemStack? stack, out ResolvedArmor armorInfo)
    {
        ArmorCompatibilityResult result = ArmorResolver.Resolve(stack, out ResolvedArmor? resolved);
        armorInfo = resolved!;
        return result.Allowed && resolved != null;
    }

    private bool CanApplyArmorCoating(ItemStack? armorStack, ResolvedArmor armorInfo, string metal, out string failure)
    {
        failure = "";

        ITreeAttribute? types = armorStack?.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            failure = FaText.Get("This armor piece cannot be coated.");
            return false;
        }

        if (!ArmorCompatibility.ValidateStateContainer(armorStack!, armorInfo, out _).Allowed)
        {
            failure = FaText.Get("This known armor has a malformed customization state.");
            return false;
        }

        string coverKey = "cover" + armorInfo.Piece;
        string currentCover = types.GetString(coverKey) ?? "none";
        if (!string.Equals(currentCover, "none", StringComparison.Ordinal))
        {
            failure = FaText.Get("This armor already has {0} coating.", currentCover);
            return false;
        }

        ArmorCompatibilityResult compatibility = ArmorCompatibility.ValidateCover(armorInfo, metal);
        if (!compatibility.Allowed)
        {
            failure = FaText.Get("This armor set does not support {0} coating.", metal);
            return false;
        }

        if (!ArmorCompatibility.TryReadLayered(types, armorInfo.Piece, out LayeredArmorState state)
            || !ArmorCompatibility.ValidateFinalLayered(armorInfo, state with
            {
                Cover = metal,
                Decoration = "none",
                Color = "none"
            }).Allowed)
        {
            failure = FaText.Get("The resulting armor customization state would be invalid.");
            return false;
        }

        return true;
    }

    private static void ApplyArmorCoating(ItemStack armorStack, ResolvedArmor armorInfo, string metal)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return;
        }

        types.SetString("cover" + armorInfo.Piece, metal);
        ArmorCompatibility.ResetDecorations(types, armorInfo);
        armorStack.Attributes?.RemoveAttribute(GetBakedBearSourceKey(armorInfo.Piece));
    }

    private static bool TryGetArmorCover(ItemStack? armorStack, ResolvedArmor armorInfo, out string coverMetal)
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

    private static void RemoveArmorCoating(ItemStack armorStack, ResolvedArmor armorInfo)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return;
        }

        types.SetString("cover" + armorInfo.Piece, "none");
        ArmorCompatibility.ResetDecorations(types, armorInfo);
        armorStack.Attributes?.RemoveAttribute(GetBakedBearSourceKey(armorInfo.Piece));
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

    private static bool TryGetCoatingDoseMetal(ItemStack? stack, out string metal)
    {
        return TryGetCoatingMetal(stack, out metal)
            && stack!.StackSize >= LiquidCapacityItems;
    }

    private static bool IsCauldronLiquid(ItemStack? stack)
    {
        return IsSulfuricAcid(stack) || IsCoatingLiquid(stack);
    }

    private void OnProcessTick(float dt)
    {
        UpdateProcess();
    }

    private void OnPreviewParticleTick(float dt)
    {
        LogPreviewParticleListener();

        if (HasAnyActiveStationPreview() && Random.Shared.NextDouble() <= 0.28125)
        {
            SpawnStationPreviewParticles();
        }
    }

    private bool HasAnyActiveStationPreview()
    {
        if (IsTrimStationBlock())
        {
            return trimArmorStack != null && pendingTrimRivetsStack != null;
        }

        return IsDecorationStationBlock()
            && ((GetDecorationArmorStack("head") != null && HasActiveDecorationPreview("head"))
                || (GetDecorationArmorStack("body") != null && HasActiveDecorationPreview("body"))
                || (GetDecorationArmorStack("legs") != null && HasActiveDecorationPreview("legs")));
    }

    private IReadOnlyList<StationElementZone> GetCachedElementZones()
    {
        return Block is BlockFAStation station
            ? station.GetElementZones()
            : StationShapeElementReader.LoadElementZones(Api, Block);
    }

    private void SpawnStationPreviewParticles()
    {
        IReadOnlyList<StationElementZone> zones = GetCachedElementZones();
        UpdatePreviewParticleDebugState(zones);

        if (IsTrimStationBlock())
        {
            if (trimArmorStack != null
                && pendingTrimRivetsStack != null
                && TryGetDecorationZoneBox(zones, "Armor", out Cuboidf armorZoneBox))
            {
                double regionScale = TryGetFAArmorInfo(trimArmorStack, out ResolvedArmor armorInfo)
                    && armorInfo.Piece is "body" or "legs"
                        ? 2.0
                        : 1.0;
                SpawnPreviewParticles(armorZoneBox, regionScale);
            }
            return;
        }

        if (!IsDecorationStationBlock())
        {
            return;
        }

        foreach ((string piece, string actionName) in new[]
        {
            ("head", "HeadPlace"),
            ("body", "BodyPlace"),
            ("legs", "LegsPlace")
        })
        {
            if (GetDecorationArmorStack(piece) == null
                || !HasActiveDecorationPreview(piece)
                || !TryGetDecorationZoneBox(zones, actionName, out Cuboidf zoneBox))
            {
                continue;
            }

            SpawnPreviewParticles(zoneBox, piece is "body" or "legs" ? 1.3 : 1.0);
        }
    }

    private void SpawnPreviewParticles(Cuboidf zoneBox, double regionScale = 1.0)
    {
        double centerX = Pos.X + (zoneBox.X1 + zoneBox.X2) * 0.5;
        double centerY = Pos.Y + (zoneBox.Y1 + zoneBox.Y2) * 0.5;
        double centerZ = Pos.Z + (zoneBox.Z1 + zoneBox.Z2) * 0.5;
        double halfWidth = 0.27 * regionScale;
        double halfHeight = 0.18 * regionScale;
        LogPreviewParticleSpawn(centerX, centerY, centerZ);
        Api.World.SpawnParticles(new SimpleParticleProperties(
            1f,
            1f,
            ColorUtil.ToRgba(128, 240, 250, 255),
            new Vec3d(centerX - halfWidth, centerY - halfHeight, centerZ - halfWidth),
            new Vec3d(centerX + halfWidth, centerY + halfHeight, centerZ + halfWidth),
            new Vec3f(-0.01f, 0.0045f, -0.01f),
            new Vec3f(0.01f, 0.012f, 0.01f),
            1.2f,
            0f,
            0.12f,
            0.20f,
            EnumParticleModel.Cube)
        {
            Async = false,
            IgnoreUserConfig = true,
            LightEmission = 0,
            OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.IDENTICAL, 0f),
            VertexFlags = 255,
            WithTerrainCollision = false
        });
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
            return FaText.Get("Idle");
        }

        double remaining = Math.Max(0, GetProcessDurationHours() - (Api.World.Calendar.TotalHours - processStartHours));
        string label = processMode switch
        {
            ProcessPlateResting => FaText.Get("Plate reacting"),
            ProcessArmorDissolving => FaText.Get("Armor coating dissolving"),
            _ => FaText.Get("Armor coating")
        };
        return FaText.Get("{0}: {1:0.#} in-game hours remaining", FaText.Get(label), remaining);
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
            return FaText.Get("{0}m", minutes);
        }

        return minutes <= 0 ? FaText.Get("{0}h", wholeHours) : FaText.Get("{0}h {1}m", wholeHours, minutes);
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
            || !TryGetCoatingDoseMetal(liquidStack, out string metal)
            || metal != processMetal
            || !TryGetFAArmorInfo(immersedStack, out ResolvedArmor armorInfo)
            || !CanApplyArmorCoating(immersedStack, armorInfo, metal, out _))
        {
            fuelLit = false;
            ClearProcess();
            MarkStationDirty();
            return;
        }

        ApplyArmorCoating(immersedStack, armorInfo, metal);
        liquidStack!.StackSize -= LiquidCapacityItems;
        if (liquidStack.StackSize <= 0)
        {
            liquidStack = null;
        }
        fuelStack = null;
        fuelLit = false;
        ClearProcess();
        MarkStationDirty();
    }

    private void CompleteArmorDissolve()
    {
        if (immersedStack == null
            || !TryGetFAArmorInfo(immersedStack, out ResolvedArmor armorInfo)
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
            return FaText.Get("liquid");
        }

        return stack.GetName();
    }

    private bool CanLightFuel(ItemStack? stack)
    {
        return stack?.Collectible?.Code?.Equals(FirestarterCode) == true
            || stack?.Collectible.HasBehavior("CanIgnite", Api.ClassRegistry) == true;
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

    private MeshData? CreateLiquidMesh(ITesselatorAPI tessThreadTesselator, ItemStack? renderLiquidStack)
    {
        if (Api is not ICoreClientAPI capi || renderLiquidStack == null)
        {
            DebugLiquidLog($"CreateLiquidMesh skipped. ApiIsClient={Api is ICoreClientAPI}, liquidStack={FormatStackDebug(renderLiquidStack)}");
            return null;
        }

        TextureAtlasPosition? liquidTexturePosition = GetLiquidTexturePosition(renderLiquidStack, out int textureSubId);
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

    private MeshData? CreateFuelMesh(ITesselatorAPI tessThreadTesselator, ItemStack? renderFuelStack, bool renderFuelLit)
    {
        if (Api is not ICoreClientAPI capi || !IsStationFuel(renderFuelStack))
        {
            DebugFuelLog($"CreateFuelMesh skipped. ApiIsClient={Api is ICoreClientAPI}, fuelStack={FormatStackDebug(renderFuelStack)}");
            return null;
        }

        TextureAtlasPosition? charcoalTexturePosition;
        lock (liquidTextureCacheLock)
        {
            charcoalTexturePosition = cachedCharcoalTexturePosition;
        }
        if (charcoalTexturePosition == null)
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
        if (TryApplyForgeFuelTexture(shape, renderFuelLit, out string fuelTextureDebug))
        {
            textureSource = new ShapeTextureSource(capi, shape, resolvedShape);
        }
        else
        {
            textureSource = new MappedTextureSource(
                tessThreadTesselator.GetTextureSource(Block, 0, false),
                "coal",
                charcoalTexturePosition
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
        DebugFuelLog($"CreateFuelMesh tesselated. vertices={mesh.VerticesCount}, indices={mesh.IndicesCount}, needsOpaque={mesh.NeedsRenderPass(EnumChunkRenderPass.Opaque)}, lit={renderFuelLit}, fuelTexture={fuelTextureDebug}, shape={resolvedShape}, bounds={FormatMeshBounds(mesh)}");
        return mesh;
    }

    private MeshData? TryCreateTableItemMesh(ITesselatorAPI tessThreadTesselator, ItemStack renderTableStack)
    {
        try
        {
            return CreateTableItemMesh(tessThreadTesselator, renderTableStack);
        }
        catch (Exception exception)
        {
            Api?.Logger.Warning("[FACore CoverStation] Could not render table item {0}: {1}", FormatStackDebug(renderTableStack), exception);
            return null;
        }
    }

    private MeshData? CreateTableItemMesh(ITesselatorAPI tessThreadTesselator, ItemStack renderTableStack)
    {
        if (renderTableStack.Collectible == null)
        {
            return null;
        }

        MeshData? mesh = CreateItemStackMesh(tessThreadTesselator, renderTableStack, "facore-coverstation-tableitem", useStationInputOrientation: true);
        if (mesh == null)
        {
            return null;
        }

        AlignTableItemMesh(mesh, ShouldRotateTableItem(renderTableStack));
        return mesh;
    }

    private void AddCoverPlateMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, IReadOnlyList<ItemStack?> renderCoverPlates)
    {
        IReadOnlyList<StationElementZone> zones = GetCachedElementZones();
        for (int index = 1; index <= 4; index++)
        {
            ItemStack? stack = renderCoverPlates[index - 1];
            if (stack?.Collectible == null || !TryGetDecorationZoneBox(zones, "Plate" + index, out Cuboidf zoneBox)) continue;
            MeshData? mesh = CreateItemStackMesh(tessThreadTesselator, stack, $"facore-coverstation-plate{index}", useStationInputOrientation: true);
            if (mesh == null) continue;
            ApplyGroundTransformOrientation(mesh, stack.Collectible.GroundTransform);
            AlignCoverPlateMesh(
                mesh,
                zoneBox,
                CoverPlateOffsetX,
                CoverPlateOffsetY,
                CoverPlateOffsetZ,
                CoverPlateScale,
                CoverPlateRotationX,
                CoverPlateRotationY,
                CoverPlateRotationZ
            );
            mesher.AddMeshData(mesh, 1);
        }
    }

    private void AlignCoverPlateMesh(MeshData mesh, Cuboidf zoneBox, float xOffset, float yOffset, float zOffset, float scale, float rotX, float rotY, float rotZ)
    {
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ)) return;
        var origin = new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
        mesh.Scale(origin, scale, scale, scale);
        mesh.Rotate(origin, rotX * GameMath.DEG2RAD, rotY * GameMath.DEG2RAD, rotZ * GameMath.DEG2RAD);
        RotateStationInputMesh(mesh, minX, minY, minZ, maxX, maxY, maxZ);
        if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ)) return;
        (xOffset, zOffset) = RotateStationInputOffset(xOffset, zOffset);
        mesh.Translate(
            (zoneBox.X1 + zoneBox.X2) * 0.5f + xOffset - (minX + maxX) * 0.5f,
            zoneBox.Y1 + yOffset - minY,
            (zoneBox.Z1 + zoneBox.Z2) * 0.5f + zOffset - (minZ + maxZ) * 0.5f
        );
    }

    private MeshData? CreateItemStackMesh(ITesselatorAPI tessThreadTesselator, ItemStack stack, string shapeName, bool applyBlockRotation = true, bool useStationInputOrientation = false)
    {
        if (Api is not ICoreClientAPI capi || stack.Collectible == null)
        {
            return null;
        }

        MeshData mesh;
        if (stack.Item != null)
        {
            ITexPositionSource fallbackTextureSource = tessThreadTesselator.GetTextureSource(Block, 0, false);
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
                applyBlockRotation ? new Vec3f(
                    Block.Shape.rotateX,
                    Block.Shape.rotateY - (useStationInputOrientation ? GetStationInputRotationRadians() * GameMath.RAD2DEG : 0f),
                    Block.Shape.rotateZ) : new Vec3f()
            );
        }
        else if (stack.Block != null)
        {
            tessThreadTesselator.TesselateBlock(stack.Block, out mesh);
            AddLiquidContainerContentMesh(tessThreadTesselator, capi, stack, mesh, shapeName);
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

    private void AddLiquidContainerContentMesh(
        ITesselatorAPI tessThreadTesselator,
        ICoreClientAPI capi,
        ItemStack containerStack,
        MeshData baseMesh,
        string shapeName)
    {
        if (containerStack.Block?.Code?.Equals(new AssetLocation("facore", "decorationkit")) != true
            || containerStack.Collectible is not ILiquidInterface liquidInterface)
        {
            return;
        }

        ItemStack? contentStack = liquidInterface.GetContent(containerStack);
        CompositeTexture? contentTexture = contentStack == null ? null : GetLiquidTexture(contentStack);
        Shape? contentShape = Shape.TryGet(Api, new AssetLocation("facore", "shapes/block/decorationkitliquid.json"));
        if (contentStack == null || contentTexture == null || contentShape == null)
        {
            return;
        }

        contentTexture.Bake(capi.Assets);
        capi.BlockTextureAtlas.GetOrInsertTexture(contentTexture, out _, out TextureAtlasPosition texturePosition, 0.005f);
        ITexPositionSource textureSource = new MappedTextureSource(
            tessThreadTesselator.GetTextureSource(containerStack.Block, 0, false),
            "dye",
            texturePosition
        );
        tessThreadTesselator.TesselateShape(
            shapeName + "-content",
            contentShape,
            out MeshData contentMesh,
            textureSource,
            new Vec3f()
        );
        if (contentMesh != null && contentMesh.VerticesCount > 0)
        {
            baseMesh.AddMeshData(contentMesh);
        }
    }

    private void AddDecorationStationMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, WorkStationRenderSnapshot snapshot)
    {
        IReadOnlyList<StationElementZone> zones = GetCachedElementZones();

        AddArmorStandMeshes(mesher, tessThreadTesselator, zones, snapshot);
        foreach ((string piece, string action, int pieceIndex) in new[] { ("head", "HeadDecorations", 0), ("body", "BodyDecorations", 1), ("legs", "LegsDecorations", 2) })
        {
            ItemStack? decorationStack = snapshot.GetDecorationMaterial(pieceIndex);
            if (TryGetBearArmorColor(decorationStack, piece, out _))
            {
                ArmorPieceTransform transform = GetBearDecorationTransform(piece);
                AddDecorationSlotMesh(
                    mesher, tessThreadTesselator, zones, action, decorationStack,
                    transform.OffsetX, transform.OffsetY, transform.OffsetZ, transform.Scale,
                    transform.RotationY, transform.RotationX, transform.RotationZ);
            }
            else
            {
                AddDecorationSlotMesh(
                    mesher, tessThreadTesselator, zones, action, decorationStack,
                    DecorationItemOffsetX, DecorationItemOffsetY, DecorationItemOffsetZ, DecorationItemScale);
            }
            AddDecorationSlotMesh(
                mesher, tessThreadTesselator, zones, action, snapshot.GetDecorationKit(pieceIndex),
                DecorationKitOffsetX, DecorationKitOffsetY, DecorationKitOffsetZ, DecorationKitScale);
            AddDecorationClothMeshes(mesher, tessThreadTesselator, zones, snapshot, pieceIndex, piece, action);
        }
    }

    private static ArmorPieceTransform GetBearDecorationTransform(string piece) => piece switch
    {
        "head" => new(BearHeadOffsetX, BearHeadOffsetY, BearHeadOffsetZ, BearHeadScale, BearHeadRotationX, BearHeadRotationY, BearHeadRotationZ),
        "legs" => new(BearLegsOffsetX, BearLegsOffsetY, BearLegsOffsetZ, BearLegsScale, BearLegsRotationX, BearLegsRotationY, BearLegsRotationZ),
        _ => new(BearBodyOffsetX, BearBodyOffsetY, BearBodyOffsetZ, BearBodyScale, BearBodyRotationX, BearBodyRotationY, BearBodyRotationZ)
    };

    private void AddDecorationClothMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, IReadOnlyList<StationElementZone> zones, WorkStationRenderSnapshot snapshot, int pieceIndex, string piece, string actionName)
    {
        if (!TryGetDecorationZoneBox(zones, actionName, out Cuboidf zoneBox))
        {
            return;
        }

        for (int pile = 1; pile <= 3; pile++)
        {
            ItemStack? clothStack = snapshot.GetDecorationCloth(pieceIndex, pile);
            if (clothStack == null) continue;
            for (int layer = 0; layer < 3; layer++)
            {
                MeshData? mesh = CreateItemStackMesh(tessThreadTesselator, clothStack, $"facore-decorationstation-{piece}-cloth-{pile}-{layer}", useStationInputOrientation: true);
                if (mesh == null) continue;
                ApplyGroundTransformOrientation(mesh, clothStack.Collectible.GroundTransform);
                AlignDecorationClothMesh(mesh, zoneBox, pile - 1, layer);
                mesher.AddMeshData(mesh, 1);
            }
        }
    }

    private void AddTrimStationMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, WorkStationRenderSnapshot snapshot)
    {
        IReadOnlyList<StationElementZone> zones = GetCachedElementZones();

        if (snapshot.TrimArmor != null)
        {
            ArmorPieceTransform placement = GetTrimArmorTransform(snapshot.TrimArmor);
            AddArmorStandMeshes(
                mesher,
                tessThreadTesselator,
                zones,
                [snapshot.TrimArmor],
                ["Armor"],
                "facore-trimstation-armor",
                value => trimArmorRenderDebug = value,
                placement.OffsetX,
                placement.OffsetY,
                placement.OffsetZ,
                placement.Scale,
                placement.RotationX,
                placement.RotationY,
                placement.RotationZ,
                centerLateralToTarget: true,
                stagedTrimPreview: snapshot.PendingTrimRivets != null
            );
        }
        else
        {
            trimArmorRenderDebug = "no armor";
        }

        ItemStack? visibleRivets = snapshot.PendingTrimRivets ?? snapshot.TrimRivets;
        TrimDebugLog($"Render RivetsPlace stack={FormatStackDebug(visibleRivets)} scale={TrimRivetsBowlItemScale}");
        AddDecorationSlotMesh(mesher, tessThreadTesselator, zones, "RivetsPlace", visibleRivets, scaleFactor: TrimRivetsBowlItemScale);
        AddDecorationSlotMesh(
            mesher,
            tessThreadTesselator,
            zones,
            "CruciblePlace",
            snapshot.TrimCrucible,
            yOffset: TrimCrucibleItemYOffset,
            scaleFactor: TrimCrucibleItemScale,
            yawDegrees: TrimCrucibleItemYawDegrees
        );
        AddSolderingIronHolderMesh(mesher, tessThreadTesselator, zones, snapshot.TrimSolderingIron);
    }

    private static ArmorPieceTransform GetTrimArmorTransform(ItemStack stack)
    {
        if (!TryGetFAArmorInfo(stack, out ResolvedArmor armorInfo))
        {
            return TrimBodyTransform;
        }

        return armorInfo.Piece switch
        {
            "head" => TrimHelmetTransform,
            "legs" => TrimLegsTransform,
            _ => TrimBodyTransform
        };
    }

    private void AddSolderingIronHolderMesh(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, IReadOnlyList<StationElementZone> zones, ItemStack? solderingIronStack)
    {
        if (solderingIronStack?.Collectible == null || !TryGetDecorationZoneBox(zones, "SolderHolder", out Cuboidf zoneBox))
        {
            return;
        }

        MeshData? mesh;
        try
        {
            mesh = CreateItemStackMesh(tessThreadTesselator, solderingIronStack, "facore-trimstation-solderholder", useStationInputOrientation: true);
        }
        catch (Exception exception)
        {
            Api?.Logger.Warning("[FACore TrimStation] Could not render soldering iron holder item {0}: {1}", FormatStackDebug(solderingIronStack), exception);
            return;
        }

        if (mesh == null)
        {
            return;
        }

        ApplyGroundTransformOrientation(mesh, solderingIronStack.Collectible.GroundTransform);
        AlignVerticalHolderMesh(mesh, zoneBox, TrimSolderingIronItemScale);
        mesher.AddMeshData(mesh, 1);
    }

    private void AddDecorationSlotMesh(
        ITerrainMeshPool mesher,
        ITesselatorAPI tessThreadTesselator,
        IReadOnlyList<StationElementZone> zones,
        string actionName,
        ItemStack? stack,
        float xOffset = 0f,
        float yOffset = 0f,
        float zOffset = 0f,
        float scaleFactor = 1f,
        float yawDegrees = 0f,
        float rotationXDegrees = 0f,
        float rotationZDegrees = 0f)
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
                "facore-decorationstation-" + actionName.ToLowerInvariant(),
                useStationInputOrientation: true
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
        AlignDecorationSlotMesh(mesh, zoneBox, xOffset, yOffset, zOffset, scaleFactor, rotationXDegrees, yawDegrees, rotationZDegrees);
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

    private static bool TryGetDecorationZoneBox(IReadOnlyList<StationElementZone> zones, string actionName, out Cuboidf zoneBox)
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

    private void AlignDecorationSlotMesh(MeshData mesh, Cuboidf zoneBox, float xOffset, float yOffset, float zOffset, float scaleFactor = 1f, float rotationXDegrees = 0f, float rotationYDegrees = 0f, float rotationZDegrees = 0f)
    {
        float shelfYaw = PrepareDecorationShelfPlacement(ref zoneBox, out string? referenceSide);
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

        if (Math.Abs(rotationXDegrees) > 0.001f || Math.Abs(rotationYDegrees) > 0.001f || Math.Abs(rotationZDegrees) > 0.001f)
        {
            var rotationOrigin = new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
            mesh.Rotate(
                rotationOrigin,
                rotationXDegrees * GameMath.DEG2RAD,
                rotationYDegrees * GameMath.DEG2RAD,
                rotationZDegrees * GameMath.DEG2RAD);

            if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
            {
                return;
            }
        }

        if (!IsDecorationStationBlock())
        {
            RotateStationInputMesh(mesh, minX, minY, minZ, maxX, maxY, maxZ);
        }
        if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
        {
            return;
        }

        // Rest the item on the bottom of its selection box (shelf surface) instead of floating it centered.
        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;
        (float rotatedOffsetX, float rotatedOffsetZ) = RotateDisplayOffset(xOffset, zOffset, referenceSide);
        float targetCenterX = (zoneBox.X1 + zoneBox.X2) * 0.5f + rotatedOffsetX;
        float targetCenterZ = (zoneBox.Z1 + zoneBox.Z2) * 0.5f + rotatedOffsetZ;
        float targetBottomY = zoneBox.Y1 + yOffset;

        mesh.Translate(
            targetCenterX - currentCenterX,
            targetBottomY - minY,
            targetCenterZ - currentCenterZ
        );
        if (shelfYaw != 0f)
        {
            mesh.Rotate(DecorationShelfRotationOrigin, 0f, shelfYaw, 0f);
        }
    }

    private float PrepareDecorationShelfPlacement(ref Cuboidf zoneBox, out string? referenceSide)
    {
        referenceSide = GetStationSideCode();
        if (!IsDecorationStationBlock() || referenceSide is not ("north" or "south")) return 0f;

        referenceSide = referenceSide == "north" ? "west" : "east";
        // Undo the shelf's quarter-turn about the block centre to recover its west/east
        // anchor. Place the entire input there, then turn mesh and offsets together.
        zoneBox = new Cuboidf(
            zoneBox.Z1, zoneBox.Y1, 1f - zoneBox.X2,
            zoneBox.Z2, zoneBox.Y2, 1f - zoneBox.X1);
        return -GameMath.PIHALF;
    }

    // Keep the tuned west/east poses as the reference. Apply ground and custom rotations
    // in that reference orientation before turning the finished input onto the north/south axis.
    private float GetStationInputRotationRadians()
    {
        return (IsDecorationStationBlock() || IsCoverStationBlock() || IsTrimStationBlock())
            && GetStationSideCode() is "north" or "south"
            ? -GameMath.PIHALF
            : 0f;
    }

    private (float X, float Z) RotateStationInputOffset(float x, float z)
    {
        return GetStationInputRotationRadians() != 0f ? (-z, x) : (x, z);
    }

    private void RotateStationInputMesh(MeshData mesh, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        float yaw = GetStationInputRotationRadians();
        if (yaw == 0f) return;

        mesh.Rotate(new Vec3f((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f), 0f, yaw, 0f);
    }

    private (float X, float Z) RotateDisplayOffset(float x, float z, string? side = null)
    {
        return (side ?? GetStationSideCode()) switch
        {
            "east" => (-z, x),
            "south" => (-x, -z),
            "west" => (z, -x),
            _ => (x, z)
        };
    }

    private void AlignVerticalHolderMesh(MeshData mesh, Cuboidf zoneBox, float scaleFactor)
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

        RotateStationInputMesh(mesh, minX, minY, minZ, maxX, maxY, maxZ);
        if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ)) return;

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

    private void AlignDecorationClothMesh(MeshData mesh, Cuboidf zoneBox, int pile, int layer)
    {
        float shelfYaw = PrepareDecorationShelfPlacement(ref zoneBox, out string? referenceSide);
        // Place every pile in the west-facing shelf frame before rotating the whole arrangement.
        // East-facing world offsets otherwise push the cloth into the back and side panels.
        if (referenceSide == "east")
        {
            zoneBox = new Cuboidf(1f - zoneBox.X2, zoneBox.Y1, 1f - zoneBox.Z2,
                1f - zoneBox.X1, zoneBox.Y2, 1f - zoneBox.Z1);
            mesh.Rotate(DecorationShelfRotationOrigin, 0f, GameMath.PI, 0f);
            shelfYaw += GameMath.PI;
        }
        if (!TryGetMeshBounds(mesh, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ))
        {
            return;
        }

        var scaleOrigin = new Vec3f((minX + maxX) * 0.5f, minY, (minZ + maxZ) * 0.5f);
        mesh.Scale(scaleOrigin, DecorationClothPileScale, DecorationClothPileScale, DecorationClothPileScale);

        if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
        {
            return;
        }

        (float rotationX, float rotationY) = layer switch
        {
            1 => (DecorationClothPiece2RotationX, DecorationClothPiece2RotationY),
            2 => (DecorationClothPiece3RotationX, DecorationClothPiece3RotationY),
            _ => (DecorationClothPiece1RotationX, DecorationClothPiece1RotationY)
        };
        float yawDegrees = DecorationClothRotationY + rotationY;

        if (Math.Abs(rotationX) > 0.001f || Math.Abs(yawDegrees) > 0.001f)
        {
            var yawOrigin = new Vec3f((minX + maxX) * 0.5f, minY, (minZ + maxZ) * 0.5f);
            mesh.Rotate(yawOrigin, rotationX * GameMath.DEG2RAD, yawDegrees * GameMath.DEG2RAD, 0f);

            if (!TryGetMeshBounds(mesh, out minX, out minY, out minZ, out maxX, out maxY, out maxZ))
            {
                return;
            }
        }

        float pileOffset = (pile - 1) * DecorationClothPileSpacing;
        (float offsetX, float offsetY, float offsetZ) = layer switch
        {
            1 => (DecorationClothPiece2OffsetX, DecorationClothPiece2OffsetY, DecorationClothPiece2OffsetZ),
            2 => (DecorationClothPiece3OffsetX, DecorationClothPiece3OffsetY, DecorationClothPiece3OffsetZ),
            _ => (DecorationClothPiece1OffsetX, DecorationClothPiece1OffsetY, DecorationClothPiece1OffsetZ)
        };

        offsetZ += DecorationClothPileOffsetZ + pileOffset;
        // Leave clearance behind the cloth's visible edges, including the backing's metal corners.
        offsetX += DecorationClothShelfOffsetX;

        float currentCenterX = (minX + maxX) * 0.5f;
        float currentCenterZ = (minZ + maxZ) * 0.5f;
        float targetCenterX = (zoneBox.X1 + zoneBox.X2) * 0.5f + offsetX;
        float targetCenterZ = (zoneBox.Z1 + zoneBox.Z2) * 0.5f + offsetZ;
        float targetBottomY = zoneBox.Y1 + offsetY;

        mesh.Translate(
            targetCenterX - currentCenterX,
            targetBottomY - minY,
            targetCenterZ - currentCenterZ
        );
        if (shelfYaw != 0f)
        {
            mesh.Rotate(DecorationShelfRotationOrigin, 0f, shelfYaw, 0f);
        }
    }

    private void AddArmorStandMeshes(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator, IReadOnlyList<StationElementZone> zones, WorkStationRenderSnapshot snapshot)
    {
        int renderedPieces = 0;
        foreach ((string piece, int pieceIndex) in new[]
        {
            ("head", 0),
            ("body", 1),
            ("legs", 2)
        })
        {
            ItemStack? stack = snapshot.GetDecorationArmor(pieceIndex);
            if (stack == null) continue;
            renderedPieces++;
            AddArmorStandMeshes(
                mesher, tessThreadTesselator, zones, [stack], ["ArmorPlace"],
                "facore-decorationstation-armorstand-" + piece, value => armorStandRenderDebug = value,
                DecorationArmorOffsetX, DecorationArmorOffsetY, DecorationArmorOffsetZ,
                stagedTrimPreview: snapshot.DecorationPreviews[pieceIndex]);
        }

        if (renderedPieces == 0) armorStandRenderDebug = "no pieces";
    }

    private bool HasActiveDecorationPreview(string piece)
    {
        if (HasAppliedLayeredPreview(piece, "decoration") || HasAppliedLayeredPreview(piece, "color")) return true;
        for (int i = 1; i <= DecorationThreeColorState.ColorsPerPiece; i++)
        {
            if (HasAppliedThreeColorPreview(piece, i)) return true;
        }
        return false;
    }

    private bool HasBakedDecorationIgnoringPreview(ItemStack armorStack, string piece)
    {
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null) return false;

        bool isCurrentMountedArmor = ReferenceEquals(GetDecorationArmorStack(piece), armorStack);
        string decoration = isCurrentMountedArmor && HasAppliedLayeredPreview(piece, "decoration")
            ? GetPendingOriginalValue(piece, "decoration")
            : types.GetString("decoration" + piece, "none") ?? "none";
        return !string.IsNullOrEmpty(decoration) && !string.Equals(decoration, "none", StringComparison.Ordinal);
    }

    private void AddArmorStandMeshes(
        ITerrainMeshPool mesher,
        ITesselatorAPI tessThreadTesselator,
        IReadOnlyList<StationElementZone> zones,
        List<ItemStack> pieces,
        string[] targetZoneNames,
        string shapeName,
        Action<string> setRenderDebug,
        float offsetX = 0f,
        float offsetY = 0f,
        float offsetZ = 0f,
        float armorScale = 1f,
        float rotationX = 0f,
        float rotationY = 0f,
        float rotationZ = 0f,
        bool centerLateralToTarget = false,
        bool stagedTrimPreview = false)
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
            ArmorStandDebugLog("{0} piece(s) stored but none merged onto the stand.", pieces.Count);
            return;
        }

        ITexPositionSource fallback = tessThreadTesselator.GetTextureSource(Block, 0, false);
        var textureSource = new CompositeBlockAtlasTextureSource(capi, new TransparentTextureSource(capi, fallback.AtlasSize), armorTextures);

        try
        {
            tessThreadTesselator.TesselateShape(
                shapeName,
                standShape,
                out MeshData mesh,
                textureSource,
                new Vec3f(),
                stagedTrimPreview ? DecorationPreviewGlowLevel : 0
            );

            if (mesh == null || mesh.VerticesCount <= 0)
            {
                setRenderDebug($"merged={mergedCount}, EMPTY MESH");
                ArmorStandDebugLog("Tesselated mesh was empty (merged={0}).", mergedCount);
                return;
            }

            ForceOpaqueRenderPass(mesh);
            if (stagedTrimPreview) ApplyDecorationPreviewTint(mesh);
            MakeArmorStandMeshDoubleSided(mesh);
            string preBounds = FormatMeshBounds(mesh);
            ArmorStandDebugLog("merged={0}, meshVerts={1}, bounds={2}, frameBounds={3}", mergedCount, mesh.VerticesCount, preBounds, frameBounds);
            MeshData ghostFrameMesh = frameMesh.Clone();
            AlignArmorStandMesh(mesh, zones, frameBounds, frameMesh, targetZoneNames, offsetX, offsetY, offsetZ, armorScale, rotationX, rotationY, rotationZ, centerLateralToTarget);
            mesher.AddMeshData(mesh, 1);
            if (IsDecorationStationBlock()
                && pieces.Exists(stack => TryGetFAArmorInfo(stack, out ResolvedArmor info) && info.Piece == "head"))
            {
                AddGhostSeraphHead(
                    mesher, tessThreadTesselator, capi, zones, frameBounds, ghostFrameMesh,
                    targetZoneNames, offsetX, offsetY, offsetZ, armorScale,
                    rotationX, rotationY, rotationZ, centerLateralToTarget);
            }
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

        ITexPositionSource fallback = tessThreadTesselator.GetTextureSource(Block, 0, false);
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

    private void AddGhostSeraphHead(
        ITerrainMeshPool mesher,
        ITesselatorAPI tessThreadTesselator,
        ICoreClientAPI capi,
        IReadOnlyList<StationElementZone> zones,
        MeshBounds frameBounds,
        MeshData frameMesh,
        string[] targetZoneNames,
        float offsetX,
        float offsetY,
        float offsetZ,
        float armorScale,
        float rotationX,
        float rotationY,
        float rotationZ,
        bool centerLateralToTarget)
    {
        Shape? ghostShape = Shape.TryGet(Api, ArmorStandShapeLocation)?.Clone();
        if (ghostShape?.Elements == null)
        {
            return;
        }

        var roots = new List<ShapeElement>();
        foreach (ShapeElement root in ghostShape.Elements)
        {
            if (KeepOnlyShapeBranch(root, "Head"))
            {
                roots.Add(root);
            }
        }
        ghostShape.Elements = roots.ToArray();
        ghostShape.Animations = null;
        ghostShape.Textures ??= new Dictionary<string, AssetLocation>(StringComparer.Ordinal);
        AssetLocation blackClothTexture = new("game", "block/cloth/linen/black");
        ghostShape.Textures["seraph"] = blackClothTexture;
        ghostShape.Textures["hair"] = blackClothTexture;
        ghostShape.ResolveReferences(Api.Logger, ArmorStandShapeLocation.ToShortString());

        var textureSource = new ShapeTextureSource(capi, ghostShape, ArmorStandShapeLocation.ToShortString());
        tessThreadTesselator.TesselateShape(
            "facore-decorationstation-ghost-head",
            ghostShape,
            out MeshData ghostMesh,
            textureSource,
            new Vec3f()
        );
        if (ghostMesh == null || ghostMesh.VerticesCount == 0)
        {
            return;
        }

        ApplyGhostHeadStyle(ghostMesh);
        AlignArmorStandMesh(
            ghostMesh, zones, frameBounds, frameMesh, targetZoneNames,
            offsetX, offsetY, offsetZ, armorScale,
            rotationX, rotationY, rotationZ, centerLateralToTarget);
        mesher.AddMeshData(ghostMesh, 1);
    }

    private static bool KeepOnlyShapeBranch(ShapeElement element, string targetName)
    {
        if (element.Name == targetName)
        {
            return true;
        }

        if (element.Children == null)
        {
            return false;
        }

        var kept = new List<ShapeElement>();
        foreach (ShapeElement child in element.Children)
        {
            if (KeepOnlyShapeBranch(child, targetName))
            {
                kept.Add(child);
            }
        }
        if (kept.Count == 0)
        {
            return false;
        }

        element.Children = kept.ToArray();
        element.FacesResolved = new ShapeElementFace[6];
        return true;
    }

    private static void ApplyGhostHeadStyle(MeshData mesh)
    {
        ForceTransparentRenderPass(mesh);
        if (mesh.Rgba == null)
        {
            return;
        }

        int rgbaCount = Math.Min(mesh.Rgba.Length, mesh.VerticesCount * 4);
        for (int index = 0; index + 3 < rgbaCount; index += 4)
        {
            mesh.Rgba[index] = 0;
            mesh.Rgba[index + 1] = 0;
            mesh.Rgba[index + 2] = 0;
            mesh.Rgba[index + 3] = 72;
        }
    }

    private bool TryMergeArmorStandPiece(ICoreClientAPI capi, ItemStack stack, Shape standShape, Dictionary<string, CompositeTexture> armorTextures)
    {
        if (!TryGetFAArmorInfo(stack, out ResolvedArmor armorInfo))
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

        if (!TryResolveFAArmorShape(armorInfo, GetArmorShapeVariant(armorInfo, types), out Shape? armorShape, out AssetLocation shapeLocation)
            || armorShape == null)
        {
            return false;
        }

        if (!TryResolveFAArmorPlateTexture(armorInfo, coverMetal, out AssetLocation plateTexture))
        {
            return false;
        }

        AddArmorRenderTextures(armorTextures, armorInfo, types, plateTexture, stripMetal, decoration, color);

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

        ArmorStandDebugLog("merge piece={0} shape={1} stepParented={2}", piece, shapeLocation, merged);
        return merged;
    }

    private void AlignArmorStandMesh(
        MeshData mesh,
        IReadOnlyList<StationElementZone> zones,
        MeshBounds frameBounds,
        MeshData frameMesh,
        string[] targetZoneNames,
        float offsetX,
        float offsetY,
        float offsetZ,
        float armorScale,
        float rotationX,
        float rotationY,
        float rotationZ,
        bool centerLateralToTarget)
    {
        if (!frameBounds.IsValid)
        {
            return;
        }

        if (!TryGetArmorStandTargetRegion(zones, targetZoneNames, out float targetCenterX, out float targetCenterZ, out float targetBottomY, out float targetTopY))
        {
            ArmorStandDebugLog("No target zones found ({0}); skipping placement.", string.Join(",", targetZoneNames));
            return;
        }

        ArmorStandDebugLog("target center=({0:0.##},{1:0.##}) y={2:0.##}..{3:0.##} side={4}; frameBounds={5}; figureBounds={6}", targetCenterX, targetCenterZ, targetBottomY, targetTopY, GetStationSideCode(), frameBounds, FormatMeshBounds(mesh));

        string? placementSide = GetStationSideCode();
        bool useNorthCranePose = IsTrimStationBlock();
        float cranePoseYaw = 0f;
        if (useNorthCranePose)
        {
            // Use one complete crane pose for every facing. Recover the north anchor,
            // apply the tilt and offsets there, then rotate around the block centre.
            // This keeps the helmet's offset on the same side of its tilted frame.
            cranePoseYaw = placementSide switch
            {
                "east" => -GameMath.PIHALF,
                "south" => GameMath.PI,
                "west" => GameMath.PIHALF,
                _ => 0f
            };
            (targetCenterX, targetCenterZ) = placementSide switch
            {
                "east" => (targetCenterZ, 1f - targetCenterX),
                "south" => (1f - targetCenterX, 1f - targetCenterZ),
                "west" => (1f - targetCenterZ, targetCenterX),
                _ => (targetCenterX, targetCenterZ)
            };
            placementSide = "north";
        }

        var origin = new Vec3f(frameBounds.CenterX, frameBounds.CenterY, frameBounds.CenterZ);

        if (armorScale > 0f && Math.Abs(armorScale - 1f) > 0.001f)
        {
            mesh.Scale(origin, armorScale, armorScale, armorScale);
            frameMesh.Scale(origin, armorScale, armorScale, armorScale);
        }

        float rotationXRadians = rotationX * GameMath.DEG2RAD;
        float stationInputYaw = useNorthCranePose ? -GameMath.PIHALF : GetStationInputRotationRadians();
        // Apply the piece's custom rotations in the reference pose before changing facing.
        float rotationYRadians = GetArmorStandFacingRadians(placementSide) + stationInputYaw + rotationY * GameMath.DEG2RAD;
        float rotationZRadians = rotationZ * GameMath.DEG2RAD;
        mesh.Rotate(origin, rotationXRadians, rotationYRadians, rotationZRadians);
        frameMesh.Rotate(origin, rotationXRadians, rotationYRadians, rotationZRadians);
        if (stationInputYaw != 0f)
        {
            mesh.Rotate(origin, 0f, stationInputYaw, 0f);
            frameMesh.Rotate(origin, 0f, stationInputYaw, 0f);
        }
        if (!TryGetMeshBounds(frameMesh, out MeshBounds transformedFrameBounds))
        {
            return;
        }

        if (!TryGetMeshBounds(mesh, out MeshBounds transformedMeshBounds))
        {
            return;
        }

        (float proxyOffsetX, float proxyOffsetZ) = GetArmorStandProxyNudge(placementSide);
        (float forwardOffsetX, float forwardOffsetZ) = GetArmorStandForwardNudge(placementSide);
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

        (offsetX, offsetZ) = useNorthCranePose
            ? (-offsetZ, offsetX)
            : RotateStationInputOffset(offsetX, offsetZ);
        mesh.Translate(
            translateX + offsetX,
            targetBottomY - ArmorStandDropY + offsetY - transformedFrameBounds.MinY,
            translateZ + offsetZ);

        if (cranePoseYaw != 0f)
        {
            mesh.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0f, cranePoseYaw, 0f);
        }
    }

    private static bool TryGetArmorStandTargetRegion(IReadOnlyList<StationElementZone> zones, string[] targetZoneNames, out float centerX, out float centerZ, out float bottomY, out float topY)
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

        _ = TryGetDecorationZoneBox(zones, targetZoneNames[0], out Cuboidf anchor);

        centerX = (anchor.X1 + anchor.X2) * 0.5f;
        centerZ = (anchor.Z1 + anchor.Z2) * 0.5f;
        bottomY = minY;
        topY = maxY;
        return true;
    }

    private float GetArmorStandFacingRadians(string? side = null)
    {
        int quarterTurns = (side ?? GetStationSideCode()) switch
        {
            "east" => 1,
            "south" => 2,
            "west" => 3,
            _ => 0
        };

        return ArmorStandBaseFacingRadians + quarterTurns * GameMath.PIHALF;
    }

    private (float X, float Z) GetArmorStandProxyNudge(string? side = null)
    {
        return (side ?? GetStationSideCode()) switch
        {
            "east" => (0f, ArmorStandPushFromMain),
            "south" => (-ArmorStandPushFromMain, 0f),
            "west" => (0f, -ArmorStandPushFromMain),
            _ => (ArmorStandPushFromMain, 0f)
        };
    }

    private (float X, float Z) GetArmorStandForwardNudge(string? side = null)
    {
        return (side ?? GetStationSideCode()) switch
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
        return TryGetFAArmorInfo(stack, out ResolvedArmor armorInfo)
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

    private MeshData? CreateImmersedItemMesh(ITesselatorAPI tessThreadTesselator, ItemStack? renderImmersedStack, bool hasVisibleLiquid)
    {
        if (Api is not ICoreClientAPI capi || renderImmersedStack?.Item == null)
        {
            return null;
        }

        bool isPlate = TryGetMetalPlateMetal(renderImmersedStack, out _);
        bool isArmor = TryGetFAArmorPiece(renderImmersedStack, out _);
        if (!isPlate && !isArmor)
        {
            DebugLiquidLog($"CreateImmersedItemMesh skipped unsupported stack={FormatStackDebug(renderImmersedStack)}");
            return null;
        }

        Item item = renderImmersedStack.Item;
        ITexPositionSource fallbackTextureSource = tessThreadTesselator.GetTextureSource(Block, 0, false);
        if (!TryResolveImmersedShapeAndTextures(
                capi,
                item,
                renderImmersedStack,
                fallbackTextureSource,
                out Shape? shape,
                out ITexPositionSource textureSource,
                out AssetLocation shapeLocation
            ))
        {
            DebugLiquidLog($"CreateImmersedItemMesh skipped missing preview shape stack={FormatStackDebug(renderImmersedStack)}");
            return null;
        }

        tessThreadTesselator.TesselateShape(
            "facore-coverstation-immersed",
            shape,
            out MeshData mesh,
            textureSource,
            new Vec3f(Block.Shape.rotateX, Block.Shape.rotateY - GetStationInputRotationRadians() * GameMath.RAD2DEG, Block.Shape.rotateZ)
        );

        if (mesh == null || mesh.VerticesCount <= 0)
        {
            DebugLiquidLog($"CreateImmersedItemMesh empty stack={FormatStackDebug(renderImmersedStack)}");
            return null;
        }

        ForceOpaqueRenderPass(mesh);
        AlignImmersedItemMesh(mesh, isPlate, hasVisibleLiquid);
        DebugLiquidLog($"CreateImmersedItemMesh stack={FormatStackDebug(renderImmersedStack)}, isPlate={isPlate}, isArmor={isArmor}, vertices={mesh.VerticesCount}, bounds={FormatMeshBounds(mesh)}");
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

        if (!TryGetFAArmorInfo(stack, out ResolvedArmor armorInfo))
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

        if (!TryResolveFAArmorShape(armorInfo, GetArmorShapeVariant(armorInfo, types), out shape, out shapeLocation))
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
        AddArmorRenderTextures(textures, armorInfo, types, plateTexture, stripMetal, decoration, color);
        textures["seraph"] = new CompositeTexture(new AssetLocation("game", "block/transparent")) { Alpha = 0 };

        textureSource = new CompositeBlockAtlasTextureSource(capi, fallbackTextureSource, textures);
        DebugLiquidLog($"TryResolveFAArmorPreview stack={FormatStackDebug(stack)}, piece={piece}, shape={shapeLocation}, plate={plateTexture}, strip={stripMetal}, decoration={decoration}, color={color}");
        return true;
    }

    private bool TryResolveFAArmorShape(ResolvedArmor armorInfo, string variant, out Shape? shape, out AssetLocation shapeLocation)
    {
        string path = ExpandArmorRenderTemplate(armorInfo.Definition.Rendering.ShapeTemplate, armorInfo, variant, "none");
        shapeLocation = new AssetLocation(armorInfo.Domain, path);
        shape = Shape.TryGet(Api, shapeLocation);
        return shape != null;
    }

    private bool TryResolveFAArmorPlateTexture(ResolvedArmor armorInfo, string coverMetal, out AssetLocation textureBase)
    {
        textureBase = null!;

        ArmorRenderingDefinition rendering = armorInfo.Definition.Rendering;
        string? template = string.Equals(coverMetal, "none", StringComparison.Ordinal)
            ? rendering.BaseTextureTemplate
            : rendering.CoverTextureTemplate;
        if (template == null) return false;
        textureBase = new AssetLocation(armorInfo.Domain, ExpandArmorRenderTemplate(template, armorInfo, "none", coverMetal));
        return TextureAssetExists(textureBase);
    }

    private bool TextureAssetExists(AssetLocation textureBase)
    {
        return Api?.Assets.TryGet(new AssetLocation(textureBase.Domain, $"textures/{textureBase.Path}.png")) != null;
    }

    private static string GetArmorShapeVariant(ResolvedArmor armorInfo, ITreeAttribute types)
    {
        if (armorInfo.Definition.Schema == ArmorAttributeSchema.Layered)
        {
            return types.GetString("decoration" + armorInfo.Piece, "none") ?? "none";
        }

        bool allPlain = true;
        for (int index = 1; index <= DecorationThreeColorState.ColorsPerPiece; index++)
        {
            allPlain &= string.Equals(
                types.GetString(new DecorationColorSlot(armorInfo.Piece, index).ArmorAttributeKey, ArmorCompatibility.ThreeColorNeutral),
                ArmorCompatibility.ThreeColorNeutral,
                StringComparison.Ordinal);
        }
        return allPlain ? "none" : "fancy";
    }

    private static string ExpandArmorRenderTemplate(string template, ResolvedArmor armorInfo, string variant, string cover) =>
        template
            .Replace("{slot}", armorInfo.SlotPrefix, StringComparison.Ordinal)
            .Replace("{style}", armorInfo.Style, StringComparison.Ordinal)
            .Replace("{piece}", armorInfo.Piece, StringComparison.Ordinal)
            .Replace("{metal}", armorInfo.BaseMetal, StringComparison.Ordinal)
            .Replace("{cover}", cover, StringComparison.Ordinal)
            .Replace("{variant}", variant, StringComparison.Ordinal);

    private static void AddArmorRenderTextures(
        Dictionary<string, CompositeTexture> textures,
        ResolvedArmor armorInfo,
        ITreeAttribute types,
        AssetLocation plateTexture,
        string stripMetal,
        string decoration,
        string color)
    {
        string piece = armorInfo.Piece;
        if (armorInfo.Definition.Schema == ArmorAttributeSchema.Layered)
        {
            textures["base" + piece] = new CompositeTexture(plateTexture) { Alpha = 255 };
            textures["strip" + piece] = new CompositeTexture(ArmorRenderAssets.TrimTexture(stripMetal)) { Alpha = 255 };
            textures["color" + piece] = GetFAArmorDecorationTexture(piece, decoration, color);
            return;
        }

        textures["metal"] = new CompositeTexture(plateTexture) { Alpha = 255 };
        for (int index = 1; index <= DecorationThreeColorState.ColorsPerPiece; index++)
        {
            DecorationColorSlot slot = new(piece, index);
            string colorValue = types.GetString(slot.ArmorAttributeKey, ArmorCompatibility.ThreeColorNeutral)
                ?? ArmorCompatibility.ThreeColorNeutral;
            textures[slot.ArmorAttributeKey] = new CompositeTexture(
                ArmorRenderAssets.DecorationTexture(piece, colorValue)) { Alpha = 255 };
        }
    }

    private static CompositeTexture GetFAArmorDecorationTexture(string piece, string decoration, string color)
    {
        if (decoration == "bear")
        {
            return new CompositeTexture(ArmorRenderAssets.BearTexture(color)) { Alpha = 255 };
        }

        if (decoration == "wolf")
        {
            return new CompositeTexture(ArmorRenderAssets.WolfTexture(color)) { Alpha = 255 };
        }

        string textureColor = decoration != "none" && color == "none" ? "plain" : color;
        return new CompositeTexture(ArmorRenderAssets.DecorationTexture(piece, textureColor)) { Alpha = 255 };
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

        RotateStationInputMesh(mesh, minX, minY, minZ, maxX, maxY, maxZ);
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

        RotateStationInputMesh(mesh, minX, minY, minZ, maxX, maxY, maxZ);
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
            tessThreadTesselator.GetTextureSource(Block, 0, false),
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

    private static void MakeArmorStandMeshDoubleSided(MeshData mesh)
    {
        if (mesh.mode != EnumDrawMode.Triangles || mesh.Indices == null || mesh.IndicesCount < 3)
        {
            return;
        }

        // Worn armor shapes are authored as outward-facing shells. The player model normally hides
        // their open interiors, but the station deliberately uses a transparent seraph frame. Add a
        // reverse-wound copy of every triangle so those interiors remain visible in the station preview.
        int originalIndexCount = mesh.IndicesCount - mesh.IndicesCount % 3;
        for (int index = 0; index < originalIndexCount; index += 3)
        {
            int first = mesh.Indices[index];
            int second = mesh.Indices[index + 1];
            int third = mesh.Indices[index + 2];
            mesh.AddIndex(first);
            mesh.AddIndex(third);
            mesh.AddIndex(second);
        }
    }

    private static void ApplyDecorationPreviewTint(MeshData mesh)
    {
        if (mesh.Rgba == null)
        {
            return;
        }

        int rgbaCount = Math.Min(mesh.Rgba.Length, mesh.VerticesCount * 4);
        for (int index = 0; index + 3 < rgbaCount; index += 4)
        {
            mesh.Rgba[index] = (byte)((mesh.Rgba[index] * 7 + 100) / 8);
            mesh.Rgba[index + 1] = (byte)((mesh.Rgba[index + 1] * 7 + 210) / 8);
            mesh.Rgba[index + 2] = (byte)((mesh.Rgba[index + 2] * 7 + 255) / 8);
        }
    }

    private static void ForceTransparentRenderPass(MeshData mesh)
    {
        mesh.WithRenderpasses();

        if (mesh.RenderPassesAndExtraBits != null)
        {
            Array.Fill(mesh.RenderPassesAndExtraBits, (short)EnumChunkRenderPass.Transparent);
        }
    }

    private static Shape? TryReadShapeAsset(IAsset asset)
    {
        return asset.ToObject<Shape>();
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
        string cacheKey = liquid.Collectible.Code.ToString();
        lock (liquidTextureCacheLock)
        {
            if (cachedLiquidTexturePositions.TryGetValue(cacheKey, out TextureAtlasPosition? texturePosition)
                && cachedLiquidTextureSubIds.TryGetValue(cacheKey, out textureSubId))
            {
                return texturePosition;
            }
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
        lock (liquidTextureCacheLock)
        {
            if (cachedLiquidTexturePositions.TryGetValue(cacheKey, out TextureAtlasPosition? cachedTexturePosition)
                && cachedLiquidTextureSubIds.TryGetValue(cacheKey, out int cachedTextureSubId))
            {
                texturePosition = cachedTexturePosition;
                textureSubId = cachedTextureSubId;
                return true;
            }
        }

        CompositeTexture? texture = GetLiquidTexture(liquid);
        if (texture == null)
        {
            DebugLiquidLog($"TryCacheLiquidTexture failed: no texture for {FormatStackDebug(liquid)}");
            return false;
        }

        texture.Bake(capi.Assets);
        capi.BlockTextureAtlas.GetOrInsertTexture(texture, out textureSubId, out TextureAtlasPosition insertedTexturePosition, 0.005f);
        lock (liquidTextureCacheLock)
        {
            cachedLiquidTexturePositions[cacheKey] = insertedTexturePosition;
            cachedLiquidTextureSubIds[cacheKey] = textureSubId;
        }
        texturePosition = insertedTexturePosition;
        DebugLiquidLog($"TryCacheLiquidTexture cached {cacheKey} subId={textureSubId}, tex=({insertedTexturePosition.x1},{insertedTexturePosition.y1})-({insertedTexturePosition.x2},{insertedTexturePosition.y2})");
        return true;
    }

    private void CacheCharcoalTexture()
    {
        lock (liquidTextureCacheLock)
        {
            if (cachedCharcoalTexturePosition != null) return;
        }
        if (Api is not ICoreClientAPI capi)
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
        capi.BlockTextureAtlas.GetOrInsertTexture(texture, out int textureSubId, out TextureAtlasPosition texturePosition, 0.005f);
        lock (liquidTextureCacheLock)
        {
            cachedCharcoalTextureSubId = textureSubId;
            cachedCharcoalTexturePosition = texturePosition;
        }
        DebugFuelLog($"CacheCharcoalTexture cached subId={textureSubId}, tex=({texturePosition.x1},{texturePosition.y1})-({texturePosition.x2},{texturePosition.y2})");
    }

    private void ClearRenderTextureCaches()
    {
        lock (liquidTextureCacheLock)
        {
            cachedSulfurTexturePosition = null;
            cachedSulfurTextureSubId = 0;
            cachedLiquidTexturePositions.Clear();
            cachedLiquidTextureSubIds.Clear();
            cachedCharcoalTexturePosition = null;
            cachedCharcoalTextureSubId = 0;
        }
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

        foreach (DecorationColorSlot slot in DecorationThreeColorState.Slots)
        {
            SetOrRemoveItemstack(tree, slot.ClothTreeKey, GetThreeColorCloth(slot.Piece, slot.Index));
            SetOrRemoveString(tree, slot.OriginalTreeKey, GetThreeColorOriginal(slot.Piece, slot.Index));
        }
    }

    private void ReadDecorationTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        SetDecorationArmorStack("HeadPlace", ResolveItemstack(tree.GetItemstack("decorationHelmetStack"), worldForResolving));
        SetDecorationArmorStack("BodyPlace", ResolveItemstack(tree.GetItemstack("decorationBodyStack"), worldForResolving));
        SetDecorationArmorStack("LegsPlace", ResolveItemstack(tree.GetItemstack("decorationLegsStack"), worldForResolving));
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

        foreach ((ItemStack? armor, string piece) in new[]
        {
            (decorationHelmetStack, "head"),
            (decorationBodyStack, "body"),
            (decorationLegsStack, "legs")
        })
        {
            if (armor?.Attributes?.GetItemstack(GetBakedBearSourceKey(piece)) == null) continue;
            if (!TryResolveBakedBearSource(armor, piece, worldForResolving, out _, out string failure))
            {
                worldForResolving.Logger.Warning("[FACore DecorationStation] Preserved unreadable stored bear source on {0}: {1}", armor.Collectible?.Code, failure);
            }
        }

        foreach (DecorationColorSlot slot in DecorationThreeColorState.Slots)
        {
            ItemStack? cloth = tree.GetItemstack(slot.ClothTreeKey);
            SetThreeColorCloth(slot.Piece, slot.Index, null);
            if (cloth != null)
            {
                bool resolved = cloth.ResolveBlockOrItem(worldForResolving);
                SetThreeColorCloth(slot.Piece, slot.Index, cloth);
                if (!resolved || cloth.StackSize != 3 || !TryGetClothColor(cloth, out _))
                {
                    worldForResolving.Logger.Warning(
                        "[FACore DecorationStation] Preserved rejected cloth data in {0}: resolved={1}, size={2}, code={3}",
                        slot.ClothTreeKey,
                        resolved,
                        cloth.StackSize,
                        cloth.Collectible?.Code);
                }
            }
            SetThreeColorOriginal(slot.Piece, slot.Index, tree.GetString(slot.OriginalTreeKey) ?? "");
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
        DropDecorationStack(ref decorationHelmetStack);
        DropDecorationStack(ref decorationBodyStack);
        DropDecorationStack(ref decorationLegsStack);

        foreach (DecorationColorSlot slot in DecorationThreeColorState.Slots)
        {
            ItemStack? cloth = GetThreeColorCloth(slot.Piece, slot.Index);
            if (cloth != null && (cloth.Collectible != null || cloth.ResolveBlockOrItem(Api.World)))
            {
                Api.World.SpawnItemEntity(cloth, Pos.ToVec3d().Add(0.5, 0.8, 0.5));
            }
            else if (cloth != null)
            {
                Api.Logger.Error("[FACore DecorationStation] Could not drop unresolved cloth from {0} while breaking the station.", slot.ClothTreeKey);
            }
            SetThreeColorCloth(slot.Piece, slot.Index, null);
        }
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
        ResolveStoredContainerContents(trimCrucibleStack, worldForResolving);
        trimSolderingIronStack = ResolveItemstack(tree.GetItemstack("trimSolderingIronStack"), worldForResolving);
        pendingTrimRivetsStack = ResolveItemstack(tree.GetItemstack("pendingTrimRivetsStack"), worldForResolving);
        pendingTrimOriginalStrip = tree.GetString("pendingTrimOriginalStrip") ?? "";
        InvalidateTrimOperation();
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
        InvalidateTrimOperation();
    }

    private void AppendTrimStationDisplayInfo(StringBuilder builder)
    {
        AppendDecorationLine(builder, "Armor", trimArmorStack);
        AppendDecorationLine(builder, FaText.Get("Rims and rivets"), trimRivetsStack);
        AppendDecorationLine(builder, FaText.Get("Solder crucible"), trimCrucibleStack);
        AppendDecorationLine(builder, FaText.Get("Soldering iron"), trimSolderingIronStack);
        AppendDecorationLine(builder, FaText.Get("Staged trim"), pendingTrimRivetsStack);

        AppendTrimRenderDebug(builder);
    }

    private void AppendDecorationStationDisplayInfo(StringBuilder builder)
    {
        AppendDecorationLine(builder, "Helmet", decorationHelmetStack);
        AppendDecorationLine(builder, "Chestplate", decorationBodyStack);
        AppendDecorationLine(builder, "Leggings", decorationLegsStack);
        AppendDecorationLine(builder, FaText.Get("Helmet staged decoration"), pendingHeadDecorationStack);
        AppendDecorationLine(builder, FaText.Get("Helmet staged color"), pendingHeadColorStack);
        AppendDecorationLine(builder, FaText.Get("Chest staged decoration"), pendingBodyDecorationStack);
        AppendDecorationLine(builder, FaText.Get("Chest staged color"), pendingBodyColorStack);
        AppendDecorationLine(builder, FaText.Get("Legs staged decoration"), pendingLegsDecorationStack);
        AppendDecorationLine(builder, FaText.Get("Legs staged color"), pendingLegsColorStack);

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(FaText.Get("ThreeColor piles: "));
        builder.Append(CountThreeColorPiles("head") + CountThreeColorPiles("body") + CountThreeColorPiles("legs"));
        builder.Append('/');
        builder.Append(DecorationThreeColorState.Slots.Count);

        AppendArmorStandRenderDebug(builder);
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void AppendTrimRenderDebug(StringBuilder builder)
    {
        if (Api?.Side != EnumAppSide.Client) return;
        if (builder.Length > 0) builder.AppendLine();
        builder.Append("[trim armor: ");
        builder.Append(trimArmorRenderDebug);
        builder.Append("]");
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void AppendArmorStandRenderDebug(StringBuilder builder)
    {
        if (Api?.Side != EnumAppSide.Client) return;
        builder.AppendLine();
        builder.Append("[armorstand: ");
        builder.Append(armorStandRenderDebug);
        builder.Append("]");
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

    private ItemStack? GetDecorationArmorStack(string actionName)
    {
        return actionName switch
        {
            "HeadPlace" or "head" => decorationHelmetStack,
            "BodyPlace" or "body" => decorationBodyStack,
            "LegsPlace" or "legs" => decorationLegsStack,
            _ => null
        };
    }

    private void SetDecorationArmorStack(string actionName, ItemStack? stack)
    {
        switch (actionName)
        {
            case "HeadPlace":
                decorationHelmetStack = stack;
                InvalidateDecorationOperation("head");
                return;
            case "BodyPlace":
                decorationBodyStack = stack;
                InvalidateDecorationOperation("body");
                return;
            case "LegsPlace":
                decorationLegsStack = stack;
                InvalidateDecorationOperation("legs");
                return;
        }
    }

    internal static bool TryGetClothColor(ItemStack stack, out string color)
    {
        color = "";
        AssetLocation? code = stack.Collectible?.Code;
        if (code == null || (code.Domain != "game" && code.Domain != "facore") || !code.Path.StartsWith("cloth-", StringComparison.Ordinal)) return false;
        string candidate = code.Path["cloth-".Length..];
        if (candidate is not ("plain" or "blue" or "red" or "yellow" or "green" or "purple" or "pink" or "orange" or "brown" or "gray" or "black" or "white" or "burgundy" or "pine" or "navy" or "rose")) return false;
        color = candidate;
        return true;
    }

    private static bool IsDecorationSmallItem(ItemStack stack)
    {
        AssetLocation? code = stack.Collectible?.Code;
        if (code == null || code.Domain != "facore")
        {
            return false;
        }

        string baseCode = code.Path.Split('-')[0];
        return baseCode is "rimsandrivets" or "brackets" or "fasteners" or "ornaments" or "wolfdec" or "decorationkit";
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

    private static bool IsTongs(ItemStack? stack)
    {
        string path = stack?.Collectible?.Code?.Path ?? "";
        return path.Contains("tongs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsToolType(ItemStack? stack, string toolName)
    {
        string path = stack?.Collectible?.Code?.Path ?? "";
        return path.Contains(toolName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScissors(ItemStack? stack)
    {
        string path = stack?.Collectible?.Code?.Path ?? "";
        return path.StartsWith("shears-", StringComparison.Ordinal) || path.StartsWith("scissors-", StringComparison.Ordinal);
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
            failure = MissingTrimSolderCrucibleMessage;
            return false;
        }

        return IsSilverSolderCrucible(trimCrucibleStack, requireHeat: true, out _, out failure);
    }

    private bool TryConsumeTrimSolder(out string failure)
    {
        if (!HasUsableTrimSolderCrucible(out failure))
        {
            return false;
        }

        if (!TryGetSolderContent(trimCrucibleStack, out SolderContent solderContent))
        {
            failure = FaText.Get("The solder crucible content could not be read.");
            return false;
        }

        if (solderContent.Amount < TrimSolderWeldAmount)
        {
            int missing = TrimSolderWeldAmount - solderContent.Amount;
            failure = FaText.Get("Not enough {0} solder: {1}/{2} units. Add {3} more units.", solderContent.Metal, solderContent.Amount, TrimSolderWeldAmount, missing);
            return false;
        }

        if (trimCrucibleStack?.Collectible is not BlockSmeltedContainer crucible || solderContent.Stack == null)
        {
            failure = FaText.Get("The solder crucible content cannot be consumed from this container.");
            return false;
        }

        int remaining = solderContent.Amount - TrimSolderWeldAmount;
        if (remaining > 0)
        {
            crucible.SetContents(trimCrucibleStack, solderContent.Stack, remaining);
            InvalidateTrimOperation();
            return true;
        }

        string? emptiedCode = crucible.Attributes?["emptiedBlockCode"]?.AsString();
        Block? emptyCrucible = string.IsNullOrEmpty(emptiedCode)
            ? null
            : Api.World.GetBlock(AssetLocation.Create(emptiedCode, crucible.Code.Domain));
        if (emptyCrucible == null)
        {
            failure = FaText.Get("The empty crucible type could not be resolved.");
            return false;
        }

        trimCrucibleStack = new ItemStack(emptyCrucible);
        InvalidateTrimOperation();
        return true;
    }

    private bool IsSilverSolderCrucible(ItemStack? stack, bool requireHeat, out string solderMetal, out string failure)
    {
        solderMetal = "";
        failure = "";

        if (!IsCrucible(stack))
        {
            failure = FaText.Get("The crane requires a crucible containing silver solder. Empty crucibles cannot be placed.");
            TrimDebugLog($"SolderCrucible check failed: not crucible stack={FormatStackDebug(stack)}");
            return false;
        }

        if (!TryGetSolderContent(stack, out SolderContent solderContent))
        {
            failure = FaText.Get("The crucible must contain silver solder.");
            TrimDebugLog($"SolderCrucible check failed: no solder content stack={FormatStackDebug(stack)} attrs={FormatTreeDebug(stack?.Attributes)} requireHeat={requireHeat}");
            return false;
        }

        solderMetal = solderContent.Metal;
        TrimDebugLog($"SolderCrucible content metal={solderContent.Metal} amount={solderContent.Amount} temp={solderContent.Temperature:0.#} stack={FormatStackDebug(solderContent.Stack)} requireHeat={requireHeat}");
        if (solderMetal != "silver")
        {
            failure = FaText.Get("The crucible must contain silver solder.");
            return false;
        }

        if (requireHeat && solderContent.Amount < TrimSolderWeldAmount)
        {
            int missing = TrimSolderWeldAmount - solderContent.Amount;
            failure = FaText.Get("Not enough {0} solder: {1}/{2} units. Add {3} more units.", solderContent.Metal, solderContent.Amount, TrimSolderWeldAmount, missing);
            return false;
        }

        if (requireHeat)
        {
            float temperature = solderContent.Temperature;
            if (temperature < TrimSolderSilverMinTemperature)
            {
                failure = FaText.Get("The silver solder is too cold: {0:0}°C. Heat it to at least {1:0}°C.", temperature, TrimSolderSilverMinTemperature);
                return false;
            }
        }

        return true;
    }

    private static bool IsCrucible(ItemStack? stack)
    {
        return stack?.Collectible is BlockSmeltedContainer;
    }

    private bool TryGetSolderContent(ItemStack? stack, out SolderContent solderContent, bool logDebug = true)
    {
        solderContent = default;
        float containerTemperature = stack?.Collectible?.GetTemperature(Api.World, stack) ?? 0f;
        if (logDebug)
        {
            TrimDebugLog($"TryGetSolderContent stack={FormatStackDebug(stack)} containerTemp={containerTemperature:0.#} attrs={FormatTreeDebug(stack?.Attributes)}");
        }

        if (stack?.Collectible is BlockSmeltedContainer crucible)
        {
            KeyValuePair<ItemStack, int> contents = crucible.GetContents(Api.World, stack);
            if (contents.Key?.Collectible?.Code == SilverSolderIngotCode && contents.Value > 0)
            {
                solderContent = new SolderContent("silver", contents.Value, containerTemperature, contents.Key);
                return true;
            }
        }

        if (logDebug)
        {
            TrimDebugLog("TryGetSolderContent rejected unsupported or empty crucible content.");
        }
        return false;
    }

    private static bool TryParseDecorationMaterial(ItemStack stack, string expectedPiece, out string editKind, out string value, out string failure)
    {
        editKind = "";
        value = "";
        failure = "";

        AssetLocation? code = stack.Collectible?.Code;
        if (TryGetSelfColoredDecoration(stack, expectedPiece, out string suppliedDecoration, out _))
        {
            editKind = "decoration";
            value = suppliedDecoration;
            return true;
        }
        if (code?.Domain != "facore")
        {
            failure = FaText.Get("Use Forgotten Armory decoration materials or a matching bear armor part here.");
            return false;
        }

        string path = code.Path;
        if (path == "decorationkit")
        {
            if (stack.Collectible is not ILiquidInterface liquidInterface)
            {
                failure = FaText.Get("That decoration kit cannot hold dye.");
                return false;
            }
            ItemStack? content = liquidInterface.GetContent(stack);
            if (content == null || !TryGetDecorationKitColor(content, out value))
            {
                failure = FaText.Get("Fill the decoration kit with a supported dye first.");
                return false;
            }
            editKind = "color";
            return true;
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
                "head" => FaText.Get("Use ornaments on helmets."),
                "body" => FaText.Get("Use brackets on chestplates."),
                "legs" => FaText.Get("Use fasteners on leggings."),
                _ => FaText.Get("That decoration does not fit this armor piece.")
            };
            return false;
        }

        editKind = "decoration";
        value = path[requiredPrefix.Length..];
        return value.Length > 0;
    }

    private static bool TryGetIntrinsicDecorationColor(ItemStack? stack, string piece, out string color) =>
        TryGetSelfColoredDecoration(stack, piece, out _, out color);

    private static bool TryGetSelfColoredDecoration(ItemStack? stack, string piece, out string decoration, out string color)
    {
        decoration = "";
        color = "";
        if (TryGetBearArmorColor(stack, piece, out color))
        {
            decoration = "bear";
            return true;
        }
        AssetLocation? code = stack?.Collectible?.Code;
        if (code?.Domain != "facore") return false;
        string wolfPrefix = "wolfdec-" + piece + "-";
        if (code.Path.StartsWith(wolfPrefix, StringComparison.Ordinal)
            && ArmorDefinitions.BearColors.Contains(code.Path[wolfPrefix.Length..]))
        {
            decoration = "wolf";
            color = code.Path[wolfPrefix.Length..];
            return true;
        }
        if (piece == "head" && code.Path.StartsWith("ornaments-metallicus-", StringComparison.Ordinal))
        {
            decoration = code.Path["ornaments-".Length..];
            color = "plain";
            return true;
        }
        return false;
    }

    private static bool TryGetBearArmorColor(ItemStack? stack, string expectedPiece, out string color)
    {
        color = "";
        AssetLocation? code = stack?.Collectible?.Code;
        if (code?.Domain != "game") return false;

        string prefix = expectedPiece switch
        {
            "head" => "armor-head-hide-bear-",
            "body" => "armor-body-hide-bear-",
            "legs" => "armor-legs-hide-bear-",
            _ => ""
        };
        if (prefix.Length == 0 || !code.Path.StartsWith(prefix, StringComparison.Ordinal)) return false;

        color = code.Path[prefix.Length..] switch
        {
            "black" => "black",
            "brown" => "brown",
            "panda" => "black",
            "polar" => "white",
            "sun" => "brown",
            _ => ""
        };
        return color.Length > 0;
    }

    private static void DamageRemovedBearArmor(ItemStack stack, string piece)
    {
        if (!TryGetBearArmorColor(stack, piece, out _)) return;
        int maxDurability = stack.Collectible.GetMaxDurability(stack);
        if (maxDurability > 0)
        {
            stack.Collectible.SetDurability(stack, Math.Max(1, (int)Math.Ceiling(maxDurability * 0.05)));
        }
    }

    private static string GetBakedBearSourceKey(string piece) => "facoreBakedBearSource-" + piece;

    private static bool TryResolveBakedBearSource(
        ItemStack armorStack,
        string piece,
        IWorldAccessor world,
        out ItemStack source,
        out string failure)
    {
        source = null!;
        failure = "";
        ItemStack? storedSource = armorStack.Attributes?.GetItemstack(GetBakedBearSourceKey(piece));
        if (storedSource == null)
        {
            failure = FaText.Get("The baked bear decoration has no recoverable source item; it was left unchanged.");
            return false;
        }
        if (!storedSource.ResolveBlockOrItem(world) || storedSource.Collectible == null)
        {
            failure = FaText.Get("The stored bear source item is unavailable. Restore its providing mod before removing this decoration.");
            return false;
        }
        if (!TryGetBearArmorColor(storedSource, piece, out _))
        {
            failure = FaText.Get("The stored bear source does not match this armor slot; the decoration was left unchanged.");
            return false;
        }

        source = storedSource;
        return true;
    }

    private static bool TryGetDecorationKitColor(ItemStack content, out string color)
    {
        color = "";
        AssetLocation? code = content.Collectible?.Code;
        if (code == null || (code.Domain != "game" && code.Domain != "facore")) return false;
        string path = code.Path;
        int separator = path.LastIndexOf('-');
        if (separator < 0 || separator == path.Length - 1) return false;
        string candidate = path[(separator + 1)..];
        if (!ArmorDefinitions.ArmorColors.Contains(candidate) || candidate == "none") return false;
        color = candidate;
        return true;
    }

    private static string GetDecorationEditName(string editKind)
    {
        return editKind == "color" ? FaText.Get("color kit") : FaText.Get("decoration");
    }

    private bool HasPendingDecorationEdits(string piece)
    {
        return HasPendingDecorationEditsLayered(piece) || HasThreeColorCloth(piece);
    }

    private bool HasPendingDecorationEditsLayered(string piece) =>
        GetPendingDecorationMaterial(piece, "decoration") != null || GetPendingDecorationMaterial(piece, "color") != null;

    private bool HasThreeColorCloth(string piece) => CountThreeColorPiles(piece) > 0;

    private bool HasAppliedLayeredPreview(string piece, string editKind) =>
        !string.IsNullOrEmpty(GetPendingOriginalValue(piece, editKind))
        && (GetPendingDecorationMaterial(piece, editKind) != null
            || editKind == "color" && TryGetIntrinsicDecorationColor(GetPendingDecorationMaterial(piece, "decoration"), piece, out _));

    private bool HasAppliedThreeColorPreview(string piece, int index) =>
        GetThreeColorCloth(piece, index) != null
        && !string.IsNullOrEmpty(GetThreeColorOriginal(piece, index));

    private int CountThreeColorPiles(string piece) => threeColorState.CountCloth(piece);

    private ItemStack? GetThreeColorCloth(string piece, int index) => threeColorState.GetCloth(piece, index);

    private void SetThreeColorCloth(string piece, int index, ItemStack? stack)
    {
        threeColorState.SetCloth(piece, index, stack);
        InvalidateDecorationOperation(piece);
    }

    private string GetThreeColorOriginal(string piece, int index) => threeColorState.GetOriginal(piece, index);

    private void SetThreeColorOriginal(string piece, int index, string value)
    {
        threeColorState.SetOriginal(piece, index, value);
        InvalidateDecorationOperation(piece);
    }

    private void RestoreThreeColorValue(ItemStack armorStack, string piece, int index)
    {
        if (!HasAppliedThreeColorPreview(piece, index)) return;
        armorStack.Attributes?.GetTreeAttribute("types")?.SetString(
            new DecorationColorSlot(piece, index).ArmorAttributeKey,
            GetThreeColorOriginal(piece, index));
    }

    private static bool UsesThreeColorSchema(ItemStack armorStack, string piece) =>
        TryGetFAArmorInfo(armorStack, out ResolvedArmor info)
        && info.Piece == piece
        && info.Definition.Schema == ArmorAttributeSchema.ThreeColor;

    private static bool UsesLayeredDecorationSchema(ItemStack armorStack, string piece) =>
        TryGetFAArmorInfo(armorStack, out ResolvedArmor info)
        && info.Piece == piece
        && info.Definition.Schema == ArmorAttributeSchema.Layered;

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
        InvalidateDecorationOperation(piece);
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
        InvalidateDecorationOperation(piece);
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
        if (!HasAppliedLayeredPreview(piece, editKind)) return;
        ITreeAttribute? types = armorStack.Attributes?.GetTreeAttribute("types");
        if (types == null)
        {
            return;
        }

        types.SetString(editKind + piece, GetPendingOriginalValue(piece, editKind));
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

        for (int i = DecorationThreeColorState.ColorsPerPiece; i >= 1; i--)
        {
            if (HasAppliedThreeColorPreview(piece, i)) RestoreThreeColorValue(armorStack, piece, i);
        }

        if (HasAppliedLayeredPreview(piece, "color"))
        {
            RestorePendingDecorationValue(armorStack, piece, "color");
        }

        if (HasAppliedLayeredPreview(piece, "decoration"))
        {
            RestorePendingDecorationValue(armorStack, piece, "decoration");
            if (TryGetIntrinsicDecorationColor(GetPendingDecorationMaterial(piece, "decoration"), piece, out _))
            {
                RestorePendingDecorationValue(armorStack, piece, "color");
            }
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

    private static void ResolveStoredContainerContents(ItemStack? containerStack, IWorldAccessor world)
    {
        ITreeAttribute? attributes = containerStack?.Attributes;
        if (attributes == null)
        {
            return;
        }

        attributes.GetItemstack("output")?.ResolveBlockOrItem(world);
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

    private void PlayTongsSound(IPlayer byPlayer)
    {
        PlayStationSound(TongsSound, byPlayer, 0.65f);
    }

    private void PlayMetalHitSound(IPlayer byPlayer)
    {
        AssetLocation sound = MetalHitSounds[Api.World.Rand.Next(MetalHitSounds.Length)];
        PlayStationSound(sound, byPlayer, 0.85f);
    }

    private void PlayHammerSound(IPlayer byPlayer)
    {
        PlayStationSound(new AssetLocation("facore", "sounds/decorationstation/hammer"), byPlayer, 0.85f);
    }

    private void PlaySawSound(IPlayer byPlayer)
    {
        PlayStationSound(new AssetLocation("facore", "sounds/decorationstation/saw"), byPlayer, 0.7f);
    }

    private void PlayShearsSound(IPlayer byPlayer)
    {
        PlayStationSound(new AssetLocation("facore", "sounds/decorationstation/shears"), byPlayer);
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
        RefreshRenderSnapshot();
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

    private void RefreshRenderSnapshot()
    {
        string[] pieces = ["head", "body", "legs"];
        var cloth = new ItemStack?[pieces.Length, DecorationThreeColorState.ColorsPerPiece];
        var armor = new ItemStack?[pieces.Length];
        var materials = new ItemStack?[pieces.Length];
        var kits = new ItemStack?[pieces.Length];
        var previews = new bool[pieces.Length];
        for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
        {
            string piece = pieces[pieceIndex];
            armor[pieceIndex] = CloneForRender(GetDecorationArmorStack(piece));
            materials[pieceIndex] = CloneForRender(GetPendingDecorationMaterial(piece, "decoration"));
            kits[pieceIndex] = CloneForRender(GetPendingDecorationMaterial(piece, "color"));
            previews[pieceIndex] = HasActiveDecorationPreview(piece);
            for (int colorIndex = 1; colorIndex <= DecorationThreeColorState.ColorsPerPiece; colorIndex++)
            {
                cloth[pieceIndex, colorIndex - 1] = CloneForRender(GetThreeColorCloth(piece, colorIndex));
            }
        }

        var snapshot = new WorkStationRenderSnapshot
        {
            LidOpen = lidOpen,
            FuelOpen = fuelOpen,
            FuelLit = fuelLit,
            Liquid = CloneForRender(liquidStack),
            Immersed = CloneForRender(immersedStack),
            Fuel = CloneForRender(fuelStack),
            Table = CloneForRender(tableStack),
            CoverPlates = [CloneForRender(coverPlate1Stack), CloneForRender(coverPlate2Stack), CloneForRender(coverPlate3Stack), CloneForRender(coverPlate4Stack)],
            DecorationArmor = armor,
            DecorationMaterials = materials,
            DecorationKits = kits,
            DecorationCloth = cloth,
            DecorationPreviews = previews,
            TrimArmor = CloneForRender(trimArmorStack),
            TrimRivets = CloneForRender(trimRivetsStack),
            TrimCrucible = CloneForRender(trimCrucibleStack),
            TrimSolderingIron = CloneForRender(trimSolderingIronStack),
            PendingTrimRivets = CloneForRender(pendingTrimRivetsStack)
        };
        Volatile.Write(ref renderSnapshot, snapshot);
    }

    private static ItemStack? CloneForRender(ItemStack? stack) => stack?.Clone();

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

    [Conditional("FACORE_STATION_DEBUG")]
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

    // Successful interactions already have sounds, animation, and state tooltips. These historical
    // chat calls stay intentionally suppressed; Conditional removes the calls and argument construction.
    [Conditional("FACORE_STATION_INFO")]
    private static void NotifyInfo(IPlayer player, string text)
    {
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void DebugLiquidLog(string message)
    {
        Api?.Logger.Notification("[FACore CoverStation Liquid] {0}: {1}", Pos, message);
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void DebugFuelLog(string message)
    {
        Api?.Logger.Notification("[FACore CoverStation Fuel] {0}: {1}", Pos, message);
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void TrimDebugLog(string message)
    {
        Api?.Logger.Notification("[FACore TrimStation] {0}: {1}", Pos, message);
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void ArmorStandDebugLog(string format, params object?[] args)
    {
        Api?.Logger.Notification("[FACore ArmorStand] " + format, args!);
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void LogPreviewParticleListener()
    {
        if (previewParticleTickLogged) return;
        previewParticleTickLogged = true;
        Api.Logger.Notification("[FACore PreviewParticles] listener active at {0}, block={1}", Pos, Block?.Code);
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void UpdatePreviewParticleDebugState(IReadOnlyList<StationElementZone> zones)
    {
        string state = IsTrimStationBlock()
            ? $"trim armor={trimArmorStack != null}, staged={pendingTrimRivetsStack != null}, zones={zones.Count}"
            : $"decoration head={HasActiveDecorationPreview("head")}, body={HasActiveDecorationPreview("body")}, legs={HasActiveDecorationPreview("legs")}, zones={zones.Count}";
        if (state == previewParticleDebugState) return;
        previewParticleDebugState = state;
        Api.Logger.Notification("[FACore PreviewParticles] {0}: {1}", Pos, state);
    }

    [Conditional("FACORE_STATION_DEBUG")]
    private void LogPreviewParticleSpawn(double centerX, double centerY, double centerZ)
    {
        if (previewParticleSpawnLogged) return;
        previewParticleSpawnLogged = true;
        Api.Logger.Notification("[FACore PreviewParticles] spawning at {0}: center=({1:0.00}, {2:0.00}, {3:0.00})", Pos, centerX, centerY, centerZ);
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

    private readonly struct SolderContent
    {
        public SolderContent(string metal, int amount, float temperature, ItemStack stack)
        {
            Metal = metal;
            Amount = amount;
            Temperature = temperature;
            Stack = stack;
        }

        public string Metal { get; }
        public int Amount { get; }
        public float Temperature { get; }
        public ItemStack Stack { get; }
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
