/* Лабораторная 4 — визуализация внешней сортировки. Ванильный JS, без сборки. */
"use strict";

const $ = (id) => document.getElementById(id);

const state = {
  tables: [],
  methods: [],
  preview: null,        // превью выбранной таблицы
  plan: null,           // ответ /external-sort/run (вся трассировка)
  rowsById: new Map(),
  cursor: -1,           // текущий шаг
  files: [],            // текущее состояние лент (снимки)
  playing: false,
  timer: null,
  delayMs: 300,
  ascending: true,
  method: "direct",
  fileName: null,
  hasHeader: true,
  logStep: -1,          // до какого шага нарисован журнал
  resultShown: null,    // имя файла результата, для которого показана таблица
};

/* ---------------- API ---------------- */

async function api(path, options) {
  const res = await fetch(path, options);
  let body = null;
  try { body = await res.json(); } catch { /* пустой ответ */ }
  if (!res.ok) {
    throw new Error((body && body.error) || `Ошибка запроса (${res.status})`);
  }
  return body;
}

const post = (path, data) => api(path, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(data),
});

/* ---------------- Инициализация ---------------- */

async function init() {
  bindControls();

  try {
    const [tables, methods] = await Promise.all([api("/api/tables"), api("/api/methods")]);
    state.tables = tables;
    state.methods = methods;
  } catch (err) {
    showError("Не удалось связаться с сервером: " + err.message);
    return;
  }

  const fileSelect = $("fileSelect");
  fileSelect.innerHTML = state.tables
    .map((t) => `<option value="${escapeHtml(t.fileName)}">${escapeHtml(t.fileName)}</option>`)
    .join("");
  if (state.tables.length === 0) {
    showError("В папке Data нет ни одного файла-таблицы (csv/tsv/txt). Положи файл и обнови страницу.");
    return;
  }
  fileSelect.addEventListener("change", onFileChanged);

  renderMethods();
  await onFileChanged();
}

function renderMethods() {
  const list = $("methodList");
  list.innerHTML = state.methods.map((m) => `
    <div class="methodCard ${m.id === state.method ? "active" : ""}" data-method="${m.id}">
      <div class="mTitle">
        <span>${escapeHtml(m.title)}</span>
        <span class="memBadge">в памяти: ${m.memorySlots}</span>
      </div>
      <div class="mInfo">${escapeHtml(m.shortInfo)}</div>
    </div>`).join("");
  list.querySelectorAll(".methodCard").forEach((card) => {
    card.addEventListener("click", () => {
      state.method = card.dataset.method;
      list.querySelectorAll(".methodCard").forEach((c) => c.classList.toggle("active", c === card));
      invalidatePlan();
    });
  });
}

async function onFileChanged() {
  const fileName = $("fileSelect").value;
  state.fileName = fileName;
  // У текстовых файлов обычно нет заголовка — умолчание по расширению.
  const isTxt = fileName.toLowerCase().endsWith(".txt");
  $("hasHeader").checked = !isTxt;
  state.hasHeader = !isTxt;
  await loadPreview();
}

async function loadPreview() {
  hideError();
  invalidatePlan();
  try {
    state.preview = await post("/api/tables/preview", {
      fileName: state.fileName,
      hasHeader: $("hasHeader").checked,
    });
    state.hasHeader = $("hasHeader").checked;
    // Подписи записей доступны и до построения плана сортировки.
    state.rowsById = new Map(state.preview.rows.map((r) => [r.id, r]));
  } catch (err) {
    showError(err.message);
    state.preview = null;
  }
  renderKeySelect();
  renderInitialTable();
  resetView();
}

function renderKeySelect() {
  const keySelect = $("keySelect");
  const cols = state.preview ? state.preview.columns : [];
  keySelect.innerHTML = cols.map((c) =>
    `<option value="${escapeHtml(c.name)}">${escapeHtml(c.name)} (${c.type === "number" ? "число" : "текст"})</option>`
  ).join("");
  keySelect.onchange = () => {
    invalidatePlan();
    highlightKeyColumn();
  };
  highlightKeyColumn();
}

function keyColumnIndex() {
  if (!state.preview) return -1;
  const name = $("keySelect").value;
  const col = state.preview.columns.find((c) => c.name === name);
  return col ? col.index : -1;
}

/* Подсветка ключевого столбца в исходной таблице. */
function highlightKeyColumn() {
  const keyIdx = keyColumnIndex();
  document.querySelectorAll("#initialTable th, #initialTable td").forEach((el) => {
    if (el.dataset.col !== undefined) {
      el.classList.toggle("keyCol", Number(el.dataset.col) === keyIdx);
    }
  });
}

function renderInitialTable() {
  const wrap = $("initialTable");
  if (!state.preview) { wrap.innerHTML = ""; $("tableCaption").textContent = ""; return; }
  const p = state.preview;
  $("tableCaption").textContent =
    `файл ${p.fileName} · записей: ${p.rows.length} · столбцов: ${p.columns.length}`;
  wrap.innerHTML = buildTableHtml(p);
  highlightKeyColumn();
}

function buildTableHtml(preview) {
  const head = `<tr><th>#</th>${preview.columns
    .map((c) => `<th data-col="${c.index}" title="${c.type === "number" ? "числовой столбец" : "текстовый столбец"}">${escapeHtml(c.name)}</th>`)
    .join("")}</tr>`;
  const body = preview.rows.map((r) =>
    `<tr><td class="rowId">${r.id + 1}</td>${r.cells
      .map((cell, i) => `<td data-col="${i}">${escapeHtml(cell)}</td>`)
      .join("")}</tr>`).join("");
  return `<table class="data"><thead>${head}</thead><tbody>${body}</tbody></table>`;
}

/* ---------------- Запуск сортировки ---------------- */

function planConfig() {
  return JSON.stringify({
    f: state.fileName,
    h: state.hasHeader,
    m: state.method,
    k: $("keySelect").value,
    a: state.ascending,
  });
}

function invalidatePlan() {
  state.plan = null;
  state.cursor = -1;
  state.logStep = -1;
  state.resultShown = null;
  stopPlayback();
  updateButtons();
  resetView();
}

async function ensurePlan() {
  if (state.plan) return true;
  hideError();
  $("descBox").textContent = "Считаем шаги сортировки…";
  try {
    const plan = await post("/api/external-sort/run", {
      fileName: state.fileName,
      hasHeader: state.hasHeader,
      method: state.method,
      keyColumn: $("keySelect").value,
      ascending: state.ascending,
    });
    plan.config = planConfig();
    state.plan = plan;
    state.rowsById = new Map(plan.rows.map((r) => [r.id, r]));
    $("counterRow").hidden = false;
    updateButtons();
    return true;
  } catch (err) {
    showError(err.message);
    $("descBox").textContent = "Настрой параметры слева и нажми «Запустить».";
    return false;
  }
}

async function onPlay() {
  if (state.playing) { stopPlayback(); return; }
  if (!await ensurePlan()) return;
  if (state.cursor >= state.plan.steps.length - 1) { resetView(); } // начнём заново
  state.playing = true;
  $("playBtn").textContent = "⏸ Пауза";
  scheduleTick();
}

function scheduleTick() {
  clearTimeout(state.timer);
  state.timer = setTimeout(tick, state.delayMs);
}

async function tick() {
  if (!state.playing) return;
  const last = state.plan.steps.length - 1;
  if (state.cursor >= last) { stopPlayback(); return; }
  applyStep(state.cursor + 1);
  if (state.cursor < last && state.playing) scheduleTick();
}

function stopPlayback() {
  state.playing = false;
  clearTimeout(state.timer);
  if ($("playBtn")) $("playBtn").textContent = "▶ Запустить";
}

async function onStep() {
  stopPlayback();
  if (!await ensurePlan()) return;
  if (state.cursor < state.plan.steps.length - 1) applyStep(state.cursor + 1);
}

async function onBack() {
  stopPlayback();
  if (!state.plan || state.cursor < 0) return;
  applyStep(state.cursor - 1);
}

function onReset() {
  stopPlayback();
  if (state.plan) {
    state.cursor = -1;
    state.logStep = -1;
    state.resultShown = null;
  }
  resetView();
}

function updateButtons() {
  const has = !!state.plan;
  $("backBtn").disabled = !has || state.cursor < 0;
  $("stepBtn").disabled = !has || state.cursor >= state.plan.steps.length - 1;
  $("resetBtn").disabled = !has;
}

/* ---------------- Отрисовка состояния ---------------- */

function resetView() {
  stopPlayback();
  if (state.plan) {
    state.cursor = -1;
    state.logStep = -1;
    state.resultShown = null;
    $("resultPanel").hidden = true;
  }
  updateButtons();
  $("phaseBadge").hidden = true;
  $("memoryBox").hidden = true;
  $("counterRow").hidden = true;
  $("doneBox").hidden = true;
  $("progressBar").style.width = "0";
  $("stepCounter").textContent = "шаг 0 / 0";
  $("descBox").classList.remove("done");
  if (state.plan) {
    $("descBox").textContent = "Готово к запуску: нажми «Запустить» или «Шаг».";
    const n = state.plan.stats.inputRows;
    state.files = [{ name: "A", role: "source", ids: [...Array(n).keys()], cursor: 0, runs: null }];
    renderTapes();
  } else {
    $("descBox").textContent =
      "Настрой параметры слева и нажми «Запустить» — покажу сортировку по шагам: " +
      "что читаем, что сравниваем и куда переписываем.";
    const n = state.preview ? state.preview.rows.length : 0;
    state.files = [{ name: "A", role: "source", ids: [...Array(n).keys()], cursor: 0, runs: null }];
    renderTapes();
  }
  $("logList").innerHTML = '<div class="logEmpty">Журнал появится после запуска сортировки.</div>';
}

/* Состояние лент на шаге i: берём последний снимок ≤ i (сравнения файлы не меняют). */
function filesAt(i) {
  if (i < 0) return null;
  for (let j = i; j >= 0; j--) {
    if (state.plan.steps[j].snap) return state.plan.steps[j].snap;
  }
  return null;
}

function applyStep(i) {
  if (i < -1 || !state.plan) return;
  state.cursor = i;
  const step = i < 0 ? null : state.plan.steps[i];

  if (!step) { resetView(); return; }

  state.files = filesAt(i) || state.files;
  renderTapes();
  renderMemory(step);
  renderDesc(step);
  renderCounters(step);
  syncLog(i);
  renderProgress(step);
  updateButtons();

  if (step.kind === "Done") {
    showDone(step);
  } else if (state.plan && state.cursor < state.plan.steps.length - 1) {
    $("doneBox").hidden = true;
  }
}

function labelOf(id) {
  const row = state.rowsById.get(id);
  if (!row) return `№${id}`;
  const lc = state.plan ? state.plan.labelColumn : guessLabelColumn();
  const text = row.cells[lc] !== undefined ? row.cells[lc] : row.cells[0];
  return text.length > 22 ? text.slice(0, 21) + "…" : text;
}

function tooltipOf(id) {
  const row = state.rowsById.get(id);
  if (!row) return "";
  if (state.plan) {
    const key = row.cells[state.plan.keyColumn];
    return `${labelOf(id)} · ключ «${state.plan.keyColumnName}»: ${key}`;
  }
  return state.preview.columns.map((c) => `${c.name}: ${row.cells[c.index]}`).join(" · ");
}

/* До построения плана подбираем колонку-подпись так же, как сервер. */
function guessLabelColumn() {
  if (!state.preview) return 0;
  const cols = state.preview.columns;
  const keyIdx = keyColumnIndex();
  const re = /назван|стран|имя|слов|name|word|товар|город/i;
  if (cols[keyIdx] && re.test(cols[keyIdx].name)) return keyIdx;
  for (const c of cols) {
    if (c.index !== keyIdx && re.test(c.name)) return c.index;
  }
  return 0;
}

function renderTapes() {
  const area = $("tapeArea");
  const total = state.plan ? state.plan.stats.inputRows : (state.preview ? state.preview.rows.length : 0);
  const phase = state.plan && state.cursor >= 0 ? state.plan.steps[state.cursor].phase : "";
  const memIds = new Set(state.plan && state.cursor >= 0 ? state.plan.steps[state.cursor].memoryIds : []);

  area.innerHTML = state.files.map((tape) => {
    const roleText = tape.role === "source" ? "файл-источник" : "вспомогательная лента";
    const remaining = tape.role === "source" ? total - tape.ids.length : 0;
    const showGhost = remaining > 0 && (phase === "Слияние" || phase === "Распределение");

    const groups = tape.runs && tape.runs.length > 0 && tape.runs.some((g) => g.length > 0)
      ? tape.runs
      : null;

    let chipHtml = "";
    let flat = 0; // сколько чипов нарисовано "плоско"
    if (groups) {
      chipHtml = groups.map((group, gi) => {
        const chips = group.map((id) => chipHtmlFor(tape, id, flat++, memIds)).join("");
        return `<span class="runGroup"><span class="runNo">серия ${gi + 1}</span>${chips}</span>`;
      }).join("");
    } else {
      chipHtml = tape.ids.map((id) => chipHtmlFor(tape, id, flat++, memIds)).join("");
    }
    if (showGhost) {
      chipHtml += `<span class="ghost" title="Ещё не переписано">… осталось ${remaining}</span>`;
    }

    return `
      <div class="tape ${tape.role === "source" ? "isSource" : ""}">
        <div class="tapeHead">
          <span class="tapeName">${tape.name}</span>
          <span class="tapeRole">${roleText}</span>
          <span class="tapeCount">записей: ${tape.ids.length}</span>
        </div>
        <div class="chips">${chipHtml || '<span class="ghost">пусто</span>'}</div>
      </div>`;
  }).join("");
}

function chipHtmlFor(tape, id, index, memIds) {
  const cls = ["chip"];
  if (index < tape.cursor) cls.push("read");
  if (index === tape.cursor) cls.push("cursorNext");
  if (memIds.has(id)) cls.push("mem");
  return `<span class="${cls.join(" ")}" title="${escapeHtml(tooltipOf(id))}">${escapeHtml(labelOf(id))}</span>`;
}

function renderMemory(step) {
  const box = $("memoryBox");
  box.hidden = false;
  const slots = state.plan.memorySlots;
  const cells = [];
  for (let i = 0; i < slots; i++) {
    const id = step.memoryIds[i];
    cells.push(id === undefined
      ? `<div class="slot"><div class="slotEmpty">пусто</div></div>`
      : `<div class="slot filled"><div class="slotLabel" title="${escapeHtml(tooltipOf(id))}">${escapeHtml(labelOf(id))}</div>
           <div class="slotKey">ключ: ${escapeHtml(keyTextOf(id))}</div></div>`);
  }
  box.innerHTML = `
    <div class="memoryTitle"><b>Память программы</b> — ячеек: ${slots}. ${escapeHtml(step.memoryNote)}</div>
    <div class="memorySlots">${cells.join("")}</div>`;
}

function keyTextOf(id) {
  const row = state.rowsById.get(id);
  const keyIdx = state.plan ? state.plan.keyColumn : keyColumnIndex();
  return row && row.cells[keyIdx] !== undefined ? String(row.cells[keyIdx]) : "";
}

function renderDesc(step) {
  const box = $("descBox");
  box.classList.toggle("done", step.kind === "Done");
  const badge = $("phaseBadge");
  if (step.passTitle) {
    badge.hidden = false;
    badge.textContent = `${step.phase} · ${step.passTitle}`;
  } else if (step.kind === "PhaseStart" || step.kind === "Done") {
    badge.hidden = false;
    badge.textContent = step.phase;
  } else {
    badge.hidden = false;
    badge.textContent = step.phase;
  }
  box.textContent = step.desc;
}

function renderCounters(step) {
  const row = $("counterRow");
  row.hidden = false;
  row.innerHTML = `
    <div class="stat" id="statC"><div class="num">${step.comparisons}</div><div class="lbl">⚖ сравнений</div></div>
    <div class="stat" id="statM"><div class="num">${step.moves}</div><div class="lbl">↔ перестановок</div></div>
    <div class="stat" id="statP"><div class="num">${step.passes}</div><div class="lbl">↻ проходов</div></div>`;
  pulse("statC", step.index);
  pulse("statM", step.index);
}

let lastPulse = { statC: -1, statM: -1, statP: -1 };
function pulse(id, stepIndex) {
  if (lastPulse[id] !== stepIndex) {
    lastPulse[id] = stepIndex;
    const el = $(id);
    el.classList.remove("pulse");
    void el.offsetWidth; // перезапуск анимации
    el.classList.add("pulse");
  }
}

function syncLog(toStep) {
  const list = $("logList");
  const steps = state.plan.steps;
  if (toStep < state.logStep) {
    // Пошли назад — перерисовываем журнал с нуля.
    state.logStep = -1;
    list.innerHTML = "";
  }
  const items = [];
  for (let i = Math.max(state.logStep + 1, 0); i <= toStep; i++) {
    if (steps[i].logLine) items.push(steps[i]);
  }
  if (items.length > 0 && state.logStep < 0 && list.querySelector(".logEmpty")) {
    list.innerHTML = "";
  }
  const fragment = document.createDocumentFragment();
  for (const st of items) {
    const div = document.createElement("div");
    div.className = `logLine ${st.logKind}`;
    div.textContent = st.logLine;
    fragment.appendChild(div);
  }
  list.appendChild(fragment);
  // Ограничиваем высоту журнала: старые записи убираем.
  while (list.children.length > 800) list.removeChild(list.firstChild);
  state.logStep = toStep;
  if ($("followLog").checked) list.scrollTop = list.scrollHeight;
}

function renderProgress(step) {
  const total = state.plan.steps.length;
  $("progressBar").style.width = `${((step.index + 1) / total) * 100}%`;
  $("stepCounter").textContent = `шаг ${step.index + 1} / ${total}`;
}

async function showDone(step) {
  const box = $("doneBox");
  box.hidden = false;
  const name = state.plan.resultFileName;
  box.innerHTML = `
    <span>🎉 Готово! Результат записан в файл</span>
    <span class="fileName">${escapeHtml(name || "")}</span>
    <a class="buttonLink" href="/api/results/${encodeURIComponent(name)}" download>⬇ Скачать</a>`;
  await showResultPanel(name);
}

async function showResultPanel(fileName) {
  if (!fileName) return;
  if (state.resultShown === fileName) { $("resultPanel").hidden = false; return; }
  try {
    const preview = await api(`/api/results/${encodeURIComponent(fileName)}/preview?hasHeader=${state.hasHeader}`);
    $("resultCaption").textContent = `${fileName} · записей: ${preview.rows.length}`;
    $("resultTable").innerHTML = buildTableHtml(preview);
    $("resultDownload").href = `/api/results/${encodeURIComponent(fileName)}`;
    $("resultPanel").hidden = false;
    state.resultShown = fileName;
  } catch (err) {
    /* файл результата не критичен для визуализации */
  }
}

/* ---------------- Служебное ---------------- */

function escapeHtml(text) {
  return String(text)
    .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
}

function showError(message) {
  const box = $("errorBox");
  box.textContent = "⚠ " + message;
  box.hidden = false;
}

function hideError() {
  $("errorBox").hidden = true;
}

function bindControls() {
  $("playBtn").addEventListener("click", onPlay);
  $("stepBtn").addEventListener("click", onStep);
  $("backBtn").addEventListener("click", onBack);
  $("resetBtn").addEventListener("click", onReset);

  $("ascBtn").addEventListener("click", () => setDirection(true));
  $("descBtn").addEventListener("click", () => setDirection(false));

  $("delayRange").addEventListener("input", (e) => {
    state.delayMs = Number(e.target.value);
    $("delayVal").textContent = String(state.delayMs);
    if (state.playing) scheduleTick(); // пересобираем таймер с новой задержкой
  });

  $("hasHeader").addEventListener("change", () => {
    state.hasHeader = $("hasHeader").checked;
    loadPreview();
  });

  // Вкладки заданий.
  $("tabs").addEventListener("click", (e) => {
    const btn = e.target.closest(".tab");
    if (!btn) return;
    document.querySelectorAll(".tab").forEach((t) => t.classList.toggle("active", t === btn));
    document.querySelectorAll(".tabPage").forEach((p) => p.classList.toggle("active", p.id === `tab-${btn.dataset.tab}`));
  });
}

function setDirection(ascending) {
  state.ascending = ascending;
  $("ascBtn").classList.toggle("active", ascending);
  $("descBtn").classList.toggle("active", !ascending);
  invalidatePlan();
}

init();
