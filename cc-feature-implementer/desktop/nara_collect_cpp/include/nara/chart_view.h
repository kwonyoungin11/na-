#pragma once

namespace nara {

struct ChartView {
    int scroll_x = 0;
    int scroll_y = 0;
    double scale = 1.0;
    int world_w = 0;
    int world_h = 0;
    int plot_w = 0;
    int plot_h = 0;
};

// C# NativeBidChartControl default _offset is (12, 12).
constexpr int kViewPad = 12;
// Empty scatter rows under the newest notice.
constexpr int kBottomPadCells = 5;

int CellSize(double scale);
inline int ScatterWorldHeight(int notice_rows, int cell) {
    const int rows = (notice_rows > 0 ? notice_rows : 0) + kBottomPadCells;
    const int safe_cell = cell > 0 ? cell : 20;
    return rows * safe_cell;
}
ChartView ClampView(ChartView view);
ChartView CenterView(ChartView view);
ChartView ShowLatestView(ChartView view);

inline int LatestNoticePlotY(int notice_count, int cell, int plot_h) {
    (void)notice_count;
    const int c = cell > 0 ? cell : 20;
    return plot_h - (kBottomPadCells + 1) * c;
}
ChartView PanBy(ChartView view, int dx, int dy);
ChartView ZoomAt(ChartView view, int plot_x, int plot_y, bool zoom_in);
int HoverColumn(int plot_x, int scroll_x, double scale);
double ColumnToRate(int column, double min_rate, double step);

}  // namespace nara
