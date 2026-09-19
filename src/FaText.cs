using System;
using System.Globalization;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace FACore;

/// <summary>Uses the interacting player's locale for server-side validation as well as UI text.</summary>
internal static class FaText
{
    private static readonly AsyncLocal<string?> playerLocale = new();

    public static IDisposable ForPlayer(IPlayer? player)
    {
        string? previous = playerLocale.Value;
        if (player is IServerPlayer serverPlayer) playerLocale.Value = serverPlayer.LanguageCode;
        return new LocaleScope(previous);
    }

    // English templates remain readable at call sites and provide a fallback before assets load.
    public static string Get(string english, params object[] args) =>
        Translate("facore:ui-" + english, english, args);

    public static string GetKey(string key, params object[] args) => Translate(key, key, args);

    public static string Value(string value)
    {
        foreach (string prefix in new[] { "basehead-", "coverhead-", "striphead-", "colorhead-", "decorationhead-", "decorationbody-", "decorationlegs-" })
        {
            string key = "game:" + prefix + value;
            string translated = GetKey(key);
            if (translated != key) return translated;
        }
        return Get(value);
    }

    private static string Translate(string key, string fallback, object[] args)
    {
        string locale = playerLocale.Value ?? Lang.CurrentLocale ?? "en";
        if (Lang.AvailableLanguages.TryGetValue(locale, out var language) && language.HasTranslation(key, false, false))
            return language.Get(key, args);
        if (Lang.AvailableLanguages.TryGetValue("en", out var english) && english.HasTranslation(key, false, false))
            return english.Get(key, args);
        return args.Length == 0 ? fallback : string.Format(CultureInfo.InvariantCulture, fallback, args);
    }

    private sealed class LocaleScope(string? previous) : IDisposable
    {
        public void Dispose() => playerLocale.Value = previous;
    }
}
