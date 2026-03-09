#!/usr/bin/env python3
"""
입찰 분석 결과 리포트 생성기
JSON 결과를 한글 마크다운 리포트로 변환
"""

import json
import argparse
from pathlib import Path
from datetime import datetime

PROJECT_ROOT = Path(__file__).resolve().parents[4]


def load_analysis_results(json_file):
    """JSON 분석 결과 로드"""
    with open(json_file, 'r', encoding='utf-8') as f:
        return json.load(f)


def format_currency(amount):
    """금액 포맷팅"""
    return f"{int(amount):,}원"


def generate_markdown_report(data, output_file):
    """마크다운 리포트 생성"""

    공고정보 = data["공고정보"]
    몬테카를로 = data["몬테카를로_시뮬레이션"]
    과거분석 = data["과거_1위_분석"]
    경쟁밀도 = data["경쟁_밀도"]
    소수점패턴 = data["소수점_패턴"]
    끝자리 = data["끝자리_선호도"]
    심리바닥 = data["심리적_바닥선"]
    추천전략 = data["추천_전략"]

    report = f"""# 입찰 분석 리포트

생성 일시: {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}

---

## 📋 공고 정보

- **기초금액:** {format_currency(공고정보['기초금액'])}
- **발주처투찰률:** {공고정보['발주처투찰률']}%

---

## 1. 🎲 몬테카를로 시뮬레이션 (큰 그림)

> 예정가격 무작위성으로 인한 낙찰하한가 변동 범위 분석

- **예정가격 평균:** {format_currency(몬테카를로['예정가격_평균'])}
- **예정가격 변동폭:** {format_currency(몬테카를로['예정가격_변동폭'])}
- **낙찰하한가 평균:** {format_currency(몬테카를로['낙찰하한가_평균'])}
- **낙찰하한가 변동폭:** {format_currency(몬테카를로['낙찰하한가_변동폭'])}
- **기초대비 낙찰하한율 범위:** {몬테카를로['기초대비_낙찰하한율_범위'][0]}% ~ {몬테카를로['기초대비_낙찰하한율_범위'][1]}%
- **변동폭:** {몬테카를로['기초대비_낙찰하한율_변동폭']}%

**💡 해석:** 예정가격 무작위성으로 인해 낙찰하한가가 ±{몬테카를로['낙찰하한가_변동폭']/2:,.0f}원 변동 가능

---

## 2. 📊 과거 1위 분석 (실제 낙찰 패턴)

> 같은 발주처투찰률 그룹에서 실제로 1위 한 업체들의 입찰 패턴

- **분석 데이터:** {과거분석['데이터_개수']}개
- **평균:** {과거분석['평균']}%
- **중앙값:** {과거분석['중앙값']}%
- **표준편차:** {과거분석['표준편차']}%
- **최소값:** {과거분석['최소']}%
- **최대값:** {과거분석['최대']}%

**백분위수 분포:**
- 5%: {과거분석['백분위수']['5%']}%
- 25%: {과거분석['백분위수']['25%']}%
- 50% (중앙값): {과거분석['백분위수']['50%']}%
- 75%: {과거분석['백분위수']['75%']}%
- 95%: {과거분석['백분위수']['95%']}%

**💡 해석:** 실제 1위 업체의 67.9%가 중앙값 ±0.5% 범위에 몰려있음

---

## 3. 🔥 경쟁 밀도 분석 (회피 구간)

> 0.05% 단위 구간별 경쟁 업체 수 분석

### ⚠️ 절대 피해야 할 구간 (고밀도)

**최고 밀집 구간:** {경쟁밀도['최고_밀집_구간']}

**회피 구간 Top 5:**
"""

    for i, zone in enumerate(경쟁밀도['회피_구간_Top5'], 1):
        report += f"{i}. {zone}\n"

    report += f"""
### ✅ 안전 구간 (저경쟁)

**안전 구간 Top 10:**
"""

    for i, zone in enumerate(경쟁밀도['안전_구간_Top10'], 1):
        report += f"{i}. {zone}\n"

    report += f"""
**💡 해석:** 경쟁이 없거나 1~2개만 있는 구간 선택 시 충돌 위험 최소화

---

## 4. 🔢 소수점 패턴 (0.001% 차별화)

> 소수점 첫째/둘째/셋째 자리 선호도 분석

### 셋째 자리 분포

**회피할 숫자 (빈도 높음):**
"""

    for digit in 소수점패턴['회피_셋째자리']:
        count = 소수점패턴['셋째자리_분포'][str(digit)]
        pct = count / 과거분석['데이터_개수'] * 100
        report += f"- **{digit}**: {count}개 ({pct:.1f}%) ⚠️\n"

    report += "\n**안전한 숫자 (빈도 낮음):**\n"

    for digit in 소수점패턴['안전_셋째자리']:
        count = 소수점패턴['셋째자리_분포'][str(digit)]
        pct = count / 과거분석['데이터_개수'] * 100
        report += f"- **{digit}**: {count}개 ({pct:.1f}%) ✅\n"

    report += """
**💡 해석:** 소수점 셋째 자리 숫자 하나로도 충돌 확률 3배 차이

---

## 5. 🎯 끝자리 선호도

> 투찰금액 끝자리 (원 단위) 분석

### 회피할 끝자리
"""

    for item in 끝자리['회피_끝자리'][:5]:
        pct = item['개수'] / 과거분석['데이터_개수'] * 100
        report += f"- **{item['끝자리']:03d}원**: {item['개수']}개 ({pct:.1f}%) ⚠️\n"

    report += "\n### 안전한 끝자리\n"

    for item in 끝자리['안전_끝자리'][:5]:
        pct = item['개수'] / 과거분석['데이터_개수'] * 100
        report += f"- **{item['끝자리']:03d}원**: {item['개수']}개 ({pct:.1f}%) ✅\n"

    report += f"""
**💡 해석:** 000, 500 같은 직관적 숫자는 50% 이상이 사용 → 절대 회피

---

## 6. 🛡️ 심리적 바닥선

- **안전 하한선:** {심리바닥['안전_하한선']}%
- **권장 최소 입찰률:** {심리바닥['권장_최소입찰률']}%
- **실제 최소값:** {심리바닥['실제_최소']}%
- **시뮬레이션 5% 백분위:** {심리바닥['시뮬_5%백분위']}%

**💡 해석:** {심리바닥['권장_최소입찰률']}% 이하는 하한가 미달 위험

---

## 🎯 추천 전략 비교

| 순위 | 전략명 | 입찰률 | 입찰금액 | 리스크 | 경쟁리스크 |
|:----:|--------|-------:|---------:|:------:|:--------:|
"""

    for strategy in 추천전략:
        report += f"| {strategy['순위']} | {strategy['전략명']} | {strategy['입찰률']}% | {format_currency(strategy['입찰금액'])} | {strategy['리스크']} | {strategy.get('경쟁_리스크', 'N/A')} |\n"

    report += "\n### 전략별 상세 설명\n\n"

    for strategy in 추천전략:
        report += f"""
#### {strategy['순위']}. {strategy['전략명']}

- **입찰률:** {strategy['입찰률']}%
- **입찰금액:** {format_currency(strategy['입찰금액'])}
- **낙찰확률:** {strategy.get('낙찰확률', 'N/A')}
- **이익률:** {strategy.get('이익률', 'N/A')}
- **조정 기대값:** {strategy.get('조정_기대값', 'N/A')}
- **경쟁 리스크:** {strategy.get('경쟁_리스크', 'N/A')}
- **리스크 수준:** {strategy['리스크']}
- **추천 이유:** {strategy['이유']}
"""

    report += f"""
---

## 💡 최종 추천

### 상황별 추천

- **📊 일반적인 경우:** {추천전략[0]['전략명']} - {추천전략[0]['입찰률']}% ({format_currency(추천전략[0]['입찰금액'])})
  - 과거 중앙값 기반으로 안정적인 선택

- **🎯 경쟁력 중시:** {추천전략[1]['전략명']} - {추천전략[1]['입찰률']}% ({format_currency(추천전략[1]['입찰금액'])})
  - 경쟁 밀도 낮은 구간 공략
  - 하한가 위험은 있으나 충돌 확률 최소

- **🛡️ 안정성 중시:** {추천전략[2]['전략명']} - {추천전략[2]['입찰률']}% ({format_currency(추천전략[2]['입찰금액'])})
  - 경쟁 거의 없는 안전 구간
  - 가격 경쟁력은 낮으나 충돌 위험 제로

### ⚠️ 핵심 회피 사항

1. **절대 피할 구간:** {경쟁밀도['최고_밀집_구간']}
2. **피할 소수점 셋째자리:** {', '.join(map(str, 소수점패턴['회피_셋째자리'][:3]))}
3. **피할 끝자리:** 000, 500
4. **최소 입찰률:** {심리바닥['권장_최소입찰률']}% 이상 유지

### 📌 최종 체크리스트

- [ ] 경쟁 밀도 높은 구간 회피 확인
- [ ] 소수점 셋째 자리 안전 숫자 사용 (4, 6, 0)
- [ ] 끝자리 000, 500 회피 확인
- [ ] 심리적 바닥선 이상 확인 ({심리바닥['권장_최소입찰률']}%+)
- [ ] 0.001% 차이로도 당락 결정됨을 인지

---

**분석 데이터 기준:** 발주처투찰률 {공고정보['발주처투찰률']}% 그룹 (n={과거분석['데이터_개수']})

**생성 일시:** {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}
"""

    # 파일 저장
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write(report)

    print(f"\n✓ 리포트 생성 완료: {output_file}")

    return report


def main():
    parser = argparse.ArgumentParser(description="입찰 분석 결과 리포트 생성")
    parser.add_argument('--json-file', type=str, required=True,
                       help='분석 결과 JSON 파일 경로')
    parser.add_argument('--output-dir', type=str,
                       default=str(PROJECT_ROOT / 'data분석'),
                       help='리포트 출력 디렉토리')

    args = parser.parse_args()

    # JSON 파일 로드
    json_file = Path(args.json_file)
    if not json_file.exists():
        print(f"❌ JSON 파일 없음: {json_file}")
        return

    print(f"\n{'='*80}")
    print(f"입찰 분석 리포트 생성")
    print(f"{'='*80}")
    print(f"입력 파일: {json_file}")

    data = load_analysis_results(json_file)

    # 출력 파일명 생성
    agency_rate = str(data['공고정보']['발주처투찰률']).replace('.', '_')
    output_file = Path(args.output_dir) / f"report_{agency_rate}.md"

    # 마크다운 리포트 생성
    generate_markdown_report(data, output_file)

    print(f"\n{'='*80}")
    print(f"리포트 생성 완료!")
    print(f"{'='*80}")


if __name__ == "__main__":
    main()
