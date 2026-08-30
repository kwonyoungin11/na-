#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "nara/dwrite_text.h"

#include <cmath>
#include <d2d1.h>
#include <dwrite.h>
#include <vector>
#include <windows.h>
#include <wrl/client.h>

#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "dwrite.lib")

namespace nara {
namespace dwrite_text {
namespace {

using Microsoft::WRL::ComPtr;

std::vector<Job> g_jobs;
D2D1_RECT_F g_plot_clip{0, 0, 0, 0};
bool g_has_plot_clip = false;

struct LayoutCache {
    std::wstring text;
    int size_milli = 0;
    int bold = 0;
    int w_milli = 0;
    int h_milli = 0;
    int h_align = 0;
    int v_align = 0;
    int ellipsis = 0;
    ComPtr<IDWriteTextLayout> layout;
};
std::vector<LayoutCache> g_layout_cache;
ComPtr<ID2D1Factory> g_d2d;
ComPtr<IDWriteFactory> g_dwrite;
ComPtr<ID2D1DCRenderTarget> g_rt;
ComPtr<IDWriteRenderingParams> g_params;

bool EnsureFactories() {
    if (g_d2d && g_dwrite && g_rt && g_params) {
        return true;
    }
    if (FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, g_d2d.ReleaseAndGetAddressOf()))) {
        return false;
    }
    if (FAILED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory),
                                   reinterpret_cast<IUnknown**>(g_dwrite.ReleaseAndGetAddressOf())))) {
        return false;
    }
    D2D1_RENDER_TARGET_PROPERTIES props = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_IGNORE), 0.0f, 0.0f,
        D2D1_RENDER_TARGET_USAGE_NONE, D2D1_FEATURE_LEVEL_DEFAULT);
    if (FAILED(g_d2d->CreateDCRenderTarget(&props, g_rt.ReleaseAndGetAddressOf()))) {
        return false;
    }
    if (FAILED(g_dwrite->CreateCustomRenderingParams(
            kDwriteGamma, kDwriteEnhancedContrast, kDwriteClearTypeLevel,
            static_cast<DWRITE_PIXEL_GEOMETRY>(kDwritePixelGeometryRgb),
            static_cast<DWRITE_RENDERING_MODE>(kDwriteRenderingNaturalSymmetric),
            g_params.ReleaseAndGetAddressOf()))) {
        return false;
    }
    return true;
}

D2D1_COLOR_F ToColor(uint32_t argb) {
    float rgba[4] = {};
    ArgbToRgba(argb, rgba);
    return D2D1::ColorF(rgba[0], rgba[1], rgba[2], rgba[3]);
}

DWRITE_TEXT_ALIGNMENT HAlign(Align a) {
    if (a == Align::Center) {
        return DWRITE_TEXT_ALIGNMENT_CENTER;
    }
    if (a == Align::Far) {
        return DWRITE_TEXT_ALIGNMENT_TRAILING;
    }
    return DWRITE_TEXT_ALIGNMENT_LEADING;
}

DWRITE_PARAGRAPH_ALIGNMENT VAlign(Align a) {
    if (a == Align::Center) {
        return DWRITE_PARAGRAPH_ALIGNMENT_CENTER;
    }
    if (a == Align::Far) {
        return DWRITE_PARAGRAPH_ALIGNMENT_FAR;
    }
    return DWRITE_PARAGRAPH_ALIGNMENT_NEAR;
}

}  // namespace

void ResetQueue() { g_jobs.clear(); }

void SetPlotClip(float x, float y, float w, float h) {
    g_plot_clip = D2D1::RectF(x, y, x + w, y + h);
    g_has_plot_clip = w > 1.0f && h > 1.0f;
}

void Queue(Job job) {
    if (job.text.empty() || job.w < 1.0f || job.h < 1.0f || job.size < 1.0f) {
        return;
    }
    g_jobs.push_back(std::move(job));
}

void Flush(void* hdc_void, int pixel_w, int pixel_h, float dpi_scale, int origin_x, int origin_y) {
    HDC hdc = static_cast<HDC>(hdc_void);
    if (hdc == nullptr || pixel_w < 1 || pixel_h < 1 || g_jobs.empty()) {
        g_jobs.clear();
        return;
    }
    if (!EnsureFactories()) {
        g_jobs.clear();
        return;
    }
    const float scale = dpi_scale > 0.0f ? dpi_scale : 1.0f;
    RECT bind{origin_x, origin_y, origin_x + pixel_w, origin_y + pixel_h};
    if (FAILED(g_rt->BindDC(hdc, &bind))) {
        g_jobs.clear();
        return;
    }
    g_rt->SetDpi(96.0f * scale, 96.0f * scale);
    g_rt->SetTextAntialiasMode(static_cast<D2D1_TEXT_ANTIALIAS_MODE>(kDwriteTextAntialiasClearType));
    g_rt->SetTextRenderingParams(g_params.Get());
    g_rt->BeginDraw();
    for (const auto& job : g_jobs) {
        ComPtr<IDWriteTextFormat> format;
        const DWRITE_FONT_WEIGHT weight = job.bold ? DWRITE_FONT_WEIGHT_BOLD : DWRITE_FONT_WEIGHT_REGULAR;
        if (FAILED(g_dwrite->CreateTextFormat(L"Malgun Gothic", nullptr, weight, DWRITE_FONT_STYLE_NORMAL,
                                              DWRITE_FONT_STRETCH_NORMAL, job.size, L"ko-kr",
                                              format.GetAddressOf()))) {
            continue;
        }
        format->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);
        format->SetTextAlignment(HAlign(job.h_align));
        format->SetParagraphAlignment(VAlign(job.v_align));
        const int size_milli = static_cast<int>(std::lround(job.size * 1000.0f));
        const int w_milli = static_cast<int>(std::lround(job.w * 1000.0f));
        const int h_milli = static_cast<int>(std::lround(job.h * 1000.0f));
        const int h_align = static_cast<int>(job.h_align);
        const int v_align = static_cast<int>(job.v_align);
        IDWriteTextLayout* layout_ptr = nullptr;
        for (auto& entry : g_layout_cache) {
            if (entry.size_milli == size_milli && entry.bold == (job.bold ? 1 : 0) && entry.w_milli == w_milli
                && entry.h_milli == h_milli && entry.h_align == h_align && entry.v_align == v_align
                && entry.ellipsis == (job.ellipsis ? 1 : 0) && entry.text == job.text) {
                layout_ptr = entry.layout.Get();
                break;
            }
        }
        ComPtr<IDWriteTextLayout> created;
        if (layout_ptr == nullptr) {
            if (FAILED(g_dwrite->CreateTextLayout(job.text.c_str(), static_cast<UINT32>(job.text.size()), format.Get(),
                                                  job.w, job.h, created.GetAddressOf()))) {
                continue;
            }
            if (job.ellipsis) {
                DWRITE_TRIMMING trim{DWRITE_TRIMMING_GRANULARITY_CHARACTER, 0, 0};
                ComPtr<IDWriteInlineObject> ellipsis;
                if (SUCCEEDED(g_dwrite->CreateEllipsisTrimmingSign(format.Get(), ellipsis.GetAddressOf()))) {
                    created->SetTrimming(&trim, ellipsis.Get());
                }
            }
            layout_ptr = created.Get();
            LayoutCache entry;
            entry.text = job.text;
            entry.size_milli = size_milli;
            entry.bold = job.bold ? 1 : 0;
            entry.w_milli = w_milli;
            entry.h_milli = h_milli;
            entry.h_align = h_align;
            entry.v_align = v_align;
            entry.ellipsis = job.ellipsis ? 1 : 0;
            entry.layout = created;
            if (g_layout_cache.size() >= 512) {
                g_layout_cache.erase(g_layout_cache.begin());
            }
            g_layout_cache.push_back(std::move(entry));
            layout_ptr = g_layout_cache.back().layout.Get();
        }
        ComPtr<ID2D1SolidColorBrush> fill;
        if (FAILED(g_rt->CreateSolidColorBrush(ToColor(job.argb), fill.GetAddressOf()))) {
            continue;
        }
        if (job.clip_plot && g_has_plot_clip) {
            g_rt->PushAxisAlignedClip(g_plot_clip, D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
        }
        if (job.clip) {
            g_rt->PushAxisAlignedClip(D2D1::RectF(job.x, job.y, job.x + job.w, job.y + job.h),
                                      D2D1_ANTIALIAS_MODE_ALIASED);
        }
        const D2D1_POINT_2F origin{job.x, job.y};
        if (job.outline_argb != 0 && job.outline_dip > 0.0f) {
            ComPtr<ID2D1SolidColorBrush> outline;
            if (SUCCEEDED(g_rt->CreateSolidColorBrush(ToColor(job.outline_argb), outline.GetAddressOf()))) {
                const float o = job.outline_dip;
                const float ox[8] = {-o, o, 0, 0, -o, -o, o, o};
                const float oy[8] = {0, 0, -o, o, -o, o, -o, o};
                for (int i = 0; i < OutlineNeighborCount(); ++i) {
                    g_rt->DrawTextLayout(D2D1::Point2F(origin.x + ox[i], origin.y + oy[i]), layout_ptr, outline.Get(),
                                         D2D1_DRAW_TEXT_OPTIONS_CLIP);
                }
            }
        }
        g_rt->DrawTextLayout(origin, layout_ptr, fill.Get(), D2D1_DRAW_TEXT_OPTIONS_CLIP);
        if (job.clip) {
            g_rt->PopAxisAlignedClip();
        }
        if (job.clip_plot && g_has_plot_clip) {
            g_rt->PopAxisAlignedClip();
        }
    }
    g_rt->EndDraw();
    g_jobs.clear();
}

}  // namespace dwrite_text
}  // namespace nara
