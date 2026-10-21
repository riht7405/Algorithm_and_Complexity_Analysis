const PALETTE_STOPS = {
    inferno: [
        [0.000, [0, 0, 4]],       [0.250, [87, 16, 110]],
        [0.500, [188, 55, 84]],   [0.750, [249, 142, 9]],
        [1.000, [252, 255, 164]]
    ],
    viridis: [
        [0.000, [68, 1, 84]],     [0.250, [59, 82, 139]],
        [0.500, [33, 145, 140]],  [0.750, [94, 201, 98]],
        [1.000, [253, 231, 37]]
    ],
    plasma: [
        [0.000, [13, 8, 135]],    [0.250, [126, 3, 168]],
        [0.500, [204, 71, 120]],  [0.750, [248, 149, 64]],
        [1.000, [240, 249, 33]]
    ],
    magma: [
        [0.000, [0, 0, 4]],       [0.250, [81, 18, 124]],
        [0.500, [183, 55, 121]],  [0.750, [252, 137, 97]],
        [1.000, [252, 253, 191]]
    ],
    turbo: [
        [0.000, [48, 18, 59]],    [0.200, [70, 134, 251]],
        [0.400, [27, 229, 181]],  [0.600, [169, 252, 74]],
        [0.800, [255, 153, 32]],  [1.000, [122, 4, 3]]
    ],
    grayscale: [
        [0.000, [0, 0, 0]],       [1.000, [255, 255, 255]]
    ]
};

export const PALETTE_NAMES = Object.keys(PALETTE_STOPS);

export function getPalette(name, size = 256) {
    const stops = PALETTE_STOPS[name] || PALETTE_STOPS.inferno;
    const data = new Uint8Array(size * 4);
    for (let i = 0; i < size; i++) {
        const t = i / (size - 1);
        const [r, g, b] = interpolate(stops, t);
        data[i * 4 + 0] = r;
        data[i * 4 + 1] = g;
        data[i * 4 + 2] = b;
        data[i * 4 + 3] = 255;
    }
    return data;
}

function interpolate(stops, t) {
    for (let i = 0; i < stops.length - 1; i++) {
        const [t0, c0] = stops[i];
        const [t1, c1] = stops[i + 1];
        if (t >= t0 && t <= t1) {
            const k = (t - t0) / (t1 - t0);
            return [
                Math.round(c0[0] + (c1[0] - c0[0]) * k),
                Math.round(c0[1] + (c1[1] - c0[1]) * k),
                Math.round(c0[2] + (c1[2] - c0[2]) * k)
            ];
        }
    }
    return stops[stops.length - 1][1];
}