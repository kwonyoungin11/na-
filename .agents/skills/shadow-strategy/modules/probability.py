#!/usr/bin/env python3
"""
확률 계산 모듈
구간별 기대확률 및 낙찰확률 계산
"""

import pandas as pd
import numpy as np
from collections import defaultdict


class ProbabilityCalculator:
    """구간별 확률 계산기 (기초대비사정률 기준)"""

    def __init__(self, df, zone_size=0.001, min_rate=99.5, max_rate=100.0):
        """
        Args:
            df: 데이터프레임
            zone_size: 구간 크기 (기본 0.001%)
            min_rate: 기초대비사정률 최소값
            max_rate: 기초대비사정률 최대값
        """
        self.df = df
        self.zone_size = zone_size
        self.min_rate = min_rate
        self.max_rate = max_rate
        self.results = []

    def _get_zone_key(self, rate):
        """투찰률을 구간 키로 변환"""
        if pd.isna(rate) or rate < self.min_rate or rate > self.max_rate:
            return None
        zone_idx = int((rate - self.min_rate) / self.zone_size)
        zone_start = self.min_rate + zone_idx * self.zone_size
        zone_end = zone_start + self.zone_size
        return f"{zone_start:.3f}~{zone_end:.3f}%"

    def calculate(self):
        """확률 계산 실행"""
        rate_col = '기초대비사정률'
        rank_col = '순위'
        announcement_col = '공고번호'

        if rate_col not in self.df.columns:
            raise ValueError(f"컬럼 없음: {rate_col}")

        # 구간별 통계 집계
        zone_stats = defaultdict(lambda: {
            'total_bids': 0,
            'wins': 0,
            'rates': [],
            'announcements': set()
        })

        for _, row in self.df.iterrows():
            rate = row[rate_col]
            zone = self._get_zone_key(rate)

            if zone is None:
                continue

            zone_stats[zone]['total_bids'] += 1
            zone_stats[zone]['rates'].append(rate)

            if announcement_col in self.df.columns:
                zone_stats[zone]['announcements'].add(row[announcement_col])

            # 낙찰(1순위) 체크
            if rank_col in self.df.columns:
                rank = row[rank_col]
                if pd.notna(rank) and rank == 1:
                    zone_stats[zone]['wins'] += 1

        # 전체 공고 수
        total_announcements = self.df[announcement_col].nunique() if announcement_col in self.df.columns else 1

        # 결과 생성
        self.results = []

        for zone, stats in sorted(zone_stats.items()):
            total_bids = stats['total_bids']
            wins = stats['wins']

            # 기대확률: 해당 구간에서 낙찰될 확률
            expected_prob = (wins / total_bids * 100) if total_bids > 0 else 0

            # 낙찰확률: 해당 구간에서 1순위가 나올 전체 확률
            win_prob = (wins / total_announcements * 100) if total_announcements > 0 else 0

            # 평균 및 표준편차
            avg_rate = np.mean(stats['rates']) if stats['rates'] else 0
            std_rate = np.std(stats['rates']) if len(stats['rates']) > 1 else 0

            self.results.append({
                'zone': zone,
                'total_bids': total_bids,
                'wins': wins,
                'expected_prob': round(expected_prob, 2),
                'win_prob': round(win_prob, 2),
                'avg_rate': round(avg_rate, 4),
                'std_rate': round(std_rate, 4),
                'unique_announcements': len(stats['announcements'])
            })

        print(f"[Probability] 확률 계산 완료: {len(self.results)}개 구간")
        return self.results

    def get_results(self):
        """계산 결과 반환"""
        return self.results

    def to_dataframe(self):
        """결과를 DataFrame으로 변환"""
        if not self.results:
            return pd.DataFrame()

        return pd.DataFrame([
            {
                '구간': r['zone'],
                '총_투찰수': r['total_bids'],
                '낙찰수': r['wins'],
                '기대확률(%)': r['expected_prob'],
                '낙찰확률(%)': r['win_prob'],
                '평균_투찰률': r['avg_rate'],
                '표준편차': r['std_rate']
            }
            for r in self.results
        ])

    def get_best_zones(self, top_n=5, metric='expected_prob'):
        """최적 구간 추천"""
        sorted_results = sorted(self.results, key=lambda x: x[metric], reverse=True)
        return sorted_results[:top_n]
