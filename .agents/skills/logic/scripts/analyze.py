#!/usr/bin/env python3
"""콘솔 전용 전체 파이프라인 실행기.

순서:
1) API 증분 수집/병합 (선택)
2) 지역 필터 분석 입력 생성
3) 군집도·기대확률 분석
4) 충돌앵커·좌측여유 분석
5) competition-only shadow-strategy 분석
6) bidder-visualization(0.01/0.001)
"""
from __future__ import annotations

import argparse
import json
import shlex
import subprocess
import sys
from pathlib import Path

import pandas as pd

BASE = Path("/mnt/a/c")
if str(BASE) not in sys.path:
    sys.path.insert(0, str(BASE))

from nara_workspace_paths import analysis_dir, charts_dir, master_path, output_dir, scripts_dir, workspace_root

WORKSPACE = workspace_root()
MASTER = master_path()
ANALYSIS_DIR = analysis_dir()
OUTPUT_DIR = output_dir()
CHART_DIR = charts_dir()
VIZ_PERIODS = ("all", "6m", "3m", "1m")

API_WRAPPER = scripts_dir() / "run_api_pipeline.sh"
CROWDING = BASE / "cc-feature-implementer" / "src" / "analyze_crowding_probability.py"
COLLISION = BASE / "cc-feature-implementer" / "src" / "analyze_collision_left_space.py"
SHADOW = BASE / ".agents" / "skills" / "shadow-strategy" / "scripts" / "analyze.py"
VIZ = BASE / ".agents" / "skills" / "bidder-visualization" / "scripts" / "visualize.py"
FIXED_BIDDER_DETAILED = BASE / ".agents" / "skills" / "fixed-bidder-map" / "scripts" / "detailed_map.py"
SHADOW_MODULE_DIR = BASE / ".agents" / "skills" / "shadow-strategy" / "modules"
REPO_SRC_DIR = BASE / "cc-feature-implementer" / "src"
if str(SHADOW_MODULE_DIR) not in sys.path:
    sys.path.insert(0, str(SHADOW_MODULE_DIR))
if str(REPO_SRC_DIR) not in sys.path:
    sys.path.insert(0, str(REPO_SRC_DIR))

from conditional_advantage_strategy import build_conditional_advantage_payload
from profile_loader import DEFAULT_ANALYSIS_PROFILE_PATH, load_analysis_profile, load_api_rule_profile


def run(cmd: list[str]) -> None:
    print("[RUN]", " ".join(shlex.quote(part) for part in cmd))
    subprocess.run(cmd, check=True)


def clear_generated_files(paths: list[Path]) -> None:
    for path in paths:
        if path.is_file():
            path.unlink()
        elif path.is_dir():
            for child in path.iterdir():
                if child.is_file():
                    child.unlink()


def load_json_if_exists(path: Path) -> dict:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def load_anchor_rows_if_exists(path: Path) -> list[dict]:
    if not path.exists():
        return []
    return pd.read_csv(path).to_dict(orient="records")


def build_viz_output_paths(region: str) -> dict[str, dict[str, Path]]:
    return {
        period: {
            "0.01": CHART_DIR / f"{region}_{period}_시각화_0.01.png",
            "0.001": CHART_DIR / f"{region}_{period}_시각화_0.001.png",
        }
        for period in VIZ_PERIODS
    }


def build_api_command(args: argparse.Namespace) -> list[str]:
    cmd = [str(API_WRAPPER)]
    if args.start_month:
        cmd += ["--start-month", args.start_month]
    if args.end_month:
        cmd += ["--end-month", args.end_month]
    if args.months is not None:
        cmd += ["--months", str(args.months)]
    if args.years is not None:
        cmd += ["--years", str(args.years)]
    if args.keywords:
        cmd += ["--keywords", args.keywords]
    return cmd


def normalize_region(value: str) -> str:
    token = str(value or "").replace(" ", "")
    if "전국" in token:
        return "전국"
    if "대구" in token and ("경북" not in token and "경상북" not in token):
        return "대구"
    if "경북" in token or "경상북" in token:
        return "경북"
    return str(value or "").strip()


def build_analysis_input(region: str, source_file: Path, retired_company_tokens: list[str]) -> tuple[Path, int, int]:
    if not source_file.exists():
        raise FileNotFoundError(f"소스 데이터 파일 없음: {source_file}")

    df = pd.read_excel(source_file)
    if "지역제한" not in df.columns:
        df["지역제한"] = ""
    if "업체명" not in df.columns:
        df["업체명"] = ""

    df["지역정규"] = df["지역제한"].map(normalize_region)
    if region != "전체":
        df = df[df["지역정규"] == region].copy()

    retired_mask = pd.Series(False, index=df.index)
    for token in retired_company_tokens:
        retired_mask = retired_mask | df["업체명"].astype(str).str.contains(str(token), na=False)
    df = df[~retired_mask].copy()

    ANALYSIS_DIR.mkdir(parents=True, exist_ok=True)
    out = ANALYSIS_DIR / f"input_{region}.xlsx"
    df.drop(columns=["지역정규"], errors="ignore").to_excel(out, index=False)
    row_count = len(df)
    notice_count = int(df["공고번호"].nunique()) if "공고번호" in df.columns else 0
    print(f"[OK] 분석입력 생성: {out} (rows={row_count:,}, notices={notice_count:,})")
    return out, row_count, notice_count


def count_rows_in_rate_window(source_file: Path, min_rate: float, max_rate: float) -> int:
    df = pd.read_excel(source_file, usecols=["기초대비사정률"])
    rates = pd.to_numeric(df["기초대비사정률"], errors="coerce")
    return int(rates.between(min_rate, max_rate, inclusive="both").sum())


def main() -> None:
    pre_parser = argparse.ArgumentParser(add_help=False)
    pre_parser.add_argument("--analysis-profile", default=str(DEFAULT_ANALYSIS_PROFILE_PATH))
    pre_args, _ = pre_parser.parse_known_args()
    analysis_profile, analysis_profile_path = load_analysis_profile(pre_args.analysis_profile)
    api_rule_profile, _ = load_api_rule_profile()
    allowed_regions = list(api_rule_profile.get("allowed_regions", ["전국", "대구", "경북"])) + ["전체"]

    p = argparse.ArgumentParser(description="콘솔 전용 전체 파이프라인")
    p.add_argument("--analysis-profile", default=str(analysis_profile_path), help="분석 규칙 프로필 JSON 경로")
    p.add_argument("--start-month", default=None)
    p.add_argument("--end-month", default=None)
    p.add_argument("--months", type=int, default=None)
    p.add_argument("--years", type=int, default=None)
    p.add_argument("--keywords", default=None, help="API 수집 키워드(미지정 시 API 정본 규칙 사용)")
    p.add_argument("--region", default=analysis_profile["cli_defaults"]["pipeline_region"], choices=allowed_regions)
    p.add_argument("--period", default=analysis_profile["cli_defaults"]["pipeline_period"], choices=list(analysis_profile["period_days"].keys()))
    p.add_argument("--min-rate", type=float, default=float(analysis_profile["rate_range"]["min"]))
    p.add_argument("--max-rate", type=float, default=float(analysis_profile["rate_range"]["max"]))
    p.add_argument("--source-file", default=str(MASTER), help="분석 입력 생성에 사용할 최종 통합 데이터 파일")
    p.add_argument("--my-company", default=analysis_profile["cli_defaults"]["my_company"], help="경쟁업체 분석에서 제외할 자사명")
    p.add_argument("--skip-api", action="store_true")
    p.add_argument("--dry-run", action="store_true", help="실행 명령/출력 경로만 표시하고 실제 실행하지 않음")
    args = p.parse_args()
    analysis_profile, analysis_profile_path = load_analysis_profile(args.analysis_profile)
    api_rule_profile, _ = load_api_rule_profile()
    retired_company_tokens = list(api_rule_profile.get("retired_company_tokens", []))

    source_file = Path(args.source_file)
    api_cmd = None if args.skip_api else build_api_command(args)
    analysis_input = ANALYSIS_DIR / f"input_{args.region}.xlsx"
    fixed_map_out = ANALYSIS_DIR / f"fixed_bidder_map_detailed_{args.region}"
    crowding_out = ANALYSIS_DIR / f"crowding_{args.region}"
    collision_out = ANALYSIS_DIR / f"collision_{args.region}"
    shadow_out = ANALYSIS_DIR / f"shadow_{args.region}"
    crowding_cmd = [
        sys.executable,
        str(CROWDING),
        "--data", str(analysis_input),
        "--output-dir", str(crowding_out),
        "--bin-width", "0.001",
        "--min-rate", str(args.min_rate),
        "--max-rate", str(args.max_rate),
    ]
    collision_cmd = [
        sys.executable,
        str(COLLISION),
        "--data", str(analysis_input),
        "--output-dir", str(collision_out),
        "--min-rate", str(args.min_rate),
        "--max-rate", str(args.max_rate),
    ]
    shadow_cmd = [
        sys.executable,
        str(SHADOW),
        "--data", str(analysis_input),
        "--output-dir", str(shadow_out),
        "--profile", str(analysis_profile_path),
        "--period", args.period,
        "--min-rate", str(args.min_rate),
        "--max-rate", str(args.max_rate),
        "--my-company", args.my_company,
        "--output", "all",
    ]
    chart_outputs = build_viz_output_paths(args.region)
    viz_cmds: list[list[str]] = []
    for viz_period in VIZ_PERIODS:
        for precision in ("0.01", "0.001"):
            viz_cmds.append(
                [
                    sys.executable,
                    str(VIZ),
                    "--input", str(analysis_input),
                    "--period", viz_period,
                    "--precision", precision,
                    "--min-rate", str(args.min_rate),
                    "--max-rate", str(args.max_rate),
                    "--output", str(chart_outputs[viz_period][precision]),
                ]
            )

    if args.dry_run:
        print("[DRY-RUN] 콘솔 전체 파이프라인 실행 계획")
        print(f"- workspace_root: {WORKSPACE}")
        print(f"- source_file: {source_file}")
        print(f"- analysis_input: {analysis_input}")
        print(f"- fixed_bidder_map_output: {fixed_map_out}")
        print(f"- crowding_output: {crowding_out}")
        print(f"- collision_output: {collision_out}")
        print(f"- shadow_output: {shadow_out}")
        print(f"- chart_output: {CHART_DIR}")
        if api_cmd is None:
            print("[PLAN] API 단계 생략 (--skip-api)")
        else:
            print("[PLAN]", " ".join(shlex.quote(part) for part in api_cmd))
        print("[PLAN]", " ".join(shlex.quote(part) for part in [
            sys.executable,
            str(FIXED_BIDDER_DETAILED),
            "--data", str(analysis_input),
            "--output-dir", str(fixed_map_out),
        ]))
        print("[PLAN]", " ".join(shlex.quote(part) for part in crowding_cmd))
        print("[PLAN]", " ".join(shlex.quote(part) for part in collision_cmd))
        print("[PLAN]", " ".join(shlex.quote(part) for part in shadow_cmd))
        for viz_cmd in viz_cmds:
            print("[PLAN]", " ".join(shlex.quote(part) for part in viz_cmd))
        return

    WORKSPACE.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    CHART_DIR.mkdir(parents=True, exist_ok=True)

    if api_cmd is not None:
        run(api_cmd)
        source_file = MASTER

    analysis_input, row_count, notice_count = build_analysis_input(args.region, source_file, retired_company_tokens)
    window_row_count = count_rows_in_rate_window(analysis_input, args.min_rate, args.max_rate)

    if row_count == 0 or window_row_count == 0:
        shadow_out.mkdir(parents=True, exist_ok=True)
        crowding_out.mkdir(parents=True, exist_ok=True)
        collision_out.mkdir(parents=True, exist_ok=True)
        legacy_chart_files = list(CHART_DIR.glob(f"{args.region}*.png"))
        clear_generated_files(
            [
                crowding_out,
                collision_out,
                shadow_out,
                *legacy_chart_files,
                *[
                    chart_path
                    for period_paths in chart_outputs.values()
                    for chart_path in period_paths.values()
                ],
            ]
        )
        status_path = shadow_out / "pipeline_status.json"
        status_name = "skipped_empty_input" if row_count == 0 else "skipped_empty_rate_window"
        status_path.write_text(
            json.dumps(
                {
                    "status": status_name,
                    "region": args.region,
                    "source_file": str(source_file),
                    "analysis_input": str(analysis_input),
                    "rows": row_count,
                    "notices": notice_count,
                    "rate_window": {
                        "min": args.min_rate,
                        "max": args.max_rate,
                        "rows": window_row_count,
                    },
                    "outputs": {
                        "fixed_bidder_map_detailed": str(fixed_map_out / "detailed_fixed_bidder_map.json"),
                        "crowding": str(crowding_out),
                        "collision": str(collision_out),
                        "shadow": str(shadow_out),
                        "charts": {
                            period: {precision: str(path) for precision, path in precision_map.items()}
                            for period, precision_map in chart_outputs.items()
                        },
                    },
                },
                ensure_ascii=False,
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )
        if row_count == 0:
            print("[SKIP] 분석입력이 비어 있어 시장분석/전략/시각화 단계를 생략합니다.")
        else:
            print("[SKIP] 지정 사정률 구간에 해당하는 행이 없어 시장분석/전략/시각화 단계를 생략합니다.")
        print("[DONE] 콘솔 전체 파이프라인 완료")
        print(f"- source_file: {source_file}")
        print(f"- analysis_input: {analysis_input}")
        print(f"- fixed_bidder_map_output: {fixed_map_out}")
        print(f"- filtered_notices: {notice_count}")
        print(f"- rate_window_rows: {window_row_count}")
        print(f"- crowding_output: {crowding_out}")
        print(f"- collision_output: {collision_out}")
        print(f"- strategy_output: {shadow_out}")
        print(f"- pipeline_status: {status_path}")
        return

    crowding_out.mkdir(parents=True, exist_ok=True)
    collision_out.mkdir(parents=True, exist_ok=True)
    shadow_out.mkdir(parents=True, exist_ok=True)
    fixed_map_out.mkdir(parents=True, exist_ok=True)
    fixed_map_cmd = [
        sys.executable,
        str(FIXED_BIDDER_DETAILED),
        "--data", str(analysis_input),
        "--output-dir", str(fixed_map_out),
    ]
    run(fixed_map_cmd)
    run(crowding_cmd)
    run(collision_cmd)
    run(shadow_cmd)
    for viz_cmd in viz_cmds:
        run(viz_cmd)

    crowding_summary = load_json_if_exists(crowding_out / "summary.json")
    collision_summary = load_json_if_exists(collision_out / "summary.json")
    competition_profile = load_json_if_exists(shadow_out / "competition_profile.json")
    gap_analysis = load_json_if_exists(shadow_out / "gap_analysis.json")
    shadow_strategy = load_json_if_exists(shadow_out / "shadow_strategy.json")
    conditional_advantage = build_conditional_advantage_payload(
        crowding_summary,
        collision_summary,
        competition_profile,
        gap_analysis,
        shadow_strategy,
        load_anchor_rows_if_exists(collision_out / "anchor_company_summary.csv"),
        analysis_profile.get("decision_rules"),
    )
    (shadow_out / "conditional_advantage.json").write_text(
        json.dumps(conditional_advantage, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    status_path = shadow_out / "pipeline_status.json"
    status_payload = {
        "status": "completed",
        "region": args.region,
        "source_file": str(source_file),
        "analysis_input": str(analysis_input),
        "rows": row_count,
        "notices": notice_count,
        "rate_window": {
            "min": args.min_rate,
            "max": args.max_rate,
            "rows": window_row_count,
        },
        "outputs": {
            "fixed_bidder_map_detailed": str(fixed_map_out / "detailed_fixed_bidder_map.json"),
            "crowding": str(crowding_out),
            "collision": str(collision_out),
            "shadow": str(shadow_out),
            "conditional_advantage": str(shadow_out / "conditional_advantage.json"),
            "charts": {
                period: {precision: str(path) for precision, path in precision_map.items()}
                for period, precision_map in chart_outputs.items()
            },
        },
        "crowding_key_findings": crowding_summary.get("key_findings", {}),
        "collision_key_findings": collision_summary.get("key_findings", {}),
        "shadow_summary": shadow_strategy.get("summary", {}),
        "conditional_advantage_summary": conditional_advantage.get("summary", {}),
        "conditional_advantage": conditional_advantage,
    }
    status_path.write_text(json.dumps(status_payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print("[DONE] 콘솔 전체 파이프라인 완료")
    print(f"- source_file: {source_file}")
    print(f"- analysis_input: {analysis_input}")
    print(f"- fixed_bidder_map_output: {fixed_map_out}")
    print(f"- rate_window_rows: {window_row_count}")
    print(f"- crowding_output: {crowding_out}")
    print(f"- collision_output: {collision_out}")
    print(f"- strategy_output: {shadow_out}")
    print(f"- charts: {CHART_DIR} ({', '.join(VIZ_PERIODS)} x 0.01/0.001)")
    print(f"- pipeline_status: {status_path}")


if __name__ == "__main__":
    main()
