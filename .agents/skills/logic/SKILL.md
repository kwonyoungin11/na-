---
name: logic
description: "콘솔 전용 전체 파이프라인 실행. API 증분수집→분석입력 생성→군집/충돌 분석→가림전략 분석→시각화를 한 번에 실행"
allowed-tools: Bash, Read, Write
---

# 콘솔 전용 전체 파이프라인

이 스킬은 **/mnt/a/c/console-workspace** 기준으로만 동작합니다.

## 원칙
- C# 비언급 요청에서는 C# 경로/코드/실행을 사용하지 않음
- 분석/수집 산출물은 console-workspace 하위에만 저장
- 수집 기본 규칙(키워드/지역/최소 참여업체/대상업체)은 `/mnt/a/c/.agents/skills/API/rule_profile.json` 정본을 재사용
- `--keywords` 는 정본 규칙을 덮어쓰는 임시 override 용도로만 사용
- 경쟁전략 전용 기본값은 `/mnt/a/c/.agents/skills/shadow-strategy/analysis_profile.json` 에서 로드하고, wrapper/CLI에서 주입하며 Python 코드에 업무 규칙을 복제하지 않음

## 고정 경로
- 마스터: `/mnt/a/c/전체_통합_데이터.xlsx`
- 정본 규칙: `/mnt/a/c/.agents/skills/API/rule_profile.json`
- 분석 프로필: `/mnt/a/c/.agents/skills/shadow-strategy/analysis_profile.json`
- API 출력: `/mnt/a/c/console-workspace/output`
- 수집 로그: `/mnt/a/c/console-workspace/logs/collection_history.jsonl`
- 분석 입력: `/mnt/a/c/console-workspace/analysis/input_{지역}.xlsx`
- 군집 분석: `/mnt/a/c/console-workspace/analysis/crowding_{지역}`
- 충돌 분석: `/mnt/a/c/console-workspace/analysis/collision_{지역}`
- 전략 결과: `/mnt/a/c/console-workspace/analysis/shadow_{지역}` (competition-only 추천 결과)
- 시각화: `/mnt/a/c/console-workspace/output/charts` (`1y/6m/3m/1m` 각각 `0.01`, `0.001`)

## 전체 실행 (직접)
```bash
python3 /mnt/a/c/.agents/skills/logic/scripts/analyze.py --months 6 --region 전국
```

## 전체 실행 (래퍼)
```bash
/mnt/a/c/console-workspace/scripts/run_full_pipeline.sh --months 6 --region 전국 --period 6m
```

## 검증 커맨드
```bash
# 도움말 확인
python3 /mnt/a/c/.agents/skills/logic/scripts/analyze.py --help

# Dry-run 점검(API 생략 + 실행 계획만 출력)
python3 /mnt/a/c/.agents/skills/logic/scripts/analyze.py --skip-api --dry-run --region 경북 --period 3m
```

## 기본 흐름
1. API 증분 동기화
   - `console-workspace/scripts/run_console_api_pipeline.sh`
   - 기본 키워드/지역/대상업체 규칙은 API 정본 rule_profile 에서 로드
2. 분석 입력 생성(지역 필터)
   - `console-workspace/analysis/input_{지역}.xlsx`
3. 군집도·기대확률 분석
   - `cc-feature-implementer/src/analyze_crowding_probability.py`
   - 기대확률(1/n), 붐비는 구간/한산한 구간, band 리프트 산출
4. 충돌앵커·좌측여유 분석
   - `cc-feature-implementer/src/analyze_collision_left_space.py`
   - 우측앵커/좌측여유/좌측우위차이 리프트 산출
5. 경쟁업체 전용 가림전략 분석
   - `shadow-strategy/scripts/analyze.py`
   - 낙찰확률 대신 경쟁밀도/과열도/공백지속성 사용
6. 시각화 생성(0.01/0.001)
   - `bidder-visualization/scripts/visualize.py`
   - `동국문화재연구원`은 shadow 분석에서만 제외하며, 시각화에서는 제외하지 않음
   - 전국/대구/경북 입력 기준으로 `1y`, `6m`, `3m`, `1m` 차트를 각각 생성

## 주요 옵션
- `--start-month`, `--end-month`, `--months`, `--years`
- `--keywords` (정본 규칙 override 시에만 사용)
- `--region 전국|대구|경북|전체`
- `--period 1m|3m|6m|1y|2y`
- `--min-rate`, `--max-rate` (기본 99.5~100.0)
- `--source-file` (이미 수집된 최종 통합 파일을 직접 분석)
- `--analysis-profile` (경쟁전략 기본값 프로필 경로)
- `--my-company` (자사 제외 이름)
- `--skip-api` (수집 생략)
- `--dry-run` (실행 계획만 출력)

## 빈 입력 처리

- 지역 필터 결과가 0건이면 shadow/viz 단계를 호출하지 않습니다.
- 지정 사정률 범위(`--min-rate ~ --max-rate`)에 해당하는 행이 0건이어도 시장분석/shadow/viz 단계를 호출하지 않습니다.
- 기존 시장분석/전략 산출물/차트가 있으면 정리한 뒤 `shadow_{지역}/pipeline_status.json` 에
  - `skipped_empty_input`
  - `skipped_empty_rate_window`
  중 하나의 상태를 남깁니다.
- 이 동작은 재실행 시 stale 산출물이 남아 판단을 흐리지 않도록 하기 위한 기본 안전장치입니다.

## 완료 상태 파일

- 정상 완료 시에도 `shadow_{지역}/pipeline_status.json` 을 생성합니다.
- 포함 내용:
  - 입력 행/공고 수
  - 실제 사용 사정률 범위 행 수
  - 군집 분석 경로 및 핵심 findings
  - 충돌 분석 경로 및 핵심 findings
  - shadow 전략 요약

## 연계 메모

- shadow 단계 상세 출력 구조는 `/mnt/a/c/.agents/skills/shadow-strategy/SKILL.md` 를 따릅니다.
- `console-workspace/scripts/run_console_api_pipeline.sh` 는 API 스킬 래퍼를 그대로 호출하므로 규칙 경로를 별도로 복제하지 않습니다.
