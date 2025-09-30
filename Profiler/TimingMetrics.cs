using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Profiler
{
    internal static class TimingMetrics
    {
        public static IProfilerAPI API { get; private set; }

        private static IMonitor Monitor;

        private static readonly Stopwatch DrawStopwatch = new();
        private static TimeSpan DrawStartedAt = TimeSpan.Zero;

        private static readonly Stopwatch UpdateStopwatch = new();
        private static TimeSpan UpdateStartedAt = TimeSpan.Zero;

        public static void Initialize(IProfilerAPI api, Harmony harmony, IMonitor monitor)
        {
            Monitor = monitor;
            API = api;

            harmony.Patch(
                typeof(DebugTimings).GetMethod(nameof(DebugTimings.StartDrawTimer)),
                postfix: new HarmonyMethod(typeof(TimingMetrics), nameof(DebugTimings__StartDrawTimer__Postfix))
            );
            harmony.Patch(
                typeof(DebugTimings).GetMethod(nameof(DebugTimings.StopDrawTimer)),
                postfix: new HarmonyMethod(typeof(TimingMetrics), nameof(DebugTimings__StopDrawTimer__Postfix))
            );
            harmony.Patch(
                typeof(DebugTimings).GetMethod(nameof(DebugTimings.StartUpdateTimer)),
                postfix: new HarmonyMethod(typeof(TimingMetrics), nameof(DebugTimings__StartUpdateTimer__Postfix))
            );
            harmony.Patch(
                typeof(DebugTimings).GetMethod(nameof(DebugTimings.StopUpdateTimer)),
                postfix: new HarmonyMethod(typeof(TimingMetrics), nameof(DebugTimings__StopUpdateTimer__Postfix))
            );
        }
        private static void DebugTimings__StartDrawTimer__Postfix()
        {
            DrawStartedAt = API.GetTime();
            DrawStopwatch.Restart();
        }

        private static void DebugTimings__StopDrawTimer__Postfix()
        {
            DrawStopwatch.Stop();
            API.AddMetric("Stardew Valley", "DrawTime", DrawStopwatch.Elapsed.TotalMilliseconds, DrawStartedAt.TotalMilliseconds);
        }

        private static void DebugTimings__StartUpdateTimer__Postfix()
        {
            UpdateStartedAt = API.GetTime();
            UpdateStopwatch.Restart();
        }

        private static void DebugTimings__StopUpdateTimer__Postfix()
        {
            UpdateStopwatch.Stop();
            API.AddMetric("Stardew Valley", "UpdateTime", UpdateStopwatch.Elapsed.TotalMilliseconds, UpdateStartedAt.TotalMilliseconds);
        }


    }
}
