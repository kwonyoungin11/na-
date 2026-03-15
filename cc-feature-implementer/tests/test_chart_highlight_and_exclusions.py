from pathlib import Path


def test_rank_one_highlight_uses_dark_red_border():
    source = (
        Path(__file__).resolve().parents[1]
        / "desktop"
        / "NaraCollector.Desktop"
        / "Controls"
        / "NativeBidChartControl.cs"
    ).read_text(encoding="utf-8")

    assert 'CreatePen("#FACC15", 2.3)' in source
    assert "if (point.Rank == 1)" in source
    assert "dc.DrawRectangle(null, RankHighlightPen, rect);" in source


def test_excluded_company_clause_contains_only_hyorim():
    source = (
        Path(__file__).resolve().parents[1]
        / "desktop"
        / "NaraCollector.Desktop"
        / "Services"
        / "SqliteDashboardRepository.cs"
    ).read_text(encoding="utf-8")

    assert "NOT LIKE '%효림%'" in source
