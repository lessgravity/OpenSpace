using System;
using System.Collections.Generic;
using System.Linq;
using lessGravity.Mathematics;
using Serilog;

namespace OpenSpace.Engine.Graphics;

internal sealed class MaterialLibrary : IMaterialLibrary
{
    private readonly ISamplerLibrary _samplerLibrary;
    private readonly ILogger _logger;
    private readonly IDictionary<string, Material> _materials;
    private readonly Random _random;

    public MaterialLibrary(ILogger logger, ISamplerLibrary samplerLibrary)
    {
        _samplerLibrary = samplerLibrary;
        _logger = logger.ForContext<MaterialLibrary>();
        _materials = new Dictionary<string, Material>(256);
        _random = new Random();
        CreateSystemMaterials();
    }

    public Material GetRandomMaterial()
    {
        return _materials.Values.ElementAt(_random.Next(0, _materials.Values.Count));
    }

    public bool Exists(string materialName)
    {
        return _materials.ContainsKey(materialName);
    }

    public void AddMaterial(Material material)
    {
        if (string.IsNullOrEmpty(material.Name))
        {
            return;
        }

        if (_materials.ContainsKey(material.Name))
        {
            _logger.Debug("{Category}: Material {MaterialName} exists already", nameof(MaterialLibrary), material.Name);
            return;
        }

        _materials.Add(material.Name, material);

        _samplerLibrary.AddSamplerIfNotExists(material.BaseColorTextureSamplerInformation);
        _samplerLibrary.AddSamplerIfNotExists(material.NormalTextureSamplerInformation);
        _samplerLibrary.AddSamplerIfNotExists(material.MetalnessRoughnessTextureSamplerInformation);
        _samplerLibrary.AddSamplerIfNotExists(material.SpecularTextureSamplerInformation);
        _samplerLibrary.AddSamplerIfNotExists(material.OcclusionTextureSamplerInformation);
        _samplerLibrary.AddSamplerIfNotExists(material.EmissiveTextureSamplerInformation);
    }

    public void RemoveMaterial(string name)
    {
        _materials.Remove(name);
    }

    public IList<string> GetMaterialNames()
    {
        return _materials.Keys.ToList();
    }

    public Material GetMaterialByName(string materialName)
    {
        return _materials.TryGetValue(materialName, out var material)
            ? material
            : _materials["M_NotFound"];
    }

    private void CreateSystemMaterials()
    {
        var notFoundMaterial = new Material("M_NotFound")
        {
            BaseColor = Color.Firebrick,
            BaseColorTextureDataName = "NotFound.BaseColor",
            BaseColorTextureFilePath = "Data/Default/T_Red_D.png",
            EmissiveColor = Color.Firebrick
        };

        _materials.Add(notFoundMaterial.Name, notFoundMaterial);
    }
}