using ImGuiNET;
using EngineKit.Mathematics;
using EngineKit;
using EngineKit.Graphics;
using EngineKit.UI;
using Num = System.Numerics;

namespace OpenSpace.Windows;

public class SceneUiWindow : UiWindow
{
    private static readonly Num.Vector2 _uv0 = new Num.Vector2(0, 1);
    private static readonly Num.Vector2 _uv1 = new Num.Vector2(1, 0);

    private readonly IApplicationContext _applicationContext;
    private readonly IUIRenderer _uiRenderer;

    private Point _sceneTextureSize;
    private IHasTextureId? _texture;
    private IHasTextureId? _osdTexture;

    private EditorOperation _editorOperation;

    public SceneUiWindow(
        IApplicationContext applicationContext,
        IUIRenderer uiRenderer)
        : base($"{MaterialDesignIcons.Gamepad}  Scene")
    {
        _applicationContext = applicationContext;
        _uiRenderer = uiRenderer;
        _editorOperation = EditorOperation.Select;
        Visible = true;
    }

    public void SetTexture(IHasTextureId? texture)
    {
        _texture = texture;
    }
    
    public void SetOsdTexture(IHasTextureId? osdTexture)
    {
        _osdTexture = osdTexture;
    }
    
    protected override void BeforeRenderInternal()
    {
	    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Num.Vector2.Zero);
    }

    protected override void AfterRenderInternal()
    {
        ImGui.PopStyleVar();
    }

    protected override ImGuiWindowFlags GetImGuiWindowFlags()
    {
        return ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    protected override void RenderInternal()
    {
        if (_texture == null)
        {
            return;
        }

        RenderToolbar();
        /*
        var offset = ImGui.GetCursorPos();
        var sceneViewSize = ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin() - offset * 0.5f;
        var sceneViewPosition = ImGui.GetWindowPos() + offset;
        */
        var sceneViewSize = ImGui.GetContentRegionAvail();

        if (!HandleWindowResize())
        {
            ImGui.End();
            ImGui.PopStyleVar();
        }

        ImGuiExtensions.ShowImage(_texture, sceneViewSize);
    }

    private void RenderToolbar()
    {
        ImGui.Indent();
        ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        var isSelected = false;
        {
            isSelected = _editorOperation == EditorOperation.Select;
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Num.Vector4(1.0f, 0.8f, 0.0f, 1.0f));
            }
            ImGui.SameLine();
            if (ImGui.Button(MaterialDesignIcons.CursorDefault))
            {
                _editorOperation = EditorOperation.Select;
            }

            if (isSelected)
            {
                ImGui.PopStyleColor();
            }
            ImGuiExtensions.Tooltip(nameof(EditorOperation.Select));
        }

        ImGui.SameLine();
        ImGui.Separator();
        ImGui.SameLine();

        {
            isSelected = _editorOperation == EditorOperation.Translate;
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Num.Vector4(1.0f, 0.8f, 0.0f, 1.0f));
            }
            ImGui.SameLine();
            if (ImGui.Button(MaterialDesignIcons.ArrowAll))
            {
                _editorOperation = EditorOperation.Translate;
            }

            if (isSelected)
            {
                ImGui.PopStyleColor();
            }
            ImGuiExtensions.Tooltip(nameof(EditorOperation.Translate));
        }

        ImGui.SameLine();
        ImGui.Separator();
        ImGui.SameLine();

        {
            isSelected = _editorOperation == EditorOperation.Rotate;
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Num.Vector4(1.0f, 0.8f, 0.0f, 1.0f));
            }
            ImGui.SameLine();
            if (ImGui.Button(MaterialDesignIcons.RotateOrbit))
            {
                _editorOperation = EditorOperation.Rotate;
            }

            if (isSelected)
            {
                ImGui.PopStyleColor();
            }
            ImGuiExtensions.Tooltip(nameof(EditorOperation.Rotate));
        }

        ImGui.SameLine();
        ImGui.Separator();
        ImGui.SameLine();

        {
            isSelected = _editorOperation == EditorOperation.Scale;
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Num.Vector4(1.0f, 0.8f, 0.0f, 1.0f));
            }
            ImGui.SameLine();
            if (ImGui.Button(MaterialDesignIcons.ArrowExpandAll))
            {
                _editorOperation = EditorOperation.Scale;
            }

            if (isSelected)
            {
                ImGui.PopStyleColor();
            }
            ImGuiExtensions.Tooltip(nameof(EditorOperation.Scale));
        }

        ImGui.PopStyleColor();
        ImGui.Unindent();
    }

    private bool HandleWindowResize()
    {
        var view = ImGui.GetContentRegionAvail();
        if (view.X != _applicationContext.FramebufferSize.X || view.Y != _applicationContext.FramebufferSize.Y)
        {
	        if (view.X == 0 || view.Y == 0)
	        {
		        return false;
	        }

            _sceneTextureSize = new Point((int)view.X, (int)view.Y);

            /*
	        _applicationContext.FramebufferSize = new Point((int)view.X, (int)view.Y);
            var framebufferSizeChanged = _applicationContext.FramebufferSizeChanged;
            framebufferSizeChanged?.Invoke();
            */

            return true;
        }
        return true;
    }
}