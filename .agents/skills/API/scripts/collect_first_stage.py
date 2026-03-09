#!/usr/bin/env python3
"""나라장터 API 1차 수집 실행 래퍼 (정본 규칙 프로필 기반)."""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

import pandas as pd

SKILL_ROOT = Path(__file__).resolve().parents[1]
RULE_PROFILE_PATH = SKILL_ROOT / "rule_profile.json"
DEFAULT_RULE_PROFILE = json.loads(RULE_PROFILE_PATH.read_text(encoding="utf-8")) if RULE_PROFILE_PATH.exists() else {}
DEFAULT_KEYWORDS = [
    str(x).strip()
    for x in [
        *DEFAULT_RULE_PROFILE.get("primary_keywords", []),
        *DEFAULT_RULE_PROFILE.get("recovery_keywords", []),
    ]
    if str(x).strip()
]


def parse_ymdhm(value: str) -> str:
    if len(value) != 12 or not value.isdigit():
        raise argparse.ArgumentTypeError("날짜 형식은 YYYYMMDDHHMM 이어야 합니다.")
    return value


def main() -> None:
    parser = argparse.ArgumentParser(description="나라장터 API 1차 수집")
    parser.add_argument("--start", required=True, type=parse_ymdhm)
    parser.add_argument("--end", required=True, type=parse_ymdhm)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--keywords", default="")
    parser.add_argument("--env-file", default="/mnt/a/c/.env")
    parser.add_argument("--max-notices", type=int, default=None)
    parser.add_argument("--clean", action="store_true", help="실행 전 output-dir 초기화")
    parser.add_argument("--rule-profile", default=str(RULE_PROFILE_PATH), help="정본 규칙 프로필 JSON")
    parser.add_argument(
        "--known-notice-file",
        default="",
        help="기존 최종/증분 공고번호 목록(엑셀/텍스트)",
    )
    args = parser.parse_args()

    collector = Path("/mnt/a/c/cc-feature-implementer/src/fetch_keyword_bids_api_per_notice.py")
    if not collector.exists():
        raise FileNotFoundError(f"수집 스크립트가 없습니다: {collector}")

    output_dir = Path(args.output_dir)
    if args.clean and output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    keywords = args.keywords.strip() or ",".join(DEFAULT_KEYWORDS)

    cmd = [
        sys.executable,
        str(collector),
        "--start",
        args.start,
        "--end",
        args.end,
        "--output-dir",
        str(output_dir),
        "--env-file",
        args.env_file,
        "--rule-profile",
        args.rule_profile,
    ]
    if keywords:
        cmd.extend(["--keywords", keywords])
    if args.known_notice_file.strip():
        cmd.extend(["--known-notice-file", args.known_notice_file])
    if args.max_notices is not None:
        cmd.extend(["--max-notices", str(max(0, args.max_notices))])

    print("[RUN]", " ".join(cmd))
    subprocess.run(cmd, check=True)

    files = sorted(output_dir.glob("*.xlsx"))
    index_file = output_dir / "공고_파일_인덱스.xlsx"
    audit_file = output_dir / "공고_후보_감사로그.xlsx"
    stage_summary_file = output_dir / "stage_summary.json"

    print("완료")
    print(f"- output-dir: {output_dir}")
    print(f"- xlsx 개수: {len(files)}")
    print(f"- 인덱스 존재: {index_file.exists()}")
    print(f"- 감사로그 존재: {audit_file.exists()}")
    print(f"- 단계요약 존재: {stage_summary_file.exists()}")

    if index_file.exists():
        df = pd.read_excel(index_file)
        print(f"- 인덱스 행수: {len(df)}")
        if "처리상태" in df.columns:
            print(f"- 상태분포: {df['처리상태'].value_counts(dropna=False).to_dict()}")


if __name__ == "__main__":
    main()
