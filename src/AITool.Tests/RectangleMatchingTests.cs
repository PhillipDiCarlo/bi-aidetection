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
