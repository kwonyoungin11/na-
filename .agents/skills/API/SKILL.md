---
name: 나라장터api수집전처리
description: "나라장터 OpenAPI 월단위 수집 + 마스터(전체_통합_데이터.xlsx) 누적 동기화"
---

# 나라장터 OpenAPI 정본 수집 + 전처리 Skill (프로젝트 정본)

## 이 스킬이 하는 일
1. 월 단위(1개월 창 반복)로 OpenAPI 원천후보를 수집
2. **후보 검증층**에서 일반공사/물품/비대상 공고를 하드 필터링
3. 검증 통과 공고만 상세 조회 후 공고별 파일 생성
4. 2차 전처리와 마스터 누적 병합을 실행
5. raw 후보 로그 / 후보 감사로그 / 단계 요약을 남긴다

## 정본 정책 (Single Source of Truth)
- 정책 파일: `/mnt/a/c/.agents/skills/API/rule_profile.json`
- 이 파일이 아래 규칙의 유일한 진실원천이다.
  - 1차 기본 키워드
  - 복구용 키워드(`발굴`, `시굴`)
  - 기술용역/일반공사 제외 규칙
  - 전국/대구/경북 + 허용권역이 섞인 혼합 지역 보존
  - 공고당 참여업체 최소 수(`5`)
  - `문화유산연구원 → 문화재연구원` 업체명 정규화
  - `문화재연구원` 참여 공고만 유지
- `.agents/skills/data-preprocessing` 와 운영 스킬은 정책을 복제하지 않고 이 정본을 따른다.

## 참여업체 수집 기준 (중요)
- 기본 소스: `getOpengResultListInfoOpengCompt` (개찰완료상세)
- 즉, **1순위만이 아니라 공고별 참여업체 전체 행**을 수집/저장
- 상세 조회 실패 시에만 `opengCorpInfo`를 fallback으로 사용
- 따라서 결과 `순위` 컬럼은 1,2,3... 다수 순위를 포함해야 정상

## 기본 수집/전처리 규칙
- raw 후보는 **전부 보존**
- 후보 검증층에서 아래 규칙을 먼저 적용
  - `기술용역/용역` 계열만 허용
  - `공사/물품/구매/제조/설치/보수/정비/철거/조성공사/건설공사` 제외
  - `취소공고`, `비복수예가` 제외
  - `발굴`, `시굴` 단독 키워드는 복구용으로만 사용하고 도메인 문맥이 맞을 때만 통과
- 상세수집 후 2차 전처리에서 아래 규칙을 적용
  - 지역제한: `전국(명시된 전국 입찰), 대구, 경북`만 유지
  - `대구/경북` 같은 혼합 지역은 보존하고, downstream 분석에서 대구/경북 양쪽 slice에 포함 가능하게 둔다
  - 공고당 참여업체 수: `5개 이상`
  - 업체명 정규화: `(재)`, `(재단)`, `재단법인`, `()`, `문화유산연구원` 정리
  - 공고 내 `문화재연구원` 참여 1건 이상(정규화 후 판정)
  - 제외업체 `한국문화재연구원`, `효림` 처리
  - `한국문화재연구원`: 분석/차트/옵션에서 제외
  - `효림`: 데이터 단계에서 삭제

옵션으로 조정 가능:
- `--allowed-regions`
- `--min-participants`
- `--target-company-terms`
- `--include-retired-company` (기본 제외 규칙 해제)

## 경로 기준 (이 프로젝트)
- 스킬 루트: `/mnt/a/c/.agents/skills/API`
- 실행 스크립트:
  - `/mnt/a/c/.agents/skills/API/scripts/run_master_sync_pipeline.py`
  - `/mnt/a/c/.agents/skills/API/scripts/run_nara_openapi_pipeline.py`
- 기본 마스터: `/mnt/a/c/전체_통합_데이터.xlsx`
- 기본 출력: `/mnt/a/c/공공데이터포털api/output`

## 콘솔 분리 운영(권장)
- 콘솔 전용 작업공간: `/mnt/a/c/console-workspace`
- 콘솔 마스터: `/mnt/a/c/전체_통합_데이터.xlsx`
- 콘솔 출력: `/mnt/a/c/console-workspace/output`
- 콘솔 로그: `/mnt/a/c/console-workspace/logs/collection_history.jsonl`

> 원칙: 사용자 요청에 C# 언급이 없으면 C# 런타임 경로와 혼합하지 않는다.

## API 키 우선순위
1. `.agents/skills/API/.api_key.env`
2. `/mnt/a/c/.env`
3. `/mnt/a/c/공공데이터포털api/.env`

## 권장 실행
```bash
cd /mnt/a/c
python3 .agents/skills/API/scripts/run_incremental_pipeline.py \
  --baseline-file /mnt/a/c/전체_통합_데이터.xlsx \
  --first-stage-dir /mnt/a/c/data/공고별_개별데이터_증분 \
  --final-output /mnt/a/c/전체_통합_데이터.xlsx \
  --audit-output /mnt/a/c/2차전처리_판정로그_증분.xlsx \
  --end 202602282359 \
  --clean-first-stage
```

## 호환용 래퍼(기존 운영 유지 시)
```bash
cd /mnt/a/c
python3 .agents/skills/API/scripts/run_master_sync_pipeline.py
```

> `run_master_sync_pipeline.py` 는 기존 운영 호환용 래퍼다. 정본 규칙 검증과 감사로그 확인은 `run_pipeline.py` / `run_incremental_pipeline.py` 기준으로 본다.

## 콘솔 전용 실행(권장)
```bash
/mnt/a/c/console-workspace/scripts/run_console_api_pipeline.sh --months 6
```

## 1차+2차 파이프라인 실행(통합)
```bash
python3 .agents/skills/API/scripts/run_pipeline.py \
  --start 202602010000 \
  --end 202602282359 \
  --first-stage-dir /mnt/a/c/data/공고별_개별데이터_파이프라인 \
  --final-output /mnt/a/c/전체_통합_데이터.xlsx \
  --audit-output /mnt/a/c/2차전처리_판정로그.xlsx \
  --clean-first-stage
```

기본값은 `rule_profile.json` 에서 읽는다. 별도 조정이 필요할 때만 `--keywords`, `--allowed-regions`, `--target-company-terms`, `--rule-profile` 을 넘긴다.

## 증분 파이프라인 실행
```bash
python3 .agents/skills/API/scripts/run_incremental_pipeline.py \
  --baseline-file /mnt/a/c/전체_통합_데이터.xlsx \
  --first-stage-dir /mnt/a/c/data/공고별_개별데이터_증분 \
  --final-output /mnt/a/c/전체_통합_데이터.xlsx \
  --audit-output /mnt/a/c/2차전처리_판정로그_증분.xlsx \
  --end 202602282359 \
  --clean-first-stage
```

## 최신 export 검증
```bash
python3 .agents/skills/API/scripts/validate_latest_export.py \
  --master /mnt/a/c/전체_통합_데이터.xlsx
```

## 기간 지정 실행
```bash
python3 .agents/skills/API/scripts/run_master_sync_pipeline.py --months 6
python3 .agents/skills/API/scripts/run_master_sync_pipeline.py --years 1
python3 .agents/skills/API/scripts/run_master_sync_pipeline.py --start-month 2025-10 --end-month 2026-02
```

## 주요 옵션
- `--keywords`: 공고명 키워드(콤마구분, 기본은 정본 규칙 프로필)
- `--operations`: `thng,cnstwk,servc,frgcpt`
- `--master-path`: 마스터 파일 경로
- `--output-dir`: raw/clean 출력 경로
- `--replace-master`: 기존 마스터를 새 데이터로 교체

## 필수 산출물
- `raw_candidates.jsonl` / `raw_candidates.xlsx`: 원천후보 로그
- `공고_후보_감사로그.xlsx`: 유지/제외/보류 사유별 감사로그
- `공고_파일_인덱스.xlsx`: 상세조회 처리 현황
- `stage_summary.json`: raw 후보수 / 후보통과수 / 제외사유 / 최종 파일수 요약

## 참고 문서
- `/mnt/a/c/조달청_OpenAPI참고자료_나라장터_*.docx`
- 필요 시 `references/` 폴더에 프로젝트별 메모를 추가하여 사용
