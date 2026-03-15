# reconnect handover

- saved_at: 2026-03-08 14:37:00 KST
- feature: desktop-first-screen-collector-feed
- status: verified_after_force_refresh

## current intent
- 좌측: 오늘 날짜 이후 입찰해야 할 공고만 표시
- 우측: 최근 3개월 낙찰공고 누적 유지
- 업데이트 버튼:
  - collector_notices 비어 있으면 최근 3개월 백필 수집
  - 이후 체크포인트 기준 증분 수집

## current implementation
- API raw source
  - `A:\c\nara-workspace\app/api-skill-output/raw/YYYY-MM/bid_notice_raw.jsonl`
  - `A:\c\nara-workspace\app/api-skill-output/raw/YYYY-MM/openg_result_raw.jsonl`
- SQLite storage
  - `A:\c\nara-workspace\app/nara-dashboard.db`
  - tables:
    - `participants`
    - `collector_notices`
- left panel logic
  - bid notice feed from raw JSONL
  - `bidClseDt` 우선으로 입찰마감 계산
  - 오늘 이후 공고만 유지
- right panel logic
  - award notice feed from raw JSONL
  - `opengDt` 기준
  - 최근 3개월만 유지

## changed files
- `cc-feature-implementer/desktop/NaraCollector.Desktop/Services/NaraApiDashboardRepository.cs`
- `cc-feature-implementer/desktop/NaraCollector.Desktop/Services/SqliteDashboardRepository.cs`
- `cc-feature-implementer/desktop/NaraCollector.Desktop/ViewModels/MainWindowViewModel.cs`
- `cc-feature-implementer/desktop/NaraCollector.Desktop/MainWindow.xaml`
- `cc-feature-implementer/desktop/NaraCollector.Desktop/Models/DashboardModels.cs`
- `cc-feature-implementer/desktop/NaraCollector.Desktop.Tests/NaraApiDashboardRepositoryTests.cs`

## validation
- `dotnet test cc-feature-implementer/desktop/NaraCollector.Desktop.Tests/NaraCollector.Desktop.Tests.csproj -v minimal`
  - 32 passed
- `dotnet build cc-feature-implementer/desktop/NaraCollector.Desktop/NaraCollector.Desktop.csproj -v minimal`
  - succeeded
- DB check
  - `collector_notices` table exists
  - current rows: total 123 / bid 18 / award 105
- live verification
  - forceRefresh 증분 실행 후 `참여업체 0건 · 공고 123건` 확인
  - 좌측 첫 공고: `2026-03-09 10:00 | R26BK01353202 | 2026년 신라유적 발굴조사 관련 중장비 임차`
  - 우측 첫 공고: `2026-03-06 15:00 | R26BK01373647 | 와룡면 기초생활거점조성사업 문화재 시굴조사 용역`
- app run
  - desktop app launch confirmed

## remaining notes
- WSL `dotnet run` 검증 시 앱 데이터 경로는 Linux 사용자 디렉터리(`/mnt/a/c/nara-workspace/app`)를 사용할 수 있음
- 실제 Windows 데스크톱 앱은 `A:\c\nara-workspace\app` 경로를 사용함
- Windows DB에서도 `collector_notices` 123건 적재 확인 완료

## next recommended action
1. Windows 앱에서 업데이트 버튼 클릭 후 좌측/우측 표 시각 확인
2. 필요 시 컬럼 폭/정렬/요약 문구만 미세조정
3. 이후 collector_notices 증분 누적 정책 고정


## ui refinement 2026-03-08 14:44 KST
- 첫 화면을 분석문장 중심에서 수집기 대시보드로 정리
- 상단: 입찰/낙찰 건수, 수집상태, 현재범위 카드
- 중단: 좌측 입찰공고 / 우측 낙찰공고 대형 표
- 하단: 동국 밀착 연구원 / 우측 방해 연구원 / 최근 밀착 공고 표
- footer에서 output 경로 제거, 마지막 동기화만 유지
- 진행률 ProgressBar 바인딩을 OneWay로 수정하여 시작 크래시 해소
- 첫 화면은 데이터형 유지, 상단에 차트 화면 진입 버튼을 복구

## collector filter refinement 2026-03-08 15:16 KST
- collector 화면은 `servc` 그룹만 사용하도록 강화
- collector scope에 `required_service_tokens` 적용 (`용역`, `기술용역`)
- collector 전용 제외토큰 추가: `임차`, `보고서`, `제작`, `공급`, `인부`, `측량`, `촬영`, `스캔`, `경제지표`, `모집단` 등
- collector cache 저장 시 기존 DB merge 대신 raw 3개월창을 다시 읽어 전체 재구성하도록 변경
- stale/noise 공고가 DB에 남지 않도록 `collector_notices` 전량 재기록 방식으로 수정
- 강제 새로고침 후 Windows DB 기준: bid 1건 / award 83건 / 총 84건
- 불필요 예시(`임차`,`보고서`,`현장인부`,`경제지표`,`측량`,`스캔`) 0건 확인

## collector scope recalibration from master 2026-03-08 15:55 KST
- 사용자 피드백에 따라 `/mnt/a/c/nara-workspace/master/전체_통합_데이터.xlsx` 기준으로 collector 범위를 다시 점검함
- 마스터 공고 281건을 분석한 결과:
  - `용역` 포함 273건이지만, **용역 미포함 정상 공고도 8건 존재**
  - `공사/건설공사/정비` 문자열이 들어간 **정상 공고 18건 존재**
  - `공급촉진지구` 문자열이 들어간 **정상 공고 1건 존재**
  - `국가유산영향진단`, `유해발굴` 계열도 마스터에 존재
- 따라서 collector first-screen은 마스터 정본 규칙과 동일하게 `용역 필수`, `공사 문자열 제외`를 그대로 쓰면 안 된다고 판단
- 조정 내용:
  - collector 전용 `collector_domain_context_tokens` 도입
  - collector 전용 `collector_required_service_tokens` / `collector_excluded_service_tokens` 를 **빈 배열**로 분리
  - `공급` 제외 제거
  - 대신 실제 잡음으로 확인된 `단가계약`, `중장비`, `방안 연구` 제외 추가
- 검증 결과:
  - 마스터 공고 281건 기준 새 collector 조건은 **281건 모두 통과**
  - Windows forceRefresh 후 `collector_notices`: bid 1건 / award 110건 / 총 111건
  - 오늘 이후 servc 미래 입찰공고는 raw 기준 4건이었고, 그중 실제 관련 공고는 1건뿐이라 bid 1건은 데이터상 정상

## collector date policy split 2026-03-08 16:12 KST
- 사용자 요청에 따라 collector 날짜 정책을 입찰/낙찰로 분리
- 입찰공고(`bid`)
  - 기준: **오늘 포함 최근 15일**
  - 유지 범위: `today - 15 days` 이후의 bid notice
  - 현재(2026-03-08) 기준 보존 시작: **2026-02-21 00:00:00**
- 낙찰공고(`award`)
  - 기준: `/mnt/a/c/nara-workspace/master/전체_통합_데이터.xlsx` 의 마지막 개찰일시 이후부터 누적
  - 현재 마스터 최대 개찰일시: **2026-02-23 11:00:00**
  - 따라서 collector award 시작점: **2026-02-23 11:01:00**
- forceRefresh 검증 결과
  - `collector_notices`: bid **10건** / award **17건** / total **27건**
  - bid earliest: `2026-02-23 16:00:00`
  - award earliest: `2026-02-23 16:00:00`

## bid collector window fix 2026-03-08 16:17 KST
- 사용자 피드백: “입찰공고가 너무 많이 빠진다”
- 원인:
  - bid collector가 최근 15일 범위를 **등록시각(`rgstDt`) 기준**으로 읽고 있었음
  - 그래서 등록일은 더 과거지만 **입찰마감일(`bidClseDt`)이 최근 15일 안에 있는 정상 공고**가 누락됨
- 수정:
  - `LoadBidNoticeRows`가 collector 윈도우를 `bidClseDt -> opengDt -> bidNtceDt` 기준으로 판정하도록 변경
- 재검증:
  - forceRefresh 후 `collector_notices`: bid **22건** / award **17건** / total **39건**
  - bid earliest: `2026-02-23 10:00:00`

## bid collector ±15 days 2026-03-08 16:24 KST
- 사용자 요청: 입찰공고는 **오늘 기준 ±15일**
- 수정:
  - bid retention upper bound 추가
  - bid collector cache/DB/filter 모두 `today-15d <= bidClseDt < today+16d` 로 통일
  - collector raw 재구성용 filter `EndDate` 도 `today+15d` 쪽까지 열어 미래 bidClseDt 공고가 누락되지 않게 수정
- 재검증:
  - forceRefresh 후 `collector_notices`: bid **23건** / award **17건** / total **40건**
  - bid max notice_date: **2026-03-19 14:00:00** (today+15 범위 내 미래 입찰공고 포함 확인)


## 2026-03-08 추가 수정: 입찰공고 누락 복구
- 원인1: `NaraApiDashboardRepository` 기본 수집 키워드가 너무 좁아 `정밀발굴조사` 등 마스터형 공고를 놓칠 수 있었음.
- 원인2: `.agents/skills/API/scripts/run_nara_openapi_pipeline.py` 가 월별 raw JSONL을 매 실행마다 `w`로 덮어써서, 0건 수집 시 `2026-03/bid_notice_raw.jsonl` 이 빈 파일로 바뀌고 기존 3월 입찰공고가 사라졌음.
- 수정:
  - 앱 기본 키워드를 `rule_profile.json` 의 `primary_keywords` 기준으로 로드하도록 변경.
  - 월별 raw JSONL 저장을 overwrite가 아니라 merge-append 방식으로 변경.
  - `run_master_sync_pipeline.py` 기본 키워드도 `rule_profile.json` 기반으로 맞춤.
- 실 API 재수집 검증:
  - 기간 `2026-02-21 00:00 ~ 2026-03-24 23:59`
  - 키워드 `발굴조사,정밀발굴조사,시굴조사,지표조사,표본조사,매장유산,문화재조사,문화재발굴조사,국가유산영향진단,영향진단,유해발굴`
  - 결과: `2026-03/bid_notice_raw.jsonl` 누적 41건 복구.
  - 사용자 지적 공고번호 확인: R26BK01366957, R26BK01366613, R26BK01366528, R26BK01366440, R26BK01364852, R26BK01363668, R26BK01368607 모두 raw에 존재.
- 검증:
  - `python -m pytest cc-feature-implementer/tests -q` → 40 passed
  - `dotnet test ...NaraCollector.Desktop.Tests.csproj -v minimal` → 36 passed
  - `dotnet build ...NaraCollector.Desktop.csproj -o /mnt/a/c/review-build` → succeeded
- 주의:
  - 실행 중인 기존 앱 바이너리는 파일 잠금으로 제자리 빌드가 안 되었음.
  - 최신 적용본은 `/mnt/a/c/review-build/동국문화재연구원_낙찰분석프로그램.dll`
