#!/usr/bin/env python3
"""나라장터 API 후보 수집 + 후보 검증 + 공고별 상세 파일 생성."""
from __future__ import annotations

import argparse
import json
import re
from collections import Counter, deque
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path
import sys
from typing import Any, Dict, Iterable, List, Mapping, Sequence, Set, Tuple

import pandas as pd

from fetch_keyword_bids_api_only import (
    BID_NOTICE_SEARCH_API,
    OPEN_RESULT_BY_BID_API,
    OPEN_COMPLETED_API,
    PREPAR_PC_DETAIL_API,
    PublicDataClient,
    BidKey,
    choose_notice_item_by_ord,
    compute_bidder_metrics,
    dedupe_keywords,
    extract_region_name,
    format_bid_no_for_output,
    load_service_keys,
    normalize_ord,
    round_half_up,
    to_float,
)

PROJECT_ROOT = Path(__file__).resolve().parents[2]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from nara_workspace_paths import default_env_path, first_stage_dir

DEFAULT_RULE_PROFILE_PATH = PROJECT_ROOT / ".agents" / "skills" / "API" / "rule_profile.json"

RAW_CANDIDATE_COLUMNS = [
    "공고번호",
    "공고명",
    "개찰일시",
    "키워드일치",
    "키워드구분",
    "공고종류",
    "서비스구분",
    "조달대분류",
    "조달중분류",
    "조달분류명",
    "예가방식",
]

AUDIT_COLUMNS = [
    "공고번호",
    "공고명",
    "개찰일시",
    "키워드일치",
    "키워드구분",
    "공고종류",
    "서비스구분",
    "조달대분류",
    "조달중분류",
    "조달분류명",
    "예가방식",
    "지역제한",
    "업체수",
    "문화재연구원참여수",
    "처리상태",
    "처리사유",
    "파일",
    "오류",
]

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
    "사업자번호",
    "대표자",
    "비고",
    "키워드일치",
]

NOTICE_KIND_FIELDS = ("ntceKindNm", "ntceKindCd")
SERVICE_FIELDS = (
    "srvceDivNm",
    "bidNtceNm",
    "pubPrcrmntLrgClsfcNm",
    "pubPrcrmntMidClsfcNm",
    "pubPrcrmntClsfcNm",
)
CLASSIFICATION_FIELDS = (
    "pubPrcrmntLrgClsfcNm",
    "pubPrcrmntMidClsfcNm",
    "pubPrcrmntClsfcNm",
)


@dataclass(frozen=True)
class RuleProfile:
    primary_keywords: Tuple[str, ...]
    recovery_keywords: Tuple[str, ...]
    domain_context_tokens: Tuple[str, ...]
    required_service_tokens: Tuple[str, ...]
    excluded_service_tokens: Tuple[str, ...]
    excluded_notice_kind_tokens: Tuple[str, ...]
    allowed_regions: Tuple[str, ...]
    mixed_region_policy: str
    min_participants: int
    target_company_terms: Tuple[str, ...]
    retired_company_tokens: Tuple[str, ...]

    @property
    def default_search_keywords(self) -> List[str]:
        return dedupe_keywords(list(self.primary_keywords) + list(self.recovery_keywords))


@dataclass
class SearchStats:
    token: str
    calls: int = 0
    splits: int = 0
    items: int = 0


@dataclass
class CandidateDecision:
    status: str
    reason: str
    keyword_class: str


@dataclass
class DetailResult:
    rows: List[Dict[str, Any]]
    status: str
    reason: str
    region_name: str
    participant_count: int
    target_participant_count: int


def normalize_text(value: Any) -> str:
    return re.sub(r"\s+", "", str(value or "")).strip()


def parse_list(csv_text: str) -> List[str]:
    return [x.strip() for x in str(csv_text or "").split(",") if x.strip()]


def load_rule_profile(path: str | Path | None = None) -> RuleProfile:
    profile_path = Path(path or DEFAULT_RULE_PROFILE_PATH)
    if not profile_path.exists():
        raise FileNotFoundError(f"규칙 프로필 파일이 없습니다: {profile_path}")

    payload = json.loads(profile_path.read_text(encoding="utf-8"))
    return RuleProfile(
        primary_keywords=tuple(parse_list(",".join(payload.get("primary_keywords", [])))),
        recovery_keywords=tuple(parse_list(",".join(payload.get("recovery_keywords", [])))),
        domain_context_tokens=tuple(parse_list(",".join(payload.get("domain_context_tokens", [])))),
        required_service_tokens=tuple(parse_list(",".join(payload.get("required_service_tokens", [])))),
        excluded_service_tokens=tuple(parse_list(",".join(payload.get("excluded_service_tokens", [])))),
        excluded_notice_kind_tokens=tuple(parse_list(",".join(payload.get("excluded_notice_kind_tokens", [])))),
        allowed_regions=tuple(parse_list(",".join(payload.get("allowed_regions", [])))),
        mixed_region_policy=str(payload.get("mixed_region_policy", "exclude_non_nationwide_multi_region")),
        min_participants=int(payload.get("min_participants", 5) or 5),
        target_company_terms=tuple(parse_list(",".join(payload.get("target_company_terms", [])))),
        retired_company_tokens=tuple(parse_list(",".join(payload.get("retired_company_tokens", [])))),
    )


def normalize_notice_id_variants(raw: Any) -> List[str]:
    text = str(raw or "").strip().replace(" ", "")
    if not text:
        return []

    if "-" not in text:
        return [text, f"{text}-00", f"{text}-000"]

    base, suffix = text.rsplit("-", 1)
    if not suffix.isdigit():
        return [text]

    norm_suffix = suffix.zfill(3) if base.startswith("R") else suffix.zfill(2)
    if norm_suffix == suffix:
        return [f"{base}-{norm_suffix}"]
    return [f"{base}-{suffix}", f"{base}-{norm_suffix}"]


def load_known_notice_ids(path: str) -> Set[str]:
    source = Path(path)
    if not source.exists():
        raise FileNotFoundError(f"기준 공고 파일이 없습니다: {source}")

    notice_ids: Set[str] = set()

    if source.suffix.lower() in {".xls", ".xlsx"}:
        df = pd.read_excel(source, engine="openpyxl")
        for col in ["공고번호", "bidNtceNo", "notice_no"]:
            if col in df.columns:
                raw_values = df[col]
                break
        else:
            raise ValueError("기준 파일에 공고번호 컬럼이 없습니다.")

        for raw in raw_values.fillna("").astype(str):
            for item in normalize_notice_id_variants(raw):
                notice_ids.add(item)
        return notice_ids

    for line in source.read_text(encoding="utf-8", errors="ignore").splitlines():
        raw = line.strip()
        if not raw:
            continue
        for token in re.split(r"[,\s]+", raw):
            for item in normalize_notice_id_variants(token):
                notice_ids.add(item)
    return notice_ids


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


def build_month_windows(start: datetime, end: datetime) -> List[Tuple[datetime, datetime]]:
    windows: List[Tuple[datetime, datetime]] = []
    cursor = start
    while cursor <= end:
        window_end = min(end, cursor + timedelta(days=30) - timedelta(minutes=1))
        windows.append((cursor, window_end))
        cursor = window_end + timedelta(minutes=1)
    return windows


def normalize_company_name(value: Any) -> str:
    text = str(value or "").strip()
    if not text:
        return ""

    patterns = [
        r"\(재\)",
        r"\(재단\)",
        "재단법인",
        "재단",
        r"\(주\)",
        "주식회사",
        r"\(사\)",
        "사단법인",
        r"\(유\)",
        "유한회사",
        r"\(합\)",
        "합명회사",
        r"\(합자\)",
        "합자회사",
    ]
    for pattern in patterns:
        text = re.sub(pattern, "", text)

    text = re.sub(r"문화유산연구원", "문화재연구원", text)
    text = re.sub(r"문화재보존센터", "문화재연구원", text)
    text = re.sub(r"고고학연구소", "문화재연구원", text)
    if "문화유산마을" in text and "문화재연구원" not in text:
        text = "문화유산마을 문화재연구원"

    text = re.sub(r"[()（）]+", " ", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text


def sanitize_filename(name: str) -> str:
    return re.sub(r"[<>:\"/\\|?*]", "_", name)


def normalize_region_tokens(region_name: Any) -> List[str]:
    text = str(region_name or "").strip()
    if not text:
        return []

    normalized = (
        text.replace("\r", "")
        .replace("\n", "")
        .replace("대구광역시", "대구")
        .replace("경상북도", "경북")
        .replace("경상북", "경북")
        .replace(" ", "")
        .replace("/", ",")
        .replace(";", ",")
        .replace("|", ",")
        .replace("·", ",")
        .replace("ㆍ", ",")
    )

    tokens: List[str] = []
    for token in normalized.split(","):
        token = token.strip()
        if not token:
            continue
        if "전국" in token:
            bucket = "전국"
        elif "대구" in token:
            bucket = "대구"
        elif "경북" in token:
            bucket = "경북"
        else:
            bucket = token
        if bucket not in tokens:
            tokens.append(bucket)
    return tokens


def is_allowed_region(region_name: Any, allowed_regions: Iterable[str]) -> bool:
    tokens = normalize_region_tokens(region_name)
    allowed = {normalize_text(x) for x in allowed_regions if normalize_text(x)}
    if not tokens:
        return False
    if "전국" in tokens:
        return "전국" in allowed
    return any(normalize_text(token) in allowed for token in tokens)


def is_mixed_region(region_name: Any, allowed_regions: Iterable[str]) -> bool:
    tokens = normalize_region_tokens(region_name)
    if not tokens or "전국" in tokens:
        return False
    allowed = {normalize_text(x) for x in allowed_regions if normalize_text(x)}
    allowed_specific = [token for token in tokens if normalize_text(token) in allowed and normalize_text(token) != "전국"]
    return len(set(allowed_specific)) > 1


def count_target_participants(bidders: Sequence[Dict[str, Any]], target_terms: Sequence[str]) -> int:
    matched = 0
    for bidder in bidders:
        raw_company = str(bidder.get("prcbdrNm") or "").strip()
        normalized = normalize_company_name(raw_company)
        if any(term in raw_company or term in normalized for term in target_terms if term):
            matched += 1
    return matched


def adaptive_search_by_keyword(
    client: PublicDataClient,
    keyword: str,
    start: datetime,
    end: datetime,
    max_depth: int = 14,
) -> Tuple[List[Dict[str, Any]], SearchStats]:
    stats = SearchStats(token=keyword)
    queue: deque[Tuple[datetime, datetime, int]] = deque([(start, end, 0)])
    items_all: List[Dict[str, Any]] = []

    while queue:
        sdt, edt, depth = queue.popleft()
        stats.calls += 1

        try:
            items = client.fetch_all(
                BID_NOTICE_SEARCH_API,
                {
                    "inqryDiv": "2",
                    "inqryBgnDt": sdt.strftime("%Y%m%d%H%M"),
                    "inqryEndDt": edt.strftime("%Y%m%d%H%M"),
                    "bidNtceNm": keyword,
                },
                page_size=200,
            )
            items_all.extend(items)
            continue
        except Exception as exc:  # noqa: BLE001
            msg = str(exc)
            minutes = int((edt - sdt).total_seconds() // 60)
            if "입력범위값 초과" in msg and depth < max_depth and minutes >= 120:
                half = max(1, minutes // 2)
                mid = sdt + timedelta(minutes=half)
                left_end = mid
                right_start = mid + timedelta(minutes=1)
                if right_start > edt:
                    right_start = edt
                if left_end < sdt:
                    left_end = sdt
                queue.appendleft((right_start, edt, depth + 1))
                queue.appendleft((sdt, left_end, depth + 1))
                stats.splits += 1
                continue

            print(
                "[WARN] 검색 실패",
                keyword,
                sdt.strftime("%Y%m%d%H%M"),
                edt.strftime("%Y%m%d%H%M"),
                msg,
            )

    stats.items = len(items_all)
    return items_all, stats


def gather_candidates(
    client: PublicDataClient,
    keywords: List[str],
    start_ymdhm: str,
    end_ymdhm: str,
) -> Tuple[Dict[BidKey, Dict[str, Any]], Dict[BidKey, Set[str]], Dict[str, SearchStats]]:
    start = datetime.strptime(start_ymdhm, "%Y%m%d%H%M")
    end = datetime.strptime(end_ymdhm, "%Y%m%d%H%M")
    windows = build_month_windows(start, end)

    key_to_notice: Dict[BidKey, Dict[str, Any]] = {}
    key_to_keywords: Dict[BidKey, Set[str]] = {}
    keyword_stats: Dict[str, SearchStats] = {}

    for kw in keywords:
        merged_items: List[Dict[str, Any]] = []
        total_calls = 0
        total_splits = 0

        for w_start, w_end in windows:
            items, stats = adaptive_search_by_keyword(client, kw, w_start, w_end)
            merged_items.extend(items)
            total_calls += stats.calls
            total_splits += stats.splits

        for item in merged_items:
            bid_no = str(item.get("bidNtceNo") or "").strip()
            if not bid_no:
                continue

            key = BidKey(
                bid_no=bid_no,
                bid_ord=normalize_ord(item.get("bidNtceOrd")),
                bid_clsfc_no="0",
                rbid_no="000",
            )
            if key not in key_to_notice:
                key_to_notice[key] = item
            else:
                if parse_open_dt(item.get("opengDt")) > parse_open_dt(key_to_notice[key].get("opengDt")):
                    key_to_notice[key] = item

            key_to_keywords.setdefault(key, set()).add(kw)

        keyword_stats[kw] = SearchStats(token=kw, calls=total_calls, splits=total_splits, items=len(merged_items))
        print(
            f"키워드 [{kw}] 검색건수: {len(merged_items)} "
            f"(월분할 {len(windows)}개, API호출 {total_calls}회, 자동분할 {total_splits}회)"
        )

    return key_to_notice, key_to_keywords, keyword_stats


def determine_keyword_class(keyword_hits: Iterable[str], rules: RuleProfile) -> str:
    hits = {normalize_text(x) for x in keyword_hits if normalize_text(x)}
    primary = {normalize_text(x) for x in rules.primary_keywords}
    recovery = {normalize_text(x) for x in rules.recovery_keywords}
    has_primary = bool(hits & primary)
    has_recovery = bool(hits & recovery)
    if has_primary and has_recovery:
        return "mixed"
    if has_primary:
        return "primary"
    if has_recovery:
        return "recovery"
    return "unknown"


def _combine_notice_text(notice_item: Mapping[str, Any], fields: Sequence[str]) -> str:
    return " ".join(str(notice_item.get(field) or "").strip() for field in fields if str(notice_item.get(field) or "").strip())


def validate_candidate_scope(
    notice_item: Mapping[str, Any],
    keyword_hits: Iterable[str],
    rules: RuleProfile,
) -> CandidateDecision:
    title = str(notice_item.get("bidNtceNm") or "").strip()
    title_norm = normalize_text(title)
    notice_kind = normalize_text(_combine_notice_text(notice_item, NOTICE_KIND_FIELDS))
    service_text = _combine_notice_text(notice_item, SERVICE_FIELDS)
    service_norm = normalize_text(service_text)
    classification_text = _combine_notice_text(notice_item, CLASSIFICATION_FIELDS)
    classification_norm = normalize_text(classification_text)
    combined_norm = normalize_text(" ".join([title, service_text, classification_text]))
    keyword_class = determine_keyword_class(keyword_hits, rules)

    if any(token and normalize_text(token) in notice_kind for token in rules.excluded_notice_kind_tokens):
        return CandidateDecision("제외_취소공고", "공고종류제외", keyword_class)

    pre_method = normalize_text(notice_item.get("prearngPrceDcsnMthdNm") or "")
    if pre_method and "복수예가" not in pre_method:
        return CandidateDecision("제외_비복수예가", "예가방식불일치", keyword_class)

    has_allowed_service = any(normalize_text(token) in service_norm or normalize_text(token) in title_norm for token in rules.required_service_tokens)
    has_excluded_service = any(normalize_text(token) in service_norm for token in rules.excluded_service_tokens)
    if has_excluded_service and not has_allowed_service:
        if any(normalize_text(token) in service_norm for token in ("물품", "구매", "제조")):
            return CandidateDecision("제외_물품", "서비스분류불일치", keyword_class)
        return CandidateDecision("제외_일반공사", "서비스분류불일치", keyword_class)

    primary_hits = {normalize_text(x) for x in keyword_hits if normalize_text(x)} & {normalize_text(x) for x in rules.primary_keywords}
    recovery_hits = {normalize_text(x) for x in keyword_hits if normalize_text(x)} & {normalize_text(x) for x in rules.recovery_keywords}

    has_domain_context = any(normalize_text(token) in combined_norm for token in rules.domain_context_tokens)
    if primary_hits:
        domain_ok = True
    elif recovery_hits:
        domain_ok = has_domain_context
    else:
        domain_ok = has_domain_context

    if not domain_ok:
        return CandidateDecision("제외_비문화재조사용역", "도메인불일치", keyword_class)

    if not has_allowed_service:
        if "공사" in title_norm and "용역" not in title_norm:
            return CandidateDecision("제외_일반공사", "제목형태불일치", keyword_class)
        return CandidateDecision("제외_비기술용역", "서비스구분없음", keyword_class)

    if keyword_class == "recovery" and "용역" not in title_norm and "기술용역" not in service_norm:
        return CandidateDecision("제외_일반공사", "복구키워드오탐", keyword_class)

    if not (primary_hits or recovery_hits or has_domain_context):
        return CandidateDecision("제외_비문화재조사용역", "키워드불일치", keyword_class)

    return CandidateDecision("유지_후보", "후보통과", keyword_class)


def build_raw_candidate_row(notice_key: str, notice_item: Mapping[str, Any], keyword_hit: str, keyword_class: str) -> Dict[str, Any]:
    return {
        "공고번호": notice_key,
        "공고명": notice_item.get("bidNtceNm") or "",
        "개찰일시": notice_item.get("opengDt") or "",
        "키워드일치": keyword_hit,
        "키워드구분": keyword_class,
        "공고종류": notice_item.get("ntceKindNm") or "",
        "서비스구분": notice_item.get("srvceDivNm") or "",
        "조달대분류": notice_item.get("pubPrcrmntLrgClsfcNm") or "",
        "조달중분류": notice_item.get("pubPrcrmntMidClsfcNm") or "",
        "조달분류명": notice_item.get("pubPrcrmntClsfcNm") or "",
        "예가방식": notice_item.get("prearngPrceDcsnMthdNm") or "",
    }


def build_audit_row(base_raw: Mapping[str, Any], status: str, reason: str = "", **overrides: Any) -> Dict[str, Any]:
    row = {col: base_raw.get(col, "") for col in RAW_CANDIDATE_COLUMNS}
    row.update(
        {
            "지역제한": "",
            "업체수": 0,
            "문화재연구원참여수": 0,
            "처리상태": status,
            "처리사유": reason,
            "파일": "",
            "오류": "",
        }
    )
    row.update(overrides)
    return row


def build_rows(
    client: PublicDataClient,
    key: BidKey,
    notice_item: Dict[str, Any],
    keyword_hit: str,
    open_cache: Dict[str, List[Dict[str, Any]]],
    prepar_cache: Dict[Tuple[str, str], Dict[str, Any]],
    region_cache: Dict[Tuple[str, str], str],
    apply_filters: bool,
    rules: RuleProfile,
) -> DetailResult:
    if key.bid_no not in open_cache:
        open_cache[key.bid_no] = client.fetch_all(
            OPEN_RESULT_BY_BID_API,
            {
                "inqryDiv": "4",
                "bidNtceNo": key.bid_no,
            },
            page_size=200,
        )

    open_item = choose_notice_item_by_ord(open_cache[key.bid_no], key.bid_ord)
    if not open_item:
        return DetailResult([], "보류_개찰결과없음", "개찰결과없음", "", 0, 0)

    bid_clsfc_no = str(open_item.get("bidClsfcNo") or "0").strip() or "0"
    rbid_no = normalize_ord(open_item.get("rbidNo") or "000")

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
        for item in prep_items:
            if to_float(item.get("plnprc")) is not None or to_float(item.get("bssamt")) is not None:
                chosen = item
                break
        prepar_cache[pre_key] = {
            "plnprc": to_float(chosen.get("plnprc")),
            "bssamt": to_float(chosen.get("bssamt")),
        }

    if pre_key not in region_cache:
        region_cache[pre_key] = extract_region_name(client, key.bid_no, key.bid_ord)

    prep = prepar_cache[pre_key]
    region_name = region_cache[pre_key]
    if apply_filters:
        if not is_allowed_region(region_name, rules.allowed_regions):
            return DetailResult([], "제외_지역", "지역불일치", region_name, 0, 0)

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
    participant_count = len(bidders)
    if apply_filters and participant_count < max(1, rules.min_participants):
        return DetailResult([], "제외_참여업체수미달", "업체수미달", region_name, participant_count, 0)

    target_participant_count = count_target_participants(bidders, rules.target_company_terms)
    if apply_filters and target_participant_count < 1:
        return DetailResult([], "제외_문화재연구원참여없음", "타깃업체부재", region_name, participant_count, target_participant_count)

    low_rate = to_float(notice_item.get("sucsfbidLwltRate"))
    if low_rate is None:
        low_rate = to_float(open_item.get("sucsfbidLwltRate"))

    plnprc = prep.get("plnprc")
    bssamt = prep.get("bssamt")
    price_adjust_rate = None
    if plnprc is not None and bssamt and bssamt > 0:
        price_adjust_rate = round_half_up(plnprc / bssamt * 100, 5)

    rows: List[Dict[str, Any]] = []
    for bidder in bidders:
        bid_amt = to_float(bidder.get("bidprcAmt"))
        bidprcrt = to_float(bidder.get("bidprcrt"))
        calc = compute_bidder_metrics(
            bid_amt=bid_amt,
            bidprcrt=bidprcrt,
            plnprc=plnprc,
            bssamt=bssamt,
            low_rate=low_rate,
        )

        rank_raw = str(bidder.get("opengRank") or "").strip()
        rank = int(rank_raw) if rank_raw.lstrip("-").isdigit() else -1

        rows.append(
            {
                "공고번호": format_bid_no_for_output(key.bid_no, key.bid_ord),
                "공고명": notice_item.get("bidNtceNm") or open_item.get("bidNtceNm"),
                "발주처": notice_item.get("dminsttNm") or open_item.get("dminsttNm"),
                "지역제한": region_name,
                "순위": rank,
                "업체명": normalize_company_name(bidder.get("prcbdrNm") or ""),
                "개찰일시": open_item.get("opengDt") or notice_item.get("opengDt"),
                "투찰일시": bidder.get("bidprcDt"),
                "투찰금액": int(round_half_up(bid_amt, 0)) if bid_amt is not None else None,
                "예가대비투찰률": calc.get("예가대비투찰률"),
                "기초대비투찰률": calc.get("기초대비투찰률"),
                "기초대비사정률": calc.get("기초대비사정률"),
                "예정가격": int(round_half_up(plnprc, 0)) if plnprc is not None else None,
                "낙찰하한가": calc.get("낙찰하한가"),
                "기초금액": int(round_half_up(bssamt, 0)) if bssamt is not None else None,
                "사정률": price_adjust_rate,
                "발주처투찰률": round_half_up(low_rate, 3) if low_rate is not None else None,
                "낙찰하한가차이": calc.get("낙찰하한가차이"),
                "사업자번호": bidder.get("prcbdrBizno"),
                "대표자": bidder.get("prcbdrCeoNm"),
                "비고": bidder.get("rmrk"),
                "키워드일치": keyword_hit,
            }
        )

    rows.sort(
        key=lambda x: (
            0 if isinstance(x["순위"], int) and x["순위"] > 0 else 1,
            x["순위"] if isinstance(x["순위"], int) and x["순위"] > 0 else 10**9,
            x["투찰금액"] if x["투찰금액"] is not None else 10**15,
        )
    )

    return DetailResult(rows, "정상", "상세수집완료", region_name, participant_count, target_participant_count)


def write_jsonl(path: Path, rows: Sequence[Mapping[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as file:
        for row in rows:
            file.write(json.dumps(dict(row), ensure_ascii=False) + "\n")


def write_stage_summary(
    path: Path,
    *,
    start: str,
    end: str,
    keywords: Sequence[str],
    raw_candidate_count: int,
    audit_rows: Sequence[Mapping[str, Any]],
    keyword_stats: Mapping[str, SearchStats],
) -> None:
    counter = Counter(str(row.get("처리상태") or "") for row in audit_rows)
    payload = {
        "start": start,
        "end": end,
        "keywords": list(keywords),
        "raw_candidate_count": raw_candidate_count,
        "status_counts": dict(counter),
        "keyword_stats": {
            key: {
                "calls": value.calls,
                "splits": value.splits,
                "items": value.items,
            }
            for key, value in keyword_stats.items()
        },
    }
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="나라장터 API 공고별 개별 엑셀 생성")
    parser.add_argument("--env-file", default=str(default_env_path()))
    parser.add_argument("--start", default="202502260000", type=parse_ymdhm)
    parser.add_argument("--end", default="202602262359", type=parse_ymdhm)
    parser.add_argument("--keywords", default="")
    parser.add_argument("--output-dir", default=str(first_stage_dir("adhoc")))
    parser.add_argument("--max-notices", type=int, default=None, help="테스트용 상한")
    parser.add_argument("--no-filters", action="store_true", help="상세수집 후 전처리 필터만 비활성화 (후보 검증은 유지)")
    parser.add_argument("--rule-profile", default=str(DEFAULT_RULE_PROFILE_PATH), help="정본 규칙 프로필 JSON")
    parser.add_argument(
        "--known-notice-file",
        default="",
        help="기존 최종/증분 공고번호 목록(엑셀/텍스트)",
    )
    args = parser.parse_args()

    rules = load_rule_profile(args.rule_profile)
    cli_keywords = dedupe_keywords([x.strip() for x in args.keywords.split(",") if x.strip()])
    keywords = cli_keywords or rules.default_search_keywords
    if not keywords:
        raise ValueError("키워드가 비어있습니다.")

    known_notice_ids = set()
    if args.known_notice_file.strip():
        known_notice_ids = load_known_notice_ids(args.known_notice_file)

    service_keys = load_service_keys(Path(args.env_file))
    client = PublicDataClient(service_key=service_keys)

    print(f"수집 기간: {args.start} ~ {args.end}")
    print(f"규칙 프로필: {args.rule_profile}")
    print(f"후단 필터 모드: {'비활성화' if args.no_filters else '활성화'}")
    key_to_notice, key_to_keywords, keyword_stats = gather_candidates(client, keywords, args.start, args.end)

    ordered = list(key_to_notice.items())
    ordered.sort(
        key=lambda kv: (
            parse_open_dt(kv[1].get("opengDt")),
            kv[0].bid_no,
            kv[0].bid_ord,
        ),
        reverse=True,
    )

    if args.max_notices is not None:
        ordered = ordered[: max(0, args.max_notices)]

    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    open_cache: Dict[str, List[Dict[str, Any]]] = {}
    prepar_cache: Dict[Tuple[str, str], Dict[str, Any]] = {}
    region_cache: Dict[Tuple[str, str], str] = {}

    raw_rows: List[Dict[str, Any]] = []
    audit_rows: List[Dict[str, Any]] = []
    total = len(ordered)

    for i, (key, notice_item) in enumerate(ordered, start=1):
        notice_no = format_bid_no_for_output(key.bid_no, key.bid_ord)
        keyword_hit = ", ".join(sorted(key_to_keywords.get(key, set())))
        decision = validate_candidate_scope(notice_item, key_to_keywords.get(key, set()), rules)
        raw_row = build_raw_candidate_row(notice_no, notice_item, keyword_hit, decision.keyword_class)
        raw_rows.append(raw_row)

        if decision.status != "유지_후보":
            audit_rows.append(build_audit_row(raw_row, decision.status, decision.reason))
            print(f"[{i}/{total}] 후보제외: {notice_no} / {decision.status}")
            continue

        if any(item in known_notice_ids for item in normalize_notice_id_variants(notice_no)):
            audit_rows.append(build_audit_row(raw_row, "제외_기존데이터", "기존데이터", 처리사유="기존데이터"))
            print(f"[{i}/{total}] 스킵: {notice_no} (기존데이터)")
            continue

        try:
            detail = build_rows(
                client=client,
                key=key,
                notice_item=notice_item,
                keyword_hit=keyword_hit,
                open_cache=open_cache,
                prepar_cache=prepar_cache,
                region_cache=region_cache,
                apply_filters=not args.no_filters,
                rules=rules,
            )
            if detail.status != "정상":
                audit_rows.append(
                    build_audit_row(
                        raw_row,
                        detail.status,
                        detail.reason,
                        지역제한=detail.region_name,
                        업체수=detail.participant_count,
                        문화재연구원참여수=detail.target_participant_count,
                    )
                )
                print(
                    f"[{i}/{total}] 제외: {notice_no} / {detail.status} / 업체 {detail.participant_count}건"
                )
                continue

            file_name = f"{sanitize_filename(notice_no)}.xlsx"
            file_path = out_dir / file_name
            df = pd.DataFrame(detail.rows, columns=OUTPUT_COLUMNS)
            with pd.ExcelWriter(file_path, engine="openpyxl") as writer:
                df.to_excel(writer, sheet_name="참여업체", index=False)

            audit_rows.append(
                build_audit_row(
                    raw_row,
                    "정상",
                    detail.reason,
                    지역제한=detail.region_name,
                    업체수=detail.participant_count,
                    문화재연구원참여수=detail.target_participant_count,
                    파일=str(file_path),
                )
            )
            print(f"[{i}/{total}] 저장 완료: {file_name} / 업체 {len(df)}건")
        except Exception as exc:  # noqa: BLE001
            audit_rows.append(build_audit_row(raw_row, "오류", "상세수집오류", 오류=str(exc)))
            print(f"[{i}/{total}] 오류: {key.bid_no}-{key.bid_ord} ({exc})")

    raw_candidates_path = out_dir / "raw_candidates.jsonl"
    write_jsonl(raw_candidates_path, raw_rows)

    raw_df = pd.DataFrame(raw_rows, columns=RAW_CANDIDATE_COLUMNS)
    raw_df.to_excel(out_dir / "raw_candidates.xlsx", index=False)

    audit_df = pd.DataFrame(audit_rows, columns=AUDIT_COLUMNS)
    index_path = out_dir / "공고_파일_인덱스.xlsx"
    audit_path = out_dir / "공고_후보_감사로그.xlsx"
    with pd.ExcelWriter(index_path, engine="openpyxl") as writer:
        audit_df.to_excel(writer, sheet_name="index", index=False)
    audit_df.to_excel(audit_path, index=False)

    stage_summary_path = out_dir / "stage_summary.json"
    write_stage_summary(
        stage_summary_path,
        start=args.start,
        end=args.end,
        keywords=keywords,
        raw_candidate_count=len(raw_rows),
        audit_rows=audit_rows,
        keyword_stats=keyword_stats,
    )

    ok_count = int((audit_df["처리상태"] == "정상").sum()) if not audit_df.empty else 0
    err_count = int((audit_df["처리상태"] == "오류").sum()) if not audit_df.empty else 0
    excluded_count = len(audit_df) - ok_count - err_count if not audit_df.empty else 0

    print("완료")
    print(f"- 출력 디렉토리: {out_dir}")
    print(f"- raw 후보: {len(raw_rows)}")
    print(f"- 정상: {ok_count}")
    print(f"- 제외: {excluded_count}")
    print(f"- 오류: {err_count}")
    print(f"- raw 로그: {raw_candidates_path}")
    print(f"- 감사로그: {audit_path}")
    print(f"- 인덱스: {index_path}")
    print(f"- 요약: {stage_summary_path}")


if __name__ == "__main__":
    main()
