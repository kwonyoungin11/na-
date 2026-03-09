#!/usr/bin/env python3
"""나라장터 API에서 최근 공고를 조회해 공고별 개별 엑셀 파일 생성."""
from __future__ import annotations

import argparse
import re
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional, Set

import pandas as pd

from nara_bid_excel import (
    OPEN_COMPLETED_API,
    PARTICIPANT_REGION_API,
    PREPAR_PC_DETAIL_API,
    PublicDataClient,
    format_bid_no_for_output,
    load_service_keys,
    normalize_ord,
    round_half_up,
    to_float,
)

# 검색용(나라장터 공고 검색)
BID_NOTICE_SEARCH_API = "/ad/BidPublicInfoService/getBidPblancListInfoServcPPSSrch"
# 공고번호 -> 개찰결과 목록
OPEN_RESULT_BY_BID_API = "/as/ScsbidInfoService/getOpengResultListInfoServc"

DEFAULT_KEYWORDS = ["발굴조사", "시굴조사", "시굴", "발굴", "지표조사"]


@dataclass(frozen=True)
class BidKey:
    bid_no: str
    bid_ord: str


def parse_ymdhm(value: str) -> str:
    if not re.fullmatch(r"\d{12}", value):
        raise argparse.ArgumentTypeError("날짜 형식은 YYYYMMDDHHMM 이어야 합니다.")
    return value


def parse_open_dt(value: Any) -> datetime:
    text = str(value or "").strip()
    for fmt in ("%Y-%m-%d %H:%M:%S", "%Y-%m-%d %H:%M", "%Y-%m-%d"):
        try:
            return datetime.strptime(text, fmt)
        except ValueError:
            continue
    return datetime.min


def dedupe_keywords(items: List[str]) -> List[str]:
    out: List[str] = []
    for x in items:
        x = x.strip()
        if x and x not in out:
            out.append(x)
    return out


def choose_item_by_ord(items: List[Dict[str, Any]], bid_ord: str) -> Dict[str, Any]:
    ord_norm = normalize_ord(bid_ord)
    for item in items:
        if normalize_ord(item.get("bidNtceOrd")) == ord_norm:
            return item
    return items[0] if items else {}


def extract_region_name(client: PublicDataClient, bid_no: str, bid_ord: str, notice_item: Dict[str, Any]) -> str:
    region_items = client.fetch_all(
        PARTICIPANT_REGION_API,
        {
            "inqryDiv": "2",
            "bidNtceNo": bid_no,
            "bidNtceOrd": normalize_ord(bid_ord),
        },
        page_size=100,
    )
    names: List[str] = []
    for it in region_items:
        name = str(it.get("prtcptPsblRgnNm") or "").strip()
        if name and name not in names:
            names.append(name)

    if names:
        return ",".join(names)

    # fallback
    fallback = str(notice_item.get("prtcptLmtRgnNm") or "").strip()
    return fallback if fallback else "전국"


def build_rows_for_notice(
    bid_no: str,
    bid_ord: str,
    notice_item: Dict[str, Any],
    open_item: Dict[str, Any],
    participants: List[Dict[str, Any]],
    region_name: str,
    plnprc: Optional[float],
    bssamt: Optional[float],
    low_rate: Optional[float],
) -> pd.DataFrame:
    low_price: Optional[int] = None
    if plnprc is not None and low_rate is not None and low_rate > 0:
        low_price = int(round_half_up(plnprc * low_rate / 100, 0))

    adj_rate: Optional[float] = None
    if plnprc is not None and bssamt is not None and bssamt > 0:
        adj_rate = round_half_up(plnprc / bssamt * 100, 5)

    rows: List[Dict[str, Any]] = []
    for p in participants:
        bid_amt = to_float(p.get("bidprcAmt"))
        pre_rate = to_float(p.get("bidprcrt"))
        if pre_rate is None and bid_amt is not None and plnprc and plnprc > 0:
            pre_rate = bid_amt / plnprc * 100

        bss_rate = None
        if bid_amt is not None and bssamt and bssamt > 0:
            bss_rate = bid_amt / bssamt * 100

        bss_adj_rate = None
        if bss_rate is not None and low_rate and low_rate > 0:
            bss_adj_rate = bss_rate / low_rate * 100

        diff = None
        if bid_amt is not None and low_price is not None:
            diff = int(round_half_up(bid_amt - low_price, 0))

        rank_text = str(p.get("opengRank") or "").strip()
        rank = int(rank_text) if re.fullmatch(r"-?\d+", rank_text) else -1

        rows.append(
            {
                "공고번호": format_bid_no_for_output(bid_no, bid_ord),
                "공고명": notice_item.get("bidNtceNm") or open_item.get("bidNtceNm"),
                "발주처": notice_item.get("dminsttNm") or open_item.get("dminsttNm"),
                "지역제한": region_name,
                "순위": rank,
                "업체명": p.get("prcbdrNm") or "",
                "개찰일시": open_item.get("opengDt") or notice_item.get("opengDt"),
                "투찰일시": p.get("bidprcDt") or "",
                "투찰금액": int(round_half_up(bid_amt, 0)) if bid_amt is not None else None,
                "예가대비투찰률": round_half_up(pre_rate, 3) if pre_rate is not None else None,
                "기초대비투찰률": round_half_up(bss_rate, 3) if bss_rate is not None else None,
                "기초대비사정률": round_half_up(bss_adj_rate, 5) if bss_adj_rate is not None else None,
                "예정가격": int(round_half_up(plnprc, 0)) if plnprc is not None else None,
                "낙찰하한가": low_price,
                "기초금액": int(round_half_up(bssamt, 0)) if bssamt is not None else None,
                "사정률": adj_rate,
                "발주처투찰률": round_half_up(low_rate, 3) if low_rate is not None else None,
                "낙찰하한가차이": diff,
                # 참여업체 상세
                "사업자번호": p.get("prcbdrBizno"),
                "대표자": p.get("prcbdrCeoNm"),
                "비고": p.get("rmrk"),
                "입찰가격점수": p.get("bidPrceEvlVal"),
                "기술평가점수": p.get("techEvlVal"),
                "종합평가점수": p.get("totalEvlAmtVal"),
            }
        )

    rows.sort(
        key=lambda x: (
            0 if isinstance(x["순위"], int) and x["순위"] > 0 else 1,
            x["순위"] if isinstance(x["순위"], int) and x["순위"] > 0 else 10**9,
            x["투찰금액"] if x["투찰금액"] is not None else 10**15,
        )
    )
    return pd.DataFrame(rows)


def main() -> None:
    parser = argparse.ArgumentParser(description="최근 공고 개별파일 생성")
    parser.add_argument("--env-file", default="/mnt/a/c/.env")
    parser.add_argument("--start", type=parse_ymdhm, default="202601010000")
    parser.add_argument("--end", type=parse_ymdhm, default="202602252359")
    parser.add_argument("--count", type=int, default=10)
    parser.add_argument("--keywords", default=",".join(DEFAULT_KEYWORDS))
    parser.add_argument("--output-dir", default="/mnt/a/c/최근10공고_개별파일_2026-02-25")
    args = parser.parse_args()

    keywords = dedupe_keywords(args.keywords.split(","))
    service_keys = load_service_keys(Path(args.env_file))
    client = PublicDataClient(service_key=service_keys)

    # 1) 키워드별 공고 검색 (나라장터 API만)
    by_key: Dict[BidKey, Dict[str, Any]] = {}
    key_keywords: Dict[BidKey, Set[str]] = {}

    for kw in keywords:
        items = client.fetch_all(
            BID_NOTICE_SEARCH_API,
            {
                "inqryDiv": "2",
                "inqryBgnDt": args.start,
                "inqryEndDt": args.end,
                "bidNtceNm": kw,
            },
            page_size=50,
        )
        print(f"키워드 [{kw}] 검색건수: {len(items)}")
        for it in items:
            bid_no = str(it.get("bidNtceNo") or "").strip()
            if not bid_no:
                continue
            key = BidKey(bid_no=bid_no, bid_ord=normalize_ord(it.get("bidNtceOrd")))
            if key not in by_key:
                by_key[key] = it
            else:
                # 같은 공고면 최신 개찰일시 정보 유지
                if parse_open_dt(it.get("opengDt")) > parse_open_dt(by_key[key].get("opengDt")):
                    by_key[key] = it
            key_keywords.setdefault(key, set()).add(kw)

    candidates = list(by_key.items())
    candidates.sort(
        key=lambda kv: (
            parse_open_dt(kv[1].get("opengDt")),
            parse_open_dt(kv[1].get("bidNtceDt")),
            kv[0].bid_no,
            kv[0].bid_ord,
        ),
        reverse=True,
    )

    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    # 2) 최신순으로 순회하며 참여업체 데이터 있는 공고 count개 생성
    selected = 0
    index_rows: List[Dict[str, Any]] = []

    for key, notice_item in candidates:
        if selected >= args.count:
            break

        bid_no, bid_ord = key.bid_no, key.bid_ord

        try:
            open_items = client.fetch_all(
                OPEN_RESULT_BY_BID_API,
                {
                    "inqryDiv": "4",
                    "bidNtceNo": bid_no,
                },
                page_size=100,
            )
            open_item = choose_item_by_ord(open_items, bid_ord)
            if not open_item:
                continue

            bid_clsfc_no = str(open_item.get("bidClsfcNo") or "0").strip() or "0"
            rbid_no = normalize_ord(open_item.get("rbidNo") or "000")

            participants = client.fetch_all(
                OPEN_COMPLETED_API,
                {
                    "bidNtceNo": bid_no,
                    "bidNtceOrd": bid_ord,
                    "bidClsfcNo": bid_clsfc_no,
                    "rbidNo": rbid_no,
                },
                page_size=100,
            )

            # 참여업체 없는 공고는 제외
            if not participants:
                continue

            prep_items = client.fetch_all(
                PREPAR_PC_DETAIL_API,
                {
                    "inqryDiv": "2",
                    "bidNtceNo": bid_no,
                    "bidNtceOrd": bid_ord,
                },
                page_size=100,
            )
            prep = {}
            for item in prep_items:
                if to_float(item.get("plnprc")) is not None or to_float(item.get("bssamt")) is not None:
                    prep = item
                    break
            if not prep and prep_items:
                prep = prep_items[0]

            plnprc = to_float(prep.get("plnprc"))
            bssamt = to_float(prep.get("bssamt"))
            low_rate = to_float(notice_item.get("sucsfbidLwltRate"))
            region_name = extract_region_name(client, bid_no, bid_ord, notice_item)

            df = build_rows_for_notice(
                bid_no=bid_no,
                bid_ord=bid_ord,
                notice_item=notice_item,
                open_item=open_item,
                participants=participants,
                region_name=region_name,
                plnprc=plnprc,
                bssamt=bssamt,
                low_rate=low_rate,
            )

            selected += 1
            file_name = f"{selected:02d}_{format_bid_no_for_output(bid_no, bid_ord)}.xlsx"
            file_path = out_dir / file_name

            with pd.ExcelWriter(file_path, engine="openpyxl") as writer:
                df.to_excel(writer, sheet_name="필수18컬럼_참여업체전체", index=False)

            index_rows.append(
                {
                    "순번": selected,
                    "파일명": file_name,
                    "공고번호": format_bid_no_for_output(bid_no, bid_ord),
                    "공고명": notice_item.get("bidNtceNm"),
                    "개찰일시": open_item.get("opengDt") or notice_item.get("opengDt"),
                    "참여업체수": len(df),
                    "키워드일치": ", ".join(sorted(key_keywords.get(key, set()))),
                }
            )

            print(f"[{selected}/{args.count}] 생성 완료: {file_name} (업체 {len(df)}건)")

        except Exception as exc:  # noqa: BLE001
            print(f"스킵(오류): {format_bid_no_for_output(bid_no, bid_ord)} - {exc}")

    index_path = out_dir / "00_목록.xlsx"
    pd.DataFrame(index_rows).to_excel(index_path, index=False)

    print(f"\n완료: {out_dir}")
    print(f"- 생성 파일 수: {selected} (요청 {args.count})")
    print(f"- 목록 파일: {index_path}")


if __name__ == "__main__":
    main()
