using System.Text.Json;
using System.Text.Json.Nodes;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class AnalyticsRequestSerializationTests
{
    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [TestMethod]
    public void Request_SerializedThroughBaseType_CarriesDiscriminatorAndDerivedProperties()
    {
        AnalyticsRequest request = new PackageInstallAnalyticsRequest
        {
            PackageName = "ComfyUI",
            PackageVersion = "1.0",
            IsSuccess = true,
            Timestamp = DateTimeOffset.Parse("2026-09-01T14:02:11+00:00"),
        };

        var json = JsonSerializer.Serialize(request, WireOptions);
        var expected = """
            {"type":"package-install","packageName":"ComfyUI","packageVersion":"1.0","isSuccess":true,"timestamp":"2026-09-01T14:02:11+00:00"}
            """;
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(json)), json);
    }

    [TestMethod]
    public void EveryRequestType_HasItsWireName()
    {
        var expected = new Dictionary<Type, string>
        {
            [typeof(LaunchAnalyticsRequest)] = "launch",
            [typeof(PackageInstallAnalyticsRequest)] = "package-install",
            [typeof(FirstTimeInstallAnalytics)] = "first-time-install",
        };

        var derivedTypes = typeof(AnalyticsRequest)
            .Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(AnalyticsRequest).IsAssignableFrom(t))
            .ToList();

        CollectionAssert.AreEquivalent(expected.Keys.ToList(), derivedTypes);

        foreach (var (type, name) in expected)
        {
            var instance = (AnalyticsRequest)(
                type == typeof(PackageInstallAnalyticsRequest)
                    ? new PackageInstallAnalyticsRequest { PackageName = "x", PackageVersion = "y" }
                    : Activator.CreateInstance(type)!
            );

            var node = JsonNode.Parse(JsonSerializer.Serialize(instance, WireOptions))!.AsObject();
            Assert.AreEqual(name, node["type"]!.GetValue<string>(), type.Name);
            Assert.IsFalse(node.ContainsKey("installationId"));
            Assert.AreEqual(
                type,
                JsonSerializer.Deserialize<AnalyticsRequest>(node.ToJsonString(), WireOptions)!.GetType()
            );
        }
    }
}
