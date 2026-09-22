namespace GoBd.Reader.Data;

/// <summary>
/// A read-only pass-through that tallies the bytes handed on.
/// </summary>
/// <remarks>
/// Progress is measured in bytes read because that is the only figure an export declares before
/// it is read: the entry's length, which the listing already knows and the space check already
/// totals. Records cannot serve — nobody knows how many a file holds until it has been read —
/// and a spinner tells a person watching a multi-gigabyte import nothing at all.
/// <para>
/// The count has to come from a wrapper rather than from the stream's own position. A ZIP
/// member's stream is not seekable and its <see cref="Stream.Position"/> throws, and the
/// uncompressed length the listing reports is exactly the number of bytes that stream yields, so
/// counting what is handed on measures folder and archive entries identically.
/// </para>
/// </remarks>
internal sealed class CountingStream(Stream inner, Action<long> read) : Stream
{
    private long total;

    /// <summary>Bytes handed on so far.</summary>
    public long Total => total;

    /// <inheritdoc />
    public override bool CanRead => inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => total;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        Count(inner.Read(buffer, offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

    /// <inheritdoc />
    public override int ReadByte()
    {
        var value = inner.ReadByte();
        if (value >= 0)
        {
            Count(1);
        }

        return value;
    }

    private int Count(int bytes)
    {
        if (bytes > 0)
        {
            total += bytes;
            read(total);
        }

        return bytes;
    }

    /// <inheritdoc />
    public override void Flush() => inner.Flush();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
