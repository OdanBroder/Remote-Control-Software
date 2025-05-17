#include "pch.h"
#include "FFmpegStreamer.h"

FFmpegStreamer::FFmpegStreamer(int width, int height, int fps)
{
    String^ args = String::Format(
        "-f rawvideo -pix_fmt bgr0 -s {0}x{1} -r {2} -i - -c:v libx264 -preset ultrafast -tune zerolatency -f h264 -",
        width, height, fps);

    ffmpegProcess = gcnew Process();
    ffmpegProcess->StartInfo->FileName = "ffmpeg";
    ffmpegProcess->StartInfo->Arguments = args;
    ffmpegProcess->StartInfo->UseShellExecute = false;
    ffmpegProcess->StartInfo->RedirectStandardInput = true;
    ffmpegProcess->StartInfo->RedirectStandardOutput = true;
    ffmpegProcess->StartInfo->CreateNoWindow = true;

    ffmpegProcess->Start();

    ffmpegInput = ffmpegProcess->StandardInput->BaseStream;
    ffmpegOutput = ffmpegProcess->StandardOutput->BaseStream;
}

array<Byte>^ FFmpegStreamer::EncodeFrame(array<Byte>^ frameData)
{
    ffmpegInput->Write(frameData, 0, frameData->Length);
    ffmpegInput->Flush();

    // Wait a little to allow output (can adjust depending on your FPS)
    System::Threading::Thread::Sleep(5); // small delay to buffer output

    array<Byte>^ buffer = gcnew array<Byte>(65536); // 64KB buffer (adjust if needed)
    int bytesRead = ffmpegOutput->Read(buffer, 0, buffer->Length);

    if (bytesRead > 0) {
        array<Byte>^ encodedPacket = gcnew array<Byte>(bytesRead);
        Array::Copy(buffer, encodedPacket, bytesRead);
        return encodedPacket;
    }

    return nullptr;
}

void FFmpegStreamer::Close()
{
  
    ffmpegInput->Close();
    ffmpegOutput->Close();
    ffmpegProcess->Kill();
    ffmpegProcess->WaitForExit();

}
