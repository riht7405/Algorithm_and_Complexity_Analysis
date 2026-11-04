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
const LIST_TASKS = {
  reverse: {
    title: "1. Перевернуть список",
    condition: "Дан односвязный список L. Нужно изменить ссылки так, чтобы элементы шли в обратном порядке.",
    logic: "Идём по списку, сохраняя предыдущий узел (prev). На каждом шаге меняем указатель next текущего узла на prev. После прохода prev становится новой головой. Время O(n), память O(1)."
  },
  moveLastToFront: {
    title: "2. Перенести последний элемент в начало",
    condition: "Непустой список L. Перенести его последний элемент в начало.",
    logic: "Находим последний узел (RemoveLast), сохраняем значение, затем AddFirst. Если список пуст или один элемент — ничего не делаем. Время O(n) из-за поиска конца, память O(1)."
  },
  moveFirstToEnd: {
    title: "3. Перенести первый элемент в конец",
    condition: "Непустой список L. Перенести его первый элемент в конец.",
    logic: "RemoveFirst забирает голову, AddLast кладёт её в хвост. Обе операции O(1), потому что список хранит и Head, и Tail. Итого O(1)."
  },
  countDistinct: {
    title: "4. Количество различных элементов",
    condition: "Список L целых чисел. Найти число уникальных значений.",
    logic: "Проходим по списку, складывая значения в HashSet (или группируя). Возвращаем размер множества. Время O(n), память O(k), где k — число уникальных."
  },
  removeNonUnique: {
    title: "5. Удалить неуникальные элементы",
    condition: "Список L. Оставить только те элементы, которые встречаются ровно один раз.",
    logic: "Проходим список, считаем частоты (Dictionary). Вторым проходом удаляем все узлы, чья частота > 1. Время O(n), память O(k)."
  },
  insertSelfAfterX: {
    title: "6. Вставить список сам в себя после первого вхождения x",
    condition: "Список L и число x. После первого узла со значением x вставить копию всего списка.",
    logic: "Находим узел с x. Сохраняем снимок значений списка. Идём по снимку, создавая новые узлы, и вставляем их после найденного узла, сохраняя остаток списка. Время O(n + k), где k — длина списка на момент вставки."
  },
  sortedInsert: {
    title: "7. Вставить в упорядоченный список",
    condition: "Список L упорядочен по неубыванию. Вставить элемент E так, чтобы порядок сохранился.",
    logic: "Идём по списку, пока следующий элемент меньше E. Вставляем новый узел между текущим и следующим. Особые случаи: пустой список (AddFirst) и E меньше головы (AddFirst). Время O(n)."
  },
  removeAll: {
    title: "8. Удалить все элементы E",
    condition: "Список L и число E. Удалить все вхождения E.",
    logic: "Удаляем голову, пока она равна E. Затем идём по списку и пропускаем узлы, у которых next.Value == E, перепривязывая next. Время O(n), память O(1)."
  },
  insertBefore: {
    title: "9. Вставить F перед первым вхождением E",
    condition: "Список L, числа E и F. Если E есть в списке, вставить F перед первым E.",
    logic: "Если голова — E, AddFirst(F). Иначе идём, пока next.Value != E, и вставляем узел F после текущего (то есть перед следующим). Если E нет — ничего не делаем. Время O(n)."
  },
  append: {
    title: "10. Дописать список E к списку L",
    condition: "Два списка L и E целых чисел. Дописать E в конец L.",
    logic: "Копируем элементы E в конец L (AddLast для каждого). Оба списка не разрушаются. Время O(n + m)."
  },
  splitByValue: {
    title: "11. Разбить по первому вхождению x",
    condition: "Список L и число x. Разбить список на два: левый — до первого x, правый — начиная с x. Если x нет — правый пуст.",
    logic: "Идём по списку, складывая значения в left. Как только встретили x, флаг found = true, остальные значения — в right. Время O(n), память O(n)."
  },
  double: {
    title: "12. Удвоить список",
    condition: "Список L. Приписать в конец копию самого себя.",
    logic: "Сохраняем снимок значений списка (ToArray). Затем для каждого значения из снимка AddLast. Время O(n), память O(n)."
  },
  swap: {
    title: "13. Поменять местами два элемента",
    condition: "Список L и два значения a и b. Поменять местами узлы, содержащие a и b.",
    logic: "Ищем узлы a и b и их предшественников (prevA, prevB). Если оба найдены — переставляем указатели. Отдельно обрабатываем случаи: a и b соседние, a или b — голова. Время O(n), память O(1)."
  }
};

function renderTaskDesc(op) {
  const task = LIST_TASKS[op];
  const box = document.getElementById("list-desc");
  if (!task) { box.innerHTML = ""; return; }
  box.innerHTML =
    '<h4>' + esc(task.title) + '</h4>' +
    '<p><strong>Условие:</strong> ' + esc(task.condition) + '</p>' +
    '<p><strong>Логика решения:</strong> ' + esc(task.logic) + '</p>';
}
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
  renderTaskDesc(op);
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
  const max = document.getElementById("bench-max").value;
  const step = document.getElementById("bench-step").value;
  const out = document.getElementById("bench-table");
  out.textContent = "Считаем (это может занять до 10 секунд)...";
  try {
    const r = await get("/benchmarks/" + kind + "?max=" + max + "&step=" + step);
    renderChart(r);
  } catch (e) {
    out.textContent = "Ошибка: " + e;
  }
}

function renderChart(r) {
  const ctx = document.getElementById("bench-chart").getContext("2d");
  if (benchChart) benchChart.destroy();

  const customData = r.points.map(p => ({ x: p.size, y: p.customMs }));
  const standardData = r.points.map(p => ({ x: p.size, y: p.standardMs }));

  // Опция для 1:1 или квадратного аспекта (по желанию можно поменять 1.5 на 1)
  const aspect = 1.6;

  benchChart = new Chart(ctx, {
    type: "scatter", // base type
    data: {
      datasets: [
        {
          label: "Своя структура",
          data: customData,
          borderColor: "rgba(0, 188, 212, 0.8)",
          backgroundColor: "rgba(0, 188, 212, 0.8)",
          pointRadius: 3,
          pointHoverRadius: 5,
          showLine: false,
          order: 1
        },
        {
          label: "Стандартная .NET",
          data: standardData,
          borderColor: "#ff9800",
          backgroundColor: "#ff9800",
          pointRadius: 0,
          borderWidth: 2,
          showLine: true,
          tension: 0,
          order: 0
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: true,
      aspectRatio: aspect,
      interaction: { mode: "nearest", intersect: false },
      scales: {
        x: {
          type: "linear",
          beginAtZero: true,
          title: { display: true, text: "Размер входных данных (N)" },
          grid: { color: "#e5e7eb", drawBorder: false },
          ticks: { callback: (v) => v.toLocaleString() }
        },
        y: {
          type: "linear",
          beginAtZero: true,
          title: { display: true, text: "Время (мс)" },
          grid: { color: "#e5e7eb", drawBorder: false },
          ticks: { callback: (v) => v.toFixed(2) }
        }
      },
      plugins: {
        legend: {
          position: "bottom",
          align: "end",
          labels: {
            usePointStyle: true,
            boxWidth: 10,
            padding: 20
          }
        },
        tooltip: {
          callbacks: {
            title: (ctxs) => "N = " + ctxs[0].parsed.x.toLocaleString(),
            afterBody: (ctxs) => {
              const i = ctxs[0].dataIndex;
              const p = r.points[i];
              if (!p) return "";
              return "Ratio (своя / станд.): " + p.ratio.toFixed(2) + "x";
            }
          }
        }
      }
    }
  });

  const rows = r.points.map(p =>
    "<tr><td>" + p.size.toLocaleString() + "</td><td>" + p.customMs.toFixed(4) + "</td><td>" +
    p.standardMs.toFixed(4) + "</td><td>" + p.ratio.toFixed(2) + "</td></tr>"
  ).join("");
  out.innerHTML =
    "<div class=\"timing\">" + esc(r.structure) + " · " + esc(r.operation) + "</div>" +
    "<table class=\"trace\"><thead><tr><th>N</th><th>Своя, мс</th><th>Стандартная, мс</th><th>Ratio</th></tr></thead><tbody>" + rows + "</tbody></table>";
}

document.getElementById("bench-stack").addEventListener("click", () => runBench("stacks"));
document.getElementById("bench-queue").addEventListener("click", () => runBench("queues"));