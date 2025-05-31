using ContentPatcher.Framework.ConfigModels;
using Newtonsoft.Json;
using Pathoschild.Stardew.Common.Utilities;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SinZ.Debugger;

public class ContentConfigExtended : ContentConfig
{
    public ContentConfigExtended(ISemanticVersion? format, DynamicTokenConfig?[]? dynamicTokens, InvariantDictionary<string?>? aliasTokenNames, CustomLocationConfig?[]? customLocations, PatchConfigExtended?[]? changes, InvariantDictionary<ConfigSchemaFieldConfig?>? configSchema) : base(format, dynamicTokens, aliasTokenNames, customLocations, changes, configSchema)
    {
    }

    protected int DidExtend = 1;
}
[JsonConverter(typeof(LineNumberConverter))]
public class PatchConfigExtended : PatchConfig, IHasLineNumberRange
{
    [JsonIgnore]
    
    public LineNumberRange Debugger_LineNumberRange { get; set; }

}