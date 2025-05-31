using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace SinZ.Debugger;

public class Config
{
    public int Port { get; set; } = 1337;

    public KeybindList ScheduleDebugger = new(SButton.F8);
}
