using System.Text;
using System.Text.Json;
using StabilityMatrix.Core.Models.Settings;
using StabilityMatrix.Core.Services;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class SettingsJsonSanitizerTests
{
    [TestMethod]
    public void SanitizeBytes_RemovesNullBytes()
    {
        var input = Encoding.UTF8.GetBytes("{\"key\": 123}");
        // Insert null bytes
        var corrupted = new byte[input.Length + 3];
        Array.Copy(input, 0, corrupted, 0, 5);
        corrupted[5] = 0x00;
        corrupted[6] = 0x00;
        corrupted[7] = 0x00;
        Array.Copy(input, 5, corrupted, 8, input.Length - 5);

        var result = SettingsJsonSanitizer.SanitizeBytes(corrupted);
        var resultText = Encoding.UTF8.GetString(result);

        Assert.AreEqual("{\"key\": 123}", resultText);
    }

    [TestMethod]
    public void SanitizeBytes_CleanInput_ReturnsSameArray()
    {
        var input = Encoding.UTF8.GetBytes("{\"key\": 123}");

        var result = SettingsJsonSanitizer.SanitizeBytes(input);

        // Should return the same reference (no copy) for clean input
        Assert.AreSame(input, result);
    }

    [TestMethod]
    public void TryFixBraces_MissingClosingBrace_AppendsBrace()
    {
        var input = """{"key": "value" """;

        var result = SettingsJsonSanitizer.TryFixBraces(input);

        // Should be valid JSON now
        Assert.IsNotNull(JsonDocument.Parse(result));
    }

    [TestMethod]
    public void TryFixBraces_ValidJson_ReturnsUnchanged()
    {
        var input = """{"key": "value"}""";

        var result = SettingsJsonSanitizer.TryFixBraces(input);

        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void TryFixBraces_NestedMissingBraces_AppendsMultiple()
    {
        var input = """{"outer": {"inner": "value" """;

        var result = SettingsJsonSanitizer.TryFixBraces(input);

        Assert.IsNotNull(JsonDocument.Parse(result));
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_ValidJson_ReturnsSettings()
    {
        var json = """
        {
            "Version": 1,
            "Theme": "Dark",
            "InferenceDimensionStepChange": 64
        }
        """;

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Version);
        Assert.AreEqual("Dark", result.Theme);
        Assert.AreEqual(64, result.InferenceDimensionStepChange);
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_NullBytesInNumber_Recovers()
    {
        // Simulate the exact issue from #1590: null byte in InferenceDimensionStepChange value
        var json = "{\n  \"Version\": 1,\n  \"InferenceDimensionStepChange\": 12\08\n}";

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Version);
        // The value 128 is recovered after stripping the null byte
        Assert.AreEqual(128, result.InferenceDimensionStepChange);
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_NullBytesScattered_Recovers()
    {
        var json = "{\n  \"Theme\": \"Da\0rk\",\n  \"CheckForUpdates\": tr\0ue\n}";

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNotNull(result);
        Assert.AreEqual("Dark", result.Theme);
        Assert.AreEqual(true, result.CheckForUpdates);
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_TruncatedJson_RecoversSalvageableProperties()
    {
        var json = """
        {
            "Version": 1,
            "Theme": "Dark",
            "InferenceDimensionStepChange": 64,
            "CheckForUpdates": true,
            "ConsoleFontSize":
        """;

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Version);
        Assert.AreEqual("Dark", result.Theme);
        Assert.AreEqual(64, result.InferenceDimensionStepChange);
        Assert.AreEqual(true, result.CheckForUpdates);
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_TotallyCorrupt_ReturnsNull()
    {
        var json = "not json at all !@#$%^&*()";

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_EmptyObject_ReturnsSettingsWithDefaults()
    {
        var json = "{}";

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNotNull(result);
        // Should have default values
        Assert.AreEqual(128, result.InferenceDimensionStepChange);
        Assert.AreEqual(14, result.ConsoleFontSize);
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_PreservesValidProperties_WhenOthersCorrupt()
    {
        // JSON where Theme is valid but we have an invalid property type
        var json = """
        {
            "Version": 1,
            "Theme": "Dark",
            "FirstLaunchSetupComplete": true,
            "InferenceDimensionStepChange": 64
        }
        """;

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNotNull(result);
        Assert.AreEqual("Dark", result.Theme);
        Assert.AreEqual(true, result.FirstLaunchSetupComplete);
        Assert.AreEqual(64, result.InferenceDimensionStepChange);
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_MissingClosingBrace_Recovers()
    {
        var json = """
        {
            "Version": 1,
            "Theme": "Dark"
        """;

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Version);
        Assert.AreEqual("Dark", result.Theme);
    }

    [TestMethod]
    public void TryDeserializeWithRecovery_TrailingComma_Recovers()
    {
        var json = """
        {
            "Version": 1,
            "Theme": "Dark",
        }
        """;

        var result = SettingsJsonSanitizer.TryDeserializeWithRecovery(json);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Version);
        Assert.AreEqual("Dark", result.Theme);
    }
}
