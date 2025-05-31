using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace SinZ.Debugger;

public class LineNumberRange
{
    public int StartLineNumber { get; set; }
    public int StartLineColumn { get; set; }

    public int EndLineNumber { get; set; }
    public int EndLineColumn { get; set; }
}
public interface IHasLineNumberRange
{
    public LineNumberRange Debugger_LineNumberRange { get; set; }
}

class LineNumberConverter : JsonConverter
{
    public override bool CanWrite
    {
        get { return false; }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException("Converter is not writable. Method should not be invoked");
    }

    public override bool CanConvert(Type objectType)
    {
        return typeof(IHasLineNumberRange).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.StartObject)
        {
            var jsonLineInfo = reader as IJsonLineInfo;

            var rawJObject = JObject.Load(reader);
            var lineInfoObject = Activator.CreateInstance(objectType) as IHasLineNumberRange;
            serializer.Populate(this.CloneReader(reader, rawJObject), lineInfoObject);

            lineInfoObject.Debugger_LineNumberRange = new()
            {
                StartLineNumber = (rawJObject as IJsonLineInfo).LineNumber,
                StartLineColumn = (rawJObject as IJsonLineInfo).LinePosition,
                EndLineNumber = jsonLineInfo.LineNumber,
                EndLineColumn = jsonLineInfo.LinePosition,
            };

            return lineInfoObject;
        }

        return null;
    }

    private JsonReader CloneReader(JsonReader reader, JObject jobject)
    {
        var clonedReader = jobject.CreateReader();

        clonedReader.Culture = reader.Culture;
        clonedReader.DateParseHandling = reader.DateParseHandling;
        clonedReader.DateTimeZoneHandling = reader.DateTimeZoneHandling;
        clonedReader.FloatParseHandling = reader.FloatParseHandling;
        clonedReader.MaxDepth = reader.MaxDepth;
        return clonedReader;
    }
}