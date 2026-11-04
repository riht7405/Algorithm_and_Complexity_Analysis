// ---------- утилиты ----------
const API = "/api";

document.querySelectorAll(".tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach(t => t.classList.remove("active"));
    document.querySelectorAll(".panel").forEach(p => p.classList.remove("active"));
    tab.classList.add("active");
    document.getElementById("panel-" + tab.dataset.tab).classList.add("active");
  });
});

async function post(path, body) {
  const r = await fetch(API + path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  return r.json();
}
async function get(path) {
  const r = await fetch(API + path);
  return r.json();
}

function esc(s) {
  return String(s).replace(/[&<>"']/g, c => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  }[c]));
}

function renderTrace(trace) {
  if (!trace || trace.length === 0) return "<em>нет событий</em>";
  const rows = trace.map(t =>
    "<tr><td>" + t.index + "</td><td>" + esc(t.operation) + "</td><td>" +
    esc(t.argument ?? "") + "</td><td>" + esc(t.result) + "</td></tr>"
  ).join("");
  return "<table class=\"trace\"><thead><tr><th>#</th><th>Операция</th><th>Аргумент</th><th>Результат</th></tr></thead><tbody>" + rows + "</tbody></table>";
}

function renderTiming(t) {
  return "<div class=\"timing\">read: " + t.readMs.toFixed(3) + " ms · parse: " +
    t.parseMs.toFixed(3) + " ms · execute: " + t.executeMs.toFixed(3) +
    " ms · total: " + t.totalMs.toFixed(3) + " ms</div>";
}

// ---------- stack ----------
document.getElementById("stack-run").addEventListener("click", async () => {
  const input = document.getElementById("stack-input").value;
  const out = document.getElementById("stack-output");
  out.textContent = "Выполняется...";
  try {
    const r = await post("/runs/stack", { input });
    out.innerHTML = renderTiming(r.timing) +
      "<div class=\"final\">Итог: <code>" + esc(r.finalState) + "</code></div>" +
      renderTrace(r.trace);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

// ---------- queue ----------
document.getElementById("queue-run").addEventListener("click", async () => {
  const input = document.getElementById("queue-input").value;
  const out = document.getElementById("queue-output");
  out.textContent = "Выполняется...";
  try {
    const r = await post("/runs/queue", { input });
    out.innerHTML = renderTiming(r.timing) +
      "<div class=\"final\">Итог: <code>" + esc(r.finalState) + "</code></div>" +
      renderTrace(r.trace);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

// ---------- expressions ----------
document.getElementById("postfix-eval").addEventListener("click", async () => {
  const expression = document.getElementById("postfix-input").value;
  const out = document.getElementById("postfix-output");
  out.textContent = "...";
  try {
    const r = await post("/expressions/postfix/evaluate", { expression });
    out.innerHTML = r.ok
      ? "Результат: <code>" + r.value + "</code>"
      : "Ошибка: " + esc(r.error);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

document.getElementById("infix-convert").addEventListener("click", async () => {
  const expression = document.getElementById("infix-input").value;
  const out = document.getElementById("infix-output");
  out.textContent = "...";
  try {
    const r = await post("/expressions/infix/to-postfix", { expression });
    out.innerHTML = r.ok
      ? "Постфиксная: <code>" + esc(r.postfix) + "</code>"
      : "Ошибка: " + esc(r.error);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

// ---------- lists ----------
const LIST_SPECS = {
  reverse: {},
  moveLastToFront: {},
  moveFirstToEnd: {},
  countDistinct: {},
  removeNonUnique: {},
  insertSelfAfterX: { arg: "x" },
  sortedInsert: { arg: "e" },
  removeAll: { arg: "e" },
  insertBefore: { arg: "e", arg2: "f" },
  append: { secondList: true },
  splitByValue: { arg: "x" },
  double: {},
  swap: { arg: "a", arg2: "b" }
};

function updateListForm() {
  const op = document.getElementById("list-op").value;
  const spec = LIST_SPECS[op] || {};
  const form = document.getElementById("list-args");
  let html = "";
  if (spec.arg) html += "<label>" + spec.arg + ": <input id=\"list-arg\" type=\"number\" value=\"3\"></label>";
  if (spec.arg2) html += "<label>" + spec.arg2 + ": <input id=\"list-arg2\" type=\"number\" value=\"5\"></label>";
  if (spec.secondList) html += "<label>secondList (через запятую): <input id=\"list-second\" type=\"text\" value=\"10, 20, 30\"></label>";
  form.innerHTML = html;
}

document.getElementById("list-op").addEventListener("change", updateListForm);
updateListForm();

document.getElementById("list-run").addEventListener("click", async () => {
  const op = document.getElementById("list-op").value;
  const items = document.getElementById("list-items").value
    .split(",").map(x => parseInt(x.trim(), 10)).filter(x => !isNaN(x));
  const body = { items };

  const arg = document.getElementById("list-arg"); if (arg) body.arg = parseInt(arg.value, 10);
  const arg2 = document.getElementById("list-arg2"); if (arg2) body.arg2 = parseInt(arg2.value, 10);
  const second = document.getElementById("list-second"); if (second) body.secondList = second.value
    .split(",").map(x => parseInt(x.trim(), 10)).filter(x => !isNaN(x));

  const out = document.getElementById("list-output");
  out.textContent = "...";
  try {
    const r = await post("/lists/operations/" + op, body);
    if (!r.ok) { out.innerHTML = "Ошибка: " + esc(r.error); return; }
    let html = "<div>До: <code>[" + r.before.join(", ") + "]</code></div>" +
               "<div>После: <code>[" + r.after.join(", ") + "]</code></div>";
    if (r.result) html += "<div>Результат: <code>" + esc(JSON.stringify(r.result)) + "</code></div>";
    out.innerHTML = html;
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

// ---------- tree ----------
document.getElementById("tree-run").addEventListener("click", async () => {
  const expression = document.getElementById("tree-input").value;
  const out = document.getElementById("tree-output");
  out.textContent = "...";
  try {
    const r = await post("/trees/parse-prefix", { expression });
    if (!r.ok) { out.innerHTML = "Ошибка: " + esc(r.error); return; }
    const preorderStr  = String.fromCharCode.apply(null, r.preorder);
    const inorderStr   = String.fromCharCode.apply(null, r.inorder);
    const postorderStr = String.fromCharCode.apply(null, r.postorder);
    out.innerHTML =
      "<div>Preorder: <code>" + esc(preorderStr) + "</code></div>" +
      "<div>Inorder: <code>" + esc(inorderStr) + "</code></div>" +
      "<div>Postorder: <code>" + esc(postorderStr) + "</code></div>" +
      "<div>Высота: " + r.height + "</div>" +
      "<div class=\"tree-svg\">" + renderTree(r.root) + "</div>";
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
});

function renderTree(root) {
  if (!root) return "<em>пусто</em>";
  const nodes = [];
  let nextX = 0;
  const XGAP = 46, LEVEL = 64, R = 18;

  function place(n, depth) {
    if (!n) return;
    place(n.left, depth + 1);
    n._x = nextX * XGAP;
    n._y = depth * LEVEL;
    nextX++;
    nodes.push(n);
    place(n.right, depth + 1);
  }
  place(root, 0);

  let maxX = 0, maxY = 0;
  for (let i = 0; i < nodes.length; i++) {
    if (nodes[i]._x > maxX) maxX = nodes[i]._x;
    if (nodes[i]._y > maxY) maxY = nodes[i]._y;
  }

  const edges = [];
  function drawEdges(n) {
    if (!n) return;
    if (n.left) edges.push("<line x1=\"" + (n._x + R) + "\" y1=\"" + (n._y + R) +
      "\" x2=\"" + (n.left._x + R) + "\" y2=\"" + (n.left._y + R) + "\" stroke=\"#9ca3af\" />");
    if (n.right) edges.push("<line x1=\"" + (n._x + R) + "\" y1=\"" + (n._y + R) +
      "\" x2=\"" + (n.right._x + R) + "\" y2=\"" + (n.right._y + R) + "\" stroke=\"#9ca3af\" />");
    drawEdges(n.left);
    drawEdges(n.right);
  }
  drawEdges(root);

  const circles = nodes.map(n =>
    "<circle cx=\"" + (n._x + R) + "\" cy=\"" + (n._y + R) + "\" r=\"" + R + "\" fill=\"#0b6bcb\" />" +
    "<text x=\"" + (n._x + R) + "\" y=\"" + (n._y + R + 5) + "\" text-anchor=\"middle\" fill=\"white\" font-size=\"14\">" + esc(n.value) + "</text>"
  ).join("");

  return "<svg width=\"" + (maxX + 2 * R + 10) + "\" height=\"" + (maxY + 2 * R + 10) + "\">" +
    edges.join("") + circles + "</svg>";
}

// ---------- benchmarks ----------
let benchChart = null;

async function runBench(kind) {
  const sizes = document.getElementById("bench-sizes").value;
  try {
    const r = await get("/benchmarks/" + kind + "?sizes=" + encodeURIComponent(sizes));
    renderChart(r);
  } catch (e) {
    document.getElementById("bench-table").textContent = "Ошибка: " + e;
  }
}

function renderChart(r) {
  const ctx = document.getElementById("bench-chart").getContext("2d");
  if (benchChart) benchChart.destroy();

  const customPoints = r.points.map(p => ({ x: p.size, y: p.customMs }));
  const standardPoints = r.points.map(p => ({ x: p.size, y: p.standardMs }));

  benchChart = new Chart(ctx, {
    type: "line",
    data: {
      datasets: [
        {
          label: "Своя структура",
          data: customPoints,
          borderColor: "#dc2626",
          backgroundColor: "rgba(220, 38, 38, 0.15)",
          tension: 0.2,
          pointRadius: 4
        },
        {
          label: "Стандартная .NET",
          data: standardPoints,
          borderColor: "#2563eb",
          backgroundColor: "rgba(37, 99, 235, 0.15)",
          tension: 0.2,
          pointRadius: 4
        }
      ]
    },
    options: {
      parsing: false,
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: "nearest", intersect: false },
      scales: {
        x: {
          type: "logarithmic",
          title: { display: true, text: "Размер входа (N)" }
        },
        y: {
          type: "logarithmic",
          title: { display: true, text: "Время, мс" }
        }
      },
      plugins: {
        legend: { position: "top" },
        tooltip: {
          callbacks: {
            title: ctxs => "N = " + ctxs[0].parsed.x,
            afterBody: ctxs => {
              const i = ctxs[0].dataIndex;
              const p = r.points[i];
              return "Ratio (своя / станд.): " + p.ratio.toFixed(2) + "x";
            }
          }
        }
      }
    }
  });

  const rows = r.points.map(p =>
    "<tr><td>" + p.size + "</td><td>" + p.customMs.toFixed(4) + "</td><td>" +
    p.standardMs.toFixed(4) + "</td><td>" + p.ratio.toFixed(2) + "</td></tr>"
  ).join("");
  document.getElementById("bench-table").innerHTML =
    "<div class=\"timing\">" + esc(r.structure) + " · " + esc(r.operation) + "</div>" +
    "<table class=\"trace\"><thead><tr><th>N</th><th>Своя, мс</th><th>Стандартная, мс</th><th>Ratio</th></tr></thead><tbody>" + rows + "</tbody></table>";
}

document.getElementById("bench-stack").addEventListener("click", () => runBench("stacks"));
document.getElementById("bench-queue").addEventListener("click", () => runBench("queues"));