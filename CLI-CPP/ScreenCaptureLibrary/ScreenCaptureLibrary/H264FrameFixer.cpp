#include "pch.h"
#include "H264FrameFixer.hpp"

std::vector<uint8_t> H264FrameFixer::addStartCode(const std::vector<uint8_t>& nal) {
    // Start code thường là 00 00 00 01
    std::vector<uint8_t> out = { 0x00, 0x00, 0x00, 0x01 };
    out.insert(out.end(), nal.begin(), nal.end());
    return out;
}

std::vector<uint8_t> H264FrameFixer::fix(const uint8_t* data, size_t size) {
    // Kiểm tra đủ điều kiện để đọc NAL header (giả định start code 4 byte)
    if (size < 5) return {};

    // Giả sử dữ liệu có start code ở 4 byte đầu, và sau đó NAL header ở byte thứ 5
    uint8_t nal_header = data[4];
    // Tạo vector chứa toàn bộ gói tin, bao gồm start code
    std::vector<uint8_t> nal_packet(data, data + size);

    // Nếu đây là SPS hoặc PPS, lưu lại và không gửi gói tin riêng (hoặc có thể gửi nếu bạn cần)
    if (isSPS(nal_header)) {
        sps.assign(data + 4, data + size);  // lưu phần payload (không cần start code nếu bạn thêm lại sau)
        return {}; // Không gửi gói tin riêng
    }
    if (isPPS(nal_header)) {
        pps.assign(data + 4, data + size);
        return {}; // Không gửi gói tin riêng
    }

    // Nếu đây là IDR frame
    if (isIDR(nal_header)) {
        std::vector<uint8_t> out;
        if (!sps.empty() && !pps.empty()) {
            // Prepend SPS và PPS với start code (nếu chưa có)
            std::vector<uint8_t> spsPacket = addStartCode(sps);
            std::vector<uint8_t> ppsPacket = addStartCode(pps);
            out.insert(out.end(), spsPacket.begin(), spsPacket.end());
            out.insert(out.end(), ppsPacket.begin(), ppsPacket.end());
        }
        // Sau đó thêm IDR frame
        out.insert(out.end(), nal_packet.begin(), nal_packet.end());
        return out;
    }

    // Nếu không phải IDR, gửi gói tin như thường (có thể là P-frame)
    return nal_packet;
}