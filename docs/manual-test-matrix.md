# Ma Trận Kiểm Thử Thủ Công — PC Power Monitor

## Tổng Quan

Các trường hợp kiểm thử dưới đây yêu cầu máy tính thực, quyền admin, hoặc điều kiện hệ thống mà bộ kiểm thử tự động không thể mô phỏng. Mỗi kịch bản phải được chạy ít nhất một lần trên phần cứng cụ thể; ghi lại kết quả vào cột "Kết Quả" sau khi chạy.

## Cách Chạy

1. Xây dựng ứng dụng: `dotnet build PcPowerMonitor.sln -c Release`
2. Chạy app từ thư mục build hoặc đăng ký tác vụ Windows Scheduled Task để auto-start
3. Kiểm tra tray icon, bảng điều khiển (dashboard), và log để xác nhận hành vi mong đợi

## Ma Trận (M1 — M16)

| # | Cấu Hình / Kịch Bản | Kỳ Vọng | Kết Quả |
|---|---|---|---|
| M1 | Desktop Intel (12th gen+) + GPU NVIDIA rời, chạy admin | `Availability=Full`, CPU/GPU power > 0, WallW ≈ baseline + CPU + GPU / efficiency | CHƯA CHẠY (cần phần cứng) |
| M2 | Desktop AMD (Ryzen) + GPU AMD rời, chạy admin | `Full`, sensor "Package Power"/"GPU PPT" đọc được, Quality = Measured | CHƯA CHẠY (cần phần cứng) |
| M3 | Chỉ iGPU (không GPU rời) | Không crash, GPU W = 0 hoặc fallback TDP, Quality = Mixed/Estimated | CHƯA CHẠY (cần phần cứng) |
| M4 | Laptop (Intel/AMD) | Không crash; nhiều sensor null; ghi rõ độ chính xác thấp | CHƯA CHẠY (cần phần cứng) |
| M5 | CPU cũ (pre-Haswell) không có RAPL | Fallback `TDP × Load`, Quality = Estimated, có note "assumed TDP" | CHƯA CHẠY (cần phần cứng) |
| M6 | **Chạy KHÔNG quyền admin** | Banner đỏ, `Availability=Unavailable`, app không crash, UI mở được | CHƯA CHẠY (cần test không admin) |
| M7 | **Defender/antivirus chặn/xóa WinRing0** | `Open()` fail → degraded, reason hiển thị, link hướng dẫn hoạt động | CHƯA CHẠY (cần simulate block) |
| M8 | Sleep 30 phút rồi resume | 1 record `is_gap=1`, kWh không nhảy, chart có đoạn đứt | CHƯA CHẠY (cần test sleep) |
| M9 | Hibernate qua đêm | Như M8; buffer đã flush trước khi ngủ (không mất record) | CHƯA CHẠY (cần test hibernate) |
| M10 | Kill process bằng Task Manager rồi mở lại | `TotalKwh` khôi phục từ `Meta`, không cộng khoảng chết | CHƯA CHẠY (cần test restart) |
| M11 | Đổi giờ hệ thống +1h khi đang chạy | kWh không sai (dùng Stopwatch); rollup theo local vẫn đúng | CHƯA CHẠY (cần test đổi giờ) |
| M12 | Auto-start sau reboot | App chạy elevated, không UAC prompt, chỉ tray icon | CHƯA CHẠY (cần test reboot) |
| M13 | Chạy 24h liên tục | RAM không tăng đơn điệu; DB tăng ~3MB/ngày; CPU < 2% | CHƯA CHẠY (cần test 24h) |
| M14 | Mở CSV bằng Excel VN | Tiếng Việt có dấu đúng, cột tách đúng, số là số | CHƯA CHẠY (cần test Excel) |
| M15 | Đường dẫn cài có khoảng trắng | Auto-start tạo task thành công, app chạy được | CHƯA CHẠY (cần test path spaces) |
| M16 | Ổ đĩa đầy khi đang ghi DB | Log Error, app không crash, tiếp tục chạy khi có chỗ trống | CHƯA CHẠY (cần simulate full disk) |

## Ghi Chú

- Các trường hợp M1–M5 test **phần cứng thực**, không tự động hóa được.
- Các trường hợp M6–M16 test **điều kiện ngoài phần cứng** (quyền, file, thời gian, disk, v.v.).
- Kết quả "CHƯA CHẠY" có nghĩa đo kiểm chưa được thực hiện; chuyển sang "✓ PASS" hoặc "✗ FAIL" sau khi chạy.
- Trong quá trình test, ghi lại:
  - **Phiên bản app**: `Help → About` hoặc tương tự
  - **Ngày/giờ kiểm thử**
  - **Kết quả chi tiết**: nêu rõ thành công hay lỗi, hiệu ứng quan sát
  - **Ghi chú bổ sung**: điều kỳ lạ, hiệu suất, lỗi nào
