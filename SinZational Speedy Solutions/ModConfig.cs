namespace SinZ.SpeedySolutions;

internal class ModConfig
{
    public bool EnableAudioCueModificationCache { get; set; } = true;
    public bool EnableAudioCueModificationParallelization { get; set; } = false;
    public bool EnableSlowGameLocationLoaderBypass { get; set; } = true;

    public bool EnableTBinSave { get; set; } = true;
    public bool EnableTBinLoad { get; set; } = true;
    public bool EnableImageCache { get; set; } = true;
    public int MapInMemoryThreshold { get; set; } = 10_000;
}
