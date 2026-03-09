using NaraCollector.Desktop.Services;
using Xunit;

namespace NaraCollector.Desktop.Tests;

public sealed class CompanyNamePresentationTests
{
    [Fact]
    public void Canonicalize_Removes_EmptyParentheses_And_FoundationMarkers()
    {
        var normalized = CompanyNamePresentation.Canonicalize("()(재)동아세아문화유산연구원");

        Assert.Equal("동아세아문화재연구원", normalized);
    }

    [Fact]
    public void BuildDisplayMap_Uses_TwoLetter_Label_When_Unique()
    {
        var map = CompanyNamePresentation.BuildDisplayMap(["동아세아문화재연구원", "가경문화재연구원"]);

        Assert.Equal("동아", map["동아세아문화재연구원"]);
        Assert.Equal("가경", map["가경문화재연구원"]);
    }

    [Fact]
    public void BuildDisplayMap_Uses_Compressed_Unique_Label_When_TwoLetter_Label_Conflicts()
    {
        var map = CompanyNamePresentation.BuildDisplayMap(["동아세아문화재연구원", "동아문화재연구원"]);

        Assert.Equal("동세", map["동아세아문화재연구원"]);
        Assert.Equal("동문", map["동아문화재연구원"]);
    }
}
