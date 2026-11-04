<#
.SYNOPSIS
    Шаг 9: откат вкладки "Примеры" + условия задач для 12 алгоритмов списка.
#>
[CmdletBinding()]
param([string]$Root = ".")

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Info([string]$msg) { Write-Host "  $msg" }

Set-Location (Resolve-Path $Root).Path

# ---------- 1. Откат: убираем кнопку "Примеры" из навигации ----------
Write-Step "Откат: убираем вкладку 'Примеры'"
$htmlPath = "LabApp/wwwroot/index.html"
$c = [System.IO.File]::ReadAllText($htmlPath, [System.Text.Encoding]::UTF8)

$c = $c.Replace('      <button class="tab" data-tab="examples">Примеры</button>' + "`r`n", '')
$c = $c.Replace('      <button class="tab" data-tab="examples">Примеры</button>' + "`n", '')

# ---------- 2. Откат: удаляем секцию panel-examples ----------
Write-Step "Откат: удаляем секцию panel-examples"
# Ищем от <!-- Examples --> до закрывающего </section>
$pattern = '(?s)  <!-- Examples -->.*?</section>'
$c = [regex]::Replace($c, $pattern, '')

[System.IO.File]::WriteAllText($htmlPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "index.html откачен"

# ---------- 3. Добавляем блок описания в panel-lists ----------
Write-Step "index.html: блок описания задачи"
$c = [System.IO.File]::ReadAllText($htmlPath, [System.Text.Encoding]::UTF8)

$oldLists = @'
  <!-- Lists -->
  <section id="panel-lists" class="panel">
    <h2>Связный список</h2>
    <label>Элементы (через запятую): <input id="list-items" type="text" value="3, 1, 4, 1, 5, 9, 2, 6"></label>
    <label>Операция:
      <select id="list-op">
        <option value="reverse">reverse</option>
        <option value="moveLastToFront">moveLastToFront</option>
        <option value="moveFirstToEnd">moveFirstToEnd</option>
        <option value="countDistinct">countDistinct</option>
        <option value="removeNonUnique">removeNonUnique</option>
        <option value="insertSelfAfterX">insertSelfAfterX</option>
        <option value="sortedInsert">sortedInsert</option>
        <option value="removeAll">removeAll</option>
        <option value="insertBefore">insertBefore</option>
        <option value="append">append</option>
        <option value="splitByValue">splitByValue</option>
        <option value="double">double</option>
        <option value="swap">swap</option>
      </select>
    </label>
    <div id="list-args"></div>
    <button id="list-run">Применить</button>
    <div id="list-output" class="output"></div>
  </section>
'@

$newLists = @'
  <!-- Lists -->
  <section id="panel-lists" class="panel">
    <h2>Связный список: 12 алгоритмов</h2>
    <label>Элементы (через запятую): <input id="list-items" type="text" value="3, 1, 4, 1, 5, 9, 2, 6"></label>
    <label>Операция:
      <select id="list-op">
        <option value="reverse">reverse — перевернуть список</option>
        <option value="moveLastToFront">moveLastToFront — последний в начало</option>
        <option value="moveFirstToEnd">moveFirstToEnd — первый в конец</option>
        <option value="countDistinct">countDistinct — количество различных</option>
        <option value="removeNonUnique">removeNonUnique — удалить неуникальные</option>
        <option value="insertSelfAfterX">insertSelfAfterX — вставить себя после x</option>
        <option value="sortedInsert">sortedInsert — вставить в упорядоченный</option>
        <option value="removeAll">removeAll — удалить все E</option>
        <option value="insertBefore">insertBefore — вставить F перед E</option>
        <option value="append">append — дописать список</option>
        <option value="splitByValue">splitByValue — разбить по x</option>
        <option value="double">double — удвоить список</option>
        <option value="swap">swap — поменять местами</option>
      </select>
    </label>
    <div id="list-args"></div>
    <button id="list-run">Применить</button>

    <div id="list-desc" class="task-desc"></div>

    <div id="list-output" class="output"></div>
  </section>
'@

$c = $c.Replace($oldLists, $newLists)
[System.IO.File]::WriteAllText($htmlPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "блок описания добавлен"

# ---------- 4. app.js: описания задач ----------
Write-Step "app.js: описания задач"
$jsPath = "LabApp/wwwroot/app.js"
$c = [System.IO.File]::ReadAllText($jsPath, [System.Text.Encoding]::UTF8)

# 4.1. Вставляем объект LIST_TASKS и функцию renderTaskDesc перед функцией updateListForm
$tasksJs = @'
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

'@

# Вставляем перед "const LIST_SPECS ="
$c = $c.Replace("const LIST_SPECS = {", $tasksJs + "const LIST_SPECS = {")

# 4.2. Обновляем updateListForm, чтобы вызывать renderTaskDesc
$oldUpdate = @'
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
'@

$newUpdate = @'
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
'@

$c = $c.Replace($oldUpdate, $newUpdate)

[System.IO.File]::WriteAllText($jsPath, $c, [System.Text.UTF8Encoding]::new($true))
Write-Info "app.js обновлён"

# ---------- 5. styles.css: стиль task-desc ----------
Write-Step "styles.css: стиль task-desc"
$cssPath = "LabApp/wwwroot/styles.css"
$css = [System.IO.File]::ReadAllText($cssPath, [System.Text.Encoding]::UTF8)

$append = @'

/* ---- Описание задачи в Lists ---- */
.task-desc {
  margin-top: 1.25rem;
  padding: 1rem 1.25rem;
  background: #fefce8;
  border: 1px solid #fde047;
  border-radius: 8px;
  line-height: 1.6;
}
.task-desc h4 {
  margin: 0 0 .75rem;
  color: #854d0e;
  font-size: 1.05rem;
}
.task-desc p { margin: .5rem 0; font-size: 0.95rem; }
.task-desc strong { color: #713f12; }
'@

[System.IO.File]::WriteAllText($cssPath, $css + $append, [System.Text.UTF8Encoding]::new($true))
Write-Info "styles.css обновлён"

# ---------- 6. Сборка и тесты ----------
Write-Step "Сборка"
$sln = if (Test-Path "LabApp.slnx") { "LabApp.slnx" } else { "LabApp.sln" }
dotnet build $sln --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build упал" }

Write-Step "Тесты"
dotnet test $sln --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet test упал" }

Write-Step "Готово"
Write-Host "  Запускай: dotnet run --project LabApp"
Write-Host "  Открой http://localhost:5000 -> вкладка Lists"
Write-Host "  При выборе операции появится блок с условием задачи и логикой решения."