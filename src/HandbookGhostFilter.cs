using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FACore.ArmorCommands;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace FACore;

/// <summary>
/// Client-only handbook fix. Delete this file and rebuild to remove it completely.
/// ModSystem discovery registers it automatically; no project or asset edits are needed.
/// </summary>
public sealed class HandbookGhostFilter : ModSystem
{
    private const string PatchId = "facore.handbookghostfilter";
    private static readonly MethodInfo PrefixMethod = typeof(HandbookGhostFilter).GetMethod(nameof(Before), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo PostfixMethod = typeof(HandbookGhostFilter).GetMethod(nameof(After), BindingFlags.NonPublic | BindingFlags.Static)!;
    private object? harmony;
    private MethodInfo? target;
    private MethodInfo? unpatch;
    private ICoreClientAPI? clientApi;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        clientApi = api;
        try
        {
            target = typeof(CollectibleBehaviorHandbookTextAndExtraInfo).GetMethod(
                "addIngredientForInfo", BindingFlags.Instance | BindingFlags.NonPublic);
            if (target?.ReturnType != typeof(bool)
                || !target.GetParameters().Any(p => p.Name == "components" && p.ParameterType == typeof(List<RichTextComponentBase>))
                || !target.GetParameters().Any(p => p.Name == "haveText" && p.ParameterType == typeof(bool)))
            {
                throw new MissingMethodException("Unsupported handbook ingredient-list method.");
            }

            // Use the game's Harmony without adding a csproj reference or bundling a second copy.
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "0Harmony")
                ?? Assembly.Load("0Harmony");
            Type harmonyType = assembly.GetType("HarmonyLib.Harmony", true)!;
            Type methodType = assembly.GetType("HarmonyLib.HarmonyMethod", true)!;
            unpatch = harmonyType.GetMethod("Unpatch", new[] { typeof(MethodBase), typeof(MethodInfo) })
                ?? throw new MissingMethodException("Harmony.Unpatch");
            MethodInfo patch = harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(MethodBase));
            harmony = Activator.CreateInstance(harmonyType, PatchId);
            object?[] arguments = patch.GetParameters().Select(p => p.Name switch
            {
                "original" => (object?)target,
                "prefix" => Activator.CreateInstance(methodType, PrefixMethod),
                "postfix" => Activator.CreateInstance(methodType, PostfixMethod),
                _ => null
            }).ToArray();
            patch.Invoke(harmony, arguments);
        }
        catch (Exception exception)
        {
            Dispose();
            api.Logger.Warning("[FA-Core] Handbook ghost filter disabled: {0}", exception.GetBaseException().Message);
        }
    }

    public override void Dispose()
    {
        try
        {
            if (harmony != null && target != null && unpatch != null)
            {
                unpatch.Invoke(harmony, new object[] { target, PrefixMethod });
                unpatch.Invoke(harmony, new object[] { target, PostfixMethod });
            }
        }
        catch (Exception exception)
        {
            clientApi?.Logger.Warning("[FA-Core] Could not remove handbook ghost filter: {0}", exception.GetBaseException().Message);
        }
        finally
        {
            harmony = null;
        }
    }

    private static void Before(List<RichTextComponentBase> components, bool haveText, out (int Start, bool HadText) __state)
    {
        __state = (components.Count, haveText);
    }

    private static void After(List<RichTextComponentBase> components, (int Start, bool HadText) __state, ref bool __result)
    {
        bool changed = false;
        // Only inspect components appended by Ingredient for, never Created by or the page's main item.
        for (int i = components.Count - 1; i >= __state.Start; i--)
        {
            if (components[i] is not SlideshowItemstackTextComponent slideshow
                || slideshow.overrideCurrentItemStack != null || slideshow.Itemstacks == null) continue;

            ItemStack[] visible = slideshow.Itemstacks.Where(stack => !IsGhost(stack)).ToArray();
            if (visible.Length == slideshow.Itemstacks.Length) continue;
            changed = true;
            if (visible.Length > 0) slideshow.Itemstacks = visible;
            else
            {
                components.RemoveAt(i);
                slideshow.Dispose();
            }
        }

        // Remove an empty vanilla heading, but retain any other mod's extra content.
        if (changed && components.Skip(__state.Start).All(component => component is ClearFloatTextComponent
            || component.GetType() == typeof(RichTextComponent)
                && ((RichTextComponent)component).DisplayText == Lang.Get("Ingredient for") + "\n"))
        {
            foreach (RichTextComponentBase component in components.Skip(__state.Start)) component.Dispose();
            components.RemoveRange(__state.Start, components.Count - __state.Start);
            __result = __state.HadText;
        }
    }

    private static bool IsGhost(ItemStack? stack)
    {
        if (!ArmorResolver.Resolve(stack, out ResolvedArmor? armor).Allowed || armor == null) return false;
        ITreeAttribute? types = stack!.Attributes?.GetTreeAttribute("types");
        string[] fields = armor.Definition.Schema == ArmorAttributeSchema.ThreeColor
            ? new[] { "color1", "color2", "color3" }
            : new[] { "cover", "strip", "decoration", "color" };

        // "none" is a valid variant. Only missing/empty appearance fields count as ghosts.
        // Do not validate decoration combinations here: complete stacks must remain visible.
        return fields.Any(field => string.IsNullOrWhiteSpace((types?[field + armor.Piece] as StringAttribute)?.value));
    }
}
