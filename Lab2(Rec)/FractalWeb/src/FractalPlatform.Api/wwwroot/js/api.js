export async function listFractals() {
    const r = await fetch('/api/fractals', { cache: 'no-store' });
    if (!r.ok) throw new Error(`List failed: ${r.status}`);
    return r.json();
}

export async function computeFractal(fractalId, params, signal) {
    const t0 = performance.now();
    const response = await fetch(`/api/fractals/${fractalId}/compute?t=${Date.now()}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(params),
        signal,
        cache: 'no-store'
    });

    if (!response.ok) {
        const text = await response.text();
        throw new Error(`Compute failed: ${response.status} ${text}`);
    }
    
    const buffer = await response.arrayBuffer();
    const totalMs = performance.now() - t0;
    const resultType = response.headers.get('X-Fractal-ResultType');
    const computeMs = parseInt(response.headers.get('X-Fractal-ComputeMs'), 10) || 0;

    // 1. Растровые данные (Тёрн)
    if (resultType === 'IterationMap') {
        const width = parseInt(response.headers.get('X-Fractal-Width'), 10);
        const height = parseInt(response.headers.get('X-Fractal-Height'), 10);
        const maxIter = parseInt(response.headers.get('X-Fractal-MaxIter'), 10);
        
        return {
            resultType,
            iterations: new Uint16Array(buffer),
            width, height, maxIter,
            meta: { computeMs, totalMs }
        };
    } 
    // 2. Сеточные данные (Кувина)
    else if (resultType === 'CellGrid') {
        const rows = parseInt(response.headers.get('X-Fractal-Rows'), 10);
        const cols = parseInt(response.headers.get('X-Fractal-Cols'), 10);
        const states = parseInt(response.headers.get('X-Fractal-States'), 10);

        return {
            resultType,
            cells: new Uint8Array(buffer),
            rows, cols, states,
            meta: { computeMs, totalMs }
        };
    }
    // 3. Векторные данные (Кечеджи)
    else if (resultType === 'PointPath') {
        const text = new TextDecoder('utf-8').decode(buffer);
        const points = JSON.parse(text);

        return {
            resultType,
            points,
            meta: { computeMs, totalMs }
        };
    }
    // 4. Спектральные данные (Хофштадтер)
    else if (resultType === 'Spectrum') {
        const width = parseInt(response.headers.get('X-Fractal-Width'), 10);
        const height = parseInt(response.headers.get('X-Fractal-Height'), 10);
        
        return {
            resultType,
            density: new Float32Array(buffer),
            width, height,
            meta: { computeMs, totalMs }
        };
    }

    throw new Error(`Unknown result type: ${resultType}`);
}