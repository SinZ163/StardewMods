using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SinZ.SpeedySolutions;

public class ModEntry : Mod
{
    internal static ModConfig Config;
    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();

        var harmony = new Harmony(ModManifest.UniqueID);
        GenericPatches.Init(harmony, helper, Monitor);
        AudioCueModifications.Init(harmony, helper, Monitor);
        TBinMapper.Init(harmony, helper, Monitor);
        ModImageCache.Init(harmony, helper, Monitor);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    }

    private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
    {
        // get Generic Mod Config Menu's API (if it's installed)
        var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (configMenu is null)
            return;

        // register mod
        configMenu.Register(
            mod: this.ModManifest,
            reset: () => ModEntry.Config = new ModConfig(),
            save: () => this.Helper.WriteConfig(ModEntry.Config)
        );
        configMenu.AddParagraph(
            mod: this.ModManifest,
            text: () => "This mod has a bunch of modules handling discrete different changes that improve performance");
        configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "Audio");
        configMenu.AddBoolOption(
            mod: this.ModManifest,
            name: () => "Audio Cue Modification Cache",
            getValue: () => ModEntry.Config.EnableAudioCueModificationCache,
            setValue: value => ModEntry.Config.EnableAudioCueModificationCache = value,
            tooltip: () => "This tracks changes to the Data/AudioChanges asset and suppresses reloading of audio cues that were previously loaded"
        );

        configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "TBin Map Cache");
        configMenu.AddParagraph(
            mod: this.ModManifest,
            text: () => """
            Regular TMX files are XML Text documents that serialize to the TMXMap format, but Stardew uses the tIDEs map format. 
            This means on every .tmx load, SMAPI is deserializing the .tmx XML file, and then having to do a processing step to convert the data model over to the xTiles Map data structure.  
            But there are map formats that write directly to the xTiles Map type, .tbin and .tide 
            .tbin is the binary serialized form, while .tide is the XML form which SMAPI doesn't actually support. 

            This module will track TMX files being loaded, and save them as a tbin file, and then on future loads, will load from the tbin file instead, saving the processing step, along with a more efficient deserialization from a binary file as opposed to XML. 

            This functionality is very experimental, and as such is currently disabled by default. 
                
            NOTE: The database keeping track of the cache will only be written to when the game is saving, so exiting the game without sleeping and starting again will make new cache entries. 

            There is no file cleanup of unreferenced cache entries at this stage.
            """);

        configMenu.AddBoolOption(
            mod: this.ModManifest,
            name: () => "Enable populating cache",
            getValue: () => ModEntry.Config.EnableTBinSave,
            setValue: value => ModEntry.Config.EnableTBinSave = value,
            tooltip: () => "This enables the functionality of saving tmx maps as a tbin on asset load"
        );
        configMenu.AddBoolOption(
            mod: this.ModManifest,
            name: () => "Enable loading from cache",
            getValue: () => ModEntry.Config.EnableTBinLoad,
            setValue: value => ModEntry.Config.EnableTBinLoad = value,
            tooltip: () => "This enables the functionality of loading cached tbins in place of a given tmx file"
        );
        configMenu.AddNumberOption(
            mod: this.ModManifest,
            name: () => "In Memory Threshold",
            getValue: () => ModEntry.Config.MapInMemoryThreshold,
            setValue: value => ModEntry.Config.MapInMemoryThreshold = value,
            tooltip: () => "This sets a threshold in bytes where maps below this size will be stored in-memory for faster loading"
        );

        configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "Modded Image Cache", tooltip: () => "This is for optimising image loads by keeping them in-memory to reduce file io costs");

        configMenu.AddBoolOption(
            mod: this.ModManifest,
            name: () => "Enable In-memory image cache",
            getValue: () => ModEntry.Config.EnableImageCache,
            setValue: value => ModEntry.Config.EnableImageCache = value,
            tooltip: () => "This enables the functionality of having mod images cached so that they don't read from filesystem multiple times"
        );

        configMenu.AddSectionTitle(mod: this.ModManifest, text: () => "Other", tooltip: () => "This is for misc changes that aren't large enough for a standalone section");
        configMenu.AddBoolOption(
            mod: this.ModManifest,
            name: () => "Slow mapLoader Bypass",
            getValue: () => ModEntry.Config.EnableSlowGameLocationLoaderBypass,
            setValue: value => ModEntry.Config.EnableSlowGameLocationLoaderBypass = value,
            tooltip: () => "This bypasses the dedicated mapLoader's that Town and Farmhouse use, so they will remain cached and loaded when leaving and re-entering those locations"
        );

    }
}
public static class GenericPatches
{
    private static IMonitor monitor;
    private static IModHelper helper;
    public static void Init(Harmony harmony, IModHelper helper, IMonitor monitor)
    {
        GenericPatches.monitor = monitor;
        GenericPatches.helper = helper;

        harmony.Patch(AccessTools.Method(typeof(StardewValley.Locations.Town), "getMapLoader"), prefix: new HarmonyMethod(typeof(GenericPatches).GetMethod(nameof(SlowGameLocation__getMapLoader__Prefix))));
        harmony.Patch(AccessTools.Method(typeof(StardewValley.Locations.FarmHouse), "getMapLoader"), prefix: new HarmonyMethod(typeof(GenericPatches).GetMethod(nameof(SlowGameLocation__getMapLoader__Prefix))));
    }

    public static bool SlowGameLocation__getMapLoader__Prefix(ref LocalizedContentManager __result)
    {
        if (!ModEntry.Config.EnableSlowGameLocationLoaderBypass) return true;

        __result = Game1.game1.xTileContent;
        return false;
    }
}