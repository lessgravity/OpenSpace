using ImGuiNET;
using OpenSpace.Engine.Graphics;
using OpenSpace.Engine.UI;
using Num = System.Numerics;

namespace OpenSpace.Windows;

public class AssetUiWindow : UiWindow
{
    private static readonly Num.Vector2 _textureSize = new Num.Vector2(320, 180);
    private static readonly Num.Vector2 _uv0 = new Num.Vector2(0, 1);
    private static readonly Num.Vector2 _uv1 = new Num.Vector2(1, 0);
    
    private readonly IMessageBus _messageBus;
    private readonly IMaterialLibrary _materialLibrary;
    private readonly IModelLibrary _modelLibrary;

    public AssetUiWindow(
        IMessageBus messageBus,
        IMaterialLibrary materialLibrary,
        IModelLibrary modelLibrary)
        : base($"{MaterialDesignIcons.FolderStar} Assets")
    {
        _messageBus = messageBus;
        _materialLibrary = materialLibrary;
        _modelLibrary = modelLibrary;

        Visible = true;
    }

    public Material? SelectedMaterial { get; set; }

    protected override void RenderInternal()
    {
        RenderModels();
        RenderMaterials();
    }

    private void RenderModels()
    {
        var importModelPressed = ImGui.Button( $"{MaterialDesignIcons.DatabaseImport} Import Model");
        if (importModelPressed)
        {

        }

        var lightsNodeExpanded = ImGui.TreeNode($"{MaterialDesignIcons.Lightbulb}  Lights");
        if (lightsNodeExpanded)
        {
            ImGui.TextUnformatted($"{MaterialDesignIcons.CarLightHigh}  Directional");
            ImGui.SameLine();
            if (ImGui.Button("Instantiate##DL"))
            {

            }

            ImGui.TextUnformatted($"{MaterialDesignIcons.Lighthouse}  Point");
            ImGui.SameLine();
            if (ImGui.Button("Instantiate##PL"))
            {

            }

            ImGui.TextUnformatted($"{MaterialDesignIcons.SpotlightBeam}  Spot");
            ImGui.SameLine();
            if (ImGui.Button("Instantiate##SL"))
            {

            }

            ImGui.TreePop();
        }

        var modelsNodeExpanded = ImGui.TreeNode($"{MaterialDesignIcons.CubeOutline}  Models");
        if (modelsNodeExpanded)
        {
            var modelNames = _modelLibrary.GetModelNames();
            foreach (var modelName in modelNames)
            {
                var model = _modelLibrary.GetModelByName(modelName);
                if (model == null)
                {
                    continue;
                }
                var modelNodeExpanded = ImGui.TreeNodeEx($"{MaterialDesignIcons.Cube}  {model.Name}");
                if (modelNodeExpanded)
                {
                    if (ImGui.Button($"{MaterialDesignIcons.PlusOne}  Instantiate##{model.GetHashCode()}"))
                    {
                        //_messageBus.PublishWait(new InstantiateModelMessage(model));
                    }

                    foreach (var modelMesh in model.ModelMeshes)
                    {
                        ImGui.TextUnformatted(modelMesh.MeshPrimitive.MeshName);
                        ImGui.SameLine();
                        if (ImGui.Button($"{MaterialDesignIcons.PlusOne}  Instantiate##{modelMesh.GetHashCode()}"))
                        {
                            //_messageBus.PublishWait(new InstantiateModelMeshMessage(modelMesh));
                        }
                    }

                    ImGui.TreePop();
                }
            }

            ImGui.TreePop();
        }
    }

    private void RenderMaterials()
    {
        if (ImGui.TreeNode($"{MaterialDesignIcons.MaterialDesign}  Materials"))
        {
            var materialNames = _materialLibrary.GetMaterialNames();
            foreach (var materialName in materialNames)
            {
                var material = _materialLibrary.GetMaterialByName(materialName);
                RenderMaterial(material);
            }

            ImGui.TreePop();
        }
    }

    private void RenderMaterial(Material material)
    {
        if (ImGui.Selectable(material.Name))
        {
            SelectedMaterial = material;
        }

        /*
        var baseColor = material.BaseColor.ToNumerics();
        var emissiveColor = material.EmissiveColor.ToNumerics();
        if (ImGui.ColorEdit4($"Base Color##{material.GetHashCode()}", ref baseColor))
        {
            material.BaseColor = baseColor.ToColor4();
        }

        if (ImGui.ColorEdit4($"Emissive Color##{material.GetHashCode()}", ref emissiveColor))
        {
            material.EmissiveColor = emissiveColor.ToColor4();
        }
        */

        if (material.BaseColorTexture != null && ImGui.TreeNode($"Base Color Texture##{material.GetHashCode()}"))
        {
            ImGui.Image((nint)material.BaseColorTexture.Id, _textureSize, _uv0, _uv1);
            ImGui.TreePop();
        }

        if (material.NormalTexture != null && ImGui.TreeNode($"Normals##{material.GetHashCode()}"))
        {
            ImGui.Image((nint)material.NormalTexture.Id, _textureSize, _uv0, _uv1);
            ImGui.TreePop();
        }

        if (material.MetalnessRoughnessTexture != null && ImGui.TreeNode($"Metalness & Roughness##{material.GetHashCode()}"))
        {
            var metallicFactor = material.MetallicFactor;
            var roughnessFactor = material.RoughnessFactor;
            if (ImGui.SliderFloat("Metallic Factor", ref metallicFactor, 0, 1))
            {
                material.MetallicFactor = metallicFactor;
            }

            if (ImGui.SliderFloat("Roughness Factor", ref roughnessFactor, 0, 1))
            {
                material.RoughnessFactor = roughnessFactor;
            }

            ImGui.Image((nint)material.MetalnessRoughnessTexture.Id, _textureSize, _uv0, _uv1);
            ImGui.TreePop();
        }

        if (material.SpecularTexture != null && ImGui.TreeNode($"Specular##{material.GetHashCode()}"))
        {
            ImGui.Image((nint)material.SpecularTexture.Id, _textureSize, _uv0, _uv1);
            ImGui.TreePop();
        }
    }
}