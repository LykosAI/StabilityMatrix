using System.Text.Json;
using System.Text.Json.Nodes;
using StabilityMatrix.Core.Models.Api.Lykos.Analytics;
using StabilityMatrix.Core.Models.Settings;

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
        AnalyticsRequest request = new HardwareAnalyticsRequest
        {
            InstallationId = Guid.Parse("3f1c9b2e-7a44-4d8b-9c1e-5e2f8a6b0d21"),
            Gpus = [new GpuAnalyticsInfo { Name = "NVIDIA GeForce RTX 4090", MemoryGb = 24 }],
            TotalMemoryGb = 64,
        };

        var json = JsonSerializer.Serialize(request, WireOptions);
        var node = JsonNode.Parse(json)!.AsObject();

        Assert.AreEqual("type", node.First().Key);
        Assert.AreEqual("hardware", node["type"]!.GetValue<string>());
        Assert.AreEqual("3f1c9b2e-7a44-4d8b-9c1e-5e2f8a6b0d21", node["installationId"]!.GetValue<string>());
        Assert.AreEqual(24, node["gpus"]![0]!["memoryGb"]!.GetValue<int>());
        Assert.AreEqual(64, node["totalMemoryGb"]!.GetValue<int>());
    }

    [TestMethod]
    public void EveryRequestType_HasItsWireName()
    {
        var expected = new Dictionary<Type, string>
        {
            [typeof(LaunchAnalyticsRequest)] = "launch",
            [typeof(PackageInstallAnalyticsRequest)] = "package-install",
            [typeof(FirstTimeInstallAnalytics)] = "first-time-install",
            [typeof(HardwareAnalyticsRequest)] = "hardware",
            [typeof(PackageUsageAnalyticsRequest)] = "package-usage",
            [typeof(InferenceUsageAnalyticsRequest)] = "inference-usage",
            [typeof(FeatureUsageAnalyticsRequest)] = "feature-usage",
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
        }
    }

    [TestMethod]
    public void AnalyticsSettings_CategoryDictionary_RoundTrips()
    {
        var settings = new AnalyticsSettings
        {
            IsUsageDataEnabled = true,
            Categories = { [AnalyticsCategory.Hardware] = true, [AnalyticsCategory.FeatureUsage] = false },
            HardwareDetailLevel = HardwareDetailLevel.Minimal,
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AnalyticsSettings>(json)!;

        Assert.IsTrue(restored.IsCategoryEnabled(AnalyticsCategory.Hardware));
        Assert.IsFalse(restored.IsCategoryEnabled(AnalyticsCategory.FeatureUsage));
        Assert.IsFalse(restored.IsCategoryEnabled(AnalyticsCategory.PackageUsage));
        Assert.AreEqual(HardwareDetailLevel.Minimal, restored.HardwareDetailLevel);
        StringAssert.Contains(json, "\"Hardware\":true");
    }
}
