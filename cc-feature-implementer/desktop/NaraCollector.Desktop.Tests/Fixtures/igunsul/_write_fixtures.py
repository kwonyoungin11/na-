"""Write OpenXML xlsx with DEFLATE + sharedStrings (Excel/아이건설넷 style)."""
from __future__ import annotations

import zipfile
from pathlib import Path
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parent


def col_letter(index: int) -> str:
    n = index + 1
    letters = []
    while n:
        n, rem = divmod(n - 1, 26)
        letters.append(chr(ord("A") + rem))
    return "".join(reversed(letters))


def cell_xml(col: int, row: int, value, shared: list[str]) -> str:
    ref = f"{col_letter(col)}{row}"
    if value is None or value == "":
        return ""
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return f'<c r="{ref}"><v>{value}</v></c>'
    text = str(value)
    if text not in shared:
        shared.append(text)
    idx = shared.index(text)
    return f'<c r="{ref}" t="s"><v>{idx}</v></c>'


def sheet_xml(rows: list[list[object]], shared: list[str]) -> str:
    parts = [
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>',
        '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>',
    ]
    for r, row in enumerate(rows, start=1):
        cells = "".join(cell_xml(c, r, value, shared) for c, value in enumerate(row))
        parts.append(f'<row r="{r}">{cells}</row>')
    parts.append("</sheetData></worksheet>")
    return "".join(parts)


def shared_xml(shared: list[str]) -> str:
    items = "".join(f"<si><t xml:space=\"preserve\">{escape(s)}</t></si>" for s in shared)
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
        f"{items}</sst>"
    )


CONTENT_TYPES = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
</Types>
"""

RELS = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
"""

WORKBOOK = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets>
</workbook>
"""

WB_RELS = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
</Relationships>
"""


def write_xlsx(path: Path, rows: list[list[object]]) -> None:
    shared: list[str] = []
    sheet = sheet_xml(rows, shared)
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        zf.writestr("[Content_Types].xml", CONTENT_TYPES)
        zf.writestr("_rels/.rels", RELS)
        zf.writestr("xl/workbook.xml", WORKBOOK)
        zf.writestr("xl/_rels/workbook.xml.rels", WB_RELS)
        zf.writestr("xl/sharedStrings.xml", shared_xml(shared))
        zf.writestr("xl/worksheets/sheet1.xml", sheet)


def overview_rows(
    notice_number: str,
    notice_name: str,
    agency: str,
    region: str,
    open_date: str,
    base_amount: int,
    estimate_price: int,
    lower_limit: int,
    signed_rate: str,
    paren_rate: str,
    bid_rate: str,
) -> list[list[object]]:
    return [
        ["항목", "값"],
        ["공고번호", notice_number],
        ["공고명", notice_name],
        ["발주처", agency],
        ["지역제한", region],
        ["개찰일시", open_date],
        ["기초금액", base_amount],
        ["예정가격", estimate_price],
        ["낙찰하한가", lower_limit],
        ["투찰률", bid_rate],
        ["사정률", f"{signed_rate}% ({paren_rate}%)"],
        ["기초대비사정율", float(paren_rate)],
    ]


PART_HEADERS = [
    "순위",
    "사업자등록번호",
    "업체명",
    "투찰금액",
    "예가대비투찰률(%)",
    "기초대비투찰률(%)",
    "기초대비사정률(%)",
    "투찰일시",
]


def main() -> None:
    write_xlsx(
        ROOT / "changnyeong-goam_overview.xlsx",
        overview_rows(
            "R26BK01673928-000",
            "창녕-고암 국도건설공사 문화재 정밀발굴조사용역",
            "부산지방조달청",
            "전국",
            "2026/08/20 11:00:00",
            143_400_000,
            143_063_425,
            123_385_051,
            "-0.23472",
            "99.76628",
            "86.245%",
        ),
    )
    write_xlsx(
        ROOT / "changnyeong-goam_participants.xlsx",
        [
            PART_HEADERS,
            [1, "8118200255", "재단법인 춘추문화유산연구원", 123_403_300, 86.257, 86.055, -0.21996, "2026/08/20 09:03:00"],
            [2, "4028214439", "재단법인 전북문화유산연구원", 123_404_820, 86.258, 86.056, -0.21873, "2026/08/20 08:48:23"],
            [3, "7868200186", "재단법인경강문화유산연구원", 123_407_900, 86.260, 86.058, -0.21624, "2026/08/19 14:19:01"],
            [4, "1234567890", "한울문화재연구원", 123_412_000, 86.263, 86.061, -0.21000, "2026/08/20 08:10:00"],
            [5, "1234567891", "가람문화재연구원", 123_420_000, 86.270, 86.068, -0.20000, "2026/08/20 08:05:00"],
            [62, "1111111111", "동국문화재연구원", 123_000_000, 85.975, 85.774, -0.39465, "2026/08/20 07:50:00"],
        ],
    )
    write_xlsx(
        ROOT / "goseong-tongyeong_overview.xlsx",
        overview_rows(
            "R26BK01674020-000",
            "고성-통영 국도건설공사 시점부 문화재 발굴조사용역",
            "부산지방조달청",
            "전국",
            "2026/08/20 11:00:00",
            300_155_000,
            298_900_000,
            241_000_000,
            "-0.41812",
            "99.58188",
            "82.502%",
        ),
    )
    write_xlsx(
        ROOT / "goseong-tongyeong_participants.xlsx",
        [
            PART_HEADERS,
            [1, "2222222222", "충청문화재연구원", 241_903_700, 82.502, 80.590, -0.35000, "2026/08/20 09:00:00"],
            [2, "3333333333", "삼한문화재연구원", 242_073_000, 82.560, 80.646, -0.34000, "2026/08/20 08:40:00"],
            [3, "3333333334", "호서문화재연구원", 242_176_700, 82.595, 80.681, -0.33000, "2026/08/20 08:30:00"],
            [4, "3333333335", "도원문화재연구원", 242_483_000, 82.700, 80.783, -0.32000, "2026/08/20 08:20:00"],
            [5, "3333333336", "바른문화재연구원", 242_746_300, 82.789, 80.871, -0.31000, "2026/08/20 08:10:00"],
            [40, "1111111111", "동국문화재연구원", 238_000_000, 81.200, 79.292, -0.43883, "2026/08/20 08:00:00"],
        ],
    )
    write_xlsx(
        ROOT / "geumgang-gongju_overview.xlsx",
        overview_rows(
            "R26BK01661807-000",
            "금강 공주권역 배수영향구간 실시설계 국가유산영향진단 용역",
            "기후에너지환경부 금강유역환경청",
            "전국",
            "2026/08/18 11:00:00",
            80_000_000,
            79_856_000,
            69_000_000,
            "-0.18000",
            "99.82000",
            "86.100%",
        ),
    )
    write_xlsx(
        ROOT / "geumgang-gongju_participants.xlsx",
        [
            PART_HEADERS,
            [1, "4444444444", "한울문화재연구원", 69_200_000, 86.650, 86.500, -0.15000, "2026/08/18 09:10:00"],
            [2, "5555555555", "호서문화재연구원", 69_250_000, 86.710, 86.562, -0.14000, "2026/08/18 09:00:00"],
            [3, "5555555556", "삼한문화재연구원", 69_300_000, 86.770, 86.625, -0.13000, "2026/08/18 08:55:00"],
            [4, "5555555557", "가람문화재연구원", 69_350_000, 86.830, 86.687, -0.12000, "2026/08/18 08:50:00"],
            [5, "1111111111", "동국문화재연구원", 68_900_000, 86.280, 86.125, -0.22000, "2026/08/18 08:40:00"],
            [6, "5555555558", "나라문화연구원", 69_400_000, 86.890, 86.750, -0.11000, "2026/08/18 08:35:00"],
        ],
    )
    print("wrote DEFLATE+sharedStrings", ROOT)


if __name__ == "__main__":
    main()
