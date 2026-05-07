using System.Text.Json.Serialization;

namespace QuickSType.UI.SmokeTest;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SmokeExpected))]
internal partial class SmokeJsonContext : JsonSerializerContext
{
}
