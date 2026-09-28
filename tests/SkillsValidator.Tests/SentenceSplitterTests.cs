using SkillsValidator.Engine.Implementations.Realtime;

namespace SkillsValidator.Tests;

public class SentenceSplitterTests
{
    [Fact]
    public void Returns_sentences_as_soon_as_they_are_complete()
    {
        var splitter = new SentenceSplitter();

        Assert.Empty(splitter.Append("Hello Ana"));
        Assert.Equal(["Hello Ana."], splitter.Append(". What is "));
        Assert.Equal(["What is the Force?", "Tell me!"], splitter.Append("the Force? Tell me! And"));
        Assert.Equal("And", splitter.Flush());
        Assert.Null(splitter.Flush());
    }

    [Fact]
    public void Does_not_split_inside_numbers()
    {
        var splitter = new SentenceSplitter();

        Assert.Empty(splitter.Append("It came in .NET 8.0 and"));
        Assert.Equal("It came in .NET 8.0 and", splitter.Flush());
    }
}
