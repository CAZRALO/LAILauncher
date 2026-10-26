# Thiết kế Cơ sở dữ liệu SQLite - Local AI Launcher

Hệ thống sử dụng SQLite cục bộ (được lưu tại `%AppData%/LAILauncher/launcher.db`) để lưu trữ metadata ứng dụng, lịch sử tìm kiếm và thống kê hành vi người dùng.

## Các bảng chính (Core Tables)

### 1. `Applications`
Lưu thông tin các ứng dụng đã được index (Start Menu, Desktop, PATH, UWP).
```sql
CREATE TABLE IF NOT EXISTS Applications (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    path TEXT NOT NULL UNIQUE,
    arguments TEXT,
    icon_path TEXT,
    source TEXT NOT NULL, -- 'StartMenu', 'Registry', 'Path', 'UWP'
    launch_count INTEGER DEFAULT 0,
    last_used INTEGER, -- Unix timestamp in ms
    created_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_apps_name ON Applications(name);
```

### 2. `Files`
Lưu chỉ mục các tệp tin trong các thư mục được người dùng cấu hình.
```sql
CREATE TABLE IF NOT EXISTS Files (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    path TEXT NOT NULL UNIQUE,
    extension TEXT,
    size_bytes INTEGER,
    modified_time INTEGER,
    indexed_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_files_name ON Files(name);
CREATE INDEX IF NOT EXISTS idx_files_extension ON Files(extension);
```

### 3. `SearchHistory`
Lưu vết các thao tác chọn kết quả để phục vụ tính toán tần suất và huấn luyện mô hình ranking.
```sql
CREATE TABLE IF NOT EXISTS SearchHistory (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    query TEXT NOT NULL,
    result_id TEXT NOT NULL,
    result_type TEXT NOT NULL, -- 'APP', 'FILE', 'CALC', 'AI', 'PLUGIN'
    selected_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_history_query ON SearchHistory(query);
```

### 4. `Plugins`
Quản lý trạng thái và quyền hạn của các plugin.
```sql
CREATE TABLE IF NOT EXISTS Plugins (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    version TEXT NOT NULL,
    enabled INTEGER DEFAULT 1,
    permissions TEXT, -- JSON array of granted permissions
    installed_at INTEGER NOT NULL
);
```

### 5. `Settings`
Cấu hình ứng dụng dạng key-value.
```sql
CREATE TABLE IF NOT EXISTS Settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at INTEGER NOT NULL
);
```

### 6. `AIModels`
Cấu hình các provider và mô hình AI cục bộ (Ollama, ONNX, llama.cpp).
```sql
CREATE TABLE IF NOT EXISTS AIModels (
    id TEXT PRIMARY KEY,
    provider TEXT NOT NULL, -- 'Ollama', 'LlamaCpp', 'ONNX', 'Custom'
    model_name TEXT NOT NULL,
    endpoint TEXT,
    enabled INTEGER DEFAULT 1,
    metadata TEXT
);
```
