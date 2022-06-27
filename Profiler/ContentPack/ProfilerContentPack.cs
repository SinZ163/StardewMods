using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace Profiler
{

    public class ProfilerContentPack
    {
        public string? Format { get; }

        public ProfilerContentPackEntry?[] Entries { get; }

        public ProfilerContentPack(string? format, ProfilerContentPackEntry?[] entries)
        {
            this.Format = format;
            this.Entries = entries ?? Array.Empty<ProfilerContentPackEntry>();
        }
    }
    public class ProfilerContentPackEntry
    {
        public string? Type { get; set;  }
        public string? TargetType { get; set;  }
        public string? TargetMethod { get; set; }

        public string? ConditionalMod { get; set; }

        public ProfilerContentPackDetailEntry? Details { get; set; }

        [JsonConstructor]
        public ProfilerContentPackEntry()
        {
        }
    }
    public class ProfilerContentPackDetailEntry
    {
        public string? Type { get; set; }
        public string? Name { get; set;  }

        [JsonConstructor]
        public ProfilerContentPackDetailEntry()
        {
        }
    }
}
