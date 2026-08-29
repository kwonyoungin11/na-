#pragma once

#include <cstdint>
#include <string>

namespace nara {
namespace dwrite_text {

enum class Align { Near, Center, Far };

struct Job {
    float x = 0;
    float y = 0;
    float w = 0;
    float h = 0;
    std::wstring text;
    float size = 12.0f;
    uint32_t argb = 0xFF111111u;
    uint32_t outline_argb = 0;
    float outline_dip = 0;
    Align h_align = Align::Near;
    Align v_align = Align::Near;
    bool bold = false;
    bool ellipsis = false;
    bool clip = false;
    bool clip_plot = false;
};

inline void ArgbToRgba(uint32_t argb, float out[4]) {
    out[0] = static_cast<float>((argb >> 16) & 0xFFu) / 255.0f;
    out[1] = static_cast<float>((argb >> 8) & 0xFFu) / 255.0f;
    out[2] = static_cast<float>(argb & 0xFFu) / 255.0f;
    out[3] = static_cast<float>((argb >> 24) & 0xFFu) / 255.0f;
}

inline int OutlineNeighborCount() { return 8; }

void ResetQueue();
void Queue(Job job);
void SetPlotClip(float x, float y, float w, float h);

inline void QueueBox(float x, float y, float w, float h, const wchar_t* text, float size, uint32_t argb, Align ha,
                     Align va, bool bold, bool ellipsis = false, bool clip = false, uint32_t outline_argb = 0,
                     float outline_dip = 0, bool clip_plot = false) {
    if (text == nullptr || text[0] == 0) {
        return;
    }
    Job job;
    job.x = x;
    job.y = y;
    job.w = w;
    job.h = h;
    job.text = text;
    job.size = size;
    job.argb = argb;
    job.outline_argb = outline_argb;
    job.outline_dip = outline_dip;
    job.h_align = ha;
    job.v_align = va;
    job.bold = bold;
    job.ellipsis = ellipsis;
    job.clip = clip;
    job.clip_plot = clip_plot;
    Queue(std::move(job));
}
void Flush(void* hdc, int pixel_w, int pixel_h, float dpi_scale, int origin_x = 0, int origin_y = 0);

}  // namespace dwrite_text
}  // namespace nara
