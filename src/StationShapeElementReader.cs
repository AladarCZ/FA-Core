using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FACore;

public static class StationShapeElementReader
{
    public static List<StationElementZone> LoadElementZones(ICoreAPI api, Block block)
    {
        var zones = new List<StationElementZone>();
        AssetLocation? shapeLoc = ResolveShapeLocation(block);
        if (shapeLoc == null) return zones;

        Shape? shape = Shape.TryGet(api, shapeLoc);
        if (shape == null) return zones;

        Dictionary<string, List<AnimationTransform>> animationTransforms = LoadAnimationTransforms(shape);
        object? elements = GetMember(shape, "Elements");
        if (elements is IEnumerable enumerable)
        {
            foreach (object element in enumerable)
            {
                CollectZones(element, zones, block.LastCodePart(0), animationTransforms);
            }
        }

        return zones;
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

    private static void CollectZones(object element, List<StationElementZone> zones, string side, Dictionary<string, List<AnimationTransform>> animationTransforms)
    {
        string? name = GetMember(element, "Name") as string;
        float[]? from = ReadFloatArray(GetMember(element, "From"));
        float[]? to = ReadFloatArray(GetMember(element, "To"));
        float[]? rotationOrigin = ReadFloatArray(GetMember(element, "RotationOrigin"));

        if (name != null && name.EndsWith("Element", StringComparison.Ordinal) && from?.Length >= 3 && to?.Length >= 3)
        {
            string stationSide = GetStationSide(side);
            Cuboidf stationBox = OrientBox(GetElementCuboid(element, from, to, rotationOrigin), stationSide);
            List<StationElementKeyframe>? animationBoxes = GetAnimatedStationBoxes(name, from, to, rotationOrigin, stationSide, animationTransforms);
            Cuboidf? animatedStationBox = animationBoxes is { Count: > 0 } ? animationBoxes[^1].Box : null;
            zones.Add(new StationElementZone(name, stationBox, animatedStationBox, animationBoxes));
        }

        object? children = GetMember(element, "Children");
        if (children is IEnumerable enumerable)
        {
            foreach (object child in enumerable)
            {
                CollectZones(child, zones, side, animationTransforms);
            }
        }
    }

    private static Dictionary<string, List<AnimationTransform>> LoadAnimationTransforms(Shape shape)
    {
        var result = new Dictionary<string, List<AnimationTransform>>(StringComparer.Ordinal);
        object? animations = GetMember(shape, "Animations");
        if (animations is not IEnumerable enumerable)
        {
            return result;
        }

        foreach (object animation in enumerable)
        {
            string? code = GetMember(animation, "Code") as string ?? GetMember(animation, "Name") as string;
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

            object? keyframes = GetMember(animation, "KeyFrames") ?? GetMember(animation, "Keyframes");
            List<AnimationTransform> transforms = GetElementTransforms(keyframes, elementName);
            if (transforms.Count > 0)
            {
                result[elementName] = transforms;
            }
        }

        return result;
    }

    private static List<AnimationTransform> GetElementTransforms(object? keyframes, string elementName)
    {
        var transforms = new List<AnimationTransform>();
        if (keyframes is not IEnumerable enumerable)
        {
            return transforms;
        }

        float maxFrame = 0f;
        foreach (object keyframe in enumerable)
        {
            maxFrame = Math.Max(maxFrame, Convert.ToSingle(GetMember(keyframe, "Frame") ?? 0f));
        }

        foreach (object keyframe in enumerable)
        {
            float frame = Convert.ToSingle(GetMember(keyframe, "Frame") ?? 0f);
            object? elements = GetMember(keyframe, "Elements");
            object? transform = GetDictionaryValue(elements, elementName);
            if (transform != null)
            {
                transforms.Add(new AnimationTransform(maxFrame <= 0 ? 0f : frame / maxFrame, transform));
            }
        }

        transforms.Sort((first, second) => first.Progress.CompareTo(second.Progress));
        return transforms;
    }

    private static object? GetDictionaryValue(object? dictionary, string key)
    {
        if (dictionary is IDictionary genericDictionary && genericDictionary.Contains(key))
        {
            return genericDictionary[key];
        }

        return dictionary?.GetType().GetProperty("Item")?.GetValue(dictionary, [key]);
    }

    private static Cuboidf ToCuboid(float[] from, float[] to)
    {
        float x1 = Math.Min(from[0], to[0]) / 16f;
        float y1 = Math.Min(from[1], to[1]) / 16f;
        float z1 = Math.Min(from[2], to[2]) / 16f;
        float x2 = Math.Max(from[0], to[0]) / 16f;
        float y2 = Math.Max(from[1], to[1]) / 16f;
        float z2 = Math.Max(from[2], to[2]) / 16f;
        return new Cuboidf(x1, y1, z1, x2, y2, z2);
    }

    private static Cuboidf GetElementCuboid(object element, float[] from, float[] to, float[]? rotationOrigin)
    {
        float rotationX = GetFloatMember(element, "RotationX");
        float rotationY = GetFloatMember(element, "RotationY");
        float rotationZ = GetFloatMember(element, "RotationZ");

        if (rotationOrigin is not { Length: >= 3 }
            || rotationX == 0f && rotationY == 0f && rotationZ == 0f)
        {
            return ToCuboid(from, to);
        }

        return TransformCuboid(from, to, rotationOrigin, new Vec3f(), rotationX, rotationY, rotationZ);
    }

    private static List<StationElementKeyframe>? GetAnimatedStationBoxes(string elementName, float[] from, float[] to, float[]? rotationOrigin, string side, Dictionary<string, List<AnimationTransform>> animationTransforms)
    {
        if (rotationOrigin is not { Length: >= 3 } origin)
        {
            return null;
        }

        string? animatedElementName = elementName switch
        {
            "LidOpenElement" => "Lid",
            "FuelDoorElement" => "FuelDoor",
            _ => null
        };

        if (animatedElementName == null || !animationTransforms.TryGetValue(animatedElementName, out List<AnimationTransform>? transforms))
        {
            return null;
        }

        var boxes = new List<StationElementKeyframe>(transforms.Count);
        foreach (AnimationTransform animationTransform in transforms)
        {
            object transform = animationTransform.Transform;
            Vec3f offset = new(
                GetFloatMember(transform, "OffsetX"),
                GetFloatMember(transform, "OffsetY"),
                GetFloatMember(transform, "OffsetZ")
            );

            Cuboidf transformed = TransformCuboid(
                from,
                to,
                origin,
                offset,
                GetFloatMember(transform, "RotationX"),
                GetFloatMember(transform, "RotationY"),
                GetFloatMember(transform, "RotationZ")
            );

            boxes.Add(new StationElementKeyframe(animationTransform.Progress, OrientBox(transformed, side)));
        }

        return boxes;
    }

    private static Cuboidf TransformCuboid(float[] from, float[] to, float[] origin, Vec3f offset, float rotXDeg, float rotYDeg, float rotZDeg)
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
                    Vec3f point = new(xi == 0 ? from[0] : to[0], yi == 0 ? from[1] : to[1], zi == 0 ? from[2] : to[2]);
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

    private static Vec3f RotatePoint(Vec3f point, float[] origin, float rotXDeg, float rotYDeg, float rotZDeg)
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

    private static object? GetMember(object instance, string name)
    {
        Type type = instance.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return type.GetField(name, flags)?.GetValue(instance)
            ?? type.GetProperty(name, flags)?.GetValue(instance);
    }

    private static float GetFloatMember(object instance, string name)
    {
        object? value = GetMember(instance, name);
        return value == null ? 0f : Convert.ToSingle(value);
    }

    private static float[]? ReadFloatArray(object? value)
    {
        if (value is null) return null;

        if (value is Vec3f vec)
        {
            return [vec.X, vec.Y, vec.Z];
        }

        if (value is IEnumerable enumerable)
        {
            var list = new List<float>();
            foreach (object? item in enumerable)
            {
                if (item == null) continue;
                list.Add(Convert.ToSingle(item));
            }

            return list.ToArray();
        }

        return null;
    }

    private sealed class AnimationTransform
    {
        public AnimationTransform(float progress, object transform)
        {
            Progress = progress;
            Transform = transform;
        }

        public float Progress { get; }
        public object Transform { get; }
    }
}
