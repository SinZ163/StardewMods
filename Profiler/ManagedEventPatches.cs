﻿using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Profiler
{
    internal static class ManagedEventPatches
    {
        public static IMonitor Monitor { get; private set; }
        public static ModConfig Config { get; private set; }

        public static ProfilerAPI API { get; private set; }


        public static void Initialize(IMonitor monitor, ModConfig config, ProfilerAPI api, Harmony harmony)
        {
            Monitor = monitor;
            Config = config;
            API = api;

            var targetClass = Type.GetType("StardewModdingAPI.Framework.Events.ManagedEvent`1,StardewModdingAPI").MakeGenericType(typeof(TimeChangedEventArgs));
            var modMetadata = Type.GetType("StardewModdingAPI.Framework.IModMetadata,StardewModdingAPI");

            harmony.Patch(
                targetClass.GetMethod("Raise", new Type[] { typeof(TimeChangedEventArgs) }),
                transpiler: new HarmonyMethod(typeof(ManagedEventPatches), nameof(Raise__Transpiler))
            );
            harmony.Patch(
                targetClass.GetMethod("Raise", new Type[] { typeof(Action<,>).MakeGenericType(modMetadata, typeof(Action<>).MakeGenericType(typeof(TimeChangedEventArgs))) }),
                transpiler: new HarmonyMethod(typeof(ManagedEventPatches), nameof(Raise__Transpiler))
            );
        }

        public static IEnumerable<CodeInstruction> Raise__Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions)
        {
            var output = new List<CodeInstruction>();
            var outerSW = generator.DeclareLocal(typeof(Stopwatch));
            var innerSW = generator.DeclareLocal(typeof(Stopwatch));
            var timers = generator.DeclareLocal(typeof(List<>).MakeGenericType(typeof(ValueTuple<,>).MakeGenericType(typeof(IModInfo), typeof(Stopwatch))));

            // Initialize the timer list
            output.Add(new CodeInstruction(OpCodes.Nop));
            output.Add(new CodeInstruction(OpCodes.Newobj, timers.LocalType.GetConstructor(Array.Empty<Type>())));
            output.Add(new CodeInstruction(OpCodes.Stloc, timers.LocalIndex));

            // Initialize the outer Stopwatch
            output.Add(new CodeInstruction(OpCodes.Nop));
            output.Add(new CodeInstruction(OpCodes.Call, outerSW.LocalType.GetMethod("StartNew")));
            output.Add(new CodeInstruction(OpCodes.Stloc, outerSW.LocalIndex));

            CodeInstruction lastLdLoc = null;
            CodeInstruction handlerLocal = null;
            foreach (var instruction in instructions)
            {
                if (instruction.IsLdloc())
                {
                    lastLdLoc = instruction;
                }
                if (instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo).Name == "get_SourceMod")
                {
                    handlerLocal = lastLdLoc;
                }
                // Pushing Mod into HeuristicModsRunningCode stack
                if (instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo).Name == "Push")
                {
                    // Call StartTimerSmallLoop(this, handler): Stopwatch
                    output.Add(new CodeInstruction(OpCodes.Nop));
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    output.Add(handlerLocal);
                    output.Add(new CodeInstruction(OpCodes.Call, typeof(ManagedEventPatches).GetMethod(nameof(StartTimerSmallLoop))));
                    output.Add(new CodeInstruction(OpCodes.Stloc, innerSW.LocalIndex));
                }
                else if (instruction.opcode == OpCodes.Ret)
                {
                    // Call StopTimerBigLoop(this, outerSW, timers);
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    output.Add(new CodeInstruction(OpCodes.Ldloc, outerSW.LocalIndex));
                    output.Add(new CodeInstruction(OpCodes.Ldloc, timers.LocalIndex));
                    output.Add(new CodeInstruction(OpCodes.Call, typeof(ManagedEventPatches).GetMethod(nameof(StopTimerBigloop))));
                    output.Add(new CodeInstruction(OpCodes.Nop));
                }
                output.Add(instruction);
                // Popping Mod from HeuristicModsRunningCode stack
                if (instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo).Name == "TryPop")
                {
                    // Call StopTimerSmallLoop(this, handler, innerSW, timers);
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    output.Add(handlerLocal);
                    output.Add(new CodeInstruction(OpCodes.Ldloc, innerSW.LocalIndex));
                    output.Add(new CodeInstruction(OpCodes.Ldloc, timers.LocalIndex));
                    output.Add(new CodeInstruction(OpCodes.Call, typeof(ManagedEventPatches).GetMethod(nameof(StopTimerSmallLoop))));
                    output.Add(new CodeInstruction(OpCodes.Nop));
                }
            }
            return output;
        }
        public static Stopwatch StartTimerSmallLoop(object instance, object handler)
        {
            // intentionally not setting modId and eventType to save on reflection calls
            API.Push(new EventDurationMetadata("", "", "", -1, new()));
            return Stopwatch.StartNew();
        }
        public static void StopTimerSmallLoop(object instance, object handler, Stopwatch sw, List<(IModInfo, Stopwatch)> timers)
        {
            sw.Stop();

            // For whatever reason, one of the overloads of Raise wraps the local variable in a thing, this grabs both
            var actualHandler = handler.GetType().GetField("handler")?.GetValue(handler) ?? handler;
            var property = actualHandler.GetType().GetProperty("SourceMod");
            var modInfo = property.GetValue(actualHandler) as IModInfo;
            timers.Add((modInfo, sw));

            string eventName = (string)instance.GetType().GetProperty("EventName").GetValue(instance);
            var extra = eventName switch
            {
                "GameLoop.TimeChanged" => $" ({Game1.timeOfDay:D4})",
                _ => ""
            };

            API.Pop(metadata =>
            {
                metadata = metadata with { 
                    ModId = modInfo.Manifest.UniqueID,
                    EventType = eventName,
                    Details = extra
                };
                if (metadata is EventDurationMetadata durationMetadata)
                {
                    metadata = durationMetadata with { Duration = sw.Elapsed.TotalMilliseconds };
                }
                return metadata;
            });

            if (sw.Elapsed.TotalMilliseconds > Config.EventThreshold)
            {
                Monitor.Log($"'{modInfo.Manifest.UniqueID}' took {sw.Elapsed.TotalMilliseconds:N}ms handling {eventName}{extra}", LogLevel.Debug);
            }
        }
        public static void StopTimerBigloop(object instance, Stopwatch sw, List<(IModInfo, Stopwatch)> timers)
        {
            sw.Stop();
            if (!API.EventMetadata.IsEmpty)
            {

                var eventName = (string)instance.GetType().GetProperty("EventName").GetValue(instance);
                if (eventName.StartsWith("GameLoop."))
                {
                    Monitor.Log("An error occurred during Profiler logging that caused the internal stack to desync. Emptying now.", LogLevel.Error);
                    while (!API.EventMetadata.IsEmpty)
                    {
                        API.Pop();
                    }
                }
            }

            if (sw.Elapsed.TotalMilliseconds > Config.BigLoopThreshold)
            {
                var eventName = instance.GetType().GetProperty("EventName").GetValue(instance);
                var extra = eventName switch
                {
                    "GameLoop.TimeChanged" => $" ({Game1.timeOfDay:D4})",
                    _ => ""
                };
                var slowestMods = timers.Where(o => o.Item2.Elapsed.TotalMilliseconds >= Config.BigLoopInnerThreshold).OrderByDescending(o => o.Item2.Elapsed.TotalMilliseconds);
                var msg = string.Join("\n", slowestMods.Select(t => $"\t{t.Item1.Manifest.UniqueID} => {t.Item2.Elapsed.TotalMilliseconds:N}ms"));
                Monitor.Log($"[BigLoop] In total, it took {sw.Elapsed.TotalMilliseconds:N}ms handling {eventName}{extra}\n{msg}", LogLevel.Debug);
            }
        }
    }
}
