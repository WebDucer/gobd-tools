using System.Collections;
using System.Collections.Immutable;

namespace GoBd.Validation.Collections;

/// <summary>
/// An immutable sequence whose equality is element-wise, so that a record containing one gets
/// correct value equality from its compiler-generated members.
/// </summary>
/// <remarks>
/// <see cref="ImmutableArray{T}"/> deliberately implements <see cref="IEquatable{T}"/> as
/// reference comparison of the underlying array. A record compares each member through
/// <c>EqualityComparer&lt;T&gt;.Default</c>, so a record holding an <see cref="ImmutableArray{T}"/>
/// inherits that reference semantics and two instances with identical contents compare unequal.
/// Wrapping the array in a type whose own equality is element-wise fixes the cause rather than
/// the symptom, and keeps the record's generated <c>Equals</c> and <c>GetHashCode</c> authoritative.
/// </remarks>
/// <typeparam name="T">Element type.</typeparam>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> items;

    /// <summary>Wraps an existing immutable array.</summary>
    public EquatableArray(ImmutableArray<T> items) => this.items = items;

    /// <summary>Copies a sequence into a new instance.</summary>
    public EquatableArray(IEnumerable<T> items) => this.items = [.. items];

    /// <summary>An empty sequence.</summary>
    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    /// <summary>The elements, as an immutable array.</summary>
    public ImmutableArray<T> AsImmutableArray() => items.IsDefault ? ImmutableArray<T>.Empty : items;

    /// <inheritdoc />
    public int Count => items.IsDefault ? 0 : items.Length;

    /// <inheritdoc />
    public T this[int index] => AsImmutableArray()[index];

    /// <inheritdoc />
    public bool Equals(EquatableArray<T> other) =>
        AsImmutableArray().AsSpan().SequenceEqual(other.AsImmutableArray().AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in AsImmutableArray())
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    /// <summary>Element-wise equality.</summary>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    /// <summary>Element-wise inequality.</summary>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    /// <summary>Wraps an immutable array.</summary>
    public static implicit operator EquatableArray<T>(ImmutableArray<T> items) => new(items);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
