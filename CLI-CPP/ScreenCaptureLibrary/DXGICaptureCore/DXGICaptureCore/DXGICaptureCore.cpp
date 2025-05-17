#include "pch.h"
#define DXGICAPTURE_EXPORTS
#include "DXGICaptureCore.h"
#include <dxgi1_2.h>
#include <d3d11.h>
#include <wrl/client.h>
#include <iostream>
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "d3d11.lib")
class DXGICore {
public:
    Microsoft::WRL::ComPtr<ID3D11Device> d3dDevice;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> d3dContext;
    Microsoft::WRL::ComPtr<IDXGIOutputDuplication> duplication;
    bool initialized = false;

    bool Initialize();
    bool CaptureFrame(BYTE** outBuffer, unsigned int* width, unsigned int* height, unsigned int* stride);
};

bool DXGICore::Initialize() {
    if (initialized) return true;

    Microsoft::WRL::ComPtr<IDXGIFactory1> factory;
    if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1), (void**)factory.GetAddressOf())))
        return false;

    Microsoft::WRL::ComPtr<IDXGIAdapter1> adapter;
    if (FAILED(factory->EnumAdapters1(0, &adapter)))
        return false;

    if (FAILED(D3D11CreateDevice(adapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION,
        &d3dDevice, nullptr, &d3dContext)))
        return false;

    Microsoft::WRL::ComPtr<IDXGIOutput> output;
    if (FAILED(adapter->EnumOutputs(0, &output)))
        return false;

    Microsoft::WRL::ComPtr<IDXGIOutput1> output1;
    if (FAILED(output.As(&output1)))
        return false;

    if (FAILED(output1->DuplicateOutput(d3dDevice.Get(), &duplication)))
        return false;
    DXGI_OUTDUPL_DESC outputDesc;
    duplication->GetDesc(&outputDesc);
    initialized = true;
    return true;
}

bool DXGICore::CaptureFrame(BYTE** outBuffer, unsigned int* width, unsigned int* height, unsigned int* stride) {
    if (!initialized) return false;

    Microsoft::WRL::ComPtr<IDXGIResource> desktopResource;
    DXGI_OUTDUPL_FRAME_INFO frameInfo = {};

    HRESULT hr = duplication->AcquireNextFrame(500, &frameInfo, &desktopResource);
    if (FAILED(hr)) return false;

    Microsoft::WRL::ComPtr<ID3D11Texture2D> acquiredTex;
    if (FAILED(desktopResource.As(&acquiredTex))) return false;

    D3D11_TEXTURE2D_DESC desc;
    acquiredTex->GetDesc(&desc);
    printf("Texture Format: %u\n", desc.Format);
    D3D11_TEXTURE2D_DESC cpuDesc = desc;
    cpuDesc.Usage = D3D11_USAGE_STAGING;
    cpuDesc.BindFlags = 0;
    cpuDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    cpuDesc.MiscFlags = 0;

    Microsoft::WRL::ComPtr<ID3D11Texture2D> cpuTex;
    if (FAILED(d3dDevice->CreateTexture2D(&cpuDesc, nullptr, &cpuTex))) return false;

    d3dContext->CopyResource(cpuTex.Get(), acquiredTex.Get());

    D3D11_MAPPED_SUBRESOURCE resource = {};
    if (FAILED(d3dContext->Map(cpuTex.Get(), 0, D3D11_MAP_READ, 0, &resource)))
        return false;

    int imageSize = desc.Height * resource.RowPitch;
    *stride = resource.RowPitch;
    *outBuffer = new BYTE[imageSize];
    memcpy(*outBuffer, resource.pData, imageSize);
    d3dContext->Unmap(cpuTex.Get(), 0);
    duplication->ReleaseFrame();
    *width = desc.Width;
    *height = desc.Height;
    return true;
}

// C-style exports
extern "C" {
    __declspec(dllexport) void* DXGI_Create() {
        return new DXGICore();
    }

    __declspec(dllexport) void DXGI_Destroy(void* instance) {
        delete static_cast<DXGICore*>(instance);
    }

    __declspec(dllexport) bool DXGI_Initialize(void* instance) {
        return static_cast<DXGICore*>(instance)->Initialize();
    }

    __declspec(dllexport) bool DXGI_CaptureFrame(void* instance, BYTE** outBuffer, unsigned int* width, unsigned int* height, unsigned int* stride) {
        return static_cast<DXGICore*>(instance)->CaptureFrame(outBuffer, width, height,stride);
    }
}

