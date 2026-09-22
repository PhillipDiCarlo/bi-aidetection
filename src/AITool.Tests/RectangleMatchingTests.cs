using System.Drawing;
using AITool;
using Xunit;

namespace AITool.Tests;

public class RectangleMatchingTests
{
    [Fact]
    public void IntersectPercent_IdenticalRectanglesAre100Percent()
    {
        var r = new Rectangle(10, 10, 100, 50);
        Assert.Equal(100, r.IntersectPercent(r), 5);
    }

    [Fact]
    public void IntersectPercent_DisjointRectanglesAreZero()
    {
        var a = new Rectangle(0, 0, 10, 10);
        var b = new Rectangle(100, 100, 10, 10);
        Assert.Equal(0, a.IntersectPercent(b), 5);
    }

    [Fact]
    public void IntersectPercent_HalfOverlapOfEqualRectanglesIs50Percent()
    {
        var a = new Rectangle(0, 0, 100, 100);
        var b = new Rectangle(50, 0, 100, 100);
        Assert.Equal(50, a.IntersectPercent(b), 5);
    }

    [Fact]
    public void RectangleMatches_UsesThresholdWhenNotPartialMode()
    {
        var a = new Rectangle(0, 0, 100, 100);
        var b = new Rectangle(10, 10, 100, 100);

        Assert.True(AITOOL.RectangleMatches(a, b, 50, out double pct, false));
        Assert.InRange(pct, 50, 100);
        Assert.False(AITOOL.RectangleMatches(a, b, 99, out _, false));
    }

    [Fact]
    public void RectangleMatches_PartialModeAcceptsFaceInsidePerson()
    {
        var person = new Rectangle(0, 0, 100, 300);
        var face = new Rectangle(30, 10, 40, 40);

        Assert.True(AITOOL.RectangleMatches(person, face, 0, out double pct, true));
        Assert.InRange(pct, 5, 95);
    }

    [Fact]
    public void RectangleMatches_PartialModeRejectsDisjoint()
    {
        var a = new Rectangle(0, 0, 10, 10);
        var b = new Rectangle(500, 500, 10, 10);
        Assert.False(AITOOL.RectangleMatches(a, b, 0, out _, true));
    }
}

public class IntersectPercentRegressionTests
{
    [Fact]
    public void IntersectPercent_IsSymmetricForDifferentWidths()
    {
        // Before the fix the denominator used compareRect.Width * rect.Height, so the
        // result depended on which rectangle was "first" whenever widths differed.
        var narrow = new Rectangle(0, 0, 50, 100);
        var wide = new Rectangle(0, 0, 100, 100);

        // narrow is fully inside wide: 2*5000 / (5000 + 10000) = 66.67%
        Assert.Equal(66.6667, narrow.IntersectPercent(wide), 3);
        Assert.Equal(narrow.IntersectPercent(wide), wide.IntersectPercent(narrow), 6);
    }

    [Fact]
    public void IntersectPercent_ZeroAreaRectanglesDoNotDivideByZero()
    {
        var empty = new Rectangle(0, 0, 0, 0);
        Assert.Equal(0, empty.IntersectPercent(empty));
    }
}
