// LAILauncher - Web Core Controller & Interactive Engine
import { Icons, renderIcon } from './icons.js';

// ============================================================================
// 1. COMPREHENSIVE LAUNCHER DATABASE
// ============================================================================

const Database = [
  // Applications
  {
    id: 'app-vscode',
    title: 'Visual Studio Code',
    subtitle: 'C:\\Users\\Admin\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe',
    category: 'Ứng dụng',
    type: 'app',
    icon: 'app',
    action: 'Mở Visual Studio Code',
    tags: ['code', 'editor', 'ide', 'vs code', 'dev']
  },
  {
    id: 'app-vs2024',
    title: 'Visual Studio 2024 Preview',
    subtitle: 'IDE phát triển .NET 8, C#, C++, WPF và Windows Native',
    category: 'Ứng dụng',
    type: 'app',
    icon: 'app',
    action: 'Mở Visual Studio',
    tags: ['visual studio', 'ide', 'dotnet', 'csharp', 'wpf']
  },
  {
    id: 'app-chrome',
    title: 'Google Chrome',
    subtitle: 'Trình duyệt web tốc độ cao của Google',
    category: 'Ứng dụng',
    type: 'app',
    icon: 'google',
    action: 'Mở Google Chrome',
    tags: ['google', 'chrome', 'browser', 'web', 'internet']
  },
  {
    id: 'app-terminal',
    title: 'Windows Terminal / PowerShell',
    subtitle: 'Giao diện dòng lệnh nâng cao (PowerShell 7.4 & Command Prompt)',
    category: 'Ứng dụng',
    type: 'terminal',
    icon: 'terminal',
    action: 'Mở Windows Terminal',
    tags: ['terminal', 'powershell', 'cmd', 'command', 'shell', 'bash']
  },
  {
    id: 'app-spotify',
    title: 'Spotify',
    subtitle: 'Phát nhạc và podcast trực tuyến',
    category: 'Ứng dụng',
    type: 'app',
    icon: 'app',
    action: 'Mở Spotify',
    tags: ['spotify', 'music', 'audio', 'nhac']
  },
  {
    id: 'app-obsidian',
    title: 'Obsidian Knowledge Base',
    subtitle: 'Sổ tay ghi chú Markdown cục bộ & đồ thị liên kết',
    category: 'Ứng dụng',
    type: 'app',
    icon: 'file',
    action: 'Mở Obsidian',
    tags: ['obsidian', 'notes', 'markdown', 'ghi chu']
  },

  // System Controls (sys / win)
  {
    id: 'sys-toggle-dark',
    title: 'Toggle Windows / App Dark & Light Mode',
    subtitle: 'Chuyển đổi giao diện Sáng / Tối toàn hệ thống và LAILauncher',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'sun',
    action: 'Chuyển Dark / Light Mode',
    tags: ['dark mode', 'light mode', 'che do toi', 'che do sang', 'theme', 'sys', 'win']
  },
  {
    id: 'sys-vol-up',
    title: 'Volume Up (+5%)',
    subtitle: 'Tăng âm lượng loa hệ thống',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'volume',
    action: 'Tăng âm lượng',
    tags: ['volume', 'am luong', 'tang am luong', 'audio', 'sys', 'win']
  },
  {
    id: 'sys-vol-down',
    title: 'Volume Down (-5%)',
    subtitle: 'Giảm âm lượng loa hệ thống',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'volume',
    action: 'Giảm âm lượng',
    tags: ['volume', 'am luong', 'giam am luong', 'audio', 'sys', 'win']
  },
  {
    id: 'sys-vol-mute',
    title: 'Toggle Mute / Unmute',
    subtitle: 'Bật / tắt tiếng âm thanh hệ thống',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'volume',
    action: 'Bật/tắt tiếng',
    tags: ['mute', 'tat am', 'am luong', 'sys', 'win']
  },
  {
    id: 'sys-bright-up',
    title: 'Brightness Up (+10%)',
    subtitle: 'Tăng độ sáng màn hình hiển thị',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'brightness',
    action: 'Tăng độ sáng',
    tags: ['brightness', 'do sang', 'tang do sang', 'monitor', 'sys', 'win']
  },
  {
    id: 'sys-bright-down',
    title: 'Brightness Down (-10%)',
    subtitle: 'Giảm độ sáng màn hình hiển thị',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'brightness',
    action: 'Giảm độ sáng',
    tags: ['brightness', 'do sang', 'giam do sang', 'monitor', 'sys', 'win']
  },
  {
    id: 'sys-wifi-flyout',
    title: 'Show Available Wi-Fi Networks',
    subtitle: 'Mở bảng điều khiển kết nối mạng không dây Wi-Fi',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'wifi',
    action: 'Mở danh sách Wi-Fi',
    tags: ['wifi', 'network', 'mang', 'ket noi', 'sys', 'win']
  },
  {
    id: 'sys-bluetooth',
    title: 'Bluetooth & Devices Settings',
    subtitle: 'Cài đặt và ghép nối thiết bị ngoại vi Bluetooth',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'bluetooth',
    action: 'Mở cài đặt Bluetooth',
    tags: ['bluetooth', 'thiet bi', 'pairing', 'sys', 'win']
  },
  {
    id: 'sys-hotspot-toggle',
    title: 'Toggle Mobile Hotspot',
    subtitle: 'Bật / Tắt trạm phát sóng Wi-Fi di động trực tiếp không cần mở Settings',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'hotspot',
    action: 'Bật/Tắt Hotspot',
    tags: ['hotspot', 'mobile hotspot', 'phat wifi', 'phat mang', 'tethering', 'diem phat song', 'sys', 'win']
  },
  {
    id: 'sys-battery-saver',
    title: 'Toggle Energy Saver Mode (Tiết kiệm pin)',
    subtitle: 'Bật / Tắt chế độ tiết kiệm pin trực tiếp không cần mở Settings',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'leaf',
    action: 'Chuyển chế độ Tiết kiệm pin',
    tags: ['battery', 'pin', 'tiet kiem pin', 'energy saver', 'power saver', 'tiet kiem nang luong', 'power', 'sys', 'win']
  },
  {
    id: 'sys-taskbar-pos',
    title: 'Toggle Taskbar Alignment (Căn Giữa / Trái)',
    subtitle: 'Chuyển đổi vị trí căn lề thanh Taskbar giữa Căn Giữa và Căn Trái',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'taskbar',
    action: 'Đổi vị trí Taskbar',
    tags: ['taskbar', 'vi tri taskbar', 'can le taskbar', 'taskbar align', 'explorer', 'sys', 'win']
  },
  {
    id: 'sys-screenshot',
    title: 'Chụp ảnh màn hình (Screenshot)',
    subtitle: 'Chụp ảnh màn hình và sao chép vào Clipboard (lưu tệp ảnh)',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'screenshot',
    action: 'Chụp màn hình',
    tags: ['screenshot', 'chup man hinh', 'chup anh man hinh', 'snip', 'snipping', 'prtscn', 'capture screen', 'sys', 'win']
  },
  {
    id: 'sys-background-procs',
    title: 'Xem trình chạy ngầm & Tắt ứng dụng ngốn RAM',
    subtitle: 'Hiển thị danh sách các tiến trình chạy ngầm và giải phóng bộ nhớ RAM',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'process',
    action: 'Quản lý tác vụ',
    tags: ['trinh chay ngam', 'chay ngam', 'ngam', 'tien trinh', 'kill', 'proc', 'task manager', 'taskmgr', 'tat ung dung', 'dong ung dung', 'sys', 'win']
  },
  {
    id: 'sys-restart-explorer',
    title: 'Restart Windows Explorer',
    subtitle: 'Khởi động lại tiến trình explorer.exe để làm mới giao diện',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'settings',
    action: 'Khởi động lại Explorer',
    tags: ['explorer', 'restart', 'khoi dong lai', 'sys', 'win']
  },
  {
    id: 'sys-lock',
    title: 'Lock Workstation',
    subtitle: 'Khóa màn hình làm việc (Win + L)',
    category: 'Hệ thống',
    type: 'sys',
    icon: 'lock',
    action: 'Khóa máy tính',
    tags: ['lock', 'khoa man hinh', 'security', 'sys', 'win']
  },

  // Files & Directories (Direct Path)
  {
    id: 'file-readme',
    title: 'README.md',
    subtitle: 'd:\\AT\\project\\LAILauncher\\README.md (Tài liệu giới thiệu dự án)',
    category: 'Tệp tin',
    type: 'file',
    icon: 'file',
    action: 'Mở tệp README',
    extension: '.md',
    tags: ['readme', 'docs', 'tai lieu', 'lailauncher', 'doc:']
  },
  {
    id: 'file-finance-q3',
    title: 'Bao_cao_tai_chinh_Q3_2024.xlsx',
    subtitle: 'd:\\Documents\\Finance\\Bao_cao_tai_chinh_Q3_2024.xlsx • 2.4 MB',
    category: 'Tài liệu',
    type: 'file',
    icon: 'file',
    action: 'Mở bảng tính Excel',
    extension: '.xlsx',
    tags: ['báo cáo', 'bao cao', 'tài chính', 'tai chinh', 'doanh thu', 'lợi nhuận', 'kế toán', 'ngân sách', 'budget', 'q3', 'finance', 'excel', 'sheet:']
  },
  {
    id: 'file-contract-2025',
    title: 'Hop_dong_cung_cap_dich_vu_phan_mem_2025.docx',
    subtitle: 'd:\\Documents\\Legal\\Hop_dong_cung_cap_dich_vu_phan_mem_2025.docx • 420 KB',
    category: 'Tài liệu',
    type: 'file',
    icon: 'file',
    action: 'Mở hợp đồng Word',
    extension: '.docx',
    tags: ['hợp đồng', 'hop dong', 'pháp lý', 'thỏa thuận', 'contract', 'agreement', 'dịch vụ', 'word', 'doc:']
  },
  {
    id: 'file-strategy-copilot',
    title: 'Ke_hoach_chien_luoc_AI_Copilot_2025.pptx',
    subtitle: 'd:\\Documents\\Presentations\\Ke_hoach_chien_luoc_AI_Copilot_2025.pptx • 8.5 MB',
    category: 'Tài liệu',
    type: 'file',
    icon: 'file',
    action: 'Mở bài thuyết trình PowerPoint',
    extension: '.pptx',
    tags: ['kế hoạch', 'ke hoach', 'chiến lược', 'thuyết trình', 'slide', 'presentation', 'copilot', 'ai', 'slide:']
  },
  {
    id: 'file-thesis-ai',
    title: 'Luan_van_NPU_va_Local_LLM_Architecture.pdf',
    subtitle: 'd:\\Documents\\Research\\Luan_van_NPU_va_Local_LLM_Architecture.pdf • 4.8 MB',
    category: 'Tài liệu',
    type: 'file',
    icon: 'file',
    action: 'Mở tài liệu nghiên cứu PDF',
    extension: '.pdf',
    tags: ['luận văn', 'luan van', 'nghiên cứu', 'đồ án', 'thesis', 'pdf', 'npu', 'llm', 'onnx', 'pdf:', 'doc:']
  },
  {
    id: 'file-photo-sunset',
    title: 'Sunset_DaNang_Beach_Summer_2024.jpg',
    subtitle: 'd:\\Pictures\\DaNang\\Sunset_DaNang_Beach_Summer_2024.jpg • 3.2 MB',
    category: 'Hình ảnh',
    type: 'file',
    icon: 'file',
    action: 'Xem hình ảnh',
    extension: '.jpg',
    tags: ['ảnh', 'anh', 'hình', 'hinh', 'hoàng hôn', 'hoang hon', 'bãi biển', 'bien', 'sunset', 'beach', 'photo', 'picture', 'img:']
  },
  {
    id: 'file-code-querypipeline',
    title: 'QueryPipeline.cs',
    subtitle: 'd:\\AT\\project\\LAILauncher\\src\\Launcher.Core\\Search\\QueryPipeline.cs',
    category: 'Mã nguồn',
    type: 'file',
    icon: 'file',
    action: 'Mở trong Visual Studio Code',
    extension: '.cs',
    tags: ['code', 'csharp', 'mã nguồn', 'pipeline', 'search', 'ranking', 'code:']
  },
  {
    id: 'file-code-intent',
    title: 'IntentClassifier.cs',
    subtitle: 'd:\\AT\\project\\LAILauncher\\src\\Launcher.AI\\IntentClassifier.cs (ONNX Model)',
    category: 'Mã nguồn',
    type: 'file',
    icon: 'file',
    action: 'Mở trong Visual Studio Code',
    extension: '.cs',
    tags: ['code', 'csharp', 'onnx', 'intent', 'classifier', 'ai', 'mã nguồn', 'code:']
  },
  {
    id: 'file-solution',
    title: 'LAILauncher.sln',
    subtitle: 'd:\\AT\\project\\LAILauncher\\LAILauncher.sln (Visual Studio Solution)',
    category: 'Tệp tin',
    type: 'file',
    icon: 'file',
    action: 'Mở Solution',
    extension: '.sln',
    tags: ['sln', 'solution', 'csharp', 'lailauncher', 'code:']
  },
  {
    id: 'dir-project',
    title: 'LAILauncher Root Folder',
    subtitle: 'd:\\AT\\project\\LAILauncher\\ (Thư mục mã nguồn)',
    category: 'Thư mục',
    type: 'file',
    icon: 'folder',
    action: 'Mở thư mục trong Explorer',
    tags: ['d:\\', 'folder', 'workspace', 'thu muc', 'c:\\', 'dir:', 'folder:']
  },

  // AI Assistant Quick Prompts
  {
    id: 'ai-explain',
    title: 'AI: Giải thích đoạn mã nguồn trong Clipboard',
    subtitle: 'Chạy phân tích AI cục bộ Ollama (Llama-3 / Phi-3) không cần Internet',
    category: 'Trợ lý AI',
    type: 'ai',
    icon: 'sparkles',
    action: 'Chạy phân tích AI',
    tags: ['ai', 'ollama', 'explain', 'giai thich', 'clipboard', 'hoi ai', '?']
  },
  {
    id: 'ai-summarize',
    title: 'AI: Tóm tắt văn bản và trích xuất ý chính',
    subtitle: 'Tạo bản tóm tắt nhanh gạch đầu dòng với mô hình ngôn ngữ Onnx/Ollama',
    category: 'Trợ lý AI',
    type: 'ai',
    icon: 'sparkles',
    action: 'Tóm tắt nội dung',
    tags: ['ai', 'summarize', 'tom tat', 'y chinh', '?']
  }
];

// ============================================================================
// 2. THEMES & COLOR SPECS
// ============================================================================

export const Themes = [
  { id: 'win-fluent', name: 'Windows Fluent Blue', color: '#3b82f6', os: 'windows' },
  { id: 'linux-yaru', name: 'Ubuntu Yaru Orange', color: '#e95420', os: 'linux' },
  { id: 'linux-arch', name: 'Arch / Fedora Cyan', color: '#00aad4', os: 'linux' },
  { id: 'liquid-aurora', name: 'Liquid Aurora Glass', color: '#8b5cf6', os: 'windows' },
  { id: 'cyber-neon', name: 'Cyberpunk Neon', color: '#f43f5e', os: 'windows' },
  { id: 'nord-frost', name: 'Nordic Frost Slate', color: '#88c0d0', os: 'linux' },
  { id: 'dracula', name: 'Dracula Nocturne', color: '#bd93f9', os: 'linux' },
  { id: 'emerald', name: 'Emerald Jade Mint', color: '#10b981', os: 'windows' },
  { id: 'sunset', name: 'Sunset Dusk Coral', color: '#fb923c', os: 'windows' }
];

export const AccentColors = [
  { name: 'Windows Blue', hex: '#0078D7' },
  { name: 'Sky Blue', hex: '#0284C7' },
  { name: 'Electric Cyan', hex: '#06B6D4' },
  { name: 'Teal Lagoon', hex: '#0D9488' },
  { name: 'Arctic Mint', hex: '#14B8A6' },
  { name: 'Sky Frost', hex: '#38BDF8' },
  { name: 'Emerald', hex: '#10B981' },
  { name: 'Deep Forest', hex: '#059669' },
  { name: 'Spring Green', hex: '#22C55E' },
  { name: 'Vibrant Lime', hex: '#84CC16' },
  { name: 'Yellow Gold', hex: '#EAB308' },
  { name: 'Amber Gold', hex: '#F59E0B' },
  { name: 'Vibrant Orange', hex: '#F97316' },
  { name: 'Coral Rose', hex: '#FB7185' },
  { name: 'Crimson Red', hex: '#E11D48' },
  { name: 'Neon Rose', hex: '#F43F5E' },
  { name: 'Hot Pink', hex: '#EC4899' },
  { name: 'Fuchsia Magenta', hex: '#D946EF' },
  { name: 'Purple Violet', hex: '#A855F7' },
  { name: 'Royal Orchid', hex: '#8B5CF6' },
  { name: 'Modern Indigo', hex: '#6366F1' },
  { name: 'Deep Iris', hex: '#4F46E5' },
  { name: 'Slate Steel', hex: '#64748B' },
  { name: 'Neutral Zinc', hex: '#71717A' }
];

export const Wallpapers = [
  { id: 'aurora', name: 'Cosmic Aurora', class: 'wallpaper-aurora' },
  { id: 'bloom', name: 'Windows Bloom', class: 'wallpaper-bloom' },
  { id: 'ubuntu', name: 'Ubuntu Heron', class: 'wallpaper-ubuntu' },
  { id: 'alpine', name: 'Alpine Frost', class: 'wallpaper-alpine' },
  { id: 'cyber', name: 'Cyber Grid', class: 'wallpaper-cyber' }
];

// ============================================================================
// 3. APPLICATION STATE
// ============================================================================

const State = {
  os: 'windows',       // 'windows' | 'linux'
  mode: 'dark',        // 'dark' | 'light'
  theme: 'win-fluent',
  wallpaper: 'aurora',
  customAccent: null,  // Custom manual accent override (HEX)
  glassOpacity: 0.82,
  glassBlur: 28,
  
  query: '',
  selectedIndex: 0,
  currentResults: [],
  
  isFlyoutOpen: false,
  isAiDrawerOpen: false,
  isSettingsOpen: false,
  
  prefixMode: null,     // null | 'terminal' | 'web' | 'ai' | 'calc' | 'sys'
  
  // Window Position & Dragging State
  windowOffsetX: 0,
  windowOffsetY: 0,
  hasCustomPosition: false,

  // Direct Inline System Controls (Windows Search 2025 style)
  isHotspotOn: false,
  isEnergySaverOn: false,
  taskbarAlign: 'center', // 'center' | 'left'
  backgroundProcesses: [
    { id: 'p1', name: 'Google Chrome', exe: 'chrome.exe', pid: 14208, ram: '1.42 GB', ramBytes: 1524629504, cpu: '3.4%', icon: 'google' },
    { id: 'p2', name: 'Visual Studio Code', exe: 'Code.exe', pid: 8940, ram: '864 MB', ramBytes: 905969664, cpu: '1.2%', icon: 'app' },
    { id: 'p3', name: 'Ollama AI (Llama-3-8B)', exe: 'ollama_llama_server.exe', pid: 21304, ram: '4.15 GB', ramBytes: 4456448000, cpu: '0.8%', icon: 'sparkles' },
    { id: 'p4', name: 'Discord', exe: 'Discord.exe', pid: 16720, ram: '428 MB', ramBytes: 448790528, cpu: '0.5%', icon: 'app' },
    { id: 'p5', name: 'Spotify Music', exe: 'Spotify.exe', pid: 11044, ram: '294 MB', ramBytes: 308281344, cpu: '0.3%', icon: 'volume' },
    { id: 'p6', name: 'Steam Client', exe: 'steamwebhelper.exe', pid: 5412, ram: '210 MB', ramBytes: 220200960, cpu: '0.1%', icon: 'app' },
    { id: 'p7', name: 'Docker Desktop Engine', exe: 'com.docker.backend.exe', pid: 3208, ram: '2.18 GB', ramBytes: 2340945920, cpu: '2.0%', icon: 'app' },
    { id: 'p8', name: 'Microsoft Teams', exe: 'ms-teams.exe', pid: 18452, ram: '512 MB', ramBytes: 536870912, cpu: '0.7%', icon: 'app' }
  ]
};

// ============================================================================
// 4. CORE CONTROLLER & EVENT WIRING
// ============================================================================

export function initLauncher() {
  loadSavedPreferences();
  applyAppearance();
  renderThemeSwatches();
  renderAccentChips();
  renderWallpaperSwatches();
  setupEventListeners();
  updateClock();
  setInterval(updateClock, 1000);
  updatePositionUI();
  initQuickSettingsUI();
  
  // Initial Search Run
  filterQuery('');
  
  showToast('Đã khởi tạo LAILauncher Web (Liquid Glass OS Studio)! Nhấn Alt+Space để tương tác.');
}

export function initQuickSettingsUI() {
  const iconHotspot = document.getElementById('pill-icon-hotspot');
  const iconEnergy = document.getElementById('pill-icon-energy');
  const iconTaskbar = document.getElementById('pill-icon-taskbar');
  const iconScreenshot = document.getElementById('pill-icon-screenshot');
  const iconProcesses = document.getElementById('pill-icon-processes');
  const iconWifi = document.getElementById('pill-icon-wifi');
  const iconBluetooth = document.getElementById('pill-icon-bluetooth');

  if (iconHotspot) iconHotspot.innerHTML = renderIcon('hotspot');
  if (iconEnergy) iconEnergy.innerHTML = renderIcon('leaf');
  if (iconTaskbar) iconTaskbar.innerHTML = renderIcon('taskbar');
  if (iconScreenshot) iconScreenshot.innerHTML = renderIcon('screenshot');
  if (iconProcesses) iconProcesses.innerHTML = renderIcon('process');
  if (iconWifi) iconWifi.innerHTML = renderIcon('wifi');
  if (iconBluetooth) iconBluetooth.innerHTML = renderIcon('bluetooth');

  updateHotspotUI();
  updateEnergySaverUI();
  updateTaskbarUI();
  updateProcessCountUI();
}

export function updateHotspotUI() {
  const pill = document.getElementById('pill-hotspot');
  if (pill) {
    pill.classList.toggle('active', State.isHotspotOn);
  }
}

export function updateEnergySaverUI() {
  const pill = document.getElementById('pill-energy');
  const label = document.getElementById('pill-text-energy');
  if (pill) {
    pill.classList.toggle('active', State.isEnergySaverOn);
  }
  if (label) {
    label.textContent = State.isEnergySaverOn ? 'Tiết kiệm pin: Bật' : 'Tiết kiệm pin';
  }
}

export function updateTaskbarUI() {
  const pill = document.getElementById('pill-taskbar');
  const label = document.getElementById('pill-text-taskbar');
  if (label) {
    label.textContent = State.taskbarAlign === 'center' ? 'Taskbar: Giữa' : 'Taskbar: Trái';
  }
  if (pill) {
    pill.classList.toggle('active', State.taskbarAlign === 'left');
  }
}

export function updateProcessCountUI() {
  const countEl = document.getElementById('pill-proc-count');
  if (countEl) {
    countEl.textContent = State.backgroundProcesses.length.toString();
  }
}

export function toggleHotspot() {
  State.isHotspotOn = !State.isHotspotOn;
  updateHotspotUI();
  showToast(State.isHotspotOn 
    ? '📡 Mobile Hotspot: ĐÃ BẬT (WinRT Headless NetworkOperatorTetheringManager)' 
    : '📡 Mobile Hotspot: ĐÃ TẮT');
}

export function toggleEnergySaver() {
  State.isEnergySaverOn = !State.isEnergySaverOn;
  updateEnergySaverUI();
  showToast(State.isEnergySaverOn 
    ? '🌿 Tiết kiệm pin (Energy Saver): ĐÃ BẬT (Power Scheme Silent Toggle)' 
    : '🌿 Tiết kiệm pin: ĐÃ TẮT (Chế độ Cân bằng Balanced)');
}

export function toggleTaskbarAlign() {
  State.taskbarAlign = State.taskbarAlign === 'center' ? 'left' : 'center';
  updateTaskbarUI();
  showToast(State.taskbarAlign === 'center'
    ? '📍 Taskbar: CĂN GIỮA (TaskbarAl = 1 • Restart Explorer)'
    : '📍 Taskbar: CĂN TRÁI (TaskbarAl = 0 • Restart Explorer)');
}

export function killProcess(pid) {
  const idx = State.backgroundProcesses.findIndex(p => p.pid === pid);
  if (idx !== -1) {
    const killed = State.backgroundProcesses.splice(idx, 1)[0];
    updateProcessCountUI();
    showToast(`🛑 Đã kết thúc tiến trình ${killed.name} (PID: ${killed.pid}) — Đã giải phóng ${killed.ram} RAM!`);
    filterQuery(State.query);
  }
}

function hexToRgb(hex) {
  if (!hex) return null;
  let clean = hex.trim();
  if (clean.startsWith('#')) clean = clean.slice(1);
  if (clean.length === 3) {
    clean = clean.split('').map(c => c + c).join('');
  }
  const num = parseInt(clean, 16);
  if (isNaN(num)) return null;
  return {
    r: (num >> 16) & 255,
    g: (num >> 8) & 255,
    b: num & 255
  };
}

export function getActiveAccentHex() {
  if (State.customAccent) return State.customAccent;
  const currentThemeObj = Themes.find(t => t.id === State.theme) || Themes[0];
  return currentThemeObj.color;
}

function loadSavedPreferences() {
  const savedOs = localStorage.getItem('lail_os');
  if (savedOs) State.os = savedOs;

  const savedMode = localStorage.getItem('lail_mode');
  if (savedMode) State.mode = savedMode;

  const savedTheme = localStorage.getItem('lail_theme');
  if (savedTheme) State.theme = savedTheme;

  const savedCustomAccent = localStorage.getItem('lail_custom_accent');
  if (savedCustomAccent) State.customAccent = savedCustomAccent;

  const savedWallpaper = localStorage.getItem('lail_wallpaper');
  if (savedWallpaper) State.wallpaper = savedWallpaper;

  const savedOpacity = localStorage.getItem('lail_glass_opacity');
  if (savedOpacity) State.glassOpacity = parseFloat(savedOpacity);

  const savedBlur = localStorage.getItem('lail_glass_blur');
  if (savedBlur) State.glassBlur = parseInt(savedBlur, 10);

  // Restore Window Custom Position
  const savedCustomPos = localStorage.getItem('lail_window_custom');
  if (savedCustomPos === 'true') {
    const ox = parseFloat(localStorage.getItem('lail_window_offset_x'));
    const oy = parseFloat(localStorage.getItem('lail_window_offset_y'));
    if (!isNaN(ox) && !isNaN(oy)) {
      State.hasCustomPosition = true;
      State.windowOffsetX = ox;
      State.windowOffsetY = oy;
      const shell = document.getElementById('launcher-shell');
      if (shell) {
        shell.style.transform = `translate(${ox}px, ${oy}px)`;
      }
    }
  }
}

function savePreferences() {
  localStorage.setItem('lail_os', State.os);
  localStorage.setItem('lail_mode', State.mode);
  localStorage.setItem('lail_theme', State.theme);
  if (State.customAccent) {
    localStorage.setItem('lail_custom_accent', State.customAccent);
  } else {
    localStorage.removeItem('lail_custom_accent');
  }
  localStorage.setItem('lail_wallpaper', State.wallpaper);
  localStorage.setItem('lail_glass_opacity', State.glassOpacity.toString());
  localStorage.setItem('lail_glass_blur', State.glassBlur.toString());
}

export function applyAppearance() {
  const root = document.documentElement;
  root.setAttribute('data-os', State.os);
  root.setAttribute('data-mode', State.mode);
  root.setAttribute('data-theme', State.theme);
  
  root.style.setProperty('--glass-opacity', State.glassOpacity);
  root.style.setProperty('--glass-blur', `${State.glassBlur}px`);

  // Dynamic Accent Color Handling
  const activeAccent = getActiveAccentHex();
  const rgb = hexToRgb(activeAccent);

  if (State.customAccent && rgb) {
    root.style.setProperty('--accent-base', activeAccent);
    root.style.setProperty('--accent-hover', activeAccent);
    root.style.setProperty('--accent-light', activeAccent);
    root.style.setProperty('--accent-glow', `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, 0.45)`);
    root.style.setProperty('--accent-badge-bg', `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, 0.16)`);
    root.style.setProperty('--accent-hue-rgb', `${rgb.r}, ${rgb.g}, ${rgb.b}`);
  } else {
    root.style.removeProperty('--accent-base');
    root.style.removeProperty('--accent-hover');
    root.style.removeProperty('--accent-light');
    root.style.removeProperty('--accent-glow');
    root.style.removeProperty('--accent-badge-bg');
    root.style.removeProperty('--accent-hue-rgb');
  }

  // Update accent input & preview
  const accentPreview = document.getElementById('custom-accent-preview');
  const accentInput = document.getElementById('custom-accent-hex');
  if (accentPreview) {
    accentPreview.style.background = activeAccent;
    accentPreview.style.boxShadow = `0 0 10px rgba(${rgb ? `${rgb.r}, ${rgb.g}, ${rgb.b}` : '59, 130, 246'}, 0.5)`;
  }
  if (accentInput && document.activeElement !== accentInput) {
    accentInput.value = activeAccent.toUpperCase();
  }
  
  // Update desktop wallpaper
  const desktop = document.getElementById('desktop-viewport');
  if (desktop) {
    desktop.className = 'desktop-viewport';
    const wp = Wallpapers.find(w => w.id === State.wallpaper) || Wallpapers[0];
    desktop.classList.add(wp.class);
  }
  
  // Update sliders
  const opacitySlider = document.getElementById('slider-glass-opacity');
  const opacityVal = document.getElementById('label-opacity-val');
  if (opacitySlider && opacityVal) {
    opacitySlider.value = Math.round(State.glassOpacity * 100);
    opacityVal.textContent = `${Math.round(State.glassOpacity * 100)}%`;
  }
  
  const blurSlider = document.getElementById('slider-glass-blur');
  const blurVal = document.getElementById('label-blur-val');
  if (blurSlider && blurVal) {
    blurSlider.value = State.glassBlur;
    blurVal.textContent = `${State.glassBlur}px`;
  }
  
  // Update quick dock buttons
  document.querySelectorAll('.dock-os-btn').forEach(btn => {
    btn.classList.toggle('active', btn.getAttribute('data-os') === State.os);
  });
  
  const modeBtn = document.getElementById('dock-btn-mode');
  if (modeBtn) {
    modeBtn.innerHTML = State.mode === 'dark' 
      ? `${renderIcon('moon')} <span>Tối</span>` 
      : `${renderIcon('sun')} <span>Sáng</span>`;
  }
  
  savePreferences();
}

// ============================================================================
// 5. QUERY & SEARCH PIPELINE
// ============================================================================

export function filterQuery(rawQuery) {
  State.query = rawQuery;
  const q = rawQuery.trim();
  const searchPrefixBadge = document.getElementById('search-prefix-badge');
  const searchClearBtn = document.getElementById('search-clear-btn');
  
  if (searchClearBtn) {
    searchClearBtn.classList.toggle('visible', q.length > 0);
  }

  // 1. Math / Calculator Check
  if (isMathExpression(q)) {
    State.prefixMode = 'calc';
    if (searchPrefixBadge) {
      searchPrefixBadge.className = 'search-prefix-badge visible';
      searchPrefixBadge.innerHTML = `${renderIcon('calc')} <span>Máy tính</span>`;
    }
    const mathResult = evaluateMath(q);
    State.currentResults = [{
      id: 'calc-eval',
      title: `${mathResult}`,
      subtitle: `Kết quả phép tính cho: ${q} (Nhấn Enter để sao chép)`,
      category: 'Máy tính',
      type: 'calc',
      icon: 'calc',
      action: 'Sao chép kết quả',
      tags: ['calc', 'math']
    }];
    renderResults();
    return;
  }

  // 2. Shell / Terminal Prefix ('>')
  if (q.startsWith('>')) {
    State.prefixMode = 'terminal';
    if (searchPrefixBadge) {
      searchPrefixBadge.className = 'search-prefix-badge visible';
      searchPrefixBadge.innerHTML = `${renderIcon('terminal')} <span>Lệnh Shell</span>`;
    }
    const command = q.substring(1).trim();
    State.currentResults = [
      {
        id: 'term-exec-cmd',
        title: `cmd.exe /k ${command || '<lệnh>'}`,
        subtitle: 'Thực thi lệnh qua Command Prompt (Nhấn Enter)',
        category: 'Terminal',
        type: 'terminal',
        icon: 'terminal',
        action: 'Chạy lệnh'
      },
      {
        id: 'term-exec-ps',
        title: `powershell -NoExit -Command "${command || '<lệnh>'}"`,
        subtitle: 'Thực thi lệnh qua PowerShell 7',
        category: 'Terminal',
        type: 'terminal',
        icon: 'terminal',
        action: 'Chạy PowerShell'
      },
      {
        id: 'term-exec-admin',
        title: `Run as Administrator: ${command || '<lệnh>'}`,
        subtitle: 'Khởi chạy với quyền Quản trị viên (Ctrl + Shift + Enter)',
        category: 'Terminal',
        type: 'terminal',
        icon: 'shield',
        action: 'Chạy quyền Admin'
      }
    ];
    renderResults();
    return;
  }

  // 3. Web Search Engine Keywords ('g ', 'yt ', 'gh ', 'wiki ')
  const webEngines = [
    { prefix: 'g ', name: 'Google', url: 'https://google.com/search?q=', icon: 'google' },
    { prefix: 'yt ', name: 'YouTube', url: 'https://youtube.com/results?search_query=', icon: 'youtube' },
    { prefix: 'gh ', name: 'GitHub', url: 'https://github.com/search?q=', icon: 'github' },
    { prefix: 'wiki ', name: 'Wikipedia', url: 'https://vi.wikipedia.org/wiki/Special:Search?search=', icon: 'web' }
  ];

  for (const eng of webEngines) {
    if (q.startsWith(eng.prefix)) {
      State.prefixMode = 'web';
      if (searchPrefixBadge) {
        searchPrefixBadge.className = 'search-prefix-badge visible';
        searchPrefixBadge.innerHTML = `${renderIcon(eng.icon)} <span>${eng.name}</span>`;
      }
      const term = q.substring(eng.prefix.length).trim();
      State.currentResults = [{
        id: `web-${eng.name.toLowerCase()}`,
        title: `Tìm "${term}" trên ${eng.name}`,
        subtitle: `Mở trình duyệt mặc định: ${eng.url}${encodeURIComponent(term)}`,
        category: 'Web Search',
        type: 'web',
        icon: eng.icon,
        action: `Tìm kiếm trên ${eng.name}`
      }];
      renderResults();
      return;
    }
  }

  // 4. AI Query Prefix ('ai ' or '? ')
  if (q.startsWith('ai ') || q.startsWith('?')) {
    State.prefixMode = 'ai';
    if (searchPrefixBadge) {
      searchPrefixBadge.className = 'search-prefix-badge visible';
      searchPrefixBadge.innerHTML = `${renderIcon('sparkles')} <span>Local AI</span>`;
    }
    const prompt = q.replace(/^(ai\s+|\?\s*)/i, '').trim();
    State.currentResults = [
      {
        id: 'ai-prompt-run',
        title: `Hỏi AI Cục bộ: "${prompt || 'Hãy đặt câu hỏi...'}"`,
        subtitle: 'Mô hình Ollama Llama-3-8B • Chạy hoàn toàn Offline trên máy',
        category: 'Trợ lý AI',
        type: 'ai',
        icon: 'sparkles',
        action: 'Gửi câu hỏi cho AI'
      },
      {
        id: 'ai-code-review',
        title: `AI Code Review & Tối ưu hóa hiệu năng`,
        subtitle: 'Phân tích mã nguồn và gợi ý cấu trúc thuật toán',
        category: 'Trợ lý AI',
        type: 'ai',
        icon: 'sparkles',
        action: 'Phân tích mã nguồn'
      }
    ];
    renderResults();
    return;
  }

  // 5. Background Processes Query ('trinh chay ngam', 'chay ngam', 'ngam', 'tien trinh', 'kill', 'proc', 'task manager', 'taskmgr', 'tat ung dung', 'dong ung dung')
  const procKeywords = ['trinh chay ngam', 'chay ngam', 'ngam', 'tien trinh', 'kill', 'proc', 'task manager', 'taskmgr', 'tat ung dung', 'dong ung dung'];
  const lowerQ = q.toLowerCase();
  const isProcSearch = procKeywords.some(k => lowerQ.startsWith(k) || lowerQ === k);

  if (isProcSearch) {
    State.prefixMode = 'proc';
    if (searchPrefixBadge) {
      searchPrefixBadge.className = 'search-prefix-badge visible';
      searchPrefixBadge.innerHTML = `${renderIcon('process')} <span>Trình chạy ngầm</span>`;
    }
    let filterTerm = '';
    for (const k of procKeywords) {
      if (lowerQ.startsWith(k)) {
        filterTerm = lowerQ.slice(k.length).trim();
        break;
      }
    }
    const filteredProcs = filterTerm 
      ? State.backgroundProcesses.filter(p => 
          p.name.toLowerCase().includes(filterTerm) || 
          p.exe.toLowerCase().includes(filterTerm) || 
          p.pid.toString().includes(filterTerm))
      : State.backgroundProcesses;

    State.currentResults = filteredProcs.map(p => ({
      id: `proc-${p.pid}`,
      title: `${p.name}`,
      subtitle: `${p.exe} • PID: ${p.pid} • CPU: ${p.cpu}`,
      category: 'Trình chạy ngầm',
      type: 'proc',
      icon: p.icon || 'process',
      action: `Tắt tiến trình (PID: ${p.pid})`,
      procData: p
    }));
  // 6. Format Prefix Filters (pdf:, doc:, sheet:, slide:, img:, code:, zip:)
  const formatFilters = [
    { prefix: 'pdf:', tag: 'PDF', exts: ['.pdf'] },
    { prefix: 'doc:', tag: 'Văn bản', exts: ['.docx', '.doc', '.pdf', '.txt', '.md', '.rtf'] },
    { prefix: 'docs:', tag: 'Văn bản', exts: ['.docx', '.doc', '.pdf', '.txt', '.md', '.rtf'] },
    { prefix: 'sheet:', tag: 'Bảng tính', exts: ['.xlsx', '.xls', '.csv'] },
    { prefix: 'excel:', tag: 'Excel', exts: ['.xlsx', '.xls', '.csv'] },
    { prefix: 'slide:', tag: 'Thuyết trình', exts: ['.pptx', '.ppt'] },
    { prefix: 'ppt:', tag: 'PowerPoint', exts: ['.pptx', '.ppt'] },
    { prefix: 'img:', tag: 'Hình ảnh', exts: ['.jpg', '.jpeg', '.png', '.webp', '.gif', '.svg'] },
    { prefix: 'photo:', tag: 'Ảnh', exts: ['.jpg', '.jpeg', '.png', '.webp', '.gif'] },
    { prefix: 'pic:', tag: 'Ảnh', exts: ['.jpg', '.jpeg', '.png', '.webp', '.gif'] },
    { prefix: 'code:', tag: 'Mã nguồn', exts: ['.cs', '.py', '.js', '.ts', '.html', '.css', '.json', '.sql', '.sln'] },
    { prefix: 'zip:', tag: 'Tệp nén', exts: ['.zip', '.rar', '.7z', '.tar', '.gz'] }
  ];

  for (const ff of formatFilters) {
    if (lowerQ.startsWith(ff.prefix)) {
      State.prefixMode = 'filter';
      if (searchPrefixBadge) {
        searchPrefixBadge.className = 'search-prefix-badge visible';
        searchPrefixBadge.innerHTML = `${renderIcon('folder')} <span>Bộ lọc: ${ff.tag}</span>`;
      }
      const term = lowerQ.slice(ff.prefix.length).trim();
      State.currentResults = Database.filter(item => {
        const matchesExt = ff.exts.some(ext => item.extension === ext || item.title.toLowerCase().endsWith(ext) || (item.tags && item.tags.includes(ff.prefix)));
        if (!matchesExt) return false;
        if (!term) return true;
        return item.title.toLowerCase().includes(term) || item.subtitle.toLowerCase().includes(term) || (item.tags && item.tags.some(t => t.toLowerCase().includes(term)));
      }).map(item => ({ ...item, isSemanticMatch: false }));
      State.selectedIndex = 0;
      renderResults();
      return;
    }
  }

  // 7. Local Semantic Natural Language Matching (Zero-NPU AI Search)
  State.prefixMode = null;
  if (searchPrefixBadge) {
    searchPrefixBadge.className = 'search-prefix-badge';
  }

  if (!q) {
    State.currentResults = Database.map(item => ({ ...item, isSemanticMatch: false }));
  } else {
    const lower = q.toLowerCase();
    const normalized = normalizeVietnamese(lower);

    // Semantic Intent Dictionary
    const semanticCategories = [
      {
        keywords: ['báo cáo', 'bao cao', 'tài chính', 'tai chinh', 'doanh thu', 'kế toán', 'ngân sách', 'chi tiêu', 'lợi nhuận', 'budget', 'finance', 'report', 'revenue', 'hóa đơn', 'hoa don'],
        exts: ['.xlsx', '.xls', '.csv', '.pdf', '.docx']
      },
      {
        keywords: ['hợp đồng', 'hop dong', 'pháp lý', 'thỏa thuận', 'ký kết', 'contract', 'agreement', 'dịch vụ', 'điều khoản', 'biên bản'],
        exts: ['.docx', '.doc', '.pdf']
      },
      {
        keywords: ['kế hoạch', 'ke hoach', 'chiến lược', 'thuyết trình', 'slide', 'presentation', 'roadmap', 'pitch', 'copilot'],
        exts: ['.pptx', '.ppt', '.pdf']
      },
      {
        keywords: ['luận văn', 'luan van', 'nghiên cứu', 'đồ án', 'thesis', 'research', 'sách', 'tài liệu học', 'paper'],
        exts: ['.pdf', '.docx', '.md']
      },
      {
        keywords: ['ảnh', 'anh', 'hình', 'hinh', 'hoàng hôn', 'hoang hon', 'bãi biển', 'bien', 'sunset', 'beach', 'photo', 'picture', 'wallpaper', 'hình nền', 'chụp'],
        exts: ['.jpg', '.jpeg', '.png', '.webp']
      },
      {
        keywords: ['mã nguồn', 'ma nguon', 'code', 'source', 'dự án', 'project', 'pipeline', 'intent', 'csharp', 'python', 'javascript'],
        exts: ['.cs', '.py', '.js', '.ts', '.sln']
      }
    ];

    let hasSemanticQuery = false;
    let targetExtensions = [];
    for (const sc of semanticCategories) {
      if (sc.keywords.some(kw => normalized.includes(normalizeVietnamese(kw)))) {
        hasSemanticQuery = true;
        targetExtensions.push(...sc.exts);
      }
    }

    const scored = Database.map(item => {
      let score = 0;
      let isSemantic = false;

      const titleNorm = normalizeVietnamese(item.title.toLowerCase());
      const subNorm = normalizeVietnamese(item.subtitle.toLowerCase());
      const catNorm = normalizeVietnamese(item.category.toLowerCase());
      const inTags = item.tags && item.tags.some(t => normalizeVietnamese(t.toLowerCase()).includes(normalized) || normalized.includes(normalizeVietnamese(t.toLowerCase())));

      if (titleNorm.includes(normalized)) score += 50;
      if (subNorm.includes(normalized)) score += 20;
      if (catNorm.includes(normalized)) score += 25;
      if (inTags) score += 35;

      // Semantic Boost
      if (hasSemanticQuery) {
        if (item.extension && targetExtensions.includes(item.extension)) {
          score += 45;
          isSemantic = true;
        }
        if (item.tags && item.tags.some(t => normalized.includes(normalizeVietnamese(t)))) {
          score += 55;
          isSemantic = true;
        }
      }

      return {
        ...item,
        searchScore: score,
        isSemanticMatch: isSemantic
      };
    }).filter(i => i.searchScore > 0);

    scored.sort((a, b) => b.searchScore - a.searchScore);
    State.currentResults = scored;

    // Fallback: Always offer Google Web Search if not found
    if (State.currentResults.length === 0) {
      State.currentResults.push({
        id: 'web-fallback',
        title: `Tìm kiếm "${q}" trên Google`,
        subtitle: 'Không tìm thấy mục khớp trên máy cục bộ. Nhấn Enter để tìm trên web.',
        category: 'Web',
        type: 'web',
        icon: 'google',
        action: 'Tìm trên Google',
        isSemanticMatch: false
      });
    }
  }

  State.selectedIndex = 0;
  renderResults();
}

function normalizeVietnamese(str) {
  if (!str) return '';
  return str
    .toLowerCase()
    .replace(/[àáạảãâầấậẩẫăằắặẳẵ]/g, 'a')
    .replace(/[èéẹẻẽêềếệểễ]/g, 'e')
    .replace(/[ìíịỉĩ]/g, 'i')
    .replace(/[òóọỏõôồốộổỗơờớợởỡ]/g, 'o')
    .replace(/[ùúụủũưừứựửữ]/g, 'u')
    .replace(/[ỳýỵỷỹ]/g, 'y')
    .replace(/[đ]/g, 'd');
}

function renderResults() {
  const container = document.getElementById('results-list');
  const emptyState = document.getElementById('results-empty-state');
  if (!container) return;

  container.innerHTML = '';

  if (State.currentResults.length === 0) {
    if (emptyState) emptyState.classList.add('visible');
    return;
  } else {
    if (emptyState) emptyState.classList.remove('visible');
  }

  State.currentResults.forEach((item, index) => {
    const isSelected = index === State.selectedIndex;
    const li = document.createElement('li');
    li.className = `result-item ${isSelected ? 'selected' : ''}`;
    li.setAttribute('data-index', index);
    li.setAttribute('id', `result-item-${index}`);

    if (item.type === 'proc') {
      li.innerHTML = `
        <div class="result-item-left">
          <div class="result-icon-box icon-proc" style="color: #EF4444; background: rgba(239, 68, 68, 0.12);">
            ${renderIcon(item.icon)}
          </div>
          <div class="result-text-box">
            <div class="result-title">${escapeHtml(item.title)}</div>
            <div class="result-subtitle">${escapeHtml(item.subtitle)}</div>
          </div>
        </div>
        <div class="proc-row-actions">
          <span class="proc-ram-chip">RAM: ${item.procData?.ram || '0 MB'}</span>
          <button type="button" class="btn-kill-proc" data-pid="${item.procData?.pid}" title="Tắt tiến trình ngay lập tức">
            <span>🛑 Tắt</span>
          </button>
        </div>
      `;

      const killBtn = li.querySelector('.btn-kill-proc');
      if (killBtn) {
        killBtn.addEventListener('click', (e) => {
          e.stopPropagation();
          killProcess(item.procData?.pid);
        });
      }
    } else {
      const semanticBadgeHtml = item.isSemanticMatch
        ? `<span class="semantic-tag">🎯 AI Semantic</span>`
        : '';

      li.innerHTML = `
        <div class="result-item-left">
          <div class="result-icon-box icon-${item.type}">
            ${renderIcon(item.icon)}
          </div>
          <div class="result-text-box">
            <div class="result-title">${escapeHtml(item.title)}</div>
            <div class="result-subtitle">${escapeHtml(item.subtitle)}</div>
          </div>
        </div>
        <div class="result-item-right">
          ${semanticBadgeHtml}
          <span class="category-tag">${item.category}</span>
          ${isSelected ? `<span class="hotkey-badge">Enter ↵</span>` : ''}
        </div>
      `;
    }

    li.addEventListener('click', () => {
      State.selectedIndex = index;
      renderResults();
      executeAction(item);
    });

    li.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      State.selectedIndex = index;
      renderResults();
      openActionFlyout(e);
    });

    container.appendChild(li);
  });

  // Ensure selected item is scrolled into view
  const activeEl = document.querySelector('.result-item.selected');
  if (activeEl) {
    activeEl.scrollIntoView({ block: 'nearest' });
  }
}

// ============================================================================
// 6. ACTION EXECUTION & INTERACTIONS
// ============================================================================

export function executeAction(item) {
  if (!item) return;

  if (item.type === 'proc') {
    killProcess(item.procData?.pid);
    return;
  }

  if (item.id === 'sys-hotspot-toggle') {
    toggleHotspot();
    return;
  }

  if (item.id === 'sys-battery-saver') {
    toggleEnergySaver();
    return;
  }

  if (item.id === 'sys-taskbar-pos') {
    toggleTaskbarAlign();
    return;
  }

  if (item.id === 'sys-screenshot') {
    takeScreenshot();
    return;
  }

  if (item.id === 'sys-background-procs') {
    const searchInput = document.getElementById('search-input');
    if (searchInput) {
      searchInput.value = 'trinh chay ngam';
      searchInput.focus();
    }
    filterQuery('trinh chay ngam');
    return;
  }

  if (item.type === 'ai') {
    openAiDrawer(item.title);
    return;
  }

  if (item.id === 'sys-toggle-dark') {
    State.mode = State.mode === 'dark' ? 'light' : 'dark';
    applyAppearance();
    showToast(`Đã chuyển sang ${State.mode === 'dark' ? 'Chế độ Tối (Dark Mode)' : 'Chế độ Sáng (Light Mode)'}`);
    return;
  }

  if (item.type === 'calc') {
    navigator.clipboard?.writeText(item.title);
    showToast(`Đã sao chép kết quả máy tính "${item.title}" vào Clipboard!`);
    return;
  }

  if (item.type === 'web') {
    showToast(`Đang mở tìm kiếm web: ${item.title}`);
    return;
  }

  if (item.type === 'terminal') {
    showToast(`Đang thực thi lệnh hệ thống: ${item.title}`);
    return;
  }

  showToast(`Đã kích hoạt: ${item.title} (${item.action})`);
}

// ============================================================================
// 7. LOCAL AI STREAMING SIMULATOR
// ============================================================================

let aiStreamInterval = null;

export function openAiDrawer(promptText) {
  const drawer = document.getElementById('ai-stream-drawer');
  const content = document.getElementById('ai-stream-text');
  const latency = document.getElementById('ai-stream-latency');
  if (!drawer || !content) return;

  drawer.classList.add('visible');
  State.isAiDrawerOpen = true;

  content.innerHTML = `<strong>Đang kết nối tới Ollama / llama-3-8b (Cục bộ)...</strong><br><br>`;
  if (latency) latency.textContent = 'Khởi tạo bộ đệm mô hình ONNX...';

  if (aiStreamInterval) clearInterval(aiStreamInterval);

  const fullResponse = `### Phân tích AI cho câu hỏi của bạn:
  
Dựa trên ngữ cảnh và mô hình cục bộ **Llama-3-8B-Instruct** (chạy trên GPU/NPU qua ONNX Runtime):

1. **Khởi động & Thực thi**: Thời gian phản hồi lần đầu cực nhanh, không phụ thuộc kết nối internet.
2. **Bảo mật tối đa**: Toàn bộ dữ liệu bộ nhớ đệm, tệp tin và lệnh được xử lý hoàn toàn offline.
3. **Mã nguồn minh họa**:

\`\`\`csharp
// Local AI Intent Classifier Pipeline
var classifier = new IntentClassifier("models/intent.onnx");
var result = await classifier.ClassifyAsync(query);
Console.WriteLine($"Intent: {result.Intent}, Confidence: {result.Confidence:P1}");
\`\`\`

Bạn có thể nhấn **Enter** để lưu kết quả hoặc sao chép vào bộ nhớ đệm!`;

  let currentIndex = 0;
  content.innerHTML = '';
  
  const startTime = Date.now();
  aiStreamInterval = setInterval(() => {
    if (currentIndex < fullResponse.length) {
      currentIndex += 3;
      const partial = fullResponse.substring(0, currentIndex);
      content.innerHTML = formatMarkdown(partial);
      const elapsed = Date.now() - startTime;
      const speed = Math.round((currentIndex / (elapsed / 1000)) || 45);
      if (latency) latency.textContent = `Tốc độ: ${speed} tokens/giây • Latency: 18ms`;
      content.scrollTop = content.scrollHeight;
    } else {
      clearInterval(aiStreamInterval);
      if (latency) latency.textContent = `Hoàn tất (258 tokens) • Ollama Llama-3-8B Cục bộ`;
    }
  }, 22);
}

export function closeAiDrawer() {
  const drawer = document.getElementById('ai-stream-drawer');
  if (drawer) drawer.classList.remove('visible');
  State.isAiDrawerOpen = false;
  if (aiStreamInterval) clearInterval(aiStreamInterval);
}

// ============================================================================
// 8. POPUP ACTION MENU FLYOUT
// ============================================================================

export function openActionFlyout(e) {
  const menu = document.getElementById('action-flyout-menu');
  if (!menu) return;

  const current = State.currentResults[State.selectedIndex];
  if (!current) return;

  const titleEl = document.getElementById('flyout-target-title');
  if (titleEl) titleEl.textContent = current.title;

  menu.classList.add('visible');
  State.isFlyoutOpen = true;
}

export function closeActionFlyout() {
  const menu = document.getElementById('action-flyout-menu');
  if (menu) menu.classList.remove('visible');
  State.isFlyoutOpen = false;
}

// ============================================================================
// 9. EVENT LISTENERS & SHORTCUTS
// ============================================================================

function setupEventListeners() {
  const searchInput = document.getElementById('search-input');
  const clearBtn = document.getElementById('search-clear-btn');

  // Input changes
  if (searchInput) {
    searchInput.addEventListener('input', (e) => {
      filterQuery(e.target.value);
    });

    searchInput.addEventListener('keydown', handleKeyNavigation);
  }

  // Clear button
  if (clearBtn) {
    clearBtn.addEventListener('click', () => {
      if (searchInput) {
        searchInput.value = '';
        searchInput.focus();
        filterQuery('');
      }
    });
  }

  // Global Keyboard Shortcuts
  window.addEventListener('keydown', (e) => {
    // Alt + Space: Focus / Show launcher
    if (e.altKey && e.code === 'Space') {
      e.preventDefault();
      toggleLauncherVisibility();
      return;
    }

    // Ctrl + D: Toggle Dark / Light mode
    if (e.ctrlKey && e.key.toLowerCase() === 'd') {
      e.preventDefault();
      State.mode = State.mode === 'dark' ? 'light' : 'dark';
      applyAppearance();
      showToast(`Đã chuyển sang ${State.mode === 'dark' ? 'Chế độ Tối' : 'Chế độ Sáng'}`);
      return;
    }

    // Ctrl + , : Open Settings
    if (e.ctrlKey && e.key === ',') {
      e.preventDefault();
      toggleSettingsModal();
      return;
    }
  });

  // Dock Controls
  document.querySelectorAll('.dock-os-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const targetOs = btn.getAttribute('data-os');
      if (targetOs) {
        State.os = targetOs;
        applyAppearance();
        showToast(targetOs === 'windows' ? 'Chuyển sang giao diện Windows 11 Fluent' : 'Chuyển sang giao diện Linux GNOME Adwaita');
      }
    });
  });

  document.getElementById('dock-btn-mode')?.addEventListener('click', () => {
    State.mode = State.mode === 'dark' ? 'light' : 'dark';
    applyAppearance();
    showToast(`Đã chuyển sang ${State.mode === 'dark' ? 'Chế độ Tối' : 'Chế độ Sáng'}`);
  });

  document.getElementById('dock-btn-settings')?.addEventListener('click', () => {
    toggleSettingsModal();
  });

  document.getElementById('dock-btn-ai')?.addEventListener('click', () => {
    openAiDrawer('Giới thiệu hệ thống LAILauncher');
  });

  // Windows & Linux Titlebar Controls
  document.getElementById('btn-win-close')?.addEventListener('click', () => {
    showToast('Đóng cửa sổ LAILauncher (Nhấn Alt+Space để mở lại)');
    toggleLauncherVisibility();
  });
  document.getElementById('btn-linux-close')?.addEventListener('click', () => {
    showToast('Đóng cửa sổ Linux GNOME (Nhấn Alt+Space để mở lại)');
    toggleLauncherVisibility();
  });

  // Action Flyout Items
  document.getElementById('flyout-action-run')?.addEventListener('click', () => {
    executeAction(State.currentResults[State.selectedIndex]);
    closeActionFlyout();
  });
  document.getElementById('flyout-action-admin')?.addEventListener('click', () => {
    const item = State.currentResults[State.selectedIndex];
    showToast(`Đang thực thi với quyền Admin: ${item?.title}`);
    closeActionFlyout();
  });
  document.getElementById('flyout-action-folder')?.addEventListener('click', () => {
    const item = State.currentResults[State.selectedIndex];
    showToast(`Mở thư mục chứa trong Explorer: ${item?.subtitle || item?.title}`);
    closeActionFlyout();
  });
  document.getElementById('flyout-action-copy')?.addEventListener('click', () => {
    const item = State.currentResults[State.selectedIndex];
    navigator.clipboard?.writeText(item?.subtitle || item?.title);
    showToast(`Đã sao chép đường dẫn: ${item?.title}`);
    closeActionFlyout();
  });
  document.getElementById('flyout-action-summarize')?.addEventListener('click', () => {
    const item = State.currentResults[State.selectedIndex];
    openAiDrawer(`Hãy đọc và tóm tắt ngắn gọn các ý chính của tài liệu "${item?.title}":\n\n1. Tổng quan nội dung tài liệu\n2. Các điểm số liệu cốt lõi\n3. Đề xuất hành động tiếp theo.`);
    closeActionFlyout();
  });
  document.getElementById('flyout-action-ai')?.addEventListener('click', () => {
    const item = State.currentResults[State.selectedIndex];
    openAiDrawer(`Giải thích và hỗ trợ cho: ${item?.title}`);
    closeActionFlyout();
  });

  // Settings Controls
  document.getElementById('settings-close-btn')?.addEventListener('click', toggleSettingsModal);
  document.getElementById('settings-overlay')?.addEventListener('click', (e) => {
    if (e.target.id === 'settings-overlay') toggleSettingsModal();
  });

  // Sliders
  const opacitySlider = document.getElementById('slider-glass-opacity');
  if (opacitySlider) {
    opacitySlider.addEventListener('input', (e) => {
      State.glassOpacity = e.target.value / 100;
      applyAppearance();
    });
  }

  const blurSlider = document.getElementById('slider-glass-blur');
  if (blurSlider) {
    blurSlider.addEventListener('input', (e) => {
      State.glassBlur = parseInt(e.target.value, 10);
      applyAppearance();
    });
  }

  // Multi-Monitor Mode selector
  document.querySelectorAll('.monitor-mode-card').forEach(card => {
    card.addEventListener('click', () => {
      document.querySelectorAll('.monitor-mode-card').forEach(c => c.classList.remove('active'));
      card.classList.add('active');
      const mode = card.dataset.mode;
      const titles = {
        cursor: 'Theo con trỏ chuột (Khuyên dùng)',
        primary: 'Màn hình chính Windows',
        active_window: 'Theo cửa sổ đang làm việc',
        specific: 'Màn hình chỉ định cụ thể'
      };
      showToast(`Đã lưu thiết lập: ${titles[mode] || mode}`);
    });
  });

  // Accent Color Customizer Listeners
  const btnApplyAccent = document.getElementById('btn-apply-accent');
  const btnResetAccent = document.getElementById('btn-reset-accent');
  const hexInput = document.getElementById('custom-accent-hex');

  btnApplyAccent?.addEventListener('click', () => {
    const val = hexInput?.value?.trim();
    if (val) {
      let clean = val.startsWith('#') ? val : '#' + val;
      if (/^#[0-9A-Fa-f]{6}$/.test(clean) || /^#[0-9A-Fa-f]{3}$/.test(clean)) {
        State.customAccent = clean.toUpperCase();
        applyAppearance();
        renderAccentChips();
        showToast(`Đã áp dụng màu Accent: ${clean.toUpperCase()}`);
      } else {
        showToast('Mã màu HEX không hợp lệ (Ví dụ: #0078D7 hoặc #10B981)');
      }
    }
  });

  hexInput?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      btnApplyAccent?.click();
    }
  });

  btnResetAccent?.addEventListener('click', () => {
    State.customAccent = null;
    applyAppearance();
    renderAccentChips();
    showToast('Đã khôi phục màu Accent mặc định theo Theme');
  });

  // Quick Settings Horizontal Strip Controls (Windows Search 2025 Inline Toggles)
  document.getElementById('pill-hotspot')?.addEventListener('click', toggleHotspot);
  document.getElementById('pill-energy')?.addEventListener('click', toggleEnergySaver);
  document.getElementById('pill-taskbar')?.addEventListener('click', toggleTaskbarAlign);
  document.getElementById('pill-screenshot')?.addEventListener('click', takeScreenshot);
  document.getElementById('pill-processes')?.addEventListener('click', () => {
    const searchInput = document.getElementById('search-input');
    if (searchInput) {
      searchInput.value = 'trinh chay ngam';
      searchInput.focus();
    }
    filterQuery('trinh chay ngam');
  });
  document.getElementById('pill-wifi')?.addEventListener('click', () => {
    showToast('📶 Wi-Fi: Đang quét tìm các điểm truy cập không dây khả dụng...');
  });
  document.getElementById('pill-bluetooth')?.addEventListener('click', () => {
    showToast('🔵 Bluetooth: Đang sẵn sàng ghép nối thiết bị ngoại vi');
  });

  // Screenshot Preview Modal Controls
  document.getElementById('btn-close-screenshot-modal')?.addEventListener('click', closeScreenshotModal);
  document.getElementById('screenshot-preview-modal')?.addEventListener('click', (e) => {
    if (e.target.id === 'screenshot-preview-modal') closeScreenshotModal();
  });
  document.getElementById('btn-copy-screenshot')?.addEventListener('click', () => {
    const canvas = document.getElementById('screenshot-canvas');
    if (canvas && navigator.clipboard && window.ClipboardItem) {
      canvas.toBlob(blob => {
        if (blob) {
          navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })])
            .then(() => showToast('📋 Đã sao chép ảnh chụp màn hình vào Clipboard!'))
            .catch(() => showToast('📋 Đã sao chép ảnh vào bộ nhớ tạm!'));
        }
      });
    } else {
      showToast('📋 Đã sao chép ảnh chụp màn hình vào Clipboard!');
    }
  });
  document.getElementById('btn-download-screenshot')?.addEventListener('click', () => {
    const canvas = document.getElementById('screenshot-canvas');
    if (canvas) {
      const a = document.createElement('a');
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
      a.download = `Screenshot_${timestamp}.png`;
      a.href = canvas.toDataURL('image/png');
      a.click();
      showToast('💾 Đã lưu tệp ảnh chụp màn hình vào Pictures\\Screenshots');
    }
  });

  // AI Drawer Close
  document.getElementById('ai-stream-close')?.addEventListener('click', closeAiDrawer);

  // Window Dragging & Position Management
  setupWindowDragging();
  document.getElementById('dock-btn-reset-pos')?.addEventListener('click', resetWindowPosition);
  document.getElementById('titlebar-reset-pos-btn')?.addEventListener('click', resetWindowPosition);
  document.getElementById('btn-reset-pos-settings')?.addEventListener('click', resetWindowPosition);

  // Close flyout on outside click
  document.addEventListener('click', (e) => {
    if (State.isFlyoutOpen && !e.target.closest('#action-flyout-menu')) {
      closeActionFlyout();
    }
  });
}

// ============================================================================
// 10. SCREENSHOT CAPTURE ENGINE
// ============================================================================

export function takeScreenshot() {
  const flash = document.getElementById('screenshot-flash-overlay');
  if (flash) {
    flash.classList.add('flashing');
    setTimeout(() => flash.classList.remove('flashing'), 140);
  }

  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (AudioCtx) {
      const ctx = new AudioCtx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(880, ctx.currentTime);
      osc.frequency.exponentialRampToValueAtTime(220, ctx.currentTime + 0.08);
      gain.gain.setValueAtTime(0.3, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.08);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.08);
    }
  } catch {
    // Ignore audio restrictions
  }

  const canvas = document.getElementById('screenshot-canvas');
  if (canvas) {
    const ctx = canvas.getContext('2d');
    const w = canvas.width;
    const h = canvas.height;

    // Desktop wallpaper gradient
    const grad = ctx.createLinearGradient(0, 0, w, h);
    grad.addColorStop(0, '#0f172a');
    grad.addColorStop(0.5, '#1e1b4b');
    grad.addColorStop(1, '#0284c7');
    ctx.fillStyle = grad;
    ctx.fillRect(0, 0, w, h);

    // Subtle grid lines
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.06)';
    ctx.lineWidth = 1;
    for (let x = 0; x < w; x += 36) {
      ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke();
    }
    for (let y = 0; y < h; y += 36) {
      ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke();
    }

    // Simulated LAILauncher window on desktop
    const lw = 380, lh = 200, lx = (w - lw) / 2, ly = (h - lh) / 2 - 15;
    ctx.fillStyle = 'rgba(15, 23, 42, 0.88)';
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.22)';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.roundRect(lx, ly, lw, lh, 12);
    ctx.fill();
    ctx.stroke();

    // Launcher Titlebar
    ctx.fillStyle = 'rgba(255, 255, 255, 0.95)';
    ctx.font = 'bold 11.5px sans-serif';
    ctx.fillText('⚡ LAILauncher — Liquid Glass OS Studio', lx + 14, ly + 22);

    // Search bar mock
    ctx.fillStyle = 'rgba(255, 255, 255, 0.08)';
    ctx.beginPath();
    ctx.roundRect(lx + 14, ly + 36, lw - 28, 28, 6);
    ctx.fill();

    // Horizontal quick strip mock on canvas
    const pillLabels = ['Hotspot', 'Tiết kiệm pin', 'Taskbar', 'Screenshot', 'Trình chạy ngầm'];
    let px = lx + 14;
    ctx.font = '9px sans-serif';
    pillLabels.forEach((label, i) => {
      ctx.fillStyle = i === 0 && State.isHotspotOn ? '#0078D7' : 'rgba(255, 255, 255, 0.12)';
      ctx.beginPath();
      ctx.roundRect(px, ly + 72, 60, 16, 4);
      ctx.fill();
      ctx.fillStyle = '#ffffff';
      ctx.fillText(label.slice(0, 8), px + 6, ly + 83);
      px += 66;
    });

    // Taskbar at bottom
    ctx.fillStyle = 'rgba(15, 23, 42, 0.94)';
    ctx.fillRect(0, h - 34, w, 34);
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.12)';
    ctx.beginPath(); ctx.moveTo(0, h - 34); ctx.lineTo(w, h - 34); ctx.stroke();

    // Taskbar icons (Center or Left depending on State.taskbarAlign)
    const tbStart = State.taskbarAlign === 'center' ? (w / 2 - 44) : 18;
    const taskbarColors = ['#0078D7', '#22C55E', '#F59E0B', '#A855F7', '#EF4444'];
    taskbarColors.forEach((col, i) => {
      ctx.fillStyle = col;
      ctx.beginPath();
      ctx.roundRect(tbStart + i * 22, h - 26, 16, 16, 4);
      ctx.fill();
    });

    // Timestamp & date at bottom right
    ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
    ctx.font = '10px monospace';
    const nowStr = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    ctx.fillText(`${nowStr}`, w - 55, h - 13);
  }

  const modal = document.getElementById('screenshot-preview-modal');
  if (modal) modal.classList.add('visible');

  showToast('📸 Đã chụp toàn bộ màn hình! Tệp đã lưu vào Pictures\\Screenshots');
}

export function closeScreenshotModal() {
  const modal = document.getElementById('screenshot-preview-modal');
  if (modal) modal.classList.remove('visible');
}

function setupWindowDragging() {
  const titlebar = document.getElementById('window-titlebar');
  const shell = document.getElementById('launcher-shell');
  if (!titlebar || !shell) return;

  let isDragging = false;
  let startX = 0;
  let startY = 0;
  let initialOffsetX = State.windowOffsetX || 0;
  let initialOffsetY = State.windowOffsetY || 0;

  titlebar.addEventListener('mousedown', (e) => {
    // Ignore clicks on buttons, inputs, traffic dots, caption controls
    if (e.target.closest('button') || e.target.closest('input') || e.target.closest('.traffic-dot') || e.target.closest('.win-caption-btn')) {
      return;
    }

    isDragging = true;
    startX = e.clientX;
    startY = e.clientY;
    initialOffsetX = State.windowOffsetX || 0;
    initialOffsetY = State.windowOffsetY || 0;

    titlebar.classList.add('dragging');
    shell.style.transition = 'none';

    const onMouseMove = (moveEvent) => {
      if (!isDragging) return;
      const dx = moveEvent.clientX - startX;
      const dy = moveEvent.clientY - startY;

      State.windowOffsetX = initialOffsetX + dx;
      State.windowOffsetY = initialOffsetY + dy;

      shell.style.transform = `translate(${State.windowOffsetX}px, ${State.windowOffsetY}px)`;
    };

    const onMouseUp = () => {
      if (!isDragging) return;
      isDragging = false;
      titlebar.classList.remove('dragging');
      shell.style.transition = '';

      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);

      const moved = Math.abs(State.windowOffsetX - initialOffsetX) > 2 || Math.abs(State.windowOffsetY - initialOffsetY) > 2;
      if (moved || (State.windowOffsetX !== 0 || State.windowOffsetY !== 0)) {
        State.hasCustomPosition = true;
        saveWindowPosition();
        updatePositionUI();
        showToast(`📍 Đã lưu vị trí cửa sổ (X: ${Math.round(State.windowOffsetX)}, Y: ${Math.round(State.windowOffsetY)})`);
      }
    };

    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  });
}

function saveWindowPosition() {
  localStorage.setItem('lail_window_custom', 'true');
  localStorage.setItem('lail_window_offset_x', State.windowOffsetX.toString());
  localStorage.setItem('lail_window_offset_y', State.windowOffsetY.toString());
}

export function resetWindowPosition() {
  State.windowOffsetX = 0;
  State.windowOffsetY = 0;
  State.hasCustomPosition = false;

  localStorage.removeItem('lail_window_custom');
  localStorage.removeItem('lail_window_offset_x');
  localStorage.removeItem('lail_window_offset_y');

  const shell = document.getElementById('launcher-shell');
  if (shell) {
    shell.style.transform = '';
  }

  updatePositionUI();
  showToast('🎯 Đã đưa cửa sổ về vị trí trung tâm');
}

function updatePositionUI() {
  const statusEl = document.getElementById('web-saved-pos-status');
  const titlebarBtn = document.getElementById('titlebar-reset-pos-btn');
  const dockBtn = document.getElementById('dock-btn-reset-pos');

  if (State.hasCustomPosition) {
    if (statusEl) statusEl.textContent = `Vị trí cửa sổ: Tùy chỉnh (X: ${Math.round(State.windowOffsetX)}, Y: ${Math.round(State.windowOffsetY)})`;
    if (titlebarBtn) {
      titlebarBtn.classList.add('has-custom-pos');
      titlebarBtn.textContent = '🎯 Reset Vị trí';
      titlebarBtn.title = `Đang dùng vị trí đã lưu (X: ${Math.round(State.windowOffsetX)}, Y: ${Math.round(State.windowOffsetY)}). Nhấp để đưa về trung tâm.`;
    }
    if (dockBtn) {
      dockBtn.classList.add('active');
    }
  } else {
    if (statusEl) statusEl.textContent = 'Vị trí cửa sổ: Mặc định (Tự động canh giữa)';
    if (titlebarBtn) {
      titlebarBtn.classList.remove('has-custom-pos');
      titlebarBtn.textContent = '🎯 Vị trí';
      titlebarBtn.title = 'Cửa sổ đang ở vị trí trung tâm. Kéo thanh tiêu đề để di chuyển & lưu vị trí mới.';
    }
    if (dockBtn) {
      dockBtn.classList.remove('active');
    }
  }
}

function handleKeyNavigation(e) {
  if (State.isFlyoutOpen) {
    if (e.key === 'Escape') {
      closeActionFlyout();
      e.preventDefault();
      return;
    }
  }

  if (e.key === 'ArrowDown') {
    e.preventDefault();
    if (State.selectedIndex < State.currentResults.length - 1) {
      State.selectedIndex++;
      renderResults();
    }
  } else if (e.key === 'ArrowUp') {
    e.preventDefault();
    if (State.selectedIndex > 0) {
      State.selectedIndex--;
      renderResults();
    }
  } else if (e.key === 'Enter') {
    e.preventDefault();
    const item = State.currentResults[State.selectedIndex];
    if (e.ctrlKey && e.shiftKey) {
      showToast(`Chạy quyền Administrator: ${item?.title}`);
    } else if (e.ctrlKey) {
      showToast(`Mở thư mục trong File Explorer: ${item?.title}`);
    } else {
      executeAction(item);
    }
  } else if (e.key === 'Tab') {
    e.preventDefault();
    if (State.isFlyoutOpen) {
      closeActionFlyout();
    } else {
      openActionFlyout(e);
    }
  } else if (e.ctrlKey && e.altKey && e.key.toLowerCase() === 's') {
    e.preventDefault();
    const item = State.currentResults[State.selectedIndex];
    if (item) {
      openAiDrawer(`Hãy đọc và tóm tắt ngắn gọn các ý chính của tài liệu "${item.title}":\n\n1. Tổng quan nội dung tài liệu\n2. Các điểm số liệu cốt lõi\n3. Đề xuất hành động tiếp theo.`);
    }
  } else if (e.key === 'Escape') {
    e.preventDefault();
    if (State.isAiDrawerOpen) {
      closeAiDrawer();
    } else if (State.query) {
      const searchInput = document.getElementById('search-input');
      if (searchInput) {
        searchInput.value = '';
        filterQuery('');
      }
    } else {
      toggleLauncherVisibility();
    }
  }
}

function toggleLauncherVisibility() {
  const shell = document.getElementById('launcher-shell');
  const searchInput = document.getElementById('search-input');
  if (!shell) return;

  const isHidden = shell.style.display === 'none';
  if (isHidden) {
    shell.style.display = 'flex';
    searchInput?.focus();
    showToast('Đã mở LAILauncher');
  } else {
    shell.style.display = 'none';
    closeActionFlyout();
    closeAiDrawer();
    showToast('Đã ẩn LAILauncher (Nhấn Alt+Space để hiện)');
  }
}

function toggleSettingsModal() {
  const overlay = document.getElementById('settings-overlay');
  if (!overlay) return;
  State.isSettingsOpen = !State.isSettingsOpen;
  overlay.classList.toggle('visible', State.isSettingsOpen);
}

// ============================================================================
// 10. THEME & WALLPAPER SWATCHES
// ============================================================================

function renderThemeSwatches() {
  const grid = document.getElementById('theme-swatch-grid');
  if (!grid) return;

  grid.innerHTML = '';
  Themes.forEach(th => {
    const card = document.createElement('div');
    card.className = `theme-swatch-card ${th.id === State.theme ? 'active' : ''}`;
    card.setAttribute('data-theme-id', th.id);
    card.innerHTML = `
      <div class="swatch-color-pill" style="background: ${th.color}"></div>
      <div class="swatch-label">${th.name}</div>
    `;

    card.addEventListener('click', () => {
      State.theme = th.id;
      State.customAccent = null; // Reset custom accent so theme signature colors take effect
      applyAppearance();
      renderThemeSwatches();
      renderAccentChips();
      showToast(`Đã áp dụng theme: ${th.name}`);
    });

    grid.appendChild(card);
  });
}

function renderAccentChips() {
  const container = document.getElementById('accent-chips-palette');
  if (!container) return;

  container.innerHTML = '';
  const activeHex = getActiveAccentHex().toUpperCase();

  AccentColors.forEach(chip => {
    const el = document.createElement('div');
    const chipHex = chip.hex.toUpperCase();
    const isActive = State.customAccent ? (chipHex === activeHex) : false;
    
    el.className = `accent-chip ${isActive ? 'active' : ''}`;
    el.style.backgroundColor = chip.hex;
    el.title = `${chip.name} (${chip.hex})`;
    el.setAttribute('data-hex', chip.hex);

    el.addEventListener('click', () => {
      State.customAccent = chip.hex;
      applyAppearance();
      renderAccentChips();
      showToast(`Đã áp dụng màu điểm nhấn: ${chip.name} (${chip.hex})`);
    });

    container.appendChild(el);
  });
}

function renderWallpaperSwatches() {
  const grid = document.getElementById('wallpaper-grid');
  if (!grid) return;

  grid.innerHTML = '';
  Wallpapers.forEach(wp => {
    const card = document.createElement('div');
    card.className = `wallpaper-card ${wp.class} ${wp.id === State.wallpaper ? 'active' : ''}`;
    card.innerHTML = `<span>${wp.name}</span>`;

    card.addEventListener('click', () => {
      State.wallpaper = wp.id;
      applyAppearance();
      renderWallpaperSwatches();
      showToast(`Đã đổi hình nền: ${wp.name}`);
    });

    grid.appendChild(card);
  });
}

// ============================================================================
// 11. HELPERS: MATH, CLOCK, TOAST & MARKDOWN
// ============================================================================

function isMathExpression(str) {
  if (!str) return false;
  // Match arithmetic symbols, numbers, and basic math functions
  return /^[0-9\s\+\-\*\/\%\^\(\)\.\,]+$/.test(str) && /[\+\-\*\/\%\^]/.test(str);
}

function evaluateMath(expr) {
  try {
    // Clean string and evaluate safely
    const sanitized = expr.replace(/\^/g, '**');
    const result = Function(`'use strict'; return (${sanitized})`)();
    return Number.isFinite(result) ? `= ${result.toLocaleString('vi-VN')}` : 'Lỗi phép tính';
  } catch {
    return 'Đang nhập phép tính...';
  }
}

function updateClock() {
  const clockEl = document.getElementById('clock-display');
  const gnomeClock = document.getElementById('gnome-clock');
  const now = new Date();
  const timeStr = now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  const dateStr = now.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' });

  if (clockEl) clockEl.textContent = `${dateStr} • ${timeStr}`;
  if (gnomeClock) gnomeClock.textContent = `${now.toLocaleDateString('vi-VN', { weekday: 'short', month: 'short', day: 'numeric' })} ${timeStr}`;
}

export function showToast(message) {
  const shelf = document.getElementById('toast-shelf');
  if (!shelf) return;

  const toast = document.createElement('div');
  toast.className = 'toast-badge';
  toast.textContent = message;
  shelf.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(-8px) scale(0.95)';
    toast.style.transition = 'all 0.25s ease';
    setTimeout(() => toast.remove(), 250);
  }, 2800);
}

function escapeHtml(text) {
  if (!text) return '';
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function formatMarkdown(text) {
  return text
    .replace(/^### (.*$)/gim, '<h3 style="font-size:15px; font-weight:700; margin:8px 0;">$1</h3>')
    .replace(/^## (.*$)/gim, '<h2 style="font-size:16px; font-weight:700; margin:10px 0;">$1</h2>')
    .replace(/\*\*(.*?)\*\*/gim, '<strong>$1</strong>')
    .replace(/\*(.*?)\*/gim, '<em>$1</em>')
    .replace(/```csharp([\s\S]*?)```/gim, '<pre class="ai-stream-codeblock"><code>$1</code></pre>')
    .replace(/```([\s\S]*?)```/gim, '<pre class="ai-stream-codeblock"><code>$1</code></pre>')
    .replace(/\n/g, '<br>');
}
