#pragma once
#include <vector>
#include <cstdint>

class H264FrameFixer {
public:
    // Hàm xử lý một gói NAL (theo cả packet, nếu có nhiều NAL đơn trong 1 packet thì bạn cần tách riêng)
    // Trả về buffer mới nếu là IDR, chứa SPS, PPS được prepend; nếu không, trả về buffer nguyên gốc hoặc rỗng nếu không cần gửi.
    std::vector<uint8_t> fix(const uint8_t* data, size_t size);

private:
    std::vector<uint8_t> sps;
    std::vector<uint8_t> pps;

    bool isSPS(uint8_t nal) { return (nal & 0x1F) == 7; }
    bool isPPS(uint8_t nal) { return (nal & 0x1F) == 8; }
    bool isIDR(uint8_t nal) { return (nal & 0x1F) == 5; }

    // Hàm thêm start code vào một NAL unit đã lưu
    std::vector<uint8_t> addStartCode(const std::vector<uint8_t>& nal);
};