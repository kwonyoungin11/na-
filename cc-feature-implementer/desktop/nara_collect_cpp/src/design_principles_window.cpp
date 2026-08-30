#ifndef NOMINMAX
#define NOMINMAX
#endif
#include "nara/chart_popup.h"

#include "nara/chart_theme.h"
#include "nara/design_principles.h"
#include "nara/dwrite_text.h"

#include <algorithm>
#include <windows.h>
#include <windowsx.h>
#include <gdiplus.h>

#pragma comment(lib, "gdiplus.lib")

namespace nara {
namespace {

using chart_theme::DesignPrinciplesChrome;
using chart_theme::DipToPx;
using chart_theme::DpiScaleFromDpi;
using chart_theme::kAccentBar0Argb;
using chart_theme::kAccentBar1Argb;
using chart_theme::kCardFillArgb;
using chart_theme::kDesignPrinciplesClass;
using chart_theme::kDesignPrinciplesTitle;
using chart_theme::kEpilogueAccentArgb;
using chart_theme::kEpilogueQuoteFillArgb;
using chart_theme::kHoverRadius;
using chart_theme::kPrincipleMinH;
using chart_theme::kPrincipleMinW;
using chart_theme::kTextArgb;
using chart_theme::kWindowBgArgb;
using design_principles::EpilogueKind;
using chart_theme::LayoutDesignPrinciplesChrome;
using chart_theme::PxToDip;
using design_principles::LayoutEpilogue;

HWND g_hwnd = nullptr;
int g_scroll = 0;
float g_dpi = 1.0f;
DesignPrinciplesChrome g_chrome;

unsigned WindowDpi(HWND hwnd) {
    using Fn = UINT(WINAPI*)(HWND);
    static Fn fn =
        reinterpret_cast<Fn>(GetProcAddress(GetModuleHandleW(L"user32.dll"), "GetDpiForWindow"));
    UINT dpi = 96;
    if (fn != nullptr && hwnd != nullptr) {
        dpi = fn(hwnd);
    }
    if (dpi < 96) {
        dpi = 96;
    }
    if (dpi > 384) {
        dpi = 384;
    }
    return dpi;
}

void SyncDpi(HWND hwnd) { g_dpi = DpiScaleFromDpi(WindowDpi(hwnd)); }
int ToPx(int dip) { return DipToPx(dip, g_dpi); }
int ToDip(int px) { return PxToDip(px, g_dpi); }

Gdiplus::Color Argb(uint32_t argb) { return Gdiplus::Color(argb); }

void AddRoundRect(Gdiplus::GraphicsPath& path, Gdiplus::RectF r, float radius) {
    const float d = radius * 2.0f;
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
    path.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
    path.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
    path.CloseFigure();
}

void FillRoundRect(Gdiplus::Graphics& g, Gdiplus::RectF r, float radius, uint32_t fill) {
    Gdiplus::GraphicsPath path;
    AddRoundRect(path, r, radius);
    Gdiplus::SolidBrush brush(Argb(fill));
    g.FillPath(&brush, &path);
}

void SyncChrome(HWND hwnd) {
    SyncDpi(hwnd);
    RECT rc{};
    GetClientRect(hwnd, &rc);
    g_chrome = LayoutDesignPrinciplesChrome(PxToDip(static_cast<int>(rc.right), g_dpi),
                                           PxToDip(static_cast<int>(rc.bottom), g_dpi));
}

void UpdateScroll(HWND hwnd) {
    SyncChrome(hwnd);
    const auto layout = LayoutEpilogue(g_chrome.page.w, g_chrome.page.h);
    g_scroll = design_principles::ClampPrincipleScroll(g_scroll, layout.content_h, g_chrome.page.h);
    SCROLLINFO si{sizeof(si), SIF_RANGE | SIF_PAGE | SIF_POS};
    si.nMin = 0;
    si.nMax = (std::max)(0, layout.content_h);
    si.nPage = static_cast<UINT>((std::max)(1, g_chrome.page.h));
    si.nPos = g_scroll;
    SetScrollInfo(hwnd, SB_VERT, &si, TRUE);
}

void PaintWindow(HWND hwnd, HDC hdc) {
    RECT client{};
    GetClientRect(hwnd, &client);
    const int pw = (std::max)(1, static_cast<int>(client.right));
    const int ph = (std::max)(1, static_cast<int>(client.bottom));
    SyncChrome(hwnd);
    const auto layout = LayoutEpilogue(g_chrome.page.w, g_chrome.page.h);
    g_scroll = design_principles::ClampPrincipleScroll(g_scroll, layout.content_h, g_chrome.page.h);

    HDC mem = CreateCompatibleDC(hdc);
    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = pw;
    bmi.bmiHeader.biHeight = -ph;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;
    void* bits = nullptr;
    HBITMAP bmp = CreateDIBSection(hdc, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!bmp) {
        bmp = CreateCompatibleBitmap(hdc, pw, ph);
    }
    HGDIOBJ old = SelectObject(mem, bmp);
    nara::dwrite_text::ResetQueue();
    {
        Gdiplus::Graphics g(mem);
        g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
        g.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHalf);
        g.ScaleTransform(g_dpi, g_dpi);
        g.Clear(Argb(kWindowBgArgb));
        Gdiplus::LinearGradientBrush accent(Gdiplus::Point(0, 0), Gdiplus::Point(pw, 0), Argb(kAccentBar0Argb),
                                            Argb(kAccentBar1Argb));
        g.FillRectangle(&accent, g_chrome.accent.x, g_chrome.accent.y, g_chrome.accent.w, g_chrome.accent.h);
        Gdiplus::SolidBrush paper(Argb(kCardFillArgb));
        g.FillRectangle(&paper, g_chrome.card.x, g_chrome.card.y, g_chrome.card.w, g_chrome.card.h);
        g.FillRectangle(&paper, g_chrome.page.x, g_chrome.page.y, g_chrome.page.w, g_chrome.page.h);

        nara::dwrite_text::SetPlotClip(static_cast<float>(g_chrome.page.x), static_cast<float>(g_chrome.page.y),
                                       static_cast<float>(g_chrome.page.w), static_cast<float>(g_chrome.page.h));
        const int ox = g_chrome.page.x;
        const int oy = g_chrome.page.y - g_scroll;
        for (const auto& line : layout.lines) {
            const int y = oy + line.y;
            const int bg_y = oy + line.bg_y;
            if (line.kind == EpilogueKind::Quote && line.bg_w > 0) {
                FillRoundRect(g,
                              Gdiplus::RectF(static_cast<Gdiplus::REAL>(ox + line.bg_x),
                                             static_cast<Gdiplus::REAL>(bg_y),
                                             static_cast<Gdiplus::REAL>(line.bg_w),
                                             static_cast<Gdiplus::REAL>(line.bg_h)),
                              static_cast<float>(kHoverRadius), kEpilogueQuoteFillArgb);
                Gdiplus::SolidBrush bar(Argb(kEpilogueAccentArgb));
                g.FillRectangle(&bar, ox + line.bg_x, bg_y, 3, line.bg_h);
            } else if (line.kind == EpilogueKind::Lead && line.bg_w > 0) {
                Gdiplus::SolidBrush bar(Argb(kEpilogueAccentArgb));
                g.FillRectangle(&bar, ox + line.bg_x, bg_y, line.bg_w, line.bg_h);
            } else if (line.kind == EpilogueKind::Rule && line.bg_w > 0) {
                Gdiplus::SolidBrush rule(Argb(line.argb != 0 ? line.argb : 0xFFD9DEE6u));
                g.FillRectangle(&rule, ox + line.bg_x, oy + line.bg_y, line.bg_w, (std::max)(1, line.bg_h));
            }
            if (line.text.empty()) {
                continue;
            }
            if (y + line.h < g_chrome.page.y || y > g_chrome.page.y + g_chrome.page.h) {
                continue;
            }
            nara::dwrite_text::QueueBox(static_cast<float>(ox + line.x), static_cast<float>(y),
                                        static_cast<float>(line.w), static_cast<float>(line.h), line.text.c_str(),
                                        line.size, line.argb != 0 ? line.argb : kTextArgb, nara::dwrite_text::Align::Near,
                                        nara::dwrite_text::Align::Near, line.bold, true, true);
        }
        g.Flush();
    }
    nara::dwrite_text::Flush(mem, pw, ph, g_dpi);
    BitBlt(hdc, 0, 0, pw, ph, mem, 0, 0, SRCCOPY);
    SelectObject(mem, old);
    DeleteObject(bmp);
    DeleteDC(mem);
}

LRESULT CALLBACK PrinciplesProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_PAINT: {
        PAINTSTRUCT ps{};
        HDC hdc = BeginPaint(hwnd, &ps);
        PaintWindow(hwnd, hdc);
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_ERASEBKGND:
        return 1;
    case WM_SIZE:
        UpdateScroll(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_GETMINMAXINFO: {
        SyncDpi(hwnd);
        auto* info = reinterpret_cast<MINMAXINFO*>(lParam);
        info->ptMinTrackSize.x = ToPx(kPrincipleMinW);
        info->ptMinTrackSize.y = ToPx(kPrincipleMinH);
        return 0;
    }
    case WM_DPICHANGED: {
        const auto* rec = reinterpret_cast<const RECT*>(lParam);
        SetWindowPos(hwnd, nullptr, rec->left, rec->top, rec->right - rec->left, rec->bottom - rec->top,
                     SWP_NOZORDER | SWP_NOACTIVATE);
        UpdateScroll(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_VSCROLL: {
        SCROLLINFO si{sizeof(si), SIF_ALL};
        GetScrollInfo(hwnd, SB_VERT, &si);
        int next = si.nPos;
        switch (LOWORD(wParam)) {
        case SB_LINEUP:
            next -= 48;
            break;
        case SB_LINEDOWN:
            next += 48;
            break;
        case SB_PAGEUP:
            next -= static_cast<int>(si.nPage);
            break;
        case SB_PAGEDOWN:
            next += static_cast<int>(si.nPage);
            break;
        case SB_THUMBTRACK:
        case SB_THUMBPOSITION:
            next = si.nTrackPos;
            break;
        default:
            break;
        }
        g_scroll = design_principles::ClampPrincipleScroll(next, si.nMax, static_cast<int>(si.nPage));
        UpdateScroll(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    }
    case WM_MOUSEWHEEL:
        g_scroll += design_principles::PrincipleScrollDelta(GET_WHEEL_DELTA_WPARAM(wParam));
        UpdateScroll(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_DESTROY:
        g_hwnd = nullptr;
        return 0;
    default:
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}

}  // namespace

void OpenDesignPrinciplesWindow(HINSTANCE instance, HWND owner) {
    static bool registered = false;
    if (!registered) {
        WNDCLASSW wc{};
        wc.lpfnWndProc = PrinciplesProc;
        wc.hInstance = instance;
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
        wc.lpszClassName = kDesignPrinciplesClass;
        wc.style = CS_HREDRAW | CS_VREDRAW;
        RegisterClassW(&wc);
        registered = true;
    }
    if (g_hwnd && IsWindow(g_hwnd)) {
        ShowWindow(g_hwnd, SW_SHOW);
        SetForegroundWindow(g_hwnd);
        return;
    }
    g_scroll = 0;
    g_hwnd = CreateWindowExW(0, kDesignPrinciplesClass, kDesignPrinciplesTitle,
                             WS_OVERLAPPEDWINDOW | WS_VISIBLE | WS_VSCROLL | WS_CLIPCHILDREN, CW_USEDEFAULT,
                             CW_USEDEFAULT, 900, 1000, owner, nullptr, instance, nullptr);
    RECT work{};
    SystemParametersInfoW(SPI_GETWORKAREA, 0, &work, 0);
    const int w = (std::min)(960, static_cast<int>(work.right - work.left - 40));
    const int h = (std::min)(1040, static_cast<int>(work.bottom - work.top - 40));
    const int x = work.left + (work.right - work.left - w) / 2;
    const int y = work.top + (work.bottom - work.top - h) / 2;
    SetWindowPos(g_hwnd, HWND_TOP, x, y, w, h, SWP_SHOWWINDOW);
    UpdateScroll(g_hwnd);
    InvalidateRect(g_hwnd, nullptr, FALSE);
}

bool DesignPrinciplesWindowIsOpen() { return g_hwnd != nullptr && IsWindow(g_hwnd); }

}  // namespace nara
