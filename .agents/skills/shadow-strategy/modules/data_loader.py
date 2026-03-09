#!/usr/bin/env python3
"""
데이터 로더 모듈
전처리된 입찰 데이터를 로드하고 필터링
"""

from datetime import datetime, timedelta
from pathlib import Path
import sys

import pandas as pd

PROJECT_ROOT = Path(__file__).resolve().parents[4]
REPO_SRC_DIR = PROJECT_ROOT / "cc-feature-implementer" / "src"
if str(REPO_SRC_DIR) not in sys.path:
    sys.path.insert(0, str(REPO_SRC_DIR))

from company_day_cluster import collapse_same_company_day_clusters


class DataLoader:
    """전처리된 입찰 데이터 로더"""

    DEFAULT_DATA_PATH = PROJECT_ROOT / "전체_통합_데이터.xlsx"

    def __init__(
        self,
        data_path=None,
        *,
        period_days: dict[str, int] | None = None,
        same_day_collapse: dict | None = None,
    ):
        self.data_path = Path(data_path or self.DEFAULT_DATA_PATH)
        self.df = None
        self.filtered_df = None
        self.period_days = dict(period_days or {"all": 0, "1m": 30, "3m": 90, "6m": 180, "1y": 365, "2y": 730})
        self.same_day_collapse = dict(same_day_collapse or {})
        self.same_day_collapse_stats: dict | None = None

    def load(self):
        """데이터 로드"""
        if not self.data_path.exists():
            raise FileNotFoundError(f"데이터 파일 없음: {self.data_path}")

        self.df = pd.read_excel(self.data_path, engine='openpyxl')
        self._normalize_columns()
        print(f"[DataLoader] 데이터 로드: {len(self.df)}건")
        return self

    def _normalize_columns(self):
        """입력 컬럼명을 분석 모듈 공통 스키마로 정규화."""
        if self.df is None:
            return

        rename_map = {}
        if '기초대비사정률' not in self.df.columns and '기초대비사정률(%)' in self.df.columns:
            rename_map['기초대비사정률(%)'] = '기초대비사정률'
        if '예가대비투찰률' not in self.df.columns and '예가대비투찰률(%)' in self.df.columns:
            rename_map['예가대비투찰률(%)'] = '예가대비투찰률'
        if '기초대비투찰률' not in self.df.columns and '기초대비투찰률(%)' in self.df.columns:
            rename_map['기초대비투찰률(%)'] = '기초대비투찰률'

        if rename_map:
            self.df = self.df.rename(columns=rename_map)

    def filter_range(self, min_rate=99.5, max_rate=100.0):
        """기초대비사정률 범위 필터링

        사용자 전략: 사정률 99.5~100.0% 구간의 공고에서 고정업체 탐지
        - 기초대비사정률: (예정가격 / 기초금액) × 100
        - 이 범위의 공고만 필터링하여 분석
        """
        if self.df is None:
            raise ValueError("데이터가 로드되지 않음. load() 먼저 실행")

        # 기초대비사정률 컬럼으로 필터링 (투찰률이 아닌 사정률!)
        rate_col = '기초대비사정률'
        if rate_col not in self.df.columns:
            raise ValueError(f"컬럼 없음: {rate_col}")

        # 숫자형 변환 및 필터링
        self.df[rate_col] = pd.to_numeric(self.df[rate_col], errors='coerce')
        self.filtered_df = self.df[
            (self.df[rate_col] >= min_rate) &
            (self.df[rate_col] <= max_rate)
        ].copy()

        print(f"[DataLoader] 기초대비사정률 {min_rate}~{max_rate}% 필터링: {len(self.filtered_df)}건")
        return self

    def filter_period(self, period='2y'):
        """기간별 필터링

        Args:
            period: 'all', '1m', '3m', '6m', '1y', '2y'
        """
        if self.filtered_df is None:
            self.filtered_df = self.df.copy()

        # 투찰일시 컬럼 확인
        date_col = '투찰일시'
        if date_col not in self.filtered_df.columns:
            print(f"[DataLoader] 경고: {date_col} 컬럼 없음, 기간 필터링 생략")
            return self

        # 날짜 변환
        self.filtered_df[date_col] = pd.to_datetime(self.filtered_df[date_col], errors='coerce')

        # 기간 계산
        now = datetime.now()
        period_map = {key: timedelta(days=int(days)) for key, days in self.period_days.items()}

        if period in {"all", "전체", "전체기간"}:
            print("[DataLoader] 기간 필터링 (all): 전체기간 사용")
            return self

        if period not in period_map:
            print(f"[DataLoader] 알 수 없는 기간: {period}, 전체 사용")
            return self

        if period_map[period] <= timedelta(0):
            print(f"[DataLoader] 기간 필터링 ({period}): 전체기간 사용")
            return self

        cutoff = now - period_map[period]
        self.filtered_df = self.filtered_df[
            self.filtered_df[date_col] >= cutoff
        ].copy()

        print(f"[DataLoader] 기간 필터링 ({period}): {len(self.filtered_df)}건")
        return self

    def collapse_same_day_clusters(self):
        """같은 업체의 동일일자 근접 다건을 대표 1건으로 축약."""
        target_df = self.filtered_df if self.filtered_df is not None else self.df
        if target_df is None:
            raise ValueError("데이터가 로드되지 않음. load() 먼저 실행")

        config = {
            "enabled": True,
            "rate_tolerance": 0.003,
            "min_cluster_size": 2,
            "primary_date_column": "개찰일시",
            "fallback_date_column": "투찰일시",
            **self.same_day_collapse,
        }
        if not bool(config.get("enabled", True)):
            self.same_day_collapse_stats = {
                "input_rows": int(len(target_df)),
                "output_rows": int(len(target_df)),
                "reduced_rows": 0,
                "collapsed_cluster_count": 0,
                "rate_tolerance": float(config.get("rate_tolerance", 0.003)),
                "min_cluster_size": int(config.get("min_cluster_size", 2)),
            }
            return self

        collapsed_df, stats = collapse_same_company_day_clusters(
            target_df,
            company_col="업체명",
            rate_col="기초대비사정률",
            primary_date_col=str(config.get("primary_date_column") or "개찰일시"),
            fallback_date_col=str(config.get("fallback_date_column") or "투찰일시"),
            rate_tolerance=float(config.get("rate_tolerance", 0.003)),
            min_cluster_size=int(config.get("min_cluster_size", 2)),
        )
        self.same_day_collapse_stats = stats
        if self.filtered_df is not None:
            self.filtered_df = collapsed_df
        else:
            self.df = collapsed_df

        print(
            "[DataLoader] 동일업체·동일일자 근접묶음 축약: "
            f"{stats['input_rows']}건 → {stats['output_rows']}건 "
            f"(감소 {stats['reduced_rows']}건, 대표묶음 {stats['collapsed_cluster_count']}개)"
        )
        return self

    def get_data(self):
        """필터링된 데이터 반환"""
        if self.filtered_df is not None:
            return self.filtered_df
        return self.df

    def get_summary(self):
        """데이터 요약 정보"""
        df = self.get_data()
        return {
            'total_records': len(df),
            'unique_companies': df['업체명'].nunique() if '업체명' in df.columns else 0,
            'unique_announcements': df['공고번호'].nunique() if '공고번호' in df.columns else 0,
            'same_day_collapse': self.same_day_collapse_stats or {},
            'date_range': {
                'min': str(df['투찰일시'].min()) if '투찰일시' in df.columns else None,
                'max': str(df['투찰일시'].max()) if '투찰일시' in df.columns else None,
            }
        }
