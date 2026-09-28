using System.Text;

namespace SkillsValidator.Engine.Implementations.Realtime;

/// <summary>
/// Collects streamed text fragments and returns complete sentences, so each sentence
/// can be spoken while the agent is still generating the next one.
/// </summary>
public sealed class SentenceSplitter
{
    private readonly StringBuilder buffer = new();

    /// <summary>Adds a fragment and returns the sentences it completed.</summary>
    public IReadOnlyList<string> Append(string fragment)
    {
        buffer.Append(fragment);

        var sentences = new List<string>();
        var start = 0;
        var text = buffer.ToString();

        for (var i = 0; i < text.Length - 1; i++)
        {
            // A sentence ends with . ? ! followed by whitespace.
            if (text[i] is '.' or '?' or '!' && char.IsWhiteSpace(text[i + 1]))
            {
                AddSentence(sentences, text[start..(i + 1)]);
                start = i + 1;
            }
        }

        buffer.Remove(0, start);
        return sentences;
    }

    /// <summary>Returns the remaining text at the end of the stream, if any.</summary>
    public string? Flush()
    {
        var rest = buffer.ToString().Trim();
        buffer.Clear();
        return rest.Length > 0 ? rest : null;
    }

    private static void AddSentence(List<string> sentences, string sentence)
    {
        sentence = sentence.Trim();
        if (sentence.Length > 0)
            sentences.Add(sentence);
    }
}
