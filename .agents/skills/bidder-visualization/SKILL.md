---
name: bidder-visualization
description: "Visualize bidder positions by period with fixed company colors. Creates announcement-row heatmap squares across 기초대비사정률 99.5-100.0 range."
allowed-tools: Bash, Read, Write
argument-hint: "[--period 1y|6m|3m|1m|2y] [--precision 0.01|0.001]"
---

# Bidder Visualization (업체 시각화)

기간별 업체 투찰 위치를 공고 단위 히트맵으로 시각화합니다.

## 특징

- 업체명 앞 2글자 표시 (옵션으로 조절 가능)
- 고정 색상 (업체별 고유 하드코딩 팔레트)
- 1위(낙찰자) = 노란색 테두리
- `동국문화재연구원`은 **시각화에서 제외하지 않음**
- 제외 규칙은 `한국문화재연구원`, `효림`만 유지
- X축: 기초대비사정률 99.5% ~ 100.0% (기본 0.00100 단위 버킷)
- Y축: 공고번호 (개찰일시), 공고 1개 = 1행
- 각 투찰점은 정사각형 셀, **한 칸 = 한 눈금**에 정확히 맞춤
- 기본 캔버스를 더 넓게 잡아 X/Y 라벨 겹침을 줄임
- `0.001` 정밀도에서도 자동 저화질 축소 대신 **캔버스 확장 우선**
- 업체명 텍스트는 검정 외곽선으로 가독성 강화
- 기간 필터(`1y`, `6m`, `3m`, `1m`, `2y`) 지원
- 제목에 실제 데이터 날짜 범위 표시

## 사용법

### 단일 기간 생성
```bash
python .agents/skills/bidder-visualization/scripts/visualize.py \
  --input /mnt/a/c/nara-workspace/analysis/input_전국.xlsx \
  --period 3m \
  --precision 0.01 \
  --output /mnt/a/c/nara-workspace/output/charts/전국_3m_시각화_0.01.png
```

### 전체 기간 파이프라인 (1y, 6m, 3m, 1m)
```bash
for period in 1y 6m 3m 1m; do
  for precision in 0.01 0.001; do
    python .agents/skills/bidder-visualization/scripts/visualize.py \
      --input /mnt/a/c/nara-workspace/analysis/input_전국.xlsx \
      --period "$period" \
      --precision "$precision" \
      --output "/mnt/a/c/nara-workspace/output/charts/전국_${period}_시각화_${precision}.png"
  done
done
```

## 기간 옵션

| 옵션 | 설명 |
|------|------|
| 1m | 최근 1개월 (30일) |
| 3m | 최근 3개월 (90일) |
| 6m | 최근 6개월 (180일) |
| 1y | 최근 1년 (365일) |
| 2y | 최근 2년 (730일) |

## 출력

- `nara-workspace/output/charts/전국_1y_시각화_0.01.png`
- `nara-workspace/output/charts/전국_1y_시각화_0.001.png`
- `nara-workspace/output/charts/전국_6m_시각화_0.01.png`
- `nara-workspace/output/charts/전국_6m_시각화_0.001.png`
- `nara-workspace/output/charts/전국_3m_시각화_0.01.png`
- `nara-workspace/output/charts/전국_3m_시각화_0.001.png`
- `nara-workspace/output/charts/전국_1m_시각화_0.01.png`
- `nara-workspace/output/charts/전국_1m_시각화_0.001.png`

## 파이프라인 의존성

1. **data-preprocessing** 스킬 먼저 실행 필요
2. **fixed-bidder-map** 스킬의 `bidder_colors.json` 사용

## 데이터 소스

- 입력: `/mnt/a/c/nara-workspace/master/전체_통합_데이터.xlsx`
- 색상맵: `.agents/skills/fixed-bidder-map/bidder_colors.json`
