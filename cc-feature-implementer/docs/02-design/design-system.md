# NaraCollector Desktop Design System

## 목표
- Windows 11 / Fluent 2 감성의 **조용한 표면 + 강한 정보 위계 + 절제된 포인트 컬러** 적용
- WPF 화면을 **토큰 기반**으로 관리해 하드코딩 색/간격을 줄임
- 동일한 카드/버튼/칩/테이블 언어를 메인창과 분석창에 공통 적용

## 디자인 원칙
1. **Layered surfaces**
   - 배경은 차분한 쿨 그레이-블루 그라디언트
   - 정보는 흰색/준흰색 카드 위에 올림
2. **Accent discipline**
   - 파란색은 CTA, 선택 상태, 핵심 메트릭에만 사용
3. **Rounded + calm**
   - 기본 카드 반경 22
   - 버튼 반경 14, 필터 칩은 pill
4. **Readable data-first UI**
   - 본문보다 숫자/메트릭 위계를 높임
   - 데이터 표, 로그, 그래프 패널 간 시각적 밀도 차이를 둠

## 토큰
파일: `desktop/NaraCollector.Desktop/Theme/DesignTokens.xaml`

### Color
- `Bg0/Bg1/Bg2`: 앱 배경 / 기본 표면 / 보조 표면
- `Accent / AccentStrong / AccentSubtle`
- `StrokeSubtle / StrokeStrong`
- `TextPrimary / TextMuted / TextSubtle`

### Typography
- Heading: `Segoe UI Variable Display`
- Body: `Segoe UI Variable Text`
- Number: `JetBrains Mono`

### Radius
- `RadiusSm`: 10
- `RadiusMd`: 16
- `RadiusLg`: 22
- `RadiusPill`: 999

## 컴포넌트 계층

### Atoms
- `PrimaryButtonStyle`
- `SecondaryButtonStyle`
- `GhostButtonStyle`
- `DangerButtonStyle`
- `FilterChipCheckBoxStyle`
- `ListCheckBoxStyle`

### Molecules
- `InlineSummaryBorderStyle`
- `TagChipBorderStyle`
- `FloatingToggleButtonStyle`

### Organisms
- `HeroCardBorderStyle`
- `MetricCardBorderStyle`
- `CardBorderStyle`

## 적용 범위
- `MainWindow.xaml`
  - 상단 명령바 + 좌측 필터 레일 + 중앙 브리핑 보드 + 우측 인사이트 레일
  - KPI 카드
  - 경쟁업체 선택 칩
- `ChartPopupWindow.xaml`
  - 분석 명령 센터 헤더
  - 차트 중심 탭 구조
  - 우측 브리핑/업체 추적/이벤트 레일

## 운영 규칙
- 새 화면은 **직접 색상 hex를 박지 않고** 토큰 리소스를 우선 사용
- 새 버튼은 반드시 4개 변형 중 하나 사용
- 선택 상태는 가능하면 **AccentSubtle + AccentStrong** 조합 사용
- 위험 액션만 `DangerButtonStyle` 사용

## 다음 단계
- 다크 모드 토큰 분리
- KPI 카드/로그 카드 전용 variant 추가
- 차트 패널에도 동일한 surface token 적용

## 참고
- Fluent 2 Design System
- Windows app design guidance
