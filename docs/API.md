# ?? SonOfAnton — API Referansý

> **Bu belge kime hitap ediyor?**  
> Frontend geliþtiren, API test eden veya dýþ entegrasyon yapan herkes.
> Her endpoint'i örnekleriyle birlikte bulabilirsin.

---

## ?? Ýçindekiler

1. [Genel Bilgiler](#genel-bilgiler)
2. [Workflow API](#workflow-api)
3. [Execution API](#execution-api)
4. [Node API](#node-api)
5. [Webhook API](#webhook-api)
6. [Log Stream API (SSE)](#log-stream-api-sse)
7. [Health Check API](#health-check-api)
8. [Hata Kodlarý](#hata-kodlarý)

---

## Genel Bilgiler

| Özellik | Deðer |
|---------|-------|
| Base URL | `https://localhost:5001/api` |
| Format | JSON (`application/json`) |
| Rate Limit | 100 istek/dakika (IP baþýna) |
| OpenAPI | `GET /openapi/v1.json` (Development modunda) |

**Ortak HTTP Durum Kodlarý:**

| Kod | Anlamý | Ne zaman döner? |
|-----|--------|-----------------|
| 200 | OK | Ýþlem baþarýlý |
| 201 | Created | Yeni kaynak oluþturuldu |
| 204 | No Content | Silme baþarýlý |
| 400 | Bad Request | Geçersiz istek verisi |
| 404 | Not Found | Kaynak bulunamadý |
| 429 | Too Many Requests | Rate limit aþýldý |
| 500 | Server Error | Sunucu hatasý |

---

## Workflow API

Workflow'lar, node'lar ve baðlantýlardan oluþan iþ akýþý þablonlarýdýr.

### Tüm Workflow'larý Listele

```http
GET /api/workflow
```

**Yanýt:** `200 OK`
```json
[
  {
    "id": "a1b2c3d4-...",
    "name": "Müþteri Hoþgeldin Akýþý",
    "description": "Yeni müþteriye hoþgeldin e-postasý gönderir",
    "isActive": true,
    "nodes": [...],
    "connections": [...],
    "createdAt": "2025-01-24T10:00:00Z",
    "updatedAt": "2025-01-24T12:30:00Z"
  }
]
```

### Tek Workflow Getir

```http
GET /api/workflow/{id}
```

| Parametre | Tip | Zorunlu | Açýklama |
|-----------|-----|---------|----------|
| `id` | GUID | Evet | Workflow ID'si |

**Yanýt:** `200 OK` veya `404 Not Found`

### Workflow Oluþtur

```http
POST /api/workflow
Content-Type: application/json

{
  "name": "Yeni Ýþ Akýþý",
  "description": "Açýklama",
  "nodes": [],
  "connections": []
}
```

**Yanýt:** `201 Created`
```json
{
  "id": "yeni-guid-...",
  "name": "Yeni Ýþ Akýþý",
  "createdAt": "2025-01-24T10:00:00Z",
  "updatedAt": "2025-01-24T10:00:00Z"
}
```

### Workflow Güncelle

```http
PUT /api/workflow/{id}
Content-Type: application/json

{
  "name": "Güncellenmiþ Ad",
  "description": "Güncellenmiþ açýklama",
  "nodes": [...],
  "connections": [...]
}
```

**Yanýt:** `200 OK` veya `404 Not Found`

### Workflow Sil

```http
DELETE /api/workflow/{id}
```

**Yanýt:** `204 No Content` veya `404 Not Found`

### Workflow Çalýþtýr

```http
POST /api/workflow/{id}/execute
Content-Type: application/json

{
  "tetikleme": "verisi",
  "key": "value"
}
```

**Yanýt:** `200 OK`
```json
{
  "message": "Workflow execution started",
  "workflowId": "a1b2c3d4-...",
  "executionId": "e5f6g7h8-..."
}
```

> **Not:** triggerData opsiyoneldir. Body boþ gönderilebilir.

### Workflow Aktifleþtir / Devre Dýþý Býrak

```http
POST /api/workflow/{id}/activate
POST /api/workflow/{id}/deactivate
```

**Yanýt:** `200 OK` — Güncellenmiþ workflow döner.

---

## Execution API

Execution, bir workflow'un tek seferlik çalýþtýrýlmasýdýr.

### Execution'larý Listele

```http
GET /api/executions
GET /api/executions?workflowId={workflowId}
```

| Parametre | Tip | Zorunlu | Açýklama |
|-----------|-----|---------|----------|
| `workflowId` | GUID | Hayýr | Belirli workflow'a ait execution'lar |

**Yanýt:** `200 OK` — Son 100 execution (tarih sýrasýyla)
```json
[
  {
    "id": "exec-guid-...",
    "workflowId": "wf-guid-...",
    "workflowName": "Müþteri Akýþý",
    "status": "Completed",
    "startedAt": "2025-01-24T10:00:00Z",
    "completedAt": "2025-01-24T10:00:05Z"
  }
]
```

**Durum Deðerleri:** `Queued`, `Running`, `Completed`, `Failed`, `Cancelled`

### Execution Detayý

```http
GET /api/executions/{id}
```

### Execution Loglarý

```http
GET /api/executions/{id}/logs
```

**Yanýt:** `200 OK`
```json
[
  {
    "executionId": "exec-guid-...",
    "nodeId": "node-guid-...",
    "level": "Info",
    "message": "Koþul deðerlendiriliyor: status == active",
    "timestamp": "2025-01-24T10:00:01Z"
  }
]
```

### Execution Ýptal Et

```http
POST /api/executions/{id}/cancel
```

**Yanýt:** `200 OK` veya `400 Bad Request` (zaten tamamlanmýþsa)

### Workflow'u Hemen Çalýþtýr

```http
POST /api/executions/run/{workflowId}
```

**Yanýt:** `200 OK`
```json
{
  "executionId": "exec-guid-...",
  "message": "Workflow kuyruða eklendi"
}
```

### Gecikmeli Çalýþtýr

```http
POST /api/executions/schedule/{workflowId}?delaySeconds=60
```

### Tekrarlayan Workflow Ekle

```http
POST /api/executions/recurring/{workflowId}?cron=0 * * * *&jobName=saatlik-rapor
```

| Parametre | Tip | Zorunlu | Açýklama |
|-----------|-----|---------|----------|
| `cron` | string | Evet | Cron ifadesi (5 parça: dakika saat gün ay haftaGünü) |
| `jobName` | string | Hayýr | Ýþ adý (varsayýlan: workflow-{workflowId}) |

**Cron Örnekleri:**

| Ýfade | Anlamý |
|-------|--------|
| `* * * * *` | Her dakika |
| `0 * * * *` | Her saat baþý |
| `0 9 * * 1-5` | Hafta içi her gün saat 09:00 |
| `0 0 1 * *` | Her ayýn 1'i gece yarýsý |

### Tekrarlayan Workflow Kaldýr

```http
DELETE /api/executions/recurring/{jobName}
```

### Cron Ýfadesi Doðrula

```http
POST /api/executions/validate-cron
Content-Type: application/json

{
  "cronExpression": "0 9 * * 1-5"
}
```

**Yanýt:** `200 OK`
```json
{
  "isValid": true,
  "description": "Hafta içi her gün 09:00'da"
}
```

### Job Bilgisi

```http
GET /api/executions/job/{jobId}
```

---

## Node API

Node'lar, workflow'larýn yapý taþlarýdýr.

### Tüm Node Tiplerini Listele

```http
GET /api/nodes
```

**Yanýt:** `200 OK`
```json
[
  {
    "type": "logic.if",
    "name": "Koþul (IF)",
    "icon": "git-branch",
    "category": "Mantik",
    "description": "Koþula göre akýþý dallandýr",
    "color": "#f9e2af",
    "inputs": [{"name": "input", "type": "any"}],
    "outputs": [
      {"name": "true", "type": "any"},
      {"name": "false", "type": "any"}
    ],
    "parameters": [
      {
        "name": "condition",
        "displayName": "Koþul",
        "type": "string",
        "required": true,
        "placeholder": "status == active"
      }
    ]
  }
]
```

### Tek Node Tipi Detayý

```http
GET /api/nodes/{type}
```

| Parametre | Tip | Zorunlu | Açýklama |
|-----------|-----|---------|----------|
| `type` | string | Evet | Node tipi (ör: `logic.if`, `transform.set`) |

---

## Webhook API

Dýþ servislerden workflow tetiklemek için kullanýlýr.

### Webhook ile Tetikle

```http
POST /api/webhook/{webhookId}
Content-Type: application/json
X-Webhook-Signature: sha256=abc123...  (opsiyonel)

{
  "event": "order.created",
  "data": { "orderId": 123 }
}
```

**Yanýt:** `200 OK`
```json
{
  "message": "Workflow triggered",
  "executionId": "exec-guid-..."
}
```

**Hata Durumlarý:**
- `404` — Webhook bulunamadý veya aktif deðil
- `401` — Signature geçersiz (secret tanýmlýysa)
- `429` — Rate limit (min interval süresi dolmamýþ)

---

## Log Stream API (SSE)

Gerçek zamanlý log takibi için Server-Sent Events kullanýlýr.

### Execution Event Stream

```http
GET /api/workflow/execution/{executionId}/stream
GET /api/workflow/execution/{executionId}/stream?topics=node,log
```

| Parametre | Tip | Zorunlu | Açýklama |
|-----------|-----|---------|----------|
| `executionId` | GUID | Evet | Execution ID'si |
| `topics` | string | Hayýr | Virgülle ayrýlmýþ topic filtresi |

**Yanýt:** `200 OK` (text/event-stream)
```
: connected session=abc123

data: {"type":"node_started","nodeId":"...","timestamp":"..."}

data: {"type":"node_completed","nodeId":"...","output":{}}

: heartbeat

data: {"type":"workflow_completed"}
```

**JavaScript ile kullaným:**
```javascript
const source = new EventSource('/api/workflow/execution/' + executionId + '/stream');

source.onmessage = (event) => {
    const data = JSON.parse(event.data);
    console.log('Event:', data.type, data);
};

source.onerror = () => {
    console.log('Baðlantý kesildi, yeniden deneniyor...');
};
```

### Execution Log Stream

```http
GET /api/logs/{executionId}
```

**Yanýt:** `200 OK` (text/event-stream)

### SSE Ýstatistikleri

```http
GET /api/sse/stats
```

**Yanýt:** `200 OK`
```json
{
  "activeChannels": 3,
  "totalBytesTransferred": 15360,
  "activeSessions": 2
}
```

---

## Health Check API

Uygulamanýn saðlýk durumunu kontrol eder.

### Canlýlýk Kontrolü (Liveness)

```http
GET /health/live
```

**Yanýt:** `200 OK`
```json
{
  "status": "alive",
  "timestamp": "2025-01-24T10:00:00Z"
}
```

> Uygulama ayakta mý? (Veritabaný baðlantýsýný kontrol ETMEZ)

### Hazýrlýk Kontrolü (Readiness)

```http
GET /health/ready
```

**Yanýt:** `200 OK`
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Healthy",
      "description": null,
      "duration": 12.5
    },
    {
      "name": "hangfire",
      "status": "Healthy",
      "description": "Hangfire baðlý",
      "duration": 0.3
    }
  ],
  "totalDuration": 15.2
}
```

> Uygulama trafiðe hazýr mý? (PostgreSQL + Hangfire kontrol eder)

---

## Hata Kodlarý

Tüm hata yanýtlarý aþaðýdaki formattadýr:

```json
{
  "error": "Hata açýklamasý",
  "requestId": "abc123-trace-id"
}
```

| Hata Mesajý | Endpoint | Çözüm |
|-------------|----------|-------|
| "Workflow not found" | Workflow API | ID'nin doðru olduðunu kontrol et |
| "Bu execution iptal edilemez" | Cancel | Sadece Running/Queued durumundakiler iptal edilebilir |
| "Webhook not found or inactive" | Webhook | webhookId ve aktiflik durumunu kontrol et |
| "Invalid signature" | Webhook | X-Webhook-Signature header'ýný kontrol et |
| "Too many requests" | Rate limited | Bekle ve tekrar dene |
| "Sunucu hatasý oluþtu" | Herhangi | requestId ile log'larý incele |

---

---

## Folder API

Workflow'lari hiyerarsik klasorlerle organize etmek icin kullanilir.

### Tum Klasorleri Listele

```http
GET /api/folders
```

**Yanit:** `200 OK` - Duz klasor listesi

### Klasor Agaci

```http
GET /api/folders/tree
```

**Yanit:** `200 OK` - Hiyerarsik agac yapisi (children icice)

### Klasor Olustur

```http
POST /api/folders
Content-Type: application/json

{
  "name": "Pazarlama Akislari",
  "icon": "briefcase",
  "color": "#89b4fa",
  "parentFolderId": null
}
```

### Klasor Guncelle

```http
PUT /api/folders/{id}
```

### Klasor Sil

```http
DELETE /api/folders/{id}
```

### Klasordeki Workflow'lar

```http
GET /api/folders/{id}/workflows
```

### Workflow'u Klasore Tasi

```http
PATCH /api/workflows/{id}/move
Content-Type: application/json

{
  "folderId": "folder-guid-..."
}
```

---

## Workflow Chat API

AI asistan ile dogal dilde workflow olusturma ve duzenleme.

### Chat Mesaji Gonder

```http
POST /api/workflow/chat
Content-Type: application/json

{
  "message": "Bir webhook ile tetiklenen, gelen veriyi LLM'e gonderip sonucu email atan workflow olustur",
  "history": [],
  "currentGraph": null
}
```

**Yanit:** `200 OK`
```json
{
  "message": "Iste onerdigim workflow...",
  "suggestedGraph": { "nodes": [...], "connections": [...] },
  "yamlPreview": "workflow:\n  name: ..."
}
```

### YAML Import

```http
POST /api/workflow/chat/import-yaml
Content-Type: application/json

{
  "yamlContent": "workflow:\n  name: Test..."
}
```

**Yanit:** `200 OK`
```json
{
  "isValid": true,
  "errors": [],
  "parsedGraph": { "nodes": [...], "connections": [...] }
}
```

### YAML Export

```http
POST /api/workflow/chat/export-yaml
Content-Type: application/json

{ "nodes": [...], "connections": [...] }
```

**Yanit:** `200 OK`
```json
{
  "type": "Yaml",
  "content": "workflow:\n  ...",
  "fileName": "workflow.yaml"
}
```

---

## Search API

Global arama - workflow, log ve daha fazlasinda metin arar.

### Arama

```http
GET /api/search?q=email&type=workflow,log&page=1&pageSize=20
```

| Parametre | Tip | Zorunlu | Aciklama |
|-----------|-----|---------|----------|
| `q` | string | Evet | Arama sorgusu |
| `type` | string | Hayir | Virgullu tip filtresi (workflow, log) |
| `page` | int | Hayir | Sayfa numarasi (varsayilan: 1) |
| `pageSize` | int | Hayir | Sayfa boyutu (maks: 100) |

**Yanit:** `200 OK`
```json
{
  "items": [...],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20
}
```

---

## Pause / Resume / Snapshot API

Calisan execution'lari kontrol etme ve snapshot alma.

### Execution Duraklat

```http
POST /api/workflows/execution/{executionId}/pause
```

### Execution Devam Ettir

```http
POST /api/workflows/execution/{executionId}/resume
```

### Snapshot Al

```http
POST /api/workflows/execution/{executionId}/snapshot
Content-Type: application/json

{
  "label": "Checkpoint 1"
}
```

**Yanit:** `200 OK`
```json
{
  "id": "snap-guid-...",
  "executionId": "exec-guid-...",
  "label": "Checkpoint 1",
  "isAutomatic": false,
  "createdAt": "2025-01-24T10:00:00Z"
}
```

### Snapshot'lari Listele

```http
GET /api/workflows/execution/{executionId}/snapshots
```

### Snapshot'tan Geri Yukle

```http
POST /api/workflows/execution/{executionId}/snapshot/{snapshotId}/restore
```

---

## Stats API

Workflow calistirma istatistikleri.

### Dashboard Istatistikleri

```http
GET /api/workflows/stats
```

**Yanit:** `200 OK`
```json
{
  "totalWorkflows": 15,
  "totalExecutions": 342,
  "successRate": 94.7,
  "avgDurationMs": 1250.5,
  "activeWorkflows": 8,
  "todayExecutions": 12
}
```

### Tek Workflow Istatistigi

```http
GET /api/workflows/{id}/stats?days=30
```

**Yanit:** `200 OK`
```json
{
  "workflowId": "wf-guid-...",
  "runCount": 42,
  "successCount": 40,
  "failureCount": 2,
  "avgDurationMs": 850.3,
  "dailyStats": [
    { "date": "2025-01-24", "runs": 5, "successes": 5, "failures": 0 }
  ]
}
```

---

## A2A Protocol API

Agent-to-Agent iletisim altyapisi.

### Agent'lari Listele

```http
GET /api/a2a/agents
```

**Yanit:** `200 OK`
```json
[
  {
    "id": "analyst",
    "name": "Analyst Agent",
    "description": "Proje analizi yapar",
    "capabilities": ["analysis", "planning"],
    "status": "Available"
  }
]
```

### Agent Detayi

```http
GET /api/a2a/agents/{agentId}
```

### Task Gonder

```http
POST /api/a2a/tasks/send
Content-Type: application/json

{
  "agentId": "analyst",
  "message": "Bu projeyi analiz et",
  "metadata": {}
}
```

### Task Durumu

```http
GET /api/a2a/tasks/{taskId}
```

### Task Iptal

```http
POST /api/a2a/tasks/{taskId}/cancel
```

### Tum Task'lar

```http
GET /api/a2a/tasks
```

---

> **OpenAPI JSON:** Development modunda `GET /openapi/v1.json` adresinden
> otomatik uretilen OpenAPI spesifikasyonuna erisebilirsin.
> Bu JSON'u Postman'a import edip tum endpoint'leri test edebilirsin.
