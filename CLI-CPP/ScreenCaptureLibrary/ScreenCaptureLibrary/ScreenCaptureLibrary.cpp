#include "pch.h"
#include "ScreenCaptureLibrary.h"
using namespace System::Drawing::Imaging;
using namespace System::Diagnostics;
using namespace System::IO;
using namespace System::Runtime::InteropServices;

namespace ScreenCaptureLibrary {
    ScreenCapture::ScreenCapture() {
        nativeCapture = DXGI_Create();
        if (!DXGI_Initialize(nativeCapture)) {
            delete nativeCapture;
            throw gcnew Exception("Failed to initialize DXGI.");
        }
    }

    ScreenCapture::~ScreenCapture() {
        StopStreaming();
        delete nativeCapture;
    }

    ScreenCapture::!ScreenCapture() {
        StopStreaming();
        delete nativeCapture;
    }

    void ScreenCapture::StartStreaming(int fps, String ^IP) {
        if (isRunning) return;
        cli::array<Object^>^ args = gcnew cli::array<Object^>(2);
        args[0] = fps;
        args[1] = IP;
        isRunning = true;
        streamThread = gcnew Thread(gcnew ParameterizedThreadStart(this, &ScreenCapture::StreamLoop));
        streamThread->IsBackground = true;
        streamThread->Start(fps);
    }

    void ScreenCapture::StopStreaming() {
        isRunning = false;
        if (streamThread && streamThread->IsAlive) {
            streamThread->Join();
        }
    }

    void ScreenCapture::StreamLoop(Object^ Obj) {
        cli::array<Object^>^ args = safe_cast<cli::array<Object^>^>(Obj);
        int fps = safe_cast<int>(args[0]);
        String^ IP = safe_cast<String^>(args[1]);
        int interval = 1000 / fps;
        BYTE* buffer = nullptr;

        TcpClientWrapper^ tcpSender = gcnew TcpClientWrapper(IP, 12345);
        MemoryStream^ ms = gcnew MemoryStream(); // tái sử dụng
        Stopwatch^ sw = gcnew Stopwatch();

        while (isRunning) {
            sw->Restart();
            unsigned int w = 0, h = 0, stride = 0;

            if (DXGI_CaptureFrame(nativeCapture, &buffer, &w, &h, &stride)) {
                try {
                    Bitmap^ bmp = ConvertToBitmap32bpp(buffer, w, h);

                    // Reset stream và save lại ảnh PNG
                    ms->SetLength(0);
                    bmp->Save(ms, ImageFormat::Png);

                    // Gửi độ dài trước, sau đó gửi dữ liệu ảnh PNG
                    array<Byte>^ pngData = ms->ToArray();

                    tcpSender->Send(pngData);      // Gửi dữ liệu ảnh

                    delete bmp; // Giải phóng
                }
                catch (Exception^ ex) {
                    Console::WriteLine("Error: " + ex->Message);
                }

                delete[] buffer;
            }

            sw->Stop();
            int elapsed = static_cast<int>(sw->ElapsedMilliseconds);
            int delay = interval - elapsed;
            if (delay > 0) Thread::Sleep(delay);
        }
    }

    Bitmap^ ScreenCapture::ConvertToBitmap32bpp(BYTE* data, int width, int height)
    {
        Bitmap^ bmp = gcnew Bitmap(width, height, PixelFormat::Format32bppArgb);
        BitmapData^ bmpData = bmp->LockBits(
            System::Drawing::Rectangle(0, 0, width, height),
            ImageLockMode::WriteOnly,
            bmp->PixelFormat);

        for (int y = 0; y < height; y++) {
            BYTE* srcLine = data + y * width * 4; // BGRA
            BYTE* dstLine = (BYTE*)bmpData->Scan0.ToPointer() + y * bmpData->Stride;

            for (int x = 0; x < width; x++) {
                dstLine[x * 4 + 0] = srcLine[x * 4 + 0]; // Blue
                dstLine[x * 4 + 1] = srcLine[x * 4 + 1]; // Green
                dstLine[x * 4 + 2] = srcLine[x * 4 + 2]; // Red
                dstLine[x * 4 + 3] = 255;                // Alpha (hoặc lấy srcLine[x * 4 + 3] nếu muốn giữ alpha gốc)
            }
        }

        bmp->UnlockBits(bmpData);
        return bmp;
    }

}