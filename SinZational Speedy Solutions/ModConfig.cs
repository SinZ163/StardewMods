using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SinZ.SpeedySolutions
{
    internal class ModConfig
    {
        public bool EnableAudioCueModificationCache { get; set; } = true;
        public bool EnableSlowGameLocationLoaderBypass { get; set; } = true;

        public bool EnableTBinSave { get; set; } = false;
        public bool EnableTBinLoad { get; set; } = false;

    }
}
