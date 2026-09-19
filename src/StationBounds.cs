using Vintagestory.API.MathTools;

namespace FACore;

internal static class StationBounds
{
    public const float PhysicalCollisionHeight = 1.5f;
    public const float StaticSelectionEnvelopeHeight = 2.5f;

    public static Cuboidf[] CreatePhysicalCollisionBoxes() =>
        [new Cuboidf(0f, 0f, 0f, 1f, PhysicalCollisionHeight, 1f)];

    public static Cuboidf[] CreateStaticSelectionEnvelopeBoxes() =>
        [new Cuboidf(0f, 0f, 0f, 1f, StaticSelectionEnvelopeHeight, 1f)];
}
