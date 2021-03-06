using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Profiler
{
    /*
    public record ProfilerContentPack(string Format, ProfilerEntry[] Entries);
    public record ProfilerEntry(string Type, string TargetType, string TargetMethod, string ConditionalMod, DetailsEntry Details);
    public record DetailsEntry(string Type, string Name); */


    public class ModEntry : Mod
    {
        internal ModConfig Config { get; private set; }
        public Stopwatch timer { get; private set; }

        internal ProfilerLogger Logger { get; private set; }

        internal ProfilerAPI ProfilerAPI { get; private set; }

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            this.timer = new Stopwatch();
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoadedFast;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStartedFast;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunchedFast;
            helper.Events.Specialized.LoadStageChanged += Specialized_LoadStageChangedFast;
            helper.Events.Player.Warped += Player_WarpedFast;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoadedSlow;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStartedSlow;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunchedSlow;
            helper.Events.Specialized.LoadStageChanged += Specialized_LoadStageChangedSlow;
            helper.Events.Player.Warped += Player_WarpedSlow;

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;

            // TODO: Add logic to do the MP detection
            var path = Path.Combine(Constants.LogDir, "profile-log.log");
            Logger = new ProfilerLogger(path);

            var harmony = new HarmonyLib.Harmony(this.ModManifest.UniqueID);
            ProfilerAPI = new ProfilerAPI(Logger, Config, harmony, timer, Monitor);
            PublicPatches.Initialize(ProfilerAPI, harmony);
            ManagedEventPatches.Initialize(Monitor, this.Config, ProfilerAPI, harmony);
            this.timer.Restart();
            ProfilerAPI.Write(new EventMetadata(this.ModManifest.UniqueID, String.Join(separator: '/', this.ModManifest.UniqueID, "Init"), DateTimeOffset.Now.ToString("o", System.Globalization.CultureInfo.InvariantCulture), new()));
        }

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var contentPacks = Helper.ContentPacks.GetOwned();
            foreach (var cp in contentPacks)
            {
                if (!cp.HasFile("content.json"))
                {
                    Monitor.Log($"Content Pack {cp.Manifest.Name}: Missing a content.json", LogLevel.Error);
                    continue;
                }
                var cpData = cp.ReadJsonFile<ProfilerContentPack>("content.json");
                if (cpData is null)
                {
                    Monitor.Log($"Content Pack {cp.Manifest.Name}: Unreadable content.json", LogLevel.Error);
                    continue;
                }
                if (cpData.Format != "1.0")
                {
                    Monitor.Log($"Content Pack {cp.Manifest.Name}: Unknown Format {cpData.Format}", LogLevel.Error);
                    continue;
                }
                
                foreach(var entry in cpData.Entries)
                {
                    if (entry.Type != "Duration")
                    {
                        Monitor.Log($"Content Pack {cp.Manifest.Name}: Unknown ProfilerType {entry.Type}", LogLevel.Warn);
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(entry.ConditionalMod))
                    {
                        if (!Helper.ModRegistry.IsLoaded(entry.ConditionalMod))
                        {
                            Monitor.Log($"Content Pack {cp.Manifest.Name}: Patch for {entry.TargetType}.{entry.TargetMethod} not applied as {entry.ConditionalMod} is not loaded.", LogLevel.Info);
                            continue;
                        }
                    }
                    var methodBase = ProfilerAPI.AddGenericDurationPatch(entry.TargetType, entry.TargetMethod);
                    if (entry.Details != null && methodBase != null)
                    {
                        PublicPatches.AddDetailsEntry(methodBase, entry.Details);
                    }
                }
            }
        }

        public override object GetApi()
        {
            return ProfilerAPI;
        }

        [EventPriority((EventPriority)Int32.MaxValue)]
        private void Specialized_LoadStageChangedFast(object sender, LoadStageChangedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] LoadStageChanged {e.OldStage} -> {e.NewStage}", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MaxValue)]
        private void GameLoop_GameLaunchedFast(object sender, GameLaunchedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] Game Launched", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MaxValue)]
        private void GameLoop_DayStartedFast(object sender, DayStartedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] Day Started", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MaxValue)]
        private void GameLoop_SaveLoadedFast(object sender, SaveLoadedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] Save Loaded", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MaxValue)]
        private void Player_WarpedFast(object sender, WarpedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] Warped {e.OldLocation} -> {e.NewLocation} ({Game1.timeOfDay:D4})", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MinValue)]
        private void Specialized_LoadStageChangedSlow(object sender, LoadStageChangedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] LoadStageChanged {e.OldStage} -> {e.NewStage}", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MinValue)]
        private void GameLoop_GameLaunchedSlow(object sender, GameLaunchedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] Game Launched", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MinValue)]
        private void GameLoop_DayStartedSlow(object sender, DayStartedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] Day Started", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MinValue)]
        private void GameLoop_SaveLoadedSlow(object sender, SaveLoadedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] Save Loaded", LogLevel.Info);
        }

        [EventPriority((EventPriority)Int32.MinValue)]
        private void Player_WarpedSlow(object sender, WarpedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] Warped {e.OldLocation} -> {e.NewLocation} ({Game1.timeOfDay:D4})", LogLevel.Info);
        }
    }
}
