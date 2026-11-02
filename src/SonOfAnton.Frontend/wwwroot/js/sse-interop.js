// Fetch sends bearer tokens in headers, never in URLs. Persisted logs have a separate replay endpoint.
const connections = new Map();
const callbacks = { 'node-started':'OnNodeStarted_Internal', 'node-completed':'OnNodeCompleted_Internal', progress:'OnProgress_Internal', log:'OnLog_Internal', 'workflow-completed':'OnWorkflowCompleted_Internal' };
export function connectToStream(id, url, dotNetRef, token) {
    if (connections.has(id)) return false;
    const entry = { controller:new AbortController(), state:'Connecting', eventsReceived:0 };
    connections.set(id,entry);
    void readStream(id,url,dotNetRef,token,entry);
    return true;
}
async function readStream(id,url,ref,token,entry) {
    let reader;
    try {
        const response = await fetch(url,{ headers:{ Authorization:`Bearer ${token}`, Accept:'text/event-stream' },signal:entry.controller.signal });
        if (!response.ok || !response.body) throw new Error(`Stream HTTP ${response.status}`);
        entry.state='Connected';
        await ref.invokeMethodAsync('OnConnectionStateChanged_Internal',entry.state);
        reader=response.body.getReader();
        const decoder=new TextDecoder();
        let buffer='',event='message',data=[],frameSize=0;
        while (true) {
            const chunk=await reader.read();
            if(chunk.done) break;
            buffer+=decoder.decode(chunk.value,{stream:true});
            if(buffer.length+frameSize>1048576) throw new Error('Stream frame exceeds 1 MB');
            let i;
            while((i=buffer.indexOf('\n'))>=0) {
                const line=buffer.slice(0,i).replace(/\r$/,''); buffer=buffer.slice(i+1);
                if(line==='') {
                    if(data.length) {
                        entry.eventsReceived++;
                        await ref.invokeMethodAsync(callbacks[event]??'OnMessage_Internal',data.join('\n'));
                        if(event==='workflow-completed') return;
                    }
                    event='message';data=[];frameSize=0;
                } else if(line.startsWith('event:')) event=line.slice(6).trim();
                else if(line.startsWith('data:')) { data.push(line.slice(5).replace(/^ /,''));frameSize+=line.length; }
            }
        }
    } catch(error) {
        if(error.name!=='AbortError') console.error('Progress stream disconnected',error.message);
    } finally {
        if(reader) { try { await reader.cancel(); } catch {} }
        entry.state='Disconnected';
        if(connections.get(id)===entry) connections.delete(id);
        try { await ref.invokeMethodAsync('OnConnectionStateChanged_Internal',entry.state); } catch {}
    }
}
export function disconnect(id) { const entry=connections.get(id);if(!entry) return false;entry.controller.abort();connections.delete(id);return true; }
export function disconnectAll() { for(const id of connections.keys()) disconnect(id); }
export function getConnectionState(id) { return connections.get(id)?.state??'Disconnected'; }
export function getConnectionStats(id) { const entry=connections.get(id);return entry?{eventsReceived:entry.eventsReceived,isConnected:entry.state==='Connected'}:null; }
