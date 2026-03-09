# 복수예가 마스터 프레임워크

## 1. 왜 이 스킬이 필요한가
복수예가에서는 사용자가 나중에 보는 `기초대비사정률` 또는 `투찰 위치`가 업체의 순수한 의도를 그대로 드러내지 않는다.

관측값은 아래 4개가 겹친 결과다.

```text
Observed = LatentIntent + SystemDistortion + CrowdCompression + DiscretizationNoise
```

- **LatentIntent**: 업체가 실제로 마음속에서 겨냥한 위치
- **SystemDistortion**: 복수예비가격/예정가격 확정이 뒤에서 만든 변형
- **CrowdCompression**: 참여업체가 많고 계산법이 비슷해서 생긴 압축
- **DiscretizationNoise**: 0.001 등 구간화 때문에 생기는 뭉침

따라서 이 스킬의 전제는 단순하다.

> **관측 위치를 있는 그대로 ‘정답 지도’로 읽지 않는다.**

그리고 실전 판단은 아래 한 문장으로 압축한다.

> **반복 충돌업체의 왼쪽에 서고, 그 왼쪽 공간이 넓은 밴드만 남긴다.**

---

## 2. 분석 단위: 예측이 아니라 제거
이 스킬은 추천을 아래처럼 바꾼다.

### 하지 않는 것
- 정답점 예측
- 과거 낙찰다발 위치 복제
- 장기 누적 평균의 직접 추천

### 하는 것
- 자연스럽게 붐빌 자리 추정
- 자연밀도를 초과하는 과열 군집 탐지
- 자사가 반복 충돌하는 **충돌앵커** 탐지
- 충돌앵커의 왼쪽에서 **좌측여유가 남는 밴드**만 추리기

즉, `Best Point Search` 가 아니라 `Wrong Zone Elimination` 이다.

이때 추천 단위는 아래 구조를 따른다.

```text
CollisionAnchor + LeftOfAnchor + LeftSideSpace - LeftSideRisk
```

---

## 3. 4층 구조

### A. 시스템 층
질문:
- 이 공고는 복수예가 구조상 전략공간이 얼마나 압축되는가?
- 참여업체 수가 많아지면 자연밀도는 어느 정도까지 올라갈 것이 예상되는가?
- 둥근값/중심값으로의 심리적 쏠림이 강한 유형인가?

판단 포인트:
- 참여업체 수
- bin width (0.001/0.005/0.01)
- 공고 유형의 전문성/대중성
- 최근 동일 유형 공고의 군집 형태

출력:
- `compression_level`: low / medium / high
- `natural_crowding_floor`: 예상 최소 혼잡도
- `obvious_band_candidates`: 모두가 떠올리기 쉬운 밴드 목록
- `left_space_requirement`: 오늘 공고에서 요구되는 최소 좌측여유 기준

### B. 전체업체 층
질문:
- 전체 시장에서 자연밀도로 설명 가능한 구간과, 설명되지 않는 초과군집 구간은 어디인가?
- 최근 30/90일 신규유입은 어디에 집중되는가?

핵심 개념:
- **자연밀도**: 업체 수가 많으면 원래 생길 겹침
- **초과군집**: 자연밀도를 넘어선 비정상 집중
- **과열도**: 최근 신규유입/급증으로 인해 충돌 위험이 커진 정도

추천 기준:
- raw count가 아니라 `expected 대비 excess`를 본다.

예시 수치화:
```text
ExpectedDensity(zone) = TotalBidders / TotalBins
ExcessDensity(zone)   = max(0, ActualDensity - ExpectedDensity)
Overheat(zone)        = RecentGrowth * 0.55 + NewcomerRatio * 0.45
```

출력:
- `expected_density_map`
- `excess_density_map`
- `overheat_map`
- `market_consensus_band`
- `left_space_map`

### C. 관계 층 (자사 vs 전체업체)
이 층이 가장 중요하다.

질문:
- 자사는 전체업체 군집의 어느 쪽에 주로 들어가는가?
- 자사는 어떤 업체와 가장 자주 충돌하는가?
- 그 업체를 오늘의 **충돌앵커**로 볼 수 있는가?
- 자사가 그 앵커의 왼쪽에 설 경우, 내 왼쪽에 얼마나 넓은 공간이 남는가?
- 자사 습관이 이미 시장에 노출된 패턴인가?
- 자사의 최근 위치는 전체 합의밴드보다 왼쪽/오른쪽 어디에 치우쳤는가?

핵심 지표:
- `collision_anchor_score`
- `collision_rate_with_market`
- `collision_rate_with_fixed_bidders`
- `left_of_anchor_distance`
- `left_space_width`
- `left_space_calmness`
- `left_space_risk`
- `self_band_obviousness`
- `self_shift_direction`
- `self_entropy` (자사 위치 다양성)

해석 예시:
- 자사 entropy가 낮고 collision이 높으면: **시장에 읽힌 습관 가능성**
- 자사 shift가 최근 군집 중심으로 붙고 있으면: **추격형 패턴 가능성**
- 자사와 특정 고정업체의 거리 분포가 일정하면: **고정충돌 위험**
- 충돌앵커 점수는 높은데 `left_space_width` 가 좁으면: **왼쪽 선점은 맞지만 버틸 공간이 부족한 밴드**
- 앵커 왼쪽이면서 `left_space_calmness` 가 높으면: **복수예가 흔들림에도 상대우위 유지 가능성**

출력:
- `self_market_relation.json` 같은 요약 구조
- 자사 습관 진단 메모
- `collision_anchors`
- `left_space_candidates`

### D. 결정 층
결정은 반드시 3분류로 낸다.

1. **금지구간**
   - 초과군집 높음
   - 과열도 높음
   - 고정업체 점유 강함
   - 자사 충돌률 높음
   - 앵커는 좋아 보여도 내 왼쪽 공간이 거의 없음

2. **경계구간**
   - 데이터는 있으나 일관성이 약함
   - 최근 유입 변동성이 큼
   - 시스템 왜곡으로 신뢰도가 낮음
   - 앵커 왼쪽은 맞지만 좌측여유가 짧거나 불안정함

3. **후보밴드**
   - 특정 충돌앵커의 왼쪽에 위치
   - 초과군집 낮음
   - 과열도 낮음
   - 좌측여유폭이 충분함
   - 너무 obvious하지 않음

중요:
- 최종 출력은 **single point**보다 **narrow band**여야 한다.
- 각 band에는 반드시 불확실성 설명이 붙어야 한다.
- 각 band에는 반드시 **앵커명 / 앵커 대비 왼쪽 거리 / 좌측여유폭**이 붙어야 한다.

---

## 4. 시간 가중치 원칙
장기 데이터는 설명용이고, 결정은 짧은 구간 위주로 한다.

권장 해석:
- 1년: 구조 파악용
- 3~6개월: 최근 시장 습관 파악용
- 1~2개월: 오늘형 군집 추정용

중요:
- 오래된 데이터가 많다고 강한 근거가 되는 것이 아니다.
- 최근성이 없는 군집은 **역사**일 뿐 **오늘 전략**은 아니다.

권장 가중치 예시:
```text
1m   0.45
3m   0.30
6m   0.15
1y   0.10
```

---

## 5. 추천 점수의 방향
이 스킬에서 점수는 플러스보다 마이너스 제거 비중이 더 크다.

```text
DecisionScore =
  + CollisionAnchorStrength
  + LeftOfAnchorAdvantage
  + LeftSpaceWidth
  + LeftSpaceCalmness
  - ExcessDensity
  - Overheat
  - Obviousness
  - LeftSpaceRisk
  - SelfPatternExposure
```

설명:
- `CollisionAnchorStrength`: 반복 충돌업체가 실제 기준점 역할을 하는 정도
- `LeftOfAnchorAdvantage`: 그 앵커보다 약간 왼쪽에서 얻는 상대 우위
- `LeftSpaceWidth`: 추천 밴드의 왼쪽에 남는 공백 폭
- `LeftSpaceCalmness`: 그 왼쪽 영역이 최근에도 조용한 정도
- `Obviousness`: 둥근값/중심값/유명구간에 가까운 정도
- `LeftSpaceRisk`: 왼쪽 바로 아래 또 다른 군집/과열/낭떠러지가 있는 정도
- `SelfPatternExposure`: 자사 패턴이 시장에 읽혔을 가능성

핵심은:
> **좋은 점을 찾는 것보다, 충돌앵커 왼쪽이면서도 왼쪽 공간이 좁은 가짜 후보를 먼저 제거하는 것이 더 중요하다.**

---

## 6. 출력 형식(권장)

```json
{
  "system_view": {
    "compression_level": "high",
    "predictability": "low",
    "market_consensus_band": [99.742, 99.754]
  },
  "market_view": {
    "forbidden_zones": [
      {
        "band": [99.748, 99.751],
        "reason_codes": ["EXCESS_DENSITY", "OVERHEAT", "LEFT_SPACE_TOO_NARROW"]
      }
    ]
  },
  "relation_view": {
    "primary_anchor": "A문화재연구원",
    "left_space_width": 0.006,
    "self_pattern_exposure": 0.72,
    "collision_rate_with_market": 0.66,
    "collision_companies": ["A문화재연구원", "B문화재연구원"]
  },
  "decision_view": {
    "candidate_bands": [
      {
        "band": [99.736, 99.739],
        "anchor": "A문화재연구원",
        "anchor_gap_left": 0.002,
        "left_space_width": 0.008,
        "priority": 1,
        "uncertainty": "medium",
        "why": ["LEFT_OF_COLLISION_ANCHOR", "LEFT_SPACE_WIDE", "LOW_OVERHEAT", "LESS_OBVIOUS"]
      }
    ]
  }
}
```

---

## 7. 이유 코드 체계
권장 reason code:
- `EXCESS_DENSITY`
- `OVERHEAT`
- `OBVIOUS_CENTER`
- `FIXED_BIDDER_OCCUPANCY`
- `LEFT_OF_COLLISION_ANCHOR`
- `LEFT_SPACE_WIDE`
- `LEFT_SPACE_TOO_NARROW`
- `LEFT_SPACE_RISK_HIGH`
- `SELF_COLLISION_HIGH`
- `SELF_PATTERN_EXPOSED`
- `CALM_BAND`
- `LOW_NEWCOMER_RATIO`
- `LESS_OBVIOUS`
- `UNCERTAINTY_HIGH`

---

## 8. 이 스킬이 기존 shadow 전략과 다른 점
기존 경쟁업체 분석은 주로
- 고정업체
- 공백
- 최근 밀도
를 본다.

이 마스터 스킬은 거기에서 한 단계 더 올라간다.

추가되는 질문:
- **전체업체의 자연밀도 대비 지금 군집은 얼마나 비정상적인가?**
- **자사는 전체업체 구조 속에서 어떤 습관을 노출하고 있는가?**
- **자사-시장 관계를 보면 무엇을 버려야 하는가?**

즉, 경쟁업체 분석을 **시장구조 + 자기반성 구조**로 확장한다.
여기에 더해 **충돌앵커 좌측선점 + 좌측여유 유지 구조**를 포함한다.

---

## 9. 실제 운영 문장
분석 결과를 말할 때는 이렇게 표현한다.

좋지 않은 표현:
- "여기가 정답입니다"
- "여기가 낙찰 위치입니다"
- "1년 전 많이 나왔으니 여기입니다"

좋은 표현:
- "이 구간은 전체업체 초과군집과 최근 과열이 높아 제거대상입니다"
- "이 밴드는 A업체의 왼쪽에 있으면서 내 왼쪽 여유가 남아 있어 상대 우위 후보입니다"
- "이 후보밴드는 추천이 아니라 제거 후 남은 상대적 우선순위이며, 좌측여유가 핵심 근거입니다"

---

## 10. 결론
복수예가 마스터 스킬의 본질은 다음 한 문장으로 요약된다.

> **복수예가에서는 미래를 맞추는 것이 아니라, 반복 충돌앵커의 왼쪽을 선점하고 내 왼쪽 공간이 넓은 밴드만 남도록 오답을 지워 나가는 것이 전략이다.**
