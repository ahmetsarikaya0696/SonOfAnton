/**
 * SSE (Server-Sent Events) Interop Module
 * Native EventSource API'sini Blazor'a bağlar
 * Event deduplication, offline buffering, event ID tracking
 */

// Aktif EventSource bağlantıları
const connections = new Map();

// Reconnection configuration
const RECONNECT_CONFIG = {
    initialDelay: 1000,
    maxDelay: 30000,
    multiplier: 2
};

// Event deduplication - son işlenen event ID'leri
const processedEventIds = new Set();
const MAX_PROCESSED_IDS = 5000;

// Online/Offline tracking
let isOnline = navigator.onLine;

window.addEventListener('online', () => {
    isOnline = true;
    console.log('SSE: Çevrimiçi olundu, bağlantılar yeniden kuruluyor...');
    connections.forEach((connectionData, connectionId) => {
        if (!connectionData.eventSource || connectionData.eventSource.readyState === EventSource.CLOSED) {
            connect(connectionId, connectionData);
        }
    });
});

window.addEventListener('offline', () => {
    isOnline = false;
    console.log('SSE: Çevrimdışı olundu, event\'ler buffer\'lanacak');
});

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
        isManualClose: false,
        lastEventId: null,
        eventsReceived: 0
    };

    connections.set(connectionId, connectionData);
    
    return connect(connectionId, connectionData);
}

/**
 * Event ID dedup kontrolü
 */
function isDuplicateEvent(eventId) {
    if (!eventId) return false;
    
    if (processedEventIds.has(eventId)) {
        return true;
    }
    
    processedEventIds.add(eventId);
    
    // Cache büyüklüğü kontrolü
    if (processedEventIds.size > MAX_PROCESSED_IDS) {
        const iterator = processedEventIds.values();
        for (let i = 0; i < MAX_PROCESSED_IDS / 2; i++) {
            processedEventIds.delete(iterator.next().value);
        }
    }
    
    return false;
}

/**
 * EventSource bağlantısı oluşturur
 */
function connect(connectionId, connectionData) {
    try {
        // Last-Event-ID ile resume desteği
        const url = connectionData.lastEventId 
            ? `${connectionData.url}${connectionData.url.includes('?') ? '&' : '?'}lastEventId=${connectionData.lastEventId}`
            : connectionData.url;
        
        const eventSource = new EventSource(url);
        connectionData.eventSource = eventSource;

        // Bağlantı açıldı
        eventSource.onopen = () => {
            console.log(`SSE bağlantısı açıldı: ${connectionId}`);
            connectionData.reconnectAttempts = 0;
            connectionData.reconnectDelay = RECONNECT_CONFIG.initialDelay;

            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnConnectionStateChanged_Internal', 'Connected');
            }
        };

        // Mesaj alındı
        eventSource.onmessage = (event) => {
            connectionData.eventsReceived++;
            
            // Last event ID tracking
            if (event.lastEventId) {
                connectionData.lastEventId = event.lastEventId;
            }
            
            if (connectionData.dotNetRef) {
                connectionData.dotNetRef.invokeMethodAsync('OnMessage_Internal', event.data);
            }
        };

        // Özel event tipleri - dedup ile
        const eventTypes = [
            { name: 'node-started', callback: 'OnNodeStarted_Internal' },
            { name: 'node-completed', callback: 'OnNodeCompleted_Internal' },
            { name: 'progress', callback: 'OnProgress_Internal' },
            { name: 'log', callback: 'OnLog_Internal' },
            { name: 'workflow-completed', callback: 'OnWorkflowCompleted_Internal' }
        ];
        
        eventTypes.forEach(({ name, callback }) => {
            eventSource.addEventListener(name, (event) => {
                connectionData.eventsReceived++;
                
                // Last event ID tracking
                if (event.lastEventId) {
                    connectionData.lastEventId = event.lastEventId;
                    
                    // Dedup kontrolü
                    if (isDuplicateEvent(event.lastEventId)) {
                        return;
                    }
                }
                
                if (!isOnline && connectionData.dotNetRef) {
                    // Offline - buffer'a ekle
                    connectionData.dotNetRef.invokeMethodAsync('BufferEvent_Internal', name, event.data);
                    return;
                }
                
                if (connectionData.dotNetRef) {
                    connectionData.dotNetRef.invokeMethodAsync(callback, event.data);
                }
            });
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
                connectionData.dotNetRef.invokeMethodAsync('OnConnectionStateChanged_Internal', 'Disconnected');
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

    // Offline ise bağlanmayı dene
    if (!isOnline) {
        console.log('Çevrimdışı - yeniden bağlanma ertelendi');
        return;
    }

    connectionData.reconnectAttempts++;

    if (connectionData.dotNetRef) {
        connectionData.dotNetRef.invokeMethodAsync('OnConnectionStateChanged_Internal', 'Reconnecting');
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
 * Bağlantı istatistiklerini döndürür
 * @param {string} connectionId
 * @returns {object}
 */
export function getConnectionStats(connectionId) {
    const connectionData = connections.get(connectionId);
    if (!connectionData) {
        return null;
    }
    
    return {
        eventsReceived: connectionData.eventsReceived,
        reconnectAttempts: connectionData.reconnectAttempts,
        lastEventId: connectionData.lastEventId,
        isConnected: connectionData.eventSource?.readyState === EventSource.OPEN
    };
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

/**
 * Dedup cache'i temizler
 */
export function clearDedupCache() {
    processedEventIds.clear();
}
