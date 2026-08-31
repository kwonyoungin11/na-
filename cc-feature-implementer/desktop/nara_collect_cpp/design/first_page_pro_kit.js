/* First-page mock chart kit. Historical observation only. No sit / no forecast. */
(function (g) {
  const F = '"Malgun Gothic","Apple SD Gothic Neo",sans-serif';
  const COL = {
    text: "#111111",
    muted: "#5A6473",
    grid: "#EEF2F6",
    axis: "#CBD5E1",
    hot: "#0E7490",
    accent: "#D97706",
    dong: "#E11D48",
    gold: "#CA8A04",
    ink: "#0F172A",
    blue: "#2563EB",
    violet: "#7C3AED",
    slate: "#94A3B8",
    fill: "#CBD5E1",
    home: "#0E7490",
    peak: "#334155",
  };

  function ctx2d(id) {
    const c = document.getElementById(id);
    if (!c) return null;
    const dpr = 1;
    const w = c.clientWidth || c.width;
    const h = c.clientHeight || c.height;
    c.width = Math.max(1, Math.round(w * dpr));
    c.height = Math.max(1, Math.round(h * dpr));
    const x = c.getContext("2d");
    x.scale(dpr, dpr);
    x.w = w;
    x.h = h;
    return x;
  }

  function clear(x) {
    x.clearRect(0, 0, x.w, x.h);
  }

  function text(x, s, px, py, opt) {
    opt = opt || {};
    x.font = (opt.bold ? "700 " : "500 ") + (opt.size || 10) + "px " + F;
    x.fillStyle = opt.color || COL.muted;
    x.textAlign = opt.align || "left";
    x.textBaseline = opt.base || "alphabetic";
    x.fillText(s, px, py);
  }

  function pad(x, l, t, r, b) {
    return { x: l, y: t, w: x.w - l - r, h: x.h - t - b };
  }

  function maxv(a) {
    return Math.max.apply(null, a.concat([1]));
  }

  function spark(id, data, color) {
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const p = pad(x, 2, 4, 2, 4);
    const lo = Math.min.apply(null, data);
    const hi = Math.max.apply(null, data);
    const span = hi - lo || 1;
    x.beginPath();
    x.strokeStyle = color || COL.hot;
    x.lineWidth = 1.4;
    data.forEach(function (v, i) {
      const px = p.x + (i / (data.length - 1)) * p.w;
      const py = p.y + p.h - ((v - lo) / span) * p.h;
      if (i === 0) x.moveTo(px, py);
      else x.lineTo(px, py);
    });
    x.stroke();
  }

  function vbars(id, labels, values, opt) {
    opt = opt || {};
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const p = pad(x, 8, 16, 8, 18);
    const m = maxv(values);
    const n = values.length;
    const gap = 3;
    const bw = (p.w - gap * (n - 1)) / n;
    values.forEach(function (v, i) {
      const h = Math.max(2, (v / m) * p.h);
      const bx = p.x + i * (bw + gap);
      const by = p.y + p.h - h;
      x.fillStyle = i === opt.home ? COL.home : i === opt.peak ? COL.peak : COL.fill;
      x.fillRect(bx, by, bw, h);
      text(x, String(v), bx + bw / 2, by - 3, { size: 9, bold: true, align: "center", color: COL.text });
      text(x, labels[i], bx + bw / 2, x.h - 4, { size: 8, align: "center", color: i === opt.home ? COL.home : COL.muted });
    });
  }

  function hbars(id, labels, values, opt) {
    opt = opt || {};
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const p = pad(x, 36, 4, 28, 4);
    const m = maxv(values);
    const n = values.length;
    const rh = p.h / n;
    values.forEach(function (v, i) {
      const y = p.y + i * rh + 3;
      const h = Math.max(6, rh - 6);
      const w = (v / m) * p.w;
      x.fillStyle = opt.colors && opt.colors[i] ? opt.colors[i] : COL.hot;
      x.globalAlpha = 0.85;
      x.fillRect(p.x, y, w, h);
      x.globalAlpha = 1;
      text(x, labels[i], 4, y + h * 0.72, { size: 10, color: COL.text });
      text(x, String(v), p.x + w + 4, y + h * 0.72, { size: 10, bold: true, color: COL.text });
    });
  }

  function lines(id, series, opt) {
    opt = opt || {};
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const p = pad(x, 36, 8, 10, 18);
    let lo = opt.ymin;
    let hi = opt.ymax;
    if (lo == null || hi == null) {
      const all = [];
      series.forEach(function (s) {
        all.push.apply(all, s.data);
      });
      lo = Math.min.apply(null, all);
      hi = Math.max.apply(null, all);
      const padn = (hi - lo) * 0.12 || 0.05;
      lo -= padn;
      hi += padn;
    }
    const span = hi - lo || 1;
    x.strokeStyle = COL.grid;
    x.lineWidth = 1;
    for (let g = 0; g < 4; g++) {
      const y = p.y + (g / 3) * p.h;
      x.beginPath();
      x.moveTo(p.x, y);
      x.lineTo(p.x + p.w, y);
      x.stroke();
      const v = hi - (g / 3) * span;
      text(x, v.toFixed(opt.digits == null ? 2 : opt.digits), 2, y + 3, { size: 9, color: COL.muted });
    }
    series.forEach(function (s) {
      x.beginPath();
      x.strokeStyle = s.color;
      x.lineWidth = 1.6;
      s.data.forEach(function (v, i) {
        const px = p.x + (i / (s.data.length - 1)) * p.w;
        const py = p.y + p.h - ((v - lo) / span) * p.h;
        if (i === 0) x.moveTo(px, py);
        else x.lineTo(px, py);
      });
      x.stroke();
    });
    if (opt.xlabels) {
      opt.xlabels.forEach(function (lb, i) {
        const px = p.x + (i / (opt.xlabels.length - 1)) * p.w;
        text(x, lb, px, x.h - 3, { size: 9, align: "center" });
      });
    }
    if (opt.legend) {
      let lx = p.x;
      series.forEach(function (s) {
        x.fillStyle = s.color;
        x.fillRect(lx, 2, 8, 8);
        text(x, s.name, lx + 11, 10, { size: 10, color: COL.text });
        lx += 12 + x.measureText(s.name).width + 14;
      });
    }
  }

  function area(id, data, opt) {
    opt = opt || {};
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const p = pad(x, 36, 8, 8, 16);
    const lo = opt.ymin != null ? opt.ymin : Math.min.apply(null, data.concat([0]));
    const hi = opt.ymax != null ? opt.ymax : Math.max.apply(null, data.concat([0]));
    const span = hi - lo || 1;
    const zero = p.y + p.h - ((0 - lo) / span) * p.h;
    x.strokeStyle = COL.grid;
    x.beginPath();
    x.moveTo(p.x, zero);
    x.lineTo(p.x + p.w, zero);
    x.stroke();
    x.beginPath();
    data.forEach(function (v, i) {
      const px = p.x + (i / (data.length - 1)) * p.w;
      const py = p.y + p.h - ((v - lo) / span) * p.h;
      if (i === 0) x.moveTo(px, py);
      else x.lineTo(px, py);
    });
    x.strokeStyle = opt.color || COL.violet;
    x.lineWidth = 1.5;
    x.stroke();
    x.lineTo(p.x + p.w, zero);
    x.lineTo(p.x, zero);
    x.closePath();
    x.fillStyle = opt.color || COL.violet;
    x.globalAlpha = 0.16;
    x.fill();
    x.globalAlpha = 1;
    text(x, lo.toFixed(2), 2, p.y + p.h, { size: 9 });
    text(x, hi.toFixed(2), 2, p.y + 8, { size: 9 });
  }

  function scatter(id, pts, opt) {
    opt = opt || {};
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const p = pad(x, 36, 10, 10, 22);
    const x0 = opt.x0, x1 = opt.x1, y0 = opt.y0, y1 = opt.y1;
    function X(v) { return p.x + ((v - x0) / (x1 - x0)) * p.w; }
    function Y(v) { return p.y + p.h - ((v - y0) / (y1 - y0)) * p.h; }
    x.strokeStyle = COL.grid;
    x.lineWidth = 1;
    for (let i = 0; i <= 4; i++) {
      const xv = x0 + (i / 4) * (x1 - x0);
      const yv = y0 + (i / 4) * (y1 - y0);
      x.beginPath();
      x.moveTo(X(xv), p.y);
      x.lineTo(X(xv), p.y + p.h);
      x.stroke();
      x.beginPath();
      x.moveTo(p.x, Y(yv));
      x.lineTo(p.x + p.w, Y(yv));
      x.stroke();
      text(x, xv.toFixed(1), X(xv), x.h - 4, { size: 9, align: "center" });
      text(x, yv.toFixed(1), 2, Y(yv) + 3, { size: 9 });
    }
    x.strokeStyle = "#E2E8F0";
    x.setLineDash([3, 3]);
    x.beginPath();
    x.moveTo(X(Math.max(x0, y0)), Y(Math.max(x0, y0)));
    x.lineTo(X(Math.min(x1, y1)), Y(Math.min(x1, y1)));
    x.stroke();
    x.setLineDash([]);
    pts.forEach(function (pt) {
      x.fillStyle = pt.dong ? COL.dong : COL.blue;
      x.globalAlpha = pt.dong ? 0.9 : 0.45;
      x.beginPath();
      x.arc(X(pt.x), Y(pt.y), pt.dong ? 3.2 : 2.4, 0, Math.PI * 2);
      x.fill();
      x.globalAlpha = 1;
    });
    text(x, opt.xlab || "1위 사정률", p.x + p.w / 2, x.h - 2, { size: 10, align: "center", color: COL.text });
  }

  function heatmap(id, matrix, rowL, colL) {
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const p = pad(x, 36, 6, 8, 18);
    const rows = matrix.length;
    const cols = matrix[0].length;
    let m = 1;
    matrix.forEach(function (r) {
      r.forEach(function (v) { if (v > m) m = v; });
    });
    const cw = p.w / cols;
    const rh = p.h / rows;
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        const t = matrix[r][c] / m;
        const gch = Math.round(248 - t * 140);
        const bch = Math.round(250 - t * 90);
        const rch = Math.round(236 - t * 180);
        x.fillStyle = "rgb(" + rch + "," + gch + "," + bch + ")";
        x.fillRect(p.x + c * cw + 0.5, p.y + r * rh + 0.5, cw - 1, rh - 1);
      }
      text(x, rowL[r], 2, p.y + r * rh + rh * 0.7, { size: 9 });
    }
    colL.forEach(function (lb, c) {
      text(x, lb, p.x + c * cw + cw / 2, x.h - 4, { size: 8, align: "center" });
    });
  }

  function donut(id, parts) {
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const cx = 46;
    const cy = x.h / 2;
    const r = Math.min(34, x.h / 2 - 6);
    const sum = parts.reduce(function (a, p) { return a + p.n; }, 0) || 1;
    let a = -Math.PI / 2;
    parts.forEach(function (p) {
      const da = (p.n / sum) * Math.PI * 2;
      x.beginPath();
      x.fillStyle = p.color;
      x.moveTo(cx, cy);
      x.arc(cx, cy, r, a, a + da);
      x.closePath();
      x.fill();
      a += da;
    });
    x.globalCompositeOperation = "destination-out";
    x.beginPath();
    x.arc(cx, cy, r * 0.58, 0, Math.PI * 2);
    x.fill();
    x.globalCompositeOperation = "source-over";
    let ly = 14;
    parts.forEach(function (p) {
      x.fillStyle = p.color;
      x.fillRect(92, ly - 8, 8, 8);
      text(x, p.label + "  " + p.n, 104, ly, { size: 11, color: COL.text });
      ly += 16;
    });
  }

  function boxplot(id, st) {
    const x = ctx2d(id);
    if (!x) return;
    clear(x);
    const p = pad(x, 10, 18, 10, 16);
    const lo = st.min - 0.02;
    const hi = st.max + 0.02;
    function X(v) { return p.x + ((v - lo) / (hi - lo)) * p.w; }
    const mid = p.y + p.h / 2;
    x.strokeStyle = COL.ink;
    x.lineWidth = 1.2;
    x.beginPath();
    x.moveTo(X(st.min), mid);
    x.lineTo(X(st.max), mid);
    x.stroke();
    x.fillStyle = "#ECFEFF";
    x.strokeStyle = COL.hot;
    x.fillRect(X(st.q1), mid - 14, X(st.q3) - X(st.q1), 28);
    x.strokeRect(X(st.q1), mid - 14, X(st.q3) - X(st.q1), 28);
    x.beginPath();
    x.moveTo(X(st.med), mid - 14);
    x.lineTo(X(st.med), mid + 14);
    x.stroke();
    x.fillStyle = COL.ink;
    x.fillRect(X(st.min) - 1, mid - 8, 2, 16);
    x.fillRect(X(st.max) - 1, mid - 8, 2, 16);
    text(x, st.min.toFixed(2), X(st.min), p.y + p.h + 12, { size: 9, align: "center" });
    text(x, st.med.toFixed(2), X(st.med), p.y + 2, { size: 10, align: "center", bold: true, color: COL.text });
    text(x, st.max.toFixed(2), X(st.max), p.y + p.h + 12, { size: 9, align: "center" });
  }

  const TABLE_ROWS = [
    ["sel", "08-26", "정읍시 정읍1지구 하천환경정비사업", "전국", "99.906", "미래", "19위 · 52개사"],
    ["", "08-20", "창녕-고암 국도건설공사 문화재 정밀발굴", "전국", "99.780", "춘추", "58위 · 74개사"],
    ["", "08-20", "고성-통영 국도건설공사 시점부 발굴", "전국", "100.561", "중청", "26위 · 73개사"],
    ["", "08-14", "부산 용천리 334-1번지 국비지원", "전국", "99.482", "동양", "5위 · 73개사"],
    ["", "08-14", "광양 목성리 621-1번지 국비지원", "전국", "99.840", "국강", "61위 · 78개사"],
    ["", "08-14", "나주 월태리 109-1번지 국비지원", "전국", "99.722", "고대", "67위 · 78개사"],
    ["", "08-14", "제주 광령리 1615번지 국비지원", "전국", "99.737", "가디", "2위 · 65개사"],
    ["", "08-06", "제주 곽지리 2109번지 국비지원", "전국", "99.874", "울산", "49위 · 64개사"],
    ["", "08-06", "경주 인왕동 536-12번지 국비지원", "전국", "100.510", "가야", "27위 · 61개사"],
    ["", "08-04", "대구 용수동 429번지 국비지원", "전국", "100.132", "경산", "12위 · 72개사"],
    ["", "08-04", "경주 사정동 462-3번지 국비지원", "전국", "99.440", "고운", "3위 · 68개사"],
    ["", "08-04", "포항 성내리 53-2번지 국비지원", "전국", "99.899", "민족", "55위 · 74개사"],
    ["", "07-22", "충주 단월동 550번지 국비지원", "전국", "99.288", "중앙", "14위 · 70개사"],
    ["", "07-22", "상주 복용동 300번지 국비지원", "전국", "99.201", "라온", "5위 · 61개사"],
  ];

  function mountTable(id, compact) {
    const el = document.getElementById(id);
    if (!el) return;
    let html;
    if (compact) {
      html = "<table><thead><tr><th>개찰</th><th>1위</th><th>업체</th><th>동국</th></tr></thead><tbody>";
      TABLE_ROWS.forEach(function (r) {
        const rk = r[6].split("·")[0].trim();
        html += "<tr class=\"" + r[0] + "\"><td>" + r[1] + "</td><td class=\"gold\">" + r[4] + "</td><td>" + r[5]
          + "</td><td>" + rk + "</td></tr>";
      });
      html += "</tbody></table>";
    } else {
      html = "<table><thead><tr><th>개찰일</th><th>공고명</th><th>지역</th><th>1위 사정률</th><th>1위 업체</th><th>동국순위</th></tr></thead><tbody>";
      TABLE_ROWS.forEach(function (r) {
        html += "<tr class=\"" + r[0] + "\"><td>" + r[1] + "</td><td class=\"clip\">" + r[2] + "</td><td>" + r[3]
          + "</td><td class=\"gold\">" + r[4] + "</td><td>" + r[5] + "</td><td>" + r[6] + "</td></tr>";
      });
      html += "</tbody></table>";
    }
    el.innerHTML = html;
  }

  g.Pro = {
    COL: COL,
    spark: spark,
    vbars: vbars,
    hbars: hbars,
    lines: lines,
    area: area,
    scatter: scatter,
    heatmap: heatmap,
    donut: donut,
    boxplot: boxplot,
    mountTable: mountTable,
  };

  g.ProData = {
    months: ["9", "10", "11", "12", "1", "2", "3", "4", "5", "6", "7", "8"],
    bins: ["99.10", "99.20", "99.30", "99.40", "99.50", "99.60", "99.70", "99.80", "99.90", "100.00", "100.10", "100.20"],
    winN: [1, 5, 3, 4, 3, 8, 11, 10, 11, 14, 6, 8],
    rankL: ["1–10", "11–20", "21–30", "31–40", "41–50", "51–60", "61–70", "71+"],
    rankN: [12, 18, 22, 20, 16, 14, 12, 10],
    dongRate: [99.58, 99.60, 99.61, 99.59, 99.62, 99.61, 99.60, 99.63, 99.61, 99.58, 99.60, 99.61],
    winRate: [99.72, 99.80, 99.75, 99.88, 99.70, 99.85, 99.90, 99.78, 99.82, 100.05, 99.74, 99.81],
    gap: [0.14, 0.20, 0.14, 0.29, 0.08, 0.24, 0.30, 0.15, 0.21, 0.47, 0.14, 0.20],
    notices: [8, 9, 11, 10, 12, 9, 14, 11, 10, 13, 8, 9],
    nFirm: [68, 72, 70, 74, 65, 71, 78, 64, 61, 72, 68, 70],
    rankMed: [52, 48, 45, 50, 42, 48, 40, 51, 47, 55, 44, 48],
    watchL: ["미래", "한울", "해동", "고대", "춘추", "동양"],
    watchN: [18, 14, 11, 9, 7, 6],
    winL: ["미래", "춘추", "고대", "한울", "중청"],
    winC: [8, 7, 6, 5, 4],
    heat: [
      [0, 1, 0, 1, 0, 1, 2, 1, 1, 2, 0, 1],
      [0, 0, 1, 0, 1, 1, 1, 2, 1, 1, 1, 0],
      [1, 1, 0, 1, 0, 0, 2, 1, 2, 1, 0, 1],
      [0, 0, 1, 0, 1, 1, 1, 1, 1, 2, 1, 1],
      [0, 1, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1],
      [0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0],
      [0, 1, 1, 0, 1, 1, 0, 1, 1, 2, 0, 1],
      [1, 0, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1],
      [0, 1, 0, 0, 1, 0, 1, 1, 1, 1, 1, 0],
      [0, 0, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1],
      [0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0],
      [0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1],
    ],
    pts: (function () {
      const o = [];
      const wr = [99.91, 99.78, 100.56, 99.48, 99.84, 99.72, 99.74, 99.87, 100.51, 100.13, 99.44, 99.90, 99.29, 99.20, 99.67, 99.81, 99.61, 99.55, 100.04, 99.73];
      const dr = [100.48, 99.39, 99.40, 99.61, 99.61, 99.61, 99.74, 99.37, 99.37, 100.27, 99.55, 99.42, 99.75, 99.44, 99.66, 99.58, 99.62, 99.59, 99.60, 99.63];
      for (let i = 0; i < wr.length; i++) o.push({ x: wr[i], y: dr[i], dong: true });
      return o;
    })(),
  };
})(window);
