/* Public ABI fields consumed by osu.Framework 2026.303.0 / FFmpeg.AutoGen 4.3.0.1. */
#include <stddef.h>
#include <stdio.h>
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/frame.h>

#define FIELD(type, name) printf("\"%s.%s\":%zu,", #type, #name, offsetof(type, name))

int main(void)
{
    printf("{");
    FIELD(AVFormatContext, pb);
    FIELD(AVFormatContext, flags);
    FIELD(AVFormatContext, streams);
    FIELD(AVFormatContext, duration);
    FIELD(AVFormatContext, iformat);
    FIELD(AVCodecContext, width);
    FIELD(AVCodecContext, height);
    FIELD(AVCodecContext, colorspace);
    FIELD(AVCodecContext, pix_fmt);
    FIELD(AVCodecContext, pkt_timebase);
    FIELD(AVCodecContext, hw_device_ctx);
    FIELD(AVStream, time_base);
    FIELD(AVStream, duration);
    FIELD(AVStream, start_time);
    FIELD(AVStream, index);
    FIELD(AVStream, codecpar);
    FIELD(AVStream, avg_frame_rate);
    FIELD(AVFrame, best_effort_timestamp);
    FIELD(AVFrame, pts);
    FIELD(AVFrame, format);
    FIELD(AVFrame, data);
    FIELD(AVFrame, linesize);
    FIELD(AVFrame, hw_frames_ctx);
    FIELD(AVFrame, width);
    FIELD(AVFrame, height);
    FIELD(AVPacket, buf);
    FIELD(AVPacket, stream_index);
    FIELD(AVIOContext, buffer);
    FIELD(AVCodec, id);
    FIELD(AVCodec, name);
    FIELD(AVCodecHWConfig, device_type);
    FIELD(AVCodecParameters, codec_id);
    FIELD(AVRational, num);
    FIELD(AVRational, den);
    printf("\"sizeof.AVCodecParameters\":%zu}", sizeof(AVCodecParameters));
    return 0;
}
