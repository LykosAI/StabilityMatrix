using StabilityMatrix.Avalonia.Controls;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class MarkdownViewerTests
{
    [TestMethod]
    public void EncodeAstralCharacters_Emoji_BecomesOneCharacterReference()
    {
        var html = MarkdownViewer.EncodeAstralCharacters("<p>so sad \U0001F97A its perfect</p>");

        Assert.AreEqual("<p>so sad &#x1F97A; its perfect</p>", html);
    }

    [TestMethod]
    public void EncodeAstralCharacters_ZwjSequence_KeepsJoinersBetweenReferences()
    {
        var html = MarkdownViewer.EncodeAstralCharacters("\U0001F43A\u200D\U0001F525");

        Assert.AreEqual("&#x1F43A;\u200D&#x1F525;", html);
    }

    [TestMethod]
    public void EncodeAstralCharacters_BmpOnly_ReturnsSameInstance()
    {
        const string html = "<p>héllo 狼 ;3</p>";

        Assert.AreSame(html, MarkdownViewer.EncodeAstralCharacters(html));
    }

    [TestMethod]
    public void EncodeAstralCharacters_LoneSurrogate_BecomesReplacementCharacter()
    {
        Assert.AreEqual("a\uFFFDb", MarkdownViewer.EncodeAstralCharacters("a\uD83Eb"));
    }
}
