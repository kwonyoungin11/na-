from pathlib import Path


def test_rank_one_highlight_uses_dark_red_border():
    source = (
        Path(__file__).resolve().parents[1]
        / "desktop"
        / "NaraCollector.Desktop"
        / "Controls"
        / "NativeBidChartControl.cs"
    ).read_text(encoding="utf-8")

    assert 'CreatePen("#B91C1C", 2.2)' in source
    assert "if (point.Rank == 1)" in source
    assert "dc.DrawRectangle(null, RankHighlightPen, rect);" in source


def test_excluded_company_clause_contains_hyorim_and_korean_institute():
    source = (
        Path(__file__).resolve().parents[1]
        / "desktop"
        / "NaraCollector.Desktop"
        / "Services"
        / "SqliteDashboardRepository.cs"
    ).read_text(encoding="utf-8")

    assert "NOT LIKE '%한국문화재연구원%'" in source
    assert "NOT LIKE '%효림%'" in source
