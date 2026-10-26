# Module Huấn luyện Ranking Model (Giai đoạn 11)

Module này thu thập hành vi chọn kết quả của người dùng từ bảng `SearchHistory` trong SQLite để huấn luyện mô hình Learning-to-Rank (LTR).

## Luồng dữ liệu:
1. Export dữ liệu `SearchHistory` và `Applications` sang format CSV/JSON.
2. Trích xuất đặc trưng:
   - `FuzzyScore`
   - `LaunchCount`
   - `RecencyHours` (thời gian từ lần cuối sử dụng)
   - `QueryLength`, `TargetLength`
   - `IntentMatchBonus`
3. Huấn luyện mô hình LambdaMART / LightGBM / Small Neural Ranker.
4. Xuất file `.onnx` và nhúng vào `Launcher.Core.Search.RankingEngine`.
