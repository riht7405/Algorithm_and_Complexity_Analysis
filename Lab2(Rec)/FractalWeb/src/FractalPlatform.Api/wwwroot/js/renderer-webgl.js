/**
 * WebGL2-рендерер для IterationMap.
 *
 * Архитектурные решения:
 *  - Итерации → текстура R16UI (usampler2D). Точные значения, без нормализации.
 *  - Палитра → текстура RGBA8 256×1 (sampler2D).
 *  - Fullscreen quad, фрагментный шейдер делает lookup.
 *  - Смена палитры → пересоздание ТОЛЬКО текстуры палитры.
 *  - Режим раскраски — uniform, живёт на клиенте, сервер о нём не знает.
 */

const VERT = `#version 300 es
in vec2 a_pos;
out vec2 v_uv;
void main() {
    v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
    gl_Position = vec4(a_pos, 0.0, 1.0);
}`;

const FRAG = `#version 300 es
precision highp float;
precision highp usampler2D;

uniform usampler2D u_iterations;
uniform sampler2D  u_palette;
uniform int        u_maxIter;
uniform int        u_colorMode;   // 0=linear, 1=cyclic, 2=log, 3=sqrt
uniform float      u_frequency;   // для cyclic
uniform vec3       u_inSetColor;  // цвет пикселей «внутри» множества

in  vec2 v_uv;
out vec4 outColor;

void main() {
    uint uIter = texture(u_iterations, v_uv).r;

    // Пиксели, не «убежавшие» за maxIter, — отдельный цвет
    if (uIter >= uint(u_maxIter)) {
        outColor = vec4(u_inSetColor, 1.0);
        return;
    }

    float fIter = float(uIter);
    float fMax  = float(u_maxIter);
    float t;

    if (u_colorMode == 0) {
        t = fIter / fMax;                          // linear
    } else if (u_colorMode == 1) {
        t = fract(fIter / u_frequency);            // cyclic
    } else if (u_colorMode == 2) {
        t = log(fIter + 1.0) / log(fMax + 1.0);    // log
    } else {
        t = sqrt(fIter / fMax);                    // sqrt
    }

    t = clamp(t, 0.0, 1.0);
    outColor = texture(u_palette, vec2(t, 0.5));
}`;

export class WebGLRenderer {
    constructor(canvas) {
        const gl = canvas.getContext('webgl2', {
            antialias: false,
            preserveDrawingBuffer: false,
            powerPreference: 'high-performance'
        });
        if (!gl) throw new Error('WebGL2 not supported');

        this.canvas = canvas;
        this.gl = gl;
        this.width = 0;
        this.height = 0;
        this.maxIter = 1;

        // Стиль раскраски — по умолчанию циклический
        this.colorMode = 1;
        this.frequency = 32.0;
        this.inSetColor = [0.0, 0.0, 0.0];

        this.program = this._createProgram(VERT, FRAG);
        this.iterationTex = gl.createTexture();
        this.paletteTex = gl.createTexture();
        this.vao = this._createQuad();
    }

    /**
     * Загружает новые данные фрактала.
     * @param {Uint16Array} iterations
     * @param {number} width
     * @param {number} height
     * @param {number} maxIter
     */
    setData(iterations, width, height, maxIter) {
        const gl = this.gl;

         console.log('setData вызван:', { width, height, maxIter, firstIter: iterations[0] });

        if (this.canvas.width !== width || this.canvas.height !== height) {
            this.canvas.width = width;
            this.canvas.height = height;
        }
        this.width = width;
        this.height = height;
        this.maxIter = Math.max(maxIter, 1);

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, this.iterationTex);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
        gl.texImage2D(
            gl.TEXTURE_2D, 0, gl.R16UI,
            width, height, 0,
            gl.RED_INTEGER, gl.UNSIGNED_SHORT,
            iterations
        );

        this._render();
    }

    /**
     * Загружает палитру (RGBA8, 256 пикселей). Текстура итераций не трогается.
     * @param {Uint8Array} rgba — 256×4 байт
     */
    setPalette(rgba) {
        const gl = this.gl;
        gl.activeTexture(gl.TEXTURE1);
        gl.bindTexture(gl.TEXTURE_2D, this.paletteTex);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texImage2D(
            gl.TEXTURE_2D, 0, gl.RGBA,
            256, 1, 0,
            gl.RGBA, gl.UNSIGNED_BYTE,
            rgba
        );
        this._render();
    }

    /**
     * Обновляет параметры раскраски. Не требует новых данных от сервера.
     * @param {{mode?: number, frequency?: number, inSetColor?: number[]}} opts
     */
    setColoring({ mode, frequency, inSetColor } = {}) {
        if (mode !== undefined)        this.colorMode = mode;
        if (frequency !== undefined)   this.frequency = frequency;
        if (inSetColor !== undefined)  this.inSetColor = inSetColor;
        this._render();
    }

    _render() {
        const gl = this.gl;
        if (this.width === 0) return;

        gl.viewport(0, 0, this.width, this.height);
        gl.useProgram(this.program);

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, this.iterationTex);
        gl.uniform1i(gl.getUniformLocation(this.program, 'u_iterations'), 0);

        gl.activeTexture(gl.TEXTURE1);
        gl.bindTexture(gl.TEXTURE_2D, this.paletteTex);
        gl.uniform1i(gl.getUniformLocation(this.program, 'u_palette'), 1);

        gl.uniform1i(gl.getUniformLocation(this.program, 'u_maxIter'), this.maxIter);
        gl.uniform1i(gl.getUniformLocation(this.program, 'u_colorMode'), this.colorMode);
        gl.uniform1f(gl.getUniformLocation(this.program, 'u_frequency'), this.frequency);
        gl.uniform3fv(gl.getUniformLocation(this.program, 'u_inSetColor'), this.inSetColor);

        gl.bindVertexArray(this.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

                const err = gl.getError();
        if (err !== gl.NO_ERROR) {
            console.error('WebGL Error:', err);
        }
    }

    _createProgram(vsSrc, fsSrc) {
        const gl = this.gl;
        const vs = this._compile(gl.VERTEX_SHADER, vsSrc);
        const fs = this._compile(gl.FRAGMENT_SHADER, fsSrc);
        const program = gl.createProgram();
        gl.attachShader(program, vs);
        gl.attachShader(program, fs);
        gl.linkProgram(program);
        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
            throw new Error('Program link failed: ' + gl.getProgramInfoLog(program));
        }
        return program;
    }

    _compile(type, src) {
        const gl = this.gl;
        const shader = gl.createShader(type);
        gl.shaderSource(shader, src);
        gl.compileShader(shader);
        if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
            throw new Error('Shader compile failed: ' + gl.getShaderInfoLog(shader));
        }
        return shader;
    }

    _createQuad() {
        const gl = this.gl;
        const vao = gl.createVertexArray();
        gl.bindVertexArray(vao);

        const buffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([
            -1, -1,  1, -1,  -1,  1,  1,  1
        ]), gl.STATIC_DRAW);

        const posLoc = gl.getAttribLocation(this.program, 'a_pos');
        gl.enableVertexAttribArray(posLoc);
        gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);

        return vao;
    }
}