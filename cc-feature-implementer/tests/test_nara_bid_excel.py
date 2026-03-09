from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
sys.path.insert(0, str(SRC))

from nara_bid_excel import (  # noqa: E402
    BidKey,
    BidSummary,
    calculate_row,
    format_bid_no_for_output,
    parse_service_key_from_text,
    load_service_keys,
)


def test_parse_service_key_from_text_with_note_style() -> None:
    text = """일반 인증키
(Encoding)
ABC%2FDEF%3D%3D
일반 인증키
(Decoding)
ABC/DEF==
"""
    assert parse_service_key_from_text(text) == ["ABC/DEF=="]


def test_parse_service_key_from_text_with_multiple_keys() -> None:
    text = """NARA_SERVICE_KEY=KEY1==
FOO=IGNORE
NARA_SERVICE_KEY=KEY2==
"""
    assert parse_service_key_from_text(text) == ["KEY1==", "KEY2=="]


def test_load_service_keys_prefers_env_value() -> None:
    from pathlib import Path
    import os

    os.environ["SERVICE_KEY"] = "ENVKEY1, ENVKEY2"
    try:
        keys = load_service_keys(Path("/does/not/matter"))
        assert keys == ["ENVKEY1", "ENVKEY2"]
    finally:
        del os.environ["SERVICE_KEY"]


def test_format_bid_no_for_output_old_style() -> None:
    assert format_bid_no_for_output("20230917081", "001") == "20230917081-01"
    assert format_bid_no_for_output("20230917081", "000") == "20230917081"


def test_calculate_row_core_metrics() -> None:
    row = calculate_row(
        bid_key=BidKey("20230917081", "001", "0", "000"),
        summary=BidSummary(
            bid_ntce_nm="테스트 용역",
            dminstt_nm="테스트 발주처",
            openg_dt="2023-09-22 11:00:00",
        ),
        region_name="전국",
        bidder_item={
            "opengRank": "1",
            "prcbdrNm": "테스트업체",
            "bidprcAmt": "105625100",
            "bidprcrt": "86.75",
            "bidprcDt": "2023-09-22 08:52:39",
        },
        plnprc=121757925,
        bssamt=120835000,
        sucsfbid_lwlt_rate=86.745,
    )

    assert row["공고번호"] == "20230917081-01"
    assert row["순위"] == 1
    assert row["예가대비투찰률"] == 86.75
    assert row["기초대비투찰률"] == 87.413
    assert row["사정률"] == 100.76379
    assert row["낙찰하한가"] == 105618912
    assert row["낙찰하한가차이"] == 6188
