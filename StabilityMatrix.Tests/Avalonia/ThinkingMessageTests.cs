using StabilityMatrix.Avalonia.Models.BananaVision;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class ThinkingMessageTests
{
    [TestMethod]
    public void ThinkingPreview_CutInsideEmoji_DoesNotSplitTheSurrogatePair()
    {
        var thinking = new ThinkingMessage(new string('a', 99) + "\U0001F97A" + new string('b', 20));

        Assert.AreEqual(new string('a', 99) + "...", thinking.ThinkingPreview);
    }
}
