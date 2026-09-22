using System.Collections.Immutable;
using GoBd.Validation.Collections;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Collections;

/// <summary>
/// The reason this type exists: ImmutableArray compares by underlying reference, so a record
/// holding one gets broken value equality from its own generated members.
/// </summary>
public sealed class EquatableArrayTests
{
    [Fact]
    public void ImmutableArrayComparesByReferenceWhichIsWhyThisTypeExists()
    {
        var left = ImmutableArray.Create("x", "y");
        var right = ImmutableArray.Create("x", "y");

        EqualityComparer<ImmutableArray<string>>.Default.Equals(left, right).ShouldBeFalse();
        EqualityComparer<EquatableArray<string>>.Default.Equals(new(left), new(right)).ShouldBeTrue();
    }

    [Fact]
    public void EqualContentsAreEqual()
    {
        var left = new EquatableArray<string>(["a", "b"]);
        var right = new EquatableArray<string>(["a", "b"]);

        left.ShouldBe(right);
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void DifferentContentsAreNotEqual() =>
        new EquatableArray<string>(["a"]).ShouldNotBe(new EquatableArray<string>(["b"]));

    [Fact]
    public void OrderIsSignificant() =>
        new EquatableArray<string>(["a", "b"]).ShouldNotBe(new EquatableArray<string>(["b", "a"]));

    [Fact]
    public void DefaultInstanceBehavesAsEmpty()
    {
        default(EquatableArray<string>).Count.ShouldBe(0);
        default(EquatableArray<string>).ShouldBe(EquatableArray<string>.Empty);
        default(EquatableArray<string>).AsImmutableArray().ShouldBeEmpty();
    }

    [Fact]
    public void EnumeratesInOrder() =>
        new EquatableArray<string>(["a", "b", "c"]).ToArray().ShouldBe(["a", "b", "c"]);

    [Fact]
    public void IndexerReadsElements() => new EquatableArray<string>(["a", "b"])[1].ShouldBe("b");
}
