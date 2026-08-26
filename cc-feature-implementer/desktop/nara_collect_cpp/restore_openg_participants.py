"""Restore full openg participant lists and map 96.x API rates onto 99.xxxx chart scale."""
from __future__ import annotations

import json
import sqlite3
from collections import defaultdict
from datetime import datetime
from pathlib import Path

JSONL = Path(r"A:\c\nara-workspace\app\participants.jsonl")
DB = Path(r"A:\c\nara-workspace\app\nara-dashboard.db")
NOTICES = ("R26BK01673928-000", "R26BK01674020-000")
# 창녕-고암 아이건설넷 1위 기초대비사정율. OpenAPI 96.x 스케일 앵커.
RANK1_CHART = {
    "R26BK01673928-000": 99.78004,
}


def to_chart_adjust_rate(raw: float, rank1_raw: float, rank1_chart: float) -> float:
    if 99.0 <= raw <= 101.0:
        return raw
    if abs(raw) <= 10.0:
        return round(100.0 + raw, 5)
    return round(raw + (rank1_chart - rank1_raw), 5)


def load_jsonl() -> dict[str, list[dict]]:
    by: dict[str, list[dict]] = defaultdict(list)
    for line in JSONL.read_text(encoding="utf-8").splitlines():
        if "01673928" not in line and "01674020" not in line:
            continue
        row = json.loads(line)
        num = row.get("bidNtceNo") or ""
        if num in NOTICES:
            by[num].append(row)
    return by


def replace_notice(cur: sqlite3.Cursor, notice: str, rows: list[dict], now: str) -> None:
    rank1 = next(r for r in rows if int(r["rank"]) == 1)
    rank1_raw = float(rank1["adjustRate"])
    rank1_chart = RANK1_CHART.get(notice)
    if rank1_chart is None:
        rank1_chart = to_chart_adjust_rate(rank1_raw, 96.78818, 99.78004)
        if not (99.0 <= rank1_chart <= 101.0):
            rank1_chart = 99.78004
    base = notice.split("-")[0]
    cur.execute(
        "DELETE FROM participants WHERE notice_number = ? OR notice_number = ? OR notice_no = ?",
        (notice, base, base),
    )
    for row in rows:
        raw = float(row["adjustRate"])
        adj = to_chart_adjust_rate(raw, rank1_raw, rank1_chart)
        cur.execute(
            """
            INSERT INTO participants (
                notice_no, notice_number, notice_name, ordering_agency, region, rank,
                company_name, normalized_company_name, open_date, bid_date,
                bid_amount, bid_rate_vs_estimate, bid_rate_vs_base, adjust_rate,
                estimate_price, lower_limit_price, base_amount, price_adjustment_rate,
                lower_rate, lower_diff, biz_no, ceo_name, remark, is_awarded, updated_at
            ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
            """,
            (
                base,
                notice,
                row.get("bidNtceNm") or "",
                row.get("dminsttNm") or "",
                row.get("rgnLmtBidLocplcJdgmBssNm") or "",
                int(row["rank"]),
                row.get("companyName") or "",
                row.get("companyName") or "",
                (row.get("opengDt") or "").replace("/", "-"),
                None,
                None,
                None,
                None,
                adj,
                None,
                None,
                None,
                rank1_chart,
                None,
                None,
                "",
                "",
                "",
                1 if row.get("isAwarded") else 0,
                now,
            ),
        )


def main() -> None:
    by = load_jsonl()
    con = sqlite3.connect(str(DB))
    cur = con.cursor()
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    for notice in NOTICES:
        rows = by[notice]
        if len(rows) < 6:
            raise SystemExit(f"{notice} expected full list, got {len(rows)}")
        replace_notice(cur, notice, rows, now)
        print(f"restored {notice} rows={len(rows)}")
    con.commit()
    con.close()


if __name__ == "__main__":
    main()
