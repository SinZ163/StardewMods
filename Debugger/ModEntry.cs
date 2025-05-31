using StardewModdingAPI;
using HarmonyLib;
using System.Collections.Concurrent;
using SinZ.Debugger.Schedule;
using SinZ.Debugger.DAP;

namespace SinZ.Debugger;

public class ModEntry : Mod
{
    public static Config Config;
    private ScheduleDebugger scheduleModule;
    private DebugAdapterManager debugAdapterManager;
    public override void Entry(IModHelper helper)
    {
        StaticMonitor = this.Monitor;
        Config = helper.ReadConfig<Config>();
        var harmony = new Harmony(this.ModManifest.UniqueID);
        scheduleModule = new ScheduleDebugger(helper);
        debugAdapterManager = new DebugAdapterManager(helper, Monitor, harmony);

        helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
    }

    private void GameLoop_GameLaunched(object? sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
    {
        // get Generic Mod Config Menu's API (if it's installed)
        var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu is null)
            return;
        // register mod
        configMenu.Register(
            mod: this.ModManifest,
            reset: () => Config = new Config(),
            save: () => this.Helper.WriteConfig(Config)
        );
        configMenu.AddParagraph(
            mod: this.ModManifest,
            text: () => "Debugger is a mod that contains the various Mod Author facing features by SinZ");
        scheduleModule.PopulateConfig(configMenu, this.ModManifest);

    }

    private static IMonitor StaticMonitor;
    public static void Log(string message, LogLevel level = LogLevel.Trace)
    {
        lock(StaticMonitor)
        {
            StaticMonitor.Log(message, level);
        }
    }
}
