from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "cc-feature-implementer" / "src"
SCRIPT = SRC / "fetch_keyword_bids_api_per_notice.py"


def _load_module():
    sys.path.insert(0, str(SRC))
    spec = spec_from_file_location("fetch_keyword_bids_api_per_notice", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"모듈 로드 실패: {SCRIPT}")
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return module


def test_normalize_company_name_unifies_foundation_tokens() -> None:
    mod = _load_module()

    assert mod.normalize_company_name("(재단) 문화유산연구원 ()") == "문화재연구원"
    assert mod.normalize_company_name("재단법인 문화유산마을") == "문화유산마을 문화재연구원"


def test_validate_candidate_scope_rejects_general_construction_notice() -> None:
    mod = _load_module()
    rules = mod.load_rule_profile()

    notice = {
        "bidNtceNm": "문화재 주변 정비공사",
        "ntceKindNm": "일반공고",
        "srvceDivNm": "공사",
        "pubPrcrmntLrgClsfcNm": "공사",
        "pubPrcrmntMidClsfcNm": "정비공사",
        "pubPrcrmntClsfcNm": "문화재정비",
        "prearngPrceDcsnMthdNm": "복수예가",
    }

    decision = mod.validate_candidate_scope(notice, ["발굴"], rules)

    assert decision.status == "제외_일반공사"
    assert decision.reason in {"서비스분류불일치", "복구키워드오탐", "제목형태불일치"}


def test_validate_candidate_scope_accepts_cultural_heritage_service_notice() -> None:
    mod = _load_module()
    rules = mod.load_rule_profile()

    notice = {
        "bidNtceNm": "매장문화재 표본조사 용역",
        "ntceKindNm": "일반공고",
        "srvceDivNm": "기술용역",
        "pubPrcrmntLrgClsfcNm": "기술용역",
        "pubPrcrmntMidClsfcNm": "학술연구용역",
        "pubPrcrmntClsfcNm": "문화재조사",
        "prearngPrceDcsnMthdNm": "복수예가",
    }

    decision = mod.validate_candidate_scope(notice, ["표본조사"], rules)

    assert decision.status == "유지_후보"


def test_mixed_region_detection_preserves_daegu_gyeongbuk_combo() -> None:
    mod = _load_module()
    rules = mod.load_rule_profile()

    assert mod.is_allowed_region("대구광역시", rules.allowed_regions)
    assert mod.is_allowed_region("대구광역시/경상북도", rules.allowed_regions)
    assert mod.is_mixed_region("대구광역시/경상북도", rules.allowed_regions)
    assert not mod.is_mixed_region("전국/경상북도", rules.allowed_regions)
