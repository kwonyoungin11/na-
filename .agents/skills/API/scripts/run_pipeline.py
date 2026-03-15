#!/usr/bin/env python3
"""Run the canonical 2-stage pipeline (API skill single source of truth)."""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

import pandas as pd

PROJECT_ROOT = Path(__file__).resolve().parents[4]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from nara_workspace_paths import default_env_path, first_stage_dir, master_path, reports_dir

API_SKILL_ROOT = Path(__file__).resolve().parents[1]
FIRST_STAGE_SCRIPT = API_SKILL_ROOT / "scripts" / "collect_first_stage.py"
SECOND_STAGE_SCRIPT = PROJECT_ROOT / ".agents" / "skills" / "data-preprocessing" / "scripts" / "preprocess_api_stage2.py"
RULE_PROFILE_PATH = API_SKILL_ROOT / "rule_profile.json"


def run(cmd: list[str]) -> None:
    print("[RUN]", " ".join(cmd))
    subprocess.run(cmd, check=True)


def summarize_excel(path: Path, title: str) -> None:
    if not path.exists():
        print(f"[WARN] {title}: 파일 없음 -> {path}")
        return

    try:
        df = pd.read_excel(path)
    except Exception as exc:  # noqa: BLE001
        print(f"[WARN] {title}: 읽기 실패 -> {path} ({exc})")
        return

    notice_col = "공고번호" if "공고번호" in df.columns else None
    notice_count = df[notice_col].nunique() if notice_col else "N/A"
    print(
        f"[OK] {title}: 파일={path.name}, 행={len(df):,}, 공고="
        f"{notice_count if isinstance(notice_count, str) else f'{notice_count:,}'}"
    )


def summarize_index(path: Path) -> None:
    if not path.exists():
        print(f"[WARN] 인덱스 없음 -> {path}")
        return

    df = pd.read_excel(path)
    print(f"[OK] 인덱스: 행={len(df):,}")
    if "처리상태" in df.columns:
        print(f"[OK] 처리상태: {df['처리상태'].value_counts(dropna=False).to_dict()}")


def main() -> None:
    parser = argparse.ArgumentParser(description="나라장터 API 고정파이프라인 실행")
    parser.add_argument("--start", required=True, help="조회 시작(YYYYMMDDHHMM)")
    parser.add_argument("--end", required=True, help="조회 종료(YYYYMMDDHHMM)")
    parser.add_argument("--keywords", default="", help="비우면 규칙 프로필 기본값 사용")
    parser.add_argument("--rule-profile", default=str(RULE_PROFILE_PATH), help="정본 규칙 프로필 JSON")
    parser.add_argument(
        "--first-stage-dir",
        default=str(first_stage_dir("pipeline")),
        help="1차 수집 산출 디렉터리",
    )
    parser.add_argument(
        "--final-output",
        default=str(master_path()),
        help="2차 전처리 최종 통합 엑셀",
    )
    parser.add_argument(
        "--audit-output",
        default=str(reports_dir() / "2차전처리_판정로그_파이프라인.xlsx"),
        help="2차 전처리 판정 로그 엑셀",
    )
    parser.add_argument("--allowed-regions", default="", help="비우면 규칙 프로필 기본값 사용")
    parser.add_argument("--min-participants", type=int, default=None, help="비우면 규칙 프로필 기본값 사용")
    parser.add_argument("--target-company-terms", default="", help="비우면 규칙 프로필 기본값 사용")
    parser.add_argument("--env-file", default=str(default_env_path()), help="서비스키 .env 경로")
    parser.add_argument("--max-notices", type=int, default=None, help="테스트용 1차 수집 상한")
    parser.add_argument("--clean-first-stage", action="store_true", help="1차 수집 디렉터리 초기화")
    parser.add_argument("--skip-collect", action="store_true", help="1차 수집 생략")
    args = parser.parse_args()

    first_stage_dir = Path(args.first_stage_dir)
    final_output = Path(args.final_output)
    audit_output = Path(args.audit_output)
    if not FIRST_STAGE_SCRIPT.exists():
        raise FileNotFoundError(f"1차 수집 스크립트가 없습니다: {FIRST_STAGE_SCRIPT}")
    if not SECOND_STAGE_SCRIPT.exists():
        raise FileNotFoundError(f"2차 전처리 스크립트가 없습니다: {SECOND_STAGE_SCRIPT}")

    if not args.skip_collect:
        first_stage_cmd = [
            sys.executable,
            str(FIRST_STAGE_SCRIPT),
            "--start",
            args.start,
            "--end",
            args.end,
            "--env-file",
            args.env_file,
            "--output-dir",
            str(first_stage_dir),
            "--rule-profile",
            args.rule_profile,
        ]
        if args.keywords.strip():
            first_stage_cmd.extend(["--keywords", args.keywords])
        if args.max_notices is not None:
            first_stage_cmd.extend(["--max-notices", str(max(0, args.max_notices))])
        if args.clean_first_stage:
            first_stage_cmd.append("--clean")
        run(first_stage_cmd)

    index_path = first_stage_dir / "공고_파일_인덱스.xlsx"
    summarize_index(index_path)

    second_stage_cmd = [
        sys.executable,
        str(SECOND_STAGE_SCRIPT),
        "--first-stage-dir",
        str(first_stage_dir),
        "--final-output",
        str(final_output),
        "--audit-output",
        str(audit_output),
        "--rule-profile",
        args.rule_profile,
    ]
    if args.allowed_regions.strip():
        second_stage_cmd.extend(["--allowed-regions", args.allowed_regions])
    if args.min_participants is not None:
        second_stage_cmd.extend(["--min-participants", str(args.min_participants)])
    if args.target_company_terms.strip():
        second_stage_cmd.extend(["--target-company-terms", args.target_company_terms])
    run(second_stage_cmd)

    summarize_excel(final_output, "최종 통합 파일")
    summarize_excel(audit_output, "판정 로그")


if __name__ == "__main__":
    main()
