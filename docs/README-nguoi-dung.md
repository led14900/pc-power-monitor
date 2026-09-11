> **QUAN TRỌNG — ĐÂY LÀ SỐ LIỆU ƯỚC TÍNH**
> PC Power Monitor tính điện năng dựa trên **cảm biến bên trong máy tính**
> (công suất CPU/GPU, cấu hình phần cứng bạn khai báo). Đây **KHÔNG phải** thiết
> bị đo tại ổ cắm điện. Sai số điển hình **±10–30%** tuỳ máy và tuỳ mức tải.
> Dùng để theo dõi xu hướng và ước lượng hoá đơn, **không dùng để tranh chấp
> hoá đơn EVN**.

# PC Power Monitor — Hướng dẫn người dùng

Ứng dụng chạy hoàn toàn **offline**: không có tài khoản, không thu thập telemetry,
không gửi dữ liệu ra ngoài. Chỉ khi bạn tự bấm vào một liên kết trong phần "Giới
thiệu" thì trình duyệt mới mở ra.

---

## 1. Ứng dụng này làm gì / KHÔNG làm gì

**Làm:**
- Đọc công suất tức thời của CPU và GPU từ cảm biến phần cứng (qua thư viện
  LibreHardwareMonitor).
- Cộng thêm phần điện ước tính của RAM, ổ cứng, quạt, mainboard, hao hụt nguồn
  (PSU) dựa trên cấu hình bạn khai báo.
- Tích phân công suất theo thời gian để ra **kWh**, rồi nhân đơn giá điện để ước
  tính **chi phí tiền điện** theo ngày / tháng.
- Vẽ biểu đồ, lập báo cáo, xuất CSV.
- Cảnh báo khi nhiệt độ hoặc công suất vượt ngưỡng.

**KHÔNG làm:**
- Không đo điện tại ổ cắm; không đo bằng đồng hồ/clamp meter.
- **Không tính điện màn hình** (v1 chỉ tính phần điện mà thùng máy/PSU tiêu thụ).
- Không tính điện của thiết bị ngoại vi cắm nguồn riêng (loa công suất lớn, sạc
  điện thoại, dock…), trừ phần "Watt ngoại vi" bạn tự nhập.
- Không đo laptop chạy pin theo dòng xả pin (v1 tập trung máy để bàn).
- Không thay thế hoá đơn EVN.

---

## 2. Yêu cầu hệ thống

- **Windows 10 64-bit** trở lên (Windows 11 hỗ trợ đầy đủ).
- **Quyền Administrator** khi chạy (xem mục 4).
- Máy để bàn có cảm biến công suất CPU/GPU. Nếu bo mạch/CPU không xuất cảm biến
  công suất, ứng dụng vẫn chạy nhưng ở **chế độ giới hạn** (không có số liệu W).
- ~200 MB dung lượng đĩa cho bản cài (đã kèm .NET 8, không cần cài runtime).

---

## 3. Cài đặt và xử lý cảnh báo SmartScreen

Bản cài **chưa được ký số** (code signing). Vì vậy Windows SmartScreen sẽ hiện
cảnh báo "Windows protected your PC" / "Unknown publisher". Đây là điều **bình
thường với phần mềm nhỏ chưa mua chứng chỉ**, không phải virus.

Các bước:
1. Tải tệp `PcPowerMonitorSetup-1.0.0.exe`.
2. Bấm đúp để chạy. Nếu hiện màn hình xanh **"Windows protected your PC"**:
   - Bấm dòng chữ **"More info"** (Thêm thông tin).
   - Bấm nút **"Run anyway"** (Vẫn chạy).
3. Cửa sổ **UAC** ("Do you want to allow this app to make changes…") hiện ra →
   bấm **Yes**. (Bắt buộc, vì ứng dụng cài vào `C:\Program Files`.)
4. Trình cài đặt mở ra:
   - Giữ nguyên thư mục cài mặc định `C:\Program Files\PcPowerMonitor`
     (không nên đổi — xem mục 4).
   - Tùy chọn: tích **"Tạo lối tắt trên màn hình nền"**.
   - Tùy chọn: tích **"Khởi động cùng Windows"** (xem mục 8).
5. Bấm **Install** → **Finish**. Có thể tích "Chạy PC Power Monitor ngay".

Gỡ cài đặt: xem mục 11.

---

## 4. Tại sao cần quyền Administrator

Ứng dụng đọc cảm biến công suất/nhiệt độ thông qua một **driver kernel**
(WinRing0, đi kèm LibreHardwareMonitor). Windows chỉ cho tiến trình **chạy với
quyền Administrator** nạp và giao tiếp với driver này. Không có quyền admin ⇒
không đọc được công suất ⇒ không tính được kWh.

Ứng dụng **luôn** yêu cầu quyền admin ngay khi khởi động (bạn sẽ thấy UAC).

**Vì sao bắt buộc cài vào `C:\Program Files`:** nếu bật "Khởi động cùng Windows",
ứng dụng tạo một *tác vụ Task Scheduler* chạy **với quyền cao nhất và không hỏi
UAC**. Nếu tệp `.exe` nằm ở thư mục mà người dùng thường có quyền ghi
(`%LOCALAPPDATA%`, Desktop…), thì một phần mềm độc hại chạy quyền thường có thể
**thay thế tệp .exe** và được Task Scheduler chạy lên quyền admin — tức leo thang
đặc quyền. `Program Files` chỉ Administrator mới ghi được nên chặn được kịch bản
này. Đây là lý do bảo mật, không phải lựa chọn tùy thích.

---

## 5. Windows Defender báo `VulnerableDriver:WinNT/Winring0`

### Chuyện gì đang xảy ra

`WinRing0` là driver đọc cảm biến **dùng chung** bởi rất nhiều phần mềm phổ biến:
**HWiNFO, MSI Afterburner, FanControl, OpenRGB, Libre/Open Hardware Monitor…**
Từ khoảng **tháng 3/2025**, Microsoft đưa driver này vào danh sách
"vulnerable driver" (liên quan lỗ hổng **CVE-2020-14979**) nên Windows Defender
có thể cảnh báo `VulnerableDriver:WinNT/Winring0` hoặc **cách ly (quarantine)**
tệp driver mà PC Power Monitor giải nén ra.

Đây **không phải** Defender phát hiện PC Power Monitor là virus — nó cảnh báo về
*driver dùng chung* nói trên.

### Nếu Defender chặn driver

Ứng dụng vẫn **mở lên và chạy được ở chế độ giới hạn**: bạn xem được giao diện,
cấu hình, báo cáo cũ… nhưng **không có số liệu công suất mới** (thanh trạng thái
sẽ báo "cảm biến không khả dụng"). Xem thêm mục "Chẩn đoán" trong tab **Cài đặt**.

### Cách cho phép (thêm exclusion) — CÂN NHẮC KỸ

> ⚠️ **Cảnh báo bảo mật:** Thêm exclusion nghĩa là **Defender sẽ ngừng quét**
> thư mục đó. Nếu về sau có tệp độc hại lọt vào `C:\Program Files\PcPowerMonitor`,
> Defender sẽ **không phát hiện**. Chỉ làm nếu bạn hiểu và chấp nhận đánh đổi này.
> Nếu **không** chấp nhận: cứ để nguyên, ứng dụng vẫn dùng được ở chế độ giới hạn.

Các bước thêm exclusion (Windows 10/11):
1. Mở **Start** → gõ **"Windows Security"** → mở ứng dụng **Windows Security**
   (Bảo mật Windows).
2. Vào **Virus & threat protection** (Bảo vệ khỏi vi-rút & mối đe dọa).
3. Ở mục **Virus & threat protection settings**, bấm **Manage settings**
   (Quản lý cài đặt).
4. Kéo xuống mục **Exclusions** (Loại trừ) → bấm **Add or remove exclusions**.
5. Bấm **Add an exclusion** → chọn **Folder** (Thư mục).
6. Chọn thư mục: **`C:\Program Files\PcPowerMonitor`** → **Select Folder**.
7. Nếu trước đó driver đã bị "Protection history" cách ly: vào
   **Windows Security → Protection history**, tìm mục Winring0 và chọn
   **Restore** (Khôi phục).
8. Mở lại PC Power Monitor. Nếu vẫn báo giới hạn, **khởi động lại máy** rồi mở lại
   (driver kernel chỉ nạp được sạch sau khi reboot).

---

## 6. Cấu hình giá điện và đối chiếu hoá đơn EVN

Mở tab **Cài đặt → Giá điện**.

**Giá trị mặc định của ứng dụng:**
- Đơn giá: **3.460 đ/kWh** — đây là **giá bậc 6 (bậc cao nhất) của biểu giá bán
  lẻ điện sinh hoạt EVN, CHƯA gồm VAT**.
- **VAT: 10%** (thuế GTGT hiện hành cho điện sinh hoạt). Có ô bật/tắt "Tính VAT
  vào chi phí".
- ⇒ Chi phí hiển thị mặc định ≈ `kWh × 3.460 × 1,10`.

**Vì sao dùng giá phẳng bậc 6 chứ không lũy tiến 6 bậc:**
Hầu hết hộ dùng máy tính để bàn nhiều giờ/ngày đã tiêu thụ vượt các bậc thấp từ
các thiết bị khác trong nhà. Khi đó **mỗi kWh mà PC tiêu thụ thêm bị tính ở bậc
cao nhất** — đây gọi là **chi phí biên (marginal cost)**. Dùng giá phẳng bậc 6
cho ra ước tính "chi phí tăng thêm do dùng PC" **sát thực tế hơn** so với việc mô
phỏng cả biểu lũy tiến (vốn phụ thuộc tổng tiêu thụ của cả hộ, thứ ứng dụng không
biết). Biểu lũy tiến đầy đủ nằm trong kế hoạch phiên bản sau.

**Cách đối chiếu với hoá đơn EVN và tự cập nhật:**
1. Lấy hoá đơn/tin nhắn EVN gần nhất. Tìm **đơn giá bậc 6** (đ/kWh) và **thuế
   suất VAT**.
2. Nếu EVN điều chỉnh giá (thường có thông báo trên trang EVN khu vực): nhập
   **đơn giá mới (chưa VAT)** vào ô "Đơn giá", chỉnh **VAT (%)** nếu thay đổi.
3. Đặt **"Ngày hiệu lực"** đúng ngày EVN áp dụng để báo cáo các kỳ trước vẫn dùng
   giá cũ.
4. Ghi nguồn vào ô **"Ghi chú nguồn"** (ví dụ: "QĐ số … của EVN, áp dụng từ …")
   để lần sau tra lại.
5. So sánh: **kWh do ứng dụng ước tính CHỈ là phần của PC**, luôn nhỏ hơn tổng
   kWh trên hoá đơn EVN (gồm cả tủ lạnh, điều hoà, đèn, màn hình…). Đừng kỳ vọng
   hai con số bằng nhau.

---

## 7. Cấu hình phần cứng để tăng độ chính xác

Mở tab **Cài đặt → Cấu hình phần cứng**. Khai báo càng đúng, ước tính càng sát:

- **TDP CPU (W)**: tra theo model CPU (trang nhà sản xuất). Dùng khi cảm biến
  công suất CPU không có.
- **TDP GPU (W)**: nhập 0 nếu dùng GPU tích hợp / không có card rời.
- **Số thanh RAM** và **loại RAM** (DDR4/DDR5): ước tính ~2–5 W mỗi thanh.
- **Số SSD / số HDD**: HDD tốn điện hơn SSD đáng kể.
- **Số quạt**: quạt case + quạt CPU.
- **Watt mainboard / Watt ngoại vi**: phần nền khó đo (chipset, USB, đèn LED,
  phần ngoại vi bạn muốn tính thêm).
- **Công suất PSU (W)** và **Chuẩn 80 PLUS** (Bronze/Gold/…): dùng để tính
  **hao hụt hiệu suất nguồn** — điện lấy từ ổ cắm luôn lớn hơn điện linh kiện dùng.

Ô "Ước tính công suất nền" hiển thị tổng phần không gồm CPU/GPU để bạn kiểm tra
nhanh có hợp lý không.

**Hoặc dùng AI:** Nếu không chắc các thông số này, bạn có thể bấm nút **"Dò thông số bằng AI"** — nhưng trước tiên phải cấu hình Vertex AI trong **Cài đặt → Cấu hình AI**:

1. **Lấy Google Cloud Service Account JSON:**
   - Truy cập [Google Cloud Console](https://console.cloud.google.com)
   - Tạo một Service Account và tải file JSON key
   - Bấm **"Chọn file"** trong phần Cấu hình AI, chọn file JSON đó

2. **Nhập thông tin Vertex AI:**
   - **Project ID**: ID của GCP project
   - **Region**: Mặc định `us-central1` (có thể đổi nếu cần)
   - Bấm **"Lưu"** để lưu cấu hình

3. **Dùng AI để tra cứu phần cứng:**
   - Quay lại tab Cấu hình phần cứng, bấm **"Dò thông số bằng AI"**
   - Ứng dụng sẽ kết nối Vertex AI, tra cứu thông số model máy tính và gợi ý giá trị
   - Xem lại kết quả, rồi bấm **"Áp dụng"** để chấp nhận

**Lưu ý:** File Service Account JSON sẽ được mã hoá lưu trữ an toàn (không lưu dưới dạng text).

---

## 8. Chạy nền, khay hệ thống (tray) và khởi động cùng Windows

- **Đóng cửa sổ** (nút X) **không thoát** ứng dụng — nó thu nhỏ xuống **khay hệ
  thống** (góc phải thanh taskbar) để tiếp tục đo. Chuột phải vào biểu tượng khay
  để **Mở** hoặc **Thoát** hẳn.
- **Khởi động thu nhỏ xuống khay**: bật trong **Cài đặt → Khởi động**.
- **Khởi động cùng Windows**:
  - Có thể bật ngay khi cài (checkbox trong trình cài đặt), hoặc bật sau trong
    **Cài đặt → Khởi động → "Khởi động cùng Windows"**.
  - Ứng dụng tạo **tác vụ Task Scheduler** tên `PcPowerMonitorAutoStart`, chạy
    `--autostart` khi bạn đăng nhập, **với quyền cao nhất và KHÔNG hiện UAC**.
  - Ở chế độ auto-start, ứng dụng chờ ~15 giây rồi chạy nền, chỉ hiện ở khay.
  - Tắt checkbox này sẽ xoá tác vụ đó.

---

## 9. Xuất CSV và mở bằng Excel

1. Vào tab **Báo cáo**, chọn khoảng thời gian (ngày/tháng).
2. Bấm **Xuất CSV**, chọn nơi lưu.
3. Mở tệp bằng **Excel**:
   - Tệp mã hoá **UTF-8**, phân tách bằng dấu phẩy.
   - Số theo định dạng Việt Nam (dấu `.` ngăn nghìn, dấu `,` thập phân). Nếu Excel
     hiển thị sai, dùng **Data → From Text/CSV** và chọn Locale = Vietnamese.
4. Cột gồm: thời gian, kWh, công suất trung bình, chi phí ước tính (có/không VAT).

---

## 10. Vị trí dữ liệu, log và gửi thông tin chẩn đoán

Tất cả dữ liệu nằm trong:

```
%LOCALAPPDATA%\PcPowerMonitor\
├── settings.json          cấu hình
├── data\monitor.db        CSDL SQLite (lịch sử điện năng)
└── logs\                  log dạng .txt, cuộn theo ngày
```

(Thường là `C:\Users\<tên bạn>\AppData\Local\PcPowerMonitor`.)

**Khi báo lỗi:**
1. Vào **Cài đặt → Chẩn đoán**.
2. Bấm **"Sao chép chẩn đoán"** (trạng thái cảm biến, đường dẫn log, dung lượng
   CSDL, danh sách cảm biến phát hiện được) và dán vào email/issue.
3. Bấm **"Mở thư mục log"**, đính kèm tệp log mới nhất.
4. Lưu ý: chẩn đoán có thể chứa **tên phần cứng của bạn** — xem qua trước khi
   chia sẻ công khai.

---

## 11. Gỡ cài đặt

1. **Settings → Apps → Installed apps** (hoặc **Control Panel → Programs and
   Features**) → tìm **PC Power Monitor** → **Uninstall**.
2. Nếu ứng dụng đang chạy, trình gỡ sẽ yêu cầu **đóng ứng dụng** trước (thoát từ
   biểu tượng khay).
3. Trình gỡ tự động **xoá tác vụ** `PcPowerMonitorAutoStart` và toàn bộ tệp trong
   `C:\Program Files\PcPowerMonitor`.
4. Cuối cùng sẽ hỏi: **"Xóa toàn bộ dữ liệu lịch sử điện năng?"**
   - **No** (mặc định, khuyến nghị): giữ lại `%LOCALAPPDATA%\PcPowerMonitor` —
     cài lại sau vẫn còn lịch sử.
   - **Yes**: xoá vĩnh viễn toàn bộ dữ liệu, log và cấu hình.
5. Nếu trước đó bạn thêm **Defender exclusion** cho thư mục cài (mục 5), hãy tự
   **gỡ exclusion đó** sau khi gỡ cài đặt.

---

## 12. Câu hỏi thường gặp (FAQ)

**Sao số Watt khác với HWiNFO / Afterburner?**
Mỗi công cụ đọc **tập cảm biến khác nhau** và cộng gộp theo cách khác nhau.
PC Power Monitor cộng công suất CPU + GPU + phần nền *ước tính* (RAM, ổ đĩa, quạt,
mainboard) + *hao hụt PSU*. HWiNFO thường hiển thị từng cảm biến riêng lẻ
("CPU Package Power", "GPU Power") mà không cộng phần nền hay hao hụt nguồn. Ngoài
ra thời điểm lấy mẫu và bộ lọc trung bình khác nhau nên con số tức thời sẽ lệch.
Xu hướng theo thời gian mới là thứ đáng tin.

**Sao kWh không tăng khi máy đang ngủ (Sleep)?**
Khi Windows vào Sleep/Hibernate hoặc khoá phiên, ứng dụng **tạm dừng lấy mẫu**
(nghe sự kiện nguồn của hệ điều hành) và **không cộng dồn kWh** trong thời gian
đó — vì CPU/GPU gần như không tiêu thụ và cảm biến không cập nhật. Khi máy thức
dậy, việc đo tiếp tục. Điện ở chế độ chờ (vài W) không được tính trong v1.

**Có tính điện màn hình không?**
**Không.** Phiên bản 1 chỉ tính phần điện đi qua **PSU / thùng máy**. Màn hình
cắm nguồn riêng, không đi qua cảm biến của máy tính, nên không thể ước tính chính
xác. Nếu muốn ước lượng thô, bạn có thể cộng công suất màn hình (ghi trên nhãn
sau lưng, thường 15–40 W) vào ô **"Watt ngoại vi"** trong Cấu hình phần cứng.

**Ứng dụng có gửi dữ liệu của tôi đi đâu không?**
Không. Hoàn toàn offline, không telemetry, không tài khoản, không kết nối máy chủ.
Chỉ mở trình duyệt khi bạn tự bấm liên kết trong màn hình "Giới thiệu".

**Vì sao installer bị SmartScreen cảnh báo?**
Bản v1 không mua chứng chỉ ký số. Xem mục 3 để chạy qua cảnh báo.
