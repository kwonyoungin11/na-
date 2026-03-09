#!/usr/bin/env python3
"""Competition-only Shadow Strategy Analyzer.

나라장터 복수예가 데이터에서 낙찰 빈도 대신 경쟁업체 행동만 분석한다.
- 고정업체 탐지
- 최근/중기 경쟁밀도 및 과열도 분석
- 공백 지속성 분석
- 고정업체 왼쪽의 덜 붐비는 위치 추천
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from datetime import datetime

script_dir = Path(__file__).parent
module_dir = script_dir.parent / "modules"
repo_src_dir = Path(__file__).resolve().parents[4] / "cc-feature-implementer" / "src"
sys.path.insert(0, str(module_dir))
sys.path.insert(0, str(repo_src_dir))

from conditional_advantage_strategy import build_conditional_advantage_payload
from competition_profile import CompetitionProfileAnalyzer
from data_loader import DataLoader
from fixed_bidder import FixedBidderDetector
from gap_analyzer import GapAnalyzer
from profile_loader import DEFAULT_ANALYSIS_PROFILE_PATH, load_analysis_profile
from shadow_calculator import ShadowCalculator
from trend_analyzer import TrendAnalyzer


class ShadowStrategyAnalyzer:
    """경쟁업체 전용 가림전략 통합 분석기."""

    def __init__(
        self,
        data_path: str | None = None,
        output_dir: str | None = None,
        my_company: str | None = None,
        profile: dict | None = None,
    ):
        self.data_path = data_path
        self.output_dir = Path(output_dir or script_dir.parent / "output")
        self.output_dir.mkdir(exist_ok=True, parents=True)
        self.profile = profile or {}
        default_company = self.profile.get("cli_defaults", {}).get("my_company", "동국문화재연구원")
        self.my_company = (my_company if my_company is not None else default_company or "").strip()

        self.loader: DataLoader | None = None
        self.fixed_bidders: list[dict] = []
        self.trends: list[dict] = []
        self.competition_profiles: list[dict] = []
        self.gaps: list[dict] = []
        self.recommendations: list[dict] = []
        self.conditional_advantage: dict = {}
        self.summary: dict = {}

    def _decision_rules(self) -> dict:
        return self.profile.get("decision_rules", {})

    @staticmethod
    def _normalize_company_text(value: str) -> str:
        return str(value or "").replace(" ", "").strip()

    def _filter_my_company(self, df):
        if not self.my_company or "업체명" not in df.columns:
            return df, 0
        target = self._normalize_company_text(self.my_company)
        mask = df["업체명"].astype(str).map(self._normalize_company_text).str.contains(target, na=False)
        matched = int(mask.sum())
        return df.loc[~mask].copy(), matched

    @staticmethod
    def _format_band(zone_start: float, zone_end: float) -> str:
        return f"{zone_start:.3f}~{zone_end:.3f}%"

    def _build_reason_codes(self, recommendation: dict) -> list[str]:
        rules = self._decision_rules()
        codes: list[str] = []
        anchor_company = str(recommendation.get("anchor_company") or recommendation.get("nearest_right_company") or "").strip()
        left_space_raw = recommendation.get("left_space_width")
        left_space_width = None if left_space_raw is None else float(left_space_raw)
        left_boundary_distance = float(recommendation.get("left_boundary_distance") or 0.0)
        density_penalty = float(recommendation.get("density_penalty") or 0.0)
        overheat_penalty = float(recommendation.get("overheat_penalty") or 0.0)
        volatility_penalty = float(recommendation.get("volatility_penalty") or 0.0)
        priority_score = float(recommendation.get("priority_score") or 0.0)

        if anchor_company:
            codes.append("LEFT_OF_COLLISION_ANCHOR")
        if left_space_width is None:
            codes.append("LEFT_SPACE_UNVERIFIED")
            if left_boundary_distance >= float(rules.get("boundary_clear_min_distance", 0.002)):
                codes.append("LEFT_BOUNDARY_CLEAR")
        elif left_space_width >= float(rules.get("min_left_space_width", 0.002)) * 1.5:
            codes.append("LEFT_SPACE_WIDE")
        elif left_space_width > 0:
            codes.append("LEFT_SPACE_TOO_NARROW")
        if density_penalty >= float(rules.get("forbidden_density_score", 65.0)):
            codes.append("EXCESS_DENSITY")
        if overheat_penalty >= float(rules.get("forbidden_overheat_score", 60.0)):
            codes.append("OVERHEAT")
        if float(recommendation.get("fixed_score") or 0.0) >= 75:
            codes.append("FIXED_BIDDER_OCCUPANCY")
        if (
            density_penalty <= float(rules.get("less_obvious_density_score", 35.0))
            and overheat_penalty <= float(rules.get("low_newcomer_overheat_score", 35.0))
        ):
            codes.append("LESS_OBVIOUS")
        if overheat_penalty <= float(rules.get("low_newcomer_overheat_score", 35.0)):
            codes.append("LOW_NEWCOMER_RATIO")
        if (
            priority_score < float(rules.get("candidate_min_opportunity_score", 55.0))
            or volatility_penalty >= float(rules.get("uncertainty_high_volatility_penalty", 45.0))
        ):
            codes.append("UNCERTAINTY_HIGH")
        return list(dict.fromkeys(codes))

    def _classify_uncertainty(self, recommendation: dict) -> str:
        rules = self._decision_rules()
        priority_score = float(recommendation.get("priority_score") or 0.0)
        volatility_penalty = float(recommendation.get("volatility_penalty") or 0.0)
        overheat_penalty = float(recommendation.get("overheat_penalty") or 0.0)
        if (
            priority_score < float(rules.get("candidate_min_opportunity_score", 55.0))
            or volatility_penalty >= float(rules.get("uncertainty_high_volatility_penalty", 45.0))
            or overheat_penalty >= float(rules.get("forbidden_overheat_score", 60.0))
        ):
            return "높음"
        if (
            priority_score < float(rules.get("candidate_min_opportunity_score", 55.0)) + 17.0
            or volatility_penalty >= float(rules.get("uncertainty_medium_volatility_penalty", 25.0))
            or overheat_penalty >= float(rules.get("low_newcomer_overheat_score", 35.0))
        ):
            return "중간"
        return "낮음"

    def _decorate_recommendations(self, recommendations: list[dict]) -> list[dict]:
        decorated: list[dict] = []
        for row in recommendations:
            current = dict(row)
            anchor_company = str(current.get("anchor_company") or current.get("nearest_right_company") or "").strip()
            anchor_gap_left = current.get("anchor_gap_left") or current.get("nearest_right_distance")
            left_space_width = current.get("left_space_width")
            left_boundary_distance = current.get("left_boundary_distance")
            uncertainty = self._classify_uncertainty(current)
            reason_codes = self._build_reason_codes(
                {
                    **current,
                    "anchor_company": anchor_company,
                    "anchor_gap_left": anchor_gap_left,
                    "left_space_width": left_space_width,
                }
            )
            anchor_text = f"{anchor_company} 왼쪽" if anchor_company else "좌측 후보"
            left_space_text = f"{float(left_space_width):.5f}%" if left_space_width is not None else "-"
            left_boundary_text = f"{float(left_boundary_distance):.5f}%" if left_boundary_distance is not None else "-"
            current.update(
                {
                    "decision_category": "좌측선점 후보밴드",
                    "anchor_company": anchor_company,
                    "anchor_gap_left": None if anchor_gap_left is None else round(float(anchor_gap_left), 5),
                    "left_space_width": None if left_space_width is None else round(float(left_space_width), 5),
                    "left_boundary_distance": None if left_boundary_distance is None else round(float(left_boundary_distance), 5),
                    "uncertainty": uncertainty,
                    "reason_codes": reason_codes,
                    "reason": (
                        f"{anchor_text} · 좌측여유 {left_space_text}"
                        if left_space_width is not None
                        else f"{anchor_text} · 좌측앵커없음(경계 {left_boundary_text})"
                    ) + f" · 불확실성 {uncertainty}",
                }
            )
            decorated.append(current)
        return decorated

    def _build_conditional_advantage(self) -> dict:
        return build_conditional_advantage_payload(
            {},
            {},
            {"zones": self.competition_profiles},
            {"gaps": self.gaps},
            {
                "summary": {"fixed_bidders": len(self.fixed_bidders)},
                "recommendations": self.recommendations,
            },
            decision_rules=self.profile.get("decision_rules"),
        )

    def run(self, period: str | None = None, min_rate: float | None = None, max_rate: float | None = None):
        rate_profile = self.profile.get("rate_range", {})
        cli_defaults = self.profile.get("cli_defaults", {})
        period = period or cli_defaults.get("shadow_period", "2y")
        min_rate = float(rate_profile.get("min", 99.5) if min_rate is None else min_rate)
        max_rate = float(rate_profile.get("max", 100.0) if max_rate is None else max_rate)
        print("=" * 64)
        print("Competition-only Shadow Strategy Analyzer")
        print(f"분석 시작: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"기초대비사정률 필터: {min_rate}% ~ {max_rate}%")
        print(f"분석 기간: {period}")
        if self.my_company:
            print(f"자사 기준: {self.my_company}")
        print("=" * 64)

        print("\n[1/5] 데이터 로드 및 자사 기준 확인...")
        self.loader = DataLoader(
            self.data_path,
            period_days=self.profile.get("period_days"),
            same_day_collapse=self.profile.get("same_day_collapse"),
        )
        self.loader.load().filter_range(min_rate, max_rate).filter_period(period).collapse_same_day_clusters()
        df = self.loader.get_data().copy()
        df, matched_self_rows = self._filter_my_company(df)

        summary = self.loader.get_summary()
        print(f"  - 총 레코드: {summary['total_records']:,}건")
        collapse_summary = summary.get("same_day_collapse") or {}
        if collapse_summary:
            print(
                "  - 동일업체·동일일자 근접묶음: "
                f"{collapse_summary.get('collapsed_cluster_count', 0):,}개 "
                f"(감소 {collapse_summary.get('reduced_rows', 0):,}건)"
            )
        print(f"  - 자사 제외: {matched_self_rows:,}건")
        print(f"  - 분석 사용 행수: {len(df):,}건")
        print(f"  - 업체 수: {df['업체명'].nunique() if '업체명' in df.columns else 0}개")
        print(f"  - 공고 수: {df['공고번호'].nunique() if '공고번호' in df.columns else 0}개")

        print("\n[2/5] 고정업체 탐지...")
        detector = FixedBidderDetector(df, profile=self.profile)
        self.fixed_bidders = detector.detect()
        print(f"  - 고정업체: {len(self.fixed_bidders)}개")
        if not self.fixed_bidders:
            print("  [경고] 고정업체가 없습니다. 분석 종료.")
            self.summary = {
                "period": period,
                "records": len(df),
                "fixed_bidders": 0,
                "competition_zones": 0,
                "gap_zones": 0,
                "recommendations": 0,
                "self_rows_excluded": matched_self_rows,
                "self_rows_matched": matched_self_rows,
                "self_company_policy": "excluded_from_market_structure",
            }
            self.conditional_advantage = {
                "system_view": {
                    "compression_level": "unknown",
                    "predictability": "낮음",
                    "market_consensus_band": None,
                    "obvious_band_candidates": [],
                    "left_space_requirement": 0.0,
                },
                "market_view": {"forbidden_zones": [], "boundary_zones": []},
                "relation_view": {
                    "collision_anchors": [],
                    "primary_anchor": "",
                    "anchor_gap_left": None,
                    "left_space_width": None,
                    "uncertainty": "높음",
                },
                "decision_view": {"candidate_bands": [], "decision_principle": "single_point_disabled"},
                "summary": {
                    "compression_level": "unknown",
                    "predictability": "낮음",
                    "forbidden_zone_count": 0,
                    "boundary_zone_count": 0,
                    "candidate_band_count": 0,
                    "primary_anchor": "",
                },
            }
            return self

        print("\n[3/5] 경쟁 밀도/이동 분석...")
        trend_analyzer = TrendAnalyzer(df, self.fixed_bidders, profile=self.profile)
        self.trends = trend_analyzer.analyze()
        competition_analyzer = CompetitionProfileAnalyzer(df, self.fixed_bidders, min_rate=min_rate, max_rate=max_rate, profile=self.profile)
        self.competition_profiles = competition_analyzer.analyze()
        dense_top = sorted(self.competition_profiles, key=lambda x: x.get("density_score", 0), reverse=True)[:3]
        print("  - 혼잡 상위 구간:")
        for row in dense_top:
            print(f"    {row['zone']}: 혼잡 {row['density_score']:.1f} / 과열 {row['overheat_score']:.1f}")

        print("\n[4/5] 공백 지속성 분석...")
        gap_analyzer = GapAnalyzer(self.competition_profiles, self.fixed_bidders, min_rate=min_rate, max_rate=max_rate, profile=self.profile)
        self.gaps = gap_analyzer.analyze()
        best_gaps = gap_analyzer.get_best_opportunities(3)
        print("  - 공백 지속성 상위 구간:")
        for row in best_gaps:
            print(
                f"    {row['zone']}: 공백 {row['persistence_score']:.1f} / 앵커 {row['anchor_score']:.1f} / 기회 {row['opportunity_score']:.1f}"
            )

        print("\n[5/5] 경쟁업체 기반 추천 계산...")
        shadow_calc = ShadowCalculator(
            self.fixed_bidders,
            self.trends,
            self.gaps,
            self.competition_profiles,
            my_company=self.my_company,
            profile=self.profile,
        )
        self.recommendations = shadow_calc.calculate()
        self.recommendations = self._decorate_recommendations(self.recommendations)
        self.conditional_advantage = self._build_conditional_advantage()

        self.summary = {
            "period": period,
            "records": len(df),
            "fixed_bidders": len(self.fixed_bidders),
            "competition_zones": len(self.competition_profiles),
            "gap_zones": len(self.gaps),
            "recommendations": len(self.recommendations),
            "self_rows_excluded": matched_self_rows,
            "self_rows_matched": matched_self_rows,
            "self_company_policy": "excluded_from_market_structure",
            "conditional_advantage_summary": self.conditional_advantage.get("summary", {}),
        }

        print("\n" + "=" * 64)
        print("분석 완료")
        print("=" * 64)
        return self

    def save_results(self, output_format: str = "json") -> None:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        if output_format in {"json", "all"}:
            self._save_json(timestamp)
        if output_format in {"excel", "all"}:
            self._save_excel(timestamp)
        self._save_markdown_summary()

    def _save_json(self, timestamp: str) -> None:
        payloads = {
            "fixed_bidders.json": {"fixed_bidders": self.fixed_bidders, "timestamp": timestamp},
            "trend_report.json": {"trends": self.trends, "timestamp": timestamp},
            "competition_profile.json": {"zones": self.competition_profiles, "timestamp": timestamp},
            "gap_analysis.json": {"gaps": self.gaps, "timestamp": timestamp},
            "conditional_advantage.json": {
                "conditional_advantage": self.conditional_advantage,
                "summary": self.summary,
                "timestamp": timestamp,
            },
            "shadow_strategy.json": {
                "recommendations": self.recommendations,
                "conditional_advantage": self.conditional_advantage,
                "conditional_advantage_summary": self.conditional_advantage.get("summary", {}),
                "summary": self.summary,
                "timestamp": timestamp,
            },
            "probability_table.json": {
                "deprecated": True,
                "message": "competition-only 전략에서는 낙찰확률/1위빈도를 사용하지 않습니다.",
                "timestamp": timestamp,
            },
        }
        for filename, payload in payloads.items():
            with open(self.output_dir / filename, "w", encoding="utf-8") as handle:
                json.dump(payload, handle, ensure_ascii=False, indent=2)
        print(f"\n[저장 완료] JSON 파일: {self.output_dir}")

    def _save_excel(self, timestamp: str) -> None:
        import pandas as pd

        output_file = self.output_dir / f"shadow_strategy_report_{timestamp}.xlsx"
        with pd.ExcelWriter(output_file, engine="openpyxl") as writer:
            has_sheet = False
            if self.fixed_bidders:
                pd.DataFrame(self.fixed_bidders).to_excel(writer, sheet_name="고정업체", index=False)
                has_sheet = True
            if self.trends:
                pd.DataFrame(self.trends).to_excel(writer, sheet_name="이동분석", index=False)
                has_sheet = True
            if self.competition_profiles:
                pd.DataFrame(self.competition_profiles).to_excel(writer, sheet_name="경쟁밀도", index=False)
                has_sheet = True
            if self.gaps:
                pd.DataFrame(self.gaps).to_excel(writer, sheet_name="공백지속", index=False)
                has_sheet = True
            if self.recommendations:
                pd.DataFrame(self.recommendations).to_excel(writer, sheet_name="추천전략", index=False)
                has_sheet = True
            pd.DataFrame([self.summary]).to_excel(writer, sheet_name="요약", index=False)
            has_sheet = True
            if not has_sheet:
                pd.DataFrame([{"상태": "분석대상 없음", "사유": "필터 결과 0건"}]).to_excel(writer, sheet_name="요약", index=False)
        print(f"\n[저장 완료] Excel 파일: {output_file}")

    def _save_markdown_summary(self) -> None:
        lines = [
            "# 경쟁업체 전용 추천 요약",
            "",
            f"- 기간: {self.summary.get('period', '')}",
            f"- 자사 감지 행수: {self.summary.get('self_rows_matched', 0):,}",
            f"- 자사 제외 행수: {self.summary.get('self_rows_excluded', 0):,}",
            f"- 자사 정책: {self.summary.get('self_company_policy', '-')}",
            f"- 고정업체 수: {self.summary.get('fixed_bidders', 0):,}",
            f"- 추천 수: {self.summary.get('recommendations', 0):,}",
            "",
        ]
        conditional = self.conditional_advantage or {}
        system_view = conditional.get("system_view", {})
        market_view = conditional.get("market_view", {})
        relation_view = conditional.get("relation_view", {})
        decision_view = conditional.get("decision_view", {})

        if system_view:
            lines.extend(
                [
                    "## 시스템 해석",
                    f"- 압축도: {system_view.get('compression_level', '-')}",
                    f"- 예측가능성: {system_view.get('predictability', '-')}",
                    f"- 중심 합의밴드: {system_view.get('market_consensus_band', '-')}",
                    f"- 최소 좌측여유 기준: {system_view.get('left_space_requirement', '-')}",
                    "",
                ]
            )

        forbidden = market_view.get("forbidden_zones", [])
        boundary = market_view.get("boundary_zones", [])
        lines.append("## 제거 우선 구간")
        if not forbidden:
            lines.append("- 제거 우선 구간 없음")
        else:
            for row in forbidden[:8]:
                band = row.get("zone")
                if not band:
                    band_values = row.get("band") or []
                    band = f"{band_values[0]}~{band_values[1]}%" if len(band_values) >= 2 else "-"
                lines.append(f"- {band} | 사유코드: {', '.join(row.get('reason_codes', [])) or '-'}")

        lines.extend(["", "## 경계 구간"])
        if not boundary:
            lines.append("- 경계 구간 없음")
        else:
            for row in boundary[:8]:
                band = row.get("zone")
                if not band:
                    band_values = row.get("band") or []
                    band = f"{band_values[0]}~{band_values[1]}%" if len(band_values) >= 2 else "-"
                lines.append(f"- {band} | 사유코드: {', '.join(row.get('reason_codes', [])) or '-'}")

        anchors = relation_view.get("collision_anchors", [])
        lines.extend(["", "## 반복 충돌앵커"])
        if not anchors:
            lines.append("- 반복 충돌앵커 없음")
        else:
            for row in anchors[:8]:
                lines.append(
                    f"- {row.get('company', '-')}: 고정위치 {row.get('fixed_position', '-')} / 고정성 {row.get('fixed_score', '-')} / 90일 점유 {row.get('recent_share_90d', '-')}"
                )

        lines.extend(["", "## 좌측선점 후보밴드"])
        if not self.recommendations:
            lines.append("- 추천 없음")
        else:
            for row in self.recommendations[:10]:
                lines.extend(
                    [
                        f"- {row['priority']}위 | 앵커 {row.get('anchor_company') or row['target_company']} | 구간 {row['band_min']:.5f}~{row['band_max']:.5f}% | 좌측여유 {row.get('left_space_width', '-')}",
                        f"  - 불확실성: {row.get('uncertainty', '-')}",
                        f"  - 사유코드: {', '.join(row.get('reason_codes', [])) or '-'}",
                        f"  - 설명: {row['reason']}",
                    ]
                )
        (self.output_dir / "recommendation.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    def print_summary(self) -> None:
        print("\n" + "=" * 64)
        print("분석 결과 요약")
        print("=" * 64)
        if not self.recommendations:
            print("추천 없음")
            return
        for rec in self.recommendations[:10]:
            print(f"\n{rec['priority']}순위: {rec['target_company']}")
            print(f"  추천위치: {rec['recommended_rate']:.5f}%")
            print(f"  권장구간: {rec['band_min']:.5f}% ~ {rec['band_max']:.5f}%")
            print(f"  결정분류: {rec.get('decision_category', '-')}")
            print(f"  충돌앵커: {rec.get('anchor_company', '-')}")
            print(f"  좌측여유: {rec.get('left_space_width', '-')}")
            print(f"  불확실성: {rec.get('uncertainty', '-')}")
            print(f"  고정성: {rec['fixed_score']:.1f} / 공백: {rec['gap_score']:.1f} / 혼잡패널티: {rec['density_penalty']:.1f} / 과열패널티: {rec['overheat_penalty']:.1f}")
            print(f"  종합점수: {rec['priority_score']:.1f}")
            print(f"  추천이유: {rec['reason']}")


def main() -> None:
    pre_parser = argparse.ArgumentParser(add_help=False)
    pre_parser.add_argument("--profile", default=str(DEFAULT_ANALYSIS_PROFILE_PATH), help="분석 규칙 프로필 JSON 경로")
    pre_args, _ = pre_parser.parse_known_args()
    profile, profile_path = load_analysis_profile(pre_args.profile)

    parser = argparse.ArgumentParser(description="Competition-only Shadow Strategy Analyzer")
    parser.add_argument("--profile", default=str(profile_path), help="분석 규칙 프로필 JSON 경로")
    parser.add_argument("--data", default=None, help="데이터 파일 경로")
    parser.add_argument("--output-dir", default=None, help="결과 저장 디렉터리")
    parser.add_argument("--period", default=profile["cli_defaults"]["shadow_period"], choices=list(profile["period_days"].keys()), help="분석 기간")
    parser.add_argument("--output", default=profile["cli_defaults"]["output_format"], choices=["json", "excel", "all"], help="출력 형식")
    parser.add_argument("--min-rate", type=float, default=float(profile["rate_range"]["min"]), help="최소 기초대비사정률")
    parser.add_argument("--max-rate", type=float, default=float(profile["rate_range"]["max"]), help="최대 기초대비사정률")
    parser.add_argument("--my-company", default=profile["cli_defaults"]["my_company"], help="자사명(경쟁업체 시장구조 계산에서 제외)")
    args = parser.parse_args()

    profile, _ = load_analysis_profile(args.profile)
    analyzer = ShadowStrategyAnalyzer(data_path=args.data, output_dir=args.output_dir, my_company=args.my_company, profile=profile)
    analyzer.run(period=args.period, min_rate=args.min_rate, max_rate=args.max_rate)
    analyzer.print_summary()
    analyzer.save_results(args.output)


if __name__ == "__main__":
    main()
