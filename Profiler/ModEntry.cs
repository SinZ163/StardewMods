using StardewModdingAPI;
using StardewModdingAPI.Events;
using System;
using System.Diagnostics;
using System.Text.Json;

namespace Profiler
{
    public class ModEntry : Mod
    {
        internal ModConfig Config { get; private set; }
        public Stopwatch timer { get; private set; }

        public ProfilerAPI ProfilerAPI { get; private set; }

        public GcEventListener GcEventListener { get; private set; }

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            this.timer = new Stopwatch();

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;


            var harmony = new HarmonyLib.Harmony(this.ModManifest.UniqueID);
            ProfilerAPI = new ProfilerAPI(Config, harmony, timer, Monitor, helper);
            PublicPatches.Initialize(ProfilerAPI, harmony, Monitor);
            TimingMetrics.Initialize(ProfilerAPI, harmony, Monitor);
            ManagedEventPatches.Initialize(Monitor, this.Config, ProfilerAPI, harmony);

            this.timer.Restart();
            _ = new PlayerLogger(helper, Monitor, this.timer);
            ProfilerAPI.Write(new EventMetadata(
                ModId: this.ModManifest.UniqueID,
                EventType: String.Join(separator: '/', this.ModManifest.UniqueID, "Init"),
                Details: DateTimeOffset.Now.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                InnerDetails: [
                    new EventMetadata(
                        ModId: this.ModManifest.UniqueID,
                        EventType: String.Join(separator: '/', this.ModManifest.UniqueID, "Config"),
                        Details: JsonSerializer.Serialize<ModConfig>(this.Config),
                        InnerDetails: []
                    )
                ]
            ));
            GcEventListener = new GcEventListener(ProfilerAPI, Monitor);
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
                // TODO: Do this better
                if (cpData.Format != "1.0")
                {
                    Monitor.Log($"Content Pack {cp.Manifest.Name}: Unknown Format {cpData.Format}", LogLevel.Error);
                    continue;
                }
                
                foreach(var entry in cpData.Entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.ConditionalMod))
                    {
                        if (!Helper.ModRegistry.IsLoaded(entry.ConditionalMod))
                        {
                            Monitor.Log($"Content Pack {cp.Manifest.Name}: Patch for {entry.TargetType}.{entry.TargetMethod} not applied as {entry.ConditionalMod} is not loaded.", LogLevel.Info);
                            continue;
                        }
                    }
                    switch (entry.Type)
                    {
                        case "Duration":
                            {
                                var methodBase = ProfilerAPI.AddGenericDurationPatch(entry.TargetType, entry.TargetMethod, entry.TargetMethodGenericArguments, entry.Details?.Type);
                                if (entry.Details != null && methodBase != null)
                                {
                                    PublicPatches.AddDetailsEntry(methodBase, entry.Details);
                                }
                            }
                            break;
                        case "Trace":
                            {
                                var methodBase = ProfilerAPI.AddGenericTracePatch(entry.TargetType, entry.TargetMethod, entry.Details?.Type);
                                if (entry.Details != null && methodBase != null)
                                {
                                    PublicPatches.AddDetailsEntry(methodBase, entry.Details);
                                }
                            }
                            break;
                        default:
                            Monitor.Log($"Content Pack {cp.Manifest.Name}: Unknown ProfilerType {entry.Type}", LogLevel.Warn);
                            break;
                    }
                }
            }

        }

        public override object GetApi()
        {
            return ProfilerAPI;
        }
    }
}
