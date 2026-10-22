export class HeatmapRenderer {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        if (!this.ctx) throw new Error('Canvas2D context failed to initialize');
        this.density = null;
        this.width = 0;
        this.height = 0;
        this.palette = new Uint8Array(256 * 4);
    }

    setPalette(rgba) {
        this.palette = rgba;
        this._render();
    }

    setData(density, width, height) {
        this.density = density;
        this.width = width;
        this.height = height;
        
        this.canvas.width = width;
        this.canvas.height = height;
        
        this._render();
    }

    setColoring() {}

    _render() {
        if (!this.density || this.width === 0) return;
        
        const imgData = this.ctx.createImageData(this.width, this.height);
        const data = imgData.data;

        for (let i = 0; i < this.density.length; i++) {
            const val = this.density[i]; // от 0.0 до 1.0
            const paletteIdx = Math.floor(val * 255);
            const pOffset = paletteIdx * 4;
            const dOffset = i * 4;

            data[dOffset + 0] = this.palette[pOffset + 0];
            data[dOffset + 1] = this.palette[pOffset + 1];
            data[dOffset + 2] = this.palette[pOffset + 2];
            data[dOffset + 3] = 255;
        }
        this.ctx.putImageData(imgData, 0, 0);
    }
}