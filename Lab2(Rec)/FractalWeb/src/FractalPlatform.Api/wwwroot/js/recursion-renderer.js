export class TreeRenderer {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.nodes = [];
        
        this.nodeWidth = 140; 
        this.nodeHeight = 60;
        this.xGap = 40;       
        this.yGap = 90;       

        this.scale = 1;
        this.offsetX = 0;
        this.offsetY = 0;
        this.isDragging = false;
        this.lastMouseX = 0;
        this.lastMouseY = 0;
        
        this._bindEvents();
    }

    clear() { 
        this.nodes = []; 
        this.scale = 1;
        this.offsetX = 0;
        this.offsetY = 0;
        this.applyTransform();
    }

    addNode(id, parentId, data, depth) {
        this.nodes.push({ id, parentId, data, depth, x: 0, y: 0 });
    }

    layout() {
        if (this.nodes.length === 0) return;

        const root = this.nodes.find(n => n.parentId === null);
        if (!root) {
            console.error("Корневой узел не найден!");
            return;
        }

        const childrenMap = new Map();
        this.nodes.forEach(n => {
            if (n.parentId !== null) {
                if (!childrenMap.has(n.parentId)) childrenMap.set(n.parentId, []);
                childrenMap.get(n.parentId).push(n);
            }
        });

        let maxDepth = 0;

        const calculateWidth = (node) => {
            if (node.depth > maxDepth) maxDepth = node.depth;
            const children = childrenMap.get(node.id) || [];
            if (children.length === 0) {
                node.subtreeWidth = this.nodeWidth;
                return node.subtreeWidth;
            }
            let totalWidth = 0;
            children.forEach(child => { totalWidth += calculateWidth(child); });
            totalWidth += (children.length - 1) * this.xGap;
            node.subtreeWidth = Math.max(this.nodeWidth, totalWidth);
            return node.subtreeWidth;
        };

        const placeNode = (node, startX, depth) => {
            node.x = startX + node.subtreeWidth / 2;
            node.y = 40 + depth * this.yGap;

            const children = childrenMap.get(node.id) || [];
            if (children.length === 0) return;

            const childrenWidth = children.reduce((sum, c) => sum + c.subtreeWidth, 0) + (children.length - 1) * this.xGap;
            let childStartX = startX + (node.subtreeWidth - childrenWidth) / 2;

            children.forEach(child => {
                placeNode(child, childStartX, depth + 1);
                childStartX += child.subtreeWidth + this.xGap;
            });
        };

        calculateWidth(root);
        placeNode(root, 0, 0);

        // Защита от слишком больших холстов (браузеры ломаются после 32000px)
        this.canvas.width = Math.min(root.subtreeWidth + 200, 32000);
        this.canvas.height = Math.min((maxDepth + 1) * this.yGap + 200, 32000);
        
        if (this.offsetX === 0 && this.offsetY === 0) {
            const containerWidth = this.canvas.parentElement.clientWidth;
            this.offsetX = (containerWidth - this.canvas.width) / 2 + 100;
            this.offsetY = 20;
            this.applyTransform();
        }
    }

    render() {
        if (this.nodes.length === 0) return;
        
        try {
            this.layout();
        } catch (e) {
            console.error("Ошибка в layout():", e);
            return;
        }

        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        // Рисуем связи
        this.ctx.strokeStyle = '#475569';
        this.ctx.lineWidth = 2;
        this.nodes.forEach(n => {
            if (n.parentId !== null) {
                const p = this.nodes.find(x => x.id === n.parentId);
                if (p) {
                    this.ctx.beginPath();
                    this.ctx.moveTo(p.x, p.y + this.nodeHeight / 2);
                    this.ctx.bezierCurveTo(p.x, p.y + this.nodeHeight, n.x, n.y - this.nodeHeight / 2, n.x, n.y - this.nodeHeight / 2);
                    this.ctx.stroke();
                }
            }
        });

        // Рисуем узлы
        this.nodes.forEach(n => {
            const isBase = n.data.isBase;
            this.ctx.fillStyle = isBase ? '#065f46' : '#1e3a8a';
            this.ctx.strokeStyle = isBase ? '#10b981' : '#3b82f6';
            this.ctx.lineWidth = 2;

            // Безопасная отрисовка скругленного прямоугольника
            const x = n.x - this.nodeWidth / 2;
            const y = n.y - this.nodeHeight / 2;
            const r = 8;
            this.ctx.beginPath();
            this.ctx.moveTo(x + r, y);
            this.ctx.arcTo(x + this.nodeWidth, y, x + this.nodeWidth, y + this.nodeHeight, r);
            this.ctx.arcTo(x + this.nodeWidth, y + this.nodeHeight, x, y + this.nodeHeight, r);
            this.ctx.arcTo(x, y + this.nodeHeight, x, y, r);
            this.ctx.arcTo(x, y, x + this.nodeWidth, y, r);
            this.ctx.closePath();
            this.ctx.fill();
            this.ctx.stroke();

            // Текст
            this.ctx.fillStyle = '#fff';
            this.ctx.textAlign = 'center';
            this.ctx.textBaseline = 'middle';
            this.ctx.font = 'bold 13px monospace';
            this.ctx.fillText(n.data.title, n.x, n.y - 10);
            
            this.ctx.font = '12px monospace';
            this.ctx.fillStyle = isBase ? '#a7f3d0' : '#bfdbfe';
            this.ctx.fillText((n.data.args || '') + " " + (n.data.result || ''), n.x, n.y + 12);
        });
    }

    _bindEvents() {
        const container = this.canvas.parentElement;

        container.addEventListener('wheel', (e) => {
            e.preventDefault();
            const zoomFactor = e.deltaY > 0 ? 0.9 : 1.1;
            this.scale = Math.max(0.1, Math.min(this.scale * zoomFactor, 3));
            this.applyTransform();
        }, { passive: false });

        container.addEventListener('mousedown', (e) => {
            this.isDragging = true;
            this.lastMouseX = e.clientX;
            this.lastMouseY = e.clientY;
            container.style.cursor = 'grabbing';
        });

        window.addEventListener('mousemove', (e) => {
            if (!this.isDragging) return;
            this.offsetX += e.clientX - this.lastMouseX;
            this.offsetY += e.clientY - this.lastMouseY;
            this.lastMouseX = e.clientX;
            this.lastMouseY = e.clientY;
            this.applyTransform();
        });

        window.addEventListener('mouseup', () => { 
            this.isDragging = false; 
            container.style.cursor = 'grab';
        });
        
        container.style.cursor = 'grab';
    }

    applyTransform() {
        this.canvas.style.transform = `translate(${this.offsetX}px, ${this.offsetY}px) scale(${this.scale})`;
    }

    getNodeAt(mouseX, mouseY) {
        const rect = this.canvas.getBoundingClientRect();
        // Вычисляем реальные координаты с учетом масштабирования CSS
        const scaleX = this.canvas.width / rect.width;
        const scaleY = this.canvas.height / rect.height;
        
        const canvasX = (mouseX - rect.left) * scaleX;
        const canvasY = (mouseY - rect.top) * scaleY;

        return this.nodes.find(n => 
            canvasX >= n.x - this.nodeWidth/2 && canvasX <= n.x + this.nodeWidth/2 &&
            canvasY >= n.y - this.nodeHeight/2 && canvasY <= n.y + this.nodeHeight/2
        );
    }
}