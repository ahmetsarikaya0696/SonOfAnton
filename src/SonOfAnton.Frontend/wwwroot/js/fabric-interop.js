/**
 * Fabric.js Interop Module for SonOfAnton Node Editor
 * ES6 Module - C# tarafından IJSRuntime ile kontrol edilir
 */

// Canvas instance referansları
const canvasInstances = new Map();

// Node görsel sabitleri
const NODE_CONFIG = {
    minWidth: 180,
    minHeight: 80,
    headerHeight: 30,
    pinRadius: 6,
    pinSpacing: 24,
    cornerRadius: 8,
    shadowBlur: 15,
    colors: {
        default: '#89b4fa',
        selected: '#f9e2af',
        executing: '#a6e3a1',
        error: '#f38ba8',
        background: '#1e1e2e',
        text: '#cdd6f4',
        pin: {
            input: '#f5c2e7',
            output: '#94e2d5',
            flow: '#fab387'
        }
    }
};

/**
 * Canvas'ı başlatır
 * @param {string} canvasId - Canvas element ID
 * @param {object} dotNetRef - C# callback referansı
 * @returns {boolean} - Başarı durumu
 */
export function initializeCanvas(canvasId, dotNetRef) {
    if (canvasInstances.has(canvasId)) {
        console.warn(`Canvas ${canvasId} zaten başlatılmış`);
        return true;
    }

    const canvasEl = document.getElementById(canvasId);
    if (!canvasEl) {
        console.error(`Canvas element ${canvasId} bulunamadı`);
        return false;
    }

    // Parent container boyutlarını al
    const container = canvasEl.parentElement;
    canvasEl.width = container.clientWidth;
    canvasEl.height = container.clientHeight;

    const canvas = new fabric.Canvas(canvasId, {
        backgroundColor: NODE_CONFIG.colors.background,
        selection: true,
        preserveObjectStacking: true,
        renderOnAddRemove: true,
        allowTouchScrolling: false
    });

    // Canvas meta verisi
    const canvasData = {
        canvas: canvas,
        dotNetRef: dotNetRef,
        nodes: new Map(),
        connections: new Map(),
        zoom: 1,
        panOffset: { x: 0, y: 0 }
    };

    canvasInstances.set(canvasId, canvasData);

    // Event listeners
    setupEventListeners(canvasId, canvasData);

    // Resize observer
    const resizeObserver = new ResizeObserver(entries => {
        for (const entry of entries) {
            canvas.setDimensions({
                width: entry.contentRect.width,
                height: entry.contentRect.height
            });
            canvas.renderAll();
        }
    });
    resizeObserver.observe(container);

    console.log(`Canvas ${canvasId} başarıyla başlatıldı`);
    return true;
}

/**
 * Event listener'ları kurar
 */
function setupEventListeners(canvasId, canvasData) {
    const canvas = canvasData.canvas;
    const dotNetRef = canvasData.dotNetRef;

    // Node seçimi
    canvas.on('selection:created', (e) => {
        const selected = e.selected?.[0];
        if (selected?.nodeId && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnNodeSelected', selected.nodeId);
        }
    });

    canvas.on('selection:updated', (e) => {
        const selected = e.selected?.[0];
        if (selected?.nodeId && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnNodeSelected', selected.nodeId);
        }
    });

    canvas.on('selection:cleared', () => {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnSelectionCleared');
        }
    });

    // Node sürüklemesi tamamlandı
    canvas.on('object:modified', (e) => {
        const obj = e.target;
        if (obj?.nodeId && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnNodeMoved', obj.nodeId, obj.left, obj.top);
        }
    });

    // Mouse wheel zoom
    canvas.on('mouse:wheel', (opt) => {
        const delta = opt.e.deltaY;
        let zoom = canvas.getZoom();
        zoom *= 0.999 ** delta;
        zoom = Math.min(Math.max(0.3, zoom), 3); // 30% - 300% arası

        canvas.zoomToPoint({ x: opt.e.offsetX, y: opt.e.offsetY }, zoom);
        canvasData.zoom = zoom;

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnCanvasZoom', zoom);
        }

        opt.e.preventDefault();
        opt.e.stopPropagation();
    });

    // Pan (Alt + drag veya middle mouse)
    let isPanning = false;
    let lastPosX, lastPosY;

    canvas.on('mouse:down', (opt) => {
        if (opt.e.altKey || opt.e.button === 1) {
            isPanning = true;
            canvas.selection = false;
            lastPosX = opt.e.clientX;
            lastPosY = opt.e.clientY;
        }
    });

    canvas.on('mouse:move', (opt) => {
        if (isPanning) {
            const vpt = canvas.viewportTransform;
            vpt[4] += opt.e.clientX - lastPosX;
            vpt[5] += opt.e.clientY - lastPosY;
            canvas.requestRenderAll();
            lastPosX = opt.e.clientX;
            lastPosY = opt.e.clientY;
        }
    });

    canvas.on('mouse:up', () => {
        isPanning = false;
        canvas.selection = true;
    });

    // ===== TOUCH GESTURES =====
    let lastTouchDistance = 0;
    let lastTouchCenter = null;

    canvas.upperCanvasEl.addEventListener('touchstart', (e) => {
        if (e.touches.length === 2) {
            // Pinch-to-zoom başlangıcı
            e.preventDefault();
            canvas.selection = false;
            lastTouchDistance = getTouchDistance(e.touches);
            lastTouchCenter = getTouchCenter(e.touches);
        }
    }, { passive: false });

    canvas.upperCanvasEl.addEventListener('touchmove', (e) => {
        if (e.touches.length === 2) {
            e.preventDefault();
            const currentDistance = getTouchDistance(e.touches);
            const currentCenter = getTouchCenter(e.touches);

            // Pinch zoom
            if (lastTouchDistance > 0) {
                const scale = currentDistance / lastTouchDistance;
                let zoom = canvas.getZoom() * scale;
                zoom = Math.min(Math.max(0.3, zoom), 3);
                canvas.zoomToPoint(currentCenter, zoom);
                canvasData.zoom = zoom;

                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnCanvasZoom_Internal', zoom);
                }
            }

            // Two-finger pan
            if (lastTouchCenter) {
                const vpt = canvas.viewportTransform;
                vpt[4] += currentCenter.x - lastTouchCenter.x;
                vpt[5] += currentCenter.y - lastTouchCenter.y;
                canvas.requestRenderAll();
            }

            lastTouchDistance = currentDistance;
            lastTouchCenter = currentCenter;
        }
    }, { passive: false });

    canvas.upperCanvasEl.addEventListener('touchend', (e) => {
        if (e.touches.length < 2) {
            lastTouchDistance = 0;
            lastTouchCenter = null;
            canvas.selection = true;
        }
    });

    // ===== CONNECTION DRAG =====
    let isDraggingConnection = false;
    let connectionDragLine = null;
    let connectionSourceNode = null;
    let connectionSourcePin = null;

    canvas.on('mouse:down', (opt) => {
        const target = opt.target;
        if (!target) return;

        // Pin üzerinde mi kontrol et
        if (target.pinId && target.pinType === 'output') {
            isDraggingConnection = true;
            const parentGroup = target.group;
            connectionSourceNode = parentGroup?.nodeId;
            connectionSourcePin = target.pinId;

            const pos = getNodePinPosition(parentGroup, target.pinId, 'output');
            if (pos) {
                connectionDragLine = new fabric.Line(
                    [pos.x, pos.y, pos.x, pos.y],
                    {
                        stroke: NODE_CONFIG.colors.pin.output,
                        strokeWidth: 3,
                        strokeDashArray: [8, 4],
                        selectable: false,
                        evented: false
                    }
                );
                canvas.add(connectionDragLine);
            }
        }
    });

    canvas.on('mouse:move', (opt) => {
        if (isDraggingConnection && connectionDragLine) {
            const pointer = canvas.getPointer(opt.e);
            connectionDragLine.set({ x2: pointer.x, y2: pointer.y });
            canvas.renderAll();
        }
    });

    canvas.on('mouse:up', (opt) => {
        if (isDraggingConnection) {
            isDraggingConnection = false;

            if (connectionDragLine) {
                canvas.remove(connectionDragLine);
                connectionDragLine = null;
            }

            // Hedef pin'i bul
            const target = opt.target;
            if (target && target.pinId && target.pinType === 'input') {
                const targetGroup = target.group;
                if (targetGroup && targetGroup.nodeId && connectionSourceNode &&
                    targetGroup.nodeId !== connectionSourceNode) {
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnConnectionCreated_Internal',
                            connectionSourceNode, connectionSourcePin,
                            targetGroup.nodeId, target.pinId);
                    }
                }
            }

            connectionSourceNode = null;
            connectionSourcePin = null;
            canvas.renderAll();
        }
    });

    // ===== CONTEXT MENU =====
    canvas.on('mouse:down', (opt) => {
        if (opt.e.button === 2) {
            opt.e.preventDefault();
            opt.e.stopPropagation();
            const pointer = canvas.getPointer(opt.e);
            const target = opt.target;

            if (target && target.nodeId && dotNetRef) {
                dotNetRef.invokeMethodAsync('OnContextMenu_Internal',
                    target.nodeId, pointer.x, pointer.y);
            }
        }
    });

    // Sağ tık menüsünü engelle
    canvas.upperCanvasEl.addEventListener('contextmenu', (e) => {
        e.preventDefault();
    });
}

/**
 * Node ekler
 * @param {string} canvasId
 * @param {string} nodeId
 * @param {string} nodeType
 * @param {string} nodeName
 * @param {number} x
 * @param {number} y
 * @param {object} config - { color, icon, inputs: [{name, type}], outputs: [{name, type}] }
 */
export function addNode(canvasId, nodeId, nodeType, nodeName, x, y, config) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) {
        console.error(`Canvas ${canvasId} bulunamadı`);
        return false;
    }

    const canvas = canvasData.canvas;
    const color = config?.color || NODE_CONFIG.colors.default;
    const inputs = config?.inputs || [];
    const outputs = config?.outputs || [];

    // Node yüksekliği hesapla
    const maxPins = Math.max(inputs.length, outputs.length, 1);
    const nodeHeight = NODE_CONFIG.headerHeight + (maxPins * NODE_CONFIG.pinSpacing) + 20;
    const nodeWidth = Math.max(NODE_CONFIG.minWidth, nodeName.length * 8 + 60);

    // Ana node grubu
    const nodeGroup = [];

    // Arka plan (rounded rect)
    const bgRect = new fabric.Rect({
        width: nodeWidth,
        height: nodeHeight,
        fill: color + '20', // Alfa ile
        stroke: color,
        strokeWidth: 2,
        rx: NODE_CONFIG.cornerRadius,
        ry: NODE_CONFIG.cornerRadius,
        originX: 'left',
        originY: 'top'
    });
    nodeGroup.push(bgRect);

    // Header
    const headerRect = new fabric.Rect({
        width: nodeWidth,
        height: NODE_CONFIG.headerHeight,
        fill: color,
        rx: NODE_CONFIG.cornerRadius,
        ry: NODE_CONFIG.cornerRadius,
        originX: 'left',
        originY: 'top'
    });
    nodeGroup.push(headerRect);

    // Header'ın alt köşeleri düzelt
    const headerBottom = new fabric.Rect({
        width: nodeWidth,
        height: NODE_CONFIG.cornerRadius,
        top: NODE_CONFIG.headerHeight - NODE_CONFIG.cornerRadius,
        fill: color,
        originX: 'left',
        originY: 'top'
    });
    nodeGroup.push(headerBottom);

    // Node adı
    const titleText = new fabric.Text(nodeName, {
        left: 10,
        top: 6,
        fontSize: 13,
        fontWeight: 'bold',
        fontFamily: 'Inter, sans-serif',
        fill: '#1e1e2e',
        originX: 'left',
        originY: 'top'
    });
    nodeGroup.push(titleText);

    // Node tipi (küçük yazı)
    const typeText = new fabric.Text(nodeType, {
        left: nodeWidth - 10,
        top: 8,
        fontSize: 10,
        fontFamily: 'Inter, sans-serif',
        fill: '#1e1e2e80',
        originX: 'right',
        originY: 'top'
    });
    nodeGroup.push(typeText);

    // Input pinleri
    inputs.forEach((pin, index) => {
        const pinY = NODE_CONFIG.headerHeight + 15 + (index * NODE_CONFIG.pinSpacing);
        
        // Pin daire
        const pinCircle = new fabric.Circle({
            left: 0,
            top: pinY,
            radius: NODE_CONFIG.pinRadius,
            fill: NODE_CONFIG.colors.pin.input,
            stroke: '#1e1e2e',
            strokeWidth: 2,
            originX: 'center',
            originY: 'center'
        });
        pinCircle.pinId = pin.name;
        pinCircle.pinType = 'input';
        nodeGroup.push(pinCircle);

        // Pin etiketi
        const pinLabel = new fabric.Text(pin.name, {
            left: 15,
            top: pinY,
            fontSize: 11,
            fontFamily: 'Inter, sans-serif',
            fill: NODE_CONFIG.colors.text,
            originX: 'left',
            originY: 'center'
        });
        nodeGroup.push(pinLabel);
    });

    // Output pinleri
    outputs.forEach((pin, index) => {
        const pinY = NODE_CONFIG.headerHeight + 15 + (index * NODE_CONFIG.pinSpacing);
        
        // Pin daire
        const pinCircle = new fabric.Circle({
            left: nodeWidth,
            top: pinY,
            radius: NODE_CONFIG.pinRadius,
            fill: pin.type === 'flow' ? NODE_CONFIG.colors.pin.flow : NODE_CONFIG.colors.pin.output,
            stroke: '#1e1e2e',
            strokeWidth: 2,
            originX: 'center',
            originY: 'center'
        });
        pinCircle.pinId = pin.name;
        pinCircle.pinType = 'output';
        nodeGroup.push(pinCircle);

        // Pin etiketi
        const pinLabel = new fabric.Text(pin.name, {
            left: nodeWidth - 15,
            top: pinY,
            fontSize: 11,
            fontFamily: 'Inter, sans-serif',
            fill: NODE_CONFIG.colors.text,
            originX: 'right',
            originY: 'center'
        });
        nodeGroup.push(pinLabel);
    });

    // Grubu oluştur
    const group = new fabric.Group(nodeGroup, {
        left: x,
        top: y,
        hasControls: false,
        hasBorders: true,
        borderColor: NODE_CONFIG.colors.selected,
        borderScaleFactor: 2,
        lockScalingX: true,
        lockScalingY: true,
        lockRotation: true,
        subTargetCheck: true,
        shadow: new fabric.Shadow({
            color: 'rgba(0,0,0,0.3)',
            blur: NODE_CONFIG.shadowBlur,
            offsetX: 0,
            offsetY: 5
        })
    });

    // Node metadata
    group.nodeId = nodeId;
    group.nodeType = nodeType;
    group.nodeName = nodeName;
    group.nodeConfig = config;

    // Canvas'a ekle
    canvas.add(group);
    canvasData.nodes.set(nodeId, group);

    canvas.renderAll();
    return true;
}

/**
 * Node pozisyonunu günceller
 */
export function setNodePosition(canvasId, nodeId, x, y) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const node = canvasData.nodes.get(nodeId);
    if (!node) return false;

    node.set({ left: x, top: y });
    node.setCoords();
    canvasData.canvas.renderAll();
    return true;
}

/**
 * Node'un çalışma durumunu günceller (glow efekti)
 */
export function setExecutingState(canvasId, nodeId, isExecuting) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const node = canvasData.nodes.get(nodeId);
    if (!node) return false;

    if (isExecuting) {
        // Animasyonlu glow efekti
        node.set({
            shadow: new fabric.Shadow({
                color: NODE_CONFIG.colors.executing,
                blur: 25,
                offsetX: 0,
                offsetY: 0
            })
        });
        
        // Pulse animasyonu
        node._pulseAnimation = setInterval(() => {
            const currentBlur = node.shadow?.blur || 25;
            const newBlur = currentBlur === 25 ? 35 : 25;
            node.set({
                shadow: new fabric.Shadow({
                    color: NODE_CONFIG.colors.executing,
                    blur: newBlur,
                    offsetX: 0,
                    offsetY: 0
                })
            });
            canvasData.canvas.renderAll();
        }, 500);
    } else {
        // Animasyonu durdur ve normal görünüme dön
        if (node._pulseAnimation) {
            clearInterval(node._pulseAnimation);
            node._pulseAnimation = null;
        }
        node.set({
            shadow: new fabric.Shadow({
                color: 'rgba(0,0,0,0.3)',
                blur: NODE_CONFIG.shadowBlur,
                offsetX: 0,
                offsetY: 5
            })
        });
    }

    canvasData.canvas.renderAll();
    return true;
}

/**
 * Node'u hata durumuna geçirir
 */
export function setErrorState(canvasId, nodeId, hasError) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const node = canvasData.nodes.get(nodeId);
    if (!node) return false;

    if (hasError) {
        node.set({
            shadow: new fabric.Shadow({
                color: NODE_CONFIG.colors.error,
                blur: 20,
                offsetX: 0,
                offsetY: 0
            })
        });
    } else {
        node.set({
            shadow: new fabric.Shadow({
                color: 'rgba(0,0,0,0.3)',
                blur: NODE_CONFIG.shadowBlur,
                offsetX: 0,
                offsetY: 5
            })
        });
    }

    canvasData.canvas.renderAll();
    return true;
}

/**
 * İki node arasına bağlantı çizer
 */
export function connectNodes(canvasId, connectionId, sourceNodeId, targetNodeId, sourcePort, targetPort) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const sourceNode = canvasData.nodes.get(sourceNodeId);
    const targetNode = canvasData.nodes.get(targetNodeId);

    if (!sourceNode || !targetNode) {
        console.error('Kaynak veya hedef node bulunamadı');
        return false;
    }

    // Pin pozisyonlarını bul
    const sourcePos = getNodePinPosition(sourceNode, sourcePort, 'output');
    const targetPos = getNodePinPosition(targetNode, targetPort, 'input');

    if (!sourcePos || !targetPos) {
        console.error('Pin pozisyonları bulunamadı');
        return false;
    }

    // Bezier curve oluştur
    const connection = createConnectionLine(sourcePos, targetPos);
    connection.connectionId = connectionId;
    connection.sourceNodeId = sourceNodeId;
    connection.targetNodeId = targetNodeId;
    connection.sourcePort = sourcePort;
    connection.targetPort = targetPort;
    connection.selectable = true;
    connection.evented = true;

    canvasData.canvas.add(connection);
    canvasData.canvas.sendToBack(connection);
    canvasData.connections.set(connectionId, connection);
    canvasData.canvas.renderAll();

    return true;
}

/**
 * Node pin pozisyonunu hesaplar
 */
function getNodePinPosition(nodeGroup, pinName, pinType) {
    const objects = nodeGroup.getObjects();
    const pin = objects.find(obj => obj.pinId === pinName && obj.pinType === pinType);
    
    if (!pin) return null;

    // Grup içindeki pozisyonu global pozisyona dönüştür
    const groupMatrix = nodeGroup.calcTransformMatrix();
    const point = fabric.util.transformPoint(
        { x: pin.left, y: pin.top },
        groupMatrix
    );

    return { x: point.x, y: point.y };
}

/**
 * Touch yardımcı: iki parmak arası mesafe
 */
function getTouchDistance(touches) {
    const dx = touches[0].clientX - touches[1].clientX;
    const dy = touches[0].clientY - touches[1].clientY;
    return Math.sqrt(dx * dx + dy * dy);
}

/**
 * Touch yardımcı: iki parmak merkez noktası
 */
function getTouchCenter(touches) {
    return {
        x: (touches[0].clientX + touches[1].clientX) / 2,
        y: (touches[0].clientY + touches[1].clientY) / 2
    };
}

/**
 * Bezier connection line oluşturur
 */
function createConnectionLine(start, end) {
    // Kontrol noktaları (yumuşak curve için)
    const dx = Math.abs(end.x - start.x) * 0.5;
    
    const path = new fabric.Path(
        `M ${start.x} ${start.y} C ${start.x + dx} ${start.y}, ${end.x - dx} ${end.y}, ${end.x} ${end.y}`,
        {
            fill: '',
            stroke: NODE_CONFIG.colors.pin.output,
            strokeWidth: 3,
            strokeLineCap: 'round',
            selectable: false,
            evented: false,
            objectCaching: false
        }
    );

    return path;
}

/**
 * Bağlantıyı kaldırır
 */
export function removeConnection(canvasId, connectionId) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const connection = canvasData.connections.get(connectionId);
    if (!connection) return false;

    canvasData.canvas.remove(connection);
    canvasData.connections.delete(connectionId);
    canvasData.canvas.renderAll();

    return true;
}

/**
 * Node'u kaldırır
 */
export function removeNode(canvasId, nodeId) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const node = canvasData.nodes.get(nodeId);
    if (!node) return false;

    // Node'a bağlı connection'ları kaldır
    const connectionsToRemove = [];
    canvasData.connections.forEach((conn, id) => {
        if (conn.sourceNodeId === nodeId || conn.targetNodeId === nodeId) {
            connectionsToRemove.push(id);
        }
    });

    connectionsToRemove.forEach(id => removeConnection(canvasId, id));

    canvasData.canvas.remove(node);
    canvasData.nodes.delete(nodeId);
    canvasData.canvas.renderAll();

    return true;
}

/**
 * Canvas'ı temizler (tüm node ve bağlantıları siler)
 */
export function clearCanvas(canvasId) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    canvasData.canvas.clear();
    canvasData.canvas.backgroundColor = NODE_CONFIG.colors.background;
    canvasData.nodes.clear();
    canvasData.connections.clear();
    canvasData.canvas.renderAll();

    return true;
}

/**
 * Zoom seviyesini ayarlar
 */
export function setZoom(canvasId, zoom) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    zoom = Math.min(Math.max(0.3, zoom), 3);
    canvasData.canvas.setZoom(zoom);
    canvasData.zoom = zoom;
    canvasData.canvas.renderAll();

    return true;
}

/**
 * Canvas'ı belirli bir noktaya kaydırır
 */
export function panTo(canvasId, x, y) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const vpt = canvasData.canvas.viewportTransform;
    vpt[4] = x;
    vpt[5] = y;
    canvasData.canvas.requestRenderAll();

    return true;
}

/**
 * Tüm node'ları sığdırır
 */
export function fitToScreen(canvasId) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const canvas = canvasData.canvas;
    const objects = canvas.getObjects();

    if (objects.length === 0) return true;

    // Bounding box hesapla
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    objects.forEach(obj => {
        const bounds = obj.getBoundingRect();
        minX = Math.min(minX, bounds.left);
        minY = Math.min(minY, bounds.top);
        maxX = Math.max(maxX, bounds.left + bounds.width);
        maxY = Math.max(maxY, bounds.top + bounds.height);
    });

    const padding = 50;
    const contentWidth = maxX - minX + padding * 2;
    const contentHeight = maxY - minY + padding * 2;

    const scaleX = canvas.width / contentWidth;
    const scaleY = canvas.height / contentHeight;
    const scale = Math.min(scaleX, scaleY, 1);

    canvas.setViewportTransform([1, 0, 0, 1, 0, 0]);
    canvas.setZoom(scale);
    canvasData.zoom = scale;

    // Merkeze al
    const centerX = (canvas.width - contentWidth * scale) / 2 - minX * scale + padding * scale;
    const centerY = (canvas.height - contentHeight * scale) / 2 - minY * scale + padding * scale;
    
    const vpt = canvas.viewportTransform;
    vpt[4] = centerX;
    vpt[5] = centerY;

    canvas.requestRenderAll();
    return true;
}

/**
 * Canvas'ı dispose eder
 */
export function dispose(canvasId) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return;

    // Tüm animasyonları durdur
    canvasData.nodes.forEach(node => {
        if (node._pulseAnimation) {
            clearInterval(node._pulseAnimation);
        }
    });

    canvasData.canvas.dispose();
    canvasInstances.delete(canvasId);
    console.log(`Canvas ${canvasId} dispose edildi`);
}

/**
 * Connection'ları günceller (node hareket ettiğinde)
 */
export function updateConnections(canvasId, nodeId) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const node = canvasData.nodes.get(nodeId);
    if (!node) return false;

    // Bu node'a bağlı tüm connection'ları güncelle
    canvasData.connections.forEach((conn, id) => {
        if (conn.sourceNodeId === nodeId || conn.targetNodeId === nodeId) {
            const sourceNode = canvasData.nodes.get(conn.sourceNodeId);
            const targetNode = canvasData.nodes.get(conn.targetNodeId);
            
            if (sourceNode && targetNode) {
                const sourcePos = getNodePinPosition(sourceNode, conn.sourcePort, 'output');
                const targetPos = getNodePinPosition(targetNode, conn.targetPort, 'input');
                
                if (sourcePos && targetPos) {
                    const dx = Math.abs(targetPos.x - sourcePos.x) * 0.5;
                    conn.set({
                        path: [
                            ['M', sourcePos.x, sourcePos.y],
                            ['C', sourcePos.x + dx, sourcePos.y, targetPos.x - dx, targetPos.y, targetPos.x, targetPos.y]
                        ]
                    });
                }
            }
        }
    });

    canvasData.canvas.renderAll();
    return true;
}

// ===== BATCH OPERATIONS =====

/**
 * Birden fazla node'u tek seferde ekler (performans)
 */
export function addNodesBatch(canvasId, nodesArray) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    canvasData.canvas.renderOnAddRemove = false;

    // Batch işlem
    fabric.util.enlivenObjects(nodesArray, (enlivenedObjects) => {
        enlivenedObjects.forEach((obj, index) => {
            const data = nodesArray[index];
            // Burada normal addNode mantığını kullanmak daha güvenli çünkü karmaşık bir grup structure var
            addNode(canvasId, data.nodeId, data.nodeType, data.nodeName, data.x, data.y, data.config);
        });
        
        canvasData.canvas.renderOnAddRemove = true;
        canvasData.canvas.renderAll();
    });

    return true;
}

/**
 * Birden fazla connection'ı tek seferde ekler (performans)
 */
export function connectNodesBatch(canvasId, connectionsArray) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    canvasData.canvas.renderOnAddRemove = false;

    for (const conn of connectionsArray) {
        connectNodes(canvasId, conn.connectionId, conn.sourceNodeId,
                     conn.targetNodeId, conn.sourcePort, conn.targetPort);
    }

    canvasData.canvas.renderOnAddRemove = true;
    canvasData.canvas.renderAll();
    return true;
}

// ===== VIEWPORT STATE =====

/**
 * Mevcut viewport durumunu döndürür
 */
export function getViewportState(canvasId) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return null;

    const vpt = canvasData.canvas.viewportTransform;
    return {
        zoom: canvasData.canvas.getZoom(),
        panX: vpt[4],
        panY: vpt[5]
    };
}

/**
 * Viewport durumunu geri yükler
 */
export function setViewportState(canvasId, state) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    if (state.zoom) {
        canvasData.canvas.setZoom(state.zoom);
        canvasData.zoom = state.zoom;
    }
    const vpt = canvasData.canvas.viewportTransform;
    vpt[4] = state.panX || 0;
    vpt[5] = state.panY || 0;
    canvasData.canvas.requestRenderAll();
    return true;
}

// ===== GRID SNAPPING =====

/**
 * Grid snapping'i etkinleştirir/devre dışı bırakır
 */
export function setGridSnapping(canvasId, enabled, gridSize) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    canvasData.gridSnapping = enabled;
    canvasData.gridSize = gridSize || 20;

    if (enabled) {
        // Event listener daha önce eklenmiş mi kontrol etmemiz lazım aslında
        // Ama basitçe object:moving üzerinde işlem yapabiliriz
        if (!canvasData._gridSnapHandler) {
            canvasData._gridSnapHandler = (e) => {
                if (canvasData.gridSnapping) {
                    const obj = e.target;
                    obj.set({
                        left: Math.round(obj.left / canvasData.gridSize) * canvasData.gridSize,
                        top: Math.round(obj.top / canvasData.gridSize) * canvasData.gridSize
                    });
                }
            };
            canvasData.canvas.on('object:moving', canvasData._gridSnapHandler);
        }
    } else {
        if (canvasData._gridSnapHandler) {
            canvasData.canvas.off('object:moving', canvasData._gridSnapHandler);
            canvasData._gridSnapHandler = null;
        }
    }

    canvasData.canvas.renderAll();
    return true;
}

// ===== NEW FEATURES =====

/**
 * Canvas'a sticky note ekler
 */
export function addNote(canvasId, noteId, content, x, y, color) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const width = 200;
    const height = 150;

    const rect = new fabric.Rect({
        width: width,
        height: height,
        fill: color,
        stroke: '#00000020',
        strokeWidth: 1,
        rx: 2,
        ry: 2,
        shadow: new fabric.Shadow({ color: 'rgba(0,0,0,0.2)', blur: 5, offsetX: 2, offsetY: 2 })
    });

    // Textbox düzenlenebilir
    const text = new fabric.Textbox(content, {
        width: width - 20,
        height: height - 20,
        fontSize: 14,
        fontFamily: 'Inter, sans-serif',
        fill: '#4c4f69',
        left: 10,
        top: 10,
        splitByGrapheme: true,
        editable: true
    });

    const group = new fabric.Group([rect, text], {
        left: x,
        top: y,
        subTargetCheck: true,
        hasBorders: true,
        borderColor: '#f9e2af',
        cornerColor: '#f9e2af',
        cornerSize: 8,
        transparentCorners: false
    });

    group.noteId = noteId;
    group.isNote = true;

    canvasData.canvas.add(group);
    canvasData.canvas.sendToBack(group); 
    canvasData.canvas.renderAll();
    return true;
}

/**
 * Canvas'a grup ekler
 */
export function addGroup(canvasId, groupId, name, x, y, width, height, color) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const rect = new fabric.Rect({
        width: width,
        height: height,
        fill: color + '20', // Low opacity
        stroke: color,
        strokeWidth: 2,
        strokeDashArray: [5, 5],
        rx: 10,
        ry: 10
    });

    const text = new fabric.Text(name, {
        fontSize: 16,
        fontFamily: 'Inter, sans-serif',
        fontWeight: 'bold',
        fill: color,
        left: 10,
        top: -25
    });

    const group = new fabric.Group([rect, text], {
        left: x,
        top: y,
        subTargetCheck: true,
        hasBorders: true,
        borderColor: color,
        cornerColor: color,
        cornerSize: 8,
        transparentCorners: false
    });

    group.groupId = groupId;
    group.isGroup = true;

    canvasData.canvas.add(group);
    canvasData.canvas.sendToBack(group); 
    canvasData.canvas.renderAll();
    return true;
}

/**
 * Node ismini günceller
 */
export function updateNodeName(canvasId, nodeId, newName) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const node = canvasData.nodes.get(nodeId);
    if (!node) return false;
    
    // Group nesneleri
    const objects = node.getObjects();
    // Node başlığı 13px bold, text tipinde
    const titleObj = objects.find(o => o.type === 'text' && o.fontSize === 13 && o.fontWeight === 'bold');

    if (titleObj) {
        titleObj.set('text', newName);
        node.addWithUpdate(); // Grubu güncelle
        canvasData.canvas.renderAll();
        return true;
    }
    
    return false;
}

/**
 * Node bypass durumunu görselleştirir
 */
export function setNodeBypass(canvasId, nodeId, isBypassed) {
    const canvasData = canvasInstances.get(canvasId);
    if (!canvasData) return false;

    const node = canvasData.nodes.get(nodeId);
    if (!node) return false;

    if (isBypassed) {
        node.set('opacity', 0.5);
    } else {
        node.set('opacity', 1.0);
    }
    
    canvasData.canvas.renderAll();
    return true;
}
