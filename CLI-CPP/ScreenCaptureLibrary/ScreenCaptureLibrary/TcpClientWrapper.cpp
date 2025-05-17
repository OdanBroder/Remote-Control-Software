#include "pch.h"
#include "TcpClientWrapper.h"

TcpClientWrapper::TcpClientWrapper(String^ host, int port) {
    client = gcnew TcpClient();
    client->Connect(host, port);
    stream = client->GetStream();
}

void TcpClientWrapper::Send(array<Byte>^ data) {
    int len = data->Length;
    array<Byte>^ lenBytes = BitConverter::GetBytes(len);
    Console::WriteLine("Sending: Length = {0}, Data = {1} bytes", len, data->Length);
    stream->Write(lenBytes, 0, lenBytes->Length);
    stream->Write(data, 0, data->Length);
    stream->Flush();
}
void TcpClientWrapper::Close()
{
    client->Close();
    stream->Close();
}