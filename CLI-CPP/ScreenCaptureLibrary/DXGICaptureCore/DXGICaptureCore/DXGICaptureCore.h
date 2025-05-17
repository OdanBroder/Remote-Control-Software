#pragma once
#include <Windows.h>
#pragma message("Compiling with DXGICaptureCore.h version from: " __FILE__)

#ifdef DXGICAPTURE_EXPORTS
#define DXGI_API __declspec(dllexport)
#else
#define DXGI_API __declspec(dllimport)
#endif

extern "C" {
    DXGI_API void* DXGI_Create();
    DXGI_API void DXGI_Destroy(void* instance);
    DXGI_API bool DXGI_Initialize(void* instance);
    DXGI_API bool DXGI_CaptureFrame(void* instance, BYTE** outBuffer, unsigned int* width, unsigned int* height, unsigned int* stride);
}
