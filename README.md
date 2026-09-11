# PC Power Monitor

Ứng dụng Windows (WPF, .NET 8) giám sát công suất tiêu thụ điện của PC theo thời gian thực và tính tiền điện theo biểu giá EVN.

🌐 Website: **[tapgo10ngon.com](https://tapgo10ngon.com)**
📦 Tải bản mới nhất: **[Releases](https://github.com/led14900/pc-power-monitor/releases/latest)**
📖 Hướng dẫn sử dụng đầy đủ: [`docs/README-nguoi-dung.md`](docs/README-nguoi-dung.md)

> **Lưu ý:** đây là số liệu ước tính dựa trên cảm biến bên trong máy (CPU/GPU) và cấu hình phần cứng bạn khai báo — **không** phải thiết bị đo tại ổ cắm điện. Sai số điển hình ±10–30%. Dùng để theo dõi xu hướng, không dùng để tranh chấp hoá đơn EVN.

## Tính năng chính

- 📊 **Dashboard thời gian thực** — công suất tức thời, biểu đồ, chỉ số CPU/GPU/RAM/ổ đĩa/quạt
- ⚡ **Ước tính công suất** từ cảm biến phần cứng (LibreHardwareMonitor), tích lũy kWh, lưu SQLite
- 💰 **Tính tiền điện theo biểu giá EVN**, báo cáo ngày/tháng/năm, xuất CSV
- 🔔 **Cảnh báo ngưỡng** nhiệt độ CPU/GPU, công suất tiêu thụ
- 🤖 **Tra cứu hồ sơ phần cứng bằng AI** — dùng Google Vertex AI (có Google Search grounding) để tự động điền thông số máy
- 🔒 Mã hoá DPAPI cho dữ liệu nhạy cảm (service account JSON)
- 🖥️ Chạy nền khay hệ thống, tự khởi động cùng Windows

## Tải về & cài đặt

| File | Dùng khi nào |
|---|---|
| `PcPowerMonitorSetup-1.0.0.exe` | Cài đặt thông thường (khuyến nghị) |
| `pc-power-monitor-v1.0.0-portable-win-x64.zip` | Bản portable — giải nén và chạy, không cần cài |

**Yêu cầu:** Windows 10/11 64-bit, nên chạy quyền Administrator để đọc đầy đủ cảm biến phần cứng.

Chi tiết cài đặt, xử lý cảnh báo SmartScreen, và hướng dẫn sử dụng từng tính năng: xem [`docs/README-nguoi-dung.md`](docs/README-nguoi-dung.md).

## Công nghệ

.NET 8 · WPF · CommunityToolkit.Mvvm · SQLite · LibreHardwareMonitor · Serilog · Google Vertex AI

## Build từ mã nguồn

```bash
dotnet build PcPowerMonitor.sln -c Release
dotnet test PcPowerMonitor.sln -c Release
```

Đóng gói installer + bản portable: `build/build-installer.ps1` (cần [Inno Setup 6](https://jrsoftware.org/isinfo.php)).

## License

[MIT](LICENSE)
