/**
 * SSE (Server-Sent Events) Interop Module
 * Native EventSource API'sini Blazor'a bağlar
 */

// Aktif EventSource bağlantıları
const connections = new Map();

// Reconnection configuration
const RECONNECT_CONFIG = {
    initialDelay: 1000,
    maxDelay: 30000,
    multiplier: 2
};

/**
 * SSE stream'e bağlanır
 * @param {string} connectionId - Unique connection ID
 * @param {string} url - SSE endpoint URL
 * @param {object} dotNetRef - C# callback referansı
 * @returns {boolean} - Başarı durumu
 */
export function connectToStream(connectionId, url, dotNetRef) {
    if (connections.has(connectionId)) {
        console.warn(`Connection ${connectionId} zaten mevcut`);
        return false;
    }

    const connectionData = {
        url: url,
        dotNetRef: dotNetRef,
        eventSource: null,
        reconnectAttempts: 0,
        reconnectDelay: RECONNECT_CONFIG.initialDelay,
        isManualClose: false
    };

    connections.set(connectionId, connectionData);
    
    return connect(connectionId, connectionData);
}

/**
 * EventSource bağlantısı oluşturur
 */
function connect(connectionId, connectionData) {
    try {
        const eventSource = new EventSource(connectionData.url);
        connectionData.eventSource = eventSource;

        // Bağlantı açıldı
        eventSource.onopen = () => {
            console.log(`SSE bağlantısı açıldı: ${connectionId}`);
            connectionData.reconnectAttempts = 0;
            connectionData.reconnectDelay = RECONNECT_CONFIG.initialDelay;

            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnConnectionStateChanged', 'Connected');
            }
        };

        // Mesaj alındı
        eventSource.onmessage = (event) => {
            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnMessage', event.data);
            }
        };

        // Özel event tipleri
        eventSource.addEventListener('node-started', (event) => {
            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnNodeStarted', event.data);
            }
        });

        eventSource.addEventListener('node-completed', (event) => {
            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnNodeCompleted', event.data);
            }
        });

        eventSource.addEventListener('progress', (event) => {
            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnProgress', event.data);
            }
        });

        eventSource.addEventListener('log', (event) => {
            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnLog', event.data);
            }
        });

        eventSource.addEventListener('workflow-completed', (event) => {
            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnWorkflowCompleted', event.data);
            }
        });

        eventSource.addEventListener('heartbeat', () => {
            // Heartbeat - bağlantı canlı
        });

        // Hata durumu
        eventSource.onerror = (error) => {
            console.error(`SSE hatası: ${connectionId}`, error);
            
            if (connectionData.isManualClose) {
                return;
            }

            eventSource.close();
            connectionData.eventSource = null;

            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnConnectionStateChanged', 'Disconnected');
            }

            // Yeniden bağlanmayı dene
            scheduleReconnect(connectionId, connectionData);
        };

        return true;
    } catch (error) {
        console.error(`SSE bağlantısı oluşturulamadı: ${connectionId}`, error);
        return false;
    }
}

/**
 * Yeniden bağlanmayı planlar (exponential backoff)
 */
function scheduleReconnect(connectionId, connectionData) {
    if (connectionData.isManualClose) {
        return;
    }

    connectionData.reconnectAttempts++;

    if (connectionData.dotNetRef) {
        connectionData.dotNetRef.invokeMethodAsync('OnConnectionStateChanged', 'Reconnecting');
    }

    console.log(`Yeniden bağlanma denemesi ${connectionData.reconnectAttempts} - ${connectionData.reconnectDelay}ms sonra`);

    setTimeout(() => {
        if (!connectionData.isManualClose && connections.has(connectionId)) {
            connect(connectionId, connectionData);
        }
    }, connectionData.reconnectDelay);

    // Exponential backoff
    connectionData.reconnectDelay = Math.min(
        connectionData.reconnectDelay * RECONNECT_CONFIG.multiplier,
        RECONNECT_CONFIG.maxDelay
    );
}

/**
 * Bağlantıyı kapatır
 * @param {string} connectionId
 */
export function disconnect(connectionId) {
    const connectionData = connections.get(connectionId);
    if (!connectionData) {
        return false;
    }

    connectionData.isManualClose = true;

    if (connectionData.eventSource) {
        connectionData.eventSource.close();
        connectionData.eventSource = null;
    }

    connections.delete(connectionId);
    console.log(`SSE bağlantısı kapatıldı: ${connectionId}`);

    return true;
}

/**
 * Bağlantı durumunu döndürür
 * @param {string} connectionId
 * @returns {string} - 'Connected', 'Connecting', 'Disconnected'
 */
export function getConnectionState(connectionId) {
    const connectionData = connections.get(connectionId);
    if (!connectionData || !connectionData.eventSource) {
        return 'Disconnected';
    }

    switch (connectionData.eventSource.readyState) {
        case EventSource.CONNECTING:
            return 'Connecting';
        case EventSource.OPEN:
            return 'Connected';
        case EventSource.CLOSED:
            return 'Disconnected';
        default:
            return 'Disconnected';
    }
}

/**
 * Tüm bağlantıları kapatır
 */
export function disconnectAll() {
    connections.forEach((_, connectionId) => {
        disconnect(connectionId);
    });
    console.log('Tüm SSE bağlantıları kapatıldı');
}
