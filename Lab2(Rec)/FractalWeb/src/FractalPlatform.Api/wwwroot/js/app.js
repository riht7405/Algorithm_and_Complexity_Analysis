import { listFractals, computeFractal } from './api.js';
import { PALETTE_NAMES } from './palettes.js';
import { WebGLRenderer } from './renderer-webgl.js';
import { Canvas2DRenderer } from './renderer-canvas2d.js';
import { VectorRenderer } from './renderer-vector.js';
import { HeatmapRenderer } from './renderer-heatmap.js';
import { StyleEngine } from './style-engine.js';
import { PanZoomController } from './pan-zoom.js';

try {
    const canvasGL = document.getElementById('canvas-gl');
    const canvas2D = document.getElementById('canvas-2d');
    const errorEl = document.getElementById('error');
    const zoomControls = document.getElementById('zoom-controls');
    
    // Скрываем старые кнопки, так как теперь работает колесико
    if (zoomControls) zoomControls.style.display = 'none';

    const stats = {
        compute: document.getElementById('st-compute'),
        size: document.getElementById('st-size'),
        total: document.getElementById('st-total')
    };

    // Инициализируем Pan/Zoom для обоих канвасов
    const panZoomGL = new PanZoomController(canvasGL);
    const panZoom2D = new PanZoomController(canvas2D);

    const renderers = {
        'IterationMap': { renderer: new WebGLRenderer(canvasGL), canvas: canvasGL, pz: panZoomGL },
        'CellGrid':     { renderer: new Canvas2DRenderer(canvas2D), canvas: canvas2D, pz: panZoom2D },
        'PointPath':    { renderer: new VectorRenderer(canvas2D), canvas: canvas2D, pz: panZoom2D },
        'Spectrum':     { renderer: new HeatmapRenderer(canvas2D), canvas: canvas2D, pz: panZoom2D }
    };

    let activeType = 'IterationMap';
    canvasGL.classList.add('active');

    const styleEngine = new StyleEngine();
    styleEngine.setRenderer(renderers[activeType].renderer, renderers[activeType].canvas);

    function activate(type) {
        if (activeType === type) return;
        
        // Сбрасываем зум/позицию старого канваса
        renderers[activeType].pz.reset();
        renderers[activeType].canvas.classList.remove('active');
        
        activeType = type;
        renderers[type].canvas.classList.add('active');
        
        // Сбрасываем зум/позицию нового канваса
        renderers[type].pz.reset();

        styleEngine.setRenderer(renderers[type].renderer, renderers[type].canvas);
    }

    // --- Палитра ---
    const paletteSelect = document.getElementById('palette-select');
    PALETTE_NAMES.forEach(name => {
        const opt = document.createElement('option');
        opt.value = name;
        opt.textContent = name;
        paletteSelect.appendChild(opt);
    });
    paletteSelect.value = 'inferno';
    paletteSelect.addEventListener('change', () => styleEngine.applyPalette(paletteSelect.value));

    // --- Состояние ---
    const state = {
        fractalId: 'thorn',
        params: {},
        currentRequest: null
    };

    const paramsContainer = document.getElementById('params-container');
    const debounce = (fn, ms) => {
        let t;
        return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
    };

    const renderTrigger = debounce(() => requestRender(), 300);

    // --- Построение UI параметров ---
    function buildParamsUI(parameters) {
        paramsContainer.innerHTML = '';
        state.params = {};

        parameters.forEach(p => {
            const label = document.createElement('label');
            label.textContent = p.label;

            let input;
            if (p.type === 'select') {
                input = document.createElement('select');
                p.options.forEach(opt => {
                    const o = document.createElement('option');
                    o.value = opt;
                    o.textContent = opt;
                    input.appendChild(o);
                });
            } else {
                input = document.createElement('input');
                input.type = 'number';
                if (p.step) input.step = p.step;
                if (p.min !== undefined) input.min = p.min;
                if (p.max !== undefined) input.max = p.max;
            }
            
            input.value = p.defaultValue;
            state.params[p.name] = p.defaultValue;

            input.addEventListener('input', e => {
                let val = e.target.value;
                if (p.type === 'int') val = parseInt(val, 10);
                else if (p.type === 'number') val = parseFloat(val);
                
                if (val !== '' && !isNaN(val) || p.type === 'select') {
                    state.params[p.name] = val;
                    renderTrigger();
                }
            });

            label.appendChild(input);
            paramsContainer.appendChild(label);
        });
    }

    // --- Загрузка фракталов ---
    const fractalSelect = document.getElementById('fractal-select');
    listFractals().then(list => {
        list.forEach(f => {
            const opt = document.createElement('option');
            opt.value = f.id;
            opt.textContent = f.displayName;
            fractalSelect.appendChild(opt);
        });
        fractalSelect.value = state.fractalId;

        const firstFractal = list.find(f => f.id === state.fractalId);
        if (firstFractal) {
            buildParamsUI(firstFractal.parameters);
            requestRender();
        }
    }).catch(e => showError(`API: ${e.message}`));

    fractalSelect.addEventListener('change', () => {
        state.fractalId = fractalSelect.value;
        
        // Находим параметры для выбранного фрактала
        listFractals().then(list => {
            const f = list.find(x => x.id === state.fractalId);
            if (f) {
                buildParamsUI(f.parameters);
                requestRender();
            }
        });
    });

    // --- Настройки раскраски ---
    document.getElementById('color-mode').addEventListener('change', e => {
        if (renderers[activeType].renderer.setColoring) {
            renderers[activeType].renderer.setColoring({ mode: parseInt(e.target.value, 10) });
        }
    });

    document.getElementById('s-frequency').addEventListener('input', e => {
        if (renderers[activeType].renderer.setColoring) {
            renderers[activeType].renderer.setColoring({ frequency: parseFloat(e.target.value) });
        }
    });

    document.getElementById('s-brightness').addEventListener('input', e => styleEngine.setBrightness(parseInt(e.target.value, 10)));
    document.getElementById('s-contrast').addEventListener('input', e => styleEngine.setContrast(parseInt(e.target.value, 10)));
    document.getElementById('s-glow').addEventListener('input', e => styleEngine.setGlow(parseInt(e.target.value, 10)));

    // --- Запрос рендера ---
    async function requestRender() {
        if (state.currentRequest) state.currentRequest.abort();
        const controller = new AbortController();
        state.currentRequest = controller;

        stats.compute.textContent = '...';
        stats.total.textContent = '...';

        try {
            hideError();
            const result = await computeFractal(state.fractalId, state.params, controller.signal);

            activate(result.resultType);

            if (result.resultType === 'IterationMap') {
                renderers['IterationMap'].renderer.setData(result.iterations, result.width, result.height, result.maxIter);
                stats.size.textContent = `${result.width}×${result.height}`;
            } else if (result.resultType === 'CellGrid') {
                renderers['CellGrid'].renderer.setData(result.cells, result.rows, result.cols, result.states);
                stats.size.textContent = `${result.cols}×${result.rows}`;
            } else if (result.resultType === 'PointPath') {
                renderers['PointPath'].renderer.setData(result.points);
                stats.size.textContent = `${result.points.length} pts`;
            } else if (result.resultType === 'Spectrum') {
                renderers['Spectrum'].renderer.setData(result.density, result.width, result.height);
                stats.size.textContent = `${result.width}×${result.height}`;
            }

            stats.compute.textContent = `${result.meta.computeMs} ms`;
            stats.total.textContent = `${result.meta.totalMs.toFixed(0)} ms`;
        } catch (e) {
            if (e.name === 'AbortError') return;
            showError(e.message);
            stats.compute.textContent = 'Error';
            stats.total.textContent = 'Error';
        } finally {
            if (state.currentRequest === controller) state.currentRequest = null;
        }
    }

    function showError(msg) { errorEl.textContent = msg; errorEl.hidden = false; console.error(msg); }
    function hideError() { errorEl.hidden = true; }

} catch (e) {
    const errorEl = document.getElementById('error');
    if (errorEl) { errorEl.textContent = e.message; errorEl.hidden = false; }
    console.error(e);
}