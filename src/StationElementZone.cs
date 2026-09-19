using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace FACore;

public sealed class StationElementZone
{
    private static readonly IReadOnlyDictionary<string, string> decodingActions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["decodingelement"] = "Decoding",
        ["lecternelement"] = "Lectern",
        ["bowlelement"] = "Bowl",
        ["quillelement"] = "Quill"
    };

    public StationElementZone(
        string elementName,
        Cuboidf stationBox,
        Cuboidf? animatedStationBox = null,
        List<StationElementKeyframe>? animationBoxes = null,
        Func<float, Cuboidf>? animationBoxProvider = null)
    {
        ElementName = elementName;
        this.stationBox = Copy(stationBox);
        this.animatedStationBox = animatedStationBox == null ? null : Copy(animatedStationBox);
        AnimationBoxes = animationBoxes?.AsReadOnly();
        this.animationBoxProvider = animationBoxProvider;
    }

    public string ElementName { get; }
    public string ActionName => ElementName switch
    {
        "Plate1Element" => "Plate1",
        "Plate1Element2" => "Plate2",
        "Plate1Element3" => "Plate3",
        "Plate1Element4" => "Plate4",
        _ when decodingActions.TryGetValue(ElementName, out string? action) => action,
        _ => ElementName.EndsWith("Element") ? ElementName[..^"Element".Length] : ElementName
    };
    public static IEnumerable<string> DecodingElementNames => decodingActions.Keys;
    public static bool IsDecodingElement(string name) => decodingActions.ContainsKey(name);
    public static string ActionNameFor(string name) => decodingActions.TryGetValue(name, out string? action) ? action : name;
    private readonly Cuboidf stationBox;
    private readonly Cuboidf? animatedStationBox;
    private readonly Func<float, Cuboidf>? animationBoxProvider;
    public Cuboidf StationBox => Copy(stationBox);
    public Cuboidf? AnimatedStationBox => animatedStationBox == null ? null : Copy(animatedStationBox);
    public IReadOnlyList<StationElementKeyframe>? AnimationBoxes { get; }

    public bool TryGetAnimationBox(float progress, out Cuboidf box)
    {
        if (animationBoxProvider == null)
        {
            box = null!;
            return false;
        }

        box = animationBoxProvider(progress);
        return true;
    }

    private static Cuboidf Copy(Cuboidf box) => new(box.X1, box.Y1, box.Z1, box.X2, box.Y2, box.Z2);
}

public sealed class StationElementKeyframe
{
    public StationElementKeyframe(float progress, Cuboidf box)
    {
        Progress = progress;
        this.box = new Cuboidf(box.X1, box.Y1, box.Z1, box.X2, box.Y2, box.Z2);
    }

    public float Progress { get; }
    private readonly Cuboidf box;
    public Cuboidf Box => new(box.X1, box.Y1, box.Z1, box.X2, box.Y2, box.Z2);
}
