export class VectorRenderer {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        if (!this.ctx) throw new Error('Canvas2D context failed to initialize');
        this.points = [];
        this.palette = new Uint8Array(256 * 4);
        this.bounds = null;
    }

    setPalette(rgba) {
        this.palette = rgba;
        this._render();
    }

    setData(points) {
        this.points = points;
        this.bounds = null;

        if (!points || points.length === 0) return;

        // 1. Считаем границы (Bounding Box) с защитой от NaN/null
        let minX = Infinity, maxX = -Infinity;
        let minY = Infinity, maxY = -Infinity;
        
        for (const p of points) {
            const x = parseFloat(p.x) || 0;
            const y = parseFloat(p.y) || 0;
            
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        const width = Math.max(maxX - minX, 1);
        const height = Math.max(maxY - minY, 1);
        
        // 2. Задаем размер холста с отступом (padding)
        const padding = 50;
        this.canvas.width = width + padding * 2;
        this.canvas.height = height + padding * 2;

        this.bounds = { minX, minY, padding };

        console.log(`[Vector] Отрисовка ${points.length} точек. Диапазон X: ${minX.toFixed(1)}..${maxX.toFixed(1)}, Y: ${minY.toFixed(1)}..${maxY.toFixed(1)}`);

        this._render();
    }

    setColoring() {}

    _render() {
        if (!this.points || this.points.length === 0 || !this.bounds) return;

        const ctx = this.ctx;
        const { minX, minY, padding } = this.bounds;
        
        ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        // Берем цвет из палитры (например, 200-й индекс — яркий оттенок)
        const colorIdx = 200; 
        const r = this.palette[colorIdx * 4];
        const g = this.palette[colorIdx * 4 + 1];
        const b = this.palette[colorIdx * 4 + 2];

        ctx.strokeStyle = `rgb(${r}, ${g}, ${b})`;
        ctx.lineWidth = 2;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        
        ctx.shadowBlur = 8;
        ctx.shadowColor = `rgb(${r}, ${g}, ${b})`;

        ctx.beginPath();
        const p0 = this.points[0];
        ctx.moveTo(p0.x - minX + padding, p0.y - minY + padding);

        for (let i = 1; i < this.points.length; i++) {
            const p = this.points[i];
            ctx.lineTo(p.x - minX + padding, p.y - minY + padding);
        }
        ctx.stroke();
    }
}