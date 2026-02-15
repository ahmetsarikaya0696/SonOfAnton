#  SonOfAnton — API Referansı

> **Bu belge kime hitap ediyor?**  
> Frontend geliştiren, API test eden veya dış entegrasyon yapan herkes.
> Her endpoint'i örnekleriyle birlikte bulabilirsin.

---

##  İçindekiler

1. [Genel Bilgiler](#genel-bilgiler)
2. [Workflow API](#workflow-api)
3. [Execution API](#execution-api)
4. [Node API](#node-api)
5. [Webhook API](#webhook-api)
6. [Log Stream API (SSE)](#log-stream-api-sse)
7. [Health Check API](#health-check-api)
8. [Hata Kodları](#hata-kodları)

---

## Genel Bilgiler

| Özellik | Değer |
|---------|-------|
| Base URL | `https://localhost:5001/api` |
| Format | JSON (`application/json`) |
| Rate Limit | 100 istek/dakika (IP başına) |
| OpenAPI | `GET /openapi/v1.json` (Development modunda) |

**Ortak HTTP Durum Kodları:**

| Kod | Anlamı | Ne zaman döner? |
|-----|--------|-----------------|
| 200 | OK | İşlem başarılı |
| 201 | Created | Yeni kaynak oluşturuldu |
| 204 | No Content | Silme başarılı |
| 400 | Bad Request | Geçersiz istek verisi |
| 404 | Not Found | Kaynak bulunamadı |
| 429 | Too Many Requests | Rate limit aşıldı |
| 500 | Server Error | Sunucu hatası |

---

## Workflow API

Workflow'lar, node'lar ve bağlantılardan oluşan iş akışı şablonlarıdır.

### Tüm Workflow'ları Listele

```http
GET /api/workflow
```

**Yanıt:** `200 OK`
```json
[
  {
    "id": "a1b2c3d4-...",
    "name": "Müşteri Hoşgeldin Akışı",
    "description": "Yeni müşteriye hoşgeldin e-postası gönderir",
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

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `id` | GUID | Evet | Workflow ID'si |

**Yanıt:** `200 OK` veya `404 Not Found`

### Workflow Oluştur

```http
POST /api/workflow
Content-Type: application/json

{
  "name": "Yeni İş Akışı",
  "description": "Açıklama",
  "nodes": [],
  "connections": []
}
```

**Yanıt:** `201 Created`
```json
{
  "id": "yeni-guid-...",
  "name": "Yeni İş Akışı",
  "createdAt": "2025-01-24T10:00:00Z",
  "updatedAt": "2025-01-24T10:00:00Z"
}
```

### Workflow Güncelle

```http
PUT /api/workflow/{id}
Content-Type: application/json

{
  "name": "Güncellenmiş Ad",
  "description": "Güncellenmiş açıklama",
  "nodes": [...],
  "connections": [...]
}
```

**Yanıt:** `200 OK` veya `404 Not Found`

### Workflow Sil

```http
DELETE /api/workflow/{id}
```

**Yanıt:** `204 No Content` veya `404 Not Found`

### Workflow Çalıştır

```http
POST /api/workflow/{id}/execute
Content-Type: application/json

{
  "tetikleme": "verisi",
  "key": "value"
}
```

**Yanıt:** `200 OK`
```json
{
  "message": "Workflow execution started",
  "workflowId": "a1b2c3d4-...",
  "executionId": "e5f6g7h8-..."
}
```

> **Not:** triggerData opsiyoneldir. Body boş gönderilebilir.

### Workflow Aktifleştir / Devre Dışı Bırak

```http
POST /api/workflow/{id}/activate
POST /api/workflow/{id}/deactivate
```

**Yanıt:** `200 OK` — Güncellenmiş workflow döner.

---

## Execution API

Execution, bir workflow'un tek seferlik çalıştırılmasıdır.

### Execution'ları Listele

```http
GET /api/executions
GET /api/executions?workflowId={workflowId}
```

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `workflowId` | GUID | Hayır | Belirli workflow'a ait execution'lar |

**Yanıt:** `200 OK` — Son 100 execution (tarih sırasıyla)
```json
[
  {
    "id": "exec-guid-...",
    "workflowId": "wf-guid-...",
    "workflowName": "Müşteri Akışı",
    "status": "Completed",
    "startedAt": "2025-01-24T10:00:00Z",
    "completedAt": "2025-01-24T10:00:05Z"
  }
]
```

**Durum Değerleri:** `Queued`, `Running`, `Completed`, `Failed`, `Cancelled`

### Execution Detayı

```http
GET /api/executions/{id}
```

### Execution Logları

```http
GET /api/executions/{id}/logs
```

**Yanıt:** `200 OK`
```json
[
  {
    "executionId": "exec-guid-...",
    "nodeId": "node-guid-...",
    "level": "Info",
    "message": "Koşul değerlendiriliyor: status == active",
    "timestamp": "2025-01-24T10:00:01Z"
  }
]
```

### Execution İptal Et

```http
POST /api/executions/{id}/cancel
```

**Yanıt:** `200 OK` veya `400 Bad Request` (zaten tamamlanmışsa)

### Workflow'u Hemen Çalıştır

```http
POST /api/executions/run/{workflowId}
```

**Yanıt:** `200 OK`
```json
{
  "executionId": "exec-guid-...",
  "message": "Workflow kuyruğa eklendi"
}
```

### Gecikmeli Çalıştır

```http
POST /api/executions/schedule/{workflowId}?delaySeconds=60
```

### Tekrarlayan Workflow Ekle

```http
POST /api/executions/recurring/{workflowId}?cron=0 * * * *&jobName=saatlik-rapor
```

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `cron` | string | Evet | Cron ifadesi (5 parça: dakika saat gün ay haftaGünü) |
| `jobName` | string | Hayır | İş adı (varsayılan: workflow-{workflowId}) |

**Cron Örnekleri:**

| İfade | Anlamı |
|-------|--------|
| `* * * * *` | Her dakika |
| `0 * * * *` | Her saat başı |
| `0 9 * * 1-5` | Hafta içi her gün saat 09:00 |
| `0 0 1 * *` | Her ayın 1'i gece yarısı |

### Tekrarlayan Workflow Kaldır

```http
DELETE /api/executions/recurring/{jobName}
```

### Cron İfadesi Doğrula

```http
POST /api/executions/validate-cron
Content-Type: application/json

{
  "cronExpression": "0 9 * * 1-5"
}
```

**Yanıt:** `200 OK`
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

Node'lar, workflow'ların yapı taşlarıdır.

### Tüm Node Tiplerini Listele

```http
GET /api/nodes
```

**Yanıt:** `200 OK`
```json
[
  {
    "type": "logic.if",
    "name": "Koşul (IF)",
    "icon": "git-branch",
    "category": "Mantik",
    "description": "Koşula göre akışı dallandır",
    "color": "#f9e2af",
    "inputs": [{"name": "input", "type": "any"}],
    "outputs": [
      {"name": "true", "type": "any"},
      {"name": "false", "type": "any"}
    ],
    "parameters": [
      {
        "name": "condition",
        "displayName": "Koşul",
        "type": "string",
        "required": true,
        "placeholder": "status == active"
      }
    ]
  }
]
```

### Tek Node Tipi Detayı

```http
GET /api/nodes/{type}
```

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `type` | string | Evet | Node tipi (ör: `logic.if`, `transform.set`) |

---

## Webhook API

Dış servislerden workflow tetiklemek için kullanılır.

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

**Yanıt:** `200 OK`
```json
{
  "message": "Workflow triggered",
  "executionId": "exec-guid-..."
}
```

**Hata Durumları:**
- `404` — Webhook bulunamadı veya aktif değil
- `401` — Signature geçersiz (secret tanımlıysa)
- `429` — Rate limit (min interval süresi dolmamış)

---

## Log Stream API (SSE)

Gerçek zamanlı log takibi için Server-Sent Events kullanılır.

### Execution Event Stream

```http
GET /api/workflow/execution/{executionId}/stream
GET /api/workflow/execution/{executionId}/stream?topics=node,log
```

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `executionId` | GUID | Evet | Execution ID'si |
| `topics` | string | Hayır | Virgülle ayrılmış topic filtresi |

**Yanıt:** `200 OK` (text/event-stream)
```
: connected session=abc123

data: {"type":"node_started","nodeId":"...","timestamp":"..."}

data: {"type":"node_completed","nodeId":"...","output":{}}

: heartbeat

data: {"type":"workflow_completed"}
```

**JavaScript ile kullanım:**
```javascript
const source = new EventSource('/api/workflow/execution/' + executionId + '/stream');

source.onmessage = (event) => {
    const data = JSON.parse(event.data);
    console.log('Event:', data.type, data);
};

source.onerror = () => {
    console.log('Bağlantı kesildi, yeniden deneniyor...');
};
```

### Execution Log Stream

```http
GET /api/logs/{executionId}
```

**Yanıt:** `200 OK` (text/event-stream)

### SSE İstatistikleri

```http
GET /api/sse/stats
```

**Yanıt:** `200 OK`
```json
{
  "activeChannels": 3,
  "totalBytesTransferred": 15360,
  "activeSessions": 2
}
```

---

## Health Check API

Uygulamanın sağlık durumunu kontrol eder.

### Canlılık Kontrolü (Liveness)

```http
GET /health/live
```

**Yanıt:** `200 OK`
```json
{
  "status": "alive",
  "timestamp": "2025-01-24T10:00:00Z"
}
```

> Uygulama ayakta mı? (Veritabanı bağlantısını kontrol ETMEZ)

### Hazırlık Kontrolü (Readiness)

```http
GET /health/ready
```

**Yanıt:** `200 OK`
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
      "description": "Hangfire bağlı",
      "duration": 0.3
    }
  ],
  "totalDuration": 15.2
}
```

> Uygulama trafiğe hazır mı? (PostgreSQL + Hangfire kontrol eder)

---

## Hata Kodları

Tüm hata yanıtları aşağıdaki formattadır:

```json
{
  "error": "Hata açıklaması",
  "requestId": "abc123-trace-id"
}
```

| Hata Mesajı | Endpoint | Çözüm |
|-------------|----------|-------|
| "Workflow not found" | Workflow API | ID'nin doğru olduğunu kontrol et |
| "Bu execution iptal edilemez" | Cancel | Sadece Running/Queued durumundakiler iptal edilebilir |
| "Webhook not found or inactive" | Webhook | webhookId ve aktiflik durumunu kontrol et |
| "Invalid signature" | Webhook | X-Webhook-Signature header'ını kontrol et |
| "Too many requests" | Rate limited | Bekle ve tekrar dene |
| "Sunucu hatası oluştu" | Herhangi | requestId ile log'ları incele |

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


---

## Authentication & Authorization

Tüm API endpoint'leri (webhook trigger hariç) kimlik doğrulama gerektirir.
Kimlik sağlayıcı olarak **Keycloak** (OIDC) kullanılır.

### Kimlik Doğrulama Yöntemleri

**1. OIDC (Interactive Login — Keycloak)**

Blazor WASM üzerinden Keycloak login sayfasına yönlendirme. Authorization Code Flow (PKCE) ile token alınır.

**2. JWT Bearer Token (API Erişimi)**

```
Authorization: Bearer <jwt_token>
```

### Tenant Bilgisi

Her istekte tenant bilgisi gönderilmelidir:

```
X-Tenant-ID: kurumA
```

Subdomain modunda: `kurumA.uygulama.com`

### İzin Gereksinimleri

| Endpoint | Gereken İzin |
|----------|-------------|
| `GET /api/workflows` | `Workflows.Read` |
| `POST /api/workflows` | `Workflows.Edit` |
| `DELETE /api/workflows/{id}` | `Workflows.Edit` |
| `POST /api/workflows/{id}/execute` | `Workflows.Execute` |
| `GET /api/executions` | `Workflows.Read` |
| `POST /api/executions/run/{id}` | `Workflows.Execute` |
| `POST /api/executions/schedule/{id}` | `Workflows.Execute` |
| `POST /api/executions/recurring/{id}` | `Workflows.Execute` |
| `GET /api/folders` | `Workflows.Read` |
| `POST /api/folders` | `Folders.Manage` |
| `DELETE /api/folders/{id}` | `Folders.Manage` |
| `POST /api/webhook/create` | `Webhooks.Manage` |
| `POST /api/webhook/{webhookId}` | (AllowAnonymous) |
| `/scim/v2/Users` | `Users.Manage` |
| `/scim/v2/Groups` | `Users.Manage` |

> `System.Admin` izni tüm endpoint'lere erişim sağlar.

### Hata Yanıtları

| Durum Kodu | Açıklama |
|-----------|----------|
| `401 Unauthorized` | Geçersiz veya eksik kimlik bilgileri |
| `403 Forbidden` | Kimlik doğrulandı ama yetki yetersiz |

---

## SCIM 2.0 API

SCIM 2.0 (RFC 7643/7644) uyumlu kullanıcı ve grup yönetim endpoint'leri.
Keycloak veya diğer kurumsal kimlik sağlayıcıları (Entra ID, Okta) otomatik senkronizasyon için bu API'yi kullanır.

> **Content-Type:** `application/scim+json`
> **Yetki:** `Users.Manage` izni gerektirir

### Kullanıcıları Listele

```http
GET /scim/v2/Users?startIndex=1&count=100

X-Tenant-ID: kurumA
Authorization: Bearer <token>
```

**Yanıt:** `200 OK`
```json
{
  "schemas": ["urn:ietf:params:scim:api:messages:2.0:ListResponse"],
  "totalResults": 42,
  "startIndex": 1,
  "itemsPerPage": 42,
  "Resources": [
    {
      "schemas": ["urn:ietf:params:scim:schemas:core:2.0:User"],
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "userName": "ahmet@kurum.com",
      "displayName": "Ahmet Yılmaz",
      "active": true,
      "emails": [{"value": "ahmet@kurum.com", "type": "work", "primary": true}]
    }
  ]
}
```

### Kullanıcı Oluştur

```http
POST /scim/v2/Users

{
  "schemas": ["urn:ietf:params:scim:schemas:core:2.0:User"],
  "userName": "yeni@kurum.com",
  "displayName": "Yeni Kullanıcı",
  "emails": [{"value": "yeni@kurum.com", "type": "work", "primary": true}],
  "active": true
}
```

**Yanıt:** `201 Created`

### Kullanıcı Güncelle (PATCH)

```http
PATCH /scim/v2/Users/{id}

{
  "schemas": ["urn:ietf:params:scim:api:messages:2.0:PatchOp"],
  "Operations": [
    {"op": "replace", "path": "active", "value": false}
  ]
}
```

**Yanıt:** `204 No Content`

### Kullanıcı Sil

```http
DELETE /scim/v2/Users/{id}
```

**Yanıt:** `204 No Content` (soft delete  kullanıcı deaktive edilir)

### Grupları Listele

```http
GET /scim/v2/Groups?startIndex=1&count=100
```

**Yanıt:** `200 OK`

### Grup Oluştur

```http
POST /scim/v2/Groups

{
  "schemas": ["urn:ietf:params:scim:schemas:core:2.0:Group"],
  "displayName": "Workflow Yöneticileri"
}
```

**Yanıt:** `201 Created`

### Grup Sil

```http
DELETE /scim/v2/Groups/{id}
```

**Yanıt:** `204 No Content` (hard delete)
