# SonOfAnton — API referansı ve mevcut sınırlamalar

Kaynak: `c91a288`, 2 Kasım 2026 statik incelemesi. HTTPS backend taban adresi **`https://localhost:5002`**. Development ortamında `/openapi/v1.json` ve `/scalar/v1` kullanılabilir. Aşağıdaki tablolar kaynak koddan çıkarılmıştır; uçtan uca çağrı testi yapılmamıştır.

## Kritik sözleşme ayrımı

**Kalıcı workflow API'si `/api/workflows` rotasıdır.** Tekil `/api/workflow` ayrı bir legacy controller'dır: statik liste tutar, anonimdir ve DB workflow API'siyle eşdeğer değildir. Yeni entegrasyonları buna bağlamayın. `/api/workflow/chat` ve `/api/workflow/execution/{executionId}/stream` ise ayrı uçlardır; tekil CRUD kaldırılırken bunlar ayrıca değerlendirilmelidir.

Tüm endpoint'lerin yetkilendirildiği veya genel rate limit'in uygulandığı iddia edilemez. [REVIEW.md](REVIEW.md) F01–F08 erişim kusurlarını açıklar. Bu belge, mevcut açıkları desteklenen güvenlik davranışı olarak onaylamaz.

## Kalıcı workflow API

Kaynak: [WorkflowsController](../src/SonOfAnton.Backend/Controllers/WorkflowsController.cs), [DTO'lar](../src/SonOfAnton.Shared/DTOs/WorkflowDtos.cs).

Controller düzeyinde `Workflows.Read` istenir. Bazı işlemler ek policy ister; kaynak sahipliği/tenant kontrolü ayrıca eksiktir.

| Metot | Yol | Davranış / ek policy |
|---|---|---|
| GET | `/api/workflows` | Workflow listesi; graph dahil değil |
| GET | `/api/workflows/{id}` | Ayrıntı ve graph |
| POST | `/api/workflows` | `SaveWorkflowRequest`; ID varsa güncelle, yoksa oluştur; `Workflows.Edit` |
| DELETE | `/api/workflows/{id}` | Sil; `Workflows.Edit` |
| POST | `/api/workflows/{id}/execute` | `StartExecutionRequest`; `Workflows.Execute` |
| POST | `/api/workflows/{id}/test-run` | Adımlı çalışma; `Workflows.Execute` |
| POST | `/api/workflows/execution/{id}/step` | Sonraki adım; `Workflows.Execute` |
| GET | `/api/workflows/execution/{executionId}` | Execution durumu |
| GET | `/api/workflows/{id}/executions` | Workflow execution geçmişi |
| POST | `/api/workflows/execution/{executionId}/cancel` | İptal isteği |
| POST | `/api/workflows/execution/{executionId}/pause` | Duraklatma |
| POST | `/api/workflows/execution/{executionId}/resume` | Devam |
| POST | `/api/workflows/execution/{executionId}/resume-from-node/{nodeId}` | Seçilen node'dan devam |
| POST | `/api/workflows/execution/{executionId}/snapshot` | Snapshot oluştur |
| GET | `/api/workflows/execution/{executionId}/snapshots` | Snapshot listesi |
| POST | `/api/workflows/execution/{executionId}/snapshot/{snapshotId}/restore` | Snapshot geri yükle |
| PATCH | `/api/workflows/{id}/move` | Klasöre taşı |
| GET | `/api/workflows/stats` | Genel istatistikler |
| GET | `/api/workflows/{id}/stats` | Workflow istatistikleri |
| GET | `/api/workflows/{id}/revisions` | Revizyonlar |
| GET | `/api/workflows/{id}/revisions/{version}` | Tek revizyon |
| GET | `/api/workflows/{id}/artifacts` | Artifact verileri |
| POST | `/api/workflows/{id}/execute-node` | Tek node; `Workflows.Execute` |
| POST | `/api/workflows/execute-node-transient` | Kaydedilmemiş node; `Workflows.Execute` |

Cancel/pause/restore/move gibi mutasyonların yalnızca controller'ın read policy'siyle kalması ayrıca giderilmelidir. İzin tablosu hedef yetkilendirme tasarımı değildir.

Kaydetme gövdesinin şekli (boş graph çalıştırılabilir örnek değildir):

```json
{
  "id": null,
  "name": "İlk workflow",
  "description": "Geliştirme örneği",
  "graph": { "nodes": [], "connections": [], "variables": [] },
  "isActive": false
}
```

Çalıştırma gövdesi:

```http
POST /api/workflows/{id}/execute
Authorization: Bearer <token>
Content-Type: application/json

{"initialData":{"message":"Merhaba"}}
```

Yanıt `ExecutionDto` biçimindedir; execution kimliği `id` alanındadır. `/api/executions/run/{workflowId}` ise `{executionId, message}` döner. Bunlar aynı yanıt sözleşmesi değildir. Enum değerlerinin JSON gösterimi için OpenAPI/serializer çıktısını kullanın; önceki belgelerdeki string enum örneklerini garanti kabul etmeyin.

## Execution ve zamanlama

Kaynak: [ExecutionsController](../src/SonOfAnton.Backend/Controllers/ExecutionsController.cs).

| Metot | Yol | Açıklama |
|---|---|---|
| GET | `/api/executions?workflowId={id}` | Filtre opsiyonel; son 100 execution |
| GET | `/api/executions/{id}` | ExecutionDto |
| GET | `/api/executions/{id}/logs` | JSON log listesi |
| POST | `/api/executions/{id}/cancel` | DB durumunu değiştirir; devam eden yan etkinin durması ayrıca doğrulanmalı |
| POST | `/api/executions/run/{workflowId}` | Job service ile kuyruğa ekler; `executionId` döner |
| POST | `/api/executions/schedule/{workflowId}?delaySeconds=60` | Gecikmeli; `jobId` döner |
| POST | `/api/executions/recurring/{workflowId}?cron={encodedCron}&jobName={name}` | Cron kaydı |
| DELETE | `/api/executions/recurring/{jobName}` | Cron kaydını kaldırır |
| POST | `/api/executions/validate-cron` | `{"cronExpression":"0 9 * * 1-5"}` |
| GET | `/api/executions/job/{jobId}` | Hangfire job bilgisi |

Controller `Workflows.Read`; run/schedule/recurring ek olarak `Workflows.Execute` ister. Recurring silme ve cancel için ayrı mutasyon policy'si eksiktir. Cron query değeri URL-encode edilmelidir. Mevcut cron doğrulayıcının scheduler uyumu ve timezone davranışı test edilmemiştir; bir ifadeye otomatik olarak Türkiye saati anlamı vermeyin.

## Stream, public API ve sağlık

| Yol | Mevcut davranış / sınırlama |
|---|---|
| `/api/workflow/execution/{executionId}/stream?topics=node,log` | SSE; explicit authorization/ownership kontrolü yok, çok istemcili broadcast kusurlu |
| `/api/logs/{executionId}` | Ayrı log SSE controller'ı |
| `/api/notifications/stream` | Ayrı bildirim stream'i |
| `/api/sse/stats`, `/api/metrics` | Minimal endpoint; explicit authorization yok |
| `/api/published/{route}` | Dinamik workflow tetikleme; API key ve input schema eksikleri F05 |
| `/api/webhook/{webhookId}` | Dış tetikleme; imza/aktiflik davranışı WebhookController'da |
| `/health/live` | Süreç canlılığı |
| `/health/ready` | PostgreSQL ve Hangfire storage kontrolü; şema/AI sağlığı değil |

Published API `WaitForResult=false` durumunda 202 ve executionId döndürür; sonuç beklerken timeout 504 olabilir. Request header'larının trigger verisine kopyalanması giderilmeden gerçek sırlarla kullanmayın. SSE için query token desteği bulunması endpoint'in yetkili olduğu anlamına gelmez; URL token'ları loglara sızabilir.

## Diğer endpoint aileleri

Ayrıntılı request/response için ilgili controller ve üretilen OpenAPI birlikte incelenmelidir. Bu tablo uyumluluk veya tamamlanmışlık garantisi vermez.

| Rota | Kaynak |
|---|---|
| `/api/nodes` | [NodesController](../src/SonOfAnton.Backend/Controllers/NodesController.cs) |
| `/api/folders` | [FoldersController](../src/SonOfAnton.Backend/Controllers/FoldersController.cs) |
| `/api/workspaces` | [WorkspacesController](../src/SonOfAnton.Backend/Controllers/WorkspacesController.cs) |
| `/api/workflow/chat` | [WorkflowChatController](../src/SonOfAnton.Backend/Controllers/WorkflowChatController.cs); explicit Authorize yok |
| `/api/search` | [SearchController](../src/SonOfAnton.Backend/Controllers/SearchController.cs) |
| `/api/apikeys` | [ApiKeysController](../src/SonOfAnton.Backend/Controllers/ApiKeysController.cs) |
| `/api/workflow-endpoints` | [WorkflowApiController](../src/SonOfAnton.Backend/Controllers/WorkflowApiController.cs) |
| `/api/credentials`, `/api/linked-accounts` | Credential ve bağlı hesap controller'ları |
| `/api/chatbots`, `/api/public/chatbots` | [ChatbotController](../src/SonOfAnton.Backend/Controllers/ChatbotController.cs); F04/F14 |
| `/api/custom-nodes`, `/api/marketplace` | CustomNodes ve Marketplace controller'ları |
| `/api/datastores`, `/api/data-tables`, `/api/data-forms` | Veri yönetimi controller'ları |
| `/api/planning`, `/api/loops` | Planning ve RalphLoop controller'ları |
| `/api/a2a`, `/.well-known/agent.json` | A2A controller/Program; protokol uyumluluğu test edilmedi |
| `/scim/v2/Users`, `/scim/v2/Groups` | SCIM controller'ları; tam RFC uyumluluğu doğrulanmadı |
| `/api/notifications`, `/api/quotas` | Bildirim ve kota controller'ları |

## Kimlik, limit ve hatalar

JWT: `Authorization: Bearer <token>`. API key: `X-Api-Key: <key>`. `X-Tenant-ID` ve `X-Workspace-Id` güvenilir üyelik kanıtı değildir. Backend bu değerleri doğrulanmış kullanıcı ve kaynak kapsamıyla eşleştirmelidir.

Named rate limit policy'leri tanımlı fakat endpoint'lere bağlanmamış; “her API dakikada 100 istekle sınırlıdır” garantisi yoktur. Policy sabitleri için [Permissions.cs](../src/SonOfAnton.Shared/Core/Identity/Permissions.cs) referanstır; API key claim adlarıyla uyumsuzluk vardır.

Global exception handler `{error, requestId}` üretir; controller'lar string, farklı JSON veya framework validation yanıtları da döndürebilir. Tüm hatalar için tek şema varsaymayın. Hedef iyileştirme: tutarlı ProblemDetails, kaynak yetkisi ve otomatik sözleşme testleri.
