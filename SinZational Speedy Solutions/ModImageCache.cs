using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Framework.Content;

namespace SinZ.SpeedySolutions;

internal static class ModImageCache
{
    private static IModHelper helper;
    private static IMonitor monitor;

    private static readonly Dictionary<string, RawTextureData> TextureCache = [];

    internal static void Init(Harmony harmony, IModHelper helper, IMonitor monitor)
    {
        ModImageCache.helper = helper;
        ModImageCache.monitor = monitor;

        harmony.Patch(
            AccessTools.Method("StardewModdingAPI.Framework.ContentManagers.ModContentManager:LoadRawImageData"),
            prefix: new HarmonyMethod(typeof(GenericPatches).GetMethod(nameof(ModContentManager__LoadRawImageData__Prefix))),
            postfix: new HarmonyMethod(typeof(GenericPatches).GetMethod(nameof(ModContentManager__LoadRawImageData__Postfix)))
        );
    }

    private static void ModContentManager__LoadRawImageData__Postfix(FileInfo file, bool forRawData, ref IRawTextureData __result)
    {
        if (!ModEntry.Config.EnableImageCache) return;
        if (!forRawData)
        {
            ModImageCache.TextureCache.Add(file.FullName, (RawTextureData)__result);
            return;
        }
        ModImageCache.TextureCache.Add(file.FullName, new RawTextureData(__result.Width, __result.Height, (Color[])__result.Data.Clone()));
    }

    private static bool ModContentManager__LoadRawImageData__Prefix(FileInfo file, bool forRawData, ref IRawTextureData __result)
    {
        if (!ModEntry.Config.EnableImageCache) return true;
        if (ModImageCache.TextureCache.TryGetValue(file.FullName, out var cacheResult))
        {
            var cacheColors = cacheResult.Data;
            if (forRawData)
            {
                __result = new RawTextureData(cacheResult.Width, cacheResult.Height, (Color[])cacheColors.Clone());
                return false;
            }
            __result = new RawTextureData(cacheResult.Width, cacheResult.Height, cacheColors);
            return false;
        }
        return true;
    }
}
