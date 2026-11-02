import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
const source=await readFile(new URL('../../src/SonOfAnton.Frontend/wwwroot/js/sse-interop.js',import.meta.url),'utf8');
const sse=await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
test('header auth and chunked events finish without reconnecting',async()=>{
 let request;const calls=[];
 globalThis.fetch=async(url,options)=>{
  request={url,options};return new Response(new ReadableStream({start(c){
   for(const chunk of ['event: log\r\nda','ta: {"message":"hello"}\r\n\r\n','event: workflow-completed\ndata: {"success":true}\n\n'])c.enqueue(new TextEncoder().encode(chunk));c.close();
  }}));
 };
 let finish;const done=new Promise(r=>{finish=r;});
 const ref={async invokeMethodAsync(method,data){calls.push([method,data]);if(data==='Disconnected')finish();}};
 assert.equal(sse.connectToStream('id','https://example.test/stream',ref,'test-token'),true);
 assert.equal(sse.connectToStream('id','https://example.test/stream',ref,'test-token'),false);
 await done;
 assert.equal(request.options.headers.Authorization,'Bearer test-token');assert.equal(request.url.includes('test-token'),false);
 assert.ok(calls.some(([m,d])=>m==='OnLog_Internal'&&JSON.parse(d).message==='hello'));
 assert.ok(calls.some(([m])=>m==='OnWorkflowCompleted_Internal'));
 assert.equal(sse.getConnectionState('id'),'Disconnected');
});
test('disconnect aborts fetch and removes subscription',async()=>{
 let signal;globalThis.fetch=async(_,options)=>new Promise((resolve,reject)=>{
  signal=options.signal;signal.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')));
 });
 sse.connectToStream('abort','https://example.test/stream',{invokeMethodAsync:async()=>{}},'token');
 assert.equal(sse.disconnect('abort'),true);assert.equal(signal.aborted,true);assert.equal(sse.getConnectionState('abort'),'Disconnected');
});
