import { RecursionAlgorithms } from './recursion-algorithms.js';
import { TreeRenderer } from './recursion-renderer.js';

const treeCanvas = document.getElementById('tree-canvas');
const chartCanvas = document.getElementById('chart-canvas');
const infoPanel = document.getElementById('info-panel');
const btnTree = document.getElementById('btn-tree');
const btnGraph = document.getElementById('btn-graph');
const selectAlgo = document.getElementById('algo-select');
const inputN = document.getElementById('algo-n');
const inputMaxN = document.getElementById('algo-max-n');
const algoDesc = document.getElementById('algo-description'); // Новый элемент

if (treeCanvas && chartCanvas) {
    
    const renderer = new TreeRenderer(treeCanvas);
    let chartInstance = null;

    // --- Справочная информация ---
    const descriptions = {
        hanoi: `
            <b style="color: #3b82f6;">Задача Реве</b><br>
            Классическая рекурсивная задача с 4 стержнями. Требуется перенести пирамиду из n дисков с одного стержня на другой, используя вспомогательные. Нельзя класть больший диск на меньший.<br><br>
            <b>Сложность:</b> Экспоненциальная, O(2^n).<br>
            <b>Применение:</b> Демонстрация принципов рекурсии, стресс-тестирование стека и процессора.
        `,
        tak: `
            <b style="color: #ef4444;">Функция Tak</b><br>
            Разработана Икуо Такеути. Использует глубокую вложенную рекурсию, вызывая сама себя в качестве собственных аргументов. Совершает огромное количество повторных вызовов.<br><br>
            <b>Сложность:</b> Сверхсложная, близкая к O(3^n).<br>
            <b>Применение:</b> Бенчмаркинг языков, тестирование оптимизации хвостовой рекурсии.
        `,
        hofstadter: `
            <b style="color: #10b981;">Последовательность Хофштадтера (Q)</b><br>
            Самореферентная числовая последовательность. Каждое следующее значение вычисляется на основе предыдущих результатов этой же последовательности. Ветвление рекурсии ограничено.<br><br>
            <b>Сложность:</b> Линейная, O(n).<br>
            <b>Применение:</b> Дискретная математика, теория хаоса, алгоритмы динамического программирования.
        `
    };

    function updateDescription() {
        if (algoDesc && descriptions[selectAlgo.value]) {
            algoDesc.innerHTML = descriptions[selectAlgo.value];
        }
    }

    // Слушаем изменение выбора алгоритма
    if (selectAlgo) {
        selectAlgo.addEventListener('change', updateDescription);
        updateDescription(); // Вызываем сразу при загрузке страницы
    }

    // --- 1. Настройка графика (Chart.js) ---
    function initChart() {
        if (typeof Chart === 'undefined') return;
        if (chartInstance) chartInstance.destroy();
        
        Chart.defaults.color = '#94a3b8';
        Chart.defaults.borderColor = '#334155';

        chartInstance = new Chart(chartCanvas, {
            type: 'line',
            data: { 
                labels: [], 
                datasets: [{ 
                    label: 'Время (мс)', 
                    data: [], 
                    borderColor: '#3b82f6', 
                    backgroundColor: 'rgba(59, 130, 246, 0.2)', 
                    borderWidth: 2,
                    pointBackgroundColor: '#fff',
                    fill: true, 
                    tension: 0.3 
                }] 
            },
            options: {
                responsive: true, 
                maintainAspectRatio: false,
                plugins: { 
                    legend: { display: false }, 
                    title: { display: true, text: 'Зависимость времени от N', color: '#fff', font: { size: 16 } } 
                },
                scales: {
                    x: { title: { display: true, text: 'Входные данные (N)' } },
                    y: { title: { display: true, text: 'Время (мс)' }, beginAtZero: true }
                }
            }
        });
    }
    initChart();

    // --- 2. Построение дерева ---
    if (btnTree) {
        btnTree.addEventListener('click', () => {
            renderer.clear();
            if (infoPanel) infoPanel.innerHTML = `<h3>Информация об узле</h3><p style="color: #64748b; font-style: italic;">Кликните на узел...</p>`;
            
            const algo = selectAlgo.value;
            const n = parseInt(inputN.value, 10);
            const idCounter = { val: 0 };

            const onCall = (id, parentId, data, depth) => {
                renderer.addNode(id, parentId, data, depth);
            };

            if (algo === 'hanoi') {
                RecursionAlgorithms.reve(n, 'A', 'D', 'B', 'C', 0, null, onCall, idCounter);
            } else if (algo === 'tak') {
                RecursionAlgorithms.tak(n, Math.floor(n / 2), 1, 0, null, onCall, idCounter);
            } else if (algo === 'hofstadter') {
                RecursionAlgorithms.hofstadter(n, 0, null, onCall, idCounter);
            }

            renderer.render();
        });
    }

    // --- 3. Построение графика ---
    if (btnGraph) {
        btnGraph.addEventListener('click', async () => {
            const algo = selectAlgo.value;
            const maxN = parseInt(inputMaxN.value, 10);
            
            btnGraph.textContent = 'Загрузка...';
            btnGraph.disabled = true;

            try {
                const res = await fetch(`/api/algorithms/benchmark-range?algo=${algo}&maxN=${maxN}`);
                if (!res.ok) throw new Error('Ошибка сервера');
                
                const data = await res.json();
                
                if (chartInstance) {
                    chartInstance.data.labels = data.map(d => d.n);
                    chartInstance.data.datasets[0].data = data.map(d => d.timeMs);
                    
                    if (algo === 'tak') chartInstance.data.datasets[0].borderColor = '#ef4444';
                    else if (algo === 'hofstadter') chartInstance.data.datasets[0].borderColor = '#10b981';
                    else chartInstance.data.datasets[0].borderColor = '#3b82f6';
                    
                    chartInstance.update();
                }
            } catch (e) {
                console.error(e);
                alert('Не удалось получить данные от сервера C#.');
            } finally {
                btnGraph.textContent = 'Замерить скорость (C#)';
                btnGraph.disabled = false;
            }
        });
    }

    // --- 4. Клик по дереву ---
    const treeContainer = treeCanvas.parentElement;
    if (treeContainer) {
        treeContainer.addEventListener('mousedown', (e) => {
            e.currentTarget.dataset.mouseDown = 'true';
            e.currentTarget.dataset.startX = e.clientX;
            e.currentTarget.dataset.startY = e.clientY;
        });

        treeContainer.addEventListener('mouseup', (e) => {
            if (e.currentTarget.dataset.mouseDown !== 'true') return;
            e.currentTarget.dataset.mouseDown = 'false';

            const dx = Math.abs(e.clientX - parseFloat(e.currentTarget.dataset.startX || 0));
            const dy = Math.abs(e.clientY - parseFloat(e.currentTarget.dataset.startY || 0));
            
            if (dx > 5 || dy > 5) return;

            const node = renderer.getNodeAt(e.clientX, e.clientY);
            
            if (infoPanel) {
                if (node) {
                    infoPanel.innerHTML = `
                        <h3>Детали вызова</h3>
                        <div class="info-row"><span class="info-label">Метод:</span> <span class="info-value">${node.data.title.split('(')[0]}</span></div>
                        <div class="info-row"><span class="info-label">Вызов:</span> <span class="info-value">${node.data.title}</span></div>
                        <div class="info-row"><span class="info-label">Условие:</span> <span class="info-value">${node.data.args || '-'}</span></div>
                        <div class="info-row"><span class="info-label">Результат:</span> <span class="info-value">${node.data.result || '-'}</span></div>
                        <div class="info-row"><span class="info-label">Глубина:</span> <span class="info-value">${node.depth}</span></div>
                        <div class="info-row"><span class="info-label">Базовый случай:</span> <span class="info-value">${node.data.isBase ? 'Да' : 'Нет'}</span></div>
                    `;
                } else {
                    infoPanel.innerHTML = `<h3>Информация об узле</h3><p style="color: #64748b; font-style: italic;">Кликните на узел дерева...</p>`;
                }
            }
        });
    }

    // --- 5. Адаптация размеров ---
    window.addEventListener('resize', () => {
        renderer.render();
    });
}