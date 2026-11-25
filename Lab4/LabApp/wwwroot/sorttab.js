/* Лабораторная 4, задание 1 — пошаговая визуализация внутренних сортировок массива чисел
   (SelectSort, InsertSort, MergeSort) и замеры времени. Ванильный JS, без сборки.
   Массив вводится вручную или генерируется кнопкой. Как в задании 2, у каждого сочетания
   настроек (массив, алгоритм, порядок) — своя независимая сессия: сделал несколько шагов,
   переключился на другую сортировку и вернулся — прогресс на месте. Новая сессия появляется
   только когда меняется сам массив (кнопка «Сгенерировать» или правка поля).
   Замеры времени полностью независимы от настроек демонстрации и внешне повторяют
   панель замеров задания 3. */
"use strict";

(() => {
  const $ = (id) => document.getElementById(id);

  /* ---------------- состояние ---------------- */

  const state = {
    methods: [],          // алгоритмы с сервера (с полем group для группировки карточек)
    method: "select",
    ascending: true,
    valuesKey: "",        // ключ массива (нормализованный список чисел через запятую)
    sessions: new Map(),  // ключ настроек -> {plan, cursor, resultShown}
    sessionKey: "",
    plan: null,           // план текущей сессии (ответ /api/array-sort/run)
    valuesById: new Map(),// id числа -> само число
    cursor: -1,           // текущий шаг
    resultShown: false,   // показана ли панель результата для текущего прогона
    logStep: -1,          // до какого шага нарисован журнал
    playing: false,
    timer: null,
    delayMs: 300,
    benchRuns: [],        // замеры: один «Замерить» = одна запись со всеми точками
    benchNextId: 1,
    benchFilter: null,    // Set id замеров из истории — показывать на графике только их
    histSelected: new Set(),
    benchRunning: false,      // идёт замер серии точек
    benchSeriesCancel: false, // кнопка «Остановить» во время замера
    chartCleared: false,      // «Очистить график»: пустой график до следующего замера
  };

  const BENCH_KEY = "lab4.arrayBench.v1"; // один замер = одна запись со всеми точками
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
    if (!$("aArrayInput")) return; // вкладки задания 1 нет (не должно случаться)
    bindControls();

    let methods;
    try {
      methods = await api("/api/array-sort/methods");
    } catch (err) {
      showError("Не удалось связаться с сервером: " + err.message);
      return;
    }
    state.methods = methods;

    renderMethods();
    updateLegend();
    fillBenchAlgo();
    loadBench();
    renderBenchAll(); // сохранённые замеры показываем сразу при открытии страницы

    // Начальный массив: генерируем на сервере с фиксированным сидом, чтобы вкладка
    // сразу была готова к запуску (как готовые тексты в задании 3).
    try {
      const rnd = await post("/api/array-sort/random", { count: 16, seed: 2026 });
      $("aArrayInput").value = rnd.values.join(" ");
    } catch { /* начнём с пустого поля — пользователь введёт числа сам */ }
    onArrayInput(); // заполняем значения по умолчанию и сессию
  }

  /* ---------------- массив: ввод и генерация ---------------- */

  /* Текст поля -> нормализованный список чисел (для ключа сессии). Ошибки не показываем:
     их при запуске вернёт сервер с понятным описанием. */
  function readValues() {
    return $("aArrayInput").value
      .split(/[\s,;\t\r\n]+/)
      .filter((t) => t.length > 0);
  }

  /* Массив определяет сессию: правка поля или «Сгенерировать» дают новый массив —
     это новая сессия с нуля; смена алгоритма/порядка сессию не сбрасывает. */
  function onArrayInput() {
    state.valuesKey = readValues().join(",");
    state.sessionKey = sessionKey();
    restoreSession();
  }

  async function onRandom() {
    const count = clampInt($("aRandomCount"), 1, 100, 16);
    const btn = $("aRandomBtn");
    btn.disabled = true;
    btn.textContent = "Генерирую…";
    hideError();
    try {
      const rnd = await post("/api/array-sort/random", { count });
      stopPlayback();
      $("aArrayInput").value = rnd.values.join(" ");
      onArrayInput(); // новый массив — новая сессия с нуля
    } catch (err) {
      showError(err.message);
    } finally {
      btn.disabled = false;
      btn.textContent = "Сгенерировать";
    }
  }

  /* ---------------- сессии настроек ---------------- */

  function sessionKey() {
    return JSON.stringify({
      v: state.valuesKey,
      m: state.method,
      a: state.ascending,
    });
  }

  function syncSession() {
    if (!state.plan) return;
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
    const s = state.sessions.get(state.sessionKey);
    state.plan = s ? s.plan : null;
    state.cursor = s ? s.cursor : -1;
    state.resultShown = s ? s.resultShown : false;
    state.logStep = -1;
    $("aLogList").innerHTML = "";
    state.valuesById = new Map(
      state.plan ? state.plan.elements.map((w) => [w.id, w.value]) : []);
    renderSessionView({ animateLog: false, record: false });
    updateButtons();
    updatePlayBtn();
  }

  /* Смена алгоритма или порядка: переиспользуем план из сессии этого сочетания настроек —
     трассировка не пересчитывается, прогресс каждого сочетания сохраняется. */
  function onSettingsChanged() {
    stashSession();
    state.sessionKey = sessionKey();
    hideError();
    restoreSession();
  }

  /* ---------------- алгоритмы: карточки с группами ---------------- */

  function renderMethods() {
    const list = $("aMethodList");
    if (!state.methods.some((m) => m.id === state.method) && state.methods.length > 0) {
      state.method = state.methods[0].id; // сохранённый метод удалён — берём первый
    }
    // Карточки группируются по классификации из задания: квадратичные, затем усовершенствованные.
    const groups = [];
    for (const m of state.methods) {
      let g = groups.find((x) => x.name === m.group);
      if (!g) { g = { name: m.group, items: [] }; groups.push(g); }
      g.items.push(m);
    }
    list.innerHTML = groups.map((g) => `
      <div class="methodGroup">${escapeHtml(g.name)}</div>` +
      g.items.map((m) => `
      <div class="methodCard ${m.id === state.method ? "active" : ""}" data-method="${escapeHtml(m.id)}">
        <div class="mTitle">
          <span>${escapeHtml(m.title)}</span>
          <span class="memBadge">${escapeHtml(m.badge)}</span>
        </div>
        <div class="mInfo">${escapeHtml(m.shortInfo)}</div>
        <div class="mInfo">в памяти: ${m.memorySlots} знач. · счётчик: ${escapeHtml(m.passesLabel)}</div>
      </div>`).join("")).join("");
    list.querySelectorAll(".methodCard").forEach((card) => {
      card.addEventListener("click", () => {
        const id = card.dataset.method;
        if (id === state.method) return;
        state.method = id;
        list.querySelectorAll(".methodCard").forEach((c) => c.classList.toggle("active", c === card));
        updateLegend();
        onSettingsChanged(); // stash внутри: план прежнего сочетания сохранится под своим ключом
      });
    });
  }

  /* Легенда показывает только то, что используется выбранным алгоритмом:
     «особое число» — разметка текущего минимума в SelectSort, «в буфере слияния» — MergeSort. */
  const MARK_ALGOS = new Set(["select"]);
  const BUF_ALGOS = new Set(["merge"]);

  function updateLegend() {
    $("aLegendMark").hidden = !MARK_ALGOS.has(state.method);
    $("aLegendBuf").hidden = !BUF_ALGOS.has(state.method);
  }

  /* ---------------- запуск и воспроизведение ---------------- */

  async function ensurePlan() {
    if (state.plan) return true;
    const keyAtStart = state.sessionKey;
    hideError();
    $("aDescBox").textContent = "Считаем шаги сортировки…";
    try {
      const plan = await post("/api/array-sort/run", {
        values: $("aArrayInput").value,
        method: state.method,
        ascending: state.ascending,
      });
      if (state.sessionKey !== keyAtStart) return false; // настройки сменили, пока считали
      state.plan = plan;
      state.cursor = -1;
      state.logStep = -1;
      state.resultShown = false;
      state.valuesById = new Map(plan.elements.map((w) => [w.id, w.value]));
      syncSession();
      return true;
    } catch (err) {
      if (state.sessionKey === keyAtStart) {
        showError(err.message);
        $("aDescBox").textContent = "Настрой параметры и нажми «Запустить».";
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
      state.resultShown = false;
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
    const btn = $("aPlayBtn");
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
      state.resultShown = false;
      syncSession();
    }
    renderSessionView({ animateLog: false, record: false });
    updateButtons();
    updatePlayBtn();
  }

  function updateButtons() {
    const has = !!state.plan;
    $("aBackBtn").disabled = !has || state.cursor < 0;
    $("aStepBtn").disabled = false;
    $("aResetBtn").disabled = !has;
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
    const badge = $("aPhaseBadge");

    if (!state.plan || state.cursor < 0) {
      state.logStep = -1;
      renderScene();
      badge.hidden = true;
      $("aMemoryBox").hidden = true;
      $("aLegend").hidden = true;
      $("aCounterRow").hidden = true;
      $("aResultPanel").hidden = true;
      $("aProgressBar").style.width = "0";
      $("aStepCounter").textContent = "шаг 0 / 0";
      $("aDescBox").classList.remove("done");
      $("aDescBox").textContent = state.plan
        ? "Готово к запуску: нажми «Запустить» или «Шаг»."
        : "Введи числа или сгенерируй случайный массив и нажми «Запустить» — покажу сортировку " +
          "по шагам: что сравниваем, куда переставляем и что пишем в буфер слияния.";
      $("aLogList").innerHTML = '<div class="logEmpty">Журнал появится после запуска сортировки.</div>';
      return;
    }

    const step = state.plan.steps[state.cursor];
    renderScene();
    renderMemory(step);
    renderDesc(step);
    renderCounters(step);
    syncLog(state.cursor, animateLog, record);
    renderProgress(step);
    $("aLegend").hidden = false;

    if (step.kind === "Done") {
      updatePlayBtn();
      showResultPanel();
    } else {
      $("aResultPanel").hidden = true;
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
    const scene = $("aScene");
    if (!state.plan) {
      scene.innerHTML = '<div class="emptyStage">Введи числа и нажми «Запустить».</div>';
      return;
    }
    const elements = state.plan.elements;
    const arr = (state.cursor >= 0 ? snapshotAt("arrayIds", state.cursor) : null) ||
      elements.map((w) => w.id);
    const placed = new Set((state.cursor >= 0 ? snapshotAt("placed", state.cursor) : []) || []);
    const range = new Set((state.cursor >= 0 ? snapshotAt("rangeIds", state.cursor) : []) || []);
    const buffer = state.cursor >= 0 ? snapshotAt("bufferIds", state.cursor) : null;
    // Серии текущей пары слияния (MergeSort): снимок последнего шага с разметкой;
    // пустой список — разметка сброшена, null — алгоритм серии не размечает.
    const seriesA = state.cursor >= 0 ? snapshotAt("seriesA", state.cursor) : null;
    const seriesB = state.cursor >= 0 ? snapshotAt("seriesB", state.cursor) : null;
    const step = state.plan.steps[state.cursor];
    const hi = new Set(step && step.highlights ? step.highlights : []);
    const mem = new Set(step && step.memoryIds ? step.memoryIds : []);
    const isMove = step && (step.kind === "Swap" || step.kind === "Shift" || step.kind === "Write" ||
      step.kind === "CopyBack");

    const chipHtml = (id, extra = []) => {
      const cls = ["chip", "wchip", ...extra];
      if (placed.has(id)) cls.push("wPlaced");
      if (range.has(id)) cls.push("wRange");
      // Особое число (текущий минимум в SelectSort) — фиолетовое и только фиолетовое:
      // оно всегда и «в памяти», но жёлтая раскраска памяти перекрыла бы фиолетовую
      // (в CSS .wMem идёт после .wPivot), поэтому для особого числа wMem не добавляем.
      const isMark = step && id === step.markId;
      if (isMark) cls.push("wPivot");
      else if (hi.has(id)) cls.push(isMove ? "wSwap" : "wCmp");
      if (mem.has(id) && !isMark) cls.push("wMem");
      return `<span class="${cls.join(" ")}" title="${escapeHtml(titleOf(id))}">${escapeHtml(labelOf(id))}</span>`;
    };

    /* Чипы массива. Если MergeSort разметил серии — чипы каждой серии обводятся в свою
       группу с подписью (как «Серия N» на лентах внешней сортировки): чипы серии стоят
       в массиве подряд, поэтому разрезаем ряд на сегменты-группы. */
    let chips;
    if (seriesA && seriesA.length > 0) {
      const setA = new Set(seriesA);
      const setB = new Set(seriesB || []);
      const hasPair = (seriesB || []).length > 0;
      const labelA = hasPair ? "серия 1 · левая" : "серия без пары";
      const labelB = "серия 2 · правая";
      const parts = [];
      let i = 0;
      while (i < arr.length) {
        const inA = setA.has(arr[i]);
        const inB = !inA && setB.has(arr[i]);
        if (!inA && !inB) { parts.push(chipHtml(arr[i])); i++; continue; }
        const set = inA ? setA : setB;
        const label = inA ? labelA : labelB;
        const groupChips = [];
        let j = i;
        while (j < arr.length && set.has(arr[j])) { groupChips.push(chipHtml(arr[j])); j++; }
        parts.push(`<span class="runGroup aRunGroup"><span class="runNo">${label}</span>${groupChips.join("")}</span>`);
        i = j;
      }
      chips = parts.join("");
    } else {
      chips = arr.map((id) => chipHtml(id)).join("");
    }

    // Буфер слияния: снимок на текущем шаге; null — алгоритм буфер не использует,
    // пустой список — буфер показан пустым (например в начале прохода после CopyBack).
    let bufferHtml = "";
    if (buffer) {
      bufferHtml = `
      <div class="aArrayWrap">
        <div class="wArrayTitle">Буфер слияния</div>
        <div class="chips">${buffer.length === 0
          ? '<span class="ghost">пусто</span>'
          : buffer.map((id) => chipHtml(id, ["aBuf"])).join("")}</div>
      </div>`;
    }

    scene.innerHTML = `
      <div class="aArrayWrap">
        <div class="wArrayTitle">Массив</div>
        <div class="chips">${chips}</div>
      </div>
      ${bufferHtml}`;
  }

  function labelOf(id) {
    const v = state.valuesById.get(id);
    return v === undefined ? `№${id}` : String(v);
  }

  function titleOf(id) {
    const idx = state.plan.elements.findIndex((w) => w.id === id);
    return `№${idx + 1}: ${state.valuesById.get(id) ?? "?"}`;
  }

  function renderMemory(step) {
    const box = $("aMemoryBox");
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
    const box = $("aDescBox");
    box.classList.toggle("done", step.kind === "Done");
    const badge = $("aPhaseBadge");
    badge.hidden = false;
    badge.textContent = step.passTitle ? `${step.phase} · ${step.passTitle}` : step.phase;
    box.textContent = step.desc;
  }

  function renderCounters(step) {
    const row = $("aCounterRow");
    row.hidden = false;
    const passesLabel = state.plan.passesLabel || "проходов";
    const html = `
      <div class="stat" id="aStatC"><div class="num">0</div><div class="lbl">⚖ сравнений</div></div>
      <div class="stat" id="aStatM"><div class="num">0</div><div class="lbl">↔ перестановок</div></div>
      <div class="stat" id="aStatP"><div class="num">0</div><div class="lbl">↻ ${escapeHtml(passesLabel)}</div></div>`;
    if (row.dataset.key !== state.plan.passesLabel) {
      row.innerHTML = html;
      row.dataset.key = state.plan.passesLabel;
      row.querySelectorAll(".stat").forEach((s) =>
        s.addEventListener("animationend", () => s.classList.remove("pulse")));
    }
    setStat("aStatC", step.comparisons);
    setStat("aStatM", step.moves);
    setStat("aStatP", step.passes);
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

  function syncLog(toStep, animate = true, record = true) {
    const list = $("aLogList");
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
    if ($("aFollowLog").checked) {
      if (cur) cur.scrollIntoView({ block: "nearest" });
      else list.scrollTop = list.scrollHeight;
    }
  }

  function renderProgress(step) {
    const total = state.plan.steps.length;
    $("aProgressBar").style.width = `${((step.index + 1) / total) * 100}%`;
    $("aStepCounter").textContent = `шаг ${step.index + 1} / ${total}`;
  }

  function showResultPanel() {
    if (!state.resultShown) {
      const sortedIds = snapshotAt("arrayIds", state.plan.steps.length - 1) ||
        state.plan.elements.map((w) => w.id);
      $("aSortedChips").innerHTML = sortedIds.map((id) =>
        `<span class="chip wchip" title="${escapeHtml(titleOf(id))}">${escapeHtml(labelOf(id))}</span>`).join("");
      $("aResultCaption").textContent =
        `${state.plan.methodTitle} · чисел: ${state.plan.elementCount} · ` +
        `${state.plan.ascending ? "по возрастанию" : "по убыванию"}`;
      $("aResultNote").textContent =
        "Внутренняя сортировка выполняется целиком в памяти — файл результата не создаётся. " +
        `Итог: сравнений — ${state.plan.stats.comparisons}, перестановок — ${state.plan.stats.moves}, ` +
        `${state.plan.passesLabel} — ${state.plan.stats.passes}.`;
      state.resultShown = true;
      syncSession();
    }
    $("aResultPanel").hidden = false;
  }

  /* ---------------- замеры времени (внешне как в 3-м задании) ----------------
     Один «Замерить» = ОДИН замер: выбранный алгоритм, точки от «Шага» до «Максимума»
     с интервалом «Шаг». Все точки хранятся в одной записи истории, и график у замера
     один — со всеми его точками. «Построить график для выбранных» показывает замер
     целиком. График — Chart.js: оси «Чисел в массиве» / «Время (мс)», перетаскивание
     и масштабирование колесом, кнопка сброса масштаба. */

  function loadBench() {
    let runs = [];
    try { runs = JSON.parse(localStorage.getItem(BENCH_KEY)) || []; } catch { runs = []; }
    let nextId = 1;
    for (const r of runs) {
      if (!Number.isFinite(r.id)) r.id = nextId++;
      nextId = Math.max(nextId, r.id + 1);
      if (!r.ts) r.ts = new Date().toISOString();
      if (!Array.isArray(r.points)) r.points = [];
      for (const p of r.points) {
        if (!Array.isArray(p.times)) p.times = [];
      }
    }
    state.benchRuns = runs;
    state.benchNextId = nextId;
    state.histSelected = new Set();
    state.benchFilter = null;     // на графике — последний замер (см. benchRunsForView)
    state.chartCleared = false;
  }

  function saveBench() {
    try { localStorage.setItem(BENCH_KEY, JSON.stringify(state.benchRuns)); } catch { /* нет localStorage */ }
  }

  function titleOfAlgo(id) {
    const m = state.methods.find((m) => m.id === id);
    return m ? m.title : id;
  }

  function fillBenchAlgo() {
    const sel = $("aBenchAlgo");
    sel.innerHTML = state.methods
      .map((m) => `<option value="${escapeHtml(m.id)}">${escapeHtml(m.title)}</option>`)
      .join("");
    if (state.methods.length > 0) sel.value = state.methods[0].id;
  }

  /* Что показываем на графике и в таблице: выбранные в истории замеры (benchFilter),
     пустой график после «Очистить график» или последний сделанный замер. */
  function benchRunsForView() {
    if (state.benchFilter) {
      return state.benchRuns.filter((r) => state.benchFilter.has(r.id));
    }
    if (state.chartCleared) return []; // «Очистить график»: пусто до следующего замера
    let latest = null;
    for (const r of state.benchRuns) if (!latest || r.id > latest.id) latest = r;
    return latest ? [latest] : [];
  }

  function renderBenchAll() {
    renderResults();
    renderHistory();
  }

  function renderResults() {
    const runs = benchRunsForView();
    $("aBenchCsvBtn").hidden = runs.length === 0;

    const note = $("aBenchFilterNote");
    if (state.benchFilter) {
      note.textContent = `На графике и в таблице — выбранные в истории замеры (${runs.length}). ` +
        "«Очистить график» убирает их.";
      note.hidden = false;
    } else {
      note.hidden = true;
    }

    if (runs.length === 0) {
      $("aBenchTable").innerHTML = state.benchFilter
        ? '<p class="hint">Среди выбранных замеров нет точек.</p>'
        : '<p class="hint">Пока нет замеров. Задай объём, шаг и алгоритм — и нажми «Замерить».</p>';
    } else if (runs.length === 1) {
      // Один замер: все его точки с полными данными.
      const head = `<tr><th>Чисел в массиве</th><th>Среднее, мс</th><th>Мин, мс</th><th>Макс, мс</th>
        <th>Сравнений</th><th>Перестановок</th></tr>`;
      const body = runs[0].points.map((p) => `<tr>
        <td class="num">${fmtNum(p.elementCount)}</td>
        <td class="num">${fmtMs(p.avgMs)}</td>
        <td class="num">${fmtMs(p.minMs)}</td>
        <td class="num">${fmtMs(p.maxMs)}</td>
        <td class="num">${fmtNum(p.comparisons)}</td>
        <td class="num">${fmtNum(p.moves)}</td>
      </tr>`).join("");
      $("aBenchTable").innerHTML = `<table class="data"><thead>${head}</thead><tbody>${body}</tbody></table>`;
    } else {
      // Несколько замеров: колонки алгоритмов рядом; при двух алгоритмах — ещё и отношение.
      const byLen = new Map();
      for (const r of runs) {
        for (const p of r.points) {
          const e = byLen.get(p.elementCount) || {};
          e[r.algorithm] = p;
          byLen.set(p.elementCount, e);
        }
      }
      const lengths = [...byLen.keys()].sort((a, b) => a - b);
      const algoOrder = [];
      for (const r of runs) if (!algoOrder.includes(r.algorithm)) algoOrder.push(r.algorithm);
      const head = `<tr><th>Чисел в массиве</th>` +
        algoOrder.map((a) => `<th>${escapeHtml(titleOfAlgo(a))}, мс (сред.)</th>`).join("") +
        (algoOrder.length === 2 ? `<th>${escapeHtml(shortTitle(algoOrder[1]))} ÷ ${escapeHtml(shortTitle(algoOrder[0]))}</th>` : "") +
        `</tr>`;
      const body = lengths.map((L) => {
        const e = byLen.get(L);
        const cells = algoOrder.map((a) => `<td class="num">${e[a] ? fmtMs(e[a].avgMs) : "—"}</td>`).join("");
        let ratio = "";
        if (algoOrder.length === 2) {
          ratio = "<td class=\"num\">—</td>";
          const x = e[algoOrder[0]], y = e[algoOrder[1]];
          if (x && y && x.avgMs > 0) {
            const v = y.avgMs / x.avgMs;
            ratio = `<td class="num">×${v >= 10 ? v.toFixed(0) : v.toFixed(2)}</td>`;
          }
        }
        return `<tr><td class="num">${fmtNum(L)}</td>${cells}${ratio}</tr>`;
      }).join("");
      $("aBenchTable").innerHTML = `<table class="data"><thead>${head}</thead><tbody>${body}</tbody></table>`;
    }
    renderChartSafe(runs);
  }

  function shortTitle(id) {
    const m = state.methods.find((m) => m.id === id);
    if (!m) return id;
    const base = m.title.split(" ")[0]; // «SelectSort», «InsertSort», «MergeSort»
    return base.length > 10 ? base.slice(0, 10) : base;
  }

  /* Один «Замерить» = один замер: выбранный алгоритм, точки от «Шага» до «Максимума»
     с интервалом «Шаг». Пока идёт замер, показан маленький индикатор «Замеряем время»;
     график отрисовывается ОДИН раз и сразу целиком по завершении замера (так же при
     остановке или ошибке — замер сохраняется с уже готовыми точками). Кнопка во
     время замера работает как «Остановить», «Очистить график» заблокирована. */
  async function runBench() {
    if (state.benchRunning) { // кнопка работает как «Остановить»
      state.benchSeriesCancel = true;
      $("aBenchStatus").textContent = "Останавливаю после текущей точки…";
      return;
    }
    const algo = $("aBenchAlgo").value;
    const max = clampInt($("aBenchMax"), 100, 30000, 10000);
    const step = clampInt($("aBenchStep"), 1, max, 2000);
    const runs = clampInt($("aBenchRuns"), 1, 50, 3);
    const lengths = [];
    for (let L = step; L <= max; L += step) lengths.push(L);
    if (lengths.length === 0) {
      $("aBenchStatus").textContent = "Шаг больше максимального объёма — нечего мерить.";
      return;
    }
    if (lengths.length > 200) {
      $("aBenchStatus").textContent =
        `Слишком много точек (${lengths.length}). Максимум — 200: увеличь шаг или уменьши максимальный объём.`;
      return;
    }
    state.benchRunning = true;
    state.benchSeriesCancel = false;
    state.benchFilter = null;    // по завершении график переключится на новый замер
    state.chartCleared = false;
    $("aBenchRunBtn").textContent = "■ Остановить";
    $("aBenchClearChartBtn").disabled = true;  // во время замера график не очищается
    $("aHistDeleteBtn").disabled = true;
    $("aHistDeleteAllBtn").disabled = true;
    $("aBenchSpin").hidden = false;            // маленький индикатор: время ещё замеряется
    const t0 = performance.now();
    let done = 0;
    let bench = null;
    let errText = null;
    try {
      bench = {
        ts: new Date().toISOString(),
        algorithm: algo,
        algorithmTitle: titleOfAlgo(algo),
        runs,
        maxElements: max,
        step,
        points: [],
      };
      $("aBenchStatus").textContent =
        `Замер «${bench.algorithmTitle}»: ${lengths.length} точек (от ${fmtNum(step)} до ${fmtNum(max)}, шаг ${fmtNum(step)}) · ${runs} прогонов на точку.`;
      for (const L of lengths) {
        if (state.benchSeriesCancel) break;
        $("aBenchStatus").textContent =
          `Точка ${done + 1} из ${lengths.length}: ${fmtNum(L)} чисел · ${runs} прогонов…`;
        const row = await post("/api/array-sort/bench", { algorithm: algo, runs, elementCount: L });
        bench.points.push({
          elementCount: L,
          avgMs: row.avgMs,
          minMs: row.minMs,
          maxMs: row.maxMs,
          comparisons: row.comparisons,
          moves: row.moves,
          times: Array.isArray(row.times) ? row.times : [],
        });
        done++;
      }
    } catch (err) {
      errText = "Ошибка: " + err.message + " (выполнено точек: " + done + " из " + lengths.length + ")";
    }
    // Замер целиком готов (или остановлен/прерван ошибкой) — записываем в историю
    // и рисуем весь график сразу, всеми точками.
    if (bench && bench.points.length > 0) {
      bench.id = state.benchNextId++;
      state.benchRuns.push(bench);
      saveBench();
      renderBenchAll();
    }
    const sec = ((performance.now() - t0) / 1000).toFixed(1);
    if (errText) {
      $("aBenchStatus").textContent = errText;
    } else if (!bench || bench.points.length === 0) {
      $("aBenchStatus").textContent = "Остановлено до первой точки — замер не сохранён.";
    } else if (state.benchSeriesCancel) {
      $("aBenchStatus").textContent =
        `Остановлено за ${sec} с: в замере «${bench.algorithmTitle}» ${done} точек из ` +
        `${lengths.length}. Замер с ними сохранён в историю.`;
    } else {
      $("aBenchStatus").textContent =
        `Готово за ${sec} с: замер «${bench.algorithmTitle}» — ${done} точек (от ${fmtNum(step)} ` +
        `до ${fmtNum(max)}, шаг ${fmtNum(step)}) · ${runs} прогонов на точку. Замер записан в историю.`;
    }
    state.benchRunning = false;
    state.benchSeriesCancel = false;
    $("aBenchRunBtn").textContent = "▶ Замерить";
    $("aBenchClearChartBtn").disabled = false;
    $("aHistDeleteBtn").disabled = false;
    $("aHistDeleteAllBtn").disabled = false;
    $("aBenchSpin").hidden = true;
  }

  function renderHistory() {
    const wrap = $("aBenchHistory");
    const runs = [...state.benchRuns].sort((a, b) => (b.ts || "").localeCompare(a.ts || ""));
    if (runs.length === 0) {
      wrap.innerHTML = '<p class="hint">История пустая — сделай замер на вкладке «Замер».</p>';
      return;
    }
    const head = `<tr><th></th><th>Дата</th><th>Алгоритм</th><th>Точек</th><th>Объём (шаг)</th>
      <th>Прогонов на точку</th><th>Среднее по точкам, мс</th></tr>`;
    const body = runs.map((r) => {
      const sel = state.histSelected.has(r.id);
      const avg = r.points.length
        ? r.points.reduce((s, p) => s + p.avgMs, 0) / r.points.length
        : 0;
      return `<tr class="${sel ? "selected" : ""}" data-id="${r.id}">
        <td class="rowId">${sel ? "☑" : "☐"}</td>
        <td>${fmtDate(r.ts)}</td>
        <td>${escapeHtml(r.algorithmTitle)}</td>
        <td class="num">${r.points.length}</td>
        <td class="num">${fmtNum(r.maxElements)} (шаг ${fmtNum(r.step)})</td>
        <td class="num">${r.runs}</td>
        <td class="num">${fmtMs(avg)}</td>
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
    document.querySelectorAll("#aBenchTabs .benchTab").forEach((b) =>
      b.classList.toggle("active", b.dataset.btab === name));
    $("aBenchRunTab").hidden = name !== "run";
    $("aBenchHistoryTab").hidden = name !== "history";
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

  /* График всегда нарисован: либо точки показанных замеров, либо пустая рамка с подсказкой.
     Ошибку построения показываем прямо в блоке, а не молча в консоли. */
  function renderChartSafe(inputRuns) {
    try {
      renderChart(inputRuns);
    } catch (err) {
      const canvas = $("aChartCanvas");
      const empty = $("aChartEmpty");
      if (benchChart) { benchChart.destroy(); benchChart = null; }
      canvas.hidden = true;
      empty.hidden = false;
      empty.textContent = "Не удалось построить график: " + ((err && err.message) || String(err));
    }
  }

  /* График Chart.js (внешне как в 3-м задании): оси «Чисел в массиве» и «Время (мс)»,
     линия с точками на каждый показанный замер, перетаскивание мышью и масштабирование
     колесом (chartjs-plugin-zoom), кнопка сброса масштаба. */
  function renderChart(inputRuns) {
    if (typeof Chart === "undefined") {
      throw new Error("библиотека Chart.js не загрузилась (подключается с CDN — нужен интернет)");
    }
    const canvas = $("aChartCanvas");
    const empty = $("aChartEmpty");
    // Чистим данные: в график не должны попасть NaN/пустые точки.
    const runs = (inputRuns || [])
      .map((r) => ({
        ...r,
        points: (r.points || [])
          .map((p) => ({ ...p, elementCount: Number(p.elementCount), avgMs: Number(p.avgMs) }))
          .filter((p) => Number.isFinite(p.elementCount) && Number.isFinite(p.avgMs) && p.elementCount > 0 && p.avgMs >= 0),
      }))
      .filter((r) => r.points.length > 0);

    if (runs.length === 0) {
      if (benchChart) { benchChart.destroy(); benchChart = null; }
      canvas.hidden = true;
      empty.hidden = false;
      empty.textContent = state.benchFilter
        ? "Среди выбранных замеров нет точек."
        : (state.chartCleared
          ? "График очищен. Сделай новый замер — на нём появятся его точки. Прежние замеры можно показать через «Построить график для выбранных»."
          : "Сделай замер — на графике появятся его точки.");
      return;
    }

    empty.hidden = true;
    canvas.hidden = false;

    // Один цвет на алгоритм; если замеров одного алгоритма несколько — различаем палитрой.
    const algoColors = { "select": "#4f46e5", "insert": "#d97706", "merge": "#059669" };
    const palette = ["#4f46e5", "#059669", "#d97706", "#dc2626", "#0ea5e9", "#7c3aed", "#0d9488"];
    const perAlgoCount = {};
    for (const r of runs) perAlgoCount[r.algorithm] = (perAlgoCount[r.algorithm] || 0) + 1;
    const perAlgoSeen = {};
    const datasets = runs.map((r) => {
      const dup = perAlgoCount[r.algorithm] > 1;
      perAlgoSeen[r.algorithm] = (perAlgoSeen[r.algorithm] || 0) + 1;
      const color = dup
        ? palette[(perAlgoSeen[r.algorithm] - 1) % palette.length]
        : (algoColors[r.algorithm] || "#6b7280");
      return {
        label: r.algorithmTitle + (dup
          ? " · " + new Date(r.ts).toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" })
          : ""),
        data: [...r.points]
          .sort((a, b) => a.elementCount - b.elementCount)
          .map((p) => ({ x: p.elementCount, y: p.avgMs })),
        showLine: true,
        tension: 0,
        borderColor: color,
        backgroundColor: color,
        pointRadius: 3,
        pointHoverRadius: 6,
        borderWidth: 2,
      };
    });

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
            title: { display: true, text: "Чисел в массиве" },
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
              title: (ctxs) => "Чисел: " + Number(ctxs[0].parsed.x).toLocaleString("ru-RU"),
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
    const runs = benchRunsForView();
    const head = "замер;дата;алгоритм;прогонов_на_точку;объём_от;шаг;чисел;среднее_мс;мин_мс;макс_мс;сравнений;перестановок";
    const lines = [];
    for (const r of runs) {
      for (const p of r.points) {
        lines.push([r.id, fmtDate(r.ts), r.algorithmTitle, r.runs, r.maxElements, r.step,
          p.elementCount, p.avgMs.toFixed(3), p.minMs.toFixed(3), p.maxMs.toFixed(3),
          p.comparisons, p.moves].join(";"));
      }
    }
    const blob = new Blob(["\uFEFF" + [head, ...lines].join("\n")], { type: "text/csv;charset=utf-8" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "lab4_array_sort_bench.csv";
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
    const box = $("aErrorBox");
    box.textContent = "⚠ " + message;
    box.hidden = false;
  }

  function hideError() {
    $("aErrorBox").hidden = true;
  }

  function bindControls() {
    $("aPlayBtn").addEventListener("click", onPlay);
    $("aStepBtn").addEventListener("click", onStep);
    $("aEndBtn").addEventListener("click", onJumpEnd);
    $("aBackBtn").addEventListener("click", onBack);
    $("aResetBtn").addEventListener("click", onReset);
    $("aRandomBtn").addEventListener("click", onRandom);

    // Редактирование массива: сессия переключается на новый ключ; у сгенерированного
    // массива после правки сид больше не соответствует содержимому.
    $("aArrayInput").addEventListener("input", onArrayInput);

    $("aAscBtn").addEventListener("click", () => setDirection(true));
    $("aDescBtn").addEventListener("click", () => setDirection(false));

    $("aDelayRange").addEventListener("input", (e) => {
      state.delayMs = Number(e.target.value);
      $("aDelayVal").textContent = String(state.delayMs);
      if (state.playing) scheduleTick();
    });

    $("aLogDownBtn").addEventListener("click", () => {
      const list = $("aLogList");
      list.scrollTop = list.scrollHeight;
    });

    // Журнал кликабелен: клик по строке показывает соответствующий шаг.
    // Если прыгнули в середину — следующие шаги просто продолжаются с этого места,
    // журнал дописывается дальше, ничего не перестраивается с нуля.
    $("aLogList").addEventListener("click", (e) => {
      const line = e.target.closest(".logLine");
      if (!line || !state.plan) return;
      const i = Number(line.dataset.step);
      if (!Number.isFinite(i)) return;
      stopPlayback();
      applyStep(i, { animateLog: false });
    });

    // Вкладки блока «Замеры времени»: Замер | История.
    document.querySelectorAll("#aBenchTabs .benchTab").forEach((b) =>
      b.addEventListener("click", () => benchTab(b.dataset.btab)));

    // Замер: выбранный алгоритм, точки от «Шага» до «Максимума»; во время замера
    // кнопка = «Остановить», а «Очистить график» и удаление заблокированы.
    $("aBenchRunBtn").addEventListener("click", runBench);
    $("aBenchClearChartBtn").addEventListener("click", () => {
      if (state.benchRunning) return; // на всякий случай: во время замера очистка запрещена
      state.benchFilter = null;
      // Полная очистка: график пуст до следующего замера; прежние замеры остаются
      // в истории и возвращаются через «Построить график для выбранных».
      state.chartCleared = true;
      renderBenchAll();
      $("aBenchStatus").textContent =
        "График полностью очищен. Новый замер появится на нём; прежние замеры — через «Построить график для выбранных».";
    });
    $("aChartResetZoomBtn").addEventListener("click", () => {
      if (benchChart && benchChart.resetZoom) benchChart.resetZoom();
    });
    $("aBenchCsvBtn").addEventListener("click", downloadCsv);

    // История: выбор замеров, график для выбранных, удаление, обновление.
    $("aHistChartBtn").addEventListener("click", () => {
      if (state.histSelected.size === 0) {
        $("aBenchStatus").textContent = "Сначала выбери замеры в списке истории (клик по строке).";
        benchTab("run");
        return;
      }
      state.benchFilter = new Set(state.histSelected);
      state.chartCleared = false;
      benchTab("run");
      renderBenchAll();
    });
    $("aHistDeleteAllBtn").addEventListener("click", () => {
      if (state.benchRunning) return;
      if (state.benchRuns.length === 0) {
        $("aBenchStatus").textContent = "История и так пустая.";
        benchTab("history");
        return;
      }
      if (!confirm(`Удалить все замеры (${state.benchRuns.length} шт.)? Действие необратимо.`)) return;
      state.benchRuns = [];
      state.benchNextId = 1;
      state.histSelected.clear();
      state.benchFilter = null;
      state.chartCleared = false;
      saveBench();
      renderBenchAll();
      $("aBenchStatus").textContent = "Все замеры удалены.";
      benchTab("history");
    });
    $("aHistDeleteBtn").addEventListener("click", () => {
      if (state.histSelected.size === 0) {
        $("aBenchStatus").textContent = "Сначала выбери замеры в списке истории (клик по строке).";
        benchTab("run");
        return;
      }
      if (!confirm(`Удалить выбранные замеры (${state.histSelected.size})?`)) return;
      state.benchRuns = state.benchRuns.filter((r) => !state.histSelected.has(r.id));
      if (state.benchFilter) {
        state.benchFilter = new Set([...state.benchFilter].filter((id) =>
          state.benchRuns.some((r) => r.id === id)));
        if (state.benchFilter.size === 0) state.benchFilter = null;
      }
      state.histSelected = new Set();
      saveBench();
      renderBenchAll();
      benchTab("history");
    });
    $("aHistRefreshBtn").addEventListener("click", () => {
      loadBench();
      renderBenchAll();
    });
  }

  function setDirection(ascending) {
    if (ascending === state.ascending) return;
    stashSession();
    state.ascending = ascending;
    $("aAscBtn").classList.toggle("active", ascending);
    $("aDescBtn").classList.toggle("active", !ascending);
    state.sessionKey = sessionKey();
    restoreSession(); // прогресс порядка «▲/▼» для этого массива — из своей сессии
  }

  init();
})();
