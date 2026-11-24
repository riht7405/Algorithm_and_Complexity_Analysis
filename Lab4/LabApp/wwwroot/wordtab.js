/* Лабораторная 4, задание 3 — сортировка слов текста (QuickSort улучшенный + ABC-сортировка),
   подсчёт вхождений и замеры времени. Ванильный JS, без сборки.
   У каждого сочетания настроек (текст, алгоритм, порядок, лимит слов) — своя независимая
   сессия: переключение туда-обратно сохраняет прогресс, ничего не сбрасывается. */
"use strict";

(() => {
  const $ = (id) => document.getElementById(id);

  /* ---------------- состояние ---------------- */

  const state = {
    texts: [],
    methods: [],
    fileName: null,
    method: "quick-improved",
    ascending: true,
    limitWords: 60,
    sessions: new Map(),  // ключ настроек -> {plan, cursor, resultShown}
    sessionKey: "",
    plan: null,           // план текущей сессии (ответ /api/word-sort/run)
    wordsById: new Map(), // id слова -> текст
    cursor: -1,           // текущий шаг
    resultShown: null,    // файл результата, для которого показана таблица
    logStep: -1,          // до какого шага нарисован журнал
    playing: false,
    timer: null,
    delayMs: 300,
    benchRows: [],        // строки таблицы замеров
    benchRunning: false,
  };

  /* Серии экспериментов по заданию: 100, 500, 1000, 2000, 5000 слов. */
  const EXPERIMENT_LENGTHS = [100, 500, 1000, 2000, 5000];
  const BENCH_KEY = "lab4.wordBench.v1";

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

  /* ---------------- инициализация ---------------- */

  async function init() {
    if (!$("wTextSelect")) return; // вкладки задания 3 нет (не должно случаться)
    bindControls();

    try {
      const [texts, methods] = await Promise.all([api("/api/texts"), api("/api/word-sort/methods")]);
      state.texts = texts;
      state.methods = methods;
    } catch (err) {
      showError("Не удалось связаться с сервером: " + err.message);
      return;
    }

    fillTextSelect();
    renderMethods();
    loadBench();
    await onTextChanged();
  }

  function fillTextSelect(select) {
    const sel = $("wTextSelect");
    sel.innerHTML = state.texts
      .map((t) => `<option value="${escapeHtml(t.fileName)}">${escapeHtml(prettyFileName(t.fileName))} (${escapeHtml(t.fileName)})</option>`)
      .join("");
    if (select && state.texts.some((t) => t.fileName === select)) sel.value = select;
    sel.addEventListener("change", onTextChanged);
  }

  function renderMethods() {
    const list = $("wMethodList");
    if (!state.methods.some((m) => m.id === state.method) && state.methods.length > 0) {
      state.method = state.methods[0].id; // сохранённый метод удалён — берём первый
    }
    list.innerHTML = state.methods.map((m) => `
      <div class="methodCard ${m.id === state.method ? "active" : ""}" data-method="${escapeHtml(m.id)}">
        <div class="mTitle">
          <span>${escapeHtml(m.title)}</span>
          <span class="memBadge">${escapeHtml(m.badge)}</span>
        </div>
        <div class="mInfo">${escapeHtml(m.shortInfo)}</div>
        <div class="mInfo">в памяти: ${m.memorySlots} знач. · счётчик: ${escapeHtml(m.passesLabel)}</div>
      </div>`).join("");
    list.querySelectorAll(".methodCard").forEach((card) => {
      card.addEventListener("click", () => {
        const id = card.dataset.method;
        if (id === state.method) return;
        stashSession();
        state.method = id;
        list.querySelectorAll(".methodCard").forEach((c) => c.classList.toggle("active", c === card));
        state.sessionKey = sessionKey();
        restoreSession();
      });
    });
  }

  function prettyFileName(fileName) {
    const m = /^text_(\d+)\.txt$/.exec(fileName);
    if (m) return `Сгенерированный текст · ${m[1]} слов`;
    return fileName.replace(/\.[^.]+$/, "");
  }

  /* ---------------- текст: выбор, превью, генерация ---------------- */

  async function onTextChanged() {
    const fileName = $("wTextSelect").value;
    if (!fileName) return;
    stashSession();
    state.fileName = fileName;
    await loadPreview(fileName);
    if (state.fileName !== fileName) return; // пока грузились, выбрали другой текст
    state.sessionKey = sessionKey();
    restoreSession();
  }

  async function loadPreview(fileName) {
    hideError();
    try {
      const p = await post("/api/texts/preview", { fileName });
      if (state.fileName !== fileName) return; // устарело
      $("wTextCaption").textContent =
        `файл ${p.fileName} · слов: ${p.totalWords} · уникальных: ${p.uniqueWords}`;
      $("wTextPreview").textContent = p.preview + (p.previewTruncated ? "\n…" : "");
    } catch (err) {
      showError(err.message);
      $("wTextCaption").textContent = "";
      $("wTextPreview").textContent = "";
    }
  }

  async function onGenerate() {
    const n = Number($("wGenLength").value);
    const btn = $("wGenBtn");
    btn.disabled = true;
    btn.textContent = "Генерирую…";
    hideError();
    try {
      const r = await post("/api/texts/generate", { wordCount: n });
      if (!state.texts.some((t) => t.fileName === r.fileName)) {
        state.texts.push({ fileName: r.fileName, sizeBytes: 0 });
        state.texts.sort((a, b) => a.fileName.localeCompare(b.fileName));
        fillTextSelect(r.fileName);
      } else {
        $("wTextSelect").value = r.fileName;
      }
      await onTextChanged();
      $("wBenchStatus").textContent =
        `Создан файл ${r.fileName}: ${r.wordCount} слов, уникальных — ${r.uniqueWords}.`;
    } catch (err) {
      showError(err.message);
    } finally {
      btn.disabled = false;
      btn.textContent = "Создать";
    }
  }

  /* ---------------- сессии настроек ---------------- */

  function sessionKey() {
    return JSON.stringify({
      f: state.fileName,
      m: state.method,
      a: state.ascending,
      n: state.limitWords,
    });
  }

  function syncSession() {
    if (!state.plan) return;
    if (!state.sessions.has(state.sessionKey) && state.sessions.size >= 10) {
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

  function stashSession() {
    stopPlayback();
    syncSession();
  }

  function restoreSession() {
    stopPlayback();
    const s = state.sessions.get(state.sessionKey);
    state.plan = s ? s.plan : null;
    state.cursor = s ? s.cursor : -1;
    state.resultShown = s ? s.resultShown : null;
    state.logStep = -1;
    $("wLogList").innerHTML = "";
    state.wordsById = new Map(
      state.plan ? state.plan.words.map((w) => [w.id, w.text]) : []);
    renderSessionView({ animateLog: false, record: false });
    updateButtons();
    updatePlayBtn();
  }

  /* ---------------- запуск и воспроизведение ---------------- */

  async function ensurePlan() {
    if (state.plan) return true;
    const keyAtStart = state.sessionKey;
    hideError();
    $("wDescBox").textContent = "Считаем шаги сортировки и подсчёт…";
    try {
      const plan = await post("/api/word-sort/run", {
        fileName: state.fileName,
        method: state.method,
        ascending: state.ascending,
        limitWords: state.limitWords,
      });
      if (state.sessionKey !== keyAtStart) return false; // настройки сменили, пока считали
      state.plan = plan;
      state.cursor = -1;
      state.logStep = -1;
      state.resultShown = null;
      state.wordsById = new Map(plan.words.map((w) => [w.id, w.text]));
      syncSession();
      return true;
    } catch (err) {
      if (state.sessionKey === keyAtStart) {
        showError(err.message);
        $("wDescBox").textContent = "Настрой параметры и нажми «Запустить».";
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

  function updatePlayBtn() {
    const btn = $("wPlayBtn");
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
    $("wBackBtn").disabled = !has || state.cursor < 0;
    $("wStepBtn").disabled = false;
    $("wResetBtn").disabled = !has;
  }

  function applyStep(i, opts = {}) {
    if (i < -1 || !state.plan) return;
    state.cursor = i;
    syncSession();
    renderSessionView(opts);
    updateButtons();
    updatePlayBtn();
  }

  /* ---------------- отрисовка состояния ---------------- */

  function renderSessionView(opts = {}) {
    const { animateLog = true, record = true } = opts;
    const badge = $("wPhaseBadge");

    if (!state.plan || state.cursor < 0) {
      state.logStep = -1;
      renderScene();
      badge.hidden = true;
      $("wMemoryBox").hidden = true;
      $("wLegend").hidden = true;
      $("wCounterRow").hidden = true;
      $("wResultPanel").hidden = true;
      $("wProgressBar").style.width = "0";
      $("wStepCounter").textContent = "шаг 0 / 0";
      $("wDescBox").classList.remove("done");
      $("wDescBox").textContent = state.plan
        ? "Готово к запуску: нажми «Запустить» или «Шаг»."
        : "Нажми «Запустить» или «Шаг» — покажу сортировку по шагам: что сравниваем, куда переставляем и как считаем вхождения.";
      $("wLogList").innerHTML = '<div class="logEmpty">Журнал появится после запуска сортировки.</div>';
      return;
    }

    const step = state.plan.steps[state.cursor];
    renderScene();
    renderMemory(step);
    renderDesc(step);
    renderCounters(step);
    syncLog(state.cursor, animateLog);
    renderProgress(step);
    $("wLegend").hidden = false;

    if (step.kind === "Done") {
      showDone(step, record);
    } else {
      $("wResultPanel").hidden = true;
    }
  }

  /* Состояние на шаге i: последний снимок ≤ i (сравнения порядок не меняют). */
  function snapshotAt(field, i) {
    const steps = state.plan.steps;
    for (let j = Math.min(i, steps.length - 1); j >= 0; j--) {
      if (steps[j][field]) return steps[j][field];
    }
    return null;
  }

  function renderScene() {
    const scene = $("wScene");
    if (!state.plan) {
      scene.innerHTML = '<div class="emptyStage">Выбери текст и нажми «Запустить».</div>';
      return;
    }
    const words = state.plan.words;
    const arr = (state.cursor >= 0 ? snapshotAt("arrayIds", state.cursor) : null) || words.map((w) => w.id);
    const placed = new Set((state.cursor >= 0 ? snapshotAt("placed", state.cursor) : []) || []);
    const range = new Set((state.cursor >= 0 ? snapshotAt("rangeIds", state.cursor) : []) || []);
    const buckets = state.cursor >= 0 ? snapshotAt("buckets", state.cursor) : null;
    const step = state.plan.steps[state.cursor];
    const hi = new Set(step && step.highlights ? step.highlights : []);
    const mem = new Set(step && step.memoryIds ? step.memoryIds : []);
    const isSwap = step && (step.kind === "Swap" || step.kind === "Shift" || step.kind === "Place" || step.kind === "Collect");

    const chips = arr.map((id) => {
      const cls = ["chip", "wchip"];
      if (placed.has(id)) cls.push("wPlaced");
      if (range.has(id)) cls.push("wRange");
      if (id === (step && step.pivotId)) cls.push("wPivot");
      else if (hi.has(id)) cls.push(isSwap ? "wSwap" : "wCmp");
      if (mem.has(id)) cls.push("wMem");
      return `<span class="${cls.join(" ")}" title="${escapeHtml(titleOf(id))}">${escapeHtml(labelOf(id))}</span>`;
    }).join("");

    let bucketsHtml = "";
    if (buckets) {
      bucketsHtml = `<div class="wBuckets">` + buckets.map((b) => {
        const active = b.ids.some((id) => hi.has(id));
        return `<div class="bucket ${active ? "active" : ""}">
          <div class="bucketLetter">${escapeHtml(b.letter === "✓" ? "✓ короткие" : `«${b.letter}»`)}</div>
          <div class="bucketChips">${b.ids.length === 0
            ? '<span class="ghost">пусто</span>'
            : b.ids.map((id) => `<span class="chip wchip wSmall ${hi.has(id) ? "wSwap" : ""}" title="${escapeHtml(titleOf(id))}">${escapeHtml(labelOf(id))}</span>`).join("")}</div>
        </div>`;
      }).join("") + `</div>`;
    }

    const bucketNote = buckets
      ? `<div class="wBucketNote">Корзины текущего куска — слова разложены по букве, сейчас идёт разбор корзин.</div>`
      : "";

    scene.innerHTML = `
      <div class="wArrayWrap">
        <div class="wArrayTitle">Массив слов</div>
        <div class="chips">${chips}</div>
      </div>
      ${bucketNote}${bucketsHtml}`;
  }

  function labelOf(id) {
    const text = state.wordsById.get(id) ?? `№${id}`;
    return text.length > 22 ? text.slice(0, 21) + "…" : text;
  }

  function titleOf(id) {
    const idx = state.plan.words.findIndex((w) => w.id === id);
    return `№${idx + 1}: ${state.wordsById.get(id) ?? "?"}`;
  }

  function renderMemory(step) {
    const box = $("wMemoryBox");
    box.hidden = false;
    const slots = state.plan.memorySlots;
    const cells = [];
    for (let i = 0; i < slots; i++) {
      const id = step.memoryIds[i];
      cells.push(id === undefined
        ? `<div class="slot"><div class="slotEmpty">пусто</div></div>`
        : `<div class="slot filled"><div class="slotLabel" title="${escapeHtml(titleOf(id))}">${escapeHtml(labelOf(id))}</div></div>`);
    }
    box.innerHTML = `
      <div class="memoryTitle"><b>Память программы</b> — ячеек: ${slots}. ${escapeHtml(step.memoryNote)}</div>
      <div class="memorySlots">${cells.join("")}</div>`;
  }

  function renderDesc(step) {
    const box = $("wDescBox");
    box.classList.toggle("done", step.kind === "Done");
    const badge = $("wPhaseBadge");
    badge.hidden = false;
    badge.textContent = step.passTitle ? `${step.phase} · ${step.passTitle}` : step.phase;
    box.textContent = step.kind === "Done"
      ? step.desc
      : step.desc;
  }

  function renderCounters(step) {
    const row = $("wCounterRow");
    row.hidden = false;
    const passesLabel = state.plan.passesLabel || "проходов";
    const html = `
      <div class="stat" id="wStatC"><div class="num">0</div><div class="lbl">⚖ сравнений</div></div>
      <div class="stat" id="wStatM"><div class="num">0</div><div class="lbl">↔ перестановок</div></div>
      <div class="stat" id="wStatP"><div class="num">0</div><div class="lbl">↻ ${escapeHtml(passesLabel)}</div></div>`;
    if (row.dataset.key !== state.plan.passesLabel) {
      row.innerHTML = html;
      row.dataset.key = state.plan.passesLabel;
      row.querySelectorAll(".stat").forEach((s) =>
        s.addEventListener("animationend", () => s.classList.remove("pulse")));
    }
    setStat("wStatC", step.comparisons);
    setStat("wStatM", step.moves);
    setStat("wStatP", step.passes);
  }

  function setStat(id, value) {
    const stat = $(id);
    if (!stat) return;
    const num = stat.querySelector(".num");
    const text = String(value);
    if (num.textContent === text) return;
    num.textContent = text;
    stat.classList.remove("pulse");
    void stat.offsetWidth;
    stat.classList.add("pulse");
  }

  function syncLog(toStep, animate = true) {
    const list = $("wLogList");
    const steps = state.plan.steps;
    if (toStep < state.logStep) {
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
    while (list.children.length > 800) list.removeChild(list.firstChild);
    state.logStep = toStep;
    if ($("wFollowLog").checked) list.scrollTop = list.scrollHeight;
  }

  function renderProgress(step) {
    const total = state.plan.steps.length;
    $("wProgressBar").style.width = `${((step.index + 1) / total) * 100}%`;
    $("wStepCounter").textContent = `шаг ${step.index + 1} / ${total}`;
  }

  async function showDone(step, record) {
    updatePlayBtn();
    await showResultPanel(state.plan);
  }

  async function showResultPanel(plan) {
    if (!plan.resultFileName) return;
    const key = plan.resultFileName;
    if (state.resultShown !== key) {
      // данные подсчёта уже в плане — просто рисуем
      const maxCount = Math.max(...plan.counts.map((c) => c.count), 1);
      const rows = plan.counts.map((c) => `
        <tr>
          <td class="rowId">${c.rank}</td>
          <td>${escapeHtml(c.word)}</td>
          <td class="num">${c.count}</td>
          <td class="cntCell"><span class="cntBar" style="width:${Math.max(4, Math.round((c.count / maxCount) * 100))}%"></span></td>
        </tr>`).join("");
      const truncated = plan.countsTruncated
        ? `<tr class="cntTruncated"><td colspan="4">Показаны первые ${plan.counts.length} из ${plan.countsUniqueWords} уникальных слов — полный список в файле результата.</td></tr>`
        : "";
      $("wCountsTable").innerHTML = `
        <table class="data wCounts">
          <thead><tr><th>#</th><th>Слово</th><th>Вхождений</th><th></th></tr></thead>
          <tbody>${rows}${truncated}</tbody>
        </table>`;
      $("wResultCaption").textContent =
        `файл ${plan.resultFileName} · слов в тексте: ${plan.totalWords} · уникальных: ${plan.countsUniqueWords}`;
      $("wCountNote").textContent = plan.countNote;
      $("wResultDownload").href = `/api/word-sort/results/${encodeURIComponent(plan.resultFileName)}`;
      const sortedIds = snapshotAt("arrayIds", state.plan.steps.length - 1) || plan.words.map((w) => w.id);
      $("wSortedChips").innerHTML = sortedIds.map((id) =>
        `<span class="chip wchip" title="${escapeHtml(titleOf(id))}">${escapeHtml(labelOf(id))}</span>`).join("");
      state.resultShown = key;
      syncSession();
    }
    $("wResultPanel").hidden = false;
  }

  /* ---------------- замеры времени (по схеме 1-й лабораторной) ---------------- */

  function loadBench() {
    try {
      state.benchRows = JSON.parse(localStorage.getItem(BENCH_KEY)) || [];
    } catch { state.benchRows = []; }
    renderBenchTable();
  }

  function saveBench() {
    try { localStorage.setItem(BENCH_KEY, JSON.stringify(state.benchRows)); } catch { /* нет localStorage */ }
  }

  async function benchOne(algorithm, runs, params) {
    return await post("/api/word-sort/bench", { algorithm, runs, ...params });
  }

  function addBenchRow(row) {
    row.ts = new Date().toISOString();
    // Одинаковая точка (объём + алгоритм) обновляется, а не дублируется.
    const i = state.benchRows.findIndex(
      (r) => r.wordCount === row.wordCount && r.algorithm === row.algorithm);
    if (i >= 0) state.benchRows[i] = row;
    else state.benchRows.push(row);
    state.benchRows.sort((a, b) =>
      a.wordCount - b.wordCount || a.algorithm.localeCompare(b.algorithm));
    saveBench();
    renderBenchTable();
  }

  async function runSeries() {
    if (state.benchRunning) return;
    const runs = clampRuns();
    state.benchRunning = true;
    setBenchButtons();
    const algos = state.methods.map((m) => m.id);
    const points = EXPERIMENT_LENGTHS.length * algos.length;
    let done = 0;
    const t0 = performance.now();
    try {
      for (const length of EXPERIMENT_LENGTHS) {
        for (const algo of algos) {
          done++;
          $("wBenchStatus").textContent =
            `Точка ${done} из ${points}: ${length} слов · ${titleOfAlgo(algo)} · замеряю…`;
          const row = await benchOne(algo, runs, { wordCount: length });
          addBenchRow(row);
        }
      }
      $("wBenchStatus").textContent =
        `Готово: ${points} точек за ${((performance.now() - t0) / 1000).toFixed(1)} с. ` +
        "В таблице — среднее, минимум и максимум по прогонам (после прогрева).";
    } catch (err) {
      $("wBenchStatus").textContent = "Ошибка: " + err.message;
    } finally {
      state.benchRunning = false;
      setBenchButtons();
    }
  }

  async function benchByParams(params, label) {
    if (state.benchRunning) return;
    const runs = clampRuns();
    state.benchRunning = true;
    setBenchButtons();
    const algos = state.methods.map((m) => m.id);
    let done = 0;
    try {
      for (const algo of algos) {
        done++;
        $("wBenchStatus").textContent = `${label} · точка ${done} из ${algos.length}: ${titleOfAlgo(algo)} · замеряю…`;
        const row = await benchOne(algo, runs, params);
        addBenchRow(row);
      }
      $("wBenchStatus").textContent = `Готово: замер для «${label}» по ${algos.length} алгоритмам добавлен в таблицу.`;
    } catch (err) {
      $("wBenchStatus").textContent = "Ошибка: " + err.message;
    } finally {
      state.benchRunning = false;
      setBenchButtons();
    }
  }

  function clampRuns() {
    const v = Number($("wBenchRuns").value);
    return Number.isFinite(v) ? Math.min(10, Math.max(1, Math.round(v))) : 5;
  }

  function setBenchButtons() {
    ["wBenchRunBtn", "wBenchFileBtn", "wBenchCustomBtn", "wBenchClearBtn"].forEach((id) => {
      $(id).disabled = state.benchRunning;
    });
  }

  function titleOfAlgo(id) {
    const m = state.methods.find((m) => m.id === id);
    return m ? m.title : id;
  }

  function renderBenchTable() {
    const rows = state.benchRows;
    $("wBenchCsvBtn").hidden = rows.length === 0;
    if (rows.length === 0) {
      $("wBenchTable").innerHTML =
        '<p class="hint">Таблица пустая. Нажми «Провести серию» — замерим оба алгоритма на текстах 100, 500, 1000, 2000 и 5000 слов.</p>';
      $("wChartWrap").hidden = true;
      return;
    }

    // Отношение ABC к QuickSort по среднему времени (на одинаковой длине).
    const quickAvg = new Map(
      rows.filter((r) => r.algorithm === "quick-improved").map((r) => [r.wordCount, r.avgMs]));

    const head = `<tr>
      <th>Слов в тексте</th><th>Алгоритм</th><th>Прогонов</th>
      <th>Среднее, мс</th><th>Мин, мс</th><th>Макс, мс</th>
      <th>Сравнений</th><th>Перестановок</th><th>К QuickSort</th>
    </tr>`;
    const body = rows.map((r) => {
      let ratio = "";
      if (r.algorithm !== "quick-improved" && quickAvg.has(r.wordCount) && quickAvg.get(r.wordCount) > 0) {
        const x = r.avgMs / quickAvg.get(r.wordCount);
        ratio = `×${x >= 10 ? x.toFixed(0) : x.toFixed(2)}`;
      } else if (r.algorithm === "quick-improved") {
        ratio = "—";
      }
      return `<tr>
        <td class="num">${r.wordCount}</td>
        <td>${escapeHtml(r.algorithmTitle)}</td>
        <td class="num">${r.runs}</td>
        <td class="num">${fmtMs(r.avgMs)}</td>
        <td class="num">${fmtMs(r.minMs)}</td>
        <td class="num">${fmtMs(r.maxMs)}</td>
        <td class="num">${r.comparisons}</td>
        <td class="num">${r.moves}</td>
        <td class="num">${ratio}</td>
      </tr>`;
    }).join("");
    $("wBenchTable").innerHTML = `<table class="data"><thead>${head}</thead><tbody>${body}</tbody></table>`;
    renderChart();
  }

  function fmtMs(v) {
    return v >= 100 ? v.toFixed(0) : v >= 10 ? v.toFixed(1) : v.toFixed(2);
  }

  /* Простой SVG-график: среднее время (мс) по длине текста, линия на каждый алгоритм. */
  function renderChart() {
    const rows = state.benchRows;
    const lengths = [...new Set(rows.map((r) => r.wordCount))].sort((a, b) => a - b);
    const algos = [...new Set(rows.map((r) => r.algorithm))];
    if (rows.length < 2 || lengths.length < 2) { $("wChartWrap").hidden = true; return; }

    const W = 640, H = 280, padL = 60, padR = 16, padT = 22, padB = 40;
    const maxMs = Math.max(...rows.map((r) => r.avgMs)) * 1.1 || 1;
    const x = (i) => padL + (i * (W - padL - padR)) / Math.max(1, lengths.length - 1);
    const y = (v) => H - padB - (Math.min(v, maxMs) / maxMs) * (H - padT - padB);

    const grid = [];
    for (let g = 0; g <= 4; g++) {
      const v = (maxMs / 4) * g;
      const gy = y(v);
      grid.push(`<line x1="${padL}" y1="${gy}" x2="${W - padR}" y2="${gy}" stroke="#e5e7eb" stroke-width="1"/>
        <text x="${padL - 6}" y="${gy + 3.5}" text-anchor="end" class="axLabel">${fmtMs(v)}</text>`);
    }
    const xLabels = lengths.map((L, i) =>
      `<text x="${x(i)}" y="${H - padB + 16}" text-anchor="middle" class="axLabel">${L}</text>`).join("");

    const colors = { "quick-improved": "#4f46e5", "abc": "#059669" };
    const series = algos.map((algo) => {
      const pts = lengths.map((L, i) => {
        const row = rows.find((r) => r.algorithm === algo && r.wordCount === L);
        return row ? { px: x(i), py: y(row.avgMs), v: row.avgMs } : null;
      }).filter(Boolean);
      if (pts.length === 0) return "";
      const line = pts.map((p, i) => `${i === 0 ? "M" : "L"}${p.px.toFixed(1)},${p.py.toFixed(1)}`).join(" ");
      const dots = pts.map((p) =>
        `<circle cx="${p.px.toFixed(1)}" cy="${p.py.toFixed(1)}" r="4" fill="${colors[algo] || "#6b7280"}"><title>${escapeHtml(titleOfAlgo(algo))}: ${fmtMs(p.v)} мс</title></circle>`).join("");
      return `<path d="${line}" fill="none" stroke="${colors[algo] || "#6b7280"}" stroke-width="2.5"/>${dots}`;
    }).join("");

    const legend = algos.map((algo) =>
      `<span class="chartLegend"><span class="dot" style="background:${colors[algo] || "#6b7280"}"></span>${escapeHtml(titleOfAlgo(algo))}</span>`).join("");

    $("wChartWrap").innerHTML = `
      <div class="chartLegendRow">${legend}<span class="chartTitle">Среднее время сортировки, мс</span></div>
      <svg viewBox="0 0 ${W} ${H}" role="img" aria-label="График времени сортировки">
        <rect x="${padL}" y="${padT}" width="${W - padL - padR}" height="${H - padT - padB}" fill="#fcfcfe" stroke="#e5e7eb"/>
        ${grid.join("")}${xLabels}${series}
        <text x="${(padL + W - padR) / 2}" y="${H - 6}" text-anchor="middle" class="axLabel">слов в тексте</text>
      </svg>`;
    $("wChartWrap").hidden = false;
  }

  function downloadCsv() {
    const head = "слов;алгоритм;прогонов;среднее_мс;мин_мс;макс_мс;сравнений;перестановок;источник";
    const lines = state.benchRows.map((r) =>
      [r.wordCount, r.algorithmTitle, r.runs, r.avgMs.toFixed(3), r.minMs.toFixed(3),
       r.maxMs.toFixed(3), r.comparisons, r.moves, r.source].join(";"));
    const blob = new Blob(["\uFEFF" + [head, ...lines].join("\n")], { type: "text/csv;charset=utf-8" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "lab4_word_sort_bench.csv";
    a.click();
    URL.revokeObjectURL(a.href);
  }

  /* ---------------- служебное ---------------- */

  function escapeHtml(text) {
    return String(text)
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
  }

  function showError(message) {
    const box = $("wErrorBox");
    box.textContent = "⚠ " + message;
    box.hidden = false;
  }

  function hideError() {
    $("wErrorBox").hidden = true;
  }

  function bindControls() {
    $("wPlayBtn").addEventListener("click", onPlay);
    $("wStepBtn").addEventListener("click", onStep);
    $("wEndBtn").addEventListener("click", onJumpEnd);
    $("wBackBtn").addEventListener("click", onBack);
    $("wResetBtn").addEventListener("click", onReset);
    $("wGenBtn").addEventListener("click", onGenerate);

    $("wAscBtn").addEventListener("click", () => setDirection(true));
    $("wDescBtn").addEventListener("click", () => setDirection(false));

    $("wDelayRange").addEventListener("input", (e) => {
      state.delayMs = Number(e.target.value);
      $("wDelayVal").textContent = String(state.delayMs);
      if (state.playing) scheduleTick();
    });

    $("wLimitRange").addEventListener("input", (e) => {
      $("wLimitVal").textContent = e.target.value;
    });
    $("wLimitRange").addEventListener("change", (e) => {
      stashSession();
      state.limitWords = Number(e.target.value);
      state.sessionKey = sessionKey();
      restoreSession();
    });

    $("wLogDownBtn").addEventListener("click", () => {
      const list = $("wLogList");
      list.scrollTop = list.scrollHeight;
    });

    // Замеры времени.
    $("wBenchRunBtn").addEventListener("click", runSeries);
    $("wBenchFileBtn").addEventListener("click", () => {
      const file = $("wTextSelect").value;
      benchByParams({ fileName: file }, `текст ${file}`);
    });
    $("wBenchCustomBtn").addEventListener("click", () => {
      const v = Number($("wBenchCustom").value);
      if (!Number.isFinite(v) || v < 10) {
        $("wBenchStatus").textContent = "Введите объём — не меньше 10 слов.";
        return;
      }
      benchByParams({ wordCount: Math.min(100000, Math.round(v)) }, `${Math.min(100000, Math.round(v))} слов`);
    });
    $("wBenchCsvBtn").addEventListener("click", downloadCsv);
    $("wBenchClearBtn").addEventListener("click", () => {
      if (!confirm("Удалить все результаты замеров?")) return;
      state.benchRows = [];
      saveBench();
      renderBenchTable();
      $("wBenchStatus").textContent = "";
    });
  }

  function setDirection(ascending) {
    if (ascending === state.ascending) return;
    stashSession();
    state.ascending = ascending;
    $("wAscBtn").classList.toggle("active", ascending);
    $("wDescBtn").classList.toggle("active", !ascending);
    state.sessionKey = sessionKey();
    restoreSession();
  }

  init();
})();
