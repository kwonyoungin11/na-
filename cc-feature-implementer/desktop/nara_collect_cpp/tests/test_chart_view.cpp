#include "nara/chart_view.h"

#include <cmath>
#include <iostream>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

}  // namespace

int main() {
    nara::ChartView view;
    view.world_w = 4020;
    view.world_h = 800;
    view.plot_w = 1000;
    view.plot_h = 600;
    view.scroll_x = 200;
    view.scroll_y = 40;

    const auto panned = nara::PanBy(view, 40, -20);
    if (panned.scroll_x != 160) {
        return Fail("C# drag-right moves content right: scroll_x must decrease");
    }
    if (panned.scroll_y != 60) {
        return Fail("drag-up increases scroll_y");
    }

    view.scroll_x = 0;
    const auto clamped = nara::PanBy(view, 80, 0);
    if (clamped.scroll_x != -nara::kViewPad) {
        return Fail("C# default offset 12px: pan may reach -kViewPad, not flush 0");
    }
    if (nara::kViewPad != 12) {
        return Fail("C# NativeBidChartControl default offset is (12, 12)");
    }

    nara::ChartView tight = view;
    tight.world_w = 2000;
    tight.world_h = 2000;
    tight.plot_w = 800;
    tight.plot_h = 600;
    tight.scroll_x = 2000;
    tight.scroll_y = 2000;
    const auto bottom = nara::ClampView(tight);
    if (bottom.scroll_y != tight.world_h - tight.plot_h) {
        return Fail("max Y scroll ends on the 5 empty cells under the last notice");
    }
    if (bottom.scroll_x != tight.world_w - tight.plot_w + nara::kViewPad) {
        return Fail("max scroll must keep 12px pad at the right edge");
    }

    if (nara::kBottomPadCells != 5) {
        return Fail("bottom margin is 5 scatter cells");
    }
    if (nara::ScatterWorldHeight(39, 20) != (39 + 5) * 20) {
        return Fail("world height includes 5 empty cells under the notices");
    }

    nara::ChartView tall = view;
    tall.scale = 1.0;
    tall.world_w = 4020;
    tall.world_h = nara::ScatterWorldHeight(39, 20);
    tall.plot_w = 1000;
    tall.plot_h = 600;
    tall.scroll_x = 0;
    tall.scroll_y = 0;
    const auto latest = nara::ShowLatestView(tall);
    if (latest.scroll_y != tall.world_h - tall.plot_h) {
        return Fail("start view scrolls to the newest notices at the bottom");
    }
    const int cell = nara::CellSize(1.0);
    const int newest_y = nara::LatestNoticePlotY(39, cell, tall.plot_h);
    if (newest_y < 0 || newest_y + cell > tall.plot_h) {
        return Fail("newest notice is inside the plot, 5 empty cells above the bottom");
    }
    if (newest_y != tall.plot_h - (nara::kBottomPadCells + 1) * cell) {
        return Fail("newest row sits on the last data row, not reversed to the top");
    }

    nara::ChartView small = view;
    small.world_w = 400;
    small.world_h = nara::ScatterWorldHeight(3, 20);
    small.plot_w = 800;
    small.plot_h = 600;
    small.scroll_x = 0;
    small.scroll_y = 0;
    const auto fitted = nara::ShowLatestView(small);
    if (fitted.scroll_y != small.world_h - small.plot_h) {
        return Fail("short map bottom-aligns so the 5 empty cells sit on the plot bottom");
    }

    if (nara::HoverColumn(0, 0, 1.0) != 0) {
        return Fail("column 0 at plot origin");
    }
    if (nara::HoverColumn(20, 0, 1.0) != 1) {
        return Fail("20px cell at scale 1 is column 1");
    }
    if (std::fabs(nara::ColumnToRate(50, 99.0, 0.01) - 99.50) > 0.00001) {
        return Fail("column 50 is 99.50");
    }

    view.scroll_x = 100;
    view.scale = 1.0;
    const auto zoomed = nara::ZoomAt(view, 200, 100, true);
    if (zoomed.scale <= 1.0) {
        return Fail("wheel-up must increase scale like C# NativeBidChartControl");
    }
    return 0;
}
