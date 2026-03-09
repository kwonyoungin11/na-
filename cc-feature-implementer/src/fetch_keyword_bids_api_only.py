#!/usr/bin/env python3
"""나라장터 API만으로 키워드 공고/참여업체를 수집한다."""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Set, Tuple

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

DEFAULT_KEYWORDS = ["발굴조사", "시굴조사", "시굴", "발굴", "지표조사"]
BID_NOTICE_SEARCH_API = "/ad/BidPublicInfoService/getBidPblancListInfoServcPPSSrch"
OPEN_RESULT_BY_BID_API = "/as/ScsbidInfoService/getOpengResultListInfoServc"


@dataclass(frozen=True)
class BidKey:
    bid_no: str
    bid_ord: str
    bid_clsfc_no: str
    rbid_no: str


def dedupe_keywords(keywords: List[str]) -> List[str]:
    out: List[str] = []
    for k in keywords:
        k = k.strip()
        if k and k not in out:
            out.append(k)
    return out


def ensure_key(item: Dict[str, Any]) -> Optional[BidKey]:
    bid_no = str(item.get("bidNtceNo") or "").strip()
    if not bid_no:
        return None
    return BidKey(
        bid_no=bid_no,
        bid_ord=normalize_ord(item.get("bidNtceOrd")),
        bid_clsfc_no=str(item.get("bidClsfcNo") or "0").strip() or "0",
        rbid_no=normalize_ord(item.get("rbidNo") or "000"),
    )


def choose_notice_item_by_ord(items: List[Dict[str, Any]], bid_ord: str) -> Dict[str, Any]:
    for it in items:
        if normalize_ord(it.get("bidNtceOrd")) == normalize_ord(bid_ord):
            return it
    return items[0] if items else {}


def extract_region_name(client: PublicDataClient, bid_no: str, bid_ord: str) -> str:
    items = client.fetch_all(
        PARTICIPANT_REGION_API,
        {
            "inqryDiv": "2",
            "bidNtceNo": bid_no,
            "bidNtceOrd": normalize_ord(bid_ord),
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


def compute_bidder_metrics(
    bid_amt: Optional[float],
    bidprcrt: Optional[float],
    plnprc: Optional[float],
    bssamt: Optional[float],
    low_rate: Optional[float],
) -> Dict[str, Any]:
    pre_rate = bidprcrt
    if pre_rate is None and bid_amt is not None and plnprc and plnprc > 0:
        pre_rate = bid_amt / plnprc * 100

    bss_rate = None
    if bid_amt is not None and bssamt and bssamt > 0:
        bss_rate = bid_amt / bssamt * 100

    bss_adj_rate = None
    if bss_rate is not None and low_rate and low_rate > 0:
        bss_adj_rate = bss_rate / low_rate * 100

    low_price = None
    if plnprc is not None and low_rate is not None and low_rate > 0:
        low_price = int(round_half_up(plnprc * low_rate / 100, 0))

    diff = None
    if bid_amt is not None and low_price is not None:
        diff = int(round_half_up(bid_amt - low_price, 0))

    return {
        "예가대비투찰률": round_half_up(pre_rate, 3) if pre_rate is not None else None,
        "기초대비투찰률": round_half_up(bss_rate, 3) if bss_rate is not None else None,
        "기초대비사정률": round_half_up(bss_adj_rate, 5) if bss_adj_rate is not None else None,
        "낙찰하한가": low_price,
        "낙찰하한가차이": diff,
    }


def gather_api_only(
    client: PublicDataClient,
    keywords: List[str],
    start: str,
    end: str,
    max_bids: Optional[int],
) -> Tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    key_to_notice_item: Dict[BidKey, Dict[str, Any]] = {}
    key_to_keywords: Dict[BidKey, Set[str]] = {}

    # 1) 키워드별로 나라장터 입찰공고를 직접 검색 (API only)
    for kw in keywords:
        items = client.fetch_all(
            BID_NOTICE_SEARCH_API,
            {
                "inqryDiv": "2",  # 개찰일시 기준
                "inqryBgnDt": start,
                "inqryEndDt": end,
                "bidNtceNm": kw,
            },
            page_size=200,
        )
        for it in items:
            bid_no = str(it.get("bidNtceNo") or "").strip()
            if not bid_no:
                continue
            key = BidKey(
                bid_no=bid_no,
                bid_ord=normalize_ord(it.get("bidNtceOrd")),
                bid_clsfc_no="0",
                rbid_no="000",
            )
            key_to_notice_item[key] = it
            key_to_keywords.setdefault(key, set()).add(kw)

        print(f"키워드 [{kw}] 검색건수: {len(items)}")

    keys = list(key_to_notice_item.keys())
    keys.sort(key=lambda k: (
        str(key_to_notice_item[k].get("opengDt") or ""),
        k.bid_no,
        k.bid_ord,
    ))

    if max_bids is not None:
        keys = keys[: max(0, max_bids)]

    notice_rows: List[Dict[str, Any]] = []
    bidder_rows: List[Dict[str, Any]] = []
    raw_rows: List[Dict[str, Any]] = []

    prepar_cache: Dict[Tuple[str, str], Dict[str, Any]] = {}
    region_cache: Dict[Tuple[str, str], str] = {}
    open_cache: Dict[str, List[Dict[str, Any]]] = {}

    total = len(keys)
    for idx, key in enumerate(keys, start=1):
        notice_item = key_to_notice_item[key]
        keyword_hit = ", ".join(sorted(key_to_keywords.get(key, set())))

        try:
            # 공고번호로 개찰결과 목록 조회 -> bidClsfcNo/rbidNo 확보
            if key.bid_no not in open_cache:
                open_cache[key.bid_no] = client.fetch_all(
                    OPEN_RESULT_BY_BID_API,
                    {
                        "inqryDiv": "4",
                        "bidNtceNo": key.bid_no,
                    },
                    page_size=200,
                )
            open_items = open_cache[key.bid_no]
            open_item = choose_notice_item_by_ord(open_items, key.bid_ord)
            bid_clsfc_no = str(open_item.get("bidClsfcNo") or "0").strip() or "0"
            rbid_no = normalize_ord(open_item.get("rbidNo") or "000")

            # 예비가격 상세
            pre_key = (key.bid_no, normalize_ord(key.bid_ord))
            if pre_key not in prepar_cache:
                prep_items = client.fetch_all(
                    PREPAR_PC_DETAIL_API,
                    {
                        "inqryDiv": "2",
                        "bidNtceNo": key.bid_no,
                        "bidNtceOrd": key.bid_ord,
                    },
                    page_size=200,
                )
                chosen = prep_items[0] if prep_items else {}
                for x in prep_items:
                    if to_float(x.get("plnprc")) is not None or to_float(x.get("bssamt")) is not None:
                        chosen = x
                        break
                prepar_cache[pre_key] = {
                    "plnprc": to_float(chosen.get("plnprc")),
                    "bssamt": to_float(chosen.get("bssamt")),
                }
            prep = prepar_cache[pre_key]

            # 지역제한
            if pre_key not in region_cache:
                region_cache[pre_key] = extract_region_name(client, key.bid_no, key.bid_ord)
            region_name = region_cache[pre_key]

            # 참여업체 전체
            bidders: List[Dict[str, Any]] = []
            if open_item:
                bidders = client.fetch_all(
                    OPEN_COMPLETED_API,
                    {
                        "bidNtceNo": key.bid_no,
                        "bidNtceOrd": key.bid_ord,
                        "bidClsfcNo": bid_clsfc_no,
                        "rbidNo": rbid_no,
                    },
                    page_size=999,
                )

            low_rate = to_float(notice_item.get("sucsfbidLwltRate"))
            plnprc = prep.get("plnprc")
            bssamt = prep.get("bssamt")

            # 요약
            notice_rows.append(
                {
                    "공고번호": format_bid_no_for_output(key.bid_no, key.bid_ord),
                    "bidNtceNo": key.bid_no,
                    "bidNtceOrd": key.bid_ord,
                    "bidClsfcNo": bid_clsfc_no,
                    "rbidNo": rbid_no,
                    "키워드일치": keyword_hit,
                    "공고명": notice_item.get("bidNtceNm") or open_item.get("bidNtceNm"),
                    "공고기관": notice_item.get("ntceInsttNm"),
                    "수요기관": notice_item.get("dminsttNm") or open_item.get("dminsttNm"),
                    "지역제한": region_name,
                    "개찰일시": open_item.get("opengDt"),
                    "낙찰하한율": low_rate,
                    "예정가격": plnprc,
                    "기초금액": bssamt,
                    "참여업체수(개찰완료상세)": len(bidders),
                    "진행상태": open_item.get("progrsDivCdNm"),
                    "처리상태": "정상",
                    "오류": "",
                }
            )

            # 원천(open list) raw
            raw_rows.append(
                {
                    "bidNtceNo": key.bid_no,
                    "bidNtceOrd": key.bid_ord,
                    "bidClsfcNo": bid_clsfc_no,
                    "rbidNo": rbid_no,
                    "키워드일치": keyword_hit,
                    **open_item,
                }
            )

            # 업체 상세
            for b in bidders:
                bid_amt = to_float(b.get("bidprcAmt"))
                bidprcrt = to_float(b.get("bidprcrt"))
                calc = compute_bidder_metrics(
                    bid_amt=bid_amt,
                    bidprcrt=bidprcrt,
                    plnprc=plnprc,
                    bssamt=bssamt,
                    low_rate=low_rate,
                )

                bidder_rows.append(
                    {
                        "공고번호": format_bid_no_for_output(key.bid_no, key.bid_ord),
                        "bidNtceNo": key.bid_no,
                        "bidNtceOrd": key.bid_ord,
                        "bidClsfcNo": bid_clsfc_no,
                        "rbidNo": rbid_no,
                        "키워드일치": keyword_hit,
                        "공고명": notice_item.get("bidNtceNm") or open_item.get("bidNtceNm"),
                        "개찰순위": b.get("opengRank"),
                        "업체명": b.get("prcbdrNm"),
                        "사업자번호": b.get("prcbdrBizno"),
                        "대표자": b.get("prcbdrCeoNm"),
                        "투찰금액": bid_amt,
                        "투찰률": bidprcrt,
                        "투찰일시": b.get("bidprcDt"),
                        "비고": b.get("rmrk"),
                        "예정가격": plnprc,
                        "기초금액": bssamt,
                        "낙찰하한율": low_rate,
                        **calc,
                    }
                )

            print(f"[{idx}/{total}] 완료: {key.bid_no}-{key.bid_ord} / 업체={len(bidders)}")
        except Exception as exc:  # noqa: BLE001
            notice_rows.append(
                {
                    "공고번호": format_bid_no_for_output(key.bid_no, key.bid_ord),
                    "bidNtceNo": key.bid_no,
                    "bidNtceOrd": key.bid_ord,
                    "bidClsfcNo": key.bid_clsfc_no,
                    "rbidNo": key.rbid_no,
                    "키워드일치": keyword_hit,
                    "공고명": notice_item.get("bidNtceNm"),
                    "개찰일시": notice_item.get("opengDt"),
                    "처리상태": "오류",
                    "오류": str(exc),
                }
            )
            print(f"[{idx}/{total}] 오류: {key.bid_no}-{key.bid_ord} ({exc})")

    notice_df = pd.DataFrame(notice_rows)
    bidder_df = pd.DataFrame(bidder_rows)
    raw_df = pd.DataFrame(raw_rows)

    return raw_df, notice_df, bidder_df


def validate_ymdhm(value: str) -> str:
    if not value.isdigit() or len(value) != 12:
        raise argparse.ArgumentTypeError("날짜 형식은 YYYYMMDDHHMM 이어야 합니다.")
    return value


def main() -> None:
    parser = argparse.ArgumentParser(description="나라장터 API 직조회 키워드 수집")
    parser.add_argument("--env-file", default="/mnt/a/c/.env")
    parser.add_argument("--start", default="202601010000", type=validate_ymdhm)
    parser.add_argument("--end", default="202602252359", type=validate_ymdhm)
    parser.add_argument("--keywords", default=",".join(DEFAULT_KEYWORDS))
    parser.add_argument("--max-bids", type=int, default=None)
    parser.add_argument("--output", default="/mnt/a/c/나라장터_API직접_키워드수집_2026-02-25.xlsx")
    args = parser.parse_args()

    keywords = dedupe_keywords([x.strip() for x in args.keywords.split(",")])
    if not keywords:
        raise ValueError("키워드가 없습니다.")

    service_keys = load_service_keys(Path(args.env_file))
    client = PublicDataClient(service_key=service_keys)

    raw_df, notice_df, bidder_df = gather_api_only(
        client=client,
        keywords=keywords,
        start=args.start,
        end=args.end,
        max_bids=args.max_bids,
    )

    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    with pd.ExcelWriter(out, engine="openpyxl") as writer:
        raw_df.to_excel(writer, sheet_name="나라장터_원천목록", index=False)
        notice_df.to_excel(writer, sheet_name="나라장터_공고요약", index=False)
        bidder_df.to_excel(writer, sheet_name="나라장터_참여업체", index=False)

    print(f"완료: {out}")
    print(f"- 원천목록: {len(raw_df)}")
    print(f"- 공고요약: {len(notice_df)}")
    print(f"- 참여업체: {len(bidder_df)}")


if __name__ == "__main__":
    main()
