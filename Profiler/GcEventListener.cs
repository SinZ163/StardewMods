using StardewModdingAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Profiler;

enum GCReason
{
    SmallObjectHeapAllocation = 0,
    Induced = 1,
    LowMemory = 2,
    Empty = 3,
    LargeObjectHeapAllocation = 4,
    OutOfSpaceForSmall = 5,
    OutOfSpaceForLarge = 6,
    InducedNonBlocking = 7,
}
enum GCType
{
    Blocking = 0,
    Background = 1,
    BackgroundBlocking = 2
}

internal record GcEventDetails(double StartedAt, GCReason Reason, GCType Type, uint Depth);

public class GcEventListener(ProfilerAPI profilerAPI, IMonitor monitor) : EventListener
{
    private EventSource _eventSourceDotNet;
    private static readonly int keywordGC = 1;

    private static Stopwatch stopwatch = Stopwatch.StartNew();

    private static readonly ConcurrentDictionary<uint, GcEventDetails> trackedGC = new();


    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        try
        {
            if (!eventSource.Name.Equals("Microsoft-Windows-DotNETRuntime")) return;

            EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)keywordGC);
            _eventSourceDotNet = eventSource;
        }
        catch (Exception e)
        {
            monitor.Log("EnableEvents failed - no GC metrics will be collected", LogLevel.Error);
            monitor.Log(e.Message);
            monitor.Log(e.StackTrace);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName.Contains("GCStart_V2"))
        {
            GetValue("Count", out var count);
            GetValue("Depth", out var depth);
            GetValue("Reason", out var reason);
            GetValue("Type", out var type);
            trackedGC.TryAdd((uint)count, new(stopwatch.Elapsed.TotalMilliseconds, (GCReason)(uint)reason, (GCType)(uint)type, (uint)depth));
            //trackedGC.TryAdd((uint)count, (stopwatch.Elapsed.TotalMilliseconds, $"Depth: {depth}, Reason: {Enum.GetName(typeof(GCReason), reason)}, Type: {Enum.GetName(typeof(GCType), type)}"));
            //profilerAPI.Push(new EventDurationMetadata("$CLR", "GCPause", $"Depth: {depth}, Reason: {Enum.GetName(typeof(GCReason), reason)}", -1, []));
            //monitor.Log($"GCStart {stopwatch.Elapsed.TotalMilliseconds:N} {count} {depth} {reason} {type}", LogLevel.Debug);
        }
        if (eventData.EventName.Contains("GCEnd_V1"))
        {
            GetValue("Count", out var count);
            GetValue("Depth", out var depth);

            if (trackedGC.TryGetValue((uint)count, out var gcInfo))
            {
                trackedGC.TryRemove(new((uint)count, default));
                /*profilerAPI.Pop(metadata =>
                {
                    if (metadata is EventDurationMetadata durationMetadata)
                    {
                        metadata = durationMetadata with { Duration = stopwatch.Elapsed.TotalMilliseconds - startTime };
                    }
                    return metadata;
                });*/
                profilerAPI.AddWaterfallEvent(new WaterfallEvent(
                    ModId: "$CLR",
                    EventType: "GCPause " + gcInfo.Type.ToString(),
                    Timings: new()
                    {
                        ["Start"] = gcInfo.StartedAt,
                        ["End"] = stopwatch.Elapsed.TotalMilliseconds
                    },
                    Details: new()
                    {
                        ["Reason"] = gcInfo.Reason.ToString(),
                        ["Depth"] = gcInfo.Depth.ToString(),
                    }
                ));
            }
            //monitor.Log($"GCEnd {stopwatch.Elapsed.TotalMilliseconds:N} {count} {depth}", LogLevel.Debug);
        }

        void GetValue(string name, out object? value)
        {
            value = null;
            var gen0SizeIndex = IndexOf(name);
            if (gen0SizeIndex < 0) return;

            value = eventData.Payload[gen0SizeIndex];
        }

        int IndexOf(string name)
        {
            return eventData.PayloadNames.IndexOf(name);
        }
    }
}
