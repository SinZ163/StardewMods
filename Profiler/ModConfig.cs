using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Profiler
{
    internal class ModConfig
    {
        public int BigLoopThreshold { get; set; } = 100;
        public int EventThreshold { get; set; } = 10;

        public double LoggerDurationThreshold { get; set; } = 0.01;
    }
}
