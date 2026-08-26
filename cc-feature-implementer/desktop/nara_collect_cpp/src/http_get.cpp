#include "nara/http_get.h"

#include <windows.h>
#include <winhttp.h>

#include <stdexcept>
#include <vector>

#pragma comment(lib, "winhttp.lib")

namespace nara {
namespace {

std::wstring Utf8ToWide(const std::string& text) {
    if (text.empty()) {
        return {};
    }
    const int size = MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, nullptr, 0);
    std::wstring out(size - 1, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, out.data(), size);
    return out;
}

}  // namespace

std::string HttpGet(const std::string& host, const std::string& path_and_query) {
    HINTERNET session = WinHttpOpen(
        L"nara-award-collect/1.0",
        WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
        WINHTTP_NO_PROXY_NAME,
        WINHTTP_NO_PROXY_BYPASS,
        0);
    if (!session) {
        throw std::runtime_error("WinHttpOpen failed");
    }
    DWORD timeout = 20000;
    WinHttpSetTimeouts(session, timeout, timeout, timeout, timeout);

    const auto whost = Utf8ToWide(host);
    HINTERNET connect = WinHttpConnect(session, whost.c_str(), INTERNET_DEFAULT_HTTPS_PORT, 0);
    if (!connect) {
        WinHttpCloseHandle(session);
        throw std::runtime_error("WinHttpConnect failed");
    }

    const auto wpath = Utf8ToWide(path_and_query);
    HINTERNET request = WinHttpOpenRequest(
        connect,
        L"GET",
        wpath.c_str(),
        nullptr,
        WINHTTP_NO_REFERER,
        WINHTTP_DEFAULT_ACCEPT_TYPES,
        WINHTTP_FLAG_SECURE);
    if (!request) {
        WinHttpCloseHandle(connect);
        WinHttpCloseHandle(session);
        throw std::runtime_error("WinHttpOpenRequest failed");
    }

    const BOOL sent = WinHttpSendRequest(
        request,
        WINHTTP_NO_ADDITIONAL_HEADERS,
        0,
        WINHTTP_NO_REQUEST_DATA,
        0,
        0,
        0);
    if (!sent || !WinHttpReceiveResponse(request, nullptr)) {
        WinHttpCloseHandle(request);
        WinHttpCloseHandle(connect);
        WinHttpCloseHandle(session);
        throw std::runtime_error("WinHttp request failed");
    }

    std::string body;
    DWORD available = 0;
    while (WinHttpQueryDataAvailable(request, &available) && available > 0) {
        std::vector<char> buffer(available);
        DWORD read = 0;
        if (!WinHttpReadData(request, buffer.data(), available, &read)) {
            break;
        }
        body.append(buffer.data(), read);
    }

    WinHttpCloseHandle(request);
    WinHttpCloseHandle(connect);
    WinHttpCloseHandle(session);
    return body;
}

}  // namespace nara
