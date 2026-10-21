export class Canvas2DRenderer {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        if (!this.ctx) throw new Error('Canvas2D context failed to initialize');
        this.cells = null;
        this.rows = 0;
        this.cols = 0;
        this.states = 1;
        this.palette = new Uint8Array(256 * 4);
    }

    setPalette(rgba) {
        this.palette = rgba;
        this._render();
    }

    setData(cells, rows, cols, states) {
        this.cells = cells;
        this.rows = rows;
        this.cols = cols;
        this.states = states;
        this.canvas.width = cols;
        this.canvas.height = rows;
        this._render();
    }

    setColoring() {}

    _render() {
        if (!this.cells || this.rows === 0) return;
        const imgData = this.ctx.createImageData(this.cols, this.rows);
        const data = imgData.data;

        for (let i = 0; i < this.cells.length; i++) {
            const state = this.cells[i];
            const paletteIdx = Math.floor((state / this.states) * 255);
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