using ImGuiNET;

namespace OpenSpace.Windows;

public class UiWindow
{
    protected UiWindow(string caption)
    {
        Caption = caption;
    }

    public string Caption { get; set; }

    public bool Visible { get; set; }

    public void Render()
    {
        BeforeRenderInternal();

        if (Visible && ImGui.Begin(Caption, GetImGuiWindowFlags()))
        {
            RenderInternal();
            ImGui.End();
        }

        AfterRenderInternal();
    }

    protected virtual void AfterRenderInternal()
    {
    }

    protected virtual void BeforeRenderInternal()
    {
    }

    protected virtual ImGuiWindowFlags GetImGuiWindowFlags()
    {
        return ImGuiWindowFlags.None;
    }

    protected virtual void RenderInternal()
    {
    }
}