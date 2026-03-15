# nara-workspace

단일 운영 폴더입니다.

- `source-data/` 레거시 원본 엑셀
- `first-stage/` API 1차 수집 notice별 산출
- `master/` 최종 마스터 엑셀
- `output/` raw / clean / charts
- `logs/` 수집 이력
- `analysis/` crowding / collision / shadow 결과
- `reports/` 보고서
- `app/` 앱 SQLite / export / collect_state
- `scripts/` 실행 래퍼

## 실행 예시

```bash
/mnt/a/c/nara-workspace/scripts/run_api_pipeline.sh --months 6
/mnt/a/c/nara-workspace/scripts/run_full_pipeline.sh --months 6 --region 전국 --period 6m
```
