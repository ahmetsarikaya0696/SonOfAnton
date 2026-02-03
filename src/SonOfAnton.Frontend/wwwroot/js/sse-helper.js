// SSE Helper for Blazor WebAssembly
window.sseHelper = {
    connections: {},

    connect: function (dotNetRef, connectionId, url) {
        console.log('[SSE] Connecting to:', url);
        
        // Önceki baðlantý varsa kapat
        if (this.connections[connectionId]) {
            this.connections[connectionId].close();
            delete this.connections[connectionId];
        }
        
        const eventSource = new EventSource(url);
        let isCompleted = false;
        
        eventSource.onopen = function () {
            console.log('[SSE] Connection opened');
        };
        
        
        eventSource.onmessage = function (event) {
            if (isCompleted) return;
            
            // Boþluklarý temizle
            const data = event.data.trim();
            console.log('[SSE] Message received:', data.substring(0, 100));
            
            // Boþ mesajlarý ve complete event'in data'sýný atla
            if (!data || data === '{}' || data === 'done') {
                console.log('[SSE] Skipping non-JSON message:', data);
                return;
            }
            
            // JSON olup olmadýðýný kontrol et
            if (!data.startsWith('{')) {
                console.log('[SSE] Skipping non-JSON data');
                return;
            }
            
            try {
                dotNetRef.invokeMethodAsync('OnLogReceived', data);
            } catch (e) {
                console.log('[SSE] Error invoking .NET:', e);
            }
        };
        
        eventSource.addEventListener('complete', function (event) {
            console.log('[SSE] Complete event received');
            isCompleted = true;
            eventSource.close();
            delete window.sseHelper.connections[connectionId];
            try {
                dotNetRef.invokeMethodAsync('OnStreamComplete');
            } catch (e) {
                console.log('[SSE] Error invoking OnStreamComplete:', e);
            }
        });
        
        eventSource.onerror = function (error) {
            console.log('[SSE] Error - readyState:', eventSource.readyState);
            
            // Hata durumunda baðlantýyý kapat ve yeniden baðlanmayý engelle
            if (!isCompleted) {
                isCompleted = true;
                eventSource.close();
                delete window.sseHelper.connections[connectionId];
                try {
                    dotNetRef.invokeMethodAsync('OnStreamComplete');
                } catch (e) {
                    console.log('[SSE] Error invoking OnStreamComplete:', e);
                }
            }
        };
        
        this.connections[connectionId] = eventSource;
    },

    disconnect: function (connectionId) {
        console.log('[SSE] Disconnecting:', connectionId);
        const eventSource = this.connections[connectionId];
        if (eventSource) {
            eventSource.close();
            delete this.connections[connectionId];
        }
    }
};
