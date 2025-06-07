using System;
using System.Runtime.InteropServices;
using Microsoft.MixedReality.WebRTC;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using Video;
using System.IO;
namespace Client.Helpers
{
    public class VideoHelper
    {
        //public BitmapSource ConvertI420FrameToBitmap(Microsoft.MixedReality.WebRTC.I420AVideoFrame frame)
        //{
        //    int width = (int)frame.width;
        //    int height = (int)frame.height;
        //    byte[] rgbData = null;
        //    if (VideoProcessor.ConvertI420AToBGRA(
        //        frame.dataY, frame.strideY,
        //        frame.dataU, frame.strideU,
        //        frame.dataV, frame.strideV,
        //        frame.dataA, frame.strideA,
        //        width, height,
        //        ref rgbData))
        //    {
        //        int stride = width * 3; // RGB24
        //        return BitmapSource.Create(
        //            width, height,
        //            96, 96,
        //            PixelFormats.Rgb24, // Use Rgb24 since it's 3 bytes per pixel
        //            null,
        //            rgbData,
        //            stride);
        //    }

        //    return null;
        //}
        public void SaveBitmapAutoFilename(BitmapSource bitmap)
        {
            string folderName = "Screen";
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), folderName);

            // Create folder if not exists
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Generate file name with timestamp
            string fileName = $"frame_{DateTime.Now:yyyyMMdd_HHmmss_fff}.bmp";

            string filePath = Path.Combine(folderPath, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(fileStream);
            }
        }
    }
}
