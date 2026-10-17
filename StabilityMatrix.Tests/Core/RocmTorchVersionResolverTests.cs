using StabilityMatrix.Core.Services.Rocm;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class RocmTorchVersionResolverTests
{
    [DataTestMethod]
    [DataRow("0.29.0a0+rocm10.1.0", "0.29.0a0")]
    [DataRow("2.14.0+rocm10.1.0", "2.14.0")]
    [DataRow("2.11.0.3+rocm10.1.0", "2.11.0.3")]
    [DataRow("2.15.0a0+rocm10.2.0a20260917", "2.15.0a0")]
    [DataRow("2.14.0", "2.14.0")]
    public void StripLocalVersion_RemovesLocalLabel(string input, string expected)
    {
        Assert.AreEqual(expected, RocmTorchVersionResolver.StripLocalVersion(input));
    }

    [TestMethod]
    public void StripLocalVersion_ReturnsEmpty_ForNullOrWhitespace()
    {
        Assert.AreEqual(string.Empty, RocmTorchVersionResolver.StripLocalVersion(null));
        Assert.AreEqual(string.Empty, RocmTorchVersionResolver.StripLocalVersion("   "));
    }

    [DataTestMethod]
    [DataRow("2.14.0+rocm10.1.0", 2, 14)]
    [DataRow("0.29.0a0+rocm10.1.0", 0, 29)]
    [DataRow("2.11.0.3+rocm10.1.0", 2, 11)]
    [DataRow("2.15.0a0+rocm10.2.0a20260917", 2, 15)]
    [DataRow("0.30.0", 0, 30)]
    public void TryGetReleasePair_ReturnsMajorMinor(string input, int major, int minor)
    {
        var pair = RocmTorchVersionResolver.TryGetReleasePair(input);

        Assert.IsNotNull(pair);
        Assert.AreEqual(major, pair.Value.Major);
        Assert.AreEqual(minor, pair.Value.Minor);
    }

    [DataTestMethod]
    [DataRow("not-a-version")]
    [DataRow("")]
    [DataRow("1")]
    public void TryGetReleasePair_ReturnsNull_ForUnparsableInput(string input)
    {
        Assert.IsNull(RocmTorchVersionResolver.TryGetReleasePair(input));
    }

    [TestMethod]
    public void TryGetReleasePair_ReturnsNull_ForNull()
    {
        Assert.IsNull(RocmTorchVersionResolver.TryGetReleasePair(null));
    }

    [DataTestMethod]
    [DataRow("2.11.0", 0, 26)]
    [DataRow("2.13.0+rocm10.1.0", 0, 28)]
    [DataRow("2.14.0+rocm10.1.0", 0, 29)]
    [DataRow("2.15.0a0+rocm10.2.0a20261006", 0, 30)]
    public void ExpectedTorchvision_MapsTorchMinor(string torch, int major, int minor)
    {
        var expected = RocmTorchVersionResolver.ExpectedTorchvision(torch);

        Assert.IsNotNull(expected);
        Assert.AreEqual(major, expected.Value.Major);
        Assert.AreEqual(minor, expected.Value.Minor);
    }

    [TestMethod]
    public void ExpectedTorchvision_ReturnsNull_ForUnknownPairings()
    {
        Assert.IsNull(RocmTorchVersionResolver.ExpectedTorchvision("3.0.0"));
        Assert.IsNull(RocmTorchVersionResolver.ExpectedTorchvision(null));
        Assert.IsNull(RocmTorchVersionResolver.ExpectedTorchvision("not-a-version"));
    }

    [TestMethod]
    public void IsDevelopmentRelease_DetectsDevSegments()
    {
        Assert.IsTrue(RocmTorchVersionResolver.IsDevelopmentRelease("2.15.0.dev20261006"));
        Assert.IsTrue(RocmTorchVersionResolver.IsDevelopmentRelease("2.15.0.dev20261006+rocm10.2.0"));
        Assert.IsFalse(RocmTorchVersionResolver.IsDevelopmentRelease("0.29.0a0+rocm10.1.0"));
        Assert.IsFalse(RocmTorchVersionResolver.IsDevelopmentRelease("2.14.0+rocm10.1.0"));
    }

    [TestMethod]
    public void SelectHighestMatching_PrefersMatchedPreRelease_OverStaleFinal()
    {
        // pip lists available versions in descending order.
        string[] available = ["0.29.0a0+rocm10.1.0", "0.28.0+rocm10.1.0", "0.27.0+rocm10.1.0"];

        var selected = RocmTorchVersionResolver.SelectHighestMatching(available, (0, 29));

        Assert.AreEqual("0.29.0a0", selected);
    }

    [TestMethod]
    public void SelectHighestMatching_IgnoresDevelopmentReleases()
    {
        string[] available = ["0.29.0.dev20261006", "0.29.0a0+rocm10.1.0"];

        var selected = RocmTorchVersionResolver.SelectHighestMatching(available, (0, 29));

        Assert.AreEqual("0.29.0a0", selected);
    }

    [TestMethod]
    public void SelectHighestMatching_ReturnsNull_WhenNoPairMatches()
    {
        string[] available = ["0.28.0+rocm10.1.0", "0.27.0+rocm10.1.0"];

        var selected = RocmTorchVersionResolver.SelectHighestMatching(available, (0, 29));

        Assert.IsNull(selected);
    }

    [TestMethod]
    public void SelectHighestMatching_ReturnsNull_ForNullInput()
    {
        Assert.IsNull(RocmTorchVersionResolver.SelectHighestMatching(null, (0, 29)));
    }

    [TestMethod]
    public void SelectHighestPair_PicksNewestPairedTorch()
    {
        string[] torch = ["2.14.0+rocm10.1.0", "2.13.0+rocm10.1.0"];
        string[] torchvision = ["0.29.0a0+rocm10.1.0", "0.28.0+rocm10.1.0"];

        var pair = RocmTorchVersionResolver.SelectHighestPair(torch, torchvision);

        Assert.IsNotNull(pair);
        Assert.AreEqual("2.14.0", pair.Value.Torch);
        Assert.AreEqual("0.29.0a0", pair.Value.Torchvision);
    }

    [TestMethod]
    public void SelectHighestPair_SkipsUnpairedNewestTorch()
    {
        // torch 2.15 exists but no torchvision 0.30 -> walk down to 2.14 / 0.29.0a0
        string[] torch = ["2.15.0+rocm10.2.0", "2.14.0+rocm10.1.0", "2.13.0+rocm10.1.0"];
        string[] torchvision = ["0.29.0a0+rocm10.1.0", "0.28.0+rocm10.1.0"];

        var pair = RocmTorchVersionResolver.SelectHighestPair(torch, torchvision);

        Assert.IsNotNull(pair);
        Assert.AreEqual("2.14.0", pair.Value.Torch);
        Assert.AreEqual("0.29.0a0", pair.Value.Torchvision);
    }

    [TestMethod]
    public void SelectHighestPair_DedupesLocalLabelVariants()
    {
        string[] torch = ["2.14.0+rocm10.1.0", "2.14.0+rocm10.0.0", "2.13.0+rocm10.0.0"];
        string[] torchvision = ["0.29.0a0+rocm10.1.0"];

        var pair = RocmTorchVersionResolver.SelectHighestPair(torch, torchvision);

        Assert.IsNotNull(pair);
        Assert.AreEqual("2.14.0", pair.Value.Torch);
    }

    [TestMethod]
    public void SelectHighestPair_ReturnsNull_WhenNothingPairs()
    {
        string[] torch = ["2.15.0+rocm10.2.0"];
        string[] torchvision = ["0.28.0+rocm10.1.0"];

        Assert.IsNull(RocmTorchVersionResolver.SelectHighestPair(torch, torchvision));
    }

    [TestMethod]
    public void SelectHighestPair_ReturnsNull_ForNullInputs()
    {
        Assert.IsNull(RocmTorchVersionResolver.SelectHighestPair(null, ["0.29.0a0"]));
        Assert.IsNull(RocmTorchVersionResolver.SelectHighestPair(["2.14.0"], null));
    }

    [TestMethod]
    public void DistinctByBaseVersion_RemovesLocalLabelDuplicates_PreservingOrder()
    {
        string[] versions = ["2.14.0+rocm10.1.0", "2.14.0+rocm10.0.0", "2.13.0+rocm10.0.0"];

        var distinct = RocmTorchVersionResolver.DistinctByBaseVersion(versions).ToList();

        CollectionAssert.AreEqual(new[] { "2.14.0+rocm10.1.0", "2.13.0+rocm10.0.0" }, distinct);
    }

    [TestMethod]
    public void GetLocalVersion_ReturnsLocalLabelOrEmpty()
    {
        Assert.AreEqual(
            "+rocm10.2.0a20261007",
            RocmTorchVersionResolver.GetLocalVersion("2.15.0a0+rocm10.2.0a20261007")
        );
        Assert.AreEqual(string.Empty, RocmTorchVersionResolver.GetLocalVersion("2.14.0"));
        Assert.AreEqual(string.Empty, RocmTorchVersionResolver.GetLocalVersion(null));
    }

    [TestMethod]
    public void SelectHighestPairBySnapshot_PicksNewestWhenDatesAlign()
    {
        string[] torch = ["2.15.0a0+rocm10.2.0a20261007", "2.15.0a0+rocm10.2.0a20261006"];
        string[] torchvision = ["0.30.0a0+rocm10.2.0a20261007", "0.30.0a0+rocm10.2.0a20261006"];

        var pair = RocmTorchVersionResolver.SelectHighestPairBySnapshot(torch, torchvision);

        Assert.IsNotNull(pair);
        Assert.AreEqual("2.15.0a0+rocm10.2.0a20261007", pair.Value.Torch);
        Assert.AreEqual("0.30.0a0+rocm10.2.0a20261007", pair.Value.Torchvision);
    }

    [TestMethod]
    public void SelectHighestPairBySnapshot_UsesNewestSharedDate_WhenNewestTorchIsUnpaired()
    {
        // torch 20261007 exists but torchvision only has 20261006 -> both pin to 20261006.
        string[] torch = ["2.15.0a0+rocm10.2.0a20261007", "2.15.0a0+rocm10.2.0a20261006"];
        string[] torchvision = ["0.30.0a0+rocm10.2.0a20261006", "0.29.0a0+rocm10.2.0a20261006"];

        var pair = RocmTorchVersionResolver.SelectHighestPairBySnapshot(torch, torchvision);

        Assert.IsNotNull(pair);
        Assert.AreEqual("2.15.0a0+rocm10.2.0a20261006", pair.Value.Torch);
        Assert.AreEqual("0.30.0a0+rocm10.2.0a20261006", pair.Value.Torchvision);
    }

    [TestMethod]
    public void SelectHighestPairBySnapshot_ReturnsNull_WhenNoSharedDate()
    {
        string[] torch = ["2.15.0a0+rocm10.2.0a20261007"];
        string[] torchvision = ["0.30.0a0+rocm10.2.0a20261006"];

        Assert.IsNull(RocmTorchVersionResolver.SelectHighestPairBySnapshot(torch, torchvision));
    }

    [TestMethod]
    public void SelectHighestPairBySnapshot_ReturnsNull_WhenTorchHasNoLocalLabel()
    {
        string[] torch = ["2.14.0"];
        string[] torchvision = ["0.28.0+rocm10.2.0a20261006"];

        Assert.IsNull(RocmTorchVersionResolver.SelectHighestPairBySnapshot(torch, torchvision));
    }

    [TestMethod]
    public void SelectHighestPairBySnapshot_ReturnsNull_ForNullInputs()
    {
        Assert.IsNull(
            RocmTorchVersionResolver.SelectHighestPairBySnapshot(null, ["0.30.0a0+rocm10.2.0a20261007"])
        );
        Assert.IsNull(
            RocmTorchVersionResolver.SelectHighestPairBySnapshot(["2.15.0a0+rocm10.2.0a20261007"], null)
        );
    }
}
