using AITool;
using Xunit;

namespace AITool.Tests;

public class ListMatchingTests
{
    [Theory]
    [InlineData("person", "person, car, truck", true)]
    [InlineData("PERSON", "person, car, truck", true)]
    [InlineData("dog", "person, car, truck", false)]
    [InlineData("dog", "*", true)]
    [InlineData("dog", "", true)]          // empty search list matches everything by default
    [InlineData("car, dog", "dog|cat", true)] // any item in the find list matching is enough
    public void IsInList_MatchesCaseInsensitivelyWithWildcardAndEmptyList(string find, string list, bool expected)
    {
        Assert.Equal(expected, Global.IsInList(find, list));
    }

    [Fact]
    public void IsInList_EmptySearchListCanBeTreatedAsNoMatch()
    {
        Assert.False(Global.IsInList("dog", "", TrueIfEmpty: false));
    }
}
