using StardewModdingAPI;
using System.Collections.Concurrent;
using xTile.Format;
using TMXTile;
using HarmonyLib;
using xTile;
using xTile.Layers;
using xTile.Dimensions;
using StardewValley;

namespace SinZ.SpeedySolutions;

public static class TBinMapper
{
    public record TBinEntry(long Length, long WriteTime, string CacheName);
    public static ConcurrentDictionary<string, TBinEntry> Database = new();
    public static ConcurrentDictionary<string, MemoryStream> InMemoryCache = new();

    private static IModHelper helper;
    private static IMonitor monitor;

    private static ConcurrentQueue<string> PendingCache = new();

    private static Size FakeSize = new(Game1.tileSize / Game1.pixelZoom);
    private static Size TileSize = new(Game1.tileSize);

    internal static void Init(Harmony harmony, IModHelper helper, IMonitor monitor)
    {
        TBinMapper.helper = helper;
        helper.Events.GameLoop.Saving += GameLoop_Saving;
        TBinMapper.monitor = monitor;
        Directory.CreateDirectory(Path.Combine(helper.DirectoryPath, "cache"));
        Database = new ConcurrentDictionary<string, TBinEntry>(helper.Data.ReadJsonFile<Dictionary<string, TBinEntry>>("cache/cache.json") ?? []);

        harmony.Patch(
            AccessTools.Method(typeof(TMXFormat), nameof(TMXFormat.Load), [typeof(Stream)]),
            prefix: new HarmonyMethod(typeof(TBinMapper).GetMethod(nameof(TMXFormat__Load__Prefix))),
            postfix: new HarmonyMethod(typeof(TBinMapper).GetMethod(nameof(TMXFormat__Load__Postfix)))
        );
    }

    private static void GameLoop_Saving(object? sender, StardewModdingAPI.Events.SavingEventArgs e)
    {
        helper.Data.WriteJsonFile("cache/cache.json", new Dictionary<string, TBinEntry>(Database));
    }

    internal static void RemoveEntry(string fullName, FileInfo fileRef)
    {
        Database.Remove(fullName, out _);
        if (fileRef.Exists)
        {
            fileRef.Delete();
        }
    }

    public static bool TMXFormat__Load__Prefix(Stream stream, ref Map __result, out bool __state)
    {
        __state = false;
        // only tmx files need to go into the TBinMapper logic
        if (stream is not FileStream fsStream)
        {
            return true;
        }
        monitor.Log("Checking if asset is in the TBin cache");
        if (!Database.TryGetValue(fsStream.Name, out var cacheEntry))
        {
            if (!ModEntry.Config.EnableTBinSave) return true;
            __state = true;
            monitor.Log("Not in cache: " + fsStream.Name);
            return true;
        }
        if (!ModEntry.Config.EnableTBinLoad) return true;
        var file = new FileInfo(fsStream.Name);
        var replacementFile = new FileInfo(Path.Combine(helper.DirectoryPath, "cache", cacheEntry.CacheName));
        if (!replacementFile.Exists || file.Length != cacheEntry.Length || file.LastWriteTimeUtc.ToFileTimeUtc() != cacheEntry.WriteTime)
        {
            monitor.Log("Cache entry is invalid, clearing");
            RemoveEntry(file.FullName, replacementFile);
            return true;
        }
        if (InMemoryCache.TryGetValue(replacementFile.FullName, out var memStream))
        {
            monitor.Log("In Memory cache has an entry, using in memory cache and skipping fs entirely");
            memStream.Position = 0;
            __result = FormatManager.Instance.BinaryFormat.Load(memStream);
            return false;
        }
        if (cacheEntry.Length < ModEntry.Config.MapInMemoryThreshold)
        {
            monitor.Log("Map is eligible for in memory cache, writing to in-memory cache");
            var ms = new MemoryStream();
            using var fs = replacementFile.OpenRead();
            fs.CopyTo(ms);
            InMemoryCache.TryAdd(replacementFile.FullName, ms);
        }
        __result = FormatManager.Instance.LoadMap(replacementFile.FullName);
        return false;
    }
    public static void TMXFormat__Load__Postfix(ref Stream stream, ref Map __result, bool __state)
    {
        if (!__state) return;
        var fileName = ((FileStream)stream).Name;
        var file = new FileInfo(fileName);

        monitor.Log("Storing a version of map in " + Path.GetFileName(fileName));
        try
        {
            var cacheName = Path.GetFileNameWithoutExtension(fileName) + "." + Guid.NewGuid().ToString() + ".tbin";
            using (var fs = new FileStream(Path.Combine(helper.DirectoryPath, "cache", cacheName), FileMode.Create))
            {
                Layer.m_tileSize = FakeSize;
                FormatManager.Instance.BinaryFormat.Store(__result, fs);
            }
            Database[fileName] = new(file.Length, file.LastWriteTimeUtc.ToFileTimeUtc(), cacheName);
        } catch (Exception e)
        {
            monitor.Log("Failed to store cache entry for " + fileName, LogLevel.Error);
            monitor.Log($"Error: {e.GetType().Name}, Message: {e.Message}");
        }
        finally
        {
            Layer.m_tileSize = TileSize;
        }
    }
}
