using StabilityMatrix.Avalonia.Helpers;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class FuzzySearchHelperTests
{
    [TestMethod]
    public void Match_SubstringHit_IsMatchRegardlessOfScore()
    {
        var result = FuzzySearchHelper.Match("xl", "Juggernaut XL v9 sdxl checkpoint");

        Assert.IsTrue(result.ContainsQuery);
        Assert.IsTrue(result.IsMatch);
    }

    [TestMethod]
    public void Match_SubstringHit_IsCaseInsensitive()
    {
        var result = FuzzySearchHelper.Match("JUGGERNAUT", "juggernaut xl v9");

        Assert.IsTrue(result.ContainsQuery);
        Assert.IsTrue(result.IsMatch);
    }

    [TestMethod]
    public void Match_CloseMisspelling_IsAcceptedByFuzzyScore()
    {
        var result = FuzzySearchHelper.Match("jugernaut", "Juggernaut XL v9");

        Assert.IsFalse(result.ContainsQuery);
        Assert.IsTrue(result.Score >= FuzzySearchHelper.ScoreThreshold);
        Assert.IsTrue(result.IsMatch);
    }

    [TestMethod]
    public void Match_UnrelatedQuery_IsRejected()
    {
        var result = FuzzySearchHelper.Match("qwzxvbnm", "Juggernaut XL v9");

        Assert.IsFalse(result.ContainsQuery);
        Assert.IsFalse(result.IsMatch);
    }

    [TestMethod]
    public void IsMatch_MatchesMatchResult()
    {
        Assert.IsTrue(FuzzySearchHelper.IsMatch("flux", "flux1-dev-fp8"));
        Assert.IsFalse(FuzzySearchHelper.IsMatch("qwzxvbnm", "flux1-dev-fp8"));
    }

    [TestMethod]
    public void Match_ScoreIsComputedForSubstringHits()
    {
        // Ranking consumers order substring hits by score, so score must be populated
        // even when ContainsQuery is true.
        var exact = FuzzySearchHelper.Match("flux1-dev-fp8", "flux1-dev-fp8");

        Assert.IsTrue(exact.ContainsQuery);
        Assert.AreEqual(100, exact.Score);
    }
}
