#pragma once
#include "DXGICaptureCore.h"
#include <libyuv.h>
using namespace System;
using namespace System::Drawing;
using namespace System::Threading;
using namespace System::Runtime::InteropServices;
using namespace System::Diagnostics;
using namespace System::Drawing::Imaging;
namespace ScreenCaptureI420A{
    // Định nghĩa delegate trong C++/CLI cho I420A
    public delegate void FrameCapturedI420ADelegate(
        IntPtr yPlane,
        int width,
        int height,
        int stride,
        IntPtr uPlane,
        IntPtr vPlane,
        IntPtr aPlane);

    public ref class ScreenCaptureDXGI
    {
    public:
        FrameCapturedI420ADelegate^ OnFrameCaptured;
        System::Threading::Thread^ captureThread;
        void Start();
        void Stop();
        void CaptureLoop();
        bool ConvertBgraToI420A(BYTE* data, int width, int height,int stride, BYTE** yPlane, BYTE** uPlane, BYTE** vPlane, BYTE** aPlane);
        //static array<Byte>^ ConvertI420AToRGB(array<Byte>^ yData, array<Byte>^ uData, array<Byte>^ vData, array<Byte>^ aData, unsigned int width, unsigned int height);
        /*static Bitmap^ ConvertI420AToBitmap(array<Byte>^ yData, array<Byte>^ uData, array<Byte>^ vData, array<Byte>^ aData, unsigned int width, unsigned int height);*/
        !ScreenCaptureDXGI();
        ScreenCaptureDXGI();
        ~ScreenCaptureDXGI();
    private:
        bool running;
        void* nativeCapture;
    };

}