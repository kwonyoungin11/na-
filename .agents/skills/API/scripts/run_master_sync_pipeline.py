#!/usr/bin/env python3
"""나라장터 OpenAPI 마스터 동기화 파이프라인 (프로젝트 로컬 호환 버전).

기능:
- 월 단위 수집 (start/end 미지정 시 자동 증분)
- API 키 파일 자동 연결(.api_key.env 또는 프로젝트 .env)
- API 수집 결과를 마스터 엑셀(전체_통합_데이터.xlsx)에 병합/누적
- 수집 이력 JSONL 기록
"""

from __future__ import annotations

import argparse
from datetime import date, datetime
import json
from pathlib import Path
import sys
from typing import Any


PROJECT_ROOT = Path(__file__).resolve().parents[4]
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

from nara_workspace_paths import (
    collection_log_path,
    daegu_gyeongbuk_master_path,
    default_env_path,
    output_dir,
    master_path,
)

SKILL_ROOT = Path(__file__).resolve().parents[1]
SCRIPT_ROOT = SKILL_ROOT / "scripts"
SKILL_KEY_FILE = SKILL_ROOT / ".api_key.env"
DEFAULT_SOURCE_ENV = default_env_path()
DEFAULT_LEGACY_ENV = PROJECT_ROOT / "공공데이터포털api" / ".env"
DEFAULT_OUTPUT_DIR = output_dir()
DEFAULT_COLLECTION_LOG = collection_log_path()
DEFAULT_MASTER_PATH = master_path()
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


def _load_skill_module():
    import importlib.util

    candidates = [
        SCRIPT_ROOT / "run_nara_openapi_pipeline.py",
        PROJECT_ROOT
        / ".codex"
        / "skills"
        / "나라장터api수집전처리"
        / "scripts"
        / "run_nara_openapi_pipeline.py",
    ]

    script = next((path for path in candidates if path.exists()), None)
    if script is None:
        raise FileNotFoundError(
            "run_nara_openapi_pipeline.py를 찾을 수 없습니다. "
            f"checked={', '.join(str(x) for x in candidates)}"
        )

    spec = importlib.util.spec_from_file_location("nara_openapi_pipeline", script)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Skill 모듈 로드 실패: {script}")
    mod = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = mod
    spec.loader.exec_module(mod)  # type: ignore[attr-defined]
    return mod


def _select_key_source_env(preferred: Path | None) -> Path:
    candidates: list[Path] = []
    if preferred is not None:
        candidates.append(preferred)
    candidates.extend([DEFAULT_SOURCE_ENV, DEFAULT_LEGACY_ENV])
    for candidate in candidates:
        if candidate.exists():
            return candidate
    raise FileNotFoundError(
        "API 키 원본 파일을 찾을 수 없습니다. "
        f"checked={', '.join(str(x) for x in candidates)}"
    )


def ensure_skill_api_key(source_env: Path) -> Path:
    """Skill 내부 키 파일 보장."""
    mod = _load_skill_module()

    if SKILL_KEY_FILE.exists():
        try:
            key = mod.load_service_key(SKILL_KEY_FILE)
            if key:
                return SKILL_KEY_FILE
        except Exception:
            pass

    key_value = mod.load_service_key(source_env)
    SKILL_KEY_FILE.parent.mkdir(parents=True, exist_ok=True)
    SKILL_KEY_FILE.write_text(f"SERVICE_KEY={key_value}\n", encoding="utf-8")
    return SKILL_KEY_FILE


def _default_month() -> str:
    return datetime.now().strftime("%Y-%m")


def _parse_month(value: str) -> date:
    parsed = datetime.strptime(value, "%Y-%m")
    return date(parsed.year, parsed.month, 1)


def _format_month(value: date) -> str:
    return value.strftime("%Y-%m")


def _add_months(value: date, delta: int) -> date:
    m = value.month - 1 + delta
    y = value.year + m // 12
    m = m % 12 + 1
    return date(y, m, 1)


def _current_month() -> date:
    now = datetime.now()
    return date(now.year, now.month, 1)


def _read_last_logged_end_month(log_path: Path) -> date | None:
    if not log_path.exists():
        return None

    last_month: date | None = None
    for line in log_path.read_text(encoding="utf-8", errors="ignore").splitlines():
        raw = line.strip()
        if not raw:
            continue
        try:
            payload = json.loads(raw)
        except json.JSONDecodeError:
            continue
        end_month = str(payload.get("resolved_end_month") or "").strip()
        if not end_month:
            continue
        try:
            parsed = _parse_month(end_month)
        except ValueError:
            continue
        if last_month is None or parsed > last_month:
            last_month = parsed
    return last_month


def _read_master_max_month(data_path: Path) -> date | None:
    if not data_path.exists():
        return None
    try:
        import pandas as pd
    except Exception:
        return None

    try:
        df = pd.read_excel(data_path)
    except Exception:
        return None
    if "개찰일시" not in df.columns:
        return None

    dt = pd.to_datetime(df["개찰일시"], errors="coerce")
    if not dt.notna().any():
        return None
    max_dt = dt.max()
    return date(int(max_dt.year), int(max_dt.month), 1)


def _resolve_period(
    *,
    start_month: str | None,
    end_month: str | None,
    months: int | None,
    years: int | None,
    data_path: Path,
    log_path: Path,
) -> tuple[str, str, str]:
    if years is not None and years <= 0:
        raise ValueError("--years는 1 이상이어야 합니다.")
    if months is not None and months <= 0:
        raise ValueError("--months는 1 이상이어야 합니다.")

    if start_month or end_month:
        resolved_start = _parse_month(start_month or end_month or _default_month())
        resolved_end = _parse_month(end_month or start_month or _default_month())
        return _format_month(resolved_start), _format_month(resolved_end), "explicit"

    recent_months = None
    if years is not None:
        recent_months = years * 12
    elif months is not None:
        recent_months = months
    if recent_months is not None:
        end = _current_month()
        start = _add_months(end, -(recent_months - 1))
        return _format_month(start), _format_month(end), f"recent_{recent_months}m"

    logged_end = _read_last_logged_end_month(log_path)
    if logged_end is not None:
        start = logged_end
        end = _current_month()
        return _format_month(start), _format_month(end), "history_incremental"

    master_max = _read_master_max_month(data_path)
    if master_max is not None:
        start = master_max
        end = _current_month()
        return _format_month(start), _format_month(end), "master_incremental"

    end = _current_month()
    start = _add_months(end, -11)
    return _format_month(start), _format_month(end), "default_first_12m"


def _append_collection_log(log_path: Path, payload: dict[str, Any]) -> None:
    log_path.parent.mkdir(parents=True, exist_ok=True)
    line = json.dumps(payload, ensure_ascii=False)
    with log_path.open("a", encoding="utf-8") as file:
        file.write(line + "\n")


def _merge_master(existing_df, incoming_df):
    import pandas as pd

    if existing_df is None or len(existing_df) == 0:
        merged = incoming_df.copy()
    elif incoming_df is None or len(incoming_df) == 0:
        merged = existing_df.copy()
    else:
        all_cols = list(dict.fromkeys(list(existing_df.columns) + list(incoming_df.columns)))
        existing = existing_df.reindex(columns=all_cols)
        incoming = incoming_df.reindex(columns=all_cols)
        merged = pd.concat([existing, incoming], ignore_index=True)

    preferred_keys = ["공고번호", "업체명", "순위", "투찰금액", "개찰일시"]
    dedupe_keys = [col for col in preferred_keys if col in merged.columns]
    if dedupe_keys:
        merged = merged.drop_duplicates(subset=dedupe_keys, keep="last")
    else:
        merged = merged.drop_duplicates(keep="last")

    if "개찰일시" in merged.columns:
        merged["_개찰정렬"] = pd.to_datetime(merged["개찰일시"], errors="coerce")
        merged = merged.sort_values("_개찰정렬", kind="stable").drop(columns=["_개찰정렬"])

    merged = merged.reset_index(drop=True)
    return merged


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="API 키 포함 Skill 기반 마스터 동기화 파이프라인"
    )
    parser.add_argument("--start-month", default=None, help="시작월 YYYY-MM (미지정 시 자동)")
    parser.add_argument("--end-month", default=None, help="종료월 YYYY-MM (미지정 시 시작월)")
    parser.add_argument("--start-datetime", default=None, help="시작시각 YYYYMMDDHHMM (옵션)")
    parser.add_argument("--end-datetime", default=None, help="종료시각 YYYYMMDDHHMM (옵션)")
    parser.add_argument("--months", type=int, default=None, help="최근 N개월 수집")
    parser.add_argument("--years", type=int, default=None, help="최근 N년 수집")
    parser.add_argument(
        "--keywords",
        default=",".join(DEFAULT_KEYWORDS),
        help='공고명 키워드(콤마구분). 빈 문자열("")이면 전체 공고 수집',
    )
    parser.add_argument(
        "--operations",
        default="servc",
        help="수집 오퍼레이션(콤마구분)",
    )
    parser.add_argument(
        "--key-source-env",
        default=str(DEFAULT_SOURCE_ENV),
        help="Skill 키 파일이 없을 때 가져올 원본 .env",
    )
    parser.add_argument(
        "--master-path",
        default=str(DEFAULT_MASTER_PATH),
        help="최종 누적 마스터 파일 경로",
    )
    parser.add_argument(
        "--output-dir",
        default=str(DEFAULT_OUTPUT_DIR),
        help="OpenAPI raw/clean 출력 디렉터리",
    )
    parser.add_argument(
        "--allowed-regions",
        default=",".join(DEFAULT_RULE_PROFILE.get("allowed_regions", ["전국", "경북"])),
        help="전처리 허용 지역 토큰(콤마구분)",
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
    parser.add_argument(
        "--replace-master",
        action="store_true",
        help="마스터 파일을 새 데이터로 교체",
    )
    parser.add_argument(
        "--skip-master-sync",
        action="store_true",
        help="마스터 파일 병합/저장을 생략하고 증분 결과(raw)만 생성",
    )
    parser.add_argument(
        "--collection-log",
        default=str(DEFAULT_COLLECTION_LOG),
        help="수집 실행 이력 JSONL 경로",
    )
    return parser


def main() -> int:
    import pandas as pd

    args = build_parser().parse_args()
    if args.min_participants <= 0:
        raise ValueError("--min-participants는 1 이상이어야 합니다.")

    log_path = Path(args.collection_log)
    master_path = Path(args.master_path)
    output_dir = Path(args.output_dir)

    key_source_env = _select_key_source_env(Path(args.key_source_env) if args.key_source_env else None)
    key_file = ensure_skill_api_key(key_source_env)

    if bool(args.start_datetime) != bool(args.end_datetime):
        raise ValueError("--start-datetime/--end-datetime는 함께 지정해야 합니다.")

    if args.start_datetime and args.end_datetime:
        start_dt = datetime.strptime(args.start_datetime, "%Y%m%d%H%M")
        end_dt = datetime.strptime(args.end_datetime, "%Y%m%d%H%M")
        if start_dt > end_dt:
            raise ValueError("start_datetime은 end_datetime보다 작거나 같아야 합니다.")
        start_month = start_dt.strftime("%Y-%m")
        end_month = end_dt.strftime("%Y-%m")
        period_source = "explicit_datetime"
    else:
        start_month, end_month, period_source = _resolve_period(
            start_month=args.start_month,
            end_month=args.end_month,
            months=args.months,
            years=args.years,
            data_path=master_path,
            log_path=log_path,
        )

    print("[Skill] API 키 연결:", key_file)
    print("[Skill] 키 원본:", key_source_env)
    print("[Skill] 수집 키워드:", args.keywords)
    print("[Skill] 수집 기간:", start_month, "~", end_month)
    if args.start_datetime and args.end_datetime:
        print("[Skill] 수집 시각:", args.start_datetime, "~", args.end_datetime)
    print("[Skill] 기간 결정 방식:", period_source)
    print("[Skill] 마스터 경로:", master_path)
    print("[Skill] 전처리 지역:", args.allowed_regions)
    print("[Skill] 전처리 최소업체수:", args.min_participants)
    print("[Skill] 전처리 대상업체:", args.target_company_terms)

    start_dt = _parse_month(start_month)
    end_dt = _parse_month(end_month)
    run_started_at = datetime.now().isoformat(timespec="seconds")

    if start_dt > end_dt:
        print("[안내] 신규 수집 구간이 없습니다.")
        _append_collection_log(
            log_path,
            {
                "timestamp": run_started_at,
                "status": "skipped_no_new_range",
                "period_source": period_source,
                "resolved_start_month": start_month,
                "resolved_end_month": end_month,
                "keywords": args.keywords,
                "operations": args.operations,
                "allowed_regions": args.allowed_regions,
                "min_participants": int(args.min_participants),
                "target_company_terms": args.target_company_terms,
            },
        )
        return 0

    mod = _load_skill_module()
    service_key = mod.load_service_key(key_file)
    keywords = [x.strip() for x in str(args.keywords).split(",") if x.strip()]
    operations = [x.strip() for x in str(args.operations).split(",") if x.strip()]

    try:
        config = mod.PipelineConfig(
            service_key=service_key,
            start_month=start_month,
            end_month=end_month,
            start_datetime=args.start_datetime,
            end_datetime=args.end_datetime,
            keywords=keywords,
            operations=operations,
            output_dir=output_dir,
            allowed_regions=[x.strip() for x in str(args.allowed_regions).split(",") if x.strip()],
            min_participants=int(args.min_participants),
            target_company_terms=[x.strip() for x in str(args.target_company_terms).split(",") if x.strip()],
            exclude_retired_company=not bool(args.include_retired_company),
        )

        report = mod.run_pipeline(config)
        raw_master_path = Path(report["output"]["all"])
        incoming_df = pd.read_excel(raw_master_path) if raw_master_path.exists() else pd.DataFrame()
        if args.skip_master_sync:
            merged_df = incoming_df.copy()
            dg_gb_path = daegu_gyeongbuk_master_path()
            if "지역제한" in merged_df.columns:
                dg_gb_df = merged_df[
                    merged_df["지역제한"].astype(str).apply(
                        lambda x: mod.has_allowed_region(
                            x,
                            ["경북"],
                            include_nationwide_override=False,
                        )
                    )
                ].copy()
            else:
                dg_gb_df = merged_df.iloc[0:0].copy()
        else:
            existing_df = (
                pd.read_excel(master_path)
                if (master_path.exists() and not args.replace_master)
                else pd.DataFrame()
            )
            merged_df = _merge_master(existing_df, incoming_df)

            master_path.parent.mkdir(parents=True, exist_ok=True)
            merged_df.to_excel(master_path, index=False)

            dg_gb_path = daegu_gyeongbuk_master_path()
            if "지역제한" in merged_df.columns:
                dg_gb_df = merged_df[
                    merged_df["지역제한"].astype(str).apply(
                        lambda x: mod.has_allowed_region(
                            x,
                            ["경북"],
                            include_nationwide_override=False,
                        )
                    )
                ].copy()
            else:
                dg_gb_df = merged_df.iloc[0:0].copy()
            dg_gb_df.to_excel(dg_gb_path, index=False)
    except Exception as exc:
        error_message = f"{exc.__class__.__name__}: {exc}"
        print(f"\n[ERROR] 마스터 동기화 실패: {error_message}")
        _append_collection_log(
            log_path,
            {
                "timestamp": run_started_at,
                "status": "failed",
                "error": error_message,
                "period_source": period_source,
                "resolved_start_month": start_month,
                "resolved_end_month": end_month,
                "keywords": args.keywords,
                "operations": args.operations,
                "allowed_regions": args.allowed_regions,
                "min_participants": int(args.min_participants),
                "target_company_terms": args.target_company_terms,
                "replace_master": bool(args.replace_master),
                "skip_master_sync": bool(args.skip_master_sync),
                "run_finished_at": datetime.now().isoformat(timespec="seconds"),
            },
        )
        return 1

    if args.skip_master_sync:
        print("\n[완료] 증분 결과 생성 (마스터 동기화 생략)")
        print(f"- raw_master: {raw_master_path}")
        print(f"- final_rows: {len(merged_df):,}")
    else:
        print("\n[완료] 단일 마스터 갱신")
        print(f"- raw_master: {raw_master_path}")
        print(f"- final_master: {master_path}")
        print(f"- final_rows: {len(merged_df):,}")
        print(f"- gyeongbuk_only: {dg_gb_path} ({len(dg_gb_df):,}행)")

    _append_collection_log(
        log_path,
        {
            "timestamp": run_started_at,
            "status": "completed",
            "period_source": period_source,
            "resolved_start_month": start_month,
            "resolved_end_month": end_month,
            "keywords": args.keywords,
            "operations": args.operations,
            "allowed_regions": args.allowed_regions,
            "min_participants": int(args.min_participants),
            "target_company_terms": args.target_company_terms,
            "replace_master": bool(args.replace_master),
            "skip_master_sync": bool(args.skip_master_sync),
            "raw_master_path": str(raw_master_path),
            "master_path": str(master_path),
            "gyeongbuk_path": str(dg_gb_path),
            "raw_rows": int(len(incoming_df)),
            "final_rows": int(len(merged_df)),
            "run_finished_at": datetime.now().isoformat(timespec="seconds"),
        },
    )
    print(f"- collection_log: {log_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
