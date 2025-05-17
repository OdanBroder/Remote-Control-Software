#include "pch.h"
#include "ScreenCapture.h"
#include <libyuv.h> // Thêm thư viện libyuv
#include <iostream> // Để sử dụng std::cout hoặc log
#include <chrono> 
#include <thread>
namespace ScreenCaptureI420A {

    // Constructor
    ScreenCaptureDXGI::ScreenCaptureDXGI() {
        nativeCapture = DXGI_Create();
        if (!DXGI_Initialize(nativeCapture)) {
            delete nativeCapture;
            throw gcnew Exception("Failed to initialize DXGI.");
        }
    }

    // Destructor
    ScreenCaptureDXGI::~ScreenCaptureDXGI() {
        delete nativeCapture;
    }

    // Finalizer
    ScreenCaptureDXGI::!ScreenCaptureDXGI() {
        delete nativeCapture;
    }

    

    void ScreenCaptureDXGI::CaptureLoop() {
        running = true;

        while (running) {
            BYTE* buffer = nullptr;
            unsigned int w = 0, h = 0, stride = 0;

            if (!DXGI_CaptureFrame(nativeCapture, &buffer, &w, &h, &stride)) {
                std::cout << "Failed to capture frame. Skipping..." << std::endl; // Log lỗi
                continue;
            }
            std::cout << "Captured with dxgi: (w=" << w << ", h=" << h << ", stride=" << stride << ")" << std::endl;
            // Chuyển đổi BGRA sang I420A
            BYTE* yPlane = nullptr;
            BYTE* uPlane = nullptr;
            BYTE* vPlane = nullptr;
            BYTE* aPlane = nullptr;

            bool success = ConvertBgraToI420A(buffer, w, h, stride, &yPlane, &uPlane, &vPlane, &aPlane);

            if (!success) {
                std::cout << "Failed to convert BGRA to I420A (w=" << w << ", h=" << h << ", stride=" << stride << ")" << std::endl;
                delete[] buffer; // Giải phóng buffer vì capture thành công nhưng chuyển đổi thất bại
                continue;        // Bỏ qua frame này
            }

            // Gọi delegate nếu đã đăng ký
            if (OnFrameCaptured != nullptr) {
                IntPtr yPtr = IntPtr(yPlane);
                IntPtr uPtr = IntPtr(uPlane);
                IntPtr vPtr = IntPtr(vPlane);
                IntPtr aPtr = IntPtr(aPlane);

                OnFrameCaptured(yPtr, w, h, stride, uPtr, vPtr, aPtr);
            }
            else {
                std::cout << "No OnFrameCaptured delegate registered." << std::endl;
            }

            // Giải phóng bộ nhớ của I420A
            delete[] yPlane;
            delete[] uPlane;
            delete[] vPlane;
            delete[] aPlane;

            // Giải phóng buffer từ DXGI_CaptureFrame
            delete[] buffer;

            // Đợi để giữ ~30fps (tính toán thời gian xử lý)
            std::chrono::milliseconds targetFrameTime(33); // ~30fps
            auto start = std::chrono::high_resolution_clock::now();
            // Tính thời gian còn lại sau khi xử lý
            auto end = std::chrono::high_resolution_clock::now();
            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);
            if (elapsed < targetFrameTime) {
                std::this_thread::sleep_for(targetFrameTime - elapsed);
            }
        }
    }
    bool ScreenCaptureDXGI::ConvertBgraToI420A(BYTE* data, int width, int height, int stride, BYTE** yPlane, BYTE** uPlane, BYTE** vPlane, BYTE** aPlane) {
        // Kiểm tra đầu vào
        if (!data || width <= 0 || height <= 0 || stride < width * 4) {
            return false;
        }

        // Kiểm tra width và height phải chia hết cho 2 (yêu cầu của I420)
        if (width % 2 != 0 || height % 2 != 0) {
            return false;
        }

        // Tính kích thước cho các planes
        int ySize = stride * height;
        int uvSize = (width / 2) * (height / 2);

        // Cấp phát bộ nhớ với kiểm tra lỗi
        *yPlane = new BYTE[ySize];
        *uPlane = new BYTE[uvSize];
        *vPlane = new BYTE[uvSize];
        *aPlane = new BYTE[ySize];

        if (!*yPlane || !*uPlane || !*vPlane || !*aPlane) {
            // Giải phóng bộ nhớ nếu cấp phát thất bại
            delete[] * yPlane;
            delete[] * uPlane;
            delete[] * vPlane;
            delete[] * aPlane;
            *yPlane = *uPlane = *vPlane = *aPlane = nullptr;
            return false;
        }

        // Chuyển đổi từ BGRA sang I420 bằng libyuv
        int ret = libyuv::BGRAToI420(
            data, stride,       // Sử dụng stride thực tế
            *yPlane, stride,     // Y plane
            *uPlane, width / 2, // U plane
            *vPlane, width / 2, // V plane
            width, height
        );

        if (ret != 0) {
            // Giải phóng bộ nhớ nếu chuyển đổi thất bại
            delete[] * yPlane;
            delete[] * uPlane;
            delete[] * vPlane;
            delete[] * aPlane;
            *yPlane = *uPlane = *vPlane = *aPlane = nullptr;
            return false;
        }

        // Trích xuất alpha channel với stride thực tế
        int aIndex = 0;
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int index = y * stride + x * 4; // Tính vị trí dựa trên stride
                (*aPlane)[aIndex++] = data[index + 3]; // Lấy alpha từ BGRA
            }
        }

        return true;
    }
    /*
    // Chuyển đổi BGRA sang I420A
    void ScreenCaptureDXGI::ConvertBgraToI420A(BYTE* data, int width, int height,int stride, BYTE** yPlane, BYTE** uPlane, BYTE** vPlane, BYTE** aPlane) {
        // Tạo bộ nhớ cho các planes Y, U, V, Alpha (I420A)
        int ySize = width * height;
        int uvSize = (width / 2) * (height / 2);

        *yPlane = new BYTE[ySize];
        *uPlane = new BYTE[uvSize];
        *vPlane = new BYTE[uvSize];
        *aPlane = new BYTE[ySize];

        // Sử dụng libyuv để chuyển đổi từ BGRA sang I420
        libyuv::BGRAToI420(
            data, width*4,    // BGRA data và stride (width * 4 vì BGRA có 4 byte mỗi pixel)
            *yPlane, width,     // Y plane
            *uPlane, width / 2, // U plane (mỗi chiều giảm đi một nửa)
            *vPlane, width / 2, // V plane (mỗi chiều giảm đi một nửa)
            width, height       // Kích thước ảnh
        );

        // Lưu giá trị alpha (giữ nguyên alpha từ BGRA)
        int aIndex = 0;
        for (int i = 0; i < ySize; i++) {
            (*aPlane)[aIndex++] = data[i * 4 + 3];  // Lấy giá trị alpha từ BGRA (mỗi pixel có 4 byte)
        }
    }
    */
    /*
    array<Byte>^ ScreenCaptureDXGI::ConvertI420AToRGB(array<Byte>^ yData, array<Byte>^ uData, array<Byte>^ vData, array<Byte>^ aData, unsigned int width, unsigned int height)
    {
        // Kiểm tra dữ liệu đầu vào
        if (yData == nullptr || uData == nullptr || vData == nullptr || aData == nullptr)
        {
            throw gcnew ArgumentNullException("Input YUV data cannot be null.");
        }

        // Chuyển dữ liệu từ .NET sang pin_ptr (mảng gốc)
        pin_ptr<Byte> yPtr = &yData[0];
        pin_ptr<Byte> uPtr = &uData[0];
        pin_ptr<Byte> vPtr = &vData[0];
        pin_ptr<Byte> aPtr = &aData[0];

        // Tạo mảng RGB (3 bytes mỗi pixel)
        int rgbSize = width * height * 3; // Mỗi pixel có 3 byte (RGB)
        array<Byte>^ rgbData = gcnew array<Byte>(rgbSize);
        pin_ptr<Byte> rgbPtr = &rgbData[0];

        // Sử dụng libyuv để chuyển đổi từ I420A sang RGB
        libyuv::I420ToRGB24(
            yPtr, width,
            uPtr, width / 2,
            vPtr, width / 2,
            rgbPtr, width * 3,
            width, height);

        // Trả về mảng RGB cho C#
        return rgbData;
    }
    */
    //Chuyển đổi dữ liệu I420A sang Bitmap (tạo Bitmap từ I420A)
    /*
    Bitmap^ ScreenCaptureDXGI::ConvertI420AToBitmap(array<Byte>^ yData, array<Byte>^ uData, array<Byte>^ vData, array<Byte>^ aData, unsigned int width, unsigned int height) {
        
        pin_ptr<Byte> pY = &yData[0];
        pin_ptr<Byte> pU = &uData[0];
        pin_ptr<Byte> pV = &vData[0];
        pin_ptr<Byte> pA = &aData[0];

        BYTE* yPlane = pY;
        BYTE* uPlane = pU;
        BYTE* vPlane = pV;
        BYTE* aPlane = pA;
        
        // Tạo Bitmap với dữ liệu I420A
        Bitmap^ bmp = gcnew Bitmap(width, height, PixelFormat::Format32bppArgb);
        BitmapData^ bmpData = bmp->LockBits(
            System::Drawing::Rectangle(0, 0, width, height),
            ImageLockMode::WriteOnly,
            bmp->PixelFormat);

        // Cập nhật bitmap từ dữ liệu I420A (Y, U, V, Alpha)
        for (int y = 0; y < height; y++)
        {
            BYTE* srcY = yPlane + y * width;
            BYTE* srcU = uPlane + (y / 2) * (width / 2);
            BYTE* srcV = vPlane + (y / 2) * (width / 2);
            BYTE* srcA = aPlane + y * width;
            BYTE* dstLine = (BYTE*)bmpData->Scan0.ToPointer() + y * bmpData->Stride;

            for (int x = 0; x < width; x++)
            {
                int uvIndex = (y / 2) * (width / 2) + x / 2; // 4:2:0 sampling
                dstLine[x * 4 + 0] = srcY[x];  // Y component
                dstLine[x * 4 + 1] = srcU[uvIndex];  // U component
                dstLine[x * 4 + 2] = srcV[uvIndex];  // V component
                dstLine[x * 4 + 3] = srcA[x];  // Alpha component
            }
        }

        bmp->UnlockBits(bmpData);
        return bmp;
    }
    */
    void ScreenCaptureDXGI::Stop() {
        running = false;
    }
    void ScreenCaptureDXGI::Start() {
        if (running) return;  // Đã chạy rồi thì không chạy nữa

        captureThread = gcnew System::Threading::Thread(
            gcnew System::Threading::ThreadStart(this, &ScreenCaptureDXGI::CaptureLoop)
        );
        captureThread->IsBackground = true;
        captureThread->Start();
    }

}
