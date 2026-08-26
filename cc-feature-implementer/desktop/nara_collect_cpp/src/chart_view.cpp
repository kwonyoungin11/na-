#include "nara/chart_view.h"

#include <algorithm>
#include <cmath>

namespace nara {

int CellSize(double scale) {
    const double safe = scale <= 0.0001 ? 1.0 : scale;
    return (std::max)(8, static_cast<int>(std::llround(20.0 * safe)));
}

int ClampScrollX(int scroll, int world, int plot) {
    const int slack = plot - world;
    if (slack >= 0) {
        const int center = -slack / 2;
        return std::clamp(scroll, center - kViewPad, center + kViewPad);
    }
    return std::clamp(scroll, -kViewPad, -slack + kViewPad);
}

int ClampScrollY(int scroll, int world, int plot) {
    const int slack = plot - world;
    if (slack >= 0) {
        return std::clamp(scroll, -slack - kViewPad, -slack + kViewPad);
    }
    return std::clamp(scroll, -kViewPad, -slack);
}

ChartView ClampView(ChartView view) {
    view.scale = std::clamp(view.scale, 0.25, 5.0);
    view.scroll_x = ClampScrollX(view.scroll_x, view.world_w, view.plot_w);
    view.scroll_y = ClampScrollY(view.scroll_y, view.world_h, view.plot_h);
    return view;
}

ChartView CenterView(ChartView view) {
    view.scale = 1.0;
    view.scroll_x = (view.world_w - view.plot_w) / 2;
    view.scroll_y = (view.world_h - view.plot_h) / 2;
    return ClampView(view);
}

ChartView ShowLatestView(ChartView view) {
    view.scroll_x = (view.world_w - view.plot_w) / 2;
    view.scroll_y = view.world_h - view.plot_h;
    return ClampView(view);
}

ChartView PanBy(ChartView view, int dx, int dy) {
    view.scroll_x -= dx;
    view.scroll_y -= dy;
    return ClampView(view);
}

ChartView ZoomAt(ChartView view, int plot_x, int plot_y, bool zoom_in) {
    const int old_cell = CellSize(view.scale);
    const double world_x = (plot_x + view.scroll_x) / static_cast<double>(old_cell);
    const double world_y = (plot_y + view.scroll_y) / static_cast<double>(old_cell);
    view.scale = std::clamp(view.scale * (zoom_in ? 1.10 : 0.90), 0.25, 5.0);
    const int new_cell = CellSize(view.scale);
    view.world_w = static_cast<int>(std::llround(view.world_w * (new_cell / static_cast<double>(old_cell))));
    view.world_h = static_cast<int>(std::llround(view.world_h * (new_cell / static_cast<double>(old_cell))));
    view.scroll_x = static_cast<int>(std::llround(world_x * new_cell)) - plot_x;
    view.scroll_y = static_cast<int>(std::llround(world_y * new_cell)) - plot_y;
    return ClampView(view);
}

int HoverColumn(int plot_x, int scroll_x, double scale) {
    const int cell = CellSize(scale);
    if (cell <= 0) {
        return 0;
    }
    const int x = plot_x + scroll_x;
    return x / cell;
}

double ColumnToRate(int column, double min_rate, double step) {
    return min_rate + (column * step);
}

}  // namespace nara
