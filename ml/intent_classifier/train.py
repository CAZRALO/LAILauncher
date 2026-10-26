import sys
sys.stdout.reconfigure(encoding='utf-8')
import json
import os
import re
import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType
import onnxruntime as ort

def generate_samples():
    data = []

    # 1. APP (Khởi chạy ứng dụng)
    app_names = [
        "chrome", "google chrome", "visual studio code", "vscode", "code", "notepad", "notepad++",
        "sublime text", "word", "microsoft word", "excel", "microsoft excel", "powerpoint",
        "spotify", "discord", "telegram", "telegram desktop", "slack", "zalo", "vlc", "vlc media player",
        "steam", "photoshop", "adobe photoshop", "illustrator", "premiere pro", "figma",
        "calculator", "máy tính", "settings", "cài đặt", "control panel", "paint", "paint 3d",
        "task manager", "quản lý tác vụ", "terminal", "windows terminal", "cmd", "command prompt",
        "powershell", "git bash", "obs studio", "docker", "docker desktop", "postman", "notion",
        "evernote", "onenote", "blender", "unity", "unity hub", "unreal engine", "edge",
        "microsoft edge", "brave", "brave browser", "firefox", "mozilla firefox", "acrobat",
        "acrobat reader", "unikey", "evkey", "everything", "file explorer", "explorer", "github desktop",
        "dbeaver", "datagrip", "intellij idea", "pycharm", "clion", "rider", "visual studio 2022",
        "utorrent", "bitcomet", "idm", "internet download manager", "zoom", "microsoft teams", "teams",
        "skype", "whatsapp", "capcut", "audacity", "virtualbox", "vmware", "winrar", "7-zip", "7zip"
    ]
    for name in app_names:
        for verb in ["", "open ", "launch ", "mở ", "chạy "]:
            data.append({"text": f"{verb}{name}".strip(), "intent": "APP"})

    # 2. FILE (Tìm kiếm tệp tin, tài liệu)
    file_stems = [
        "report", "báo cáo tài chính", "invoice_2026", "hóa đơn đỏ", "budget_q1", "kế hoạch năm 2026",
        "presentation", "slide thuyết trình", "project_proposal", "đề xuất dự án", "meeting_notes",
        "biên bản cuộc họp", "contract_signed", "hợp đồng lao động", "resume_cv", "cv_xin_viec",
        "thesis_final", "luận văn tốt nghiệp", "photo_travel", "ảnh du lịch", "screenshot_01",
        "avatar", "video_demo", "audio_record", "ghi âm", "backup_database", "source_code",
        "data_analysis", "bảng lương tháng 9", "danh sách nhân viên", "tài liệu hướng dẫn",
        "user_manual", "specification", "architecture_design", "test_report", "changelog", "readme",
        "license", "config_local", "dataset_raw", "sales_summary", "customer_list"
    ]
    for stem in file_stems:
        for ext in [".docx", ".pdf", ".xlsx", ".pptx", ".txt"]:
            filename = f"{stem}{ext}"
            for p in ["", "file: ", "find ", "tìm ", "open file "]:
                data.append({"text": f"{p}{filename}".strip(), "intent": "FILE"})

    # 3. CALCULATOR (Tính toán, công thức, chuyển đổi)
    calc_samples = [
        "25 * 4", "1200 / 12", "(45 + 55) * 2", "15% of 200", "sqrt(144)", "500 - 128",
        "2 ^ 10", "sin(pi / 2)", "cos(0)", "tan(45)", "log(100)", "ln(e)", "abs(-99)",
        "100 km to miles", "1024 mb to gb", "1 gb to mb", "37 c to f", "100 f to c",
        "2 kg to lbs", "5 lbs to kg", "60 s to min", "120 min to hours", "24 hours to days",
        "0xFF to dec", "255 to hex", "0b1010 to dec", "10 to bin", "15 + 35", "99 * 5",
        "50 USD to VND", "100 EUR to USD", "calc 50 + 50", "calc: 10 * 20", "= 100 * 5",
        "500 * (1 + 0.1)", "sqrt(256) + 10", "2^8 + 2^4", "1500000 * 10%", "20% of 5000000",
        "tính 45 * 2 + 10", "tính 100 / 5", "tính căn bậc hai của 81", "1000 m to km",
        "5 feet to cm", "10 inch to cm", "100 cm to m", "1 gallon to liters", "1000 ml to liters",
        "0x1A to dec", "16 to hex", "0b1111 to dec", "8 to bin", "1000 - 450", "250 / 5",
        "18 * 18", "75 + 25 - 30", "12 * (34 + 56)", "calc: 999 / 3", "= 45 * 2", "1000 * 0.05"
    ]
    for s in calc_samples:
        data.append({"text": s, "intent": "CALCULATOR"})
        if not s.startswith("calc"):
            data.append({"text": f"calc {s}", "intent": "CALCULATOR"})

    # 4. URL_WEB (Địa chỉ web, tìm kiếm trang web)
    web_samples = [
        "https://github.com", "http://github.com/dotnet", "google.com", "www.google.com",
        "youtube.com/watch", "https://youtube.com", "news.ycombinator.com", "translate.google.com",
        "facebook.com", "reddit.com/r/csharp", "twitter.com", "x.com", "stackoverflow.com",
        "stackoverflow.com/questions", "vnexpress.net", "dantri.com.vn", "tuoitre.vn",
        "wikipedia.org", "en.wikipedia.org/wiki/AI", "chatgpt.com", "claude.ai", "bing.com",
        "duckduckgo.com", "amazon.com", "shopee.vn", "lazada.vn", "tiki.vn",
        "http://localhost:3000", "http://localhost:5000", "http://localhost:8080", "http://127.0.0.1:8000",
        "cnn.com", "bbc.com", "medium.com", "dev.to", "npmjs.com/package/axios",
        "nuget.org/packages", "pypi.org/project/torch", "learn.microsoft.com", "docs.docker.com",
        "gitlab.com", "bitbucket.org", "kaggle.com", "huggingface.co", "netflix.com", "spotify.com/web",
        "https://viblo.asia", "tinhte.vn", "voz.vn", "genk.vn", "theverge.com", "techcrunch.com",
        "https://antigravity.google.com", "vercel.app", "pages.dev", "github.io"
    ]
    for s in web_samples:
        data.append({"text": s, "intent": "URL_WEB"})
        if not s.startswith("http"):
            data.append({"text": f"https://{s}", "intent": "URL_WEB"})

    # 5. AI (Hỏi đáp LLM, tóm tắt, giải thích, dịch thuật)
    ai_templates = [
        "ai: tóm tắt nội dung vừa copy", "ai: giải thích đoạn code trên", "ai: dịch sang tiếng anh",
        "ai: viết email xin nghỉ phép", "ai: giải thích thuật toán dijkstra", "ai: viết hàm regex email",
        "ai summarize clipboard", "ai explain code in clipboard", "ai translate to vietnamese",
        "ai: tạo dàn ý bài thuyết trình", "ai: gợi ý tên cho dự án mới", "ai: phân tích độ phức tạp thuật toán",
        "? explain quantum computing", "? cách sửa lỗi null reference exception", "? what is dependency injection",
        "hỏi AI cách tối ưu truy vấn SQL", "hỏi AI cách cấu hình nginx reverse proxy", "hỏi AI cách deploy docker",
        "ai: hướng dẫn cài đặt python trên windows", "ai: so sánh c# và rust", "ai: viết unit test cho hàm tính tổng",
        "ai: kiểm tra lỗi ngữ pháp đoạn văn bản sau", "ai: viết bài đăng facebook quảng cáo sản phẩm",
        "ai draft email to manager", "ai rewrite professionally", "ai review my resume",
        "ai: tại sao chương trình bị tràn bộ nhớ stackoverflow", "ai: hướng dẫn sử dụng git rebase",
        "ask AI how to implement binary search", "ask AI to generate mock data json",
        "ai: tóm tắt bài báo này", "ai summarize this text", "ai explain this error message",
        "ai: hướng dẫn giải bài toán quy hoạch động", "ai: viết thơ về mùa thu hà nội",
        "ai: sinh câu truy vấn mongodb", "ai: tạo bảng dữ liệu mẫu postgresql", "ai translate clipboard to japanese",
        "? how does transformer neural network work", "? what is learning to rank", "ai: viết kịch bản video youtube"
    ]
    for s in ai_templates:
        data.append({"text": s, "intent": "AI"})
        clean = re.sub(r"^(ai:\s*|\?\s*|ask ai to\s*|hỏi ai cách\s*)", "", s, flags=re.IGNORECASE)
        data.append({"text": f"ai {clean}", "intent": "AI"})
        data.append({"text": f"hỏi AI {clean}", "intent": "AI"})

    # 6. COMMAND (Lệnh terminal, powershell, cmd)
    cmd_templates = [
        "ipconfig", "ipconfig /all", "ping google.com", "ping 8.8.8.8", "ping 1.1.1.1",
        "shutdown -s -t 3600", "shutdown /r /t 0", "Get-Process", "Get-Service", "git status",
        "git pull", "git push origin main", "git checkout -b feature", "git commit -m 'update'",
        "npm run dev", "npm install", "npm start", "dotnet build", "dotnet run", "dotnet test",
        "dir /s", "dir /b", "ls -la", "netstat -ano", "tasklist", "taskkill /f /im notepad.exe",
        "systeminfo", "traceroute 8.8.8.8", "tracert google.com", "code .", "mkdir test_folder",
        "rmdir /s /q temp", "rm -rf node_modules", "docker ps", "docker compose up -d",
        "docker stop $(docker ps -a -q)", "curl https://api.github.com", "curl -I google.com",
        "chkdsk C:", "sfc /scannow", "cls", "clear", "echo hello world", "whoami", "hostname",
        "route print", "arp -a", "wmic cpu get name", "explorer .", "start chrome", "python app.py",
        "pip install -r requirements.txt", "uvicorn main:app --reload", "flutter run", "cargo build"
    ]
    for s in cmd_templates:
        data.append({"text": f"> {s}", "intent": "COMMAND"})
        data.append({"text": f"cmd {s}", "intent": "COMMAND"})
        data.append({"text": f"ps {s}", "intent": "COMMAND"})

    return data

def train_and_export():
    dataset = generate_samples()
    print(f"Total samples generated: {len(dataset)}")

    data_dir = os.path.dirname(__file__)
    dataset_path = os.path.join(data_dir, "dataset.json")
    with open(dataset_path, "w", encoding="utf-8") as f:
        json.dump(dataset, f, ensure_ascii=False, indent=2)

    texts = [item["text"].lower() for item in dataset]
    intents = [item["intent"] for item in dataset]

    # Map labels to integer IDs
    label_list = sorted(list(set(intents)))
    label2id = {lbl: idx for idx, lbl in enumerate(label_list)}
    id2label = {idx: lbl for idx, lbl in enumerate(label_list)}
    y = np.array([label2id[lbl] for lbl in intents], dtype=np.int64)

    # Train / Test split
    X_train_raw, X_test_raw, y_train, y_test = train_test_split(
        texts, y, test_size=0.15, random_state=42, stratify=y
    )

    # Vectorizer
    vectorizer = TfidfVectorizer(
        ngram_range=(1, 2),
        min_df=1,
        max_features=2500,
        token_pattern=r"(?u)\b\w+\b|[><=?:/\-]+"
    )
    X_train = vectorizer.fit_transform(X_train_raw).toarray().astype(np.float32)
    X_test = vectorizer.transform(X_test_raw).toarray().astype(np.float32)

    # Classifier
    clf = LogisticRegression(C=5.0, max_iter=1000, random_state=42)
    clf.fit(X_train, y_train)

    y_pred = clf.predict(X_test)
    print("\n=== Classification Report on Test Set ===")
    print(classification_report(y_test, y_pred, target_names=label_list, digits=4))

    # Save vocab and labels for C#
    vocab = vectorizer.vocabulary_
    idf = vectorizer.idf_.tolist()
    vocab_data = {
        "vocab": {k: int(v) for k, v in vocab.items()},
        "idf": idf,
        "labels": label_list,
        "ngram_max": 2
    }
    vocab_path = os.path.join(data_dir, "vocab.json")
    with open(vocab_path, "w", encoding="utf-8") as f:
        json.dump(vocab_data, f, ensure_ascii=False, indent=2)
    print(f"Saved vocabulary ({len(vocab)} terms) to {vocab_path}")

    # Convert LogisticRegression to ONNX
    initial_type = [("features", FloatTensorType([None, X_train.shape[1]]))]
    onnx_model = convert_sklearn(clf, initial_types=initial_type, target_opset=15)

    onnx_path = os.path.join(data_dir, "intent_classifier.onnx")
    with open(onnx_path, "wb") as f:
        f.write(onnx_model.SerializeToString())
    print(f"Exported ONNX model ({os.path.getsize(onnx_path)} bytes) to {onnx_path}")

    # Test with onnxruntime in Python
    session = ort.InferenceSession(onnx_path)
    test_queries = [
        "launch google chrome",
        "find invoice_2026.pdf",
        "25 * 4 + 10",
        "https://github.com",
        "ai: giải thích thuật toán quicksort",
        "> ping 8.8.8.8",
        "tính 100 km to miles",
        "mở visual studio code",
        "hỏi AI cách deploy docker",
        "> ipconfig /all"
    ]
    print("\n=== Sample ONNX Inference Verification ===")
    for q in test_queries:
        feat = vectorizer.transform([q.lower()]).toarray().astype(np.float32)
        outputs = session.run(None, {"features": feat})
        pred_label_id = outputs[0][0]
        pred_label = id2label[pred_label_id]
        probs = outputs[1][0]
        max_prob = probs[pred_label_id]
        print(f"'{q}' -> {pred_label} (Confidence: {max_prob:.4f})")

    # Also copy to Launcher.AI Models directory
    target_models_dir = os.path.abspath(os.path.join(data_dir, "..", "..", "src", "Launcher.AI", "Models"))
    os.makedirs(target_models_dir, exist_ok=True)
    import shutil
    shutil.copy2(onnx_path, os.path.join(target_models_dir, "intent_classifier.onnx"))
    shutil.copy2(vocab_path, os.path.join(target_models_dir, "vocab.json"))
    print(f"Successfully deployed ONNX model and vocab to {target_models_dir}")

if __name__ == "__main__":
    train_and_export()
