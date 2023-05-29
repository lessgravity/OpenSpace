using System;
using System.Collections.Generic;
using System.Diagnostics;
using lessGravity.Mathematics;
using Serilog;
using Vector3 = System.Numerics.Vector3;

namespace OpenSpace.Engine.Graphics;

public record Material(string Name)
{
    private float _metallicFactor;
    private float _roughnessFactor;
    private Vector3 _specularFactor;
    private float _glossinessFactor;
    private float _occlusionFactor = 1.0f;
    private Color4 _emissiveColor;
    private Color4 _baseColor;
    private Color4 _specularColor;
    private string? _baseColorTextureFilePath;
    private string? _normalTextureFilePath;
    private string? _specularTextureFilePath;
    private string? _metalnessRoughnessTextureFilePath;
    private string? _occlusionTextureFilePath;
    private string? _emissiveTextureFilePath;

    public SamplerInformation? BaseColorTextureSamplerInformation;
    public SamplerInformation? NormalTextureSamplerInformation;
    public SamplerInformation? SpecularTextureSamplerInformation;
    public SamplerInformation? MetalnessRoughnessTextureSamplerInformation;
    public SamplerInformation? OcclusionTextureSamplerInformation;
    public SamplerInformation? EmissiveTextureSamplerInformation;
    
    private bool _isDirty;
    public string Name { get; set; } = Name;

    public bool TexturesLoaded { get; private set; }

    public bool IsDirty => _isDirty;

    public float OcclusionFactor
    {
        get => _occlusionFactor;
        set
        {
            if (MathF.Abs(_occlusionFactor - value) > 0.0001f)
            {
                _occlusionFactor = value;
                _isDirty = true;
            }
        }
    }

    public float GlossinessFactor
    {
        get => _glossinessFactor;
        set
        {
            if (MathF.Abs(_glossinessFactor - value) > 0.0001f)
            {
                _glossinessFactor = value;
                _isDirty = true;
            }
        }
    }

    public float MetallicFactor
    {
        get => _metallicFactor;
        set
        {
            if (MathF.Abs(_metallicFactor - value) > 0.0001f)
            {
                _metallicFactor = value;
                _isDirty = true;
            }
        }
    }

    public float RoughnessFactor
    {
        get => _roughnessFactor;
        set
        {
            if (MathF.Abs(_roughnessFactor - value) > 0.0001f)
            {
                _roughnessFactor = value;
                _isDirty = true;
            }
        }
    }

    public Color4 BaseColor
    {
        get => _baseColor;
        set
        {
            if (_baseColor != value)
            {
                _baseColor = value;
                _isDirty = true;
            }
        }
    }

    public Color4 EmissiveColor
    {
        get => _emissiveColor;
        set
        {
            if (_emissiveColor != value)
            {
                _emissiveColor = value;
                _isDirty = true;
            }
        }
    }

    public Color4 SpecularColor
    {
        get => _specularColor;
        set
        {
            if (_specularColor != value)
            {
                _specularColor = value;
                _isDirty = true;
            }
        }
    }

    public Vector3 SpecularFactor
    {
        get => _specularFactor;
        set
        {
            _specularFactor = value;
            _isDirty = true;
        }
    }
    
    public string? EmissiveTextureDataName { get; set; }

    public string? EmissiveTextureFilePath
    {
        get => _emissiveTextureFilePath;
        set
        {
            if (_emissiveTextureFilePath != value)
            {
                _emissiveTextureFilePath = value;
                _isDirty = true;
            }
        }
    }
    
    public ReadOnlyMemory<byte>? EmissiveEmbeddedImageData { get; set; }
    
    public ITexture? EmissiveTexture;

    public string? BaseColorTextureDataName { get; set; }

    public string? BaseColorTextureFilePath
    {
        get => _baseColorTextureFilePath;
        set
        {
            if (_baseColorTextureFilePath != value)
            {
                _baseColorTextureFilePath = value;
                _isDirty = true;
            }
        }
    }

    public ReadOnlyMemory<byte>? BaseColorEmbeddedImageData { get; set; }

    public ITexture? BaseColorTexture;

    public string? NormalTextureDataName { get; set; }

    public string? NormalTextureFilePath
    {
        get => _normalTextureFilePath;
        set
        {
            if (_normalTextureFilePath != value)
            {
                _normalTextureFilePath = value;
                _isDirty = true;
            }
        }
    }

    public ReadOnlyMemory<byte>? NormalEmbeddedImageData { get; set; }

    public ITexture? NormalTexture;
    
    public string? SpecularTextureDataName { get; set; }

    public string? SpecularTextureFilePath
    {
        get => _specularTextureFilePath;
        set
        {
            if (_specularTextureFilePath != value)
            {
                _specularTextureFilePath = value;
                _isDirty = true;
            }
        }
    }

    public ReadOnlyMemory<byte>? SpecularEmbeddedImageData { get; set; }

    public ITexture? SpecularTexture;

    public string? MetalnessRoughnessTextureDataName { get; set; }

    public string? MetalnessRoughnessTextureFilePath
    {
        get => _metalnessRoughnessTextureFilePath;
        set
        {
            _metalnessRoughnessTextureFilePath = value;
            _isDirty = true;
        }
    }

    public ReadOnlyMemory<byte>? MetalnessRoughnessEmbeddedImageData { get; set; }

    public ITexture? MetalnessRoughnessTexture;
    
    public string? OcclusionTextureDataName { get; set; }

    public string? OcclusionTextureFilePath
    {
        get => _occlusionTextureFilePath;
        set
        {
            _occlusionTextureFilePath = value;
            _isDirty = true;
        }
    }
    
    public ReadOnlyMemory<byte>? OcclusionEmbeddedImageData { get; set; }
    public ITexture? OcclusionTexture;

    public void Dispose()
    {
        BaseColorTexture?.Dispose();
        NormalTexture?.Dispose();
        SpecularTexture?.Dispose();
        MetalnessRoughnessTexture?.Dispose();
        OcclusionTexture?.Dispose();
        EmissiveTexture?.Dispose();
    }

    public bool LoadTextures(ILogger logger, IGraphicsContext graphicsContext, ISamplerLibrary samplerLibrary, IDictionary<string, ITexture> textures, bool makeResident)
    {
        //TODO(deccer) TextureLoader should be called and pass in material, to set its Texture objects perhaps
        //TODO(deccer) this needs to be inverted, ie someone else needs to load the stuff per material, not the material itself
        if (!string.IsNullOrEmpty(BaseColorTextureDataName) && !textures.TryGetValue(BaseColorTextureDataName, out BaseColorTexture))
        {
            var sw = Stopwatch.StartNew();
            BaseColorTexture = BaseColorEmbeddedImageData.HasValue
                ? graphicsContext.CreateTextureFromMemory(BaseColorEmbeddedImageData.Value.Span, Format.R8G8B8A8Srgb,
                    BaseColorTextureDataName, generateMipmaps: true, flipVertical: false, flipHorizontal: false)
                : string.IsNullOrEmpty(BaseColorTextureFilePath)
                    ? null
                    : graphicsContext.CreateTextureFromFile(BaseColorTextureFilePath, Format.R8G8B8A8Srgb, true, false, false);
            sw.Stop();
            if (makeResident && BaseColorTexture != null && BaseColorTextureSamplerInformation.HasValue)
            {
                BaseColorTexture.MakeResident(samplerLibrary.GetSampler(BaseColorTextureSamplerInformation.Value));
                textures.Add(BaseColorTextureDataName, BaseColorTexture);
                logger.Debug("App: Loading baseColor texture {TextureName} took {LoadingTime}ms", BaseColorTextureDataName, sw.ElapsedMilliseconds);
            }
        }
        if (!string.IsNullOrEmpty(NormalTextureDataName) && !textures.TryGetValue(NormalTextureDataName, out NormalTexture))
        {
            var sw = Stopwatch.StartNew();
            NormalTexture = NormalEmbeddedImageData.HasValue
                ? graphicsContext.CreateTextureFromMemory(NormalEmbeddedImageData.Value.Span, Format.R8G8B8A8UNorm,
                    NormalTextureDataName, generateMipmaps: true, flipVertical: false, flipHorizontal: false)
                : string.IsNullOrEmpty(NormalTextureFilePath)
                    ? null
                    : graphicsContext.CreateTextureFromFile(NormalTextureFilePath, Format.R8G8B8A8UNorm, true, false, false);
            sw.Stop();
            if (makeResident && NormalTexture != null && NormalTextureSamplerInformation.HasValue)
            {
                NormalTexture.MakeResident(samplerLibrary.GetSampler(NormalTextureSamplerInformation.Value));
                textures.Add(NormalTextureDataName, NormalTexture);
                logger.Debug("App: Loading normal texture {TextureName} took {LoadingTime}ms", NormalTextureDataName, sw.ElapsedMilliseconds);
            }
        }

        if (!string.IsNullOrEmpty(MetalnessRoughnessTextureDataName) && !textures.TryGetValue(MetalnessRoughnessTextureDataName, out MetalnessRoughnessTexture))
        {
            var sw = Stopwatch.StartNew();
            MetalnessRoughnessTexture = MetalnessRoughnessEmbeddedImageData.HasValue
                ? graphicsContext.CreateTextureFromMemory(MetalnessRoughnessEmbeddedImageData.Value.Span, Format.R8G8B8A8UNorm,
                    MetalnessRoughnessTextureDataName, generateMipmaps: true, flipVertical: false, flipHorizontal: false)
                : string.IsNullOrEmpty(MetalnessRoughnessTextureFilePath)
                    ? null
                    : graphicsContext.CreateTextureFromFile(MetalnessRoughnessTextureFilePath, Format.R8G8B8A8UNorm, true, false, false);
            sw.Stop();
            if (makeResident && MetalnessRoughnessTexture != null && MetalnessRoughnessTextureSamplerInformation.HasValue)
            {
                MetalnessRoughnessTexture.MakeResident(samplerLibrary.GetSampler(MetalnessRoughnessTextureSamplerInformation.Value));
                textures.Add(MetalnessRoughnessTextureDataName, MetalnessRoughnessTexture);
                logger.Debug("App: Loading metalnessRoughness texture {TextureName} took {LoadingTime}ms", MetalnessRoughnessTextureDataName, sw.ElapsedMilliseconds);
            }
        }

        if (!string.IsNullOrEmpty(SpecularTextureDataName) && !textures.TryGetValue(SpecularTextureDataName, out SpecularTexture))
        {
            var sw = Stopwatch.StartNew();
            SpecularTexture = SpecularEmbeddedImageData.HasValue
                ? graphicsContext.CreateTextureFromMemory(SpecularEmbeddedImageData.Value.Span, Format.R8G8B8A8UNorm,
                    SpecularTextureDataName, generateMipmaps: true, flipVertical: false, flipHorizontal: false)
                : string.IsNullOrEmpty(SpecularTextureFilePath)
                    ? null
                    : graphicsContext.CreateTextureFromFile(SpecularTextureFilePath, Format.R8G8B8A8UNorm, true, false, false);
            sw.Stop();
            if (makeResident && SpecularTexture != null && SpecularTextureSamplerInformation.HasValue)
            {
                SpecularTexture.MakeResident(samplerLibrary.GetSampler(SpecularTextureSamplerInformation.Value));
                textures.Add(SpecularTextureDataName, SpecularTexture);
                logger.Debug("App: Loading specular texture {TextureName} took {LoadingTime}ms", SpecularTextureDataName, sw.ElapsedMilliseconds);
            }
        }
        
        if (!string.IsNullOrEmpty(OcclusionTextureDataName) && !textures.TryGetValue(OcclusionTextureDataName, out OcclusionTexture))
        {
            var sw = Stopwatch.StartNew();
            OcclusionTexture = OcclusionEmbeddedImageData.HasValue
                ? graphicsContext.CreateTextureFromMemory(OcclusionEmbeddedImageData.Value.Span, Format.R8G8B8A8UNorm,
                    OcclusionTextureDataName, generateMipmaps: true, flipVertical: false, flipHorizontal: false)
                : string.IsNullOrEmpty(OcclusionTextureFilePath)
                    ? null
                    : graphicsContext.CreateTextureFromFile(OcclusionTextureFilePath, Format.R8G8B8A8UNorm, true, false, false);
            sw.Stop();
            if (makeResident && OcclusionTexture != null && OcclusionTextureSamplerInformation.HasValue)
            {
                OcclusionTexture.MakeResident(samplerLibrary.GetSampler(OcclusionTextureSamplerInformation.Value));
                textures.Add(OcclusionTextureDataName, OcclusionTexture);
                logger.Debug("App: Loading occlusion texture {TextureName} took {LoadingTime}ms", OcclusionTextureDataName, sw.ElapsedMilliseconds);
            }
        }
        
        if (!string.IsNullOrEmpty(EmissiveTextureDataName) && !textures.TryGetValue(EmissiveTextureDataName, out EmissiveTexture))
        {
            var sw = Stopwatch.StartNew();
            EmissiveTexture = EmissiveEmbeddedImageData.HasValue
                ? graphicsContext.CreateTextureFromMemory(EmissiveEmbeddedImageData.Value.Span, Format.R8G8B8A8Srgb,
                    EmissiveTextureDataName, generateMipmaps: true, flipVertical: false, flipHorizontal: false)
                : string.IsNullOrEmpty(EmissiveTextureFilePath)
                    ? null
                    : graphicsContext.CreateTextureFromFile(EmissiveTextureFilePath, Format.R8G8B8A8Srgb, true, false, false);
            sw.Stop();
            if (makeResident && EmissiveTexture != null && EmissiveTextureSamplerInformation.HasValue)
            {
                EmissiveTexture.MakeResident(samplerLibrary.GetSampler(EmissiveTextureSamplerInformation.Value));
                textures.Add(EmissiveTextureDataName, EmissiveTexture);
                logger.Debug("App: Loading emissive texture {TextureName} took {LoadingTime}ms", EmissiveTextureDataName, sw.ElapsedMilliseconds);
            }
        }

        TexturesLoaded = true;

        return TexturesLoaded;
    }
}