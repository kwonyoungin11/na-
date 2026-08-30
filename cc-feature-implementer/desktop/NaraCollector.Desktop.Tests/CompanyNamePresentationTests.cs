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
    public void Canonicalize_Keeps_HangukHeritage_Separate_From_HangukJae()
    {
        var heritage = CompanyNamePresentation.Canonicalize("재단법인 한국문화유산연구원");
        var jae = CompanyNamePresentation.Canonicalize("재단법인 한국문화재연구원");

        Assert.NotEqual(jae, heritage);
        Assert.Equal("한국문화유산연구원", heritage);
        Assert.Equal("한국문화재연구원", jae);
        Assert.Equal("한울문화재연구원", CompanyNamePresentation.Canonicalize("재단법인 한울문화유산연구원"));
    }

    [Fact]
    public void BuildDisplayMap_Splits_Jeju_And_Hanguk_Family_Labels()
    {
        var jeju = CompanyNamePresentation.BuildDisplayMap(["제주문화재연구원", "제주고고학연구소"]);
        Assert.NotEqual(jeju["제주문화재연구원"], jeju["제주고고학연구소"]);

        var hanguk = CompanyNamePresentation.BuildDisplayMap(
        [
            "한국문화재연구원",
            "한국문화유산연구원",
            "한국문물연구원",
            "한국고고환경연구소",
            "한국선사문화연구원",
        ]);
        var labels = hanguk.Values.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(5, labels.Count);
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

    [Theory]
    [InlineData("한울문화재연구원", "한울")]
    [InlineData("동국문화재연구원", "동국")]
    [InlineData("가람문화재연구원", "가람")]
    [InlineData("화서문화재연구원", "화서")]
    public void BuildShortLabel_StaysInsideTwoHangul(string fullName, string expected)
    {
        var label = CompanyNamePresentation.BuildShortLabel(fullName);

        Assert.Equal(expected, label);
        Assert.InRange(label.Length, 1, 2);
        Assert.DoesNotContain("연구원", label, StringComparison.Ordinal);
    }
}
