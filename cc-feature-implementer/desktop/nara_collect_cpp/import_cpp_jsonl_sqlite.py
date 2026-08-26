"""Merge C++ award jsonl into sqlite without dropping other notices."""
from __future__ import annotations

import json
import sqlite3
from datetime import datetime
from pathlib import Path

DB = Path(r"A:\c\nara-workspace\app\nara-dashboard.db")


def main() -> None:
    files = [
        Path(r"A:\c\nara-workspace\app\cpp_award_collect.jsonl"),
        Path(r"A:\c\nara-workspace\app\cpp_award_march.jsonl"),
        Path(r"A:\c\nara-workspace\app\cpp_award_apr_aug.jsonl"),
    ]
    rows = []
    for path in files:
        if not path.exists():
            continue
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                rows.append(json.loads(line))
    if not rows:
        raise SystemExit("no jsonl rows")

    by_notice: dict[str, list[dict]] = {}
    for row in rows:
        by_notice.setdefault(row["notice_number"], []).append(row)

    con = sqlite3.connect(str(DB))
    cur = con.cursor()
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    for notice, items in by_notice.items():
        base = notice.split("-")[0]
        cur.execute(
            "DELETE FROM participants WHERE notice_number=? OR notice_number=? OR notice_no=?",
            (notice, base, base),
        )
        for row in items:
            if "영향진단" in str(row.get("notice_name", "")):
                continue
            rank = int(row.get("rank") or 0)
            adj = row.get("adjust_rate")
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
                    row.get("notice_name") or "",
                    row.get("ordering_agency") or "",
                    row.get("region") or "전국",
                    rank,
                    row.get("company_name") or "",
                    row.get("company_name") or "",
                    (row.get("open_date") or "").replace("/", "-"),
                    None,
                    None,
                    None,
                    None,
                    adj,
                    None,
                    None,
                    None,
                    None,
                    None,
                    None,
                    "",
                    "",
                    "",
                    1 if rank == 1 else 0,
                    now,
                ),
            )
    con.commit()
    span = cur.execute("select min(open_date), max(open_date), count(distinct notice_number) from participants").fetchone()
    dong = cur.execute(
        """
        select min(open_date), max(open_date), count(distinct notice_number)
        from participants
        where company_name like '%동국%' or normalized_company_name like '%동국%'
        """
    ).fetchone()
    con.close()
    print("sqlite span", span, "dongguk", dong)


if __name__ == "__main__":
    main()
