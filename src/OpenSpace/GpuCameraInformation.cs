using lessGravity.Mathematics;

namespace OpenSpace;

public struct GpuCameraInformation
{
    public Matrix ProjectionMatrix;
    
    public Matrix ViewMatrix;

    public Vector4 Viewport;

    public Vector4 CameraPositionAndFieldOfView;

    public Vector4 CameraDirectionAndAspectRatio;
}