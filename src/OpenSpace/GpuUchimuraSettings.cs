namespace OpenSpace;

public struct GpuUchimuraSettings
{
    public float MaxDisplayBrightness; // 1.0
    public float Contrast; // 1.0
    public float LinearSectionStart; // 0.22
    public float LinearSectionLength; // 0.4

    public float Black; // 1.33
    public float Pedestal; // 0.0
    public float Gamma; // 2.2
    public bool CorrectGamma;
}