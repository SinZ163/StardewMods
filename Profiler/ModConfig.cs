﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Profiler
{
    internal class ModConfig
    {
        public int BigLoopThreshold { get; set; } = 100;
        public double BigLoopInnerThreshold { get; set; } = 0.01d;
        public int EventThreshold { get; set; } = 10;

        public double LoggerDurationOuterThreshold { get; set; } = 5.0;
        public double LoggerDurationInnerThreshold { get; set; } = 0.1;
    }
}
