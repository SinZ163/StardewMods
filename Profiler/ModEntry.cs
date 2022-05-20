using StardewModdingAPI;
using StardewModdingAPI.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Profiler
{
    public class ModEntry : Mod
    {
        public Stopwatch timer { get; private set; }

        public override void Entry(IModHelper helper)
        {
            this.timer = new Stopwatch();
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoadedFast;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStartedFast;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunchedFast;
            helper.Events.Specialized.LoadStageChanged += Specialized_LoadStageChangedFast;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoadedSlow;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStartedSlow;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunchedSlow;
            helper.Events.Specialized.LoadStageChanged += Specialized_LoadStageChangedSlow;
            this.timer.Restart();
        }

        [EventPriority(EventPriority.High + 1337)]
        private void Specialized_LoadStageChangedFast(object sender, StardewModdingAPI.Events.LoadStageChangedEventArgs e)
        {
            Monitor.Log($"[{timer.ElapsedMilliseconds:N6}][Fast] LoadStageChanged {e.OldStage} -> {e.NewStage}", LogLevel.Info);
        }

        [EventPriority(EventPriority.High + 1337)]
        private void GameLoop_GameLaunchedFast(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            Monitor.Log($"[{timer.ElapsedMilliseconds:N6}][Fast] Game Launched", LogLevel.Info);
        }

        [EventPriority(EventPriority.High + 1337)]
        private void GameLoop_DayStartedFast(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            Monitor.Log($"[{timer.ElapsedMilliseconds:N6}][Fast] Day Started", LogLevel.Info);
        }

        [EventPriority(EventPriority.High + 1337)]
        private void GameLoop_SaveLoadedFast(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            Monitor.Log($"[{timer.ElapsedMilliseconds:N6}][Fast] Save Loaded", LogLevel.Info);
        }

        [EventPriority(EventPriority.Low - 1337)]
        private void Specialized_LoadStageChangedSlow(object sender, StardewModdingAPI.Events.LoadStageChangedEventArgs e)
        {
            Monitor.Log($"[{timer.ElapsedMilliseconds:N6}][Slow] LoadStageChanged {e.OldStage} -> {e.NewStage}", LogLevel.Info);
        }

        [EventPriority(EventPriority.Low - 1337)]
        private void GameLoop_GameLaunchedSlow(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            Monitor.Log($"[{timer.ElapsedMilliseconds:N6}][Slow] Game Launched", LogLevel.Info);
        }

        [EventPriority(EventPriority.Low - 1337)]
        private void GameLoop_DayStartedSlow(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            Monitor.Log($"[{timer.ElapsedMilliseconds:N6}][Slow] Day Started", LogLevel.Info);
        }

        [EventPriority(EventPriority.Low - 1337)]
        private void GameLoop_SaveLoadedSlow(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            Monitor.Log($"[{timer.ElapsedMilliseconds:N6}][Slow] Save Loaded", LogLevel.Info);
        }
    }
}
