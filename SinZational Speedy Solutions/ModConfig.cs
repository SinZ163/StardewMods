namespace SinZ.SpeedySolutions;

internal class ModConfig
{
    public bool EnableAudioCueModificationCache { get; set; } = true;
    public bool EnableSlowGameLocationLoaderBypass { get; set; } = true;

    public bool EnableTBinSave { get; set; } = true;
    public bool EnableTBinLoad { get; set; } = true;
    public bool EnableImageCache { get; set; } = false;
    public int MapInMemoryThreshold { get; set; } = 0;
}
