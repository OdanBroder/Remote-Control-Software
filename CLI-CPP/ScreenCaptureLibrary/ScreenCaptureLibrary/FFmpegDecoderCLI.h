#pragma once

using namespace System;
using namespace System::Drawing;

public ref class FFmpegDecoderCLI
{
public:
    FFmpegDecoderCLI();
    Bitmap^ Decode(array<Byte>^ data, int length);
    ~FFmpegDecoderCLI();         // C++ destructor (calls Cleanup)
protected:
    !FFmpegDecoderCLI();         // Finalizer (GC gọi nếu user không dispose)


private:
    void InitDecoder();
    void Cleanup();

    // Native FFmpeg decoder context
    void* codecCtx;
    void* parserCtx;
    void* codec;
    void* frame;
    void* pkt;
    int width;
    int height;
};
