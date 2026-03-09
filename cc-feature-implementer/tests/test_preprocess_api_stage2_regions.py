from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pandas as pd


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / ".agents" / "skills" / "data-preprocessing" / "scripts" / "preprocess_api_stage2.py"


def test_preprocess_api_stage2_keeps_mixed_daegu_gyeongbuk_notice(tmp_path: Path) -> None:
    first_stage_dir = tmp_path / "first-stage"
    first_stage_dir.mkdir()
    final_output = tmp_path / "final.xlsx"
    audit_output = tmp_path / "audit.xlsx"

    rows = [
        {
            "공고번호": "2026-TEST-001",
            "공고명": "매장문화재 시굴조사 용역",
            "발주처": "문화재청",
            "지역제한": "대구광역시/경상북도",
            "순위": index + 1,
            "업체명": company,
            "개찰일시": "2026-03-01 10:00",
            "투찰일시": "2026-03-01 09:00",
            "투찰금액": 1000 + index,
            "예가대비투찰률": 99.0,
            "기초대비투찰률": 99.0,
            "기초대비사정률": 99.7 + (index * 0.001),
            "예정가격": 100000,
            "낙찰하한가": 90000,
            "기초금액": 100000,
            "사정률": 1.0,
            "발주처투찰률": 87.0,
            "낙찰하한가차이": 100,
        }
        for index, company in enumerate(
            [
                "(재) 동국문화재연구원",
                "가람문화재연구원",
                "한빛문화재연구원",
                "누리문화재연구원",
                "다온문화재연구원",
            ]
        )
    ]

    pd.DataFrame(rows).to_excel(first_stage_dir / "2026-TEST-001.xlsx", index=False)

    result = subprocess.run(
        [
            sys.executable,
            str(SCRIPT),
            "--first-stage-dir",
            str(first_stage_dir),
            "--final-output",
            str(final_output),
            "--audit-output",
            str(audit_output),
            "--allowed-regions",
            "전국,대구,경북",
            "--min-participants",
            "5",
            "--target-company-terms",
            "문화재연구원",
        ],
        check=True,
        capture_output=True,
        text=True,
    )

    assert result.returncode == 0
    assert final_output.exists()
    assert audit_output.exists()

    final_df = pd.read_excel(final_output)
    audit_df = pd.read_excel(audit_output)

    assert len(final_df) == 5
    assert final_df["공고번호"].nunique() == 1
    assert audit_df.loc[0, "처리상태"] == "유지"
