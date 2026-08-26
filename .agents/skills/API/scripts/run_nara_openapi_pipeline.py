#!/usr/bin/env python3
"""
나라장터 OpenAPI 월별 수집 + 저장 전 전처리 파이프라인 (Skill 전용)

핵심 요구 반영:
- API는 1개월 단위로 분할 호출
- 저장 전에 전처리/스키마 정규화/수치 계산 수행
- 특히 기초대비사정률(%) 계산값 생성
- 지역제한 기준으로 전국 / 대구경북 분리 산출
- 기존 프로젝트와 별도 출력 디렉터리 사용 (공공데이터포털api/output)
"""

from __future__ import annotations

import argparse
import calendar
import json
import re
import time
import sys
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import pandas as pd
import requests
from requests import Session
from requests.adapters import HTTPAdapter
from urllib3.util import Retry


PROJECT_ROOT = Path(__file__).resolve().parents[4]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from nara_workspace_paths import default_env_path, output_dir

API_ROOT = PROJECT_ROOT / "공공데이터포털api"
DEFAULT_ENV_PATH = default_env_path()
DEFAULT_OUTPUT_DIR = output_dir()

SCSBID_BASE = "https://apis.data.go.kr/1230000/as/ScsbidInfoService"
BID_BASE = "https://apis.data.go.kr/1230000/ad/BidPublicInfoService"

# 낙찰정보(개찰결과) 오퍼레이션
OPEN_RESULT_OPS = {
    "thng": "getOpengResultListInfoThngPPSSrch",
    "cnstwk": "getOpengResultListInfoCnstwkPPSSrch",
    "servc": "getOpengResultListInfoServcPPSSrch",
    "frgcpt": "getOpengResultListInfoFrgcptPPSSrch",
}
OPEN_COMPLETED_DETAIL_OPERATION = "getOpengResultListInfoOpengCompt"

# 입찰공고정보 오퍼레이션 (낙찰하한율, 지역제한, 기초금액 보강)
BID_NOTICE_OPS = {
    "thng": "getBidPblancListInfoThngPPSSrch",
    "cnstwk": "getBidPblancListInfoCnstwkPPSSrch",
    "servc": "getBidPblancListInfoServcPPSSrch",
    "frgcpt": "getBidPblancListInfoFrgcptPPSSrch",
}
PARTICIPANT_REGION_OPERATION = "getBidPblancListInfoPrtcptPsblRgn"

TARGET_SCHEMA_COLUMNS = [
    "공고번호",
    "공사명",
    "발주처",
    "지역제한",
    "개찰일시",
    "기초금액",
    "투찰률",
    "예정가격",
    "사정률",
    "참여업체수",
    "순위",
    "업체명",
    "투찰금액",
    "예가대비투찰률(%)",
    "기초대비투찰률(%)",
    "기초대비사정률(%)",
    "투찰일시",
]

DEFAULT_ALLOWED_REGIONS = ["전국", "경북"]
DEFAULT_TARGET_COMPANY_TERMS = ["문화재연구원"]
RETIRED_COMPANY_TOKENS = ("효림",)
REGION_CANONICAL_ORDER = [
    "전국",
    "서울",
    "부산",
    "대구",
    "인천",
    "광주",
    "대전",
    "울산",
    "세종",
    "경기",
    "강원",
    "충북",
    "충남",
    "전북",
    "전남",
    "경북",
    "경남",
    "제주",
]
REGION_NORMALIZATION_RULES = [
    ("서울특별시", "서울"),
    ("부산광역시", "부산"),
    ("대구광역시", "대구"),
    ("인천광역시", "인천"),
    ("광주광역시", "광주"),
    ("대전광역시", "대전"),
    ("울산광역시", "울산"),
    ("세종특별자치시", "세종"),
    ("경기도", "경기"),
    ("강원특별자치도", "강원"),
    ("강원도", "강원"),
    ("충청북도", "충북"),
    ("충청북", "충북"),
    ("충청남도", "충남"),
    ("충청남", "충남"),
    ("전북특별자치도", "전북"),
    ("전라북도", "전북"),
    ("전라북", "전북"),
    ("전라남도", "전남"),
    ("전라남", "전남"),
    ("경상북도", "경북"),
    ("경상북", "경북"),
    ("경상남도", "경남"),
    ("경상남", "경남"),
    ("제주특별자치도", "제주"),
    ("제주도", "제주"),
]


@dataclass
class PipelineConfig:
    service_key: str
    start_month: str
    end_month: str
    keywords: List[str]
    operations: List[str]
    start_datetime: Optional[str] = None
    end_datetime: Optional[str] = None
    num_of_rows: int = 100
    request_delay_sec: float = 0.03
    output_dir: Path = DEFAULT_OUTPUT_DIR
    allowed_regions: List[str] = field(default_factory=lambda: list(DEFAULT_ALLOWED_REGIONS))
    min_participants: int = 5
    target_company_terms: List[str] = field(default_factory=lambda: list(DEFAULT_TARGET_COMPANY_TERMS))
    exclude_retired_company: bool = True


def _ts() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def _log(msg: str) -> None:
    print(f"[{_ts()}] {msg}")


def _canonical_json_signature(row: Dict) -> str:
    return json.dumps(row, ensure_ascii=False, sort_keys=True)


def _merge_jsonl_rows(path: Path, incoming_rows: List[Dict]) -> Tuple[int, int]:
    merged_rows: List[Dict] = []
    seen: set[str] = set()
    existing_count = 0

    if path.exists():
        for raw_line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
            line = raw_line.strip()
            if not line:
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                continue
            signature = _canonical_json_signature(row)
            if signature in seen:
                continue
            seen.add(signature)
            merged_rows.append(row)
            existing_count += 1

    for row in incoming_rows:
        signature = _canonical_json_signature(row)
        if signature in seen:
            continue
        seen.add(signature)
        merged_rows.append(row)

    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as file:
        for row in merged_rows:
            file.write(json.dumps(row, ensure_ascii=False) + "\n")

    return len(merged_rows), len(merged_rows) - existing_count


def load_service_key(env_path: Path) -> str:
    """.env 또는 텍스트 파일에서 서비스키 추출.

    허용 포맷:
    1) SERVICE_KEY=xxxxx
    2) DATA_GO_KR_SERVICE_KEY=xxxxx
    3) 파일 내 단독 키 문자열 (라벨 줄이 섞여 있어도 키 패턴 자동 추출)
    """
    if not env_path.exists():
        raise FileNotFoundError(f"서비스키 파일이 없습니다: {env_path}")

    text = env_path.read_text(encoding="utf-8", errors="ignore")
    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]

    # 1) KEY=VALUE
    key_names = {
        "SERVICE_KEY",
        "SERVICEKEY",
        "DATA_GO_KR_SERVICE_KEY",
        "PUBLIC_DATA_API_KEY",
        "API_KEY",
    }
    for line in lines:
        if "=" not in line:
            continue
        k, v = line.split("=", 1)
        k = k.strip().upper()
        v = v.strip().strip('"').strip("'")
        if k in key_names and v:
            return v

    # 2) 텍스트 전체에서 키 패턴 후보 추출
    # 공공데이터포털 인증키는 base64 유사 패턴(+/== 포함)
    candidates = re.findall(r"[A-Za-z0-9+/=]{40,}", text)
    if candidates:
        return max(candidates, key=len)

    raise ValueError("서비스키를 추출하지 못했습니다. .env 포맷을 확인하세요.")


def month_windows(
    start_month: str,
    end_month: str,
    start_datetime: Optional[str] = None,
    end_datetime: Optional[str] = None,
) -> List[Tuple[str, str, str]]:
    """YYYY-MM ~ YYYY-MM 범위를 월 단위 (시작/종료 시각 포함)로 반환"""
    s = datetime.strptime(start_month, "%Y-%m")
    e = datetime.strptime(end_month, "%Y-%m")
    if s > e:
        raise ValueError("start_month는 end_month보다 작거나 같아야 합니다.")

    start_dt_override = (
        datetime.strptime(start_datetime, "%Y%m%d%H%M")
        if start_datetime
        else None
    )
    end_dt_override = (
        datetime.strptime(end_datetime, "%Y%m%d%H%M")
        if end_datetime
        else None
    )

    out: List[Tuple[str, str, str]] = []
    cur_y, cur_m = s.year, s.month
    while (cur_y, cur_m) <= (e.year, e.month):
        last_day = calendar.monthrange(cur_y, cur_m)[1]
        month_str = f"{cur_y:04d}-{cur_m:02d}"
        begin_dt = datetime.strptime(f"{cur_y:04d}{cur_m:02d}010000", "%Y%m%d%H%M")
        end_dt = datetime.strptime(f"{cur_y:04d}{cur_m:02d}{last_day:02d}2359", "%Y%m%d%H%M")

        if start_dt_override and begin_dt < start_dt_override:
            begin_dt = start_dt_override
        if end_dt_override and end_dt > end_dt_override:
            end_dt = end_dt_override

        if begin_dt <= end_dt:
            out.append(
                (
                    month_str,
                    begin_dt.strftime("%Y%m%d%H%M"),
                    end_dt.strftime("%Y%m%d%H%M"),
                )
            )

        if cur_m == 12:
            cur_y += 1
            cur_m = 1
        else:
            cur_m += 1
    return out


def build_session() -> Session:
    """요청 세션 (timeout/retry)"""
    s = requests.Session()
    retries = Retry(
        total=4,
        connect=4,
        read=4,
        backoff_factor=0.3,
        status_forcelist=[429, 500, 502, 503, 504],
        allowed_methods={"GET"},
        raise_on_status=False,
    )
    adapter = HTTPAdapter(max_retries=retries)
    s.mount("http://", adapter)
    s.mount("https://", adapter)
    return s


def _extract_items(payload: Dict) -> Tuple[List[Dict], int, str, str]:
    """OpenAPI 공통 응답에서 items/item 추출"""
    response = payload.get("response", {}) if isinstance(payload, dict) else {}
    header = response.get("header", {}) if isinstance(response, dict) else {}
    body = response.get("body", {}) if isinstance(response, dict) else {}

    result_code = str(header.get("resultCode", ""))
    result_msg = str(header.get("resultMsg", ""))

    items_obj = body.get("items", {}) if isinstance(body, dict) else {}

    # 응답 패턴 1) items: [ {...}, {...} ]
    if isinstance(items_obj, list):
        items = items_obj
    # 응답 패턴 2) items: { item: [ ... ] } 또는 { item: {...} }
    elif isinstance(items_obj, dict):
        item = items_obj.get("item", [])
        if isinstance(item, list):
            items = item
        elif isinstance(item, dict):
            items = [item]
        else:
            # 응답 패턴 3) items 자체가 단건 dict
            items = [items_obj] if items_obj else []
    else:
        items = []

    total_count = int(body.get("totalCount", 0) or 0) if isinstance(body, dict) else 0
    return items, total_count, result_code, result_msg


def fetch_paged(
    session: Session,
    base_url: str,
    operation: str,
    service_key: str,
    base_params: Dict,
    num_of_rows: int,
    delay_sec: float,
) -> List[Dict]:
    """단일 오퍼레이션 페이지네이션 수집"""
    all_items: List[Dict] = []
    page = 1

    while True:
        params = dict(base_params)
        params["ServiceKey"] = service_key
        params["numOfRows"] = num_of_rows
        params["pageNo"] = page
        params.setdefault("type", "json")

        url = f"{base_url}/{operation}"
        resp = session.get(url, params=params, timeout=(5, 20))
        resp.raise_for_status()

        # JSON 우선
        try:
            payload = resp.json()
        except Exception:
            # XML fallback 파싱(간단): 실패 시 원문 저장용 에러
            raise RuntimeError(f"JSON 파싱 실패: {operation} page={page}")

        items, total_count, result_code, result_msg = _extract_items(payload)
        if result_code and result_code != "00":
            raise RuntimeError(
                f"API 오류 ({operation}, page={page}): resultCode={result_code}, msg={result_msg}"
            )

        if not items:
            break

        all_items.extend(items)
        if len(all_items) >= total_count:
            break

        page += 1
        time.sleep(delay_sec)

    return all_items


def parse_money(value) -> Optional[float]:
    if value is None or (isinstance(value, float) and pd.isna(value)):
        return None
    s = str(value).strip()
    if not s:
        return None
    s = s.replace(",", "").replace("원", "").replace("%", "").strip()
    try:
        return float(s)
    except Exception:
        return None


def fmt_won(value: Optional[float]) -> str:
    if value is None or pd.isna(value):
        return ""
    return f"{int(round(value)):,}원"


def fmt_bid_money(value: Optional[float]) -> str:
    if value is None or pd.isna(value):
        return ""
    return f"{int(round(value)):,}"


def fmt_bid_rate(value: Optional[float]) -> str:
    if value is None or pd.isna(value):
        return ""
    s = f"{float(value):.3f}".rstrip("0").rstrip(".")
    return f"{s}% "


def parse_float(value) -> Optional[float]:
    if value is None or (isinstance(value, float) and pd.isna(value)):
        return None
    s = str(value).strip().replace(",", "")
    if not s:
        return None
    try:
        return float(s)
    except Exception:
        return None


def clean_text(value) -> str:
    if value is None:
        return ""
    if isinstance(value, float) and pd.isna(value):
        return ""
    text = str(value).strip()
    if not text or text.lower() == "nan":
        return ""
    return text


def canonicalize_bid_company_name(value: str) -> str:
    name = clean_text(value)
    if not name:
        return ""

    name = re.sub(r"\s*\([^)]*\d{2,3}[-)]\d{3,4}[-)]\d{4}[^)]*\)", "", name)
    name = re.sub(r"\(재\)|재단법인|\(재단\)|주식회사|\(주\)", "", name)
    name = re.sub(r"\([^)]*\)", "", name)
    name = name.replace("(", "").replace(")", "")
    name = re.sub(r"\s+", " ", name).strip()
    name = name.replace("문화유산연구원", "문화재연구원")
    name = re.sub(r"(?<!문화)유산연구원", "문화재연구원", name)
    name = re.sub(r"문화재보존센터", "문화재연구원", name)
    name = re.sub(r"고고학연구소", "문화재연구원", name)
    if "문화유산마을" in name and "문화재연구원" not in name:
        name = "문화유산마을 문화재연구원"
    return re.sub(r"\s+", " ", name).strip()


def normalize_region(value: str) -> str:
    text = clean_text(value)
    text = text.replace("\n", " ").replace("\r", " ")
    text = re.sub(r"\s+", "", text)
    for src, dst in REGION_NORMALIZATION_RULES:
        text = text.replace(src, dst)
    return text


def is_retired_company(value: str) -> bool:
    compact = clean_text(value).replace(" ", "")
    return any(token in compact for token in RETIRED_COMPANY_TOKENS)


def extract_region_tokens(value: str) -> List[str]:
    normalized = normalize_region(value)
    if not normalized:
        return []
    return [token for token in REGION_CANONICAL_ORDER if token in normalized]


def infer_region_from_org_text(value: str) -> str:
    tokens = list(dict.fromkeys(extract_region_tokens(value)))
    if not tokens:
        return ""
    if "전국" in tokens:
        return "전국"
    if "대구" in tokens and "경북" in tokens:
        return "대구/경북"
    if "대구" in tokens:
        return "대구"
    if "경북" in tokens:
        return "경북"
    return ",".join(tokens)


def has_allowed_region(
    region_text: str,
    allowed_regions: List[str],
    *,
    include_nationwide_override: bool = True,
) -> bool:
    region_tokens = set(extract_region_tokens(region_text))
    if not region_tokens:
        return False

    # 사용자 요구사항: '전국'이 포함된 공고는 타 시도 토큰이 함께 있어도 허용
    # 단, 대구/경북 분리 산출 등 특정 용도에서는 override를 끌 수 있다.
    if include_nationwide_override and "전국" in region_tokens:
        return True

    allowed_tokens: set[str] = set()
    for raw in allowed_regions:
        allowed_tokens.update(extract_region_tokens(raw))
        normalized = normalize_region(raw)
        if normalized in REGION_CANONICAL_ORDER:
            allowed_tokens.add(normalized)

    return bool(region_tokens & allowed_tokens)


def has_target_company(name: str, target_terms: List[str]) -> bool:
    normalized = canonicalize_bid_company_name(name)
    return any(term and term in normalized for term in target_terms)


def normalize_bid_no(bid_ntce_no: str, bid_ntce_ord: Optional[str]) -> str:
    bid_ntce_no = str(bid_ntce_no or "").strip()
    ord_no = str(bid_ntce_ord or "").strip()
    if not bid_ntce_no:
        return ""
    if "-" in bid_ntce_no:
        return bid_ntce_no
    if ord_no:
        return f"{bid_ntce_no}-{ord_no.zfill(3)}"
    return bid_ntce_no


def normalize_ord(value: object) -> str:
    text = clean_text(value)
    if not text:
        return "000"
    return text.zfill(3) if text.isdigit() else text


def fetch_participant_region_names(
    session: Session,
    service_key: str,
    bid_ntce_no: str,
    bid_ntce_ord: object,
    *,
    num_of_rows: int,
    delay_sec: float,
) -> str:
    if not clean_text(bid_ntce_no):
        return ""

    items = fetch_paged(
        session=session,
        base_url=BID_BASE,
        operation=PARTICIPANT_REGION_OPERATION,
        service_key=service_key,
        base_params={
            "inqryDiv": "2",
            "bidNtceNo": clean_text(bid_ntce_no),
            "bidNtceOrd": normalize_ord(bid_ntce_ord),
            "type": "json",
        },
        num_of_rows=num_of_rows,
        delay_sec=delay_sec,
    )

    names: list[str] = []
    for item in items:
        name = clean_text(item.get("prtcptPsblRgnNm"))
        if name and name not in names:
            names.append(name)
    return ",".join(names)


def split_openg_corp_info(openg_corp_info: str) -> List[Dict]:
    """opengCorpInfo 파싱

    기본 포맷(문서 예시):
    업체명^사업자번호^대표자명^투찰금액^투찰률
    """
    if not openg_corp_info:
        return []

    raw = str(openg_corp_info).strip()
    if not raw:
        return []

    # 구분자 후보 정리
    normalized = (
        raw.replace("<br/>", "|")
        .replace("<br>", "|")
        .replace("\r", "|")
        .replace("\n", "|")
        .replace("||", "|")
    )

    chunks = [c.strip() for c in re.split(r"[|;]+", normalized) if c.strip()]
    result: List[Dict] = []

    for idx, chunk in enumerate(chunks, start=1):
        parts = [p.strip() for p in chunk.split("^")]
        if len(parts) < 5:
            continue
        company = parts[0]
        bizno = parts[1]
        ceo = parts[2]
        bid_amount = parse_money(parts[3])
        bid_rate = parse_float(parts[4])

        result.append(
            {
                "rank": idx,
                "company": company,
                "bizno": bizno,
                "ceo": ceo,
                "bid_amount": bid_amount,
                "bid_rate": bid_rate,
            }
        )

    # 단일 청크인데 분리 실패한 경우, 통짜 '^' 기준 5개씩 재시도
    if not result and "^" in normalized:
        parts = [p.strip() for p in normalized.split("^")]
        for i in range(0, len(parts), 5):
            seg = parts[i : i + 5]
            if len(seg) < 5:
                break
            result.append(
                {
                    "rank": len(result) + 1,
                    "company": seg[0],
                    "bizno": seg[1],
                    "ceo": seg[2],
                    "bid_amount": parse_money(seg[3]),
                    "bid_rate": parse_float(seg[4]),
                }
            )

    return result


def to_rank_int(value, default: int = 0) -> int:
    text = clean_text(value)
    if text:
        if text.lstrip("-").isdigit():
            return int(text)
        parsed = parse_float(text)
        if parsed is not None:
            return int(parsed)
    return default


def region_bucket(region_text: str) -> str:
    tokens = extract_region_tokens(region_text)
    if not tokens:
        return "미상"
    if "전국" in tokens:
        return "전국"
    if "대구" in tokens or "경북" in tokens:
        return "대구/경북"
    return "기타"


def collect_raw(config: PipelineConfig, *, session: Optional[Session] = None) -> Tuple[pd.DataFrame, pd.DataFrame, Dict]:
    """월별 raw 수집 (개찰결과 + 공고정보)"""
    output_raw = config.output_dir / "raw"
    output_raw.mkdir(parents=True, exist_ok=True)

    if session is None:
        session = build_session()

    months = month_windows(
        config.start_month,
        config.end_month,
        config.start_datetime,
        config.end_datetime,
    )
    all_openg_rows: List[Dict] = []
    all_notice_rows: List[Dict] = []

    stats = {
        "months": len(months),
        "keywords": config.keywords,
        "operations": config.operations,
        "openg_rows": 0,
        "notice_rows": 0,
    }

    participant_region_cache: dict[tuple[str, str], str] = {}

    for month_str, begin, end in months:
        _log(f"[수집] 월={month_str}, 기간={begin}~{end}")
        month_openg: List[Dict] = []
        month_notice: List[Dict] = []

        keyword_list = config.keywords if config.keywords else [None]

        for op in config.operations:
            open_op = OPEN_RESULT_OPS[op]
            notice_op = BID_NOTICE_OPS[op]

            for keyword in keyword_list:
                common_params = {
                    "inqryDiv": 1,
                    "inqryBgnDt": begin,
                    "inqryEndDt": end,
                    "type": "json",
                }
                if keyword:
                    common_params["bidNtceNm"] = keyword

                # 개찰결과
                openg_items = fetch_paged(
                    session=session,
                    base_url=SCSBID_BASE,
                    operation=open_op,
                    service_key=config.service_key,
                    base_params=common_params,
                    num_of_rows=config.num_of_rows,
                    delay_sec=config.request_delay_sec,
                )
                for item in openg_items:
                    row = dict(item)
                    row["_src_operation"] = open_op
                    row["_src_group"] = op
                    row["_src_month"] = month_str
                    row["_src_keyword"] = keyword or ""
                    month_openg.append(row)

                # 입찰공고(보강)
                notice_items = fetch_paged(
                    session=session,
                    base_url=BID_BASE,
                    operation=notice_op,
                    service_key=config.service_key,
                    base_params=common_params,
                    num_of_rows=config.num_of_rows,
                    delay_sec=config.request_delay_sec,
                )
                for item in notice_items:
                    row = dict(item)
                    region_key = (
                        clean_text(row.get("bidNtceNo")),
                        normalize_ord(row.get("bidNtceOrd")),
                    )
                    if region_key[0]:
                        if region_key not in participant_region_cache:
                            try:
                                participant_region_cache[region_key] = fetch_participant_region_names(
                                    session,
                                    config.service_key,
                                    region_key[0],
                                    region_key[1],
                                    num_of_rows=config.num_of_rows,
                                    delay_sec=config.request_delay_sec,
                                )
                            except Exception as exc:
                                participant_region_cache[region_key] = ""
                                _log(
                                    f"[WARN] 참가가능지역 조회 실패 "
                                    f"{normalize_bid_no(region_key[0], region_key[1])}: {exc}"
                                )
                        if participant_region_cache[region_key]:
                            row["prtcptPsblRgnNm"] = participant_region_cache[region_key]
                    row["_src_operation"] = notice_op
                    row["_src_group"] = op
                    row["_src_month"] = month_str
                    row["_src_keyword"] = keyword or ""
                    month_notice.append(row)

                time.sleep(config.request_delay_sec)

        # 월별 raw 저장
        month_dir = output_raw / month_str
        month_dir.mkdir(parents=True, exist_ok=True)

        openg_path = month_dir / "openg_result_raw.jsonl"
        notice_path = month_dir / "bid_notice_raw.jsonl"

        merged_openg_count, added_openg_count = _merge_jsonl_rows(openg_path, month_openg)
        merged_notice_count, added_notice_count = _merge_jsonl_rows(notice_path, month_notice)

        _log(
            "  - 월별 저장: "
            f"{openg_path.name}=+{added_openg_count:,} (누적 {merged_openg_count:,}), "
            f"{notice_path.name}=+{added_notice_count:,} (누적 {merged_notice_count:,})"
        )

        all_openg_rows.extend(month_openg)
        all_notice_rows.extend(month_notice)

    stats["openg_rows"] = len(all_openg_rows)
    stats["notice_rows"] = len(all_notice_rows)

    openg_df = pd.DataFrame(all_openg_rows)
    notice_df = pd.DataFrame(all_notice_rows)
    return openg_df, notice_df, stats


def preprocess_to_master(
    openg_df: pd.DataFrame,
    notice_df: pd.DataFrame,
    *,
    session: Optional[Session] = None,
    service_key: str = "",
    num_of_rows: int = 100,
    delay_sec: float = 0.03,
    allowed_regions: Optional[List[str]] = None,
    min_participants: int = 5,
    target_company_terms: Optional[List[str]] = None,
    exclude_retired_company: bool = True,
) -> Tuple[pd.DataFrame, Dict]:
    """raw -> 프로젝트 스키마 변환 + 계산 필드 생성"""
    if openg_df.empty:
        return pd.DataFrame(columns=TARGET_SCHEMA_COLUMNS), {"clean_rows": 0}

    notice = notice_df.copy()
    openg = openg_df.copy()

    # notice 키/보강필드 정리
    for c in [
        "bidNtceNo",
        "bidNtceOrd",
        "sucsfbidLwltRate",
        "bssamt",
        "asignBdgtAmt",
        "bdgtAmt",
        "presmptPrce",
        "mainCnsttyPresmptPrce",
        "prtcptLmtRgnNm",
        "rgnLmtBidLocplcJdgmBssNm",
        "incntvRgnNm1",
        "incntvRgnNm2",
        "incntvRgnNm3",
        "incntvRgnNm4",
    ]:
        if c not in notice.columns:
            notice[c] = None

    notice["_bid_key"] = (
        notice["bidNtceNo"].astype(str).str.strip() + "|" + notice["bidNtceOrd"].astype(str).str.strip()
    )

    # 최신 입력일 우선
    if "inptDt" in notice.columns:
        notice["_inpt_dt"] = pd.to_datetime(notice["inptDt"], errors="coerce")
        notice = notice.sort_values("_inpt_dt", ascending=False)

    notice = notice.drop_duplicates(subset=["_bid_key"], keep="first")

    notice_map: Dict[str, Dict] = {
        k: v for k, v in notice.set_index("_bid_key").to_dict("index").items()
    }

    # 개찰결과도 공고 단위 1건으로 축약(중복 키워드/오퍼레이션 조회 대응)
    for c in ["bidNtceNo", "bidNtceOrd", "bidClsfcNo", "rbidNo", "inptDt"]:
        if c not in openg.columns:
            openg[c] = None

    openg["_bid_key"] = (
        openg["bidNtceNo"].astype(str).str.strip()
        + "|"
        + openg["bidNtceOrd"].astype(str).str.strip()
    )
    if "inptDt" in openg.columns:
        openg["_inpt_dt"] = pd.to_datetime(openg["inptDt"], errors="coerce")
        openg = openg.sort_values("_inpt_dt", ascending=False)
    openg = openg.drop_duplicates(subset=["_bid_key"], keep="first")

    detail_cache: Dict[Tuple[str, str, str, str], List[Dict]] = {}
    detail_fetch_success = 0
    detail_fetch_failed = 0
    detail_fallback_used = 0
    detail_rows_total = 0

    rows: List[Dict] = []

    total_openg = int(len(openg))
    for row_i, (_, item) in enumerate(openg.iterrows(), start=1):
        if row_i == 1 or row_i % 25 == 0 or row_i == total_openg:
            _log(
                f"[전처리] 개찰완료상세 {row_i}/{total_openg} "
                f"성공={detail_fetch_success} 실패={detail_fetch_failed}"
            )
        bid_no = str(item.get("bidNtceNo", "")).strip()
        bid_ord = str(item.get("bidNtceOrd", "")).strip()
        bid_key = f"{bid_no}|{bid_ord}"
        add = notice_map.get(bid_key, {})

        # 1순위 편향 방지: 개찰완료상세 API를 우선 사용(전체 참여업체 수집)
        corp_list: List[Dict] = []
        if session is not None and service_key:
            bid_clsfc_no = clean_text(item.get("bidClsfcNo")) or "0"
            rbid_no = clean_text(item.get("rbidNo")) or "000"
            detail_key = (bid_no, bid_ord, bid_clsfc_no, rbid_no)

            if detail_key not in detail_cache:
                params = {
                    "bidNtceNo": bid_no,
                    "bidNtceOrd": bid_ord,
                    "bidClsfcNo": bid_clsfc_no,
                    "rbidNo": rbid_no,
                    "type": "json",
                }
                try:
                    detail_items = fetch_paged(
                        session=session,
                        base_url=SCSBID_BASE,
                        operation=OPEN_COMPLETED_DETAIL_OPERATION,
                        service_key=service_key,
                        base_params=params,
                        num_of_rows=num_of_rows,
                        delay_sec=delay_sec,
                    )
                    detail_cache[detail_key] = detail_items
                    detail_fetch_success += 1
                except Exception as exc:
                    detail_cache[detail_key] = []
                    detail_fetch_failed += 1
                    _log(
                        f"[WARN] 개찰완료상세 조회 실패 "
                        f"{normalize_bid_no(bid_no, bid_ord)}: {exc}"
                    )

            detail_items = detail_cache.get(detail_key, [])
            for detail in detail_items:
                company = clean_text(
                    detail.get("prcbdrNm") or detail.get("bidprcCorpNm")
                )
                if not company:
                    continue
                corp_list.append(
                    {
                        "rank": to_rank_int(detail.get("opengRank"), 0),
                        "company": company,
                        "bizno": clean_text(
                            detail.get("prcbdrBizno") or detail.get("bidprcCorpBizrno")
                        ),
                        "ceo": clean_text(
                            detail.get("prcbdrCeoNm")
                            or detail.get("bidprcCorpRprsntvNm")
                        ),
                        "bid_amount": parse_money(detail.get("bidprcAmt")),
                        "bid_rate": parse_float(detail.get("bidprcrt")),
                        "bid_datetime": clean_text(detail.get("bidprcDt")),
                    }
                )

            corp_list = sorted(
                corp_list,
                key=lambda c: (
                    0 if int(c.get("rank", 0) or 0) > 0 else 1,
                    int(c.get("rank", 0) or 10**9),
                    c.get("company", ""),
                ),
            )
            for idx, corp in enumerate(corp_list, start=1):
                if to_rank_int(corp.get("rank"), 0) <= 0:
                    corp["rank"] = idx
            detail_rows_total += len(corp_list)

        # 상세 실패/부재 시에만 opengCorpInfo fallback
        if not corp_list:
            corp_list = split_openg_corp_info(str(item.get("opengCorpInfo", "")))
            if corp_list:
                detail_fallback_used += 1
        if not corp_list:
            continue

        open_dt = str(item.get("opengDt", "")).strip()
        inpt_dt = str(item.get("inptDt", "")).strip()

        bssamt = None
        for amount_key in (
            "bssamt",
            "asignBdgtAmt",
            "bdgtAmt",
            "presmptPrce",
            "mainCnsttyPresmptPrce",
        ):
            parsed_amount = parse_money(add.get(amount_key))
            if parsed_amount not in (None, 0):
                bssamt = parsed_amount
                break
        if bssamt in (None, 0):
            for amount_key in (
                "bssamt",
                "asignBdgtAmt",
                "bdgtAmt",
                "presmptPrce",
                "mainCnsttyPresmptPrce",
            ):
                parsed_amount = parse_money(item.get(amount_key))
                if parsed_amount not in (None, 0):
                    bssamt = parsed_amount
                    break

        low_rate = parse_float(add.get("sucsfbidLwltRate") or item.get("sucsfbidLwltRate"))
        if low_rate in (None, 0):
            low_rate = parse_float(add.get("bidPrceEvlRt"))

        # 사정률은 공고 단위 1개 값으로 산출
        expected_price_for_bid = None
        for expected_key in (
            "plnprc",
            "presmptPrce",
            "mainCnsttyPresmptPrce",
        ):
            parsed_expected = parse_money(add.get(expected_key))
            if parsed_expected not in (None, 0):
                expected_price_for_bid = parsed_expected
                break
        if expected_price_for_bid in (None, 0):
            for expected_key in ("plnprc", "presmptPrce", "mainCnsttyPresmptPrce"):
                parsed_expected = parse_money(item.get(expected_key))
                if parsed_expected not in (None, 0):
                    expected_price_for_bid = parsed_expected
                    break
        if expected_price_for_bid in (None, 0):
            for corp in sorted(
                corp_list, key=lambda c: int(c.get("rank", 10**9) or 10**9)
            ):
                bid_amt = corp.get("bid_amount")
                bid_rate = corp.get("bid_rate")
                if bid_amt is None or bid_rate in (None, 0):
                    continue
                expected_price_for_bid = bid_amt / (bid_rate / 100.0)
                break

        auction_adj_rate_for_bid = None
        if expected_price_for_bid is not None and bssamt not in (None, 0):
            auction_adj_rate_for_bid = (expected_price_for_bid / bssamt) * 100.0

        region_raw = ""
        for region_candidate in (
            add.get("prtcptPsblRgnNm"),
            add.get("prtcptLmtRgnNm"),
            add.get("rgnLmtBidLocplcJdgmBssNm"),
            item.get("prtcptPsblRgnNm"),
            item.get("prtcptLmtRgnNm"),
            item.get("regionNames"),
        ):
            cleaned = clean_text(region_candidate)
            if cleaned:
                region_raw = cleaned
                break
        if not region_raw:
            incentive_regions = [
                clean_text(add.get(f"incntvRgnNm{i}"))
                for i in range(1, 5)
            ]
            incentive_regions = [r for r in incentive_regions if r]
            if incentive_regions:
                region_raw = ",".join(sorted(set(incentive_regions)))
        if not region_raw:
            unrestricted_nationwide = any(
                clean_text(flag).upper() == "N"
                for flag in (
                    add.get("rgnLmtYn"),
                    add.get("bidPrtcptLmtYn"),
                    add.get("cmmnSpldmdCorpRgnLmtYn"),
                    add.get("rgnLmtBidLocplcJdgmBssCd"),
                    item.get("rgnLmtYn"),
                    item.get("bidPrtcptLmtYn"),
                    item.get("cmmnSpldmdCorpRgnLmtYn"),
                    item.get("rgnLmtBidLocplcJdgmBssCd"),
                )
            )
            if unrestricted_nationwide:
                region_raw = "전국"

        participant_count = int(parse_float(item.get("prtcptCnum")) or len(corp_list))

        for corp in corp_list:
            bid_amt = corp.get("bid_amount")
            bid_rate = corp.get("bid_rate")  # 예가대비투찰률

            expected_price = expected_price_for_bid

            base_bid_rate = None
            if bid_amt is not None and bssamt not in (None, 0):
                base_bid_rate = (bid_amt / bssamt) * 100.0

            auction_adj_rate = auction_adj_rate_for_bid

            # 핵심 계산: 기초대비사정률(%)
            # 기본식: 투찰금액 / (기초금액 * (투찰률/100)) * 100
            base_adj_rate = None
            if bid_amt is not None and bssamt not in (None, 0) and low_rate not in (None, 0):
                base_adj_rate = (bid_amt / (bssamt * (low_rate / 100.0))) * 100.0
            elif auction_adj_rate is not None:
                # 낙찰하한율 부재시 보조식
                base_adj_rate = auction_adj_rate

            issuer = str(
                add.get("dminsttNm")
                or add.get("ntceInsttNm")
                or item.get("dminsttNm")
                or item.get("ntceInsttNm")
                or ""
            ).strip()

            row = {
                "공고번호": normalize_bid_no(bid_no, bid_ord),
                "공사명": str(item.get("bidNtceNm", "")).strip(),
                "발주처": issuer,
                "지역제한": str(region_raw).strip(),
                "개찰일시": open_dt,
                "기초금액": fmt_won(bssamt),
                "투찰률": fmt_bid_rate(low_rate),
                "예정가격": fmt_won(expected_price),
                "사정률": round(auction_adj_rate, 5) if auction_adj_rate is not None else None,
                "참여업체수": participant_count,
                "순위": to_rank_int(corp.get("rank"), 0),
                "업체명": canonicalize_bid_company_name(str(corp.get("company", ""))),
                "투찰금액": fmt_bid_money(bid_amt),
                "예가대비투찰률(%)": round(bid_rate, 3) if bid_rate is not None else None,
                "기초대비투찰률(%)": round(base_bid_rate, 3) if base_bid_rate is not None else None,
                "기초대비사정률(%)": round(base_adj_rate, 5) if base_adj_rate is not None else None,
                "투찰일시": clean_text(corp.get("bid_datetime")) or inpt_dt or open_dt,
            }
            rows.append(row)

    df = pd.DataFrame(rows)

    if df.empty:
        return pd.DataFrame(columns=TARGET_SCHEMA_COLUMNS), {"clean_rows": 0}

    # 품질 필터
    df = df[df["공고번호"].astype(str).str.strip() != ""].copy()
    df = df[df["업체명"].astype(str).str.strip() != ""].copy()
    df = df.drop_duplicates(subset=["공고번호", "업체명", "순위", "투찰금액"], keep="first")

    # 2차 전처리 규칙(기본 적용):
    # - 지역제한: 전국(명시된 전국), 경북
    # - 공고당 업체수: 5개 이상
    # - 공고 내 문화재연구원/문화유산연구원 참여 1건 이상
    # - 폐업/제외업체(한국문화재연구원, 효림) 제외
    allowed_regions = [normalize_region(x) for x in (allowed_regions or DEFAULT_ALLOWED_REGIONS) if clean_text(x)]
    target_company_terms = [canonicalize_bid_company_name(x) for x in (target_company_terms or DEFAULT_TARGET_COMPANY_TERMS) if clean_text(x)]
    before_filter_rows = int(len(df))
    before_filter_notices = int(df["공고번호"].astype(str).nunique())

    retired_removed_rows = 0
    if exclude_retired_company:
        retired_mask = df["업체명"].astype(str).apply(is_retired_company)
        retired_removed_rows = int(retired_mask.sum())
        if retired_removed_rows:
            df = df[~retired_mask].copy()

    if not df.empty:
        work = df.copy()
        work["_region_norm"] = work["지역제한"].astype(str).map(normalize_region)
        work["_is_target_company"] = work["업체명"].astype(str).apply(lambda x: has_target_company(x, target_company_terms))

        notice_agg = (
            work.groupby("공고번호", dropna=False)
            .agg(
                region_norm=("_region_norm", "first"),
                participant_count=("업체명", "size"),
                target_count=("_is_target_company", "sum"),
            )
            .reset_index()
        )
        notice_agg["region_ok"] = notice_agg["region_norm"].astype(str).apply(
            lambda x: has_allowed_region(x, allowed_regions)
        )
        notice_agg["participants_ok"] = notice_agg["participant_count"] >= max(1, int(min_participants))
        notice_agg["target_ok"] = notice_agg["target_count"] >= 1
        notice_agg["keep"] = (
            notice_agg["region_ok"]
            & notice_agg["participants_ok"]
            & notice_agg["target_ok"]
        )

        excluded_region_notices = int((~notice_agg["region_ok"]).sum())
        excluded_participants_notices = int((~notice_agg["participants_ok"]).sum())
        excluded_target_notices = int((~notice_agg["target_ok"]).sum())

        keep_notice_set = set(notice_agg.loc[notice_agg["keep"], "공고번호"].astype(str))
        df = work[work["공고번호"].astype(str).isin(keep_notice_set)].copy()
        if "_region_norm" in df.columns:
            df.drop(columns=["_region_norm"], inplace=True, errors="ignore")
        if "_is_target_company" in df.columns:
            df.drop(columns=["_is_target_company"], inplace=True, errors="ignore")
    else:
        excluded_region_notices = 0
        excluded_participants_notices = 0
        excluded_target_notices = 0

    if not df.empty:
        notice_counts = df.groupby("공고번호")["업체명"].size()
        df["참여업체수"] = df["공고번호"].map(notice_counts).astype(int)
        df = df.sort_values(["공고번호", "순위", "업체명"], kind="stable")

    # 스키마 순서 맞춤
    for col in TARGET_SCHEMA_COLUMNS:
        if col not in df.columns:
            df[col] = None
    df = df[TARGET_SCHEMA_COLUMNS]

    stats = {
        "clean_rows": int(len(df)),
        "region_counts": df["지역제한"].astype(str).str.strip().value_counts().to_dict(),
        "bucket_counts": df["지역제한"].apply(region_bucket).value_counts().to_dict(),
        "unique_notice_count": int(df["공고번호"].astype(str).nunique()),
        "detail_fetch_success": int(detail_fetch_success),
        "detail_fetch_failed": int(detail_fetch_failed),
        "detail_fallback_used": int(detail_fallback_used),
        "detail_rows_total": int(detail_rows_total),
        "filter_rows_before": int(before_filter_rows),
        "filter_rows_after": int(len(df)),
        "filter_notices_before": int(before_filter_notices),
        "filter_notices_after": int(df["공고번호"].astype(str).nunique()),
        "filter_allowed_regions": allowed_regions,
        "filter_min_participants": int(min_participants),
        "filter_target_company_terms": target_company_terms,
        "filter_excluded_region_notices": int(excluded_region_notices),
        "filter_excluded_participants_notices": int(excluded_participants_notices),
        "filter_excluded_target_notices": int(excluded_target_notices),
        "filter_retired_removed_rows": int(retired_removed_rows),
    }
    return df, stats


def save_outputs(df: pd.DataFrame, output_dir: Path) -> Dict:
    clean_dir = output_dir / "clean"
    clean_dir.mkdir(parents=True, exist_ok=True)

    all_path = clean_dir / "전체통합데이터_api수집.xlsx"
    nationwide_path = clean_dir / "전체통합데이터_api수집_전국.xlsx"
    dg_gb_path = clean_dir / "전체통합데이터_api수집_경북.xlsx"

    df.to_excel(all_path, index=False)

    nationwide_df = df[
        df["지역제한"].astype(str).apply(
            lambda x: has_allowed_region(
                x,
                ["전국"],
                include_nationwide_override=True,
            )
        )
    ].copy()
    dg_gb_df = df[
        df["지역제한"].astype(str).apply(
            lambda x: has_allowed_region(
                x,
                ["경북"],
                include_nationwide_override=False,
            )
        )
    ].copy()

    nationwide_df.to_excel(nationwide_path, index=False)
    dg_gb_df.to_excel(dg_gb_path, index=False)

    return {
        "all": str(all_path),
        "nationwide": str(nationwide_path),
        "gyeongbuk": str(dg_gb_path),
        "all_rows": int(len(df)),
        "nationwide_rows": int(len(nationwide_df)),
        "gyeongbuk_rows": int(len(dg_gb_df)),
    }


def run_pipeline(config: PipelineConfig) -> Dict:
    _log("=== 나라장터 OpenAPI 월별 수집 파이프라인 시작 ===")
    _log(f"기간: {config.start_month} ~ {config.end_month}")
    _log(f"키워드: {config.keywords if config.keywords else '[없음]'}")
    _log(f"오퍼레이션: {config.operations}")
    _log(
        "전처리 규칙: "
        f"지역={config.allowed_regions}, "
        f"최소업체수={config.min_participants}, "
        f"대상업체키워드={config.target_company_terms}, "
        f"폐업제외={config.exclude_retired_company}"
    )

    config.output_dir.mkdir(parents=True, exist_ok=True)

    session = build_session()
    openg_df, notice_df, raw_stats = collect_raw(config, session=session)
    _log(f"수집 완료: openg={len(openg_df):,}, notice={len(notice_df):,}")

    clean_df, clean_stats = preprocess_to_master(
        openg_df,
        notice_df,
        session=session,
        service_key=config.service_key,
        num_of_rows=config.num_of_rows,
        delay_sec=config.request_delay_sec,
        allowed_regions=config.allowed_regions,
        min_participants=config.min_participants,
        target_company_terms=config.target_company_terms,
        exclude_retired_company=config.exclude_retired_company,
    )
    _log(f"전처리 완료: clean_rows={len(clean_df):,}")

    out_stats = save_outputs(clean_df, config.output_dir)

    report = {
        "timestamp": _ts(),
        "config": {
            "start_month": config.start_month,
            "end_month": config.end_month,
            "start_datetime": config.start_datetime,
            "end_datetime": config.end_datetime,
            "keywords": config.keywords,
            "operations": config.operations,
            "num_of_rows": config.num_of_rows,
            "allowed_regions": config.allowed_regions,
            "min_participants": config.min_participants,
            "target_company_terms": config.target_company_terms,
            "exclude_retired_company": config.exclude_retired_company,
        },
        "raw_stats": raw_stats,
        "clean_stats": clean_stats,
        "output": out_stats,
    }

    report_path = config.output_dir / "report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    _log("=== 파이프라인 완료 ===")
    _log(f"report: {report_path}")
    _log(f"전체: {out_stats['all']} ({out_stats['all_rows']:,}건)")
    _log(f"전국: {out_stats['nationwide']} ({out_stats['nationwide_rows']:,}건)")
    _log(f"경북: {out_stats['gyeongbuk']} ({out_stats['gyeongbuk_rows']:,}건)")

    return report


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="나라장터 OpenAPI 월별 수집 + 전처리")
    parser.add_argument("--start-month", required=True, help="시작월 YYYY-MM")
    parser.add_argument("--end-month", required=True, help="종료월 YYYY-MM")
    parser.add_argument("--start-datetime", default=None, help="시작시각 YYYYMMDDHHMM (옵션)")
    parser.add_argument("--end-datetime", default=None, help="종료시각 YYYYMMDDHHMM (옵션)")
    parser.add_argument(
        "--keywords",
        default="",
        help="공고명 검색 키워드(콤마구분). 예: 문화재연구원,문화재연구소,문화유산",
    )
    parser.add_argument(
        "--operations",
        default="servc",
        help="조회 오퍼레이션 그룹(콤마구분, 기본: servc)",
    )
    parser.add_argument("--num-of-rows", type=int, default=100, help="페이지당 조회 건수")
    parser.add_argument("--delay-sec", type=float, default=0.03, help="요청 간 지연(초)")
    parser.add_argument(
        "--allowed-regions",
        default="전국,경북",
        help="허용 지역 토큰(콤마구분). 예: 전국,경북",
    )
    parser.add_argument(
        "--min-participants",
        type=int,
        default=5,
        help="공고당 최소 참여업체 수(기본 5)",
    )
    parser.add_argument(
        "--target-company-terms",
        default="문화재연구원",
        help="공고 유지 판정 대상 업체 키워드(콤마구분)",
    )
    parser.add_argument(
        "--include-retired-company",
        action="store_true",
        help="폐업/제외업체(한국문화재연구원, 효림) 제외 규칙 비활성화",
    )
    parser.add_argument("--env-path", default=str(DEFAULT_ENV_PATH), help="서비스키 파일 경로")
    parser.add_argument("--output-dir", default=str(DEFAULT_OUTPUT_DIR), help="출력 디렉터리")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if bool(args.start_datetime) != bool(args.end_datetime):
        raise ValueError("--start-datetime/--end-datetime는 함께 지정해야 합니다.")
    if args.start_datetime and args.end_datetime:
        start_dt = datetime.strptime(args.start_datetime, "%Y%m%d%H%M")
        end_dt = datetime.strptime(args.end_datetime, "%Y%m%d%H%M")
        if start_dt > end_dt:
            raise ValueError("start_datetime은 end_datetime보다 작거나 같아야 합니다.")

    keywords = [k.strip() for k in args.keywords.split(",") if k.strip()]
    operations = [op.strip() for op in args.operations.split(",") if op.strip()]
    allowed_regions = [normalize_region(x) for x in str(args.allowed_regions).split(",") if clean_text(x)]
    target_company_terms = [
        canonicalize_bid_company_name(x)
        for x in str(args.target_company_terms).split(",")
        if clean_text(x)
    ]

    for op in operations:
        if op not in OPEN_RESULT_OPS:
            raise ValueError(f"지원하지 않는 operation: {op}")
    if args.min_participants <= 0:
        raise ValueError("--min-participants는 1 이상이어야 합니다.")

    service_key = load_service_key(Path(args.env_path))

    cfg = PipelineConfig(
        service_key=service_key,
        start_month=args.start_month,
        end_month=args.end_month,
        start_datetime=args.start_datetime,
        end_datetime=args.end_datetime,
        keywords=keywords,
        operations=operations,
        num_of_rows=args.num_of_rows,
        request_delay_sec=args.delay_sec,
        output_dir=Path(args.output_dir),
        allowed_regions=allowed_regions or list(DEFAULT_ALLOWED_REGIONS),
        min_participants=int(args.min_participants),
        target_company_terms=target_company_terms or list(DEFAULT_TARGET_COMPANY_TERMS),
        exclude_retired_company=not bool(args.include_retired_company),
    )

    run_pipeline(cfg)


if __name__ == "__main__":
    main()
