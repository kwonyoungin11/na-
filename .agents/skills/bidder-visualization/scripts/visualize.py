#!/usr/bin/env python3
"""C# 네이티브 차트 규칙 기반 업체 사각형 시각화 생성."""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
import sys
from typing import Iterable

import matplotlib
matplotlib.use("Agg")
from matplotlib.patches import Rectangle
import matplotlib.patheffects as pe
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd


ROOT = Path("/mnt/a/c")
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from nara_workspace_paths import master_path

REPO_SRC_DIR = ROOT / "cc-feature-implementer" / "src"
SHADOW_MODULE_DIR = ROOT / ".agents" / "skills" / "shadow-strategy" / "modules"
for extra_path in (REPO_SRC_DIR, SHADOW_MODULE_DIR):
    if str(extra_path) not in sys.path:
        sys.path.insert(0, str(extra_path))

from company_day_cluster import collapse_same_company_day_clusters
from profile_loader import load_analysis_profile

DEFAULT_INPUT = master_path()
DEFAULT_COLOR_MAP = ROOT / ".agents" / "skills" / "fixed-bidder-map" / "bidder_colors.json"
PERIOD_DAYS = {
    "all": 0,
    "1m": 30,
    "3m": 90,
    "6m": 180,
    "1y": 365,
    "2y": 730,
}
RETIRED_COMPANY_TOKENS = ("효림",)


def load_same_day_collapse_config() -> dict[str, object]:
    profile, _ = load_analysis_profile()
    return dict(profile.get("same_day_collapse") or {})


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="업체 사각형 시각화 생성 (0.01 / 0.001)")
    p.add_argument("--input", default=str(DEFAULT_INPUT))
    p.add_argument("--period", choices=list(PERIOD_DAYS.keys()), default="", help="시각화 기간 필터")
    p.add_argument("--precision", choices=["0.01", "0.001"], required=True)
    p.add_argument("--output", required=True)
    p.add_argument("--width", type=float, default=0.0, help="가로 인치(0이면 자동)")
    p.add_argument("--height", type=float, default=0.0, help="세로 인치(0이면 자동)")
    p.add_argument("--cell-px", type=float, default=0.0, help="격자 한 칸 픽셀 크기(0이면 자동)")
    p.add_argument("--font", default="", help="강제 폰트 파일 경로(ttf/ttc)")
    p.add_argument("--min-rate", type=float, default=99.0)
    p.add_argument("--max-rate", type=float, default=101.0)
    p.add_argument("--company", default="", help="특정 업체만 시각화(정규화명 기준 부분일치)")
    return p.parse_args()


def configure_font(font_path: str = "") -> None:
    candidates = []
    if font_path:
        candidates.append(Path(font_path))
    candidates.extend(
        [
            Path("/mnt/c/Windows/Fonts/malgun.ttf"),
            Path("/mnt/c/Windows/Fonts/malgunbd.ttf"),
            Path("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"),
            Path("/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc"),
        ]
    )

    for candidate in candidates:
        if candidate.exists():
            matplotlib.font_manager.fontManager.addfont(str(candidate))
            name = matplotlib.font_manager.FontProperties(fname=str(candidate)).get_name()
            plt.rcParams["font.family"] = name
            plt.rcParams["axes.unicode_minus"] = False
            return

    # 폰트 자동 탐색 실패 시 경고만 두고 진행
    plt.rcParams["axes.unicode_minus"] = False


def normalize_company_name(value: object) -> str:
    text = str(value or "").strip()
    if not text:
        return ""

    patterns = [
        r"\(재\)",
        "재단법인",
        "재단",
        r"\(주\)",
        "주식회사",
        r"\(사\)",
        "사단법인",
        r"\(유\)",
        "유한회사",
        r"\(합\)",
        "합명회사",
        r"\(합자\)",
        "합자회사",
    ]
    for pattern in patterns:
        text = re.sub(pattern, "", text)

    text = re.sub(r"문화유산연구원", "문화재연구원", text)
    text = re.sub(r"문화재보존센터", "문화재연구원", text)
    text = re.sub(r"고고학연구소", "문화재연구원", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text


def load_color_map(path: Path) -> dict[str, str]:
    if not path.exists():
        return {}
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}

    result: dict[str, str] = {}
    for k, v in raw.items():
        key = normalize_company_name(k).replace(" ", "")
        color = None
        if isinstance(v, str):
            color = v
        elif isinstance(v, dict):
            color = v.get("color")
        if isinstance(color, str) and re.fullmatch(r"#[0-9A-Fa-f]{6}", color):
            result[key] = color
    return result


def stable_color(name: str) -> str:
    seed = abs(hash(name)) % 360
    hue = seed / 360.0
    import colorsys

    r, g, b = colorsys.hsv_to_rgb(hue, 0.62, 0.90)
    return "#{:02X}{:02X}{:02X}".format(int(r * 255), int(g * 255), int(b * 255))


def to_float(value: object) -> float | None:
    if pd.isna(value):
        return None
    token = str(value).strip().replace("%", "").replace(",", "")
    if not token:
        return None
    try:
        return float(token)
    except Exception:
        return None


def to_two_chars(name: str) -> str:
    compact = (name or "").replace(" ", "")
    if len(compact) <= 2:
        return compact
    return compact[:2]


def build_company_exclusion_mask(company_series: pd.Series, tokens: Iterable[str]) -> pd.Series:
    mask = pd.Series(False, index=company_series.index)
    normalized = company_series.astype(str)
    for token in tokens:
        if token:
            mask = mask | normalized.str.contains(str(token), case=False, na=False)
    return mask


def filter_visualization_rows(df: pd.DataFrame, *, company: str = "", period: str = "") -> pd.DataFrame:
    working = df.copy()
    working["업체명정규화"] = working["업체명"].map(normalize_company_name)

    # 시각화는 자사(예: 동국문화재연구원)를 기본 제외하지 않는다.
    retired_mask = build_company_exclusion_mask(working["업체명정규화"], RETIRED_COMPANY_TOKENS)
    working = working[~retired_mask].copy()

    if company:
        key = normalize_company_name(company)
        working = working[working["업체명정규화"].astype(str).str.contains(key, case=False, na=False)].copy()

    if period:
        valid_dates = working["개찰dt"].dropna()
        if valid_dates.empty:
            return working.iloc[0:0].copy()
        reference_date = valid_dates.max()
        days = int(PERIOD_DAYS[period])
        if days > 0:
            cutoff = reference_date - pd.Timedelta(days=days)
            working = working[working["개찰dt"] >= cutoff].copy()

    return working


def collapse_visualization_rows(df: pd.DataFrame) -> tuple[pd.DataFrame, dict[str, object]]:
    config = {
        "enabled": True,
        "rate_tolerance": 0.003,
        "min_cluster_size": 2,
        "primary_date_column": "개찰dt",
        "fallback_date_column": "개찰일시",
        **load_same_day_collapse_config(),
    }
    if not bool(config.get("enabled", True)):
        stats = {
            "input_rows": int(len(df)),
            "output_rows": int(len(df)),
            "reduced_rows": 0,
            "collapsed_cluster_count": 0,
        }
        return df.copy(), stats

    return collapse_same_company_day_clusters(
        df,
        company_col="업체명",
        rate_col="기초대비사정률",
        primary_date_col=str(config.get("primary_date_column") or "개찰dt"),
        fallback_date_col=str(config.get("fallback_date_column") or "개찰일시"),
        rate_tolerance=float(config.get("rate_tolerance", 0.003)),
        min_cluster_size=int(config.get("min_cluster_size", 2)),
    )


def resolve_default_cell_px(step: float, n_rows: int) -> float:
    """정밀도별 기본 셀 크기.

    0.001 차트는 0.01보다 열 수가 10배 많으므로, 품질 저하 대신 캔버스를 크게 쓰되
    1년처럼 공고 행이 많은 경우만 메모리 폭주를 피하기 위해 완만하게 줄인다.
    """
    if step >= 0.01:
        return 64.0

    if n_rows <= 15:
        return 64.0
    if n_rows <= 40:
        return 48.0
    if n_rows <= 80:
        return 40.0
    return 32.0


def main() -> None:
    args = parse_args()
    configure_font(args.font)

    input_path = Path(args.input)
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    df = pd.read_excel(input_path)
    if df.empty:
        raise SystemExit(f"입력 데이터가 비어있습니다: {input_path}")

    rename_map = {}
    if "기초대비사정률" not in df.columns and "기초대비사정률(%)" in df.columns:
        rename_map["기초대비사정률(%)"] = "기초대비사정률"
    if "예가대비투찰률" not in df.columns and "예가대비투찰률(%)" in df.columns:
        rename_map["예가대비투찰률(%)"] = "예가대비투찰률"
    if "기초대비투찰률" not in df.columns and "기초대비투찰률(%)" in df.columns:
        rename_map["기초대비투찰률(%)"] = "기초대비투찰률"
    if rename_map:
        df = df.rename(columns=rename_map)

    required = ["공고번호", "업체명", "순위", "기초대비사정률", "개찰일시"]
    for col in required:
        if col not in df.columns:
            df[col] = ""

    df["개찰dt"] = pd.to_datetime(df["개찰일시"], errors="coerce")
    df = filter_visualization_rows(df, company=args.company, period=args.period)
    df["사정률"] = df["기초대비사정률"].map(to_float)
    df = df.dropna(subset=["사정률"]).copy()
    df = df[(df["사정률"] >= args.min_rate) & (df["사정률"] <= args.max_rate)].copy()
    df, collapse_stats = collapse_visualization_rows(df)
    if df.empty:
        period_text = f" / 기간={args.period}" if args.period else ""
        raise SystemExit(f"사정률 범위 내 데이터가 없습니다.{period_text}")
    print(
        "[Visualization] 동일업체·동일일자 근접묶음 축약: "
        f"{collapse_stats.get('input_rows', 0)}건 → {collapse_stats.get('output_rows', 0)}건 "
        f"(감소 {collapse_stats.get('reduced_rows', 0)}건)"
    )

    step = float(args.precision)
    decimals = 2 if step >= 0.01 else 3
    width = args.width if args.width and args.width > 0 else 0.0

    # X축을 격자 칸 인덱스로 고정 (한 칸 = step)
    x_bins = np.arange(args.min_rate, args.max_rate + (step / 2.0), step)
    n_cols = len(x_bins)
    if n_cols <= 0:
        raise SystemExit("격자 생성 실패: 유효한 칸 수가 없습니다.")

    # 최신 공고가 아래쪽(작은 y)으로 오도록 최신순으로 정렬 후 역축 미사용
    notice_order = (
        df.groupby("공고번호", dropna=False)["개찰dt"]
        .max()
        .sort_values(ascending=False)
        .index.tolist()
    )
    n_rows = len(notice_order)
    if n_rows <= 0:
        raise SystemExit("격자 생성 실패: 공고 행이 없습니다.")
    notice_index = {notice: idx for idx, notice in enumerate(notice_order)}
    df["y"] = df["공고번호"].map(notice_index)
    df["col"] = np.rint((df["사정률"] - args.min_rate) / step).astype(int)
    df = df[(df["col"] >= 0) & (df["col"] < n_cols)].copy()
    df["x"] = args.min_rate + (df["col"] * step)

    color_map = load_color_map(DEFAULT_COLOR_MAP)

    def resolve_color(row: pd.Series) -> str:
        key = str(row["업체명정규화"]).replace(" ", "")
        if key in color_map:
            return color_map[key]
        return stable_color(key or str(row["업체명"]))

    df["color"] = df.apply(resolve_color, axis=1)

    # 한 칸 x/y 정사각형 강제
    dpi = 140
    left_margin_px = 620
    right_margin_px = 160
    top_margin_px = 130
    bottom_margin_px = 300

    x_major_interval = 0.01
    x_major_line_step = max(1, int(round(x_major_interval / step)))
    default_cell_px = resolve_default_cell_px(step, n_rows)
    min_major_tick_px = 80.0
    min_cell_px_for_labels = max(14.0, min_major_tick_px / x_major_line_step)
    target_w_px = int(width * dpi) if width > 0 else 0
    target_h_px = int(args.height * dpi) if args.height and args.height > 0 else 0

    if args.cell_px and args.cell_px > 0:
        cell_px = max(2.0, args.cell_px)
    else:
        # 기본값은 절대 줄이지 않는다.
        # width/height 힌트가 있어도 셀 축소가 아니라 캔버스 확장에만 사용한다.
        cell_px = max(default_cell_px, min_cell_px_for_labels)
        width_based_px = (
            max(2.0, (target_w_px - left_margin_px - right_margin_px) / max(1, n_cols))
            if target_w_px > 0
            else None
        )
        height_based_px = (
            max(2.0, (target_h_px - top_margin_px - bottom_margin_px) / max(1, n_rows))
            if target_h_px > 0
            else None
        )
        if width_based_px is not None and height_based_px is not None:
            cell_px = max(default_cell_px, min_cell_px_for_labels, min(width_based_px, height_based_px))
        elif width_based_px is not None:
            cell_px = max(default_cell_px, min_cell_px_for_labels, width_based_px)
        elif height_based_px is not None:
            cell_px = max(default_cell_px, min_cell_px_for_labels, height_based_px)

    grid_w_px = int(max(1, round(n_cols * cell_px)))
    grid_h_px = int(max(1, round(n_rows * cell_px)))
    fig_w_px = left_margin_px + grid_w_px + right_margin_px
    fig_h_px = top_margin_px + grid_h_px + bottom_margin_px

    fig = plt.figure(figsize=(fig_w_px / dpi, fig_h_px / dpi), dpi=dpi)
    ax = fig.add_axes([
        left_margin_px / fig_w_px,
        bottom_margin_px / fig_h_px,
        grid_w_px / fig_w_px,
        grid_h_px / fig_h_px,
    ])
    ax.set_facecolor("white")
    ax.set_aspect("equal", adjustable="box")

    # 격자 (x,y 한칸씩)
    ax.set_xlim(-0.5, n_cols - 0.5)
    ax.set_ylim(-0.5, n_rows - 0.5)

    for gx in range(n_cols + 1):
        is_major = (gx % x_major_line_step) == 0
        ax.axvline(
            gx - 0.5,
            color="#cbd5e1" if is_major else "#e5e7eb",
            linewidth=0.85 if is_major else 0.45,
            zorder=0,
        )
    for gy in range(n_rows + 1):
        ax.axhline(gy - 0.5, color="#eef2f7", linewidth=0.45, zorder=0)

    # 요청 반영: 사각형을 눈금 한 칸에 정확히 맞춤
    cell_pad = 0.0
    rect_w = 1.0 - (cell_pad * 2.0)
    rect_h = 1.0 - (cell_pad * 2.0)

    # 같은 칸 중복은 순위 높은(작은 값) 데이터를 마지막에 그려 위로 보이게
    draw_df = df.copy()
    draw_df["순위숫자"] = pd.to_numeric(draw_df.get("순위"), errors="coerce").fillna(9999)
    draw_df = draw_df.sort_values(["y", "col", "순위숫자"], ascending=[True, True, False])

    for _, row in draw_df.iterrows():
        row_idx = int(row["y"])
        col_idx = int(row["col"])
        left = col_idx - 0.5 + cell_pad
        bottom = row_idx - 0.5 + cell_pad

        patch = Rectangle(
            (left, bottom),
            rect_w,
            rect_h,
            facecolor=row["color"],
            edgecolor="black",
            linewidth=0.55,
            zorder=3,
        )
        ax.add_patch(patch)

        rank = pd.to_numeric(row.get("순위", np.nan), errors="coerce")
        if rank == 1:
            hl = Rectangle(
                (left, bottom),
                rect_w,
                rect_h,
                facecolor="none",
                edgecolor="#FF0000",
                linewidth=1.6,
                zorder=4,
            )
            ax.add_patch(hl)

        label = to_two_chars(str(row["업체명정규화"]) or str(row["업체명"]))
        if label:
            tx = left + (rect_w / 2.0)
            ty = bottom + (rect_h / 2.0)
            label_font_pt = (cell_px * 0.50) * 72.0 / dpi
            txt = ax.text(
                tx,
                ty,
                label,
                ha="center",
                va="center",
                fontsize=label_font_pt,
                color="white",
                fontweight="heavy",
                zorder=5,
            )
            txt.set_clip_path(patch)
            txt.set_path_effects([pe.Stroke(linewidth=max(1.6, label_font_pt * 0.08), foreground="black"), pe.Normal()])

    # 0.01/0.001 모두 X축은 0.01 기준으로 읽히게 맞춘다.
    x_major = np.arange(args.min_rate, args.max_rate + 1e-9, x_major_interval)
    x_major_idx = ((x_major - args.min_rate) / step).round().astype(int)
    valid = (x_major_idx >= 0) & (x_major_idx < n_cols)
    ax.set_xticks(x_major_idx[valid])
    ax.set_xticklabels(
        [f"{x:.{decimals}f}" for x in x_major[valid]],
        fontsize=max(10.0, min(14.0, (cell_px * x_major_line_step * 0.14))),
        rotation=30,
        ha="right",
        rotation_mode="anchor",
    )
    ax.tick_params(axis="x", pad=10)

    labels: list[str] = []
    for notice in notice_order:
        block = df[df["공고번호"] == notice]
        dt = block["개찰dt"].max()
        if pd.notna(dt):
            labels.append(f"{notice} ({dt:%y-%m-%d %H:%M})")
        else:
            labels.append(str(notice))

    ax.set_yticks(range(len(notice_order)))
    ax.set_yticklabels(labels, fontsize=7)
    ax.set_xlabel("기초대비사정률 (%)", fontsize=13, fontweight="bold")
    ax.set_ylabel("공고 (아래로 갈수록 최신)", fontsize=13, fontweight="bold")
    region_label = output_path.stem.split("_")[0] if "_시각화_" in output_path.stem else input_path.stem
    min_dt = df["개찰dt"].min()
    max_dt = df["개찰dt"].max()
    period_label = f" · 기간 {args.period}" if args.period else ""
    date_label = ""
    if pd.notna(min_dt) and pd.notna(max_dt):
        date_label = f" · {min_dt:%Y-%m-%d} ~ {max_dt:%Y-%m-%d}"
    ax.set_title(
        f"{region_label} 낙찰 시각화{period_label} (정밀도 {args.precision}){date_label} · 칸수 {n_cols} x {n_rows} · 공고 {len(notice_order)}개 / 행 {len(df)}개",
        fontsize=15,
        fontweight="bold",
    )

    # tight_layout은 add_axes와 함께 축 비율을 흔들 수 있어 사용하지 않음
    fig.savefig(output_path)
    plt.close(fig)
    print(f"[OK] saved: {output_path}")


if __name__ == "__main__":
    main()
