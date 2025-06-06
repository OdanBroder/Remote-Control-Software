using System;
using System.Runtime.InteropServices;
using Microsoft.MixedReality.WebRTC;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;

namespace Client.Services
{
    public class VideoProcessor
    {
        [DllImport("libyuv.dll")]
        private static extern int I420ToARGB(
            IntPtr src_y, int src_stride_y,
            IntPtr src_u, int src_stride_u,
            IntPtr src_v, int src_stride_v,
            IntPtr dst_argb, int dst_stride_argb,
            int width, int height);

        public BitmapSource ConvertI420AToRGB(I420AVideoFrame frame)
        {
            try
            {
                int width = (int)frame.width;
                int height = (int)frame.height;
                int stride = width * 4; // ARGB = 4 bytes per pixel
                byte[] rgbBuffer = new byte[stride * height];

                // Pin the buffer
                GCHandle handle = GCHandle.Alloc(rgbBuffer, GCHandleType.Pinned);
                IntPtr rgbPtr = handle.AddrOfPinnedObject();

                // Convert I420A to ARGB
                int result = I420ToARGB(
                    frame.dataY, frame.strideY,
                    frame.dataU, frame.strideU,
                    frame.dataV, frame.strideV,
                    rgbPtr, stride,
                    width, height);

                if (result != 0)
                {
                    throw new Exception($"I420ToARGB conversion failed with error code: {result}");
                }

                // Create BitmapSource from the RGB buffer
                var bitmap = BitmapSource.Create(
                    width, height,
                    96, 96, // DPI
                    PixelFormats.Bgra32,
                    null,
                    rgbBuffer,
                    stride);

                // Free the pinned buffer
                handle.Free();

                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting frame: {ex.Message}");
                return null;
            }
        }
    }
} 