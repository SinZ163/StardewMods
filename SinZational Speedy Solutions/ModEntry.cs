using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley;
using xTile.Dimensions;
using System.Reflection.Emit;
using StardewValley.GameData;

namespace SinZ.SpeedySolutions;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        var harmony = new Harmony(ModManifest.UniqueID);
        Patches.Init(harmony, helper, Monitor);
        helper.Events.Content.AssetReady += Content_AssetReady;
    }

    private void Content_AssetReady(object? sender, StardewModdingAPI.Events.AssetReadyEventArgs e)
    {
        if (e.NameWithoutLocale.BaseName == "Data/AudioChanges")
        {
            var newAudio = DataLoader.AudioChanges(Game1.content);
            AssetCache.PreviousAudioCache = AssetCache.CurrentAudioCache;
            AssetCache.CurrentAudioCache = newAudio;
        }
    }
}
public static class AssetCache
{
    // Deliberately *not* respecting AssetInvalidated method as needs the *old* copy during propagation phase
    public static Dictionary<string, AudioCueData>? CurrentAudioCache;
    public static Dictionary<string, AudioCueData>? PreviousAudioCache;
}
public static class Patches
{
    public static IMonitor monitor;
    public static void Init(Harmony harmony, IModHelper helper, IMonitor monitor)
    {
        Patches.monitor = monitor;
        harmony.Patch(AccessTools.Method(typeof(StardewValley.Audio.AudioCueModificationManager), nameof(StardewValley.Audio.AudioCueModificationManager.ApplyCueModification)), prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(AudioCueModificationManager__ApplycueModification__Prefix))));

    }

    public static bool AudioCueModificationManager__ApplycueModification__Prefix(string key)
    {
        if (AssetCache.PreviousAudioCache == null) return true;
        if (AssetCache.CurrentAudioCache == null) return true;
        monitor.Log("Evaluating cue modification for " + key);
        if (!AssetCache.PreviousAudioCache.TryGetValue(key, out var oldValue))
        {
            return true;
        }
        if (!AssetCache.CurrentAudioCache.TryGetValue(key, out var newValue))
        {
            return true;
        }
        if (!oldValue.FilePaths.SequenceEqual(newValue.FilePaths))
        {
            return true;
        }
        if (oldValue.Category != newValue.Category
            || oldValue.StreamedVorbis != newValue.StreamedVorbis
            || oldValue.Looped != newValue.Looped
            || oldValue.UseReverb != newValue.UseReverb)
        {
            return true;
        }
        // Everything is the same, don't need to reapply the edit
        monitor.Log("Skipped reapplying cue modification for " + key);
        return false;
    }
}