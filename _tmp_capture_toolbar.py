import ctypes
import sys
import time
from ctypes import wintypes

user32 = ctypes.WinDLL("user32", use_last_error=True)
gdi32 = ctypes.WinDLL("gdi32", use_last_error=True)

PW_RENDERFULLCONTENT = 2
SRCCOPY = 0x00CC0020
DIB_RGB_COLORS = 0
BI_RGB = 0

class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", wintypes.DWORD),
        ("biWidth", ctypes.c_long),
        ("biHeight", ctypes.c_long),
        ("biPlanes", wintypes.WORD),
        ("biBitCount", wintypes.WORD),
        ("biCompression", wintypes.DWORD),
        ("biSizeImage", wintypes.DWORD),
        ("biXPelsPerMeter", ctypes.c_long),
        ("biYPelsPerMeter", ctypes.c_long),
        ("biClrUsed", wintypes.DWORD),
        ("biClrImportant", wintypes.DWORD),
    ]

class BITMAPINFO(ctypes.Structure):
    _fields_ = [("bmiHeader", BITMAPINFOHEADER), ("bmiColors", wintypes.DWORD * 3)]

user32.FindWindowW.restype = wintypes.HWND
user32.FindWindowW.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR]
user32.GetClientRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
user32.PrintWindow.argtypes = [wintypes.HWND, wintypes.HDC, wintypes.UINT]
user32.GetDC.argtypes = [wintypes.HWND]
user32.ReleaseDC.argtypes = [wintypes.HWND, wintypes.HDC]
user32.SendMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
user32.FindWindowExW.argtypes = [wintypes.HWND, wintypes.HWND, wintypes.LPCWSTR, wintypes.LPCWSTR]
user32.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
user32.IsWindowVisible.argtypes = [wintypes.HWND]


def wait_hwnd(title=None, class_name=None, timeout=12.0):
    deadline = time.time() + timeout
    while time.time() < deadline:
        hwnd = user32.FindWindowW(class_name, title)
        if hwnd:
            return hwnd
        time.sleep(0.2)
    return 0


def capture(hwnd, path):
    rc = wintypes.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(rc))
    w = rc.right - rc.left
    h = rc.bottom - rc.top
    if w <= 0 or h <= 0:
        raise SystemExit(f"empty client {w}x{h}")
    hdc_win = user32.GetDC(hwnd)
    hdc_mem = gdi32.CreateCompatibleDC(hdc_win)
    hbmp = gdi32.CreateCompatibleBitmap(hdc_win, w, h)
    old = gdi32.SelectObject(hdc_mem, hbmp)
    ok = user32.PrintWindow(hwnd, hdc_mem, PW_RENDERFULLCONTENT)
    if not ok:
        ok = user32.PrintWindow(hwnd, hdc_mem, 0)
    bmi = BITMAPINFO()
    bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bmi.bmiHeader.biWidth = w
    bmi.bmiHeader.biHeight = -h
    bmi.bmiHeader.biPlanes = 1
    bmi.bmiHeader.biBitCount = 24
    bmi.bmiHeader.biCompression = BI_RGB
    row = (w * 3 + 3) & ~3
    buf = (ctypes.c_ubyte * (row * h))()
    gdi32.GetDIBits(hdc_mem, hbmp, 0, h, buf, ctypes.byref(bmi), DIB_RGB_COLORS)
    gdi32.SelectObject(hdc_mem, old)
    gdi32.DeleteObject(hbmp)
    gdi32.DeleteDC(hdc_mem)
    user32.ReleaseDC(hwnd, hdc_win)
    with open(path, "wb") as f:
        # PPM is enough for the image tool
        f.write(f"P6\n{w} {h}\n255\n".encode("ascii"))
        for y in range(h):
            off = y * row
            for x in range(w):
                b = buf[off + x * 3]
                g = buf[off + x * 3 + 1]
                r = buf[off + x * 3 + 2]
                f.write(bytes((r, g, b)))
    print(f"saved {path} {w}x{h} print={ok}")


def click_caption(parent, caption):
    child = 0
    buf = ctypes.create_unicode_buffer(64)
    while True:
        child = user32.FindWindowExW(parent, child, "Button", None)
        if not child:
            break
        user32.GetWindowTextW(child, buf, 64)
        if buf.value == caption:
            BM_CLICK = 0x00F5
            user32.SendMessageW(child, BM_CLICK, 0, 0)
            print(f"clicked {caption}")
            return True
    print(f"button not found: {caption}")
    return False


def main():
    hwnd = wait_hwnd(class_name="NaraDesktopMain")
    if not hwnd:
        raise SystemExit("main window not found")
    time.sleep(1.2)
    capture(hwnd, r"A:\c\_tmp_cpp_toolbar.ppm")
    click_caption(hwnd, "차트 화면")
    time.sleep(1.5)
    chart = wait_hwnd(title="동국문화유산연구원 낙찰분석프로그램 - 분석 창")
    if not chart:
        raise SystemExit("chart window not found")
    capture(chart, r"A:\c\_tmp_cpp_allbtn.ppm")


if __name__ == "__main__":
    main()
