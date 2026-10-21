import { getPalette } from './palettes.js';

export class StyleEngine {
    constructor() {
        this.renderer = null;
        this.canvas = null;
        this.paletteName = 'inferno';
        this.filters = { brightness: 100, contrast: 100, glow: 0 };
    }

    setRenderer(renderer, canvas) {
        this.renderer = renderer;
        this.setCanvas(canvas);
        if (this.renderer) {
            this.applyPalette(this.paletteName);
        }
    }

    setCanvas(canvas) {
        if (this.canvas && this.canvas !== canvas) {
            this.canvas.style.filter = '';
            this.canvas.style.color = '';
        }
        this.canvas = canvas;
        this.applyFilters();
    }

    applyPalette(name) {
        this.paletteName = name;
        if (!this.renderer) return;
        const rgba = getPalette(name, 256);
        this.renderer.setPalette(rgba);
    }

    applyFilters() {
        if (!this.canvas) return;
        const { brightness, contrast, glow } = this.filters;
        const parts = [
            `brightness(${brightness}%)`,
            `contrast(${contrast}%)`
        ];
        if (glow > 0) {
            parts.push(`drop-shadow(0 0 ${glow}px currentColor)`);
            this.canvas.style.color = '#3b82f6';
        } else {
            this.canvas.style.color = '';
        }
        this.canvas.style.filter = parts.join(' ');
    }

    setBrightness(v) { this.filters.brightness = v; this.applyFilters(); }
    setContrast(v)   { this.filters.contrast = v;   this.applyFilters(); }
    setGlow(v)       { this.filters.glow = v;       this.applyFilters(); }
}