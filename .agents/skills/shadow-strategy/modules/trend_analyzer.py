#!/usr/bin/env python3
"""투찰 전략 변화 분석 모듈.

사정률 추세가 아니라 업체의 투찰 위치 이동 방향과 최근 분산을 본다.
"""

from __future__ import annotations

from datetime import datetime, timedelta

import numpy as np
import pandas as pd


class TrendAnalyzer:
    """시간대별 경쟁업체 이동 분석기."""

    def __init__(self, df: pd.DataFrame, fixed_bidders: list[dict], profile: dict | None = None) -> None:
        period_days = (profile or {}).get("period_days", {"2y": 730, "1y": 365, "6m": 180, "3m": 90, "1m": 30})
        trend_profile = (profile or {}).get("trend", {})
        self.df = df.copy()
        self.fixed_bidders = fixed_bidders
        self.results: list[dict] = []
        self.periods = {name: timedelta(days=int(days)) for name, days in period_days.items()}
        self.direction_threshold = float(trend_profile.get("direction_threshold", 0.006))
        self.recent_sample_size = int(trend_profile.get("recent_sample_size", 8))

    def analyze(self) -> list[dict]:
        rate_col = "기초대비사정률"
        company_col = "업체명"
        date_col = "투찰일시"

        if date_col not in self.df.columns:
            print("[TrendAnalyzer] 경고: 투찰일시 컬럼 없음, 추세 분석 불가")
            return []

        work = self.df.copy()
        work[date_col] = pd.to_datetime(work[date_col], errors="coerce")
        work[rate_col] = pd.to_numeric(work[rate_col], errors="coerce")
        anchor_time = work[date_col].dropna().max()
        if pd.isna(anchor_time):
            anchor_time = datetime.now()

        self.results = []
        for bidder in self.fixed_bidders:
            company = bidder["company"]
            company_df = work[work[company_col] == company].copy()
            if company_df.empty:
                continue

            period_avgs: dict[str, float | int | None] = {}
            period_medians: dict[str, float | None] = {}
            for period_name, period_delta in self.periods.items():
                cutoff = anchor_time - period_delta
                period_df = company_df[company_df[date_col] >= cutoff]
                if period_df.empty:
                    period_avgs[f"period_{period_name}"] = None
                    period_avgs[f"count_{period_name}"] = 0
                    period_medians[f"median_{period_name}"] = None
                    continue

                rates = period_df[rate_col].dropna().astype(float)
                if rates.empty:
                    period_avgs[f"period_{period_name}"] = None
                    period_avgs[f"count_{period_name}"] = 0
                    period_medians[f"median_{period_name}"] = None
                    continue

                period_avgs[f"period_{period_name}"] = round(float(rates.mean()), 5)
                period_avgs[f"count_{period_name}"] = int(len(rates))
                period_medians[f"median_{period_name}"] = round(float(rates.median()), 5)

            direction, velocity = self._calculate_trend(period_medians, period_avgs, self.direction_threshold)
            recent_df = company_df.sort_values(date_col, ascending=False).head(self.recent_sample_size)
            recent_rates = recent_df[rate_col].dropna().astype(float)
            recent_avg = float(recent_rates.mean()) if not recent_rates.empty else None
            recent_median = float(recent_rates.median()) if not recent_rates.empty else None
            recent_std = float(recent_rates.std()) if len(recent_rates) > 1 else 0.0
            recent_min = float(recent_rates.min()) if not recent_rates.empty else None
            recent_max = float(recent_rates.max()) if not recent_rates.empty else None

            self.results.append(
                {
                    "company": company,
                    "main_zone": bidder["main_zone"],
                    "concentration": bidder.get("concentration", 0),
                    **period_avgs,
                    **period_medians,
                    "direction": direction,
                    "velocity": round(float(velocity), 5) if velocity is not None else None,
                    "recent_avg": round(recent_avg, 5) if recent_avg is not None else None,
                    "recent_median": round(recent_median, 5) if recent_median is not None else None,
                    "recent_std": round(recent_std, 5),
                    "recent_min": round(recent_min, 5) if recent_min is not None else None,
                    "recent_max": round(recent_max, 5) if recent_max is not None else None,
                    "recent_count": int(len(recent_rates)),
                }
            )

        print(f"[TrendAnalyzer] 추세 분석 완료: {len(self.results)}개 업체")
        return self.results

    @staticmethod
    def _calculate_trend(
        period_medians: dict[str, float | None],
        period_avgs: dict[str, float | int | None],
        direction_threshold: float,
    ) -> tuple[str, float | None]:
        long_term = period_medians.get("median_1y") or period_medians.get("median_2y") or period_avgs.get("period_1y") or period_avgs.get("period_2y")
        short_term = period_medians.get("median_1m") or period_medians.get("median_3m") or period_avgs.get("period_1m") or period_avgs.get("period_3m")
        if long_term is None or short_term is None:
            return "알수없음", None

        diff = float(short_term) - float(long_term)
        if diff > direction_threshold:
            direction = "상향조정"
        elif diff < -direction_threshold:
            direction = "하향조정"
        else:
            direction = "유지"

        velocity = diff / 12.0
        return direction, velocity

    def get_results(self) -> list[dict]:
        return self.results

    def to_dataframe(self) -> pd.DataFrame:
        if not self.results:
            return pd.DataFrame()
        return pd.DataFrame(
            [
                {
                    "업체명": r["company"],
                    "주_투찰구간": r["main_zone"],
                    "집중도(%)": r["concentration"],
                    "2년_평균": r.get("period_2y"),
                    "1년_평균": r.get("period_1y"),
                    "6개월_평균": r.get("period_6m"),
                    "3개월_평균": r.get("period_3m"),
                    "1개월_평균": r.get("period_1m"),
                    "1개월_중앙값": r.get("median_1m"),
                    "추세": r["direction"],
                    "변화속도": r["velocity"],
                    "최근_중앙값": r.get("recent_median"),
                    "최근_표준편차": r.get("recent_std"),
                }
                for r in self.results
            ]
        )
