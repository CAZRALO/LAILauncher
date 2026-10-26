# Kiến trúc hệ thống - Local AI Launcher

## 1. Triết lý thiết kế (Design Philosophy)
- **Fast Core**: Tốc độ phản hồi hotkey < 100ms, tìm kiếm ứng dụng & file < 50ms.
- **Offline-first & Privacy-first**: Toàn bộ chỉ mục, lịch sử, dữ liệu RAG và mô hình AI chạy cục bộ trên máy người dùng, không truyền dữ liệu ra ngoài.
- **AI Non-blocking**: LLM và các tác vụ AI phức tạp chạy bất đồng bộ (streaming), không làm nghẽn luồng core search.
- **Plugin-first**: Dễ dàng mở rộng thông qua `Launcher.SDK`.

## 2. Sơ đồ các tầng kiến trúc

```
+-------------------------------------------------------------+
|                      Launcher.App (WPF)                     |
|  - MainWindow (UI/UX)          - GlobalHotkey (Alt+Space)   |
|  - TrayIcon & Notifications    - Theme & Settings UI        |
+-------------------------------------------------------------+
                              |
+-------------------------------------------------------------+
|                      Launcher.Core                          |
|  - QueryPipeline (Debounce)    - FuzzyMatcher / SearchEngine|
|  - RankingEngine (Recency/Freq)- ActionRunner (Safety Guard)|
+-------------------------------------------------------------+
         |                      |                     |
+-----------------+   +------------------+   +----------------+
| Launcher.Indexer|   | Launcher.Database|   |  Launcher.AI   |
| - StartMenuScan |   | - SQLite Repo    |   | - Router (ONNX)|
| - FileWatcher   |   | - SearchHistory  |   | - Local LLM    |
| - AppRegistry   |   | - AppMetadata    |   |   (Ollama)     |
+-----------------+   +------------------+   +----------------+
                              |
+-------------------------------------------------------------+
|                        Launcher.SDK                         |
|  - IPlugin, IQueryHandler, QueryResult, ResultAction        |
+-------------------------------------------------------------+
```

## 3. Quy trình xử lý Query (Query Pipeline)
1. Người dùng gõ phím vào ô tìm kiếm -> Bộ đệm debounce 30-50ms.
2. `QueryPipeline` kích hoạt:
   - Truy vấn song song các Provider đã đăng ký (`AppIndexer`, `Calculator`, `FileIndexer`, `Plugins`).
   - `IntentClassifier` (ONNX) xác định intent (APP, FILE, CALC, AI, COMMAND...).
3. `RankingEngine` tính điểm: `FinalScore = (FuzzyScore * 0.6) + (UsageScore * 0.3) + (RecencyScore * 0.1)`.
4. Kết quả được render lên UI.
5. Khi người dùng nhấn Enter -> `ActionRunner` thực thi hành động tương ứng (yêu cầu xác nhận nếu là action nhạy cảm).
