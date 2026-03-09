#!/usr/bin/env python3
"""나라장터(용역) 개찰 데이터를 통합해 엑셀로 저장하는 스크립트.

주요 API
- 개찰결과 목록(용역): getOpengResultListInfoServcPPSSrch
- 개찰완료 상세(투찰업체): getOpengResultListInfoOpengCompt
- 예비가격 상세(예정/기초금액): getOpengResultListInfoServcPreparPcDetail
- 입찰공고 상세(낙찰하한율): getBidPblancListInfoServc
- 참가가능지역: getBidPblancListInfoPrtcptPsblRgn
"""
from __future__ import annotations

import argparse
import os
import math
import re
import time
from dataclasses import dataclass
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple
from urllib.parse import unquote

import pandas as pd
import requests

API_ROOT = "http://apis.data.go.kr/1230000"

OPEN_RESULT_LIST_API = "/as/ScsbidInfoService/getOpengResultListInfoServcPPSSrch"
OPEN_COMPLETED_API = "/as/ScsbidInfoService/getOpengResultListInfoOpengCompt"
PREPAR_PC_DETAIL_API = "/as/ScsbidInfoService/getOpengResultListInfoServcPreparPcDetail"
BID_NOTICE_API = "/ad/BidPublicInfoService/getBidPblancListInfoServc"
PARTICIPANT_REGION_API = "/ad/BidPublicInfoService/getBidPblancListInfoPrtcptPsblRgn"

OUTPUT_COLUMNS = [
    "공고번호",
    "공고명",
    "발주처",
    "지역제한",
    "순위",
    "업체명",
    "개찰일시",
    "투찰일시",
    "투찰금액",
    "예가대비투찰률",
    "기초대비투찰률",
    "기초대비사정률",
    "예정가격",
    "낙찰하한가",
    "기초금액",
    "사정률",
    "발주처투찰률",
    "낙찰하한가차이",
]


class ApiError(RuntimeError):
    """API 호출 실패."""


@dataclass(frozen=True)
class BidKey:
    bid_ntce_no: str
    bid_ntce_ord: str
    bid_clsfc_no: str
    rbid_no: str


@dataclass
class BidSummary:
    bid_ntce_nm: str
    dminstt_nm: str
    openg_dt: str


def round_half_up(value: float, digits: int = 0) -> float:
    quant = Decimal("1") if digits == 0 else Decimal("1").scaleb(-digits)
    dec_value = Decimal(str(value)).quantize(quant, rounding=ROUND_HALF_UP)
    return float(dec_value)


def to_float(value: Any) -> Optional[float]:
    if value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value)
    text = str(value).strip().replace(",", "")
    if not text:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def to_int(value: Any) -> Optional[int]:
    num = to_float(value)
    if num is None:
        return None
    return int(round_half_up(num))


def normalize_ord(value: Any, default: str = "000") -> str:
    text = str(value).strip() if value is not None else ""
    if not text:
        return default
    if text.isdigit():
        return text.zfill(3)
    return text


def format_bid_no_for_output(bid_ntce_no: str, bid_ntce_ord: str) -> str:
    bid_ntce_no = str(bid_ntce_no or "").strip()
    bid_ntce_ord = normalize_ord(bid_ntce_ord)
    if not bid_ntce_no:
        return ""
    if bid_ntce_no.startswith("R"):
        return bid_ntce_no if bid_ntce_ord == "000" else f"{bid_ntce_no}-{bid_ntce_ord}"
    if bid_ntce_ord == "000":
        return bid_ntce_no
    # 구형 번호는 보통 001 -> -01 형태
    if bid_ntce_ord.isdigit():
        return f"{bid_ntce_no}-{int(bid_ntce_ord):02d}"
    return f"{bid_ntce_no}-{bid_ntce_ord}"


def _normalize_service_key_candidates(raw_keys: List[str]) -> List[str]:
    normalized: List[str] = []
    for raw_key in raw_keys:
        value = unquote(str(raw_key).strip().strip('"').strip("'"))
        if not value:
            continue
        if value not in normalized:
            normalized.append(value)
    return normalized


def parse_service_key_from_text(text: str) -> List[str]:
    env_key_pattern = re.compile(r"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+?)\s*$")
    key_name_hint = ("SERVICEKEY", "API_KEY", "NARA", "G2B")

    candidates: List[str] = []
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue

        match = env_key_pattern.match(line)
        if match:
            name, value = match.group(1), match.group(2).strip().strip('"').strip("'")
            if any(h in name.upper() for h in key_name_hint):
                candidates.append(value)
            continue

        if re.fullmatch(r"[A-Za-z0-9%+/=]{10,}", line):
            candidates.append(line)

    if not candidates:
        raise ValueError("서비스키를 찾지 못했습니다. .env 또는 NARA_SERVICE_KEY를 확인하세요.")

    return _normalize_service_key_candidates(candidates)


def load_service_keys(env_path: Path) -> List[str]:
    from_env = (
        os.environ.get("NARA_SERVICE_KEY")
        or os.environ.get("SERVICE_KEY")
        or os.environ.get("SERVICEKEY")
    )
    if from_env:
        raw_keys = [x for x in re.split(r"[,;\n ]+", from_env.strip()) if x]
        keys = _normalize_service_key_candidates(raw_keys)
        if keys:
            return keys

    if not env_path.exists():
        raise FileNotFoundError(f"서비스키 파일이 없습니다: {env_path}")
    return parse_service_key_from_text(env_path.read_text(encoding="utf-8", errors="ignore"))


def load_service_key(env_path: Path) -> str:
    keys = load_service_keys(env_path)
    return keys[0]


def ensure_list(items_node: Any) -> List[Dict[str, Any]]:
    if items_node is None:
        return []
    if isinstance(items_node, list):
        return [x for x in items_node if isinstance(x, dict)]
    if isinstance(items_node, dict):
        if "item" in items_node:
            item_node = items_node["item"]
            if isinstance(item_node, list):
                return [x for x in item_node if isinstance(x, dict)]
            if isinstance(item_node, dict):
                return [item_node]
            return []
        return [items_node]
    return []


class PublicDataClient:
    def __init__(self, service_key: str | List[str], timeout: int = 30, min_interval_sec: float = 0.05):
        raw_keys = service_key if isinstance(service_key, list) else [service_key]
        keys = _normalize_service_key_candidates(raw_keys)
        if not keys:
            raise ValueError("유효한 서비스키가 없습니다.")
        self.service_keys = keys
        self.service_key = self.service_keys[0]
        self._service_key_index = 0
        self.timeout = timeout
        self.min_interval_sec = min_interval_sec
        self.session = requests.Session()
        self._last_called_at = 0.0

    def _rotate_service_key(self) -> bool:
        if len(self.service_keys) <= 1:
            return False
        self._service_key_index = (self._service_key_index + 1) % len(self.service_keys)
        self.service_key = self.service_keys[self._service_key_index]
        return True

    @staticmethod
    def _is_key_rotation_candidate(code: str) -> bool:
        # 입력범위값 초과(07)는 파라미터 문제이므로 키 교체로 해결되지 않음
        return code != "07"

    @property
    def current_service_key_info(self) -> tuple[str, int]:
        return self.service_key, self._service_key_index

    def _throttle(self) -> None:
        elapsed = time.time() - self._last_called_at
        if elapsed < self.min_interval_sec:
            time.sleep(self.min_interval_sec - elapsed)

    def request(self, api_path: str, params: Dict[str, Any], retries: int = 3) -> Dict[str, Any]:
        url = f"{API_ROOT}{api_path}"
        full_params = {"ServiceKey": self.service_key, **params}
        full_params.setdefault("type", "json")

        last_error: Optional[Exception] = None
        for attempt in range(1, retries + 1):
            try:
                self._throttle()
                response = self.session.get(url, params=full_params, timeout=self.timeout)
                self._last_called_at = time.time()
                response.raise_for_status()
                data = response.json()

                # 정상 응답
                if "response" in data:
                    header = data["response"].get("header", {})
                    code = str(header.get("resultCode", "00"))
                    msg = str(header.get("resultMsg", ""))
                    if code not in {"00", "03"}:  # 03: 데이터 없음 계열
                        if (
                            self._is_key_rotation_candidate(code)
                            and attempt < retries
                            and self._rotate_service_key()
                        ):
                            full_params["ServiceKey"] = self.service_key
                            print(
                                f"[WARN] API 키 교체 후 재시도({api_path}): "
                                f"code={code}, msg={msg}, key={self.current_service_key_info[1]}"
                            )
                            time.sleep(0.4)
                            continue
                        raise ApiError(f"API 오류[{code}] {msg} | {api_path}")
                    return data

                # 오류 응답 포맷
                if "nkoneps.com.response.ResponseError" in data:
                    header = data["nkoneps.com.response.ResponseError"].get("header", {})
                    code = str(header.get("resultCode", ""))
                    msg = str(header.get("resultMsg", ""))
                    if code == "03":
                        return {"response": {"header": header, "body": {"items": [], "totalCount": 0}}}
                    if (
                        self._is_key_rotation_candidate(code)
                        and attempt < retries
                        and self._rotate_service_key()
                    ):
                        full_params["ServiceKey"] = self.service_key
                        print(
                            f"[WARN] API 키 교체 후 재시도({api_path}): "
                            f"code={code}, msg={msg}, key={self.current_service_key_info[1]}"
                        )
                        time.sleep(0.4)
                        continue
                    raise ApiError(f"API 오류[{code}] {msg} | {api_path}")

                raise ApiError(f"알 수 없는 응답 포맷 | {api_path}")
            except Exception as exc:  # noqa: BLE001
                last_error = exc
                if attempt == retries:
                    break
                time.sleep(0.6 * attempt)

        raise ApiError(f"API 호출 실패: {api_path} ({last_error})")

    def fetch_all(self, api_path: str, params: Dict[str, Any], page_size: int = 999) -> List[Dict[str, Any]]:
        page_no = 1
        results: List[Dict[str, Any]] = []
        total_count: Optional[int] = None

        while True:
            payload = self.request(
                api_path,
                {
                    **params,
                    "numOfRows": page_size,
                    "pageNo": page_no,
                },
            )
            body = payload.get("response", {}).get("body", {})
            if total_count is None:
                total_count = to_int(body.get("totalCount")) or 0

            items = ensure_list(body.get("items"))
            if not items:
                break

            results.extend(items)

            if len(results) >= total_count:
                break
            page_no += 1

        return results


def extract_lowest_rate(
    client: PublicDataClient,
    bid_ntce_no: str,
    bid_ntce_ord: str,
    notice_cache: Dict[str, List[Dict[str, Any]]],
) -> Optional[float]:
    if bid_ntce_no not in notice_cache:
        notice_cache[bid_ntce_no] = client.fetch_all(
            BID_NOTICE_API,
            {
                "inqryDiv": "2",
                "bidNtceNo": bid_ntce_no,
            },
            page_size=100,
        )

    items = notice_cache[bid_ntce_no]
    ord_norm = normalize_ord(bid_ntce_ord)
    for item in items:
        if normalize_ord(item.get("bidNtceOrd")) == ord_norm:
            rate = to_float(item.get("sucsfbidLwltRate"))
            if rate is not None and rate > 0:
                return rate
    # 같은 차수 미존재 시 첫 값 fallback
    for item in items:
        rate = to_float(item.get("sucsfbidLwltRate"))
        if rate is not None and rate > 0:
            return rate
    return None


def extract_region_name(
    client: PublicDataClient,
    bid_ntce_no: str,
    bid_ntce_ord: str,
    region_cache: Dict[Tuple[str, str], str],
) -> str:
    key = (bid_ntce_no, normalize_ord(bid_ntce_ord))
    if key in region_cache:
        return region_cache[key]

    items = client.fetch_all(
        PARTICIPANT_REGION_API,
        {
            "inqryDiv": "2",
            "bidNtceNo": bid_ntce_no,
            "bidNtceOrd": key[1],
        },
        page_size=100,
    )

    names = [str(x.get("prtcptPsblRgnNm", "")).strip() for x in items]
    names = [x for x in names if x]
    deduped: List[str] = []
    for name in names:
        if name not in deduped:
            deduped.append(name)

    region_name = ",".join(deduped) if deduped else "전국"
    region_cache[key] = region_name
    return region_name


def extract_prepar_price_summary(
    client: PublicDataClient,
    bid_ntce_no: str,
    bid_ntce_ord: str,
    prepar_cache: Dict[Tuple[str, str], Dict[str, Any]],
) -> Dict[str, Any]:
    key = (bid_ntce_no, normalize_ord(bid_ntce_ord))
    if key in prepar_cache:
        return prepar_cache[key]

    items = client.fetch_all(
        PREPAR_PC_DETAIL_API,
        {
            "inqryDiv": "2",
            "bidNtceNo": bid_ntce_no,
            "bidNtceOrd": key[1],
        },
        page_size=999,
    )

    chosen = items[0] if items else {}
    for item in items:
        if to_float(item.get("plnprc")) is not None or to_float(item.get("bssamt")) is not None:
            chosen = item
            break

    summary = {
        "plnprc": to_float(chosen.get("plnprc")),
        "bssamt": to_float(chosen.get("bssamt")),
        "rlOpengDt": chosen.get("rlOpengDt"),
    }
    prepar_cache[key] = summary
    return summary


def parse_rank(value: Any) -> int:
    text = str(value).strip() if value is not None else ""
    if re.fullmatch(r"-?\d+", text):
        return int(text)
    return -1


def calculate_row(
    bid_key: BidKey,
    summary: BidSummary,
    region_name: str,
    bidder_item: Dict[str, Any],
    plnprc: Optional[float],
    bssamt: Optional[float],
    sucsfbid_lwlt_rate: Optional[float],
) -> Dict[str, Any]:
    bid_amount = to_float(bidder_item.get("bidprcAmt"))
    pre_rate = to_float(bidder_item.get("bidprcrt"))

    if pre_rate is None and bid_amount is not None and plnprc and plnprc > 0:
        pre_rate = bid_amount / plnprc * 100

    bss_rate_raw = None
    if bid_amount is not None and bssamt and bssamt > 0:
        bss_rate_raw = bid_amount / bssamt * 100

    adj_rate_raw = None
    if plnprc and bssamt and bssamt > 0:
        adj_rate_raw = plnprc / bssamt * 100

    bss_adj_rate_raw = None
    if bss_rate_raw is not None and sucsfbid_lwlt_rate and sucsfbid_lwlt_rate > 0:
        bss_adj_rate_raw = bss_rate_raw / sucsfbid_lwlt_rate * 100

    low_price = None
    if plnprc is not None and sucsfbid_lwlt_rate is not None and sucsfbid_lwlt_rate > 0:
        low_price = int(round_half_up(plnprc * sucsfbid_lwlt_rate / 100, 0))

    diff = None
    if low_price is not None and bid_amount is not None:
        diff = int(round_half_up(bid_amount - low_price, 0))

    return {
        "공고번호": format_bid_no_for_output(bid_key.bid_ntce_no, bid_key.bid_ntce_ord),
        "공고명": summary.bid_ntce_nm,
        "발주처": summary.dminstt_nm,
        "지역제한": region_name,
        "순위": parse_rank(bidder_item.get("opengRank")),
        "업체명": bidder_item.get("prcbdrNm") or "",
        "개찰일시": summary.openg_dt,
        "투찰일시": bidder_item.get("bidprcDt") or "",
        "투찰금액": int(round_half_up(bid_amount, 0)) if bid_amount is not None else None,
        "예가대비투찰률": round_half_up(pre_rate, 3) if pre_rate is not None else None,
        "기초대비투찰률": round_half_up(bss_rate_raw, 3) if bss_rate_raw is not None else None,
        "기초대비사정률": round_half_up(bss_adj_rate_raw, 5) if bss_adj_rate_raw is not None else None,
        "예정가격": int(round_half_up(plnprc, 0)) if plnprc is not None else None,
        "낙찰하한가": low_price,
        "기초금액": int(round_half_up(bssamt, 0)) if bssamt is not None else None,
        "사정률": round_half_up(adj_rate_raw, 5) if adj_rate_raw is not None else None,
        "발주처투찰률": round_half_up(sucsfbid_lwlt_rate, 3) if sucsfbid_lwlt_rate is not None else None,
        "낙찰하한가차이": diff,
    }


def build_integrated_rows(
    client: PublicDataClient,
    start_ymdhm: str,
    end_ymdhm: str,
    max_bids: Optional[int] = None,
) -> List[Dict[str, Any]]:
    opening_items = client.fetch_all(
        OPEN_RESULT_LIST_API,
        {
            "inqryDiv": "2",  # 개찰일시 기준
            "inqryBgnDt": start_ymdhm,
            "inqryEndDt": end_ymdhm,
        },
        page_size=200,
    )

    # 개찰완료 건만 대상
    opening_items = [x for x in opening_items if str(x.get("progrsDivCdNm", "")).strip() == "개찰완료"]

    # 중복 제거
    dedup: Dict[BidKey, Dict[str, Any]] = {}
    for item in opening_items:
        key = BidKey(
            bid_ntce_no=str(item.get("bidNtceNo", "")).strip(),
            bid_ntce_ord=normalize_ord(item.get("bidNtceOrd")),
            bid_clsfc_no=str(item.get("bidClsfcNo", "0")).strip() or "0",
            rbid_no=normalize_ord(item.get("rbidNo")),
        )
        if not key.bid_ntce_no:
            continue
        dedup[key] = item

    keys = list(dedup.keys())
    keys.sort(key=lambda k: (
        str(dedup[k].get("opengDt") or ""),
        k.bid_ntce_no,
        k.bid_ntce_ord,
        k.bid_clsfc_no,
        k.rbid_no,
    ))

    if max_bids is not None:
        keys = keys[: max(0, max_bids)]

    notice_cache: Dict[str, List[Dict[str, Any]]] = {}
    region_cache: Dict[Tuple[str, str], str] = {}
    prepar_cache: Dict[Tuple[str, str], Dict[str, Any]] = {}

    rows: List[Dict[str, Any]] = []
    total = len(keys)

    for idx, bid_key in enumerate(keys, start=1):
        source = dedup[bid_key]
        summary = BidSummary(
            bid_ntce_nm=str(source.get("bidNtceNm") or "").strip(),
            dminstt_nm=str(source.get("dminsttNm") or "").strip(),
            openg_dt=str(source.get("opengDt") or "").strip(),
        )

        bidder_items = client.fetch_all(
            OPEN_COMPLETED_API,
            {
                "bidNtceNo": bid_key.bid_ntce_no,
                "bidNtceOrd": bid_key.bid_ntce_ord,
                "bidClsfcNo": bid_key.bid_clsfc_no,
                "rbidNo": bid_key.rbid_no,
            },
            page_size=999,
        )

        if not bidder_items:
            print(f"[{idx}/{total}] 스킵(개찰완료 상세 없음): {bid_key.bid_ntce_no}-{bid_key.bid_ntce_ord}")
            continue

        prepar = extract_prepar_price_summary(client, bid_key.bid_ntce_no, bid_key.bid_ntce_ord, prepar_cache)
        sucsfbid_lwlt_rate = extract_lowest_rate(client, bid_key.bid_ntce_no, bid_key.bid_ntce_ord, notice_cache)
        region_name = extract_region_name(client, bid_key.bid_ntce_no, bid_key.bid_ntce_ord, region_cache)

        for bidder in bidder_items:
            rows.append(
                calculate_row(
                    bid_key=bid_key,
                    summary=summary,
                    region_name=region_name,
                    bidder_item=bidder,
                    plnprc=prepar.get("plnprc"),
                    bssamt=prepar.get("bssamt"),
                    sucsfbid_lwlt_rate=sucsfbid_lwlt_rate,
                )
            )

        print(f"[{idx}/{total}] 완료: {bid_key.bid_ntce_no}-{bid_key.bid_ntce_ord} / 업체 {len(bidder_items)}건")

    return rows


def validate_ymdhm(value: str) -> str:
    if not re.fullmatch(r"\d{12}", value):
        raise argparse.ArgumentTypeError("날짜 형식은 YYYYMMDDHHMM 이어야 합니다.")
    return value


def main() -> None:
    parser = argparse.ArgumentParser(description="나라장터 개찰 통합 엑셀 생성기(용역)")
    parser.add_argument("--start", required=True, type=validate_ymdhm, help="조회 시작일시 (YYYYMMDDHHMM)")
    parser.add_argument("--end", required=True, type=validate_ymdhm, help="조회 종료일시 (YYYYMMDDHHMM)")
    parser.add_argument(
        "--output",
        default="/mnt/a/c/실시간_통합_데이터.xlsx",
        help="출력 엑셀 파일 경로",
    )
    parser.add_argument(
        "--env-file",
        default="/mnt/a/c/.env",
        help="서비스키 파일 경로 (.env 또는 텍스트)",
    )
    parser.add_argument("--max-bids", type=int, default=None, help="테스트용 최대 공고 건수 제한")
    args = parser.parse_args()

    service_keys = load_service_keys(Path(args.env_file))
    client = PublicDataClient(service_key=service_keys)

    rows = build_integrated_rows(
        client=client,
        start_ymdhm=args.start,
        end_ymdhm=args.end,
        max_bids=args.max_bids,
    )

    df = pd.DataFrame(rows, columns=OUTPUT_COLUMNS)
    out_path = Path(args.output)
    out_path.parent.mkdir(parents=True, exist_ok=True)

    if not df.empty:
        # 정렬: 공고번호/순위
        df["_rank_group"] = df["순위"].apply(lambda x: 0 if isinstance(x, (int, float)) and x > 0 else 1)
        df["_rank_sort"] = df["순위"].apply(lambda x: x if isinstance(x, (int, float)) and x > 0 else math.inf)
        df = df.sort_values(["공고번호", "_rank_group", "_rank_sort", "투찰금액"], na_position="last")
        df = df.drop(columns=["_rank_group", "_rank_sort"]).reset_index(drop=True)

    df.to_excel(out_path, index=False)
    print(f"완료: {out_path} ({len(df)} rows)")


if __name__ == "__main__":
    main()
