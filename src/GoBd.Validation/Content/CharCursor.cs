namespace GoBd.Validation.Content;

/// <summary>
/// A forward-only cursor over decoded text with enough lookahead to recognise a declared
/// delimiter.
/// </summary>
/// <remarks>
/// The delimiters and the text encapsulator are declared as text, not as single characters, so
/// recognising one needs lookahead of the longest of them. A file may be gigabytes, so the text
/// is never held whole: the buffer holds a window, compacts it when it runs low, and refills.
/// </remarks>
internal sealed class CharCursor
{
    private readonly TextReader reader;
    private char[] buffer;
    private int start;
    private int length;
    private bool exhausted;

    internal CharCursor(TextReader reader, int lookahead)
    {
        this.reader = reader;
        // The buffer must comfortably exceed the longest lookahead, or compaction would spin
        // without ever making a match decidable.
        buffer = new char[Math.Max(8192, lookahead * 4)];
    }

    /// <summary>True when the reader has produced everything it will and the window is empty.</summary>
    internal bool EndOfInput => length == 0 && !Fill(1);

    /// <summary>The character at the given offset ahead of the cursor, if there is one.</summary>
    internal bool TryPeek(int offset, out char value)
    {
        if (offset >= length && !Fill(offset + 1))
        {
            value = '\0';
            return false;
        }

        value = buffer[start + offset];
        return true;
    }

    /// <summary>True when the text at the cursor begins with the given sequence.</summary>
    internal bool StartsWith(string text)
    {
        if (text.Length == 0 || (length < text.Length && !Fill(text.Length)))
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (buffer[start + index] != text[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads one character, which the caller has established is present.</summary>
    internal char Read()
    {
        if (length == 0 && !Fill(1))
        {
            throw new InvalidOperationException("Read past the end of the input.");
        }

        var value = buffer[start];
        start++;
        length--;
        return value;
    }

    /// <summary>Discards the given number of characters, which the caller has established are present.</summary>
    internal void Skip(int count)
    {
        for (var index = 0; index < count; index++)
        {
            Read();
        }
    }

    private bool Fill(int wanted)
    {
        while (length < wanted)
        {
            if (exhausted)
            {
                return false;
            }

            if (start + length + 1 > buffer.Length)
            {
                Compact(wanted);
            }

            var read = reader.Read(buffer, start + length, buffer.Length - start - length);
            if (read <= 0)
            {
                exhausted = true;
                return length >= wanted;
            }

            length += read;
        }

        return true;
    }

    private void Compact(int wanted)
    {
        if (wanted > buffer.Length)
        {
            var grown = new char[Math.Max(wanted * 2, buffer.Length * 2)];
            Array.Copy(buffer, start, grown, 0, length);
            buffer = grown;
        }
        else
        {
            Array.Copy(buffer, start, buffer, 0, length);
        }

        start = 0;
    }
}
