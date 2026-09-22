using AITool;
using Xunit;

namespace AITool.Tests;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("Hello World", "world", true)]
    [InlineData("Hello World", "xyz", false)]
    [InlineData("", "a", false)]
    [InlineData("abc", "", false)]
    [InlineData(null, "a", false)]
    public void Has_IsCaseInsensitiveAndNullSafe(string value, string find, bool expected)
    {
        Assert.Equal(expected, value.Has(find));
    }

    [Fact]
    public void SplitStr_TrimsAndRemovesEmptyEntries()
    {
        var parts = " person , , car|truck ;bus".SplitStr(",;|");
        Assert.Equal(new[] { "person", "car", "truck", "bus" }, parts);
    }

    [Fact]
    public void SplitStr_CanLowercase()
    {
        var parts = "Person,CAR".SplitStr(",", ToLower: true);
        Assert.Equal(new[] { "person", "car" }, parts);
    }

    [Fact]
    public void GetWord_ExtractsTextBetweenMarkers()
    {
        string json = "{\"Error\":\"Exception when forwarding request\",\"processedBy\":\"CDODGE8\",\"timestampUTC\":\"x\"}";
        Assert.Equal("CDODGE8", json.GetWord("\"processedBy\":\"", "\","));
    }

    [Fact]
    public void GetWord_ReturnsEmptyWhenMarkerMissing()
    {
        Assert.Equal("", "no markers here".GetWord("[", "]"));
    }
}
