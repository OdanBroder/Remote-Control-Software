#include "pch.h"
#include "VideoProcessor.h"
namespace Video
{
    bool VideoProcessor::ConvertI420AToRGB(
        IntPtr dataY, int strideY,
        IntPtr dataU, int strideU,
        IntPtr dataV, int strideV,
        IntPtr dataA, int strideA,
        int width, int height,
        array<Byte>^% rgbData)
    {
        if (width <= 0 || height <= 0 || dataY == IntPtr::Zero || dataU == IntPtr::Zero || dataV == IntPtr::Zero)
            return false;

        // RGB24: 3 bytes per pixel (R, G, B)
        int strideRGB = width * 3;
        rgbData = gcnew array<Byte>(strideRGB * height);
        pin_ptr<Byte> dstPtr = &rgbData[0];
        uint8_t* dst = dstPtr;

        uint8_t* srcY = static_cast<uint8_t*>(dataY.ToPointer());
        uint8_t* srcU = static_cast<uint8_t*>(dataU.ToPointer());
        uint8_t* srcV = static_cast<uint8_t*>(dataV.ToPointer());

        // Chuyển đổi I420 sang RGB24 bằng libyuv
        int ret = libyuv::I420ToRGB24(
            srcY, strideY,
            srcU, strideU,
            srcV, strideV,
            dst, strideRGB,
            width, height);
        return (ret == 0);
    }
    /*
    array<Byte>^ VideoProcessor::ConvertI420AToRGB(array<Byte>^ yData, array<Byte>^ uData, array<Byte>^ vData, unsigned int width, unsigned int height)
    {
        // Kiểm tra dữ liệu đầu vào
        if (yData == nullptr || uData == nullptr || vData == nullptr )
        {
            throw gcnew ArgumentNullException("Input YUV data cannot be null.");
        }

        // Chuyển dữ liệu từ .NET sang pin_ptr (mảng gốc)
        pin_ptr<Byte> yPtr = &yData[0];
        pin_ptr<Byte> uPtr = &uData[0];
        pin_ptr<Byte> vPtr = &vData[0];
        //pin_ptr<Byte> aPtr = &aData[0];

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

    /*void VideoProcessor::ConvertI420AToBGRA(BYTE* yPlane, BYTE* uPlane, BYTE* vPlane, BYTE* aPlane,
        int width, int height, BYTE** outBgra)
    {
        int size = width * height * 4;
        *outBgra = new BYTE[size];

        libyuv::I420AlphaToABGR(
            yPlane, width,
            uPlane, width / 2,
            vPlane, width / 2,
            aPlane, width,
            *outBgra, width * 4,
            width, height,
            true  // enable alpha
        );
    }

    array<Byte>^ VideoProcessor::ConvertI420AToABGR(
        System::IntPtr dataY, int strideY,
        System::IntPtr dataU, int strideU,
        System::IntPtr dataV, int strideV,
        System::IntPtr dataA, int strideA,
        unsigned int width, unsigned int height)
    {
        int size = width * height * 4;
        array<System::Byte>^ output = gcnew array<System::Byte>(size);
        pin_ptr<System::Byte> outputPtr = &output[0];

        // Gọi libyuv
        libyuv::I420AlphaToABGR(
            (const uint8_t*)dataY.ToPointer(), strideY,
            (const uint8_t*)dataU.ToPointer(), strideU,
            (const uint8_t*)dataV.ToPointer(), strideV,
            (const uint8_t*)dataA.ToPointer(), strideA,
            outputPtr, width * 4,
            width, height,
            true // enable alpha
        );

        return output;
    }
    */
}