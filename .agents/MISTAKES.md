# 실수 목록 (다시 하지 말 것)

코딩·수집·차트 작업 전에 이 파일을 읽는다. 같은 실수를 반복하면 여기 항목을 어긴 것이다.

## 차트·순위

-1. **표는 6개월인데 전장 맵은 3개월 / 발주처 공란**
   - 잘못: 차트 팝업이 저장소를 다시 읽어 예전 스냅샷을 씀. C++ jsonl에 발주처를 안 넣음.
   - 규칙: 차트 팝업은 본문과 같은 `CurrentParticipants`를 복사한다. 수집 jsonl에 `ordering_agency`를 넣는다.

0. **6개월 설정인데 차트가 3개월만 보임**
   - 잘못: 2~4월 C++ 수집값이 `bidprcrt`(85~88, 예가/기초대비투찰률)인데 전장 맵은 99~101만 그린다. 6~8월만 99.xxxx라 3개월처럼 보인다.
   - 규칙: 차트 X는 기초대비사정률 = 투찰률 / 낙찰하한율 * 100. 86.x를 adjust_rate에 넣지 않는다.

0b. **전장 맵 1위가 축 중앙(100%)에만 몰림 / 실제 1위가 아님**
   - 잘못: OpenAPI `bidprcrt`(예가대비투찰률 86.x) ÷ 낙찰하한율 → 100.014를 `adjust_rate`에 넣고 `chip.rank == 1` 금색을 그대로 그림. 창녕-고암(R26BK01673928-000) 실제 1위는 춘추 99.78004(아이건설·기초예가 공식)이지 100.014가 아니다. `ToChartAdjustRate`는 99–101이면 원값을 유지해서 이 값을 고치지 못한다.
   - 규칙: 전장 맵 1위 좌표는 기초대비사정률(아이건설 99.xxxx / `ComputeBaseAdjustRate(투찰금액, 기초예가, 하한)`). 예가대비÷하한으로 만든 100.00±0.15 군집을 1위 위치로 쓰지 않는다. 금색은 그 좌표의 낙찰 업체에만. 나라장터에 이미 있는 99.xxxx는 덮지 않는다. 아이건설넷은 나라장터에 사정률이 없거나(86.x/96.x/100.00 군집) 업체가 없을 때만 쓴다.

1. **표 순위 ≠ 차트 칩 수**
   - 잘못: DB/엑셀에 남은 원본 `Rank`(62위, 40위)를 그대로 표에 씀. 전장 맵은 99–101 창의 고유 업체만 칩으로 그림.
   - 규칙: 칩 수 = 창 안 고유 업체 수. 동국 순위 = 그 칩들 안에서의 순위. 문구에 `N개사`를 넣어 칩 수와 같게 한다. `ScatterChartRules` / C++ `BuildScatterNotice`만 쓴다.

2. **동국이 안 들어간 공고에 칩**
   - 잘못: `CurrentParticipants`에 스냅샷 전체를 넣어 전장 맵이 전 시장을 그림.
   - 규칙: 차트는 `ApplyChartRules` 이후만. 동국 참여 공고의 공동입찰 업체는 남긴다.

3. **기간 설정을 차트에 안 넣음**
   - 잘못: 좌측 하단은 최근 6개월인데 전장 맵이 3개월(또는 캐시된 전체)만 그림.
   - 규칙: `NativeBidChartControl`은 `StartDate`/`EndDate`를 바인딩하고 `ScatterChartRules.InPeriod`로 자른다. 1개월·3개월·6개월·1년·전체기간 모두 같은 경로.

4. **업체 필터 후 KeepDongguk을 다시 걸어 차트 공백**
   - 잘못: 한울만 남긴 뒤에 동국 게이트를 또 적용해 공고가 사라짐.
   - 규칙: 공고 우주를 먼저 자르고, 그다음 업체로 칩만 줄인다.

## 수집

4. **업데이트 = 마스터 엑셀만 재로딩 / 수집이 3개월만 / 수집을 파이썬으로 함**
   - 잘못: 파이썬 파이프라인이 타임아웃·3개월 데이터만 남림. 수집은 C++이어야 하는데 파이썬을 돌림.
   - 규칙: 나라장터 낙찰 수집 엔트리는 `nara_award_collect` (C++). 조회는 3개월 창으로 나눈다. 파이썬 collect는 쓰지 않는다.

20. **지역을 목록 빈칸이면 전부 전국으로 넣음**
   - 잘못: `rgnLmtBidLocplcJdgmBssNm`이 비면 `전국`. sqlite 185공고 중 183이 전국. 파이썬은 `getBidPblancListInfoPrtcptPsblRgn`으로 참가가능지역을 읽는다.
   - 규칙: 토큰은 `전국`|`경북`만. `경상북도`/`대구,경북` → `경북`. 목록이 비면 참가가능지역 API. 전북 등은 버린다.

19. **나라장터 1위를 예가대비÷하한으로 100.00에 붙임**
   - 잘못: `bidprcrt`÷`sucsfbidLwltRate` 또는 예산(asignBdgtAmt)을 기초예가로 써서 분홍선이 100에 수평.
   - 규칙: 기초대비사정률 = `ComputeBaseAdjustRate(투찰금액, bssamt, 하한)`. 기초예가는 PreparPcDetail `bssamt`만. 86.x를 100으로 바꾸지 않는다.

18. **차트 칩 165건 vs 표 86건**
   - 잘못: 히어로 `전국 · 공고 N건`에 persist(6개사+) 시장 공고 수를 넣고, 표는 동국 99–101만 셈.
   - 규칙: 칩·추세·표는 같은 `g_table` / `BuildAwardNoticeRows` 건수다.

17. **업데이트 후 jsonl을 sqlite에 안 넣음 / 경로를 A:\\c에 고정**
   - 잘못: `nara_award_collect`만 돌리고 `LoadParticipants`로 예전 DB를 다시 읽음. db/jsonl/env가 `A:\c\...` 고정이라 다른 PC에서 빈 화면이 됨.
   - 규칙: 수집 jsonl을 앱 폴더 `nara-dashboard.db`에 공고 단위로 병합한 뒤 표·차트를 다시 읽는다. sqlite는 포터블 누적 파일이다. 파이썬 merge 스크립트에 의존하지 않는다.

5. **나라장터에 없는 낙찰을 아이건설넷이 아닌 추측으로 채움**
   - 잘못: 96.x에 델타를 더해 99로 밀어 넣음. 5개사 스텁으로 79개사 공고를 덮어씀. 차트는 6칩, 옆 공고는 수십 칩.
   - 규칙: 참여업체 전체 행을 유지한다(`participants.jsonl` / 개찰완료상세). OpenAPI `adjustRate` 96.x는 전장 맵 99.xxxx가 아니다. 1위 아이건설넷 99.xxxx 앵커로 스케일한 뒤 칩을 그린다. 스텁 6개사로 덮지 않는다.

6. **영향진단 수집**
   - 잘못: `국가유산영향진단`을 도메인 토큰에 넣음. 금강 공주 공고가 차트에 뜸.
   - 규칙: 영향진단은 수집·저장·차트 금지. `배수영향구간`은 영향진단이 아니다.

7. **5개사 이하 공고를 올림**
   - 규칙: 고유 업체 6개 미만 공고는 저장·표시하지 않는다.

8. **표준 xlsx를 STORE만 읽음**
   - 잘못: ZIP_STORED·inlineStr만 읽어 실제 엑셀(DEFLATE+sharedStrings)이 실패.
   - 규칙: C++ ingest는 DEFLATE와 sharedStrings를 읽는다.

## 실행·협업

9. **앱을 띄운 뒤 셸 Job이 프로세스를 죽임**
   - 규칙: GUI는 background + Wait-Process. 완료 시마다 Debug exe를 다시 실행한다.

14. **전장 맵 업체명이 칩 밖으로 넘쳐 옆 글자와 붙음**
   - 잘못: 글자 클립을 빼고 글자 크기를 칸보다 크게 그림. `화서`+`미리`가 `화사미리…`처럼 보임.
   - 규칙: 칩 라벨은 2글자, 칸 안에 클립. `CompanyNamePresentation.BuildShortLabel`을 쓴다.

13. **ProgressBar에 읽기 전용 LoadProgress를 TwoWay로 묶어 시작 크래시**
   - 잘못: `ProgressBar.Value` 기본이 TwoWay인데 `LoadProgress` setter가 private.
   - 규칙: `Value="{Binding LoadProgress, Mode=OneWay}"`. 디자이너가 Mode를 빼지 않게 계약 테스트로 고정.

12. **겹친 칩은 동국을 마지막에 그렸는데 히트테스트는 앞에서 고름**
    - 잘못: 그릴 때는 `SortOverlappingChips`로 동국이 맨 앞(마지막 페인트)인데, 호버/클릭은 버킷 앞에서 고르면 가려진 업체가 잡힌다.
    - 규칙: 0.1/0.01 같은 칸 히트테스트는 뒤에서 앞으로. 업데이트 후 전장 맵은 표 스냅샷을 복사하고 필터 재조회로 덮지 않는다.

10. **병렬 에이전트가 같은 파일을 덮어 필터를 지움**
    - 규칙: `CollectorNoticeRules` / `rule_profile.json`은 한 작성자만 수정. 끝난 에이전트 결과를 머지하기 전에 현재 파일을 다시 읽는다.

11. **아이건설넷 비밀번호를 소스에 넣음**
    - 규칙: `nara-workspace/app/igunsul.local.env`만. 깃 추적 금지 (`**/igunsul.local.env`).

16. **전장 맵 드래그 후 칩에 업체명이 없음**
   - 잘못: 팬 중에 라벨을 생략한 프레임을 정적 비트맵으로 캐시하고, 호버는 그 비트맵만 BitBlt함. ClearType은 메모리 DC에 글자가 안 남음.
   - 규칙: 드래그 프레임은 캐시하지 않는다. 마우스 업에서 라벨 포함 레이어를 다시 그린다. 칩 글자는 AntiAliasGridFit + 32bpp DIB.

15. **Windows 훅 command에 `${GROK_WORKSPACE_ROOT}`를 넣음**
   - 잘못: `-File "${GROK_WORKSPACE_ROOT}/.grok/hooks/dev-engineering.ps1"` → 변수가 비어 `/.grok/hooks/...`가 되고 PowerShell이 exit 1. 매 프롬프트/툴마다 빨간 글자.
   - 규칙: 훅 command에 `${GROK_WORKSPACE_ROOT}`를 쓰지 않는다. cwd 상대 `.grok\hooks\dev-engineering.ps1` 또는 절대 경로만 `-File`에 넘긴다.

21. **수집 CLI 기본 기간이 2~8월(7개월)인데 표/푸터는 1년**
   - 잘못: `nara_award_collect` argv 기본값이 `2026-02`~`2026-08`. 업데이트 버튼은 12개월을 넘기지만, 인자 없이 돌리면 7개월만 모인다.
   - 규칙: 수집 기본 구간은 `kCollectLookbackMonths`(12). 오늘 기준 같은 달까지. OpenAPI는 3개월 창으로 나눈다. 표·전장 맵도 그 start/end를 쓴다.

22. **한글 사용자 경로에서 sqlite를 ACP `path.string()`으로 열어 빈 표**
   - 잘못: `sqlite3_open`은 UTF-8인데 `PathString`이 `.string()`(ACP)을 넘김. `C:\Users\권을\...`에서 `EnsureParticipantsSchema` 실패, db 없음, 공고 0건.
   - 규칙: sqlite open은 `path.u8string()`/`sqlite3_open16`. 빈 표는 먼저 db/jsonl 존재와 `GetLocalTime` 연도를 나눈다. OS가 2026이면 푸터는 2025-08-28~2026-08-28이다.
