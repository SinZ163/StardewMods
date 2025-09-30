using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Diagnostics;

namespace Profiler
{
    internal class PlayerLogger
    {
        private readonly IMonitor Monitor;
        private readonly Stopwatch timer;

        public PlayerLogger(IModHelper helper, IMonitor monitor, Stopwatch timer)
        {
            this.Monitor = monitor;
            this.timer = timer;

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
        }

        [EventPriority((EventPriority)int.MaxValue)]
        private void Specialized_LoadStageChangedFast(object sender, LoadStageChangedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] LoadStageChanged {e.OldStage} -> {e.NewStage}", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MaxValue)]
        private void GameLoop_GameLaunchedFast(object sender, GameLaunchedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] Game Launched", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MaxValue)]
        private void GameLoop_DayStartedFast(object sender, DayStartedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] Day Started", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MaxValue)]
        private void GameLoop_SaveLoadedFast(object sender, SaveLoadedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] Save Loaded", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MaxValue)]
        private void Player_WarpedFast(object sender, WarpedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Fast] Warped {e.OldLocation.NameOrUniqueName} -> {e.NewLocation.NameOrUniqueName} ({Game1.timeOfDay:D4})", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MinValue)]
        private void Specialized_LoadStageChangedSlow(object sender, LoadStageChangedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] LoadStageChanged {e.OldStage} -> {e.NewStage}", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MinValue)]
        private void GameLoop_GameLaunchedSlow(object sender, GameLaunchedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] Game Launched", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MinValue)]
        private void GameLoop_DayStartedSlow(object sender, DayStartedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] Day Started", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MinValue)]
        private void GameLoop_SaveLoadedSlow(object sender, SaveLoadedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] Save Loaded", LogLevel.Info);
        }

        [EventPriority((EventPriority)int.MinValue)]
        private void Player_WarpedSlow(object sender, WarpedEventArgs e)
        {
            Monitor.Log($"[{timer.Elapsed.TotalMilliseconds:N}][Slow] Warped {e.OldLocation.NameOrUniqueName} -> {e.NewLocation.NameOrUniqueName} ({Game1.timeOfDay:D4})", LogLevel.Info);
        }
    }
}
