#pragma once

using namespace System;
using namespace System::Diagnostics;
using namespace System::IO;

public ref class FFmpegStreamer
{
private:
    Process^ ffmpegProcess;
    Stream^ ffmpegInput;
    Stream^ ffmpegOutput;

public:
    FFmpegStreamer(int width, int height, int fps);
    array<Byte>^ EncodeFrame(array<Byte>^ frameData);
    void Close();
};
