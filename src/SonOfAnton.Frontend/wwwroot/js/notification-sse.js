export function startNotificationStream(url, token, dotNetRef) {
    const fullUrl = token ? `${url}?access_token=${token}` : url;
    
    // Query parametresinde token desteği sağlamak için (veya header destekleyen bir polyfill kullanın),
    // standart EventSource başlıkları (Authorization) desteklemez.
    // Genellikle tokenlar SSE için query parametreleri veya cookie'ler aracılığıyla iletilir.
    const eventSource = new EventSource(fullUrl);
    
    eventSource.onmessage = (event) => {
        if (event.data && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnNotificationReceived', event.data);
        }
    };
    
    eventSource.addEventListener('connected', (event) => {
        console.log("Notification SSE Connected", event.data);
    });
    
    eventSource.onerror = (error) => {
        console.error("Notification SSE Hatası", error);
        // EventSource otomatik olarak yeniden bağlanmaya çalışacaktır.
    };
    
    return {
        stop: () => {
            eventSource.close();
        }
    };
}
