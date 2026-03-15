---
name: optimal-position
description: "Precision analysis for optimal bid position in Korean government multiple-reserve-price bidding. Analyzes every 0.001% interval in 99.5-100% range to find the highest probability position."
allowed-tools: Bash, Read, Write
argument-hint: "[--top N]"
---

# Optimal Position Analyzer (최적 투찰 위치 분석기)

99.5~100% 구간을 0.001% 단위로 정밀 분석하여 최적의 투찰 위치를 추천합니다.

## 핵심 원리

```
낙찰 조건: 예정가격 바로 아래 & 낙찰하한가 이상
```

최적 위치 = 다음 조건을 모두 만족:
1. **바로 위에 고정업체** → 가림막 효과 (해당 사정률에서 고정업체 이김)
2. **바로 아래 빈 공간** → 안전마진 (넓은 사정률 범위에서 1위 가능)
3. **주변 경쟁 적음** → 단독 낙찰 확률 높음
4. **역사적 낙찰 실적** → 검증된 위치

## 분석 단위

- **구간**: 99.500% ~ 100.000%
- **단위**: 0.001% (500개 구간)
- **정밀도**: 소수점 3자리

## 사용법

### 기본 실행 (1개월 데이터)

```bash
python .agents/skills/optimal-position/scripts/analyze.py
```

### 옵션

```bash
# 3개월 데이터로 분석
python .agents/skills/optimal-position/scripts/analyze.py --period 3m

# Top 10 추천
python .agents/skills/optimal-position/scripts/analyze.py --top 10
```

## 분석 알고리즘

### 1. 가림막 점수 (Shadow Score)

```python
# 바로 위(+0.001%)에 고정업체 있으면 높은 점수
if 고정업체 at (position + 0.001):
    shadow_score = 고정업체_집중도 × 투찰횟수
```

### 2. 안전마진 점수 (Margin Score)

```python
# 바로 아래에 빈 공간이 넓을수록 높은 점수
left_gap = position - 가장_가까운_왼쪽_업체
margin_score = left_gap × 1000  # 0.1% = 100점
```

### 3. 경쟁밀도 점수 (Competition Score)

```python
# 주변 ±0.01% 범위 내 업체 수
nearby_bidders = count(position - 0.01 ~ position + 0.01)
competition_score = 100 - (nearby_bidders × 5)
```

### 4. 역사적 낙찰 점수 (Win Score)

```python
# 해당 위치에서 실제 1위가 나온 횟수
win_score = 해당위치_1위횟수 × 10
```

### 종합 점수

```python
total_score = (shadow × 0.35) + (margin × 0.30) + (competition × 0.20) + (win × 0.15)
```

## 출력

### 1. 추천 리포트 (recommendation.md)

```markdown
=== 최적 투찰 위치 추천 ===
분석일: 2026-01-25
기간: 1개월 (2025-12-26 ~ 2026-01-25)
분석 건수: 520건

[1순위] 99.629%
├ 가림막: 동국문화재연구원 (99.630%, 집중도 85%)
├ 안전마진: 0.129% (129칸 빈 공간)
├ 경쟁밀도: 낮음 (주변 3개 업체)
├ 역사적 낙찰: 5회
└ 종합점수: 87.5점

[2순위] 99.747%
...
```

### 2. 시각화 (optimal_position_chart.png)

- 1개월 그래프에 **빨간 화살표**로 추천 위치 표시
- 기초대비사정률 표시
- 고정업체 위치 표시

## 입력 데이터

- **경로(기본)**: `/mnt/a/c/nara-workspace/master/전체_통합_데이터.xlsx`
- **필수 컬럼**: 업체명, 기초대비사정률, 투찰일시, 순위

## 콘솔 분리 원칙
- C# 언급 없는 요청에서는 C# 런타임 경로를 사용하지 않음
- 콘솔 분석 산출물은 `nara-workspace/analysis/optimal-position`에 저장

## 파이프라인 의존성

1. **data-preprocessing** → 전처리된 데이터
2. **fixed-bidder-map** → 고정업체 색상
3. **shadow-strategy** → 고정업체 탐지 결과

## 주의사항

- 0.001% 단위 정밀 분석 (대충 보면 안 됨)
- 모든 데이터 하나하나 분석
- 확률은 보장이 아닌 참고용
