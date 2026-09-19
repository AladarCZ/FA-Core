using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FACore;

public static class StationShapeElementReader
{
    public static IReadOnlyList<StationElementZone> LoadElementZones(ICoreAPI api, Block block)
    {
        var zones = new List<StationElementZone>();
        AssetLocation? shapeLoc = ResolveShapeLocation(block);
        if (shapeLoc == null) return zones.AsReadOnly();

        Shape? shape = Shape.TryGet(api, shapeLoc);
        if (shape == null) return zones.AsReadOnly();

        Dictionary<string, AnimationTrack> animationTracks = LoadAnimationTransforms(shape);
        if (shape.Elements != null)
        {
            float[] identity = Mat4f.Create();
            foreach (ShapeElement element in shape.Elements)
            {
                CollectZones(element, zones, block.LastCodePart(0), animationTracks, identity, block.Variant["purpose"] == "decoding", true);
            }
        }

        return zones.AsReadOnly();
    }

    private static AssetLocation? ResolveShapeLocation(Block block)
    {
        AssetLocation? baseLoc = block.Shape?.Base;
        if (baseLoc == null) return null;

        string domain = string.IsNullOrEmpty(baseLoc.Domain) ? block.Code.Domain : baseLoc.Domain;
        string path = baseLoc.Path;

        bool usesLegacyParts = block.LastCodePart(1) == "main" || block.LastCodePart(1) == "proxy";
        string purpose = usesLegacyParts ? block.LastCodePart(2) : block.LastCodePart(1);
        string part = usesLegacyParts ? block.LastCodePart(1) : "main";
        if (part == "proxy" && !string.IsNullOrEmpty(purpose))
        {
            domain = block.Code.Domain;
            path = $"block/stations/{purpose}station";
        }

        if (!path.StartsWith("shapes/")) path = "shapes/" + path;
        if (!path.EndsWith(".json")) path += ".json";

        return new AssetLocation(domain, path);
    }

    private static void CollectZones(
        ShapeElement element,
        List<StationElementZone> zones,
        string side,
        Dictionary<string, AnimationTrack> animationTracks,
        float[] parentTransform,
        bool isDecodingShape,
        bool parentTransformValid)
    {
        string? name = element.Name;
        double[]? from = element.From;
        double[]? to = element.To;
        double[]? rotationOrigin = element.RotationOrigin;

        float[] elementTransform = parentTransform;
        Cuboidf? modelBox = null;
        bool elementTransformValid = from is { Length: >= 3 }
            && to is { Length: >= 3 }
            && (rotationOrigin == null || rotationOrigin.Length >= 3);
        bool hierarchyTransformValid = parentTransformValid && elementTransformValid;
        if (elementTransformValid)
        {
            float[] localTransform = element.GetLocalTransformMatrix(0);
            elementTransform = Mat4f.Create();
            Mat4f.Mul(elementTransform, parentTransform, localTransform);
            modelBox = new Cuboidf(
                0f,
                0f,
                0f,
                (float)(to![0] - from![0]) / 16f,
                (float)(to[1] - from[1]) / 16f,
                (float)(to[2] - from[2]) / 16f
            ).TransformedCopy(elementTransform);
        }

        bool isDecodingAnchor = isDecodingShape
            && name != null
            && StationElementZone.IsDecodingElement(name);
        if (name != null
            && (name.EndsWith("Element", StringComparison.Ordinal) || name.StartsWith("Plate1Element", StringComparison.Ordinal) || isDecodingAnchor)
            && from?.Length >= 3
            && to?.Length >= 3
            && (!isDecodingAnchor || hierarchyTransformValid))
        {
            string stationSide = GetStationSide(side);
            Cuboidf stationBox = OrientBox(
                modelBox ?? GetElementCuboid(element, from, to, rotationOrigin),
                stationSide
            );
            List<StationElementKeyframe>? animationBoxes = GetAnimatedStationBoxes(name, from, to, rotationOrigin, stationSide, animationTracks);
            System.Func<float, Cuboidf>? animationBoxProvider = GetAnimationBoxProvider(name, from, to, rotationOrigin, stationSide, animationTracks);
            Cuboidf? animatedStationBox = animationBoxProvider?.Invoke(1f)
                ?? (animationBoxes is { Count: > 0 } ? animationBoxes[^1].Box : null);
            zones.Add(new StationElementZone(name, stationBox, animatedStationBox, animationBoxes, animationBoxProvider));
        }

        if (element.Children != null)
        {
            foreach (ShapeElement child in element.Children)
            {
                CollectZones(child, zones, side, animationTracks, elementTransform, isDecodingShape, hierarchyTransformValid);
            }
        }
    }

    private static Dictionary<string, AnimationTrack> LoadAnimationTransforms(Shape shape)
    {
        var result = new Dictionary<string, AnimationTrack>(StringComparer.Ordinal);
        if (shape.Animations == null)
        {
            return result;
        }

        foreach (Animation animation in shape.Animations)
        {
            string? code = animation.Code ?? animation.Name;
            string? elementName = code switch
            {
                "lidopen" => "Lid",
                "fuelopen" => "FuelDoor",
                _ => null
            };

            if (elementName == null)
            {
                continue;
            }

            List<AnimationTransform> transforms = GetElementTransforms(animation.KeyFrames, elementName);
            if (transforms.Count > 0)
            {
                result[elementName] = new AnimationTrack(animation.QuantityFrames, transforms);
            }
        }

        return result;
    }

    private static List<AnimationTransform> GetElementTransforms(AnimationKeyFrame[]? keyframes, string elementName)
    {
        var transforms = new List<AnimationTransform>();
        if (keyframes == null)
        {
            return transforms;
        }

        foreach (AnimationKeyFrame keyframe in keyframes)
        {
            if (keyframe.Elements != null && keyframe.Elements.TryGetValue(elementName, out AnimationKeyFrameElement? transform))
            {
                transforms.Add(new AnimationTransform(keyframe.Frame, transform));
            }
        }

        transforms.Sort((first, second) => first.Frame.CompareTo(second.Frame));
        return transforms;
    }

    private static Cuboidf ToCuboid(double[] from, double[] to)
    {
        float x1 = (float)Math.Min(from[0], to[0]) / 16f;
        float y1 = (float)Math.Min(from[1], to[1]) / 16f;
        float z1 = (float)Math.Min(from[2], to[2]) / 16f;
        float x2 = (float)Math.Max(from[0], to[0]) / 16f;
        float y2 = (float)Math.Max(from[1], to[1]) / 16f;
        float z2 = (float)Math.Max(from[2], to[2]) / 16f;
        return new Cuboidf(x1, y1, z1, x2, y2, z2);
    }

    private static Cuboidf GetElementCuboid(ShapeElement element, double[] from, double[] to, double[]? rotationOrigin)
    {
        float rotationX = (float)element.RotationX;
        float rotationY = (float)element.RotationY;
        float rotationZ = (float)element.RotationZ;

        if (rotationOrigin is not { Length: >= 3 }
            || rotationX == 0f && rotationY == 0f && rotationZ == 0f)
        {
            return ToCuboid(from, to);
        }

        return TransformCuboid(from, to, rotationOrigin, new Vec3f(), rotationX, rotationY, rotationZ);
    }

    private static List<StationElementKeyframe>? GetAnimatedStationBoxes(string elementName, double[] from, double[] to, double[]? rotationOrigin, string side, Dictionary<string, AnimationTrack> animationTracks)
    {
        if (!TryGetAnimationTrack(elementName, rotationOrigin, animationTracks, out double[] origin, out AnimationTrack track))
        {
            return null;
        }

        var boxes = new List<StationElementKeyframe>(track.Transforms.Count);
        foreach (AnimationTransform animationTransform in track.Transforms)
        {
            Cuboidf transformed = TransformCuboid(
                from,
                to,
                origin,
                animationTransform.Offset,
                animationTransform.Rotation.X,
                animationTransform.Rotation.Y,
                animationTransform.Rotation.Z
            );

            boxes.Add(new StationElementKeyframe(track.ToProgress(animationTransform.Frame), OrientBox(transformed, side)));
        }

        return boxes;
    }

    private static System.Func<float, Cuboidf>? GetAnimationBoxProvider(
        string elementName,
        double[] from,
        double[] to,
        double[]? rotationOrigin,
        string side,
        Dictionary<string, AnimationTrack> animationTracks)
    {
        if (!TryGetAnimationTrack(elementName, rotationOrigin, animationTracks, out double[] origin, out AnimationTrack track))
        {
            return null;
        }

        double[] sourceFrom = (double[])from.Clone();
        double[] sourceTo = (double[])to.Clone();
        double[] sourceOrigin = (double[])origin.Clone();
        return progress =>
        {
            AnimationTransform transform = track.Evaluate(progress);
            return OrientBox(
                TransformCuboid(sourceFrom, sourceTo, sourceOrigin, transform.Offset, transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z),
                side
            );
        };
    }

    private static bool TryGetAnimationTrack(
        string elementName,
        double[]? rotationOrigin,
        Dictionary<string, AnimationTrack> animationTracks,
        out double[] origin,
        out AnimationTrack track)
    {
        origin = rotationOrigin!;
        track = null!;
        if (rotationOrigin is not { Length: >= 3 }) return false;

        string? animatedElementName = elementName switch
        {
            "LidOpenElement" => "Lid",
            "FuelDoorElement" => "FuelDoor",
            _ => null
        };

        if (animatedElementName != null && animationTracks.TryGetValue(animatedElementName, out AnimationTrack? foundTrack))
        {
            track = foundTrack;
            return true;
        }

        return false;
    }

    private static Cuboidf TransformCuboid(double[] from, double[] to, double[] origin, Vec3f offset, float rotXDeg, float rotYDeg, float rotZDeg)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        float maxZ = float.MinValue;

        for (int xi = 0; xi < 2; xi++)
        {
            for (int yi = 0; yi < 2; yi++)
            {
                for (int zi = 0; zi < 2; zi++)
                {
                    Vec3f point = new((float)(xi == 0 ? from[0] : to[0]), (float)(yi == 0 ? from[1] : to[1]), (float)(zi == 0 ? from[2] : to[2]));
                    Vec3f transformed = RotatePoint(point, origin, rotXDeg, rotYDeg, rotZDeg);
                    transformed.X += offset.X;
                    transformed.Y += offset.Y;
                    transformed.Z += offset.Z;

                    minX = Math.Min(minX, transformed.X);
                    minY = Math.Min(minY, transformed.Y);
                    minZ = Math.Min(minZ, transformed.Z);
                    maxX = Math.Max(maxX, transformed.X);
                    maxY = Math.Max(maxY, transformed.Y);
                    maxZ = Math.Max(maxZ, transformed.Z);
                }
            }
        }

        return new Cuboidf(minX / 16f, minY / 16f, minZ / 16f, maxX / 16f, maxY / 16f, maxZ / 16f);
    }

    private static Vec3f RotatePoint(Vec3f point, double[] origin, float rotXDeg, float rotYDeg, float rotZDeg)
    {
        double x = point.X - origin[0];
        double y = point.Y - origin[1];
        double z = point.Z - origin[2];

        if (rotXDeg != 0)
        {
            double rad = rotXDeg * GameMath.DEG2RAD;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double ny = y * cos - z * sin;
            double nz = y * sin + z * cos;
            y = ny;
            z = nz;
        }

        if (rotYDeg != 0)
        {
            double rad = rotYDeg * GameMath.DEG2RAD;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double nx = x * cos + z * sin;
            double nz = -x * sin + z * cos;
            x = nx;
            z = nz;
        }

        if (rotZDeg != 0)
        {
            double rad = rotZDeg * GameMath.DEG2RAD;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double nx = x * cos - y * sin;
            double ny = x * sin + y * cos;
            x = nx;
            y = ny;
        }

        return new Vec3f((float)(x + origin[0]), (float)(y + origin[1]), (float)(z + origin[2]));
    }

    private static Cuboidf OrientBox(Cuboidf box, string side)
    {
        Span<Vec2f> corners =
        [
            new Vec2f(box.X1, box.Z1),
            new Vec2f(box.X1, box.Z2),
            new Vec2f(box.X2, box.Z1),
            new Vec2f(box.X2, box.Z2)
        ];

        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxZ = float.MinValue;

        foreach (Vec2f corner in corners)
        {
            Vec2f rotated = OrientPoint(corner, side);
            minX = Math.Min(minX, rotated.X);
            minZ = Math.Min(minZ, rotated.Y);
            maxX = Math.Max(maxX, rotated.X);
            maxZ = Math.Max(maxZ, rotated.Y);
        }

        return new Cuboidf(minX, box.Y1, minZ, maxX, box.Y2, maxZ);
    }

    private static Vec2f OrientPoint(Vec2f point, string side)
    {
        return side switch
        {
            "east" => new Vec2f(point.Y, 1f - point.X),
            "south" => new Vec2f(1f - point.X, 1f - point.Y),
            "west" => new Vec2f(1f - point.Y, point.X),
            _ => point
        };
    }

    private static string GetStationSide(string side)
    {
        return side switch
        {
            "north" => "north",
            "east" => "west",
            "south" => "south",
            "west" => "east",
            _ => side
        };
    }

    private sealed class AnimationTransform
    {
        public AnimationTransform(float frame, AnimationKeyFrameElement transform)
        {
            Frame = frame;
            Offset = new Vec3f((float)(transform.OffsetX ?? 0), (float)(transform.OffsetY ?? 0), (float)(transform.OffsetZ ?? 0));
            Rotation = new Vec3f((float)(transform.RotationX ?? 0), (float)(transform.RotationY ?? 0), (float)(transform.RotationZ ?? 0));
        }

        private AnimationTransform(float frame, Vec3f offset, Vec3f rotation)
        {
            Frame = frame;
            Offset = offset;
            Rotation = rotation;
        }

        public float Frame { get; }
        public Vec3f Offset { get; }
        public Vec3f Rotation { get; }

        public static AnimationTransform Lerp(AnimationTransform first, AnimationTransform second, float progress)
        {
            return new AnimationTransform(
                GameMath.Lerp(first.Frame, second.Frame, progress),
                new Vec3f(
                    GameMath.Lerp(first.Offset.X, second.Offset.X, progress),
                    GameMath.Lerp(first.Offset.Y, second.Offset.Y, progress),
                    GameMath.Lerp(first.Offset.Z, second.Offset.Z, progress)
                ),
                new Vec3f(
                    GameMath.Lerp(first.Rotation.X, second.Rotation.X, progress),
                    GameMath.Lerp(first.Rotation.Y, second.Rotation.Y, progress),
                    GameMath.Lerp(first.Rotation.Z, second.Rotation.Z, progress)
                )
            );
        }
    }

    private sealed class AnimationTrack
    {
        public AnimationTrack(int quantityFrames, List<AnimationTransform> transforms)
        {
            QuantityFrames = Math.Max(1, quantityFrames);
            Transforms = transforms.AsReadOnly();
        }

        public int QuantityFrames { get; }
        public IReadOnlyList<AnimationTransform> Transforms { get; }

        public float ToProgress(float frame) => QuantityFrames <= 1 ? 0f : frame / (QuantityFrames - 1f);

        public AnimationTransform Evaluate(float progress)
        {
            float frame = GameMath.Clamp(progress, 0f, 1f) * Math.Max(0, QuantityFrames - 1);
            if (Transforms.Count == 1) return Transforms[0];

            int rightIndex = 0;
            while (rightIndex < Transforms.Count && Transforms[rightIndex].Frame <= frame) rightIndex++;

            AnimationTransform left;
            AnimationTransform right;
            float rightFrame;
            float sampleFrame = frame;
            if (rightIndex == 0 || rightIndex == Transforms.Count)
            {
                left = Transforms[^1];
                right = Transforms[0];
                rightFrame = right.Frame + QuantityFrames;
                if (sampleFrame < left.Frame) sampleFrame += QuantityFrames;
            }
            else
            {
                left = Transforms[rightIndex - 1];
                right = Transforms[rightIndex];
                rightFrame = right.Frame;
            }

            float frameDistance = rightFrame - left.Frame;
            float amount = frameDistance <= 0f ? 0f : (sampleFrame - left.Frame) / frameDistance;
            return AnimationTransform.Lerp(left, right, GameMath.Clamp(amount, 0f, 1f));
        }
    }
}
