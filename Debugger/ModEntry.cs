using StardewModdingAPI;
using System.Net.Sockets;
using System.Net;
using StardewModdingAPI.Framework;
using HarmonyLib;
using System.Collections.Concurrent;
using System.Reflection.Emit;
using System.Reflection;
using ContentPatcher.Framework.Patches;
using ContentPatcher.Framework.ConfigModels;
using ContentPatcher.Framework;

namespace SinZ.Debugger;

public class ModEntry : Mod
{
    public Config Config;
    public override void Entry(IModHelper helper)
    {
        StaticMonitor = this.Monitor;
        this.Config = helper.ReadConfig<Config>();
        ContentPatcher.ModEntry? cpModInstance = (helper.ModRegistry.Get("Pathoschild.ContentPatcher") as IModMetadata)?.Mod as ContentPatcher.ModEntry;
        if (cpModInstance == null)
        {
            Monitor.Log("Content Patcher is missing", LogLevel.Error);
            return;
        }
        var harmony = new Harmony(this.ModManifest.UniqueID);
        Patches.Init(harmony, helper, this.Monitor);
        StartDebugServer(cpModInstance);
    }

    private static IMonitor StaticMonitor;
    public static void Log(string message, LogLevel level = LogLevel.Trace)
    {
        lock(StaticMonitor)
        {
            StaticMonitor.Log(message, level);
        }
    }

    public static ConcurrentDictionary<int, DebugAdapter> Adapters = new();
    public static Dictionary<string, Dictionary<ContentPatcher.Framework.Patches.Patch, PatchConfigExtended>> SourceMap = new(StringComparer.InvariantCultureIgnoreCase);

    private void StartDebugServer(ContentPatcher.ModEntry cpMod)
    {
        Thread listenThread = new Thread(() =>
        {
            TcpListener listener = new TcpListener(IPAddress.Parse("0.0.0.0"), this.Config.Port);
            listener.Start();

            while (true)
            {
                Socket clientSocket = listener.AcceptSocket();
                Thread clientThread = new Thread(() =>
                {
                    Log("Accepted connection");

                    using (Stream stream = new NetworkStream(clientSocket))
                    {
                        var adapter = new DebugAdapter(stream, stream, cpMod);
                        adapter.Protocol.LogMessage += (sender, e) => Log(e.Message);
                        adapter.Protocol.DispatcherError += (sender, e) =>
                        {
                            Log(e.Exception.Message, LogLevel.Error);
                        };
                        Adapters.TryAdd(Thread.CurrentThread.ManagedThreadId, adapter);
                        adapter.Run();
                        adapter.Protocol.WaitForReader();

                        Adapters.TryRemove(new(Thread.CurrentThread.ManagedThreadId, adapter));
                        adapter = null;
                    }

                    Log("Connection closed");
                });

                clientThread.Name = "DebugServer connection thread";
                clientThread.Start();
            }
        });
        listenThread.Name = "DebugServer listener thread";
        listenThread.Start();
    }
}
public static class Patches
{
    public static IMonitor monitor;
    public static void Init(Harmony harmony, IModHelper helper, IMonitor monitor)
    {
        Patches.monitor = monitor;
        harmony.Patch(AccessTools.Method(typeof(TokenManager), nameof(TokenManager.UpdateContext)), postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(TokenManager__UpdateContext__Postfix))));
        harmony.Patch(AccessTools.Method(typeof(RawContentPack), nameof(RawContentPack.TryReloadContent)), transpiler: new HarmonyMethod(typeof(Patches).GetMethod(nameof(RawContentPack__TryReloadContent__Transpiler))));
        harmony.Patch(AccessTools.Method(typeof(IncludePatch), nameof(IncludePatch.AttemptLoad)), transpiler: new HarmonyMethod(typeof(Patches).GetMethod(nameof(IncludePatch__AttemptLoad__Transpiler))));
        harmony.Patch(AccessTools.Method(typeof(PatchLoader), "LoadPatch"), postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(PatchLoader__LoadPatch__Postfix))));
    }

    public static void PatchLoader__LoadPatch__Postfix(IPatch? __result, RawContentPack rawContentPack, PatchConfig entry, int[] indexPath, ContentPatcher.Framework.Patches.Patch? parentPatch)
    {
        var entryExtended = entry as PatchConfigExtended;
        var patch = __result as ContentPatcher.Framework.Patches.Patch;
        if (patch != null && entryExtended != null)
        {
            var filepath = parentPatch != null ? Path.Combine(rawContentPack.ContentPack.DirectoryPath, parentPatch.FromAsset) : Path.Combine(rawContentPack.ContentPack.DirectoryPath, "content.json");
            if (!ModEntry.SourceMap.ContainsKey(filepath))
            {
                ModEntry.SourceMap.Add(filepath, new());
            }
            ModEntry.SourceMap[filepath].TryAdd(patch, entryExtended);
        }
    }

    public static IEnumerable<CodeInstruction> RawContentPack__TryReloadContent__Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo)!.Name == "ReadJsonFile")
            {
                var method = instruction.operand as MethodInfo;
                instruction.operand = typeof(IContentPack).GetMethod(nameof(IContentPack.ReadJsonFile))!.MakeGenericMethod(typeof(ContentConfigExtended));
            }
        }
        return instructions;
    }
    public static IEnumerable<CodeInstruction> IncludePatch__AttemptLoad__Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo)!.Name == "Load")
            {
                var method = instruction.operand as MethodInfo;
                instruction.operand = typeof(IModContentHelper).GetMethod(nameof(IModContentHelper.Load))!.MakeGenericMethod(typeof(ContentConfigExtended));
            }
        }
        return instructions;
    }

    public static void TokenManager__UpdateContext__Postfix()
    {
        foreach(var adapter in ModEntry.Adapters)
        {
            adapter.Value.UpdateContext();
        }
    }
}
