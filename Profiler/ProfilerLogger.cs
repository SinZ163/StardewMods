using StardewModdingAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Profiler
{
    internal record ProfileLoggerRow(double OccuredAt, object Metadata);

    public class ProfilerLogger : IDisposable
    {

        private readonly StreamWriter File;


        internal ConcurrentStack<EventEntry> EventMetadata { get; private set; }

        internal ProfilerLogger(string path)
        {
            EventMetadata = new();
            File = new StreamWriter(path, append: false);
        }

        internal void AddRow(ProfileLoggerRow row)
        {
            File.WriteLine(JsonSerializer.Serialize(row));
            File.Flush();
        }

        public void Dispose()
        {
            File.Dispose();
        }
    }
}
