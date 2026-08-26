"""Convert stored 80-95 bid rates to 99.xxxx using 낙찰하한율 from bid_notice jsonl."""
from __future__ import annotations

import json
import sqlite3
from pathlib import Path

DB = Path(r"A:\c\nara-workspace\app\nara-dashboard.db")
RAW_ROOTS = [
    Path(r"A:\c\nara-workspace\output\raw"),
    Path(r"A:\c\nara-workspace\app\api-skill-output\raw"),
]


def load_lower_rates() -> dict[str, float]:
    rates: dict[str, float] = {}
    for root in RAW_ROOTS:
        if not root.exists():
            continue
        for path in root.glob("*/bid_notice_raw.jsonl"):
            for line in path.read_text(encoding="utf-8").splitlines():
                if not line.strip():
                    continue
                row = json.loads(line)
                notice = str(row.get("bidNtceNo") or "").strip()
                raw = str(row.get("sucsfbidLwltRate") or "").replace("%", "").strip()
                if not notice or not raw:
                    continue
                try:
                    value = float(raw)
                except ValueError:
                    continue
                if 50 < value < 95:
                    rates[notice] = value
                    rates[notice + "-000"] = value
    return rates


def convert(bid_rate: float, lower: float) -> float:
    return round(bid_rate / lower * 100.0, 5)


def main() -> None:
    lowers = load_lower_rates()
    con = sqlite3.connect(str(DB))
    cur = con.cursor()
    rows = cur.execute(
        "SELECT rowid, notice_number, notice_no, adjust_rate FROM participants"
    ).fetchall()
    updated = 0
    skipped = 0
    for rowid, notice, notice_no, adj in rows:
        if adj is None or not (80 <= float(adj) <= 95):
            continue
        lower = lowers.get(str(notice) or "") or lowers.get(str(notice_no) or "")
        if lower is None:
            skipped += 1
            continue
        new_rate = convert(float(adj), lower)
        cur.execute("UPDATE participants SET adjust_rate=? WHERE rowid=?", (new_rate, rowid))
        updated += 1
    con.commit()
    dong = cur.execute(
        """
        SELECT substr(open_date,1,7),
               sum(case when adjust_rate between 99 and 101 then 1 else 0 end),
               count(*)
        FROM participants
        WHERE company_name LIKE '%동국%' OR normalized_company_name LIKE '%동국%'
        GROUP BY 1
        ORDER BY 1
        """
    ).fetchall()
    con.close()
    print(f"updated={updated} skipped_no_lower={skipped}")
    print("dongguk by month in99-101/total", dong)


if __name__ == "__main__":
    main()
