#include<pch.h>
#include "FFmpegDecoderCLI.h"

extern "C" {
#include <libavcodec/avcodec.h>
#include <libavutil/imgutils.h>
#include <libswscale/swscale.h>
}

using namespace System::Runtime::InteropServices;
using namespace System::Drawing::Imaging;

FFmpegDecoderCLI::FFmpegDecoderCLI() {
    InitDecoder();
}

void FFmpegDecoderCLI::InitDecoder() {
    codec = (void*)avcodec_find_decoder(AV_CODEC_ID_H264);
    if (!codec) throw gcnew Exception("H.264 decoder not found");

    codecCtx = avcodec_alloc_context3((AVCodec*)codec);
    parserCtx = av_parser_init(AV_CODEC_ID_H264);
    frame = av_frame_alloc();
    pkt = av_packet_alloc();

    if (avcodec_open2((AVCodecContext*)codecCtx, (AVCodec*)codec, nullptr) < 0)
        throw gcnew Exception("Failed to open codec");
}

Bitmap^ FFmpegDecoderCLI::Decode(array<Byte>^ data, int length) {
    pin_ptr<Byte> p = &data[0];
    uint8_t* inputData = p;
    int dataSize = length;

    while (dataSize > 0) {
        int ret = av_parser_parse2((AVCodecParserContext*)parserCtx,
            (AVCodecContext*)codecCtx,
            &((AVPacket*)pkt)->data,
            &((AVPacket*)pkt)->size,
            inputData,
            dataSize,
            AV_NOPTS_VALUE,
            AV_NOPTS_VALUE,
            0);

        inputData += ret;
        dataSize -= ret;

        if (((AVPacket*)pkt)->size > 0) {
            if (avcodec_send_packet((AVCodecContext*)codecCtx, (AVPacket*)pkt) < 0)
                continue;

            while (avcodec_receive_frame((AVCodecContext*)codecCtx, (AVFrame*)frame) == 0) {
                width = ((AVFrame*)frame)->width;
                height = ((AVFrame*)frame)->height;

                // Convert frame to BGRA
                SwsContext* swsCtx = sws_getContext(
                    width, height, (AVPixelFormat)((AVFrame*)frame)->format,
                    width, height, AV_PIX_FMT_BGRA,
                    SWS_BILINEAR, nullptr, nullptr, nullptr);

                array<Byte>^ managedData = gcnew array<Byte>(width * height * 4);
                pin_ptr<Byte> dstData = &managedData[0];

                uint8_t* dst[] = { dstData };
                int linesize[] = { width * 4 };

                sws_scale(swsCtx, ((AVFrame*)frame)->data, ((AVFrame*)frame)->linesize, 0, height, dst, linesize);
                sws_freeContext(swsCtx);

                Bitmap^ bmp = gcnew Bitmap(width, height, PixelFormat::Format32bppArgb);
                BitmapData^ bmpData = bmp->LockBits(
                    System::Drawing::Rectangle(0, 0, width, height),
                    ImageLockMode::WriteOnly,
                    bmp->PixelFormat);

                Marshal::Copy(managedData, 0, bmpData->Scan0, managedData->Length);
                bmp->UnlockBits(bmpData);

                return bmp;
            }
        }
    }

    return nullptr;
}

void FFmpegDecoderCLI::Cleanup() {
    if (codecCtx) {
        AVCodecContext* ctx = (AVCodecContext*)codecCtx;
        avcodec_free_context(&ctx);
        codecCtx = nullptr;
    }

    if (parserCtx) {
        av_parser_close((AVCodecParserContext*)parserCtx);
        parserCtx = nullptr;
    }

    if (frame) {
        AVFrame* f = (AVFrame*)frame;
        av_frame_free(&f);
        frame = nullptr;
    }

    if (pkt) {
        AVPacket* p = (AVPacket*)pkt;
        av_packet_free(&p);
        pkt = nullptr;
    }
}


FFmpegDecoderCLI::~FFmpegDecoderCLI() {
    Cleanup();
}
FFmpegDecoderCLI::!FFmpegDecoderCLI() {
    Cleanup(); // nondeterministic
}
