export const RecursionAlgorithms = {
    // Задача Реве (4 стержня)
    reve: function(n, from, to, aux1, aux2, depth, parentId, onCall, idCounter) {
        if (idCounter.val > 4000) return; 
        const id = idCounter.val++;
        
        const nodeData = { 
            title: `Reve(${n}, ${from}->${to})`, 
            args: n <= 0 ? `Base` : "",
            result: "",
            isBase: n <= 0 
        };
        onCall(id, parentId, nodeData, depth);
        
        if (n <= 0) return;

        const k = Math.floor(n / 2);
        this.reve(k, from, aux1, aux2, to, depth + 1, id, onCall, idCounter);
        this.hanoi3(n - k, from, to, aux2, depth + 1, id, onCall, idCounter);
        this.reve(k, aux1, to, from, aux2, depth + 1, id, onCall, idCounter);
    },

    // Классический алгоритм Ханойской башни (3 стержня)
    hanoi3: function(n, from, to, aux, depth, parentId, onCall, idCounter) {
        if (idCounter.val > 4000) return;
        const id = idCounter.val++;
        
        const nodeData = { 
            title: `Hanoi3(${n}, ${from}->${to})`, 
            args: n <= 0 ? `Base` : "",
            result: "",
            isBase: n <= 0 
        };
        onCall(id, parentId, nodeData, depth);

        if (n <= 0) return;

        this.hanoi3(n - 1, from, aux, to, depth + 1, id, onCall, idCounter);
        this.hanoi3(n - 1, aux, to, from, depth + 1, id, onCall, idCounter);
    },

    // Функция Tak
    tak: function(x, y, z, depth, parentId, onCall, idCounter) {
        if (idCounter.val > 4000) return 0;
        const id = idCounter.val++;
        const isBase = y >= x;
        
        const nodeData = { 
            title: `Tak(${x},${y},${z})`, 
            args: isBase ? `y ≥ x` : ``, 
            result: isBase ? `= ${z}` : "", 
            isBase: isBase 
        };
        onCall(id, parentId, nodeData, depth);

        if (isBase) return z;
        if (depth > 8) return 0; 

        const t1 = this.tak(x - 1, y, z, depth + 1, id, onCall, idCounter);
        const t2 = this.tak(y - 1, z, x, depth + 1, id, onCall, idCounter);
        const t3 = this.tak(z - 1, x, y, depth + 1, id, onCall, idCounter);
        
        const res = this.tak(t1, t2, t3, depth + 1, id, onCall, idCounter);
        
        nodeData.result = `= ${res}`;
        return res;
    },

    // Последовательность Хофштадтера (Q)
    hofstadter: function(n, depth, parentId, onCall, idCounter) {
        if (idCounter.val > 4000) return 0;
        const id = idCounter.val++;
        
        const isBase = (n === 1 || n === 2);
        
        const nodeData = { 
            title: `Q(${n})`, 
            args: isBase ? `Base` : "", 
            result: isBase ? `= 1` : "", 
            isBase: isBase 
        };
        onCall(id, parentId, nodeData, depth);

        if (n <= 0) return 0;
        if (isBase) return 1;
        if (depth > 8) return 1;

        const q_n_minus_1 = this.hofstadter(n - 1, depth + 1, id, onCall, idCounter);
        const q_n_minus_2 = this.hofstadter(n - 2, depth + 1, id, onCall, idCounter);
        
        const term1 = this.hofstadter(n - q_n_minus_1, depth + 1, id, onCall, idCounter);
        const term2 = this.hofstadter(n - q_n_minus_2, depth + 1, id, onCall, idCounter);
        
        const res = term1 + term2;
        
        nodeData.result = `= ${res}`;
        return res;
    }, // <--- ВОТ ЗДЕСЬ БЫЛА ПРОПУЩЕНА ЗАПЯТАЯ

    // Функция Аккермана (самая сложная рекурсия)
    ackermann: function(m, n, depth, parentId, onCall, idCounter) {
        if (idCounter.val > 4000) return 0;
        const id = idCounter.val++;
        
        const nodeData = { 
            title: `Ack(${m},${n})`, 
            args: m === 0 ? `m=0` : (n === 0 ? `n=0` : ``), 
            result: "", 
            isBase: m === 0 
        };
        onCall(id, parentId, nodeData, depth);

        let res = 0;
        
        if (m === 0) {
            res = n + 1;
        } else if (m > 0 && n === 0) {
            res = this.ackermann(m - 1, 1, depth + 1, id, onCall, idCounter);
        } else if (m > 0 && n > 0) {
            // Вложенный вызов: A(m-1, A(m, n-1))
            const inner = this.ackermann(m, n - 1, depth + 1, id, onCall, idCounter);
            res = this.ackermann(m - 1, inner, depth + 1, id, onCall, idCounter);
        }

        nodeData.result = `= ${res}`;
        return res;
    }
};