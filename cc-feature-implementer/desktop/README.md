# Desktop 앱 구조 초안 (WPF + Native Chart + SQLite)

## 1. 목표
나라장터 입찰/개찰 데이터를 로컬에서 빠르게 탐색할 수 있는 대시보드 데스크톱 앱의 초기 구조를 정의한다.

## 2. 기술 스택
- UI Shell: WPF (.NET)
- 차트/고급 시각화: `NativeBidChartControl` 기반 네이티브 렌더러
- 로컬 데이터 저장소: SQLite
- 패턴: MVVM (View / ViewModel / DataService)

## 3. 디렉터리 구조(초안)
```text
desktop/
  README.md
  NaraCollector.Desktop/
    NaraCollector.Desktop.csproj
    App.xaml
    MainWindow.xaml
    ViewModels/
    Models/
    Services/
    Theme/
```

## 4. 아키텍처
1. **WPF Shell**
   - 필터, KPI 카드, 목록/상세 프레임을 배치한다.
2. **ViewModel Layer**
   - 필터 상태, KPI 집계 결과, 차트 섹션 데이터를 보유한다.
3. **Data Layer(SQLite)**
   - 공고/참여업체/낙찰 정보를 로컬 테이블로 관리한다.
4. **Native Chart Host**
   - ViewModel에서 전달받은 집계 데이터를 WPF 네이티브 렌더러로 표시한다.
5. **실시간 수집 파이프라인**
   - 나라장터 OpenAPI -> 엄격 전처리 -> SQLite/CSV 저장 -> 네이티브 차트 렌더링

## 5. 실행 계획

### Phase 1: Shell 구성
- `NaraCollector.Desktop/MainWindow.xaml`로 화면 영역(필터/KPI/차트/목록/상세) 고정.
- `ViewModels/MainWindowViewModel.cs`와 기본 바인딩 연결.
- 별도 스켈레톤 없이 현재 WPF 본 프로젝트를 기준으로만 유지한다.

### Phase 2: 데이터 연결
- SQLite 파일 생성 및 스키마 정의(`notices`, `participants`, `awards`).
- 필터 조건 기반 조회 SQL 및 KPI 집계 쿼리 작성.

### Phase 3: 차트 통합
- `NativeBidChartControl` 모드(Scatter / Trend / Distribution) 연결
- ViewModel -> 필터 상태 -> 네이티브 렌더 함수 호출 경로 구현
- 기본 렌더 대상:
  - 포지션맵(0.01 / 0.001)
  - 추세 분석
  - 분포 분석

### Phase 4: 성능/사용성 보완
- 비동기 조회 + 취소 토큰.
- 목록 가상화, 로딩 스켈레톤, 오류 메시지 처리.

## 6. 개발 실행 예시(예정)
프로젝트 파일이 추가되면 아래와 같은 표준 흐름으로 실행한다.
```bash
cd desktop/NaraCollector.Desktop
dotnet restore
dotnet build
# WSL/Linux에서는 WPF 실행 불가. Windows PowerShell/CMD에서 실행:
dotnet run

# Windows 배포 산출물 생성
dotnet publish -c Release -r win-x64 --self-contained false
```

## 6.1 현재 생성된 배포 산출물
- publish 디렉터리:
  - `desktop/NaraCollector.Desktop/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`
- 실행 파일:
  - `NaraCollector.Desktop.exe`
- 압축본:
  - `NaraCollector.Desktop-win-x64.zip`

## 8. 파이프라인 연동/저장 동작
- 기본 동작은 **외부 파이프라인(stage1/stage2) 결과 연동**입니다.
  1) 수집/전처리는 앱 밖에서 수행
  2) 앱은 `A:\c\nara-workspace\master\전체_통합_데이터.xlsx` 와 `A:\c\nara-workspace\output\raw\` 를 다시 읽음
  3) `DashboardOutputService`가 앱 전용 캐시를 갱신:
     - SQLite: `A:\c\nara-workspace\app/nara-dashboard.db`
     - CSV: `A:\c\nara-workspace\app/exports/`
     - 차트 출력: 네이티브 렌더링 기반 PNG/내부 화면 표시
- 앱의 `업데이트`는 API 수집이 아니라 **파이프라인 결과 다시 불러오기**입니다.

## 7. 참고
- 디자인 시스템: `../docs/02-design/design-system.md`
- 복수예가 공식/계산 개념: `../docs/나라장터_복수예가_개발가이드.md`
