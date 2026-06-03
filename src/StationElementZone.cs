using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace FACore;

public sealed class StationElementZone
{
    public StationElementZone(string elementName, Cuboidf stationBox, Cuboidf? animatedStationBox = null, List<StationElementKeyframe>? animationBoxes = null)
    {
        ElementName = elementName;
        StationBox = stationBox;
        AnimatedStationBox = animatedStationBox;
        AnimationBoxes = animationBoxes;
    }

    public string ElementName { get; }
    public string ActionName => ElementName.EndsWith("Element") ? ElementName[..^"Element".Length] : ElementName;
    public Cuboidf StationBox { get; }
    public Cuboidf? AnimatedStationBox { get; }
    public List<StationElementKeyframe>? AnimationBoxes { get; }
}

public sealed class StationElementKeyframe
{
    public StationElementKeyframe(float progress, Cuboidf box)
    {
        Progress = progress;
        Box = box;
    }

    public float Progress { get; }
    public Cuboidf Box { get; }
}
