# 개발 게이트

훅·MCP·스킬은 사용하지 않는다. 다시 만들지 않는다.

## 루프

가설 → 실패하는 체크 → 최소 수정 → **같은 체크** 재실행. 체크가 실패하면 가설을 바꾸지 않는다.

프로덕션 코드 전에 실패 테스트를 먼저 넣는다. UI는 숫자·토큰·색을 테마/헬퍼로 빼서 테스트한다.

## 그래프

`업데이트` → C++ `nara_award_collect` jsonl → sqlite → 표 → 스냅샷(`g_snapshot` / `CurrentParticipants`) → 전장 맵.

- 최소 노드만 고친다. 표와 맵은 같은 스냅샷이다. 맵이 sqlite를 다시 읽지 않는다.
- 코딩 전 `.agents/MISTAKES.md`. 같은 실수면 항목을 추가한다.

## 지역 (전국 | 경북)

토큰은 `전국`과 `경북`만. `경상북도`/`경상북` → `경북`. UI에 대구를 넣지 않는다. 두 지역을 한 우주에 섞지 않는다.

메인 세그먼트와 전장 맵은 같은 스냅샷을 쓴다. `ReloadTable`/푸터/차트에 `"전국"`을 하드코딩하지 않는다. 지역 콤보만 바꾸고 데이터를 안 자르면 버그다.

## 앱

사용자가 C++를 쓰면 라이브 앱은

`cc-feature-implementer/desktop/nara_collect_cpp/build/Release/nara_desktop.exe`

이다. C# Debug exe를 대신 띄우지 않는다. explorer로 실행하고 셸 Job으로 죽이지 않는다. exe가 잠기면 프로세스를 종료한 뒤 빌드하고 다시 띄운다.

불변: 낙찰만, 6개월, 6개사+, 영향진단 없음, 전장 맵은 동국 참여 공고만, 칩 라벨 2글자·칸 안 클립. 수집은 C++ `nara_award_collect`. 파이썬 collect 금지.

## 쓰지 말 것

- Grok hooks, MCP servers, skills (설치·호출·복구 금지)
- 분명한 수정을 Plan 모드로 미루기
- 완료 주장 전에 검증 명령을 안 돌리기. 명령·pass/fail을 적는다.
