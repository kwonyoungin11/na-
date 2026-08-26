"""Fill empty ordering_agency from bid_notice_raw.jsonl dminsttNm."""
from __future__ import annotations

import json
import sqlite3
from pathlib import Path

DB = Path(r"A:\c\nara-workspace\app\nara-dashboard.db")
RAW_ROOTS = [
    Path(r"A:\c\nara-workspace\output\raw"),
    Path(r"A:\c\nara-workspace\app\api-skill-output\raw"),
]


def load_agency() -> dict[str, str]:
    agencies: dict[str, str] = {}
    for root in RAW_ROOTS:
        if not root.exists():
            continue
        for path in root.glob("*/bid_notice_raw.jsonl"):
            for line in path.read_text(encoding="utf-8").splitlines():
                if not line.strip():
                    continue
                row = json.loads(line)
                notice = str(row.get("bidNtceNo") or "").strip()
                agency = str(row.get("dminsttNm") or row.get("ntceInsttNm") or "").strip()
                if notice and agency:
                    agencies[notice] = agency
                    agencies[notice + "-000"] = agency
    return agencies


def main() -> None:
    agencies = load_agency()
    con = sqlite3.connect(str(DB))
    cur = con.cursor()
    rows = cur.execute(
        "SELECT rowid, notice_number, notice_no, ordering_agency FROM participants"
    ).fetchall()
    updated = 0
    for rowid, notice, notice_no, agency in rows:
        if str(agency or "").strip():
            continue
        filled = agencies.get(str(notice) or "") or agencies.get(str(notice_no) or "")
        if not filled:
            continue
        cur.execute("UPDATE participants SET ordering_agency=? WHERE rowid=?", (filled, rowid))
        updated += 1
    con.commit()
    empty = cur.execute(
        "SELECT count(*) FROM participants WHERE ordering_agency IS NULL OR trim(ordering_agency)=''"
    ).fetchone()[0]
    con.close()
    print(f"agency_updated={updated} still_empty={empty}")


if __name__ == "__main__":
    main()
