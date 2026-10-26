# Local AI Launcher

> **Windows-native • Offline-first • High Performance • Local AI & Custom Models**

Local AI Launcher là ứng dụng launcher hiện đại dành cho Windows, mang lại trải nghiệm tìm kiếm nhanh chóng, mượt mà tương tự như Flow Launcher / Raycast / Alfred, đồng thời tích hợp sức mạnh của AI chạy trực tiếp trên máy cục bộ (Ollama, ONNX Runtime).

---

## Tính năng nổi bật

- ⚡ **Siêu nhanh & Nhẹ**: Khởi động tức thì, phím nóng hiển thị `< 100ms`, tìm kiếm `< 50ms`.
- 🔒 **Bảo mật & Cục bộ (Offline-first)**: Toàn bộ chỉ mục, lịch sử và mô hình AI chạy trên máy bạn, không phụ thuộc kết nối internet.
- 🎯 **Fuzzy Search & Ranking thông minh**: Tìm kiếm ứng dụng, tệp tin, tính toán số học, điều hướng URL với thuật toán xếp hạng ưu tiên theo thói quen sử dụng.
- 🤖 **AI cục bộ tích hợp**:
  - Hỗ trợ kết nối với Ollama / llama.cpp cho sinh văn bản, tóm tắt clipboard.
  - Phân loại ý định tìm kiếm (Intent Classifier) siêu nhẹ thông qua ONNX Runtime.
- 🧩 **Hệ thống Plugin mở rộng**: Tách biệt logic với `Launcher.SDK` giúp cộng đồng dễ dàng viết plugin mới.

---

## Cấu trúc Solution

```
LAILauncher/
├── src/
│   ├── Launcher.App/         # [WPF / .NET 8] UI chính, Tray Icon, Hotkey, Theme
│   ├── Launcher.Core/        # Search Engine, Ranking, Query Pipeline, Calculator
│   ├── Launcher.SDK/         # Plugin interfaces & Contracts
│   ├── Launcher.Database/    # SQLite Repositories, History, Settings
│   ├── Launcher.Indexer/     # Start Menu & File System Scanner
│   └── Launcher.AI/          # AI Provider abstraction & ONNX Intent Router
├── tests/
│   └── Launcher.Core.Tests/  # Unit tests cho Core engine & Algorithms
├── ml/                       # Python workspace cho huấn luyện Intent & Ranking models
└── docs/                     # Tài liệu kỹ thuật chi tiết
```

---

## Yêu cầu môi trường & Cài đặt

1. **Yêu cầu hệ thống**:
   - Windows 10/11 (64-bit)
   - .NET 8.0 SDK trở lên (hoặc .NET 10 SDK)
   - Python 3.10+ (cho module ML trong thư mục `ml/`)

2. **Build dự án C#**:
   ```powershell
   dotnet restore
   dotnet build
   dotnet test
   ```

3. **Chạy ứng dụng**:
   ```powershell
   dotnet run --project src/Launcher.App
   ```

4. **Phím tắt mặc định**:
   - Mở/Ẩn Launcher: `Alt + Space`
   - Đóng / Ẩn: `Esc`
   - Điều hướng kết quả: `Phím mũi tên Lên / Xuống`
   - Mở / Thực thi mặc định: `Enter`
   - 🛡️ **Run as Administrator**: `Ctrl + Shift + Enter`
   - 📁 **Mở thư mục chứa (Explorer)**: `Ctrl + Enter`
   - 📋 **Sao chép đường dẫn**: `Ctrl + C`
   - ⚡ **Menu hành động chi tiết**: `Tab` hoặc `Chuột phải`

5. **Tìm kiếm hệ thống tệp & Tìm kiếm Ngữ nghĩa (Zero-NPU Semantic Search & Path Navigation)**:
   - 🎯 **Tìm kiếm Ngữ nghĩa tự nhiên (Semantic Search)**: Không cần nhớ chính xác tên tệp! Nhập câu tự nhiên bằng tiếng Việt hoặc tiếng Anh như:
     - `báo cáo tài chính` / `tai chinh`: Tự động tìm bảng tính, hóa đơn, báo cáo tài chính (`.xlsx`, `.csv`, `.pdf`, `.docx`).
     - `hợp đồng` / `hop dong`: Tìm các văn bản hợp đồng, thỏa thuận, điều khoản pháp lý (`.docx`, `.pdf`).
     - `kế hoạch` / `slide`: Tìm bài thuyết trình PowerPoint, tài liệu roadmap (`.pptx`, `.pdf`).
     - `ảnh hoàng hôn` / `anh bien`: Nhận diện ảnh chụp phong cảnh, biển, screenshot trong thư viện ảnh (`.jpg`, `.png`).
     - `luận văn` / `đồ án`: Tìm tài liệu nghiên cứu, sách, giáo trình PDF.
     - **Ưu điểm vượt trội so với Windows Search mới**: Chạy 100% offline trên **mọi CPU/GPU thông thường**, không bắt buộc chip NPU 40 TOPS hay Copilot+ PC đắt tiền!
   - 📁 **Bộ lọc định dạng thông minh (Smart Format Filters)**:
     - `pdf: <từ khóa>`: Chỉ tìm tệp PDF.
     - `doc: <từ khóa>` / `docs:`: Lọc tài liệu văn bản (`.docx`, `.doc`, `.pdf`, `.txt`, `.md`).
     - `sheet: <từ khóa>` / `excel:`: Lọc bảng tính (`.xlsx`, `.xls`, `.csv`).
     - `slide: <từ khóa>` / `ppt:`: Lọc slide thuyết trình (`.pptx`, `.ppt`).
     - `img: <từ khóa>` / `photo:`: Lọc hình ảnh (`.png`, `.jpg`, `.jpeg`, `.webp`, `.svg`).
     - `code: <từ khóa>`: Lọc tệp mã nguồn (`.cs`, `.py`, `.js`, `.ts`, `.html`, `.cpp`...).
     - `zip: <từ khóa>` / `archive:`: Lọc tệp tin nén.
     - `ext:<đuôi_tệp>`: Lọc theo phần mở rộng bất kỳ (vd: `ext:mp4`, `ext:iso`).
   - 🤖 **Tác vụ AI Copilot trên Tài liệu (Local RAG & Summarization)**:
     - Chọn bất kỳ tệp văn bản hoặc tài liệu nào và nhấn `Ctrl + Alt + S` để **Tóm tắt nhanh các ý chính với AI (Ollama)**.
     - Nhấn `Tab` để mở Action Menu: chọn **"💬 Chat với tài liệu này (Local RAG)"** để đặt câu hỏi trực tiếp dựa trên nội dung tệp.
   - 🧭 **Điều hướng đường dẫn trực tiếp**: Gõ `C:\`, `D:\`, `\\`, hoặc `~/` để duyệt cây thư mục theo thời gian thực.

6. **Chạy lệnh Terminal / Shell (`>`)**:
   - Gõ `>` theo sau là lệnh để thực thi trực tiếp qua Terminal (CMD / PowerShell). Ví dụ: `> ping google.com`, `> ipconfig /all`, `> code .`.
   - Nhấn `Enter` để chạy lệnh qua `cmd.exe /k` (giữ cửa sổ để xem kết quả).
   - Nhấn `Ctrl + Shift + Enter` để chạy với quyền **Administrator** (`runas`).
   - Nhấn `Tab` để chọn chạy trong **PowerShell** hoặc sao chép lệnh.

7. **Tìm kiếm Web & Action Keywords (`g`, `yt`, `gh`, `wiki`, `so`, `trans`, `maps`...)**:
   - `g <query>`: Tìm kiếm Google
   - `yt <query>`: Tìm kiếm video YouTube
   - `gh <query>`: Tra cứu GitHub repositories & code
   - `wiki <query>`: Tra cứu bách khoa toàn thư Wikipedia
   - `so <query>`: Tìm kiếm giải pháp lập trình Stack Overflow
   - `trans <query>`: Dịch văn bản qua Google Translate
   - `maps <query>`: Tìm địa điểm trên Google Maps
   - `npm <query>`, `pip <query>`, `nuget <query>`: Tra cứu thư viện package
   - `ddg <query>`, `bing <query>`, `reddit <query>`

8. **Lệnh Hệ Thống & Điều Khiển Windows (System Commands - `sys` / `win`)**:
   - Gõ `sys`, `win` hoặc gõ trực tiếp tên lệnh tìm kiếm:
     - 🌓 **Giao diện Sáng / Tối (Dark / Light Mode)**: `Toggle Windows Dark / Light Mode`, `Windows Dark Mode`, `Windows Light Mode` (`dark mode`, `che do toi`, `che do sang`).
     - 🔊 **Âm lượng**: `Volume Up (+5%)`, `Volume Down (-5%)`, `Toggle Mute / Unmute`, `Set Volume 50%`, `Set Volume 100%`, `Volume Mixer` (`am luong`, `tang am luong`, `giam am luong`, `mute`, `tat am`).
     - 🔆 **Độ sáng màn hình**: `Brightness Up (+10%)`, `Brightness Down (-10%)`, `Set Brightness 100%`, `Set Brightness 50%`, `Set Brightness 25% (Night)` (`do sang`, `tang do sang`, `giam do sang`, `do sat`).
     - 🪫 **Tiết kiệm pin**: `Enable Battery Saver (Power Saver)`, `Balanced Power Mode`, `Battery & Power Settings` (`tiet kiem pin`, `battery saver`, `pin`).
     - 📡 **Mạng Wi-Fi**: `Show Available Wi-Fi Networks` (Bật Flyout danh sách Wi-Fi khả dụng), `Turn On Wi-Fi`, `Turn Off Wi-Fi` (`wifi`, `danh sach wifi`, `bat wifi`, `tat wifi`).
     - 📶 **Bluetooth**: `Bluetooth & Devices Settings`, `Add Bluetooth Device` (Ghép nối thiết bị), `Toggle Bluetooth` (`bluetooth`, `cai dat bluetooth`).
     - 📱 **Mobile Hotspot**: `Mobile Hotspot Settings`, `Toggle Mobile Hotspot` (Bật/tắt trạm phát Wi-Fi từ máy tính) (`hotspot`, `phat wifi`, `chia se mang`).
     - 📌 **Thanh Taskbar**:
       - Vị trí: `Taskbar Position: Bottom` (Dưới cùng), `Taskbar Position: Top` (Trên cùng), `Taskbar Position: Left` (Bên trái), `Taskbar Position: Right` (Bên phải). Tự động khởi động lại Explorer để áp dụng ngay.
       - Căn lề Windows 11: `Taskbar Align: Center` (Căn giữa icon), `Taskbar Align: Left` (Căn lề trái icon).
     - 🎨 **Màu Theme Windows (Accent Color)**: `Windows Accent Color: Electric Blue`, `Royal Purple`, `Crimson Red`, `Emerald Green`, `Amber Orange`, `Vivid Turquoise`, `Magenta Pink` (`mau theme window`, `doi mau window`, `accent color`).
     - ⚙️ **Lệnh nguồn & tiện ích khác**:
       - `Lock`: Khóa máy tính (`Win + L`)
       - `Shut Down`: Tắt máy tính
       - `Restart`: Khởi động lại
       - `Sleep`: Chế độ ngủ (Standby/Sleep)
       - `Hibernate`: Ngủ đông máy tính
       - `Sign Out / Log Off`: Đăng xuất tài khoản
       - `Empty Recycle Bin` / `emptybin`: Dọn sạch toàn bộ thùng rác
       - `Task Manager` / `taskmgr`: Mở Task Manager
       - `Control Panel`: Mở bảng điều khiển Control Panel
       - `Network Connections`: Danh sách kết nối mạng (`ncpa.cpl`)
       - `Device Manager`: Quản lý phần cứng máy tính
   - 🛠️ **Giao diện trực quan trong Cài đặt**: Tab **"Hệ thống Windows"** trong cửa sổ Cài đặt (`settings`) cho phép điều chỉnh thanh trượt âm lượng, độ sáng, bật tắt Wi-Fi, chọn vị trí Taskbar và bảng màu theme chỉ với 1 cú click.

9. **Cài Đặt Windows (Deep Search - `set`)**:
   - Gõ `set <từ khóa>` hoặc tìm kiếm trực tiếp tên trang cài đặt:
     - `Bluetooth`, `Display`, `Wi-Fi`, `Sound`, `Night Light`, `Battery`, `Storage`, `Windows Update`, `Installed Apps`, `Date & Time`, `Default Apps`...

10. **Bookmarks Trình Duyệt (`b` / `bm`)**:
    - Quét và tìm kiếm nhanh toàn bộ dấu trang (Bookmarks) từ **Google Chrome**, **Microsoft Edge**, **Brave**.
    - Gõ `bm <từ khóa>` hoặc `b <từ khóa>` để lọc danh sách bookmark và mở trong trình duyệt.

11. **Quản Lý & Tắt Tiến Trình (Process Killer - `kill` / `proc`)**:
    - Gõ `kill <tên tiến trình>` hoặc `proc <tên>` để xem danh sách tiến trình đang chạy kèm mã PID và dung lượng RAM tiêu thụ.
    - Nhấn `Enter` để Kill tiến trình, hoặc `Ctrl + Shift + Enter` để Force Kill với quyền Administrator (`taskkill /F /PID`).

12. **Bộ Tính Toán & Chuyển Đổi Nâng Cao (Calculator & Converter)**:
    - **Số học & hàm nâng cao**: `(5 + 5) * 3`, `2 ^ 8`, `sqrt(144)`, `sin(pi / 2)`, `cos(0)`.
    - **Chuyển đổi đơn vị**: `100 km to miles`, `1024 mb to gb`, `1 gb to mb`, `37 c to f`, `100 f to c`, `2 kg to lbs`, `60 s to min`.
    - **Chuyển đổi cơ số**: `0xFF to dec`, `255 to hex`, `0b1010 to dec`, `10 to bin`.

13. **Trợ Lý AI Cục Bộ & Mô Hình Tự Huấn Luyện (Local AI & ONNX Intent Classifier)**:
    - 🤖 **Hỏi đáp AI trực tiếp**: Gõ `ai <câu hỏi>`, `ai: <câu hỏi>` hoặc `? <câu hỏi>`.
      - Ví dụ: `ai: giải thích thuật toán Dijkstra`, `ai cách viết regex email`, `? explain quantum computing`.
      - Cửa sổ **AI Streaming** hiển thị câu trả lời theo thời gian thực (token streaming), hỗ trợ sao chép nhanh (`Ctrl + C`) hoặc dừng stream (`Esc`).
    - 📋 **Tác vụ AI với Clipboard (Khay nhớ tạm)**:
      - `ai summarize` / `ai: tóm tắt`: Tự động đọc và tóm tắt nội dung văn bản trong Clipboard.
      - `ai explain` / `ai: giải thích`: Phân tích và giải thích đoạn code hoặc tài liệu trong Clipboard.
      - `ai translate` / `ai: dịch`: Dịch nội dung Clipboard sang Tiếng Việt chuẩn xác.
    - 🧠 **Mô hình Intent Classifier tự huấn luyện (ONNX)**:
      - Nhận diện ý định người dùng (`APP`, `FILE`, `CALCULATOR`, `URL_WEB`, `AI`, `COMMAND`) với tốc độ siêu nhanh (`< 5ms`).
      - Hoạt động 100% offline với `Microsoft.ML.OnnxRuntime`, không phụ thuộc mạng.
    - ⚙️ **Cài đặt AI trong Cài đặt (`settings`)**:
      - Tùy chỉnh endpoint Ollama (mặc định: `http://localhost:11434`).
         - Nhấn **Khởi chạy** để bật máy chủ Ollama cục bộ ngay trong ứng dụng.
         - Chọn **Khởi chạy Ollama cùng LAILauncher** để tự bật máy chủ khi mở ứng dụng.
      - Chọn mô hình ngôn ngữ (Llama 3.2, Qwen 2.5, Mistral...), kiểm tra kết nối với một cú click.
      - Bật/tắt AI Router theo nhu cầu.


