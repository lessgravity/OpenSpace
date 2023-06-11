using EngineKit.Mathematics;

namespace OpenSpace.Engine.Graphics;

public sealed class SwapchainDescriptorBuilder
{
    private SwapchainDescriptor _swapchainDescriptor;

    public SwapchainDescriptorBuilder()
    {
        _swapchainDescriptor = new SwapchainDescriptor();
        _swapchainDescriptor.ClearColor = false;
        _swapchainDescriptor.ClearDepth = false;
        _swapchainDescriptor.ClearStencil = false;
        _swapchainDescriptor.ClearColorValue.ColorFloat = new float[4] { 0.0f, 0.0f, 0.0f, 0.0f };
        _swapchainDescriptor.ClearColorValue.ColorSignedInteger = new int[4] { 0, 0, 0, 0 };
        _swapchainDescriptor.ClearColorValue.ColorUnsignedInteger = new uint[4] { 0, 0, 0, 0 };
    }

    public SwapchainDescriptorBuilder EnableSrgb()
    {
        _swapchainDescriptor.EnableSrgb = true;
        return this;
    }

    public SwapchainDescriptorBuilder WithViewport(int width, int height)
    {
        _swapchainDescriptor.Viewport = new Viewport(0, 0, width, height);
        return this;
    }

    public SwapchainDescriptorBuilder WithScissorRectangle(int left, int top, int width, int height)
    {
        _swapchainDescriptor.ScissorRect = new Viewport(left, top, width, height);
        return this;
    }

    public SwapchainDescriptorBuilder ClearColor(Color4 clearValue)
    {
        _swapchainDescriptor.ClearColor = true;
        _swapchainDescriptor.ClearColorValue = new ClearColorValue();
        _swapchainDescriptor.ClearColorValue.ColorFloat = new float[4];
        _swapchainDescriptor.ClearColorValue.ColorFloat[0] = clearValue.Red;
        _swapchainDescriptor.ClearColorValue.ColorFloat[1] = clearValue.Green;
        _swapchainDescriptor.ClearColorValue.ColorFloat[2] = clearValue.Blue;
        _swapchainDescriptor.ClearColorValue.ColorFloat[3] = clearValue.Alpha;
        return this;
    }

    public SwapchainDescriptorBuilder ClearDepth(float clearValue = 1.0f)
    {
        _swapchainDescriptor.ClearDepth = true;
        _swapchainDescriptor.ClearDepthValue = clearValue;
        return this;
    }

    public SwapchainDescriptorBuilder ClearStencil(int clearValue = 0)
    {
        _swapchainDescriptor.ClearStencil = true;
        _swapchainDescriptor.ClearStencilValue = clearValue;
        return this;
    }

    public SwapchainDescriptor Build()
    {
        return _swapchainDescriptor;
    }
}