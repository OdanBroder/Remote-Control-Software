#include <libyuv.h>  // Bao gồm thư viện libyuv
#include <cstring>
#include <Windows.h>
using namespace System;
using namespace System::Drawing;
using namespace System::Drawing::Imaging;
using namespace System::Runtime::InteropServices;
namespace Video
{
    public ref class VideoProcessor
    {
    public:
        // Phương thức chuyển đổi từ I420A sang mảng RGB
        
        static bool ConvertI420AToRGB(IntPtr dataY, int strideY,
            IntPtr dataU, int strideU,
            IntPtr dataV, int strideV,
            IntPtr dataA, int strideA,
            int width, int height,
            array<Byte>^% rgbData);

        /*
        array<Byte>^ ConvertI420AToRGB(array<Byte>^ yData, array<Byte>^ uData, array<Byte>^ vData, unsigned int width, unsigned int height);
        void ConvertI420AToBGRA(BYTE* yPlane, BYTE* uPlane, BYTE* vPlane, BYTE* aPlane,
            int width, int height, BYTE** outBgra);
        
        static array<System::Byte>^ ConvertI420AToABGR(
            System::IntPtr dataY, int strideY,
            System::IntPtr dataU, int strideU,
            System::IntPtr dataV, int strideV,
            System::IntPtr dataA, int strideA,
            unsigned int width, unsigned int height);
        */
    };
}
