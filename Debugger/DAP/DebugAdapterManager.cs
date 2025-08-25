using StardewModdingAPI;
using System.Net.Sockets;
using System.Net;
using StardewModdingAPI.Framework;
using ContentPatcher.Framework.ConfigModels;
using ContentPatcher.Framework.Patches;
using ContentPatcher.Framework;
using HarmonyLib;
using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Concurrent;

namespace SinZ.Debugger.DAP;

internal class DebugAdapterManager
{
    private IModHelper Helper;
    private IMonitor Monitor;

    public static ConcurrentDictionary<int, DebugAdapter> Adapters = new();
    public static Dictionary<string, Dictionary<ContentPatcher.Framework.Patches.Patch, PatchConfigExtended>> SourceMap = new(StringComparer.InvariantCultureIgnoreCase);

    public DebugAdapterManager(IModHelper helper, IMonitor monitor, Harmony harmony)
    {
        this.Helper = helper;
        this.Monitor = monitor;
        ContentPatcher.ModEntry? cpModInstance = (helper.ModRegistry.Get("Pathoschild.ContentPatcher") as IModMetadata)?.Mod as ContentPatcher.ModEntry;
        if (cpModInstance == null)
        {
            Monitor.Log("Content Patcher is missing", LogLevel.Error);
            return;
        }
        StartDebugServer(cpModInstance);

        Patches.Init(harmony, helper, monitor);
    }

    private void StartDebugServer(ContentPatcher.ModEntry cpMod)
    {
        Thread listenThread = new Thread(() =>
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Parse("0.0.0.0"), ModEntry.Config.Port);
                listener.Start();

                while (true)
                {
                    Socket clientSocket = listener.AcceptSocket();
                    Thread clientThread = new Thread(() =>
                    {
                        try
                        {
                            ModEntry.Log("Accepted connection");

                            using (Stream stream = new NetworkStream(clientSocket))
                            {
                                var adapter = new DebugAdapter(stream, stream, cpMod);
                                adapter.Protocol.LogMessage += (sender, e) => ModEntry.Log(e.Message);
                                adapter.Protocol.DispatcherError += (sender, e) =>
                                {
                                    ModEntry.Log(e.Exception.Message, LogLevel.Error);
                                };
                                DebugAdapterManager.Adapters.TryAdd(Thread.CurrentThread.ManagedThreadId, adapter);
                                adapter.Run();
                                adapter.Protocol.WaitForReader();

                                DebugAdapterManager.Adapters.TryRemove(new(Thread.CurrentThread.ManagedThreadId, adapter));
                                adapter = null;
                            }

                            ModEntry.Log("Connection closed");
                        } catch (Exception e)
                        {

                            Monitor.Log("An error occurred on socket thread: " + e.Message, LogLevel.Error);
                            Monitor.Log(e.StackTrace ?? "Unknown stack trace");
                        }
                    });

                    clientThread.Name = "DebugServer connection thread";
                    clientThread.Start();
                }
            } catch (Exception e)
            {
                Monitor.Log("An error occurred on listener thread: " + e.Message, LogLevel.Error);
                Monitor.Log(e.StackTrace ?? "Unknown stack trace");
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
            if (!DebugAdapterManager.SourceMap.ContainsKey(filepath))
            {
                DebugAdapterManager.SourceMap.Add(filepath, new());
            }
            DebugAdapterManager.SourceMap[filepath].TryAdd(patch, entryExtended);
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
        foreach (var adapter in DebugAdapterManager.Adapters)
        {
            adapter.Value.UpdateContext();
        }
    }
}
