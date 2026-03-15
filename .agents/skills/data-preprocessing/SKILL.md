---
name: data-preprocessing
description: "Preprocess raw Korean government bidding data. This skill should be used when cleaning and structuring raw auction data from Naramarket into analysis-ready format."
allowed-tools: Bash, Read, Write
---

# Data Preprocessing

## Purpose

Transform raw bidding data by merging announcement files, normalizing company names, validating data quality, and separating by agency bid rates.

**기본 지역 필터 정책**: 전체 파이프라인에서는 `입찰참여제한` 기준으로 **전국/경북**이 허용되는 공고만 전처리합니다.

**정책 소유자**: API 1차 수집 결과에 대한 키워드/공고종류/대상업체 정책은 `/mnt/a/c/.agents/skills/API/rule_profile.json` 을 따른다.

**핵심 기능**: 증분 업데이트 (Incremental Update) - 신규 데이터만 처리하고 기존 데이터와 병합

## 콘솔 분리 원칙
- 콘솔 분석 파이프라인에서는 `/mnt/a/c/nara-workspace` 하위 결과를 우선 사용합니다.
- 사용자 요청에 C# 언급이 없으면 C# 런타임 경로를 참조하지 않습니다.

## When to Use

Use this skill when:
- Raw bidding data needs preprocessing
- Data from `<project-root>/nara-workspace/source-data` directory
- Before running any analysis skills
- New data files are added and need to be merged with existing data

## How to Use

### Command Line Usage

```bash
# 증분 업데이트 (기본 모드) - 신규 데이터만 처리
python scripts/preprocess_v2.py

# 전체 재처리 - 기존 데이터 무시하고 처음부터 처리
python scripts/preprocess_v2.py --force

# 지역 범위 지정 (기본: nationwide+gyeongbuk)
python scripts/preprocess_v2.py --region-scope core3
python scripts/preprocess_v2.py --region-scope all

# 단일 공고 처리
python scripts/preprocess_v2.py --single 2023-21331

# 커스텀 디렉토리
python scripts/preprocess_v2.py --source-dir /path/to/data --output-dir /path/to/output
```

### Input/Output

| 구분 | 경로 | 설명 |
|------|------|------|
| 입력 | `nara-workspace/source-data/` | 원본 Excel 파일 (공고번호.xlsx + 참여업체목록.xlsx 쌍) |
| 출력 | `nara-workspace/master/` | 전처리된 데이터 |

### Output Files

- `전체_통합_데이터.xlsx` - 모든 공고 통합 데이터
- `투찰률_XX_XXX%_데이터.xlsx` - 발주처투찰률별 분리 데이터
- `preprocessing_log.txt` - 처리 로그

### Incremental Update Features

1. **중복 방지**: 이미 처리된 공고번호는 자동으로 건너뜀
2. **데이터 병합**: 신규 데이터를 기존 데이터에 추가
3. **중복 제거**: 공고번호 + 업체명 기준 중복 데이터 제거
4. **로그 보존**: 기존 로그에 append 모드로 기록
5. **지역제한 필터**: 기본값으로 전국/경북 입찰 가능 공고만 유지

### Scripts

- `scripts/preprocess_v2.py` - 메인 전처리 스크립트
- `scripts/preprocess_api_stage2.py` - API 1차 수집 결과(공고별 파일)용 2차 전처리

## API 1차 수집 결과용 2차 전처리

아래는 `.agents/skills/API/scripts/collect_first_stage.py` 또는 동등한 방식으로 만든 **공고별 xlsx 폴더**를 2차 전처리하는 명령입니다.

```bash
python scripts/preprocess_api_stage2.py \
  --first-stage-dir /mnt/a/c/nara-workspace/first-stage/6개월 \
  --final-output /mnt/a/c/nara-workspace/master/전체_통합_데이터.xlsx \
  --allowed-regions 전국,경북 \
  --min-participants 5 \
  --target-company-terms 문화재연구원
```

### 2차 전처리 규칙

1. 업체명 정규화: `문화유산연구원` → `문화재연구원`, `(재)`, `(재단)`, `재단법인`, `()`, `（）` 제거
2. 지역제한 필터: `전국`, `경북` 입찰 가능 공고만 유지
   - `전국`은 지역제한이 없는 전국 입찰로 해석
   - `대구/경북` 같은 혼합 지역은 경북 참여 가능 공고로 보존
3. 공고당 업체 수: 5개 이상 유지
4. 공고 내 참여업체 중 `문화재연구원` 포함이 1건 이상인 공고만 유지 (정규화 후 판정)
5. 제외 사유는 `2차전처리_판정로그.xlsx`에 기록
6. 제외업체 처리:
   - `한국문화재연구원` 제외
   - `효림` 데이터 삭제

> 주의: API 1차 수집 단계의 raw 후보 보존, 기술용역/일반공사 구분, recovery 키워드 운영은 API 스킬 정본 규칙을 그대로 따른다.

### Integration

This skill integrates with the bidding analysis ecosystem:
1. **data-preprocessing** (현재) → 원본 데이터 전처리
2. **logic** → 최적 투찰률 분석
3. **report** → 분석 리포트 생성
