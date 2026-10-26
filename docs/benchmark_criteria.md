# Tiêu chí Đo lường Hiệu năng (Benchmark Criteria) & Definition of Done

## 1. Tiêu chí hiệu năng mục tiêu

| Chỉ số | Mục tiêu (Target) | Phương pháp đo lường |
| :--- | :--- | :--- |
| **Hotkey -> Launcher hiển thị** | `< 100 ms` | Đo thời gian từ sự kiện bàn phím OS đến khi WPF Window hoàn tất vẽ (`Rendered`) |
| **Tìm kiếm ứng dụng (App Search)** | `< 50 ms` | Đo thời gian chạy `SearchEngine.QueryAsync` trên tập dữ liệu > 1,000 apps |
| **Tìm kiếm file (File Search)** | `< 100 ms` | Đo thời gian SQLite query + fuzzy match trên tập dữ liệu > 100,000 files |
| **Gõ phím -> Cập nhật kết quả UI** | `< 50 ms` | UI Debounce + Dispatcher render time |
| **CPU khi ở chế độ nghỉ (Idle CPU)**| `~ 0%` | Windows Performance Monitor / Task Manager |
| **Bộ nhớ khi ở chế độ nghỉ (Idle RAM)** | `< 100 MB` | Task Manager Private Working Set (trước khi tải AI Model) |
| **Local AI Latency** | Streaming; không chặn UI | AI Query chạy trên background worker / Task độc lập |

## 2. Definition of Done (DoD)
- [x] Solution .NET biên dịch sạch sẽ không có cảnh báo/lỗi nghiêm trọng.
- [ ] Launcher hiển thị tức thì khi ấn phím nóng (`Alt + Space`) và ẩn khi nhấn `Esc` hoặc mất focus.
- [ ] Tắt toàn bộ AI thì Core search vẫn hoạt động độc lập và đầy đủ tính năng.
- [ ] Không có thao tác destructive (xóa file, chạy lệnh shell can thiệp hệ thống) tự động thực thi mà không có hộp thoại xác nhận.
- [ ] Các plugin chạy cô lập; lỗi hoặc timeout trong plugin không làm sập ứng dụng chính.
- [ ] Mô hình phân loại ý định (Intent Classifier) chạy offline qua ONNX Runtime với độ trễ < 20ms.
