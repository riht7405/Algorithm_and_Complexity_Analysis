export class PanZoomController {
    constructor(canvas, onZoomChange) {
        this.canvas = canvas;
        this.scale = 1;
        this.panX = 0;
        this.panY = 0;
        this.isDragging = false;
        this.lastX = 0;
        this.lastY = 0;
        
        // Коллбэк для обновления UI (например, зума в статусе)
        this.onZoomChange = onZoomChange || (() => {});

        this._bindEvents();
    }

    _bindEvents() {
        // Колесико мыши (Zoom)
        this.canvas.addEventListener('wheel', (e) => {
            e.preventDefault(); // Останавливаем скролл страницы
            
            const zoomFactor = e.deltaY > 0 ? 0.9 : 1.1;
            this.scale = Math.max(0.1, Math.min(this.scale * zoomFactor, 50));
            
            this._applyTransform();
        }, { passive: false });

        // Таскание мышкой (Pan)
        this.canvas.addEventListener('mousedown', (e) => {
            this.isDragging = true;
            this.lastX = e.clientX;
            this.lastY = e.clientY;
            this.canvas.style.cursor = 'grabbing';
        });

        window.addEventListener('mousemove', (e) => {
            if (!this.isDragging) return;
            
            const dx = e.clientX - this.lastX;
            const dy = e.clientY - this.lastY;
            
            this.panX += dx;
            this.panY += dy;
            
            this.lastX = e.clientX;
            this.lastY = e.clientY;
            
            this._applyTransform();
        });

        window.addEventListener('mouseup', () => {
            this.isDragging = false;
            this.canvas.style.cursor = 'grab';
        });

        // Сброс (Двойной клик)
        this.canvas.addEventListener('dblclick', () => {
            this.reset();
        });

        this.canvas.style.cursor = 'grab';
    }

    _applyTransform() {
        // Применяем CSS-трансформацию. 
        // Картинка не перерисовывается, браузер просто двигает её на GPU.
        this.canvas.style.transform = `translate(${this.panX}px, ${this.panY}px) scale(${this.scale})`;
        this.onZoomChange(this.scale);
    }

    reset() {
        this.scale = 1;
        this.panX = 0;
        this.panY = 0;
        this._applyTransform();
    }
}