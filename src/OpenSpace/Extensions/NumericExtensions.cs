using System.Numerics;

namespace OpenSpace.Extensions;

public static class NumericExtensions
{
    public static Vector2 ToNumVector2(this EngineKit.Mathematics.Vector2 vector)
    {
        return new Vector2(vector.X, vector.Y);
    }
    public static Vector3 ToNumVector3(this EngineKit.Mathematics.Vector3 vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
    }

    public static Vector4 ToNumVector4(this EngineKit.Mathematics.Vector4 vector)
    {
        return new Vector4(vector.X, vector.Y, vector.Z, vector.W);
    }
    
    public static EngineKit.Mathematics.Vector2 ToVector2(this Vector2 vector)
    {
        return new EngineKit.Mathematics.Vector2(vector.X, vector.Y);
    }
    
    public static EngineKit.Mathematics.Vector3 ToVector3(this Vector3 vector)
    {
        return new EngineKit.Mathematics.Vector3(vector.X, vector.Y, vector.Z);
    }

    public static EngineKit.Mathematics.Vector4 ToVector4(this Vector4 vector)
    {
        return new EngineKit.Mathematics.Vector4(vector.X, vector.Y, vector.Z, vector.W);
    }
}