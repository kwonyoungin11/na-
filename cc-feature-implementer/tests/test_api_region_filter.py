from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / ".agents" / "skills" / "API" / "scripts" / "run_nara_openapi_pipeline.py"


def _load_pipeline_module():
    spec = spec_from_file_location("run_nara_openapi_pipeline", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"모듈 로드 실패: {SCRIPT}")
    module = module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)  # type: ignore[attr-defined]
    return module


def test_has_allowed_region_only_nationwide_daegu_gyeongbuk():
    mod = _load_pipeline_module()
    allowed = ["전국", "대구", "경북"]

    assert mod.has_allowed_region("전국", allowed)
    assert mod.has_allowed_region("대구광역시", allowed)
    assert mod.has_allowed_region("경상북도", allowed)

    assert not mod.has_allowed_region("전라남도", allowed)
    assert not mod.has_allowed_region("충청북도", allowed)
    assert not mod.has_allowed_region("", allowed)


def test_infer_region_from_org_text_no_default_nationwide():
    mod = _load_pipeline_module()

    assert mod.infer_region_from_org_text("전라남도 전라남도") == "전남"
    assert mod.infer_region_from_org_text("대구광역시 경상북도") == "대구/경북"
    assert mod.infer_region_from_org_text("조달청") == ""


def test_region_bucket_unknown_not_nationwide():
    mod = _load_pipeline_module()

    assert mod.region_bucket("") == "미상"
    assert mod.region_bucket("전라남도") == "기타"
    assert mod.region_bucket("전국 입찰") == "전국"
