---
name: nara-award-analysis-program
description: "동국문화재연구원 낙찰분석프로그램 운영 스킬. 낙찰 전용(입찰 제외), 전국/대구/경북 분리, 증분 수집 체크포인트, 중복·누락 교차검증, 빌드/실행/점검 절차를 표준화할 때 사용."
allowed-tools: Bash, Read, Write
---

# 동국문화재연구원 낙찰분석프로그램 운영 스킬

## 목적
- 현재 제작된 C# 프로그램의 **운영 규칙**을 고정한다.
- 재접속 후에도 동일한 방식으로 수집/전처리/검증/실행을 반복한다.

## 고정 정책 (반드시 준수)
1. 데이터 기준 시드: `/mnt/a/c/nara-workspace/master/전체_통합_데이터.xlsx`
2. 데이터 구분: **낙찰 전용** (입찰 제외)
3. 지역 규칙: `전국`, `대구`, `경북` 분리(혼합 금지)
4. 수집 방식: 수집 완료시각 체크포인트 이후 **증분 수집**
   - 체크포인트: `A:\c\nara-workspace\app\collect_state.json`
   - API 원천 수집 규칙은 `/mnt/a/c/.agents/skills/API/rule_profile.json` 을 따른다.
5. 품질 로그: 수집 후 아래 두 줄 반드시 확인
   - `교차검증: 신규/중복/기존충돌/누락행/누락공고`
   - `전처리검증: 낙찰규칙위반/지역규칙위반`
6. 범위 제외: 계약정보서비스 연동은 현재 단계에서 사용하지 않음

## 실행 절차 (표준)

### 1) 빌드
```bash
dotnet build /mnt/a/c/cc-feature-implementer/desktop/NaraCollector.Desktop/NaraCollector.Desktop.csproj -v minimal
```

### 2) 실행(Windows)
```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "A:\c\run_publish_check9.ps1"
```

### 3) 데이터 수집
- 프로그램에서 `데이터 수집` 버튼 실행
- forceRefresh=true로 API 증분수집 수행

### 4) 상태 검증(권장)
```bash
python /mnt/a/c/.agents/skills/nara-award-analysis-program/scripts/verify_award_pipeline_state.py
```

### 5) API 원천 검증(필요 시)
- API 파이프라인을 다시 돌렸다면 아래 파일도 함께 확인한다.
  - `raw_candidates.xlsx`
  - `공고_후보_감사로그.xlsx`
  - `stage_summary.json`
- 최소 확인 항목
  - raw 후보 수 > 후보 통과 수 인지
  - `제외_일반공사`, `제외_비기술용역`, `제외_비문화재조사용역` 분포가 남는지
  - 최종 마스터에는 `문화재연구원` 참여 공고만 남는지

## 산출물/참조 위치
- 실행로그: `/mnt/a/c/cc-feature-implementer/docs/개발_실행로그.md`
- 재접속 요약: `/mnt/a/c/cc-feature-implementer/docs/reconnect-handover.md`
- OpenAPI 6종 메모: `/mnt/a/c/cc-feature-implementer/docs/나라장터_openapi_6종_적용메모.md`
- 앱 DB(기본): `A:\c\nara-workspace\app\nara-dashboard.db`

## 장애 대응
- 수집 0건 반복: 기간/키워드/체크포인트/네트워크/API키 확인
- 업체 목록 비정상: 필터 초기화 후 `필터 적용`, 필요 시 `데이터 수집` 재실행
- 지역 혼입: 체크박스(전국/대구/경북) 및 로그의 지역규칙위반 확인
