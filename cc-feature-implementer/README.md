# cc-feature-implementer

나라장터(용역) 개찰 데이터를 실시간으로 수집하고, 계산 컬럼을 포함한 통합 엑셀을 생성합니다.

## 빠른 시작
```bash
python src/nara_bid_excel.py \
  --start 202309220000 \
  --end 202309222359 \
  --output /mnt/a/c/실시간_통합_데이터.xlsx
```

## 옵션
- `--env-file`: 서비스키 파일 경로 (기본: `/mnt/a/c/.env`)
- `--max-bids`: 테스트용 공고 건수 제한

## 테스트
```bash
python -m pytest tests -q
```

세부 개념/공식은 `docs/나라장터_복수예가_개발가이드.md` 참고.
