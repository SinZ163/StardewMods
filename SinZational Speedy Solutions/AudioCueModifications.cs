﻿using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;

namespace SinZ.SpeedySolutions
{
    internal class AudioCueModifications
    {
        private static Harmony harmony;
        private static IModHelper helper;
        private static IMonitor monitor;

        // Deliberately *not* respecting AssetInvalidated method as needs the *old* copy during propagation phase
        private static Dictionary<string, AudioCueData>? CurrentAudioCache;
        private static Dictionary<string, AudioCueData>? PreviousAudioCache;

        internal static void Init(Harmony harmony, IModHelper helper, IMonitor monitor)
        {
            AudioCueModifications.harmony = harmony;
            AudioCueModifications.helper = helper;
            AudioCueModifications.monitor = monitor;
            helper.Events.Content.AssetReady += Content_AssetReady;

            harmony.Patch(AccessTools.Method(typeof(StardewValley.Audio.AudioCueModificationManager), nameof(StardewValley.Audio.AudioCueModificationManager.ApplyCueModification)), prefix: new HarmonyMethod(typeof(AudioCueModifications).GetMethod(nameof(AudioCueModificationManager__ApplyCueModification__Prefix))));
        }

        private static void Content_AssetReady(object? sender, StardewModdingAPI.Events.AssetReadyEventArgs e)
        {
            if (e.NameWithoutLocale.BaseName == "Data/AudioChanges")
            {
                var newAudio = DataLoader.AudioChanges(Game1.content);
                PreviousAudioCache = CurrentAudioCache;
                CurrentAudioCache = newAudio;
            }
        }
        public static bool AudioCueModificationManager__ApplyCueModification__Prefix(string key)
        {
            if (!ModEntry.Config.EnableAudioCueModificationCache) return true;

            if (PreviousAudioCache == null) return true;
            if (CurrentAudioCache == null) return true;
            monitor.Log("Evaluating cue modification for " + key);
            if (!PreviousAudioCache.TryGetValue(key, out var oldValue))
            {
                return true;
            }
            if (!CurrentAudioCache.TryGetValue(key, out var newValue))
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
}
