from __future__ import annotations

import importlib.util
from pathlib import Path

import pandas as pd


def _load_module():
    module_path = Path(__file__).resolve().parents[1] / "src" / "company_day_cluster.py"
    spec = importlib.util.spec_from_file_location("company_day_cluster", module_path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def test_collapse_same_company_day_clusters_merges_nearby_same_day_rows() -> None:
    module = _load_module()
    df = pd.DataFrame(
        [
            {"공고번호": "N1", "업체명": "가람문화재연구원", "기초대비사정률": 99.6310, "개찰일시": "2026-03-01 10:00"},
            {"공고번호": "N2", "업체명": "가람문화재연구원", "기초대비사정률": 99.6318, "개찰일시": "2026-03-01 11:00"},
            {"공고번호": "N3", "업체명": "가람문화재연구원", "기초대비사정률": 99.6322, "개찰일시": "2026-03-01 12:00"},
            {"공고번호": "N4", "업체명": "가람문화재연구원", "기초대비사정률": 99.6325, "개찰일시": "2026-03-01 13:00"},
            {"공고번호": "N5", "업체명": "가람문화재연구원", "기초대비사정률": 99.6500, "개찰일시": "2026-03-01 14:00"},
            {"공고번호": "N6", "업체명": "가람문화재연구원", "기초대비사정률": 99.6510, "개찰일시": "2026-03-01 15:00"},
            {"공고번호": "N7", "업체명": "가람문화재연구원", "기초대비사정률": 99.6311, "개찰일시": "2026-03-02 10:00"},
            {"공고번호": "N8", "업체명": "나루문화재연구원", "기초대비사정률": 99.7001, "개찰일시": "2026-03-01 10:00"},
            {"공고번호": "N9", "업체명": "나루문화재연구원", "기초대비사정률": 99.7002, "개찰일시": "2026-03-01 11:00"},
        ]
    )

    collapsed, stats = module.collapse_same_company_day_clusters(df, rate_tolerance=0.003, min_cluster_size=2)

    assert stats["input_rows"] == 9
    assert stats["output_rows"] == 4
    assert stats["reduced_rows"] == 5
    assert stats["collapsed_cluster_count"] == 3

    first_cluster = collapsed[(collapsed["업체명"] == "가람문화재연구원") & (collapsed["공고번호"] == "N1")].iloc[0]
    assert round(float(first_cluster["기초대비사정률"]), 5) == 99.63200
    assert int(first_cluster["동일일자묶음수"]) == 4


def test_collapse_same_company_day_clusters_keeps_far_apart_same_day_rows_separate() -> None:
    module = _load_module()
    df = pd.DataFrame(
        [
            {"공고번호": "A1", "업체명": "다온문화재연구원", "기초대비사정률": 99.5510, "개찰일시": "2026-03-01 10:00"},
            {"공고번호": "A2", "업체명": "다온문화재연구원", "기초대비사정률": 99.5520, "개찰일시": "2026-03-01 11:00"},
            {"공고번호": "A3", "업체명": "다온문화재연구원", "기초대비사정률": 99.8320, "개찰일시": "2026-03-01 12:00"},
            {"공고번호": "A4", "업체명": "다온문화재연구원", "기초대비사정률": 99.8330, "개찰일시": "2026-03-01 13:00"},
        ]
    )

    collapsed, stats = module.collapse_same_company_day_clusters(df, rate_tolerance=0.003, min_cluster_size=2)

    assert stats["output_rows"] == 2
    assert collapsed["기초대비사정률"].round(5).tolist() == [99.5515, 99.8325]
