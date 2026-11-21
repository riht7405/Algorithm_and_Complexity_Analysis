/* Лабораторная 4 — визуализация внешней сортировки. Ванильный JS, без сборки.
   У каждого сочетания настроек (таблица, метод, ключ, порядок) — своя независимая
   сессия: переключение туда-обратно сохраняет прогресс, ничего не сбрасывается. */
"use strict";

const $ = (id) => document.getElementById(id);

/* Подзаголовок шапки и подпись примечания для каждой вкладки. */
const TAB_SUBTITLES = {
  task1: "Внутренние сортировки",
  task2: "Алгоритмы внешней сортировки",
  task3: "Подсчёт слов и сравнение алгоритмов",
};

const state = {
  tables: [],
  methods: [],
  preview: null,        // превью выбранной таблицы
  sessions: new Map(),  // ключ настроек -> {plan, cursor, resultShown}
  sessionKey: "",
  plan: null,           // план текущей сессии
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
    .map((t) => `<option value="${escapeHtml(t.fileName)}">${escapeHtml(prettyFileName(t.fileName))} (${escapeHtml(t.fileName)})</option>`)
    .join("");
  if (state.tables.length === 0) {
    showError("В папке Data нет ни одного файла-таблицы (csv/tsv/txt). Положи файл и обнови страницу.");
    return;
  }
  fileSelect.addEventListener("change", onFileChanged);

  renderMethods();
  await onFileChanged();
  renderHistory();
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
      const id = card.dataset.method;
      if (id === state.method) return;
      stashSession();
      state.method = id;
      list.querySelectorAll(".methodCard").forEach((c) => c.classList.toggle("active", c === card));
      state.sessionKey = planConfig();
      restoreSession();
    });
  });
}

async function onFileChanged() {
  const fileName = $("fileSelect").value;
  stashSession(); // текущая сессия сохраняется под своим ключом
  state.fileName = fileName;
  // У текстовых файлов обычно нет заголовка — умолчание по расширению.
  const isTxt = fileName.toLowerCase().endsWith(".txt");
  $("hasHeader").checked = !isTxt;
  state.hasHeader = !isTxt;
  await loadPreview(fileName, state.hasHeader);
  if (state.fileName !== fileName) return; // пока грузились, выбрали другой файл
  state.sessionKey = planConfig();
  restoreSession();
}

async function loadPreview(fileName, hasHeader) {
  hideError();
  try {
    const preview = await post("/api/tables/preview", { fileName, hasHeader });
    if (state.fileName !== fileName || state.hasHeader !== hasHeader) return; // устарело
    state.preview = preview;
    // Подписи записей доступны и до построения плана сортировки.
    state.rowsById = new Map(preview.rows.map((r) => [r.id, r]));
  } catch (err) {
    showError(err.message);
    state.preview = null;
  }
  renderKeySelect();
  renderInitialTable();
}

function renderKeySelect() {
  const keySelect = $("keySelect");
  const cols = state.preview ? state.preview.columns : [];
  keySelect.innerHTML = cols.map((c) =>
    `<option value="${escapeHtml(c.name)}">${escapeHtml(c.name)} (${c.type === "number" ? "число" : "текст"})</option>`
  ).join("");
  keySelect.onchange = () => {
    stashSession();
    state.sessionKey = planConfig();
    highlightKeyColumn();
    restoreSession();
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

/* ---------------- Сессии настроек ---------------- */
/* Ключ сессии = сочетание всех настроек. Переключение настройки сохраняет
   текущую сессию и показывает сессию нового сочетания (или пустую сцену). */

function planConfig() {
  return JSON.stringify({
    f: state.fileName,
    h: state.hasHeader,
    m: state.method,
    k: $("keySelect").value,
    a: state.ascending,
  });
}

/* Записать текущую сессию в кэш (под её собственным ключом). */
function syncSession() {
  if (!state.plan) return;
  if (!state.sessions.has(state.sessionKey) && state.sessions.size >= 15) {
    for (const k of state.sessions.keys()) {
      if (k !== state.sessionKey) { state.sessions.delete(k); break; }
    }
  }
  state.sessions.set(state.sessionKey, {
    plan: state.plan,
    cursor: state.cursor,
    resultShown: state.resultShown,
  });
}

/* Перед сменой настройки: сохранить текущую сессию и остановить проигрывание. */
function stashSession() {
  stopPlayback();
  syncSession();
}

/* Показать сессию для текущего сочетания настроек (или пустую сцену). */
function restoreSession() {
  stopPlayback();
  const s = state.sessions.get(state.sessionKey);
  state.plan = s ? s.plan : null;
  state.cursor = s ? s.cursor : -1;
  state.resultShown = s ? s.resultShown : null;
  state.logStep = -1;
  $("logList").innerHTML = ""; // журнал прошлой сессии не должен досыпаться в новую
  if (state.plan) {
    state.rowsById = new Map(state.plan.rows.map((r) => [r.id, r]));
  } else if (state.preview) {
    state.rowsById = new Map(state.preview.rows.map((r) => [r.id, r]));
  }
  renderSessionView({ animateLog: false, record: false });
  updateButtons();
  updatePlayBtn();
}

/* ---------------- Запуск сортировки ---------------- */

async function ensurePlan() {
  if (state.plan) return true;
  const keyAtStart = state.sessionKey;
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
    if (state.sessionKey !== keyAtStart) return false; // настройки сменили, пока считали
    state.plan = plan;
    state.cursor = -1;
    state.logStep = -1;
    state.resultShown = null;
    state.rowsById = new Map(plan.rows.map((r) => [r.id, r]));
    syncSession();
    return true;
  } catch (err) {
    if (state.sessionKey === keyAtStart) {
      showError(err.message);
      $("descBox").textContent = "Настрой параметры и нажми «Запустить».";
    }
    return false;
  }
}

async function onPlay() {
  if (state.playing) { stopPlayback(); return; }
  if (!await ensurePlan()) return;
  const last = state.plan.steps.length - 1;
  if (state.cursor >= last) { // «Заново»: начинаем этот прогон с начала
    state.cursor = -1;
    state.logStep = -1;
    state.resultShown = null;
    syncSession();
    renderSessionView({ animateLog: false, record: false });
  }
  state.playing = true;
  updatePlayBtn();
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
  if (state.playing && state.cursor >= last) stopPlayback();
  else if (state.playing) scheduleTick();
}

function stopPlayback() {
  state.playing = false;
  clearTimeout(state.timer);
  updatePlayBtn();
}

/* Кнопка запуска: Пауза во время проигрывания, «Заново» у завершённой сортировки. */
function updatePlayBtn() {
  const btn = $("playBtn");
  if (state.playing) btn.textContent = "⏸ Пауза";
  else if (state.plan && state.plan.steps.length > 0 && state.cursor >= state.plan.steps.length - 1) {
    btn.textContent = "↻ Заново";
  } else {
    btn.textContent = "▶ Запустить";
  }
}

async function onStep() {
  stopPlayback();
  if (!await ensurePlan()) return;
  if (state.cursor < state.plan.steps.length - 1) applyStep(state.cursor + 1);
  else updatePlayBtn();
}

/* «В конец»: мгновенно показать последний шаг сортировки. */
async function onJumpEnd() {
  stopPlayback();
  if (!await ensurePlan()) return;
  applyStep(state.plan.steps.length - 1);
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
    syncSession();
  }
  renderSessionView({ animateLog: false, record: false });
  updateButtons();
  updatePlayBtn();
}

function updateButtons() {
  const has = !!state.plan;
  $("backBtn").disabled = !has || state.cursor < 0;
  $("stepBtn").disabled = false; // «Шаг» доступен сразу: сам построит план и сделает шаг
  $("resetBtn").disabled = !has;
}

/* ---------------- Отрисовка состояния ---------------- */
/* Единая точка отрисовки: пустая сцена / готова к запуску / произвольный шаг / готово. */

function renderSessionView(opts = {}) {
  const { animateLog = true, record = true } = opts;
  const badge = $("phaseBadge");

  if (!state.plan || state.cursor < 0) {
    const ready = !!state.plan; // план построен, но шаги ещё не делали
    state.logStep = -1;
    const n = ready ? state.plan.stats.inputRows : (state.preview ? state.preview.rows.length : 0);
    state.files = [{ name: "A", role: "source", ids: [...Array(n).keys()], cursor: 0, runs: null }];
    renderTapes();
    badge.hidden = true;
    $("memoryBox").hidden = true;
    $("counterRow").hidden = true;
    $("resultPanel").hidden = true;
    $("progressBar").style.width = "0";
    $("stepCounter").textContent = "шаг 0 / 0";
    $("descBox").classList.remove("done");
    $("descBox").textContent = ready
      ? "Готово к запуску: нажми «Запустить» или «Шаг»."
      : "Нажми «Запустить» или «Шаг» — покажу сортировку по шагам: что читаем, что сравниваем и куда переписываем.";
    $("logList").innerHTML = '<div class="logEmpty">Журнал появится после запуска сортировки.</div>';
    return;
  }

  const step = state.plan.steps[state.cursor];
  state.files = filesAt(state.cursor) || state.files;
  renderTapes();
  renderMemory(step);
  renderDesc(step);
  renderCounters(step);
  syncLog(state.cursor, animateLog);
  renderProgress(step);

  if (step.kind === "Done") {
    showDone(step, record);
  } else {
    $("resultPanel").hidden = true;
  }
}

/* Состояние лент на шаге i: берём последний снимок ≤ i (сравнения файлы не меняют). */
function filesAt(i) {
  if (i < 0) return null;
  for (let j = i; j >= 0; j--) {
    if (state.plan.steps[j].snap) return state.plan.steps[j].snap;
  }
  return null;
}

function applyStep(i, opts = {}) {
  if (i < -1 || !state.plan) return;
  state.cursor = i;
  syncSession();
  renderSessionView(opts);
  updateButtons();
  updatePlayBtn();
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
        return `<span class="runGroup"><span class="runNo">Серия ${gi + 1}</span>${chips}</span>`;
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
  badge.hidden = false;
  badge.textContent = step.passTitle ? `${step.phase} · ${step.passTitle}` : step.phase;
  box.textContent = step.kind === "Done"
    ? "Сортировка завершена. Результат — на панели «Результат» ниже."
    : step.desc;
}

function renderCounters(step) {
  const row = $("counterRow");
  row.hidden = false;
  if (!row.dataset.built) {
    row.innerHTML = `
      <div class="stat" id="statC"><div class="num">0</div><div class="lbl">⚖ сравнений</div></div>
      <div class="stat" id="statM"><div class="num">0</div><div class="lbl">↔ перестановок</div></div>
      <div class="stat" id="statP"><div class="num">0</div><div class="lbl">↻ проходов</div></div>`;
    row.dataset.built = "1";
    // Подсветку снимаем после анимации — при переключении вкладок числа не мигают.
    row.querySelectorAll(".stat").forEach((s) =>
      s.addEventListener("animationend", () => s.classList.remove("pulse")));
  }
  setStat("statC", step.comparisons);
  setStat("statM", step.moves);
  setStat("statP", step.passes);
}

/* Число остаётся на своём месте; подсветка цветом — только когда значение изменилось. */
function setStat(id, value) {
  const stat = $(id);
  const num = stat.querySelector(".num");
  const text = String(value);
  if (num.textContent === text) return;
  num.textContent = text;
  stat.classList.remove("pulse");
  void stat.offsetWidth; // перезапуск анимации
  stat.classList.add("pulse");
}

function syncLog(toStep, animate = true) {
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
    div.className = `logLine ${st.logKind}` + (animate ? " fresh" : "");
    div.textContent = st.logLine;
    if (animate) div.addEventListener("animationend", () => div.classList.remove("fresh"), { once: true });
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

async function showDone(step, record) {
  updatePlayBtn();
  await showResultPanel(state.plan.resultFileName);
  if (record) addHistory(step);
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
    syncSession();
  } catch (err) {
    /* файл результата не критичен для визуализации */
  }
}

/* ---------------- История сортировок ---------------- */

const HISTORY_KEY = "lab4.sortHistory";
let historyDesc = true; // по умолчанию — сначала новые
let historySel = new Set(); // выбранные записи (id) для массового удаления

function loadHistory() {
  try { return JSON.parse(localStorage.getItem(HISTORY_KEY)) || []; } catch { return []; }
}

function saveHistory(list) {
  try { localStorage.setItem(HISTORY_KEY, JSON.stringify(list)); } catch { /* нет localStorage */ }
}

/* Одинаковая сортировка (таблица+метод+ключ+порядок) не добавляется второй раз —
   у существующей записи обновляются счётчики и время завершения. */
function addHistory(step) {
  const methodTitle = (state.methods.find((m) => m.id === state.method) || {}).title || state.method;
  const keyName = $("keySelect").value;
  const id = [state.fileName, state.method, keyName, state.ascending ? "asc" : "desc"].join("|");
  const list = loadHistory();
  let rec = list.find((h) => h.id === id);
  if (!rec) {
    rec = { id, file: state.fileName, method: methodTitle, key: keyName, asc: state.ascending };
    list.push(rec);
  }
  rec.method = methodTitle;
  rec.passes = step.passes;
  rec.comparisons = step.comparisons;
  rec.moves = step.moves;
  rec.finishedAt = new Date().toISOString();
  saveHistory(list);
  renderHistory();
}

function renderHistory() {
  const panel = $("historyPanel");
  const list = loadHistory();
  // Выбор не должен ссылаться на уже удалённые записи.
  const ids = new Set(list.map((h) => h.id));
  for (const id of [...historySel]) {
    if (!ids.has(id)) historySel.delete(id);
  }
  if (list.length === 0) { panel.hidden = true; return; }
  list.sort((a, b) => (a.finishedAt < b.finishedAt ? -1 : a.finishedAt > b.finishedAt ? 1 : 0));
  if (historyDesc) list.reverse();
  const fmt = (iso) => {
    const d = new Date(iso);
    return isNaN(d) ? "" : d.toLocaleString("ru-RU");
  };
  const allChecked = list.every((h) => historySel.has(h.id));
  const head = `<tr>
    <th class="histCheck"><input type="checkbox" id="histCheckAll" title="Выбрать или снять все записи" ${allChecked ? "checked" : ""}></th>
    <th>Таблица</th><th>Метод</th><th>Ключевой атрибут</th><th>Порядок</th>
    <th>Проходов</th><th>Сравнений</th><th>Перестановок</th>
    <th class="thSort" data-sort="time" title="Нажми, чтобы сменить порядок сортировки">Время ${historyDesc ? "▼" : "▲"}</th>
    <th></th>
  </tr>`;
  const body = list.map((h) => `<tr>
    <td class="histCheck"><input type="checkbox" class="histSel" data-id="${escapeHtml(h.id)}" ${historySel.has(h.id) ? "checked" : ""}></td>
    <td>${escapeHtml(h.file)}</td>
    <td>${escapeHtml(h.method)}</td>
    <td>${escapeHtml(h.key)}</td>
    <td>${h.asc ? "▲ по возрастанию" : "▼ по убыванию"}</td>
    <td>${h.passes ?? ""}</td>
    <td>${h.comparisons ?? ""}</td>
    <td>${h.moves ?? ""}</td>
    <td>${fmt(h.finishedAt)}</td>
    <td class="histDel"><button class="mini danger" data-del="${escapeHtml(h.id)}" title="Удалить запись">✕</button></td>
  </tr>`).join("");
  $("historyTable").innerHTML = `<table class="data"><thead>${head}</thead><tbody>${body}</tbody></table>`;
  panel.hidden = false;
  updateHistActions(list.length);
}

/* Кнопки «Удалить выбранные» и «Удалить все» над таблицей истории. */
function updateHistActions(total = loadHistory().length) {
  const bar = $("histActions");
  if (!bar) return;
  bar.hidden = total === 0;
  $("histDeleteSel").disabled = historySel.size === 0;
  $("histSelCount").textContent = historySel.size;
  const boxes = document.querySelectorAll(".histSel");
  const checkAll = $("histCheckAll");
  if (checkAll && boxes.length > 0) {
    checkAll.checked = historySel.size === boxes.length;
    checkAll.indeterminate = historySel.size > 0 && historySel.size < boxes.length;
  }
}

/* ---------------- Служебное ---------------- */

/* Красивые подписи для известных файлов, для остальных — имя без расширения. */
const PRETTY_FILE_NAMES = {
  "countries.csv": "Страны",
  "chemicals.csv": "Вещества",
  "words.txt": "Слова",
};

function prettyFileName(fileName) {
  const known = PRETTY_FILE_NAMES[fileName.toLowerCase()];
  if (known) return known;
  const stem = fileName.replace(/\.[^.]+$/, "").replace(/[_-]+/g, " ").trim();
  return stem ? stem.charAt(0).toUpperCase() + stem.slice(1) : fileName;
}

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
  $("endBtn").addEventListener("click", onJumpEnd);
  $("backBtn").addEventListener("click", onBack);
  $("resetBtn").addEventListener("click", onReset);

  $("ascBtn").addEventListener("click", () => setDirection(true));
  $("descBtn").addEventListener("click", () => setDirection(false));

  $("delayRange").addEventListener("input", (e) => {
    state.delayMs = Number(e.target.value);
    $("delayVal").textContent = String(state.delayMs);
    if (state.playing) scheduleTick(); // пересобираем таймер с новой задержкой
  });

  $("hasHeader").addEventListener("change", async () => {
    stashSession();
    state.hasHeader = $("hasHeader").checked;
    await loadPreview(state.fileName, state.hasHeader);
    state.sessionKey = planConfig();
    restoreSession();
  });

  // Кнопка «пролистать журнал вниз».
  $("logDownBtn").addEventListener("click", () => {
    const list = $("logList");
    list.scrollTop = list.scrollHeight;
  });

  // История сортировок: сортировка по времени и удаление записей.
  $("historyTable").addEventListener("click", (e) => {
    const del = e.target.closest("button[data-del]");
    if (del) {
      saveHistory(loadHistory().filter((h) => h.id !== del.dataset.del));
      renderHistory();
      return;
    }
    if (e.target.closest("th[data-sort]")) {
      historyDesc = !historyDesc;
      renderHistory();
    }
  });

  // Выбор записей истории: чекбоксы в строках и «выбрать все» в шапке.
  $("historyTable").addEventListener("change", (e) => {
    if (e.target.classList.contains("histSel")) {
      if (e.target.checked) historySel.add(e.target.dataset.id);
      else historySel.delete(e.target.dataset.id);
      updateHistActions();
    } else if (e.target.id === "histCheckAll") {
      document.querySelectorAll(".histSel").forEach((b) => {
        b.checked = e.target.checked;
        if (e.target.checked) historySel.add(b.dataset.id);
        else historySel.delete(b.dataset.id);
      });
      updateHistActions();
    }
  });

  // Массовое удаление: только выбранные или вся история.
  $("histDeleteSel").addEventListener("click", () => {
    saveHistory(loadHistory().filter((h) => !historySel.has(h.id)));
    historySel.clear();
    renderHistory();
  });
  $("histDeleteAll").addEventListener("click", () => {
    if (!confirm("Удалить все записи истории сортировок?")) return;
    saveHistory([]);
    historySel.clear();
    renderHistory();
  });

  // Вкладки заданий.
  $("tabs").addEventListener("click", (e) => {
    const btn = e.target.closest(".tab");
    if (!btn) return;
    document.querySelectorAll(".tab").forEach((t) => t.classList.toggle("active", t === btn));
    document.querySelectorAll(".tabPage").forEach((p) => p.classList.toggle("active", p.id === `tab-${btn.dataset.tab}`));
    const sub = TAB_SUBTITLES[btn.dataset.tab];
    if (sub) $("brandSub").textContent = `Лабораторная работа №4 · ${sub}`;
    // Примечание про внешнюю сортировку относится только к заданию 2.
    $("pageNote").hidden = btn.dataset.tab !== "task2";
  });
}

function setDirection(ascending) {
  if (ascending === state.ascending) return;
  stashSession();
  state.ascending = ascending;
  $("ascBtn").classList.toggle("active", ascending);
  $("descBtn").classList.toggle("active", !ascending);
  state.sessionKey = planConfig();
  restoreSession();
}

init();
