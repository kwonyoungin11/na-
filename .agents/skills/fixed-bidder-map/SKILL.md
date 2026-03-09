---
name: fixed-bidder-map
description: "Create a detailed all-company fixed-position map across 12m/6m/3m/1m windows, excluding 동국문화재연구원 by default, with period-wise concentration and deviation analysis."
allowed-tools: Bash, Read, Write
argument-hint: "[--min-bids 5] [--concentration 60]"
---

# Fixed Bidder Map (전체업체 고정자리 상세 지도)

전체업체의 실제 투찰 위치를 기간별로 추출하고, 고정자리/집중도/편차를 전수 분석합니다.

기본 원칙:
- **동국문화재연구원은 기본 제외**
- **상위 몇 개만 보는 것이 아니라 전체업체 전수조사**
- **평균이 아니라 중앙값/최빈구간/편차 중심 분석**
- **새 공고로 마스터 데이터가 갱신되면 콘솔 전체 파이프라인에서 자동 재생성**

## 색상 정책 (하드코딩)

- `bidder_colors.json`은 업체별 **고유 색상 1:1**로 유지
- 색상은 스크립트 내부 `PREMIUM_PALETTE` 하드코딩 목록에서 할당
- `동국문화재연구원`은 고정 대표색 `#C62828`

## 사용법

```bash
# 업체 색상맵 생성/갱신 (bidder_colors.json)
python .agents/skills/fixed-bidder-map/scripts/generate_colors.py

# 기본 고정업체 위치 맵 생성
python .agents/skills/fixed-bidder-map/scripts/map.py

# 상세 전체업체 고정자리 지도 생성 (기본: 동국문화재연구원 제외)
python .agents/skills/fixed-bidder-map/scripts/detailed_map.py

# 추가 제외업체 지정 가능
python .agents/skills/fixed-bidder-map/scripts/detailed_map.py \
  --exclude-company 동국문화재연구원 \
  --exclude-company 한국문화재연구원
```

## 출력

- `bidder_colors.json` - 업체별 고정 색상/ID
- `output/fixed_bidder_map.json` - 고정업체별 기간별 실제 위치
- `output/fixed_bidder_map.xlsx` - Excel 형식
- `output/detailed_fixed_bidder_map.json` - 전체업체 기간별 상세 위치/집중도/편차
- `output/detailed_fixed_bidder_map.xlsx` - 전체업체 기간별 상세 표
- `output/detailed_fixed_bidder_map.md` - 전체업체 상세 리포트

콘솔 전체 파이프라인(`.agents/skills/logic/scripts/analyze.py`)을 돌리면 아래 경로로 자동 갱신됩니다.

- `console-workspace/analysis/fixed_bidder_map_detailed/detailed_fixed_bidder_map.json`
- `console-workspace/analysis/fixed_bidder_map_detailed/detailed_fixed_bidder_map.xlsx`
- `console-workspace/analysis/fixed_bidder_map_detailed/detailed_fixed_bidder_map.md`

## 출력 형식

```json
{
  "업체명": {
    "12m": {
      "median_rate": 99.8552,
      "std_rate": 0.0123,
      "dominant_band_001": "99.86",
      "dominant_concentration_001": 62.5,
      "position_list": [99.8552, 99.8601, ...]
    },
    "6m": { "...": "..." },
    "3m": { "...": "..." },
    "1m": { "...": "..." }
  }
}
```

## 판정 기준

- 분석 구간: 99.5% ~ 100%
- 기간: 12개월 / 6개월 / 3개월 / 1개월
- 기본 제외: 동국문화재연구원
- legacy 고정후보: 0.1 구간 집중도 60% 이상
- detailed 고정후보: 0.01 구간 집중도 50% 이상
- micro 고정후보: 0.001 구간 집중도 40% 이상
- 기간 간 안정성: 12m/6m/3m/1m 중앙값 편차로 비교
