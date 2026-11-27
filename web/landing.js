// LAILauncher - Landing Page Controller & Interactions
import {
  toggleLightDarkMode,
  setTheme,
  filterQuery,
  Themes,
  getAppearanceState
} from './app.js';

if (typeof document !== 'undefined') {
  document.addEventListener('DOMContentLoaded', () => {
    initNavbar();
    initFaqAccordion();
    initCliCopy();
    initDownloadFeedback();
    initNavScrollSpy();
    initThemeSync();
    initNavThemePicker();
    initDemoPromptChips();
    initScrollReveal();
  });
}

// 1. Navbar Sticky & Mobile Menu Toggle
function initNavbar() {
  const nav = document.querySelector('.landing-nav');
  const toggleBtn = document.getElementById('nav-mobile-toggle');
  const navLinks = document.querySelector('.nav-links');

  window.addEventListener('scroll', () => {
    if (nav) {
      nav.classList.toggle('scrolled', window.scrollY > 40);
    }
  }, { passive: true });

  if (toggleBtn && navLinks) {
    toggleBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      const open = navLinks.classList.toggle('mobile-open');
      toggleBtn.setAttribute('aria-expanded', open ? 'true' : 'false');
    });

    document.addEventListener('click', (e) => {
      if (navLinks.classList.contains('mobile-open') &&
          !navLinks.contains(e.target) &&
          e.target !== toggleBtn) {
        navLinks.classList.remove('mobile-open');
        toggleBtn.setAttribute('aria-expanded', 'false');
      }
    });
  }

  // Smooth scroll for nav anchor links
  document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
      const targetId = this.getAttribute('href');
      if (!targetId || targetId === '#') return;

      const targetElement = document.querySelector(targetId);
      if (targetElement) {
        e.preventDefault();
        const headerOffset = 78;
        const elementPosition = targetElement.getBoundingClientRect().top;
        const offsetPosition = elementPosition + window.pageYOffset - headerOffset;

        window.scrollTo({
          top: offsetPosition,
          behavior: 'smooth'
        });

        if (navLinks) {
          navLinks.classList.remove('mobile-open');
          toggleBtn?.setAttribute('aria-expanded', 'false');
        }
      }
    });
  });
}

// 2. Active Section Spy for Navigation Links
function initNavScrollSpy() {
  const sections = document.querySelectorAll('section[id]');
  const navLinks = document.querySelectorAll('.nav-link');
  if (!sections.length || !navLinks.length) return;

  const updateSpy = () => {
    let currentId = '';
    const scrollPos = window.scrollY + 130;

    sections.forEach(section => {
      const top = section.offsetTop;
      const height = section.offsetHeight;
      if (scrollPos >= top && scrollPos < top + height) {
        currentId = section.getAttribute('id') || '';
      }
    });

    navLinks.forEach(link => {
      link.classList.toggle('active', link.getAttribute('href') === `#${currentId}`);
    });
  };

  window.addEventListener('scroll', updateSpy, { passive: true });
  updateSpy();
}

// 3. FAQ Accordion Interaction
function initFaqAccordion() {
  const faqItems = document.querySelectorAll('.faq-item');

  faqItems.forEach(item => {
    const trigger = item.querySelector('.faq-trigger');
    if (!trigger) return;

    trigger.addEventListener('click', () => {
      const isActive = item.classList.contains('active');

      faqItems.forEach(other => {
        if (other !== item) other.classList.remove('active');
      });

      item.classList.toggle('active', !isActive);
    });
  });
}

// 4. CLI & Checksum Snippet 1-Click Copy
function initCliCopy() {
  const copyButtons = document.querySelectorAll('.cli-copy-btn, .checksum-copy-btn');

  copyButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const textToCopy = btn.getAttribute('data-copy');
      if (!textToCopy) return;

      const finish = () => {
        const originalText = btn.innerHTML;
        btn.innerHTML = '✓ Đã chép!';
        btn.style.color = '#10b981';

        setTimeout(() => {
          btn.innerHTML = originalText;
          btn.style.color = '';
        }, 2000);
      };

      if (navigator.clipboard?.writeText) {
        navigator.clipboard.writeText(textToCopy).then(finish).catch(() => {});
      }
    });
  });
}

// 5. Download Trigger & Feedback Toast
function initDownloadFeedback() {
  const dlLinks = document.querySelectorAll('.dl-btn, .btn-hero-primary');

  dlLinks.forEach(link => {
    link.addEventListener('click', () => {
      const isAnchorDownload = link.hasAttribute('download') || link.getAttribute('href')?.includes('downloads/');
      if (isAnchorDownload) {
        showLandingToast('🎉 Đang khởi động tải xuống LAILauncher... Cảm ơn bạn!');
      }
    });
  });
}

// 6. Theme Mode Synchronization (Header <-> app.js State <-> Dock <-> Settings)
function syncNavAppearanceUI() {
  const { mode, theme } = getAppearanceState();
  const themeToggle = document.getElementById('nav-theme-toggle');
  if (themeToggle) {
    const isLight = mode === 'light';
    themeToggle.textContent = isLight ? '☀️' : '🌙';
    themeToggle.setAttribute('aria-pressed', isLight ? 'true' : 'false');
    themeToggle.title = isLight ? 'Chuyển sang Chế độ Tối (Ctrl + D)' : 'Chuyển sang Chế độ Sáng (Ctrl + D)';
  }

  document.querySelectorAll('.nav-theme-option').forEach(opt => {
    opt.classList.toggle('active', opt.getAttribute('data-theme-id') === theme);
  });
}

function initThemeSync() {
  const themeToggle = document.getElementById('nav-theme-toggle');
  syncNavAppearanceUI();

  if (themeToggle) {
    themeToggle.addEventListener('click', () => {
      toggleLightDarkMode();
    });
  }

  window.addEventListener('lailauncher:appearance', syncNavAppearanceUI);
}

// 7. Navigation 9-Theme Picker Dropdown
function initNavThemePicker() {
  const pickerBtn = document.getElementById('nav-theme-picker-btn');
  const dropdown = document.getElementById('nav-theme-dropdown');
  if (!pickerBtn || !dropdown) return;

  const currentTheme = getAppearanceState().theme;
  dropdown.innerHTML = '';

  const headerEl = document.createElement('div');
  headerEl.className = 'nav-theme-dropdown-header';
  headerEl.textContent = 'Chọn bảng màu (9 Themes)';
  dropdown.appendChild(headerEl);

  Themes.forEach(t => {
    const item = document.createElement('button');
    item.type = 'button';
    item.className = `nav-theme-option ${t.id === currentTheme ? 'active' : ''}`;
    item.setAttribute('data-theme-id', t.id);
    item.setAttribute('role', 'menuitem');
    item.innerHTML = `
      <span class="nav-theme-option-swatch" style="background: ${t.color};"></span>
      <span class="nav-theme-option-name">${t.name}</span>
      <span class="nav-theme-option-os">${t.os === 'linux' ? 'GNOME' : 'Win11'}</span>
    `;
    item.addEventListener('click', (e) => {
      e.stopPropagation();
      setTheme(t.id);
      dropdown.classList.remove('open');
      pickerBtn.setAttribute('aria-expanded', 'false');
    });
    dropdown.appendChild(item);
  });

  pickerBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    const isOpen = dropdown.classList.toggle('open');
    pickerBtn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
  });

  document.addEventListener('click', (e) => {
    if (dropdown.classList.contains('open') &&
        !dropdown.contains(e.target) &&
        e.target !== pickerBtn) {
      dropdown.classList.remove('open');
      pickerBtn.setAttribute('aria-expanded', 'false');
    }
  });
}

// 8. Quick Try Prompt Chips Above Interactive Demo
function initDemoPromptChips() {
  const chips = document.querySelectorAll('.demo-prompt-chip');
  const searchInput = document.getElementById('search-input');
  if (!chips.length || !searchInput) return;

  chips.forEach(chip => {
    chip.addEventListener('click', () => {
      const q = chip.getAttribute('data-query') || '';
      chips.forEach(c => c.classList.remove('active'));
      chip.classList.add('active');

      searchInput.value = q;
      searchInput.focus();
      filterQuery(q);
    });
  });
}

// 9. Smooth Reveal Animation on Scroll
function initScrollReveal() {
  const targets = document.querySelectorAll('.metric-card, .feature-card, .dl-card, .step-card, .shortcut-row, .faq-item');
  if (!targets.length || typeof IntersectionObserver === 'undefined') return;

  const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.classList.add('reveal-visible');
        observer.unobserve(entry.target);
      }
    });
  }, { threshold: 0.08, rootMargin: '0px 0px -30px 0px' });

  targets.forEach(el => {
    el.classList.add('reveal-ready');
    observer.observe(el);
  });
}

// Toast Notification Helper (Fallback if app toast shelf not yet populated)
function showLandingToast(msg) {
  let shelf = document.getElementById('toast-shelf');
  if (!shelf) {
    shelf = document.createElement('div');
    shelf.id = 'toast-shelf';
    shelf.className = 'toast-shelf';
    document.body.appendChild(shelf);
  }

  const toast = document.createElement('div');
  toast.className = 'toast-badge';
  toast.style.background = 'linear-gradient(135deg, var(--accent-base) 0%, var(--accent-hover) 100%)';
  toast.style.color = '#ffffff';
  toast.style.boxShadow = '0 8px 24px var(--accent-glow)';
  toast.textContent = msg;
  shelf.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(-10px) scale(0.95)';
    toast.style.transition = 'all 0.3s ease';
    setTimeout(() => toast.remove(), 300);
  }, 3200);
}
