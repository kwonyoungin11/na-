#!/usr/bin/env python3
"""기준 파일 기준으로 증분 수집 + 2차 전처리 (API 스킬 통합 경로)."""
from __future__ import annotations

import argparse
import subprocess
import sys
from datetime import datetime, timedelta
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


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="기준 마스터 기준 증분 파이프라인")
    parser.add_argument("--baseline-file", default=str(master_path()), help="기준 최종 마스터 파일(엑셀)")
    parser.add_argument("--first-stage-dir", default=str(first_stage_dir("incremental")), help="1차 수집 폴더")
    parser.add_argument("--final-output", default=str(master_path()), help="최종 병합 출력 파일")
    parser.add_argument("--audit-output", default=str(reports_dir() / "2차전처리_판정로그_증분.xlsx"), help="2차 판정로그")
    parser.add_argument("--end", required=True, help="수집 종료(YYYYMMDDHHMM)")
    parser.add_argument("--start", help="수집 시작(YYYYMMDDHHMM). 미지정시 baseline 최고개찰일 기준 자동 계산")
    parser.add_argument("--overlap-minutes", type=int, default=120, help="최신 데이터 중복 보정 오버랩(분)")
    parser.add_argument("--keywords", default="", help="비우면 규칙 프로필 기본값 사용")
    parser.add_argument("--rule-profile", default=str(RULE_PROFILE_PATH), help="정본 규칙 프로필 JSON")
    parser.add_argument("--allowed-regions", default="")
    parser.add_argument("--min-participants", type=int, default=None)
    parser.add_argument("--target-company-terms", default="")
    parser.add_argument("--env-file", default=str(default_env_path()))
    parser.add_argument("--max-notices", type=int, default=None)
    parser.add_argument("--clean-first-stage", action="store_true", help="1차 수집 폴더 초기화")
    parser.add_argument("--no-append", action="store_true", help="기준 마스터와 병합하지 않고 이번 실행 결과만 저장")
    parser.add_argument("--target-kept-notices", type=int, default=None, help="목표 보존 공고 수")
    parser.add_argument("--lookback-step-days", type=int, default=30, help="기간 확장 단위(일)")
    parser.add_argument("--max-lookback-days", type=int, default=365, help="최대 과거 확장(일)")
    return parser.parse_args()


def parse_start_from_baseline(path: str, overlap_minutes: int) -> str:
    baseline = Path(path)
    if not baseline.exists():
        raise FileNotFoundError(f"baseline 파일이 없습니다: {baseline}")

    df = pd.read_excel(baseline, engine="openpyxl")
    if "개찰일시" not in df.columns:
        raise ValueError("baseline에 개찰일시 컬럼이 없습니다.")

    series = pd.to_datetime(df["개찰일시"], errors="coerce")
    if series.empty or series.isna().all():
        raise ValueError("baseline에서 유효한 개찰일시를 찾지 못했습니다.")

    latest = series.max() - timedelta(minutes=max(0, overlap_minutes))
    return latest.strftime("%Y%m%d%H%M")


def count_notice_unique(path: str) -> int:
    fp = Path(path)
    if not fp.exists():
        return 0
    df = pd.read_excel(fp)
    if "공고번호" not in df.columns:
        return 0
    return int(df["공고번호"].astype(str).str.strip().nunique())


def run(cmd: list[str]) -> None:
    print("[RUN]", " ".join(cmd))
    subprocess.run(cmd, check=True)


def run_cycle(args: argparse.Namespace, start: str, first_cycle: bool, first_stage_dir: Path) -> None:
    known_notice_file = args.baseline_file
    final_path = Path(args.final_output)
    if final_path.exists() and not args.no_append:
        known_notice_file = args.final_output

    cmd_first = [
        sys.executable,
        str(FIRST_STAGE_SCRIPT),
        "--start",
        start,
        "--end",
        args.end,
        "--env-file",
        args.env_file,
        "--output-dir",
        str(first_stage_dir),
        "--known-notice-file",
        known_notice_file,
        "--rule-profile",
        args.rule_profile,
    ]
    if args.keywords.strip():
        cmd_first.extend(["--keywords", args.keywords])
    if args.max_notices is not None:
        cmd_first.extend(["--max-notices", str(max(0, args.max_notices))])
    if args.clean_first_stage and first_cycle:
        cmd_first.append("--clean")

    run(cmd_first)

    cmd_second = [
        sys.executable,
        str(SECOND_STAGE_SCRIPT),
        "--first-stage-dir",
        str(first_stage_dir),
        "--final-output",
        args.final_output,
        "--audit-output",
        args.audit_output,
        "--rule-profile",
        args.rule_profile,
    ]
    if args.allowed_regions.strip():
        cmd_second.extend(["--allowed-regions", args.allowed_regions])
    if args.min_participants is not None:
        cmd_second.extend(["--min-participants", str(args.min_participants)])
    if args.target_company_terms.strip():
        cmd_second.extend(["--target-company-terms", args.target_company_terms])
    if not args.no_append:
        cmd_second.extend(["--append-existing", args.final_output])

    run(cmd_second)


def main() -> None:
    args = parse_args()
    start = args.start or parse_start_from_baseline(args.baseline_file, args.overlap_minutes)
    start_dt = datetime.strptime(start, "%Y%m%d%H%M")

    if args.target_kept_notices is not None and args.target_kept_notices <= 0:
        raise ValueError("--target-kept-notices는 1 이상의 값이어야 합니다.")
    if args.target_kept_notices is not None and args.no_append:
        raise ValueError("--target-kept-notices는 --no-append와 함께 사용할 수 없습니다.")
    if args.lookback_step_days <= 0:
        raise ValueError("--lookback-step-days는 1 이상의 값이어야 합니다.")
    if args.max_lookback_days <= 0:
        raise ValueError("--max-lookback-days는 1 이상의 값이어야 합니다.")

    first_stage_dir = Path(args.first_stage_dir)
    if args.clean_first_stage and first_stage_dir.exists():
        for path in first_stage_dir.glob("*.xlsx"):
            path.unlink()
        for path in first_stage_dir.glob("*.json"):
            path.unlink()
        for path in first_stage_dir.glob("*.jsonl"):
            path.unlink()
    first_stage_dir.mkdir(parents=True, exist_ok=True)

    append_existing = not args.no_append
    base_notice_count = count_notice_unique(args.baseline_file) if append_existing else 0
    target = args.target_kept_notices
    min_start_dt = start_dt - timedelta(days=args.max_lookback_days)

    loop_start = start_dt
    cycle = 0

    while True:
        run_cycle(args, loop_start.strftime("%Y%m%d%H%M"), cycle == 0, first_stage_dir)

        if target is not None:
            now_count = count_notice_unique(args.final_output)
            added = now_count - base_notice_count
            print(f"[STATUS] 누적 보존: {now_count} / 기준 대비 신규 {added} (목표 {target})")
            if added >= target:
                print(f"[OK] 목표치 도달: 신규 {added} >= {target}")
                break

            next_start = loop_start - timedelta(days=args.lookback_step_days)
            if next_start < min_start_dt:
                print(
                    f"[WARN] 목표치 미달(신규 {added}/{target})인데 허용 범위 내 최대 과거 기간 "
                    f"{args.max_lookback_days}일 도달"
                )
                break

            loop_start = next_start
            print(f"[INFO] 목표치 미달로 시작일을 이전으로 확장: {loop_start:%Y%m%d%H%M}")
            cycle += 1
            continue

        break

    print("완료")
    print(f"- baseline: {args.baseline_file}")
    print(f"- start: {loop_start:%Y%m%d%H%M}")
    print(f"- final-output: {args.final_output}")


if __name__ == "__main__":
    main()
