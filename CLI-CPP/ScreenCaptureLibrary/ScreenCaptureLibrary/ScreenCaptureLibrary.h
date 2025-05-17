#pragma once
#include "DXGICaptureCore.h"
#include "TcpClientWrapper.h"
#include "ScreenCapture.h"
using namespace System;
using namespace System::Drawing;
using namespace System::Threading;

namespace ScreenCaptureLibrary {
    public ref class ScreenCapture {
    public:
        ScreenCapture();
        ~ScreenCapture();
        !ScreenCapture();

        void StartStreaming(int fps, String^ IP);
        void StopStreaming();

        property bool IsStreaming {
            bool get() { return isRunning;}
        }

    private:
        void StreamLoop(Object^ Obj);
        Bitmap^ ConvertToBitmap32bpp(BYTE* data, int width, int height);
        Thread^ streamThread;
        bool isRunning;
        TcpClientWrapper^ tcpClientWrapper;
        void* nativeCapture; // Native DXGI core (pointer)
    };
}
