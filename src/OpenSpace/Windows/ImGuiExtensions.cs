using ImGuiNET;
using EngineKit.Graphics;
using Num = System.Numerics;

namespace OpenSpace.Windows;

public static class ImGuiExtensions
{
    private static readonly Num.Vector2 _uv0 = new Num.Vector2(0, 1);
    private static readonly Num.Vector2 _uv1 = new Num.Vector2(1, 0);
    
    public static void Tooltip(string text)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Num.Vector2(4, 4));

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(text);
            ImGui.EndTooltip();
        }

        ImGui.PopStyleVar();
    }
    
    public static void ShowImage(IHasTextureId texture, Num.Vector2 textureSize)
    {
        ImGui.Image((nint)texture.Id, new Num.Vector2(textureSize.X, textureSize.Y), _uv0, _uv1);
    }
}