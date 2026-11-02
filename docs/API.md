# SonOfAnton — Çekirdek API

Development taban adresi `https://localhost:5002`. OpenAPI `/openapi/v1.json`, arayüz `/scalar/v1`. JSON enum'ları sayısaldır. Kalıcı API **`/api/workflows`**; tekil legacy CRUD kaldırılmıştır.

## Yetki ve limit

`Authorization: Bearer <token>` veya `X-Api-Key` gerekir. JWT issuer/audience/lifetime doğrulanır. API key aktif kullanıcı/tenant'a bağlıdır. Genel permission yanında kaynak sahipliği/üyeliği denetlenir: kimliksiz 401, yetkisiz mutasyon 403, erişilemeyen workflow/execution 404. Tenant header'ı claim ile çelişirse 403. Workspace header'ı üyelik vermez.

Genel limit JWT kullanıcısı için 100/dakika, API key için kayıtlı değere göre 1–1000/dakikadır. Execution mutasyonlarına ek kullanıcı başına 10/dakika uygulanır. Aşım 429'dur.

## Workflow

| Metot / yol | Davranış |
|---|---|
| GET `/api/workflows`, `/{id}` | Liste / graph dahil ayrıntı |
| POST `/api/workflows` | ID varsa güncelle, yoksa oluştur; `Workflows.Edit` ve workspace yazma rolü |
| DELETE `/api/workflows/{id}` | Aynı yazma koşuluyla sil |
| POST `/api/workflows/{id}/execute` | `StartExecutionRequest`; `ExecutionDto` döner |
| POST `/api/workflows/{id}/test-run` | Adımlı yürütme |
| POST `/api/workflows/execution/{id}/step` | Bir sonraki node'a izin ver |
| POST `/api/workflows/execution/{id}/cancel`, `/pause`, `/resume` | İptal / aynı süreçte dur–devam |
| GET `/api/workflows/{id}/executions` | Çalıştırma geçmişi |
| PATCH `/api/workflows/{id}/move` | Aynı workspace klasörüne taşı |
| GET `/api/workflows/stats`, `/{id}/stats` | Kapsamlanmış istatistik |
| GET `/api/workflows/{id}/revisions`, `/{id}/revisions/{version}` | Graph sürümleri |
| POST `/api/workflows/{id}/execute-node`, `/api/workflows/execute-node-transient` | Kayıtlı handler ile tek node; execution izni |
| POST `/api/workflows/execution/{id}/snapshot` | Bilgi amaçlı snapshot |
| GET `/api/workflows/execution/{id}/snapshots` | Snapshot listesi |
| POST `.../resume-from-node/{nodeId}`, `.../snapshot/{snapshotId}/restore` | **501**; checkpoint replay desteklenmez |

Graph 200 node/400 edge sınırında, benzersiz ID'li ve döngüsüz olmalıdır. Desteklenmeyen handler/subflow reddedilir. Boş taslak kaydedilebilir ama çalıştırılması başarısız olur. Global hata workflow zinciri kapalıdır.

Kaydetme örneği (`workspaceId` gerçek yetkili workspace olmalı):

```json
{"name":"İlk akış","workspaceId":"11111111-1111-1111-1111-111111111111","graph":{"nodes":[{"id":"22222222-2222-2222-2222-222222222222","type":"trigger.manual","name":"Başlat","parameters":{}}],"connections":[],"variables":[]}}
```

Çalıştırma gövdesi `{"initialData":{"message":"Merhaba"}}`; response execution kimliği `id` alanındadır.

## Execution ve zamanlama

| Metot / yol | Sözleşme |
|---|---|
| GET `/api/executions?workflowId={id}` | Son 100 execution |
| GET `/api/executions/{id}`, `/{id}/logs` | Durum / JSON log |
| POST `/api/executions/run/{workflowId}` | `{executionId, message}`; gövde başlangıç verisi |
| POST `/api/executions/schedule/{workflowId}?delaySeconds=60` | `{jobId, executionId, message}`; 0–30 gün |
| POST `/api/executions/recurring/{workflowId}?cron=...` | Beş alanlı **UTC** cron; ad `workflow_{workflowId}` |
| DELETE `/api/executions/recurring/workflow_{workflowId}` | Yetkili workflow'nun cron kaydını kaldır |
| POST `/api/executions/validate-cron` | `{"cronExpression":"0 9 * * 1-5"}` |
| POST `/api/executions/{id}/cancel` | Queued/Running/Paused işi iptal et |
| GET `/api/executions/job/{jobId}` | Yalnız yönetici |

Çalıştırma mutasyonları `Workflows.Execute` ve workspace yazma rolü ister. Cron URL-encode edilir. Otomatik retry kapalıdır; yeni çalışma yeni execution'dır. İptal yapılmış dış işlemleri geri almaz.

## Stream ve yardımcı uçlar

| Yol | Davranış |
|---|---|
| `/api/logs/{executionId}` | Kalıcı SSE; `id: ticks/guid`, `data: LogEventDto`, sonunda `event: complete`; reconnect `Last-Event-ID` destekler |
| `/api/workflow/execution/{executionId}/stream` | Yetkili ilerleme; node/log ve terminal `workflow-completed`; query token kabul edilmez |
| `/api/nodes` | Sunucuda kayıtlı node kataloğu |
| `/api/capabilities` | `ai`, `chat=false`, `experimental=false` |
| `/api/workspaces`, `/api/folders`, `/api/apikeys` | Kapsamlı yönetim; ayrıntı OpenAPI'de |
| `/api/notifications`, `/api/quotas` | Kullanıcının kayıtları |
| `/api/sse/stats`, `/api/metrics` | Yalnız yönetici |
| `/health/live`, `/health/ready` | Anonim sağlık |

İlerleme SSE geçmiş node event'lerini replay etmez; geç abone terminal durumu görür, geçmiş için kalıcı log API'sini kullanır. Yavaş abone ayrılabilir. Tek instance pause/step sınırı geçerlidir.

Marketplace/chatbot/webhook/published/SCIM/A2A/chat/RAG/planlama/veri tablosu/formu/credential API'leri core listesinde değildir. Kimliği doğrulanmış istemci 404 alır; kimliksiz istek önce fallback nedeniyle 401 alabilir. Bunların güvenli biçimde ürünleştirilmesi ayrı iştir.
