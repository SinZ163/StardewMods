using ContentPatcher.Framework.ConfigModels;
using Newtonsoft.Json;
using Pathoschild.Stardew.Common.Utilities;
using StardewModdingAPI;

namespace SinZ.Debugger.DAP;

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