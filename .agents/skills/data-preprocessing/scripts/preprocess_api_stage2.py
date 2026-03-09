#!/usr/bin/env python3
"""API 1차 수집(공고별 파일) 결과를 2차 전처리해 최종 마스터를 생성한다."""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Iterable, Sequence

import pandas as pd

PROJECT_ROOT = Path(__file__).resolve().parents[4]
DEFAULT_RULE_PROFILE_PATH = PROJECT_ROOT / ".agents" / "skills" / "API" / "rule_profile.json"

BASE_COLUMNS = [
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


def load_rule_profile(path: str | Path) -> dict:
    profile_path = Path(path)
    if not profile_path.exists():
        raise FileNotFoundError(f"규칙 프로필 파일이 없습니다: {profile_path}")
    return json.loads(profile_path.read_text(encoding="utf-8"))


def normalize_company_name(value: object) -> str:
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


def parse_list(csv_text: str) -> list[str]:
    return [x.strip() for x in str(csv_text or "").split(",") if x.strip()]


def normalize_region_tokens(value: object) -> list[str]:
    text = str(value or "").strip()
    if not text:
        return []
    normalized = text.replace("\n", " ").replace("\r", " ")
    normalized = re.sub(r"\s+", "", normalized)
    normalized = normalized.replace("경상북도", "경북").replace("경상북", "경북")
    normalized = normalized.replace("대구광역시", "대구")
    normalized = normalized.replace("/", ",").replace(";", ",").replace("|", ",").replace("·", ",").replace("ㆍ", ",")
    tokens: list[str] = []
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


def has_allowed_region(region_text: str, allowed_regions: Iterable[str]) -> bool:
    tokens = normalize_region_tokens(region_text)
    if not tokens:
        return False
    allowed = {x for x in allowed_regions if x}
    if "전국" in tokens:
        return "전국" in allowed
    return any(token in allowed for token in tokens)


def is_mixed_region(region_text: str, allowed_regions: Sequence[str]) -> bool:
    tokens = normalize_region_tokens(region_text)
    if not tokens or "전국" in tokens:
        return False
    allowed = [token for token in tokens if token in allowed_regions and token != "전국"]
    return len(set(allowed)) > 1


def is_retired_company(name: str, retired_tokens: Sequence[str]) -> bool:
    if not name:
        return False
    compact = name.replace(" ", "")
    return any(token and token in compact for token in retired_tokens)


def load_notice_file(path: Path) -> pd.DataFrame:
    try:
        return pd.read_excel(path, sheet_name="참여업체")
    except Exception:
        return pd.read_excel(path)


def main() -> None:
    default_rules = load_rule_profile(DEFAULT_RULE_PROFILE_PATH)
    parser = argparse.ArgumentParser(description="API 1차 수집 결과 2차 전처리")
    parser.add_argument("--first-stage-dir", required=True, help="공고별 xlsx 폴더")
    parser.add_argument("--final-output", default="/mnt/a/c/전체_통합_데이터.xlsx")
    parser.add_argument("--audit-output", default="", help="판정로그 xlsx 경로")
    parser.add_argument("--rule-profile", default=str(DEFAULT_RULE_PROFILE_PATH), help="정본 규칙 프로필 JSON")
    parser.add_argument("--allowed-regions", default=",".join(default_rules.get("allowed_regions", ["전국", "대구", "경북"])))
    parser.add_argument("--min-participants", type=int, default=int(default_rules.get("min_participants", 5) or 5))
    parser.add_argument("--target-company-terms", default=",".join(default_rules.get("target_company_terms", ["문화재연구원"])))
    parser.add_argument("--append-existing", default="", help="기존 최종 마스터와 병합")
    args = parser.parse_args()

    rules = load_rule_profile(args.rule_profile)
    first_stage_dir = Path(args.first_stage_dir)
    if not first_stage_dir.exists():
        raise FileNotFoundError(f"1차 수집 폴더 없음: {first_stage_dir}")

    xlsx_files = sorted(
        p for p in first_stage_dir.glob("*.xlsx")
        if p.name not in {"공고_파일_인덱스.xlsx", "공고_후보_감사로그.xlsx", "raw_candidates.xlsx"}
    )

    allowed_regions = [x for x in parse_list(args.allowed_regions) if x]
    target_terms = [normalize_company_name(x) for x in parse_list(args.target_company_terms)]
    retired_tokens = [str(x).replace(" ", "") for x in rules.get("retired_company_tokens", []) if str(x).strip()]

    all_frames: list[pd.DataFrame] = []
    audit_rows: list[dict] = []

    for fp in xlsx_files:
        try:
            df = load_notice_file(fp)
        except Exception as exc:  # noqa: BLE001
            audit_rows.append({
                "파일": str(fp),
                "처리상태": "제외_파일읽기실패",
                "사유": str(exc),
                "공고번호": fp.stem,
                "지역제한": "",
                "업체수": 0,
                "문화재연구원참여수": 0,
            })
            continue

        if df.empty:
            audit_rows.append({
                "파일": str(fp),
                "처리상태": "제외_빈데이터",
                "사유": "행 없음",
                "공고번호": fp.stem,
                "지역제한": "",
                "업체수": 0,
                "문화재연구원참여수": 0,
            })
            continue

        for col in ["공고번호", "공고명", "지역제한", "업체명"]:
            if col not in df.columns:
                df[col] = ""

        notice_no = str(df.iloc[0].get("공고번호", "") or "").strip()
        notice_name = str(df.iloc[0].get("공고명", "") or "").strip()
        region_text = str(df.iloc[0].get("지역제한", "") or "").strip()

        work = df.copy()
        work["업체명"] = work["업체명"].apply(normalize_company_name)
        work = work[~work["업체명"].astype(str).apply(lambda name: is_retired_company(name, retired_tokens))].copy()
        if work.empty:
            audit_rows.append({
                "파일": str(fp),
                "처리상태": "제외_폐업업체제거후빈데이터",
                "사유": "폐업업체제외",
                "공고번호": notice_no,
                "공고명": notice_name,
                "지역제한": region_text,
                "업체수": 0,
                "문화재연구원참여수": 0,
            })
            continue

        participant_count = len(work)
        target_count = sum(
            1
            for name in work["업체명"].astype(str)
            if any(term and term in name for term in target_terms)
        )

        reasons: list[str] = []
        if not has_allowed_region(region_text, allowed_regions):
            reasons.append("지역불일치")
        if participant_count < max(1, args.min_participants):
            reasons.append("업체수미달")
        if target_count < 1:
            reasons.append("문화재연구원참여없음")

        if reasons:
            audit_rows.append({
                "파일": str(fp),
                "처리상태": "제외",
                "사유": ",".join(reasons),
                "공고번호": notice_no,
                "공고명": notice_name,
                "지역제한": region_text,
                "업체수": participant_count,
                "문화재연구원참여수": target_count,
            })
            continue

        all_frames.append(work)
        audit_rows.append({
            "파일": str(fp),
            "처리상태": "유지",
            "사유": "",
            "공고번호": notice_no,
            "공고명": notice_name,
            "지역제한": region_text,
            "업체수": participant_count,
            "문화재연구원참여수": target_count,
        })

    merged = pd.concat(all_frames, ignore_index=True) if all_frames else pd.DataFrame(columns=BASE_COLUMNS)

    if args.append_existing:
        existing_path = Path(args.append_existing)
        if existing_path.exists():
            existing = pd.read_excel(existing_path)
            merged = pd.concat([existing, merged], ignore_index=True)
        else:
            print(f"[WARN] 기존 병합 파일 없음: {existing_path}")

    for col in BASE_COLUMNS:
        if col not in merged.columns:
            merged[col] = None
    extra_cols = [c for c in merged.columns if c not in BASE_COLUMNS]
    merged = merged[BASE_COLUMNS + extra_cols]

    if not merged.empty and "공고번호" in merged.columns and "업체명" in merged.columns:
        merged = merged.drop_duplicates(subset=["공고번호", "업체명"], keep="last")

    final_output = Path(args.final_output)
    final_output.parent.mkdir(parents=True, exist_ok=True)
    merged.to_excel(final_output, index=False)

    audit_df = pd.DataFrame(audit_rows)
    audit_output = Path(args.audit_output) if args.audit_output else first_stage_dir / "2차전처리_판정로그.xlsx"
    audit_df.to_excel(audit_output, index=False)

    print("완료")
    print(f"- 규칙 프로필: {args.rule_profile}")
    print(f"- 1차 폴더: {first_stage_dir}")
    print(f"- 입력 파일 수: {len(xlsx_files)}")
    print(f"- 최종 행 수: {len(merged)}")
    print(f"- 최종 공고 수: {merged['공고번호'].astype(str).nunique() if not merged.empty else 0}")
    print(f"- 최종 파일: {final_output}")
    print(f"- 판정 로그: {audit_output}")
    if not audit_df.empty and '처리상태' in audit_df.columns:
        print(f"- 판정 분포: {audit_df['처리상태'].value_counts(dropna=False).to_dict()}")


if __name__ == "__main__":
    main()
