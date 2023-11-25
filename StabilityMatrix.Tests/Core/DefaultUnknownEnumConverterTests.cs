using System.Text.Json;
using System.Text.Json.Serialization;
using StabilityMatrix.Core.Converters.Json;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class DefaultUnknownEnumConverterTests
{
    [TestMethod]
    [ExpectedException(typeof(JsonException))]
    public void TestDeserialize_NormalEnum_ShouldError()
    {
        const string json = "\"SomeUnknownValue\"";

        JsonSerializer.Deserialize<NormalEnum>(json);
    }

    [TestMethod]
    public void TestDeserialize_UnknownEnum_ShouldConvert()
    {
        const string json = "\"SomeUnknownValue\"";

        var result = JsonSerializer.Deserialize<UnknownEnum>(json);

        Assert.AreEqual(UnknownEnum.Unknown, result);
    }

    [TestMethod]
    public void TestDeserialize_DefaultEnum_ShouldConvert()
    {
        const string json = "\"SomeUnknownValue\"";

        var result = JsonSerializer.Deserialize<DefaultEnum>(json);

        Assert.AreEqual(DefaultEnum.CustomDefault, result);
    }

    [TestMethod]
    public void TestSerialize_UnknownEnum_ShouldConvert()
    {
        const string expected = "\"Unknown\"";

        var result = JsonSerializer.Serialize(UnknownEnum.Unknown);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void TestSerialize_DefaultEnum_ShouldConvert()
    {
        const string expected = "\"CustomDefault\"";

        var result = JsonSerializer.Serialize(DefaultEnum.CustomDefault);

        Assert.AreEqual(expected, result);
    }

    private enum NormalEnum
    {
        Unknown,
        Value1,
        Value2
    }

    [JsonConverter(typeof(DefaultUnknownEnumConverter<UnknownEnum>))]
    private enum UnknownEnum
    {
        Unknown,
        Value1,
        Value2
    }

    [JsonConverter(typeof(DefaultUnknownEnumConverter<DefaultEnum>))]
    private enum DefaultEnum
    {
        CustomDefault,
        Value1,
        Value2
    }
}
