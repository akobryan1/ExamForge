import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';


export interface AppSettings {
  apiKey?: string;
  model: string;
  themeColor: string;
  fontPreset: string;
}

export const settingsKeys = {
  all: ['settings'] as const,
};

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

async function fetchSettings(): Promise<AppSettings> {
  const token = localStorage.getItem('accessToken');
  const res = await fetch(`${API_BASE}/api/settings`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  if (!res.ok) throw new Error('Failed to fetch settings');
  const data = await res.json();
  return {
    apiKey: data.apiKey,
    model: data.model || 'deepseek/deepseek-chat',
    themeColor: data.themeColor || '#1B2540',
    fontPreset: data.fontPreset || 'editorial',
  };
}

async function saveSettings(settings: Partial<AppSettings>): Promise<void> {
  const token = localStorage.getItem('accessToken');
  await fetch(`${API_BASE}/api/settings`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: token ? `Bearer ${token}` : '',
    },
    body: JSON.stringify(settings),
  });
}

/**
 * Hook for reading and writing settings (theme, font, AI config).
 * Caches settings in localStorage for instant apply on page load.
 */
export function useSettings() {
  const qc = useQueryClient();

  const query = useQuery({
    queryKey: settingsKeys.all,
    queryFn: fetchSettings,
    staleTime: 1000 * 60 * 5,
  });

  const mutation = useMutation({
    mutationFn: saveSettings,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: settingsKeys.all });
    },
  });

  return { query, mutation };
}

/**
 * Apply theme/font CSS custom properties from settings.
 * Call on settings load or when user changes a setting.
 */
export function applyThemeSettings(settings: { themeColor?: string; fontPreset?: string }): void {
  const root = document.documentElement;

  if (settings.themeColor) {
    const hex = settings.themeColor;
    root.style.setProperty('--ledger-cover-1', hex);
    // Derive cover-2: lighten the base color by ~12%
    const lightened = lightenHex(hex, 12);
    root.style.setProperty('--ledger-cover-2', lightened);
    // Determine if text should be light or dark on sidebar
    const isLight = isLightColor(hex);
    root.style.setProperty('--ledger-cover-text', isLight ? 'rgba(30,42,58,0.85)' : 'rgba(235,236,240,0.82)');
    root.style.setProperty('--ledger-cover-line', isLight ? 'rgba(30,42,58,0.12)' : 'rgba(255,255,255,0.12)');
  }
}

/**
 * Cache settings to localStorage for instant load on next page visit.
 */
export function cacheSettingsLocally(settings: Partial<AppSettings>): void {
  const existing = JSON.parse(localStorage.getItem('themeSettings') || '{}');
  const updated = { ...existing, ...settings };
  localStorage.setItem('themeSettings', JSON.stringify(updated));
}

/**
 * Load cached settings from localStorage and apply them immediately.
 * Call this synchronously before React renders (in main.tsx).
 */
export function loadCachedSettings(): void {
  try {
    const cached = JSON.parse(localStorage.getItem('themeSettings') || '{}');
    if (cached.themeColor) {
      applyThemeSettings({ themeColor: cached.themeColor });
    }
  } catch {
    // Ignore parse errors
  }
}

// ── Colour utilities ──

function hexToHsl(hex: string): { h: number; s: number; l: number } {
  let r = 0, g = 0, b = 0;
  const cleaned = hex.replace('#', '');
  if (cleaned.length === 3) {
    r = parseInt(cleaned[0] + cleaned[0], 16);
    g = parseInt(cleaned[1] + cleaned[1], 16);
    b = parseInt(cleaned[2] + cleaned[2], 16);
  } else {
    r = parseInt(cleaned.substring(0, 2), 16);
    g = parseInt(cleaned.substring(2, 4), 16);
    b = parseInt(cleaned.substring(4, 6), 16);
  }
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  let h = 0, s = 0;
  const l = (max + min) / 2;
  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    switch (max) {
      case r: h = ((g - b) / d + (g < b ? 6 : 0)) / 6; break;
      case g: h = ((b - r) / d + 2) / 6; break;
      case b: h = ((r - g) / d + 4) / 6; break;
    }
  }
  return { h: h * 360, s: s * 100, l: l * 100 };
}

function hslToHex(h: number, s: number, l: number): string {
  s /= 100; l /= 100;
  const a = s * Math.min(l, 1 - l);
  const f = (n: number) => {
    const k = (n + h / 30) % 12;
    const color = l - a * Math.max(Math.min(k - 3, 9 - k, 1), -1);
    return Math.round(255 * color).toString(16).padStart(2, '0');
  };
  return `#${f(0)}${f(8)}${f(4)}`;
}

function lightenHex(hex: string, percent: number): string {
  const hsl = hexToHsl(hex);
  hsl.l = Math.min(hsl.l + percent, 95);
  return hslToHex(hsl.h, hsl.s, hsl.l);
}

function isLightColor(hex: string): boolean {
  const hsl = hexToHsl(hex);
  return hsl.l > 55;
}
