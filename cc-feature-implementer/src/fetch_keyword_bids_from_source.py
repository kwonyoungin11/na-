#!/usr/bin/env python3
"""아이건설넷 엑셀의 키워드 대상 공고를 나라장터 API로 재수집.

요구사항:
- source xlsx에서 공고명(공사명) 키워드 검색
- 추출된 공고번호를 기준으로 나라장터 API에서 원천 데이터 재조회
- 한 단계 전처리(핵심 필드 정리) 후 엑셀 저장
"""
from __future__ import annotations

import argparse
import re
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import pandas as pd

from nara_bid_excel import (
    BID_NOTICE_API,
    OPEN_COMPLETED_API,
    PARTICIPANT_REGION_API,
    PREPAR_PC_DETAIL_API,
    PublicDataClient,
    load_service_keys,
    normalize_ord,
    to_float,
)

DEFAULT_KEYWORDS = ["발굴조사", "시굴조사", "시굴", "발굴", "지표조사"]
OPEN_RESULT_BY_BID_API = "/as/ScsbidInfoService/getOpengResultListInfoServc"


def parse_bid_no(raw_bid_no: Any) -> Tuple[str, Optional[str]]:
    text = str(raw_bid_no or "").strip()
    if not text:
        return "", None

    if "-" in text:
        left, right = text.rsplit("-", 1)
        left = left.strip()
        right = right.strip()
        if right.isdigit():
            return left, right.zfill(3)
        return left, right

    return text, None


def choose_by_ord(items: List[Dict[str, Any]], ord_value: Optional[str]) -> Dict[str, Any]:
    if not items:
        return {}
    if ord_value:
        ord_norm = normalize_ord(ord_value)
        for item in items:
            if normalize_ord(item.get("bidNtceOrd")) == ord_norm:
                return item
    return items[0]


def extract_region_name(client: PublicDataClient, bid_no: str, bid_ord: str) -> str:
    items = client.fetch_all(
        PARTICIPANT_REGION_API,
        {
            "inqryDiv": "2",
            "bidNtceNo": bid_no,
            "bidNtceOrd": bid_ord,
        },
        page_size=100,
    )
    names = [str(x.get("prtcptPsblRgnNm") or "").strip() for x in items]
    names = [x for x in names if x]
    uniq: List[str] = []
    for n in names:
        if n not in uniq:
            uniq.append(n)
    return ",".join(uniq) if uniq else "전국"


def matched_keywords(text: str, keywords: List[str]) -> str:
    hits = [k for k in keywords if k in text]
    return ", ".join(hits)


def fetch_enriched_rows(
    client: PublicDataClient,
    source_rows: pd.DataFrame,
    keywords: List[str],
) -> Tuple[pd.DataFrame, pd.DataFrame]:
    notice_rows: List[Dict[str, Any]] = []
    bidder_rows: List[Dict[str, Any]] = []

    total = len(source_rows)
    for idx, row in source_rows.reset_index(drop=True).iterrows():
        source_bid_no_text = row.get("공고번호")
        bid_no, bid_ord_hint = parse_bid_no(source_bid_no_text)
        source_name = str(row.get("공사명") or "").strip()
        source_no = row.get("번호")

        if not bid_no:
            notice_rows.append(
                {
                    "source_번호": source_no,
                    "source_공고번호": source_bid_no_text,
                    "source_공고명": source_name,
                    "키워드일치": matched_keywords(source_name, keywords),
                    "처리상태": "공고번호 없음",
                }
            )
            print(f"[{idx+1}/{total}] 스킵: 공고번호 없음")
            continue

        status = "정상"
        err_msg = ""

        try:
            bid_items = client.fetch_all(
                BID_NOTICE_API,
                {
                    "inqryDiv": "2",
                    "bidNtceNo": bid_no,
                },
                page_size=100,
            )
            bid_item = choose_by_ord(bid_items, bid_ord_hint)

            bid_ord = normalize_ord(bid_item.get("bidNtceOrd") or bid_ord_hint or "000")
            bid_name = str(bid_item.get("bidNtceNm") or source_name)

            open_items = client.fetch_all(
                OPEN_RESULT_BY_BID_API,
                {
                    "inqryDiv": "4",
                    "bidNtceNo": bid_no,
                },
                page_size=100,
            )
            open_item = choose_by_ord(open_items, bid_ord)

            bid_clsfc_no = str(open_item.get("bidClsfcNo") or "0")
            rbid_no = normalize_ord(open_item.get("rbidNo") or "000")

            bidder_items = client.fetch_all(
                OPEN_COMPLETED_API,
                {
                    "bidNtceNo": bid_no,
                    "bidNtceOrd": bid_ord,
                    "bidClsfcNo": bid_clsfc_no,
                    "rbidNo": rbid_no,
                },
                page_size=999,
            )

            prep_items = client.fetch_all(
                PREPAR_PC_DETAIL_API,
                {
                    "inqryDiv": "2",
                    "bidNtceNo": bid_no,
                    "bidNtceOrd": bid_ord,
                },
                page_size=200,
            )
            prep_item = {}
            for x in prep_items:
                if to_float(x.get("plnprc")) is not None or to_float(x.get("bssamt")) is not None:
                    prep_item = x
                    break
            if not prep_item and prep_items:
                prep_item = prep_items[0]

            plnprc = to_float(prep_item.get("plnprc"))
            bssamt = to_float(prep_item.get("bssamt"))
            low_rate = to_float(bid_item.get("sucsfbidLwltRate"))
            low_price = None
            if plnprc is not None and low_rate is not None and low_rate > 0:
                low_price = round(plnprc * low_rate / 100)

            region_name = extract_region_name(client, bid_no, bid_ord)

            rank1 = {}
            for b in bidder_items:
                if str(b.get("opengRank") or "").strip() == "1":
                    rank1 = b
                    break

            notice_rows.append(
                {
                    "source_번호": source_no,
                    "source_공고번호": source_bid_no_text,
                    "source_공고명": source_name,
                    "키워드일치": matched_keywords(source_name, keywords),
                    "bidNtceNo": bid_no,
                    "bidNtceOrd": bid_ord,
                    "bidClsfcNo": bid_clsfc_no,
                    "rbidNo": rbid_no,
                    "공고명": bid_name,
                    "공고기관": bid_item.get("ntceInsttNm"),
                    "수요기관": bid_item.get("dminsttNm"),
                    "지역제한": region_name,
                    "개찰일시": open_item.get("opengDt") or bid_item.get("opengDt"),
                    "투찰마감일시": bid_item.get("bidClseDt"),
                    "낙찰하한율": low_rate,
                    "예정가격": plnprc,
                    "기초금액": bssamt,
                    "낙찰하한가": low_price,
                    "참가업체수(API)": len(bidder_items),
                    "1순위업체": rank1.get("prcbdrNm"),
                    "1순위투찰금액": to_float(rank1.get("bidprcAmt")),
                    "1순위투찰률": to_float(rank1.get("bidprcrt")),
                    "진행상태": open_item.get("progrsDivCdNm"),
                    "처리상태": status,
                    "오류": err_msg,
                }
            )

            for b in bidder_items:
                bidder_rows.append(
                    {
                        "bidNtceNo": bid_no,
                        "bidNtceOrd": bid_ord,
                        "공고명": bid_name,
                        "개찰순위": b.get("opengRank"),
                        "업체명": b.get("prcbdrNm"),
                        "사업자번호": b.get("prcbdrBizno"),
                        "대표자": b.get("prcbdrCeoNm"),
                        "투찰금액": to_float(b.get("bidprcAmt")),
                        "투찰률": to_float(b.get("bidprcrt")),
                        "투찰일시": b.get("bidprcDt"),
                        "비고": b.get("rmrk"),
                    }
                )

            print(
                f"[{idx+1}/{total}] 완료: {bid_no}-{bid_ord} / 키워드={matched_keywords(source_name, keywords)} / 업체={len(bidder_items)}"
            )
        except Exception as exc:  # noqa: BLE001
            notice_rows.append(
                {
                    "source_번호": source_no,
                    "source_공고번호": source_bid_no_text,
                    "source_공고명": source_name,
                    "키워드일치": matched_keywords(source_name, keywords),
                    "bidNtceNo": bid_no,
                    "bidNtceOrd": bid_ord_hint,
                    "처리상태": "오류",
                    "오류": str(exc),
                }
            )
            print(f"[{idx+1}/{total}] 오류: {bid_no} ({exc})")

    return pd.DataFrame(notice_rows), pd.DataFrame(bidder_rows)


def main() -> None:
    parser = argparse.ArgumentParser(description="키워드 기반 나라장터 재수집")
    parser.add_argument("--source-xlsx", default="/mnt/a/c/입찰2026-02-25_1.xlsx")
    parser.add_argument("--env-file", default="/mnt/a/c/.env")
    parser.add_argument("--output", default="/mnt/a/c/나라장터_키워드_수집_2026-02-25.xlsx")
    parser.add_argument(
        "--keywords",
        default=",".join(DEFAULT_KEYWORDS),
        help="콤마(,) 구분 키워드",
    )
    parser.add_argument("--max-rows", type=int, default=None, help="테스트용 최대 처리 건수")
    args = parser.parse_args()

    keywords = [x.strip() for x in args.keywords.split(",") if x.strip()]
    if not keywords:
        raise ValueError("키워드가 비어 있습니다.")

    source_df = pd.read_excel(args.source_xlsx, header=1)
    required_cols = {"번호", "공사명", "공고번호"}
    missing = required_cols - set(source_df.columns)
    if missing:
        raise ValueError(f"source 파일에 필요한 컬럼이 없습니다: {missing}")

    pattern = "|".join(re.escape(k) for k in keywords)
    filtered_df = source_df[source_df["공사명"].fillna("").str.contains(pattern)].copy()
    filtered_df = filtered_df.sort_values("번호", ascending=False)

    if args.max_rows is not None:
        filtered_df = filtered_df.head(max(args.max_rows, 0))

    print(f"키워드 대상 공고 수: {len(filtered_df)}")

    service_keys = load_service_keys(Path(args.env_file))
    client = PublicDataClient(service_key=service_keys)

    notice_df, bidder_df = fetch_enriched_rows(client, filtered_df, keywords)

    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    with pd.ExcelWriter(out, engine="openpyxl") as writer:
        filtered_df.to_excel(writer, sheet_name="원본_키워드필터", index=False)
        notice_df.to_excel(writer, sheet_name="나라장터_공고요약", index=False)
        bidder_df.to_excel(writer, sheet_name="나라장터_투찰업체", index=False)

    print(f"완료: {out}")
    print(f"- 원본 키워드 건수: {len(filtered_df)}")
    print(f"- 나라장터 공고요약 건수: {len(notice_df)}")
    print(f"- 나라장터 투찰업체 건수: {len(bidder_df)}")


if __name__ == "__main__":
    main()
