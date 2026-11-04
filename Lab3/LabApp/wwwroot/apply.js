// ============================================================
// 1. STACK: Killer Heuristic
// ============================================================
(function () {
  const LEVELS = 6;
  const MOVES = ["e2e4", "g1f3", "d2d4", "b1c3", "f1c4", "e7e5", "b8c6", "d2d5", "c2c4", "a2a3"];
  let killers = Array.from({ length: LEVELS }, () => [null, null]);
  let fresh = null;

  function render() {
    const el = document.getElementById("stack-visual");
    if (!el) return;
    let html = "";
    for (let i = 0; i < LEVELS; i++) {
      const [k0, k1] = killers[i];
      html += '<div class="stack-level"><span class="level-label">ply ' + i + '</span>' +
        '<span class="killer ' + (k0 ? 'filled' : '') + '">' + (k0 || '—') + '</span>' +
        '<span class="killer ' + (k1 ? 'filled' : '') + '">' + (k1 || '—') + '</span>' +
        '</div>';
    }
    el.innerHTML = html;
    if (fresh !== null) {
      const levels = el.querySelectorAll(".stack-level");
      if (levels[fresh]) {
        const killersEls = levels[fresh].querySelectorAll(".killer");
        if (killersEls[0]) killersEls[0].classList.add("fresh");
      }
      fresh = null;
    }
  }

  document.getElementById("stack-cutoff").addEventListener("click", () => {
    const level = Math.floor(Math.random() * LEVELS);
    const move = MOVES[Math.floor(Math.random() * MOVES.length)];
    killers[level][1] = killers[level][0];
    killers[level][0] = move;
    fresh = level;
    render();
    document.getElementById("stack-log").textContent =
      "Beta-cutoff на глубине " + level + " ходом " + move +
      " → killers[" + level + "] = [" + killers[level].join(", ") + "]";
  });

  document.getElementById("stack-reset").addEventListener("click", () => {
    killers = Array.from({ length: LEVELS }, () => [null, null]);
    render();
    document.getElementById("stack-log").textContent = "Сброшено";
  });

  render();
})();

// ============================================================
// 2. LIST: Sudoku 4x4 (Dancing Links)
// ============================================================
(function () {
  // . = empty
  const PUZZLE = [
    [0, 1, 0, 0],
    [0, 0, 0, 2],
    [0, 0, 4, 0],
    [0, 0, 0, 0]
  ];
  let board = JSON.parse(JSON.stringify(PUZZLE));
  let original = JSON.parse(JSON.stringify(PUZZLE));
  let steps = [];
  let stepIdx = 0;
  let iterations = 0;
  let backtracks = 0;

  function isValid(b, r, c, v) {
    for (let i = 0; i < 4; i++) if (b[r][i] === v || b[i][c] === v) return false;
    const br = Math.floor(r / 2) * 2, bc = Math.floor(c / 2) * 2;
    for (let i = 0; i < 2; i++) for (let j = 0; j < 2; j++)
      if (b[br + i][bc + j] === v) return false;
    return true;
  }

  function solve(b, r, c, path) {
    if (r === 4) return true;
    if (c === 4) return solve(b, r + 1, 0, path);
    if (b[r][c] !== 0) return solve(b, r, c + 1, path);
    for (let v = 1; v <= 4; v++) {
      if (!isValid(b, r, c, v)) {
        path.push({ type: "conflict", r, c, v });
        continue;
      }
      b[r][c] = v;
      path.push({ type: "place", r, c, v });
      if (solve(b, r, c + 1, path)) return true;
      b[r][c] = 0;
      path.push({ type: "backtrack", r, c, v });
    }
    return false;
  }

  function computeAll() {
    const b = JSON.parse(JSON.stringify(original));
    const path = [];
    solve(b, 0, 0, path);
    return path;
  }

  function renderCell(r, c) {
    const v = board[r][c];
    const given = original[r][c] !== 0;
    let cls = "sudoku-cell";
    if (given) cls += " given";
    else if (v !== 0) cls += " placed";
    return '<div class="' + cls + '" id="cell-' + r + '-' + c + '">' + (v || '') + '</div>';
  }

  function renderGrid() {
    let html = "";
    for (let r = 0; r < 4; r++)
      for (let c = 0; c < 4; c++)
        html += renderCell(r, c);
    document.getElementById("sudoku-grid").innerHTML = html;
  }

  function renderStats() {
    document.getElementById("sudoku-iter").textContent = iterations;
    document.getElementById("sudoku-back").textContent = backtracks;
  }

  function reset() {
    board = JSON.parse(JSON.stringify(PUZZLE));
    original = JSON.parse(JSON.stringify(PUZZLE));
    steps = computeAll();
    stepIdx = 0;
    iterations = 0;
    backtracks = 0;
    renderGrid();
    renderStats();
    document.getElementById("sudoku-log").textContent = "Нажми «Один шаг» или «Решить всё»";
  }

  function applyStep(step) {
    iterations++;
    const cell = document.getElementById("cell-" + step.r + "-" + step.c);
    if (step.type === "place") {
      board[step.r][step.c] = step.v;
      if (cell) { cell.textContent = step.v; cell.className = "sudoku-cell trying"; }
      document.getElementById("sudoku-log").textContent =
        "Попытка поставить " + step.v + " в (" + step.r + ", " + step.c + ")";
    } else if (step.type === "backtrack") {
      board[step.r][step.c] = 0;
      backtracks++;
      if (cell) { cell.textContent = ""; cell.className = "sudoku-cell trying"; }
      document.getElementById("sudoku-log").textContent =
        "Откат: не подошло " + step.v + " в (" + step.r + ", " + step.c + ")";
    } else if (step.type === "conflict") {
      if (cell) { cell.classList.add("conflict"); }
      document.getElementById("sudoku-log").textContent =
        "Конфликт: " + step.v + " в (" + step.r + ", " + step.c + ") не подходит";
    }
  }

  function cleanupTrying() {
    document.querySelectorAll(".sudoku-cell.trying").forEach(e => {
      e.classList.remove("trying");
      if (e.textContent) e.classList.add("placed");
    });
    document.querySelectorAll(".sudoku-cell.conflict").forEach(e => e.classList.remove("conflict"));
  }

  document.getElementById("sudoku-step").addEventListener("click", () => {
    if (stepIdx >= steps.length) {
      document.getElementById("sudoku-log").textContent = "Всё, решено!";
      return;
    }
    cleanupTrying();
    applyStep(steps[stepIdx++]);
    renderStats();
  });

  document.getElementById("sudoku-solve").addEventListener("click", () => {
    const tick = () => {
      if (stepIdx >= steps.length) {
        cleanupTrying();
        document.getElementById("sudoku-log").textContent =
          "Решено за " + iterations + " итераций, " + backtracks + " откатов.";
        return;
      }
      cleanupTrying();
      applyStep(steps[stepIdx++]);
      renderStats();
      setTimeout(tick, 80);
    };
    tick();
  });

  document.getElementById("sudoku-reset").addEventListener("click", reset);

  reset();
})();

// ============================================================
// 3. QUEUE: Snake
// ============================================================
(function () {
  const COLS = 15, ROWS = 15, CELL = 24;
  const canvas = document.getElementById("snake-canvas");
  if (!canvas) return;
  const ctx = canvas.getContext("2d");
  canvas.width = COLS * CELL;
  canvas.height = ROWS * CELL;

  let queue = [];
  let dir = { x: 1, y: 0 };
  let nextDir = { x: 1, y: 0 };
  let food = { x: 8, y: 8 };
  let score = 0;
  let running = false;
  let paused = false;
  let ticker = null;
  let gameOver = false;

  function reset() {
    queue = [{ x: 3, y: 7 }, { x: 4, y: 7 }, { x: 5, y: 7 }];
    dir = { x: 1, y: 0 }; nextDir = { x: 1, y: 0 };
    score = 0;
    gameOver = false;
    running = false;
    paused = false;
    if (ticker) { clearInterval(ticker); ticker = null; }
    placeFood();
    draw();
    updateSide();
    document.getElementById("snake-log").textContent = "Нажми «Старт»";
  }

  function placeFood() {
    for (let tries = 0; tries < 200; tries++) {
      const f = { x: Math.floor(Math.random() * COLS), y: Math.floor(Math.random() * ROWS) };
      if (!queue.some(s => s.x === f.x && s.y === f.y)) { food = f; return; }
    }
  }

  function step() {
    if (!running || paused) return;
    dir = nextDir;
    const head = queue[queue.length - 1];
    const nh = { x: head.x + dir.x, y: head.y + dir.y };
    if (nh.x < 0 || nh.x >= COLS || nh.y < 0 || nh.y >= ROWS) { end(); return; }

    const willEat = (nh.x === food.x && nh.y === food.y);
    // Если не едим — хвост уйдёт на этом шаге, его позицию занимать можно.
    const segmentsToCheck = willEat ? queue : queue.slice(1);
    if (segmentsToCheck.some(s => s.x === nh.x && s.y === nh.y)) { end(); return; }

    queue.push(nh); // enqueue
    if (willEat) {
      score++;
      placeFood();
    } else {
      queue.shift(); // dequeue
    }
    draw(); updateSide();
  }

  function end() {
    running = false;
    gameOver = true;
    if (ticker) { clearInterval(ticker); ticker = null; }
    document.getElementById("snake-log").textContent = "Игра окончена. Счёт: " + score;
    draw();
  }

  function draw() {
    ctx.fillStyle = "#0f172a";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    // grid
    ctx.strokeStyle = "rgba(148, 163, 184, 0.15)";
    for (let i = 0; i <= COLS; i++) {
      ctx.beginPath(); ctx.moveTo(i * CELL, 0); ctx.lineTo(i * CELL, canvas.height); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(0, i * CELL); ctx.lineTo(canvas.width, i * CELL); ctx.stroke();
    }
    // food
    ctx.fillStyle = "#ef4444";
    ctx.beginPath();
    ctx.arc(food.x * CELL + CELL / 2, food.y * CELL + CELL / 2, CELL / 2 - 3, 0, Math.PI * 2);
    ctx.fill();
    // snake
    for (let i = 0; i < queue.length; i++) {
      const s = queue[i];
      const isHead = i === queue.length - 1;
      ctx.fillStyle = isHead ? "#22c55e" : "#4ade80";
      ctx.fillRect(s.x * CELL + 2, s.y * CELL + 2, CELL - 4, CELL - 4);
    }
    if (gameOver) {
      ctx.fillStyle = "rgba(0,0,0,0.6)";
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = "white";
      ctx.font = "bold 24px system-ui";
      ctx.textAlign = "center";
      ctx.fillText("Game Over", canvas.width / 2, canvas.height / 2);
    }
  }

  function updateSide() {
    document.getElementById("snake-score").textContent = score;
    document.getElementById("snake-len").textContent = queue.length;
    const head = queue[queue.length - 1];
    if (head) document.getElementById("snake-head").textContent = "(" + head.x + ", " + head.y + ")";
    let html = "";
    for (let i = queue.length - 1; i >= 0; i--) {
      const s = queue[i];
      const cls = i === queue.length - 1 ? "head" : (i === 0 ? "tail" : "");
      const label = i === queue.length - 1 ? "HEAD" : (i === 0 ? "TAIL" : "    ");
      html += '<div class="qitem ' + cls + '">' + label + ' → (' + s.x + ', ' + s.y + ')</div>';
    }
    document.getElementById("snake-queue").innerHTML = html;
  }

  document.addEventListener("keydown", e => {
    const k = e.key.toLowerCase();
    const map = {
      arrowup: { x: 0, y: -1 }, w: { x: 0, y: -1 },
      arrowdown: { x: 0, y: 1 }, s: { x: 0, y: 1 },
      arrowleft: { x: -1, y: 0 }, a: { x: -1, y: 0 },
      arrowright: { x: 1, y: 0 }, d: { x: 1, y: 0 }
    };
    if (map[k]) {
      const nd = map[k];
      if (nd.x !== -dir.x || nd.y !== -dir.y) nextDir = nd;
      e.preventDefault();
    }
    if (k === " ") {
      if (running) paused = !paused;
      e.preventDefault();
    }
  });

  document.getElementById("snake-start").addEventListener("click", () => {
    if (gameOver || queue.length === 0) reset();
    if (!running) {
      running = true;
      paused = false;
      if (ticker) clearInterval(ticker);
      ticker = setInterval(step, 120);
      document.getElementById("snake-log").textContent = "Играем! Стрелки для управления.";
    }
  });

  document.getElementById("snake-pause").addEventListener("click", () => {
    if (running) paused = !paused;
    document.getElementById("snake-log").textContent = paused ? "Пауза" : "Продолжаем";
  });

  reset();
})();

// ============================================================
// 4. TREE: Behavior Tree for NPC
// ============================================================
(function () {
  const svg = document.getElementById("bt-svg");
  const canvas = document.getElementById("bt-canvas");
  if (!svg || !canvas) return;
  const ctx = canvas.getContext("2d");

  const W = 10, H = 10, CELL = 22;
  canvas.width = W * CELL; canvas.height = H * CELL;

  // Tree structure
  const tree = {
    name: "Selector", type: "selector", children: [
      {
        name: "Attack", type: "sequence", children: [
          { name: "InRange?", type: "condition" },
          { name: "Attack!", type: "action" }
        ]
      },
      {
        name: "Chase", type: "sequence", children: [
          { name: "CanSee?", type: "condition" },
          { name: "MoveToPlayer", type: "action" }
        ]
      },
      { name: "Patrol", type: "action" }
    ]
  };

  // Assign layout (simple hierarchical)
  const layout = [
    { node: tree, x: 170, y: 30 },
    { node: tree.children[0], x: 80, y: 100 },
    { node: tree.children[1], x: 170, y: 100 },
    { node: tree.children[2], x: 260, y: 100 },
    { node: tree.children[0].children[0], x: 30, y: 190 },
    { node: tree.children[0].children[1], x: 130, y: 190 },
    { node: tree.children[1].children[0], x: 200, y: 190 },
    { node: tree.children[1].children[1], x: 300, y: 190 }
  ];

  const edges = [
    [0, 1], [0, 2], [0, 3],
    [1, 4], [1, 5], [2, 6], [2, 7]
  ];

  let npc = { x: 2, y: 2 };
  let player = { x: 8, y: 7 };
  let activePath = new Set();
  let activeLeaf = null;
  let autoTimer = null;

  function drawTree() {
    let html = "";
    edges.forEach(([a, b]) => {
      const A = layout[a], B = layout[b];
      const active = activePath.has(a) && activePath.has(b);
      html += '<line x1="' + A.x + '" y1="' + (A.y + 15) + '" x2="' + B.x + '" y2="' + (B.y - 15) +
              '" stroke="' + (active ? "#0b6bcb" : "#cbd5e1") + '" stroke-width="' + (active ? 3 : 1.5) + '"/>';
    });
    layout.forEach((item, i) => {
      const node = item.node;
      const active = activePath.has(i);
      const color = node.type === "selector" ? "#8b5cf6"
                  : node.type === "sequence" ? "#f59e0b"
                  : node.type === "condition" ? "#10b981"
                  : "#3b82f6";
      const fill = active ? color : "#e5e7eb";
      const textColor = active ? "white" : "#6b7280";
      html += '<g class="bt-node">';
      html += '<rect x="' + (item.x - 48) + '" y="' + (item.y - 15) + '" width="96" height="30" rx="6" fill="' + fill + '" stroke="' + color + '" stroke-width="2"/>';
      const label = node.name.length > 14 ? node.name.slice(0, 13) + "…" : node.name;
      html += '<text x="' + item.x + '" y="' + (item.y + 5) + '" text-anchor="middle" fill="' + textColor + '" font-size="11" font-weight="600">' + label + '</text>';
      html += '</g>';
    });
    svg.innerHTML = html;
  }

  function drawWorld() {
    ctx.fillStyle = "#f1f5f9";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.strokeStyle = "#e2e8f0";
    for (let i = 0; i <= W; i++) {
      ctx.beginPath(); ctx.moveTo(i * CELL, 0); ctx.lineTo(i * CELL, canvas.height); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(0, i * CELL); ctx.lineTo(canvas.width, i * CELL); ctx.stroke();
    }
    // player (orange)
    ctx.fillStyle = "#f97316";
    ctx.beginPath();
    ctx.arc(player.x * CELL + CELL / 2, player.y * CELL + CELL / 2, CELL / 2 - 2, 0, Math.PI * 2);
    ctx.fill();
    // NPC (blue)
    ctx.fillStyle = "#0b6bcb";
    ctx.beginPath();
    ctx.arc(npc.x * CELL + CELL / 2, npc.y * CELL + CELL / 2, CELL / 2 - 2, 0, Math.PI * 2);
    ctx.fill();
    // line of sight
    if (activePath.size > 0 && canSee()) {
      ctx.strokeStyle = "rgba(11, 107, 203, 0.3)";
      ctx.setLineDash([4, 4]);
      ctx.beginPath();
      ctx.moveTo(npc.x * CELL + CELL / 2, npc.y * CELL + CELL / 2);
      ctx.lineTo(player.x * CELL + CELL / 2, player.y * CELL + CELL / 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  function dist() {
    const dx = player.x - npc.x, dy = player.y - npc.y;
    return Math.sqrt(dx * dx + dy * dy);
  }
  function inRange() { return dist() <= 1.5; }
  function canSee() { return dist() <= 4; }

  function tick() {
    activePath = new Set([0]);
    activeLeaf = null;

    // Try Attack
    if (inRange()) {
      activePath.add(1); activePath.add(4); activePath.add(5);
      activeLeaf = "Attack!";
      document.getElementById("bt-log").textContent = "NPC: игрок в радиусе → Attack!";
    } else {
      // Try Chase
      activePath.add(1); // visited selector child but failed
      // Actually mark as tried but not selected:
      activePath.delete(1);
      if (canSee()) {
        activePath.add(2); activePath.add(6); activePath.add(7);
        activeLeaf = "MoveToPlayer";
        moveNpcTowardPlayer();
        document.getElementById("bt-log").textContent = "NPC: видит игрока → MoveToPlayer";
      } else {
        activePath.add(3);
        activeLeaf = "Patrol";
        moveNpcRandom();
        document.getElementById("bt-log").textContent = "NPC: не видит → Patrol";
      }
    }
    drawTree(); drawWorld();
  }

  function moveNpcTowardPlayer() {
    const dx = Math.sign(player.x - npc.x);
    const dy = Math.sign(player.y - npc.y);
    if (Math.abs(player.x - npc.x) > Math.abs(player.y - npc.y)) {
      if (dx !== 0 && canMove(npc.x + dx, npc.y)) npc.x += dx;
      else if (dy !== 0 && canMove(npc.x, npc.y + dy)) npc.y += dy;
    } else {
      if (dy !== 0 && canMove(npc.x, npc.y + dy)) npc.y += dy;
      else if (dx !== 0 && canMove(npc.x + dx, npc.y)) npc.x += dx;
    }
  }
  function moveNpcRandom() {
    const dirs = [[1,0],[-1,0],[0,1],[0,-1]];
    const [dx, dy] = dirs[Math.floor(Math.random() * 4)];
    if (canMove(npc.x + dx, npc.y + dy)) { npc.x += dx; npc.y += dy; }
  }
  function canMove(x, y) { return x >= 0 && x < W && y >= 0 && y < H; }

  function reset() {
    npc = { x: 2, y: 2 };
    player = { x: 8, y: 7 };
    activePath = new Set();
    if (autoTimer) { clearInterval(autoTimer); autoTimer = null; }
    drawTree(); drawWorld();
    document.getElementById("bt-log").textContent = "Нажми «Шаг»";
    document.getElementById("bt-auto").textContent = "Авто";
  }

  document.getElementById("bt-step").addEventListener("click", tick);
  document.getElementById("bt-reset").addEventListener("click", reset);
  document.getElementById("bt-auto").addEventListener("click", (e) => {
    if (autoTimer) {
      clearInterval(autoTimer); autoTimer = null;
      e.target.textContent = "Авто";
      return;
    }
    autoTimer = setInterval(() => {
      // occasionally move player
      if (Math.random() < 0.35) {
        const dirs = [[1,0],[-1,0],[0,1],[0,-1]];
        const [dx, dy] = dirs[Math.floor(Math.random() * 4)];
        const nx = player.x + dx, ny = player.y + dy;
        if (nx >= 0 && nx < W && ny >= 0 && ny < H) { player.x = nx; player.y = ny; }
      }
      tick();
    }, 900);
    e.target.textContent = "Стоп";
  });

  reset();
})();