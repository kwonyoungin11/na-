#include "nara/dwrite_text.h"

#include <cmath>
#include <iostream>

namespace {

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << "\n";
    return 1;
}

}  // namespace

int main() {
    using namespace nara::dwrite_text;
    float rgba[4] = {};
    ArgbToRgba(0xFF111111u, rgba);
    if (std::fabs(rgba[0] - 17.0f / 255.0f) > 0.001f || std::fabs(rgba[3] - 1.0f) > 0.001f) {
        return Fail("WPF #111111 unpack");
    }
    ArgbToRgba(0x59000000u, rgba);
    if (std::fabs(rgba[3] - 89.0f / 255.0f) > 0.001f || rgba[0] > 0.001f) {
        return Fail("chip outline #59000000 alpha");
    }
    if (OutlineNeighborCount() != 8) {
        return Fail("glyph outline is 8-neighbor like geometry stroke");
    }
    if (std::fabs(kDwriteClearTypeLevel - 1.0f) > 0.001f || kDwriteTextAntialiasClearType != 1
        || kDwritePixelGeometryRgb != 1 || kDwriteRenderingNaturalSymmetric != 5
        || std::fabs(kDwriteEnhancedContrast - 0.5f) > 0.001f) {
        return Fail("C++ text must use ClearType like WPF, not grayscale");
    }
    std::cerr << "ok dwrite text tokens match WPF Ideal\n";
    return 0;
}
