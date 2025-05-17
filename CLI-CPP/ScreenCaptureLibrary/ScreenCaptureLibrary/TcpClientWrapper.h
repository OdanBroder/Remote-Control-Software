#pragma once
using namespace System;
using namespace System::Net::Sockets;

ref class TcpClientWrapper {
private:
    TcpClient^ client;
    NetworkStream^ stream;

public:
    TcpClientWrapper(String^ host, int port);
    void Send(array<Byte>^ data);
    void Close();
};
