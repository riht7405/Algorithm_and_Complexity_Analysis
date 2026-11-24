/* Лабораторная 4, задание 3 — сортировка слов текста (QuickSort улучшенный + ABC-сортировка),
   подсчёт вхождений и замеры времени. Ванильный JS, без сборки.
   Готовые тексты (5…100 слов) детерминированы, поэтому у каждого сочетания настроек
   (текст, алгоритм, порядок) — своя независимая сессия: переключение туда-обратно
   сохраняет прогресс. Случайные тексты каждый раз новые — их состояние не сохраняется.
   Замеры времени полностью независимы от настроек демонстрации. */
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
    sessions: new Map(),  // ключ настроек -> {plan, cursor, resultShown}
    sessionKey: "",
    plan: null,           // план текущей сессии (ответ /api/word-sort/run или /run-random)
    wordsById: new Map(), // id слова -> текст
    cursor: -1,           // текущий шаг
    resultShown: null,    // файл результата, для которого показана таблица
    logStep: -1,          // до какого шага нарисован журнал
    playing: false,
    timer: null,
    delayMs: 300,
    random: null,         // {wordCount, seed} для активного случайного текста (null — готовый текст)
    benchRows: [],        // все замеры (каждый «Замерить» — новая запись истории)
    benchNextId: 1,
    benchFilter: null,    // Set id строк истории — показывать на графике только их
    histSelected: new Set(),
    benchRunning: false,      // идёт серия замеров
    benchSeriesCancel: false, // кнопка «Остановить» во время серии
    chartEpochMs: 0,          // «Очистить график»: дальше на графике только новые замеры
  };

  const BENCH_KEY = "lab4.wordBench.v1";
  let benchChart = null; // текущий график Chart.js (пересоздаётся на каждую отрисовку)

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
    renderBenchAll(); // сохранённые замеры показываем сразу при открытии страницы
    await onTextChanged();
  }

  /* Подпись в списке «Готовые текста»: только количество слов и имя файла. */
  function prettyFileName(fileName) {
    const m = /^text_(\d+)\.txt$/.exec(fileName);
    if (m) return `${m[1]} слов`;
    return fileName.replace(/\.[^.]+$/, "");
  }

  function fillTextSelect(select) {
    const sel = $("wTextSelect");
    // Сверху вниз по количеству слов: 5, 10, 25, 50, 100; свои файлы — после них по алфавиту.
    const order = (t) => {
      const m = /^text_(\d+)\.txt$/.exec(t.fileName);
      return m ? [0, Number(m[1])] : [1, t.fileName];
    };
    state.texts.sort((a, b) => {
      const [ka, va] = order(a), [kb, vb] = order(b);
      return ka !== kb ? ka - kb : va < vb ? -1 : va > vb ? 1 : 0;
    });
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
        if (state.random) {
          loadRandom(state.random.wordCount, state.random.seed); // тот же текст, другой алгоритм
        } else {
          state.sessionKey = sessionKey();
          restoreSession();
        }
      });
    });
  }

  /* ---------------- текст: готовые текста ---------------- */

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

  /* ---------------- случайный текст ---------------- */

  async function onRandom() {
    const n = clampInt($("wRandomWords"), 1, 100, 25);
    stashSession(); // сохраняем сессию подготовленного текста, если она была
    const btn = $("wRandomBtn");
    btn.disabled = true;
    btn.textContent = "Генерирую…";
    hideError();
    try {
      await loadRandom(n, null); // без сидa — каждый раз новый случайный текст
    } catch (err) {
      showError(err.message);
    } finally {
      btn.disabled = false;
      btn.textContent = "Сгенерировать";
    }
  }

  async function loadRandom(wordCount, seed) {
    hideError();
    const plan = await post("/api/word-sort/run-random", {
      method: state.method,
      ascending: state.ascending,
      wordCount,
      seed: seed ?? undefined,
    });
    stopPlayback();
    state.plan = plan;
    state.cursor = -1;
    state.logStep = -1;
    state.resultShown = null;
    state.random = { wordCount: plan.wordCount, seed: plan.randomSeed };
    state.sessionKey = "random"; // случайные сессии не кэшируются и не восстанавливаются
    state.wordsById = new Map(plan.words.map((w) => [w.id, w.text]));
    $("wTextCaption").textContent = `${plan.fileName} · уникальных: ${plan.uniqueWords}`;
    $("wTextPreview").textContent = plan.sourcePreview || "";
    renderSessionView({ animateLog: false, record: false });
    updateButtons();
    updatePlayBtn();
  }

  /* ---------------- сессии настроек (для готовых текстов) ---------------- */

  function sessionKey() {
    return JSON.stringify({
      f: state.fileName,
      m: state.method,
      a: state.ascending,
    });
  }

  function syncSession() {
    if (!state.plan || state.random) return; // случайные тексты не сохраняем — они каждый раз новые
    if (!state.sessions.has(state.sessionKey) && state.sessions.size >= 12) {
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
    state.random = null;
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
        : "Выбери готовый текст или сгенерируй случайный и нажми «Запустить» — покажу сортировку по шагам: что сравниваем, куда переставляем и как считаем вхождения.";
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
      updatePlayBtn();
      showResultPanel(state.plan);
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

    // Пустой список корзин («[]» на старте нового куска) — корзины с экрана убраны.
    let bucketsHtml = "";
    let bucketNote = "";
    if (buckets && buckets.length > 0) {
      bucketNote = `<div class="wBucketNote">Корзины текущего куска — слова разложены по букве, сейчас идёт разбор корзин.</div>`;
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
    box.textContent = step.desc;
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
    /* Журнал — это все уже сделанные шаги: при переходе назад (клик по строке, «Назад»)
       ничего не удаляется — между существующими строками просто переключаемся.
       Дописываются только шаги дальше нарисованных. Очистка журнала происходит
       лишь при новой трассировке (пути запуска сбрасывают logStep в -1 вместе с innerHTML). */
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
      div.dataset.step = st.index;             // клик по строке → показать этот шаг
      div.title = "Показать этот шаг";
      if (animate) div.addEventListener("animationend", () => div.classList.remove("fresh"), { once: true });
      fragment.appendChild(div);
    }
    list.appendChild(fragment);
    while (list.children.length > 800) list.removeChild(list.firstChild);
    if (toStep > state.logStep) state.logStep = toStep;
    // Подсветка строки, соответствующей текущему шагу.
    const prev = list.querySelector(".logLine.current");
    if (prev) prev.classList.remove("current");
    const cur = list.querySelector(`.logLine[data-step="${toStep}"]`);
    if (cur) cur.classList.add("current");
    if ($("wFollowLog").checked) {
      if (cur) cur.scrollIntoView({ block: "nearest" });
      else list.scrollTop = list.scrollHeight;
    }
  }

  function renderProgress(step) {
    const total = state.plan.steps.length;
    $("wProgressBar").style.width = `${((step.index + 1) / total) * 100}%`;
    $("wStepCounter").textContent = `шаг ${step.index + 1} / ${total}`;
  }

  function showResultPanel(plan) {
    if (!plan.resultFileName) return;
    const key = plan.resultFileName;
    if (state.resultShown !== key) {
      const maxCount = Math.max(...plan.counts.map((c) => c.count), 1);
      const rows = plan.counts.map((c) => `
        <tr>
          <td class="rowId">${c.rank}</td>
          <td>${escapeHtml(c.word)}</td>
          <td class="num">${c.count}</td>
          <td class="cntCell"><span class="cntBar" style="width:${Math.max(4, Math.round((c.count / maxCount) * 100))}%"></span></td>
        </tr>`).join("");
      const truncated = plan.countsTruncated
        ? `<tr class="cntTruncated"><td colspan="4">Показаны первые ${plan.counts.length} из ${plan.uniqueWords} уникальных слов — полный список в файле результата.</td></tr>`
        : "";
      $("wCountsTable").innerHTML = `
        <table class="data wCounts">
          <thead><tr><th>#</th><th>Слово</th><th>Вхождений</th><th></th></tr></thead>
          <tbody>${rows}${truncated}</tbody>
        </table>`;
      $("wResultCaption").textContent =
        `файл ${plan.resultFileName} · сортировалось слов: ${plan.wordCount} · уникальных: ${plan.uniqueWords}`;
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

  /* ---------------- замеры времени (внешне как в 3-й лабораторной) ----------------
     Поля «Максимум» и «Шаг»: одна кнопка меряет серию точек для ОБОИХ алгоритмов —
     от шага до максимума с интервалом «Шаг». Каждая точка — запись в истории.
     График — Chart.js: оси «Слов в тексте» / «Время (мс)», перетаскивание и
     масштабирование колесом, кнопка сброса масштаба. */

  function loadBench() {
    let rows = [];
    try { rows = JSON.parse(localStorage.getItem(BENCH_KEY)) || []; } catch { rows = []; }
    let nextId = 1;
    for (const r of rows) {
      if (!Number.isFinite(r.id)) r.id = nextId++;
      nextId = Math.max(nextId, r.id + 1);
      if (!r.ts) r.ts = new Date().toISOString();
      if (!Array.isArray(r.times)) r.times = [];
    }
    state.benchRows = rows;
    state.benchNextId = nextId;
    state.histSelected = new Set();
    state.chartEpochMs = 0; // после загрузки страницы показываем все сохранённые замеры
  }

  function saveBench() {
    try { localStorage.setItem(BENCH_KEY, JSON.stringify(state.benchRows)); } catch { /* нет localStorage */ }
  }

  function titleOfAlgo(id) {
    const m = state.methods.find((m) => m.id === id);
    return m ? m.title : id;
  }

  /* Последний замер для каждой точки (объём + алгоритм) — они идут в таблицу и график.
     «Очистить график» ставит chartEpochMs: в обычном режиме показываются только замеры,
     сделанные позже этого момента. Выбранные в истории точки (benchFilter) показываются
     независимо от очистки. */
  function benchRowsForView() {
    const src = state.benchFilter
      ? state.benchRows.filter((r) => state.benchFilter.has(r.id))
      : state.benchRows.filter((r) => !state.chartEpochMs || Date.parse(r.ts) >= state.chartEpochMs);
    const latest = new Map();
    for (const r of src) {
      const key = `${r.wordCount}|${r.algorithm}`;
      const cur = latest.get(key);
      if (!cur || r.ts > cur.ts) latest.set(key, r);
    }
    return [...latest.values()].sort((a, b) =>
      a.wordCount - b.wordCount || a.algorithm.localeCompare(b.algorithm));
  }

  function renderBenchAll() {
    renderResults();
    renderHistory();
  }

  function renderResults() {
    const rows = benchRowsForView();
    $("wBenchCsvBtn").hidden = rows.length === 0;

    const note = $("wBenchFilterNote");
    if (state.benchFilter) {
      note.textContent = `На графике и в таблице — выбранные в истории точки (${rows.length}). ` +
        "«Очистить график» возвращает все замеры.";
      note.hidden = false;
    } else {
      note.hidden = true;
    }

    if (rows.length === 0) {
      $("wBenchTable").innerHTML = state.benchFilter
        ? '<p class="hint">Среди выбранных точек нет замеров.</p>'
        : '<p class="hint">Пока нет замеров. Задай максимальный объём и шаг — и нажми «Замерить серию».</p>';
    } else {
      // Таблица как в 3-й лабораторной: одна строка на объём текста, оба алгоритма рядом.
      const byLen = new Map();
      for (const r of rows) {
        const e = byLen.get(r.wordCount) || {};
        e[r.algorithm] = r;
        byLen.set(r.wordCount, e);
      }
      const lengths = [...byLen.keys()].sort((a, b) => a - b);
      const head = `<tr>
        <th>Слов в тексте</th>
        <th>${escapeHtml(titleOfAlgo("quick-improved"))}, мс (сред.)</th>
        <th>${escapeHtml(titleOfAlgo("abc"))}, мс (сред.)</th>
        <th>ABC / QuickSort</th>
        <th>Прогонов на точку</th>
      </tr>`;
      const body = lengths.map((L) => {
        const e = byLen.get(L);
        const q = e["quick-improved"];
        const a = e["abc"];
        let ratio = "—";
        if (q && a && q.avgMs > 0) {
          const x = a.avgMs / q.avgMs;
          ratio = `×${x >= 10 ? x.toFixed(0) : x.toFixed(2)}`;
        }
        const runsCell = q && a && q.runs === a.runs ? q.runs : "—";
        return `<tr>
          <td class="num">${fmtNum(L)}</td>
          <td class="num">${q ? fmtMs(q.avgMs) : "—"}</td>
          <td class="num">${a ? fmtMs(a.avgMs) : "—"}</td>
          <td class="num">${ratio}</td>
          <td class="num">${runsCell}</td>
        </tr>`;
      }).join("");
      $("wBenchTable").innerHTML = `<table class="data"><thead>${head}</thead><tbody>${body}</tbody></table>`;
    }
    renderChartSafe(rows);
  }

  /* Серия замеров (как в 3-й лабораторной): для каждого объёма от «Шага» до «Максимума»
     с интервалом «Шаг» меряем ОБА алгоритма по `runs` прогонов. Точки появляются на
     графике по мере готовности. Повторное нажатие кнопки во время серии останавливает
     её после текущей точки. */
  async function runBench() {
    if (state.benchRunning) { // кнопка работает как «Остановить»
      state.benchSeriesCancel = true;
      $("wBenchStatus").textContent = "Останавливаю после текущей точки…";
      return;
    }
    const max = clampInt($("wBenchMax"), 100, 1000000, 100000);
    const step = clampInt($("wBenchStep"), 1, max, 10000);
    const runs = clampInt($("wBenchRuns"), 1, 50, 3);
    const lengths = [];
    for (let L = step; L <= max; L += step) lengths.push(L);
    if (lengths.length === 0) {
      $("wBenchStatus").textContent = "Шаг больше максимального объёма — нечего мерить.";
      return;
    }
    state.benchRunning = true;
    state.benchSeriesCancel = false;
    $("wBenchRunBtn").textContent = "■ Остановить";
    const t0 = performance.now();
    let done = 0;
    try {
      for (const L of lengths) {
        if (state.benchSeriesCancel) {
          $("wBenchStatus").textContent =
            `Остановлено: выполнено точек ${done} из ${lengths.length}. Замеры сохранены в историю.`;
          return; // finally сбросит состояние кнопки
        }
        $("wBenchStatus").textContent =
          `Точка ${done + 1} из ${lengths.length}: ${fmtNum(L)} слов · ${runs} прогонов на алгоритм…`;
        for (const algo of ["quick-improved", "abc"]) {
          if (state.benchSeriesCancel) break;
          const row = await post("/api/word-sort/bench", { algorithm: algo, runs, wordCount: L });
          addBenchRow(row);
        }
        done++;
        renderBenchAll(); // обе точки объёма готовы — обновляем график и таблицу
      }
      $("wBenchStatus").textContent =
        `Готово за ${((performance.now() - t0) / 1000).toFixed(1)} с: ${done} точек × 2 алгоритма · ` +
        `${runs} прогонов на точку. Все замеры сохранены в историю.`;
    } catch (err) {
      $("wBenchStatus").textContent =
        "Ошибка: " + err.message + " (выполнено точек: " + done + " из " + lengths.length + ")";
    } finally {
      state.benchRunning = false;
      state.benchSeriesCancel = false;
      $("wBenchRunBtn").textContent = "▶ Замерить серию";
    }
  }

  function addBenchRow(row) {
    row.id = state.benchNextId++;
    row.ts = new Date().toISOString();
    if (!Array.isArray(row.times)) row.times = [];
    state.benchRows.push(row);
    saveBench(); // отрисовка — когда готовы обе точки очередного объёма (в runBench)
  }

  function renderHistory() {
    const wrap = $("wBenchHistory");
    const rows = [...state.benchRows].sort((a, b) => (b.ts || "").localeCompare(a.ts || ""));
    if (rows.length === 0) {
      wrap.innerHTML = '<p class="hint">История пустая — сделай замер на вкладке «Замер».</p>';
      return;
    }
    const head = `<tr><th></th><th>Дата</th><th>Слов</th><th>Алгоритм</th><th>Прогонов</th>
      <th>Среднее, мс</th><th>Мин, мс</th><th>Макс, мс</th><th>Сравнений</th><th>Перестановок</th></tr>`;
    const body = rows.map((r) => {
      const sel = state.histSelected.has(r.id);
      return `<tr class="${sel ? "selected" : ""}" data-id="${r.id}">
        <td class="rowId">${sel ? "☑" : "☐"}</td>
        <td>${fmtDate(r.ts)}</td>
        <td class="num">${fmtNum(r.wordCount)}</td>
        <td>${escapeHtml(r.algorithmTitle)}</td>
        <td class="num">${r.runs}</td>
        <td class="num">${fmtMs(r.avgMs)}</td>
        <td class="num">${fmtMs(r.minMs)}</td>
        <td class="num">${fmtMs(r.maxMs)}</td>
        <td class="num">${fmtNum(r.comparisons)}</td>
        <td class="num">${fmtNum(r.moves)}</td>
      </tr>`;
    }).join("");
    wrap.innerHTML = `<table class="data"><thead>${head}</thead><tbody>${body}</tbody></table>`;
    wrap.querySelectorAll("tr[data-id]").forEach((tr) =>
      tr.addEventListener("click", () => {
        const id = Number(tr.dataset.id);
        if (state.histSelected.has(id)) state.histSelected.delete(id);
        else state.histSelected.add(id);
        renderHistory();
      }));
  }

  function benchTab(name) {
    document.querySelectorAll("#wBenchTabs .benchTab").forEach((b) =>
      b.classList.toggle("active", b.dataset.btab === name));
    $("wBenchRunTab").hidden = name !== "run";
    $("wBenchHistoryTab").hidden = name !== "history";
  }

  function fmtMs(v) {
    return v >= 100 ? v.toFixed(0) : v >= 10 ? v.toFixed(1) : v.toFixed(2);
  }

  function fmtNum(n) {
    return Number(n).toLocaleString("ru-RU");
  }

  function fmtDate(ts) {
    return new Date(ts).toLocaleString("ru-RU");
  }

  /* График всегда нарисован: либо точки замеров, либо пустая рамка с подсказкой.
     Ошибку построения показываем прямо в блоке, а не молча в консоли. */
  function renderChartSafe(rows) {
    try {
      renderChart(rows);
    } catch (err) {
      const canvas = $("wChartCanvas");
      const empty = $("wChartEmpty");
      if (benchChart) { benchChart.destroy(); benchChart = null; }
      canvas.hidden = true;
      empty.hidden = false;
      empty.textContent = "Не удалось построить график: " + ((err && err.message) || String(err));
    }
  }

  /* График Chart.js (внешне как в 3-й лабораторной): оси «Слов в тексте» и
     «Время (мс)», точки с линиями на каждый алгоритм, перетаскивание мышью и
     масштабирование колесом (chartjs-plugin-zoom), кнопка сброса масштаба. */
  function renderChart(inputRows) {
    if (typeof Chart === "undefined") {
      throw new Error("библиотека Chart.js не загрузилась (подключается с CDN — нужен интернет)");
    }
    const canvas = $("wChartCanvas");
    const empty = $("wChartEmpty");
    // Чистим данные: в график не должны попасть NaN/пустые значения.
    const rows = (inputRows || [])
      .map((r) => ({ ...r, wordCount: Number(r.wordCount), avgMs: Number(r.avgMs) }))
      .filter((r) => Number.isFinite(r.wordCount) && Number.isFinite(r.avgMs) && r.wordCount > 0 && r.avgMs >= 0);

    if (rows.length === 0) {
      if (benchChart) { benchChart.destroy(); benchChart = null; }
      canvas.hidden = true;
      empty.hidden = false;
      empty.textContent = state.benchFilter
        ? "Среди выбранных точек нет замеров."
        : (state.chartEpochMs
          ? "График очищен. Сделай новый замер — точки появятся здесь. Старые можно вернуть через «Построить график для выбранных»."
          : "Сделай замер — точка появится на графике.");
      return;
    }

    empty.hidden = true;
    canvas.hidden = false;

    const colors = { "quick-improved": "#4f46e5", "abc": "#059669" };
    const lengths = [...new Set(rows.map((r) => r.wordCount))].sort((a, b) => a - b);
    const algos = [...new Set(rows.map((r) => r.algorithm))];
    const datasets = algos.map((algo) => ({
      label: titleOfAlgo(algo),
      data: lengths
        .filter((L) => rows.some((r) => r.algorithm === algo && r.wordCount === L))
        .map((L) => {
          const row = rows.find((r) => r.algorithm === algo && r.wordCount === L);
          return { x: L, y: row.avgMs };
        }),
      showLine: true,
      tension: 0,
      borderColor: colors[algo] || "#6b7280",
      backgroundColor: colors[algo] || "#6b7280",
      pointRadius: 3,
      pointHoverRadius: 6,
      borderWidth: 2,
    }));

    if (benchChart) benchChart.destroy();
    benchChart = new Chart(canvas.getContext("2d"), {
      type: "scatter",
      data: { datasets },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        aspectRatio: 1.6,
        animation: { duration: 200 },
        interaction: { mode: "nearest", intersect: false },
        scales: {
          x: {
            type: "linear",
            beginAtZero: true,
            title: { display: true, text: "Слов в тексте" },
            grid: { color: "#e5e7eb" },
            ticks: { callback: (v) => Number(v).toLocaleString("ru-RU") },
          },
          y: {
            type: "linear",
            beginAtZero: true,
            title: { display: true, text: "Время (мс)" },
            grid: { color: "#e5e7eb" },
            ticks: {
              callback: (v) => Number(v).toLocaleString("ru-RU", { maximumFractionDigits: 2 }),
            },
          },
        },
        plugins: {
          legend: {
            position: "bottom",
            align: "end",
            labels: { usePointStyle: true, boxWidth: 10, padding: 16 },
          },
          tooltip: {
            callbacks: {
              title: (ctxs) => "Слов: " + Number(ctxs[0].parsed.x).toLocaleString("ru-RU"),
              label: (ctx) => `${ctx.dataset.label}: ${fmtMs(ctx.parsed.y)} мс`,
            },
          },
          zoom: {
            pan: { enabled: true, mode: "xy" },
            zoom: { wheel: { enabled: true }, pinch: { enabled: true }, mode: "xy" },
            limits: {
              x: { min: "original", max: "original" },
              y: { min: "original", max: "original" },
            },
          },
        },
      },
    });
  }

  function downloadCsv() {
    const rows = benchRowsForView();
    const head = "слов;алгоритм;прогонов;среднее_мс;мин_мс;макс_мс;сравнений;перестановок;дата";
    const lines = rows.map((r) =>
      [r.wordCount, r.algorithmTitle, r.runs, r.avgMs.toFixed(3), r.minMs.toFixed(3),
       r.maxMs.toFixed(3), r.comparisons, r.moves, fmtDate(r.ts)].join(";"));
    const blob = new Blob(["\uFEFF" + [head, ...lines].join("\n")], { type: "text/csv;charset=utf-8" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "lab4_word_sort_bench.csv";
    a.click();
    URL.revokeObjectURL(a.href);
  }

  /* ---------------- служебное ---------------- */

  function clampInt(input, min, max, fallback) {
    const v = Number(input.value);
    if (!Number.isFinite(v)) return fallback;
    return Math.min(max, Math.max(min, Math.round(v)));
  }

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
    $("wRandomBtn").addEventListener("click", onRandom);

    $("wAscBtn").addEventListener("click", () => setDirection(true));
    $("wDescBtn").addEventListener("click", () => setDirection(false));

    $("wDelayRange").addEventListener("input", (e) => {
      state.delayMs = Number(e.target.value);
      $("wDelayVal").textContent = String(state.delayMs);
      if (state.playing) scheduleTick();
    });

    $("wLogDownBtn").addEventListener("click", () => {
      const list = $("wLogList");
      list.scrollTop = list.scrollHeight;
    });

    // Журнал кликабелен: клик по строке показывает соответствующий шаг.
    // Если прыгнули в середину — следующие шаги просто продолжаются с этого места,
    // журнал дописывается дальше, ничего не перестраивается с нуля.
    $("wLogList").addEventListener("click", (e) => {
      const line = e.target.closest(".logLine");
      if (!line || !state.plan) return;
      const i = Number(line.dataset.step);
      if (!Number.isFinite(i)) return;
      stopPlayback();
      applyStep(i, { animateLog: false });
    });

    // Вкладки блока «Замеры времени»: Замер | История.
    document.querySelectorAll("#wBenchTabs .benchTab").forEach((b) =>
      b.addEventListener("click", () => benchTab(b.dataset.btab)));

    // Замер серии: от «Шага» до «Максимума», оба алгоритма; во время серии кнопка = «Остановить».
    $("wBenchRunBtn").addEventListener("click", runBench);
    $("wBenchClearChartBtn").addEventListener("click", () => {
      state.benchFilter = null;
      // Полная очистка: график пуст; дальше на нём появятся только новые замеры.
      // Старые возвращаются через «Построить график для выбранных».
      state.chartEpochMs = state.benchRows.length > 0 ? Date.now() + 1 : 0;
      renderBenchAll();
      $("wBenchStatus").textContent =
        "График полностью очищен. Новые замеры появятся на нём заново; старые можно вернуть через «Построить график для выбранных».";
    });
    $("wChartResetZoomBtn").addEventListener("click", () => {
      if (benchChart && benchChart.resetZoom) benchChart.resetZoom();
    });
    $("wBenchCsvBtn").addEventListener("click", downloadCsv);

    // История: выбор строк, график для выбранных, удаление, обновление.
    $("wHistChartBtn").addEventListener("click", () => {
      if (state.histSelected.size === 0) {
        $("wBenchStatus").textContent = "Сначала выбери строки в списке истории (клик по строке).";
        benchTab("run");
        return;
      }
      state.benchFilter = new Set(state.histSelected);
      benchTab("run");
      renderBenchAll();
    });
    $("wHistDeleteAllBtn").addEventListener("click", () => {
      if (state.benchRows.length === 0) {
        $("wBenchStatus").textContent = "История и так пустая.";
        benchTab("history");
        return;
      }
      if (!confirm(`Удалить все замеры (${state.benchRows.length} шт.)? Действие необратимо.`)) return;
      state.benchRows = [];
      state.benchNextId = 1;
      state.histSelected.clear();
      state.benchFilter = null;
      state.chartEpochMs = 0;
      saveBench();
      renderBenchAll();
      $("wBenchStatus").textContent = "Все замеры удалены.";
      benchTab("history");
    });
    $("wHistDeleteBtn").addEventListener("click", () => {
      if (state.histSelected.size === 0) {
        $("wBenchStatus").textContent = "Сначала выбери строки в списке истории (клик по строке).";
        benchTab("run");
        return;
      }
      if (!confirm(`Удалить выбранные замеры (${state.histSelected.size})?`)) return;
      state.benchRows = state.benchRows.filter((r) => !state.histSelected.has(r.id));
      if (state.benchFilter) {
        state.benchFilter = new Set([...state.benchFilter].filter((id) =>
          state.benchRows.some((r) => r.id === id)));
        if (state.benchFilter.size === 0) state.benchFilter = null;
      }
      state.histSelected = new Set();
      saveBench();
      renderBenchAll();
    });
    $("wHistRefreshBtn").addEventListener("click", () => {
      loadBench();
      renderBenchAll();
    });
  }

  function setDirection(ascending) {
    if (ascending === state.ascending) return;
    stashSession();
    state.ascending = ascending;
    $("wAscBtn").classList.toggle("active", ascending);
    $("wDescBtn").classList.toggle("active", !ascending);
    if (state.random) {
      loadRandom(state.random.wordCount, state.random.seed); // тот же текст, другой порядок
    } else {
      state.sessionKey = sessionKey();
      restoreSession();
    }
  }

  init();
})();
