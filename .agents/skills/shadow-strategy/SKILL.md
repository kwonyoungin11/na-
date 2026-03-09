---
name: shadow-strategy
description: "Analyze Korean government bidding data to detect fixed-position bidders and calculate optimal shadow positions. Use when analyzing bidding patterns, finding fixed bidders in 99.500-100.000 range, or calculating strategic bid positions."
allowed-tools: Bash, Read, Write, Glob, Grep
argument-hint: "[--period 1m|3m|6m|1y|2y] [--output json|excel|all] [--data /path/to/input.xlsx] [--profile /path/to/analysis_profile.json]"
---

# Shadow Strategy Analyzer (가림전략 분석기)

나라장터 복수예가입찰 데이터에서 고정 투찰 업체를 탐지하고 최적의 가림 위치를 계산합니다.

## 정본 규칙 / 프로필 원칙

- 상위 수집·전처리 규칙(키워드, 허용 지역, 최소 참여업체 수, 대상 업체 토큰, 은퇴업체 토큰)은 `/mnt/a/c/.agents/skills/API/rule_profile.json` 을 정본으로 재사용합니다.
- 이 스킬은 **경쟁업체 행동 분석 전용**이므로, 상위 규칙을 다시 Python 코드에 복제하지 않습니다. 직접 `--data` 파일을 넘길 때도 동일한 정본 규칙이 이미 반영된 입력을 사용합니다.
- 경쟁 전용 기본값(자사명, 구간폭, 점수 가중치, 기간별 threshold 등)은 `/mnt/a/c/.agents/skills/shadow-strategy/analysis_profile.json` 에서 로드합니다. **업무 규칙을 모듈 내부 상수로 다시 남기지 않습니다.**
- 자사(`--my-company`)는 **최종 추천 source에서만 빼는 것이 아니라**, 원칙적으로 **fixed bidder / density / gap / candidate 판정에 들어가는 경쟁지도 자체에서 제외**해야 합니다.

## 목적

1. **고정업체 탐지**: 99.500% ~ 100.000% 구간에서 고정적으로 투찰하는 업체 식별
2. **투찰 전략 변화 분석**: 2년/1년/6개월/3개월/1개월 단위로 투찰 위치 변화 추적 (사정률 추세가 아닌 업체 전략 변화)
3. **경쟁 밀도/과열도 분석**: 최근·중기 기준으로 어디가 붐비고 신규 유입이 몰리는지 계산
4. **공백 지속성 분석**: 혼잡도 낮고 과열이 적은 공백 구간을 찾음
5. **가림위치 계산**: 고정업체 바로 왼쪽의 덜 들킨 구간 추천

## 현재 운영 원칙(중요)

- 추천은 **경쟁업체-only 구조 분석**으로 만든다.
- 최종 추천은 점수 상위가 아니라 **실제 `candidate` semantics를 만족한 밴드만** 통과시킨다.
- 근거가 얇은 밴드는 버린다.
  - supporting notice 수가 너무 적은 경우
  - 낙찰 notice 수가 없는 경우
- 같은 업체/같은 날짜/근접 사정률은 먼저 collapse 해서 고정점유를 과대평가하지 않는다.
- `대구/경북` 같은 복합 지역 토큰은 지역 필터에서 누락되지 않게 해석한다.
- 저장 JSON은 snapshot이므로, 소비 경로에서는 signature/source/profile 시각을 보고 stale 여부를 판정해야 한다.

## 사용법

### 전체 분석 실행

```bash
python3 /mnt/a/c/.agents/skills/shadow-strategy/scripts/analyze.py \
  --data /mnt/a/c/console-workspace/analysis/input_전국.xlsx \
  --output-dir /mnt/a/c/console-workspace/analysis/shadow_전국 \
  --profile /mnt/a/c/.agents/skills/shadow-strategy/analysis_profile.json \
  --period 6m \
  --my-company 동국문화재연구원 \
  --output all
```

### 특정 기간 분석

```bash
# 최근 3개월만 분석
python3 /mnt/a/c/.agents/skills/shadow-strategy/scripts/analyze.py \
  --data /mnt/a/c/console-workspace/analysis/input_전국.xlsx \
  --period 3m

# 최근 1년 분석
python3 /mnt/a/c/.agents/skills/shadow-strategy/scripts/analyze.py \
  --data /mnt/a/c/console-workspace/analysis/input_전국.xlsx \
  --period 1y
```

### 출력 형식 지정

```bash
# JSON 출력
python3 /mnt/a/c/.agents/skills/shadow-strategy/scripts/analyze.py \
  --data /mnt/a/c/console-workspace/analysis/input_전국.xlsx \
  --output json

# Excel 출력
python3 /mnt/a/c/.agents/skills/shadow-strategy/scripts/analyze.py \
  --data /mnt/a/c/console-workspace/analysis/input_전국.xlsx \
  --output excel
```

### 주요 옵션

- `--data`: 분석 입력 Excel 경로. 지정하지 않으면 스킬 기본 output 기준으로 읽습니다.
- `--output-dir`: 결과 저장 디렉터리. 지정하지 않으면 `.agents/skills/shadow-strategy/output`
- `--period`: `1m|3m|6m|1y|2y`
- `--min-rate`, `--max-rate`: 분석 구간(기본 99.5~100.0)
- `--my-company`: 경쟁업체 분석에서 제외할 자사명
- `--output`: `json|excel|all` (`all` 이 기본)

## 입력 데이터

- 경로(기본 파이프라인): `/mnt/a/c/전체_통합_데이터.xlsx`
- logic 파이프라인 연계 입력: `/mnt/a/c/console-workspace/analysis/input_{지역}.xlsx`
- 필수 컬럼:
  - `업체명`: 정규화된 업체명
  - `기초대비사정률`: 사정률 비율 (99.5~100% 구간 분석)
  - `투찰일시`: 시간대별 추세 분석용
  - `공고번호`: 공고 식별
- 선택 컬럼:
  - `순위`: 과거 호환용 보조 컬럼 (현재 경쟁전략 계산에는 사용하지 않음)

## 콘솔 분리 원칙
- 콘솔 분석은 `/mnt/a/c/console-workspace` 산출물과 함께 사용
- C# 언급 없는 요청에서는 C# 런타임 경로와 혼합하지 않음

## 출력 결과

### 1. fixed_bidders.json (고정업체 목록)

```json
{
  "fixed_bidders": [
    {
      "company": "A문화재연구원",
      "main_zone": "99.630~99.631%",
      "concentration": 85.5,
      "total_bids": 47,
      "zone_bids": 40
    }
  ]
}
```

### 2. trend_report.json (투찰 전략 변화)

```json
{
  "trends": [
    {
      "company": "A문화재연구원",
      "period_2y": 99.72,
      "period_1y": 99.70,
      "period_6m": 99.68,
      "period_3m": 99.65,
      "period_1m": 99.63,
      "direction": "하향조정",
      "velocity": -0.02
    }
  ]
}
```

### 3. competition_profile.json (경쟁 밀도/과열도)

```json
{
  "zones": [
    {
      "zone": "99.50~99.55%",
      "density_score": 32.1,
      "overheat_score": 12.4,
      "newcomer_ratio": 0.08,
      "nearest_fixed_company": "A문화재연구원"
    }
  ]
}
```

### 4. gap_analysis.json (공백 지속성 분석)

```json
{
  "gaps": [
    {
      "zone": "99.55~99.60%",
      "persistence_score": 86.2,
      "anchor_score": 64.8,
      "opportunity_score": 78.1
    }
  ]
}
```

### 5. shadow_strategy.json (최종 추천)

```json
{
  "recommendations": [
    {
      "target_company": "A문화재연구원",
      "fixed_zone": "99.630~99.631%",
      "current_trend": "하향조정",
      "recommended_rate": 99.629,
      "band_min": 99.627,
      "band_max": 99.631,
      "priority": 1,
      "reason": "고정성 높음, 혼잡도 낮음, 신규유입 낮음"
    }
  ]
}
```

### 6. recommendation.md (요약 보고서)

- 상위 10개 추천 위치와 근거를 Markdown으로 저장

### 7. probability_table.json (호환용 자리표시자)

- competition-only 전략에서는 낙찰확률/1위빈도를 사용하지 않음을 명시하는 deprecated 파일

## 분석 알고리즘

### 고정업체 판정 기준

- 분석 구간: 99.500% ~ 100.000%
- 구간 단위: 0.001% (500개 구간)
- 최소 출현, 집중도, 가중치는 코드에 중복 고정하지 말고 정본 규칙/프로필 또는 CLI 기준으로 관리

### 투찰 전략 변화 분석

- 각 기간별 평균/중앙값 투찰률 계산
- 전략 방향: 상향조정/하향조정/유지 (사정률 추세가 아닌 업체의 투찰 위치 변화)
- 최근 8건 중앙값과 표준편차로 현재 위치를 잡음

**주의**: 복수예가입찰에서 사정률은 매번 추첨이므로 "낙찰 많이 나온 자리"는 전략 근거로 쓰지 않음.

### 경쟁 밀도/과열도 분석

- 최근 30일 / 90일 / 180일 구간별 업체수·투찰수 계산
- 최근 30일 신규 진입 업체 비율로 과열도 계산
- 고정업체 인접도와 같이 봐서 공백의 질을 판단

### 가림위치 계산

```
추천위치 = 고정업체 최근 중앙값 - 기본오프셋 - 변동성보정
```

- 플러스: 고정성, 공백 지속성, 앵커(우측 고정업체), 이동 정합성
- 마이너스: 혼잡도, 신규유입(과열), 변동성
- 낙찰확률/1위빈도는 사용하지 않음
- 추천 밴드가 `candidate` 규칙을 통과하지 못하면 **경계(boundary)로 남기고 추천하지 않는다**
- 좌측 경계거리는 현재 분석 window의 `min-rate` 기준으로 계산한다

## 전략 목표

> 경쟁업체가 고정적으로 서는 자리의 왼쪽에서, 아직 붐비지 않은 공백만 찾는다.

## 모듈 구조

```
shadow-strategy/
├── SKILL.md
├── scripts/
│   └── analyze.py          # 메인 스크립트
├── modules/
│   ├── __init__.py
│   ├── data_loader.py      # 데이터 로드
│   ├── fixed_bidder.py     # 고정업체 탐지
│   ├── trend_analyzer.py   # 추세 분석
│   ├── competition_profile.py # 경쟁 밀도/과열도 계산
│   ├── gap_analyzer.py     # 빈 공백 분석
│   └── shadow_calculator.py # 가림위치 계산
└── output/                 # 결과 저장
```

## 참고

- 콘솔 전체 파이프라인에서는 `/mnt/a/c/.agents/skills/logic/SKILL.md` 의 출력 경로 규약을 그대로 따릅니다.
- 상위 수집/전처리 규칙 변경 시에는 이 문서보다 먼저 `/mnt/a/c/.agents/skills/API/rule_profile.json` 및 API 스킬 문서를 갱신합니다.
- 낙찰 빈도/1위 다발 구간은 전략 근거에서 제외
- 분석 대상은 경쟁업체의 실제 투찰 패턴
- 시간대별 밀도와 신규 유입을 같이 봐서 과열 구간을 회피
