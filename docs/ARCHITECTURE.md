Replacing block between '**Node Durumları (State Machine):**' and '**Önemli:** Her durum geçişi' Replacing block between '**Veri bir node'dan diğerine nasıl geçer?**' and '- Her node bir' Replacing block between 'Bir workflow çalıştırıldığında veri şu yolu izler:' and '**Veri bir node'dan diğerine nasıl geçer?**' Replacing block between '## 4. Bileşen Etkileşim Diyagramı' and '**İletişim Protokolleri:**' Replacing block between '## 3. Katmanlı Mimari' and '**Katman Kuralları:**' ﻿#  SonOfAnton — Mimari Dokümantasyonu

> **Bu belge kime hitap ediyor?**  
> Ekibe yeni katılan bir geliştirici olarak bu dokümanı okuduktan sonra
> projenin nasıl çalıştığını, hangi parçanın ne iş yaptığını ve kodda
> nereye bakman gerektiğini anlayabilmelisin.

---

##  İçindekiler

1. [Projeye Genel Bakış](#1-projeye-genel-bakış)
2. [Klasör Yapısı](#2-klasör-yapısı)
3. [Katmanlı Mimari](#3-katmanlı-mimari)
4. [Bileşen Etkileşim Diyagramı](#4-bileşen-etkileşim-diyagramı)
5. [Veri Akış Diyagramı](#5-veri-akış-diyagramı)
6. [Graph Engine (Çekirdek Motor)](#6-graph-engine-çekirdek-motor)
7. [Node Handler Sistemi](#7-node-handler-sistemi)
8. [Veritabanı Yapısı](#8-veritabanı-yapısı)
9. [Gerçek Zamanlı İletişim (SSE)](#9-gerçek-zamanlı-iletişim-sse)
10. [Arka Plan İşleri (Hangfire)](#10-arka-plan-işleri-hangfire)
11. [AI ve Vector Veritabanı](#11-ai-ve-vector-veritabanı)
12. [Güvenlik Katmanları](#12-güvenlik-katmanları)
13. [Test Stratejisi](#13-test-stratejisi)
14. [Sık Yapılan Görevler](#14-sık-yapılan-görevler)

---

## 1. Projeye Genel Bakış

**SonOfAnton**, Node-RED benzeri bir **görsel workflow otomasyon** platformudur.

**Ne yapar?**
- Kullanıcı, tarayıcıda sürükle-bırak ile node'ları birbirine bağlayarak
  bir iş akışı (workflow) oluşturur
- Oluşturulan workflow, arka planda çalıştırılır
- Her adımın sonucu gerçek zamanlı olarak ekranda görüntülenir

**Teknoloji Yığını:**

| Katman | Teknoloji | Neden? |
# SonOfAnton — Mimari Dokümantasyonu

> **Bu belge kime hitap ediyor?**  
> Ekibe yeni katılan bir geliştirici olarak bu dokümanı okuduktan sonra
> projenin nasıl çalıştığını, hangi parçanın ne iş yaptığını ve kodda
> nereye bakman gerektiğini anlayabilmelisin.

---

## İçindekiler

1. [Projeye Genel Bakış](#1-projeye-genel-bakış)
2. [Klasör Yapısı](#2-klasör-yapısı)
3. [Katmanlı Mimari](#3-katmanlı-mimari)
4. [Bileşen Etkileşim Diyagramı](#4-bileşen-etkileşim-diyagramı)
5. [Veri Akış Diyagramı](#5-veri-akış-diyagramı)
6. [Graph Engine (Çekirdek Motor)](#6-graph-engine-çekirdek-motor)
7. [Node Handler Sistemi](#7-node-handler-sistemi)
8. [Veritabanı Yapısı](#8-veritabanı-yapısı)
9. [Gerçek Zamanlı İletişim (SSE)](#9-gerçek-zamanlı-iletişim-sse)
10. [Arka Plan İşleri (Hangfire)](#10-arka-plan-işleri-hangfire)
11. [AI ve Vector Veritabanı](#11-ai-ve-vector-veritabanı)
12. [Güvenlik Katmanları](#12-güvenlik-katmanları)
13. [Test Stratejisi](#13-test-stratejisi)
14. [Sık Yapılan Görevler](#14-sık-yapılan-görevler)

---

## 1. Projeye Genel Bakış

**SonOfAnton**, Node-RED benzeri bir **görsel workflow otomasyon** platformudur.

**Ne yapar?**
- Kullanıcı, tarayıcıda sürükle-bırak ile node'ları birbirine bağlayarak
  bir iş akışı (workflow) oluşturur
- Oluşturulan workflow, arka planda çalıştırılır
- Her adımın sonucu gerçek zamanlı olarak ekranda görüntülenir

**Teknoloji Yığını:**

| Katman | Teknoloji | Neden? |
|--------|-----------|--------|
| Frontend | Blazor WebAssembly | C# ile tek dil, .NET 10 |
| Backend API | ASP.NET Core | Performanslı REST API |
| Kimlik Sağlayıcı | Keycloak (OIDC) | Açık kaynak, OIDC/OAuth 2.0 uyumlu |
| Veritabanı | PostgreSQL + pgvector | JSONB + vektör arama |
| Arka Plan İşleri | Hangfire | Güvenilir kuyruk sistemi |
| AI | Ollama + ONNX Runtime | Yerel LLM ve embedding |
| Gerçek Zamanlı | Server-Sent Events (SSE) | Tek yönlü event stream (Loglar & Bildirimler) |
| Kotasyon & Ücretlendirme | Kullanım Limitleri | Aylık execution takibi |

---

## 2. Klasör Yapısı

```
SonOfAnton/
|-- src/
|   |-- SonOfAnton.Shared/        ? Ortak kodlar (Frontend + Backend paylaşır)
|   |   |-- Core/GraphEngine/     ? DAG motoru (INode, Edge, Graph, Channel)
|   |   |-- DTOs/                 ? Veri Transfer Objeleri
|   |   |-- Entities/             ? Veritabanı entity'leri
|   |   |-- Enums/                ? Sabit değerler (ExecutionStatus, LogLevel vb.)
|   |   |-- Models/               ? İş modelleri (Workflow, NodeRegistry)
|   |   |-- Events/               ? Event tipleri
|   ?
|   |-- SonOfAnton.Frontend/      ? Blazor WebAssembly uygulaması
|   |   |-- Components/           ? Blazor bileşenleri (Editor, Palette, Log vb.)
|   |   |   |-- LoginDisplay.razor      ? Kullanıcı giriş/çıkış UI
|   |   |   |-- RedirectToLogin.razor   ? Yetkisiz kullanıcı yönlendirmesi
|   |   |-- Pages/                ? Sayfa bileşenleri
|   |   |   |-- Authentication.razor    ? OIDC login/logout/callback handler
|   |   |-- Services/             ? API istemci servisleri
|   |   |-- wwwroot/              ? Statik dosyalar (JS, CSS, appsettings.json)
|   ?
|   |-- SonOfAnton.Backend/       ? ASP.NET Core Web API
|       |-- Controllers/          ? REST API endpoint'leri
|       |-- Engine/               ? Workflow çalıştırma motoru
|       |   |-- Handlers/         ? Node handler'ları (Strategy Pattern)
|       |-- Data/                 ? EF Core DbContext
|       |-- Migrations/           ? Veritabanı migration'ları
|       |-- Services/             ? İş servisleri
|       |-- Infrastructure/       ? Altyapı (Embedding, Vector DB, Docker)
|       |-- Agents/               ? AI Agent servisleri
|       |-- Orchestrator/         ? Proje orkestrasyon
?
|-- tests/
|   |-- SonOfAnton.Tests/         ? xUnit test projesi
|       |-- Unit/                 ? Birim testleri
|       |   |-- GraphEngine/      ? Graph, Edge, NodeBase testleri
|       |   |-- Handlers/         ? Handler testleri
|       |   |-- Services/         ? Servis testleri
|       |-- Integration/          ? Entegrasyon testleri
?
|-- docs/                         ? Proje dokümantasyonu
|   |-- ARCHITECTURE.md           ? Bu dosya
|   |-- API.md                    ? API referansı
|   |-- DATABASE.md               ? Veritabanı şeması
|   |-- DEPLOYMENT.md             ? Dağıtım kılavuzu
?
|-- .github/workflows/            ? CI/CD pipeline'ları
    |-- ci.yml                    ? Build + Test + Coverage
```

---

## 3. Katmanlı Mimari

```
+---------------------------------------------------+
|                   FRONTEND                        |
|              (Blazor WebAssembly)                 |
|                                                   |
|  +------------+ +-------------+ +---------------+ |
|  |  Workflow  | |   Node      | |   Execution   | |
|  |  Editor    | |  Palette    | |   Monitor     | |
|  +------------+ +-------------+ +---------------+ |
|       ^             ^              ^              |
|       |             |              |              |
|  +----------------------------------------------+ |
|  |        API Services + SSE Client             | |
|  +----------------------------------------------+ |
+---------------------------------------------------+
                      ^ HTTP REST + SSE
                      |
+---------------------------------------------------+
|                   BACKEND                         |
|              (ASP.NET Core API)                   |
|                                                   |
|  +----------------------------------------------+ |
|  |              Controllers                     | |
|  |  (Workflow, Executions, Nodes, Webhook, Log) | |
|  +----------------------------------------------+ |
|                       |                           |
|                       v                           |
|  +----------------------------------------------+ |
|  |            Engine Layer                      | |
|  |  +----------------+  +---------------------+ | |
|  |  | Workflow       |  |  Node Handlers      | | |
|  |  | Runner         |--|  (Strategy Pattern) | | |
|  |  | (retry,        |  |  +---------------+  | | |
|  |  |  timeout,      |  |  | If, Switch    |  | | |
|  |  |  cycle det.)   |  |  | Delay, Loop   |  | | |
|  |  +----------------+  |  | HTTP, LLM     |  | | |
|  |                      |  | Email, DB     |  | | |
|  |                      |  +---------------+  | | |
|  +----------------------------------------------+ |
|                       |                           |
|                       v                           |
|  +------------+ +-----------------+ +-----------------------------+ |
|  | Hangfire   | |  EF Core +      | | Stream Service (Log & Notif)| |
|  | (Job Q)    | |  PostgreSQL     | +-----------------------------+ |
|  +------------+ +-----------------+                               |
+-------------------------------------------------------------------+
```

**Katman Kuralları:**
- Controller -> Service -> Repository (yukarıdan aşağı çağırır)
- Hiçbir katman kendinden üstteki katmanı çağırmaz
- Shared projesinde backend/frontend'e özel kod OLMAZ

---

## 4. Bileşen Etkileşim Diyagramı

```
   Kullanıcı
       |
       v
+---------------+     HTTP/REST     +----------------+
|   Blazor      |<----------------->|  ASP.NET       |
|   WASM        |                   |  Core API      |
|   Frontend    |<----- SSE --------|                |
+---------------+   (tek yönlü)     +----------------+
       |                                   |
       | OIDC                              |
       v                                   |
+---------------+                   +----------------+
|  Keycloak     |                   |  Hangfire      |
|  (Identity)   |                   |  Worker        |
|  Port 8080    |                   |  (Arka Plan)   |
+---------------+                   +----------------+
                                           |
                                +-----------------------+
                                |          |            |
                          +------------+ +----------+ +-----------+
                          | PostgreSQL | | Ollama   | |  Dış      |
                          | + pgvector | | (AI)     | |  API'ler  |
                          +------------+ +----------+ +-----------+
```

**İletişim Protokolleri:**
- **Frontend → Keycloak**: OIDC Authorization Code Flow (PKCE)
- **Frontend → Backend**: HTTP REST (JSON) + Authorization: Bearer token
- **Backend → Frontend**: SSE (Server-Sent Events, gerçek zamanlı)
- **Backend → Keycloak**: JWT token doğrulama (JWKS endpoint)
- **Backend → Hangfire**: .NET method çağrısı (kuyruk üzerinden)
- **Backend → PostgreSQL**: EF Core / Npgsql
- **Backend → Ollama**: HTTP (Semantic Kernel SDK ile)

---

## 5. Veri Akış Diyagramı

Bir workflow çalıştırıldığında veri şu yolu izler:

```
1. TETIKLEME
   +--------------+
   | Kullanıcı    |--> "Çalıştır" butonuna basar
   | veya Webhook |--> Dış servis HTTP isteği gönderir
   | veya Cron    |--> Zamanlayıcı tetikler
   +--------------+
          |
          v
2. KUYRUKLAMA
   +----------------------------------------+
   |  WorkflowJobService                    |
   |  - Hangfire'a job ekler                |
   |  - ExecutionEntity oluşturur (DB)      |
   |  - SSE kanalı açar                     |
   +----------------------------------------+
          |
          v
3. ÇALIŞTIRMA (Hangfire Worker Thread)
   +----------------------------------------+
   |  WorkflowRunner                        |
   |                                        |
   |  graph.GetTriggerNodes() ile başla     |
   |  +-------------------------------+     |
   |  |  Her node için:               |     |
   |  |  1. Handler'ı bul             |     |
   |  |  2. Retry + Timeout uygula    |     |
   |  |  3. Execute et                |     |
   |  |  4. Çıktıyı sonraki node'a    |     |
   |  |     inputData olarak geçir    |     |
   |  |  5. SSE event gönder          |     |
   |  |  6. Log yaz (DB)              |     |
   |  +-------------------------------+     |
   +----------------------------------------+
          |
          v
4. SONUÇ
   +----------------------------------------+
   |  - ExecutionEntity güncellenir       |
   |  - SSE "complete" event gönderilir   |
   |  - Frontend UI güncellenir           |
   +----------------------------------------+
```

**Veri bir node'dan diğerine nasıl geçer?**

```
-----------|--    outputData    -----------|--    outputData    --------------
?  Trigger   ? ------------|-- ?  Transform ? ------------|-- |   Logic    ?
?  Node      ?  (JSON object)  ?  Node      ?  (JSON object)  |   Node     ?
|            |                 |            |                 |            ?
? outputPort |                 ? outputPort |                 ? outputPort ?
? = "output" |                 ? = "output" |                 ? = "true"   ?
-----------|--                 -----------|--                 --------------
                                                                     ?
                                                        --------------
                                                        |            ?
                                                   --------|-- -----------
                                                   ?  Email  ? ?  Debug  ?
                                                   ?  Node   ? ?  Node   ?
                                                   --------|-- -----------
```
- Her node bir `NodeExecutionResult` döner
- Bu result içinde `OutputData` (sonraki node'a gidecek veri) ve `OutputPort` (hangi çıkışa gideceği) bulunur
- Logic node'ları (if, switch) farklı port'lara yönlendirebilir ("true"/"false")

---

## 6. Graph Engine (Çekirdek Motor)

Graph Engine, projenin kalbidir. Workflow'lar aslında bir **DAG** (Directed Acyclic Graph = Yönlü Döngüsüz Çizge) yapısındadır.

**Sınıf Hiyerarşisi:**

```
INode (interface)                    -> Tüm node'ların kontratı
  |-- NodeBase (abstract class)      -> Ortak implementasyon
           |-- TestNode (test'lerde)     -> Test amaçlı somut sınıf

Edge                                 -> İki node arasındaki bağlantı
Channel                              -> Node'lar arası veri taşıyıcı
Graph                                -> DAG yapısını yöneten ana sınıf
GraphExecutor                        -> Çalıştırma motoru
```

**Node Durumları (State Machine):**

```
             +------------+
             |   IDLE     |--> Başlangıç durumu
             +------------+
                  | (SetState)
                  v
          +-----------------+
          |                 |
    +-----------+    +-------------+
    | PENDING   |    | CANCELLED   |
    +-----------+    +-------------+
         |
         v
    +-----------+
    | RUNNING   |
    +-----------+
         |
    +------------------------------+
    |              |               |
+------------+ +------------+ +-------------+
| COMPLETED  | |  FAILED    | | CANCELLED   |
- HTTP timeout'u (ör: 30 saniye) workflow süresini kısıtlar
- Sunucu yeniden başlasa bile kuyruktaki işler kaybolmaz
- Birden fazla worker paralel iş yapabilir

**Kuyruklar:**

| Kuyruk | Öncelik | Kullanım |
|--------|---------|----------|
| `critical` | En yüksek | Sistem bakım işleri |
| `workflows` | Normal | Workflow execution'ları |
| `default` | Düşük | Diğer işler |

**Zamanlama Tipleri:**
```csharp
// 1. Hemen çalıştır
_jobService.EnqueueWorkflow(workflowId, triggerData);

// 2. Gecikmeli çalıştır (60 saniye sonra)
_jobService.ScheduleWorkflow(workflowId, TimeSpan.FromSeconds(60), triggerData);

// 3. Tekrarlayan (her saat başı)
_jobService.AddRecurringWorkflow(workflowId, "0 * * * *", "saatlik-rapor");
```

**Dashboard:** `https://localhost:PORT/hangfire` adresinden izlenebilir.

---

## 11. AI ve Vector Veritabanı

### Ollama (LLM)
- Yerel bilgisayarda çalışan AI modeli
- Semantic Kernel SDK üzerinden kullanılır
- Model: `qwen2.5-coder:14b` (varsayılan)

### ONNX Runtime (Embedding)
- Metin ? sayı vektörü dönüştürme
- Model: `nomic-embed-text` (768 boyut)
- Batch işleme + önbellekleme desteği

### pgvector (Vektör Arama)
- PostgreSQL uzantısı
- Cosine similarity ile benzer metinleri bulma
- IVFFlat indeksi ile hızlı arama

**Kullanım akışı:**
```
Metin ? OnnxTextEmbeddingService ? float[768] vektör
    ? PgVectorService.StoreAsync()      (kaydet)
    ? PgVectorService.SearchAsync()     (ara)
```

---

## 12. Güvenlik Katmanları

```
İstek
  │
  ▼
┌──────────────────┐
│ TenantMiddleware │ → X-Tenant-ID header veya subdomain'den tenant çözümler
                     HttpContext.Items["TenantInfo"] = TenantInfo

         

 Authentication     OIDC (interactive) veya JWT Bearer (API)
 MultiScheme         - AddOpenIdConnect: Tenant-bazlı dinamik Authority
│                     - AddJwtBearer("ExternalBearer"): API erişimi
                     - AddPolicyScheme("MultiScheme"): Otomatik seçim

         │
┌──────────────────┐
│ Claims             PermissionClaimsTransformation (IClaimsTransformation)
 Transformation      - DB'den kullanıcı rollerini yükler
│                  │   - TenantRole.MappedPermissions → "permission" claim
│                     - "sonofanton_user_id" + "tenant_id" claim eklenir

         

 Authorization      Policy-Based Authorization
                     - PermissionPolicyProvider: Her izin için dinamik policy
                     - PermissionAuthorizationHandler: Claim kontrolü
                     - System.Admin  tüm izinlere bypass
                     Örnek: [Authorize(Policy = "Workflows.Execute")]

         

 Rate Limiting      IP başına dakikada 100 istek
 (api policy)        Workflow exec: dakikada 10

         

 Security           X-Content-Type-Options: nosniff
 Headers             X-Frame-Options: DENY
                     X-XSS-Protection: 1; mode=block

         

 CORS               Sadece izin verilen origin'ler

         

 Input              SQL injection koruması (DatabaseHandler)
 Validation          Yasak SQL komutları (DROP, ALTER vb.)
                     Sorgu uzunluk limiti (5000 karakter)

         

 Webhook            HMAC-SHA256 signature doğrulama
 Security            Rate limiting (min interval)
                     [AllowAnonymous]  dış tetikleyiciler için

```

### 12.1 Keycloak OIDC Entegrasyonu

Proje, kimlik doğrulama sağlayıcısı olarak **Keycloak** kullanır. Keycloak Docker container olarak çalışır ve OIDC Authorization Code Flow (PKCE) ile kimlik doğrulama sağlar.

**Keycloak Konfigürasyonu:**

| Ayar | Değer |
|------|-------|
| Realm | `sonofanton` |
| Frontend Client | `sonofanton-frontend` (public, PKCE) |
| Backend Client | `sonofanton-backend` (confidential) |
| Admin Console | `http://localhost:8080/admin` |
| Realm URL | `http://localhost:8080/realms/sonofanton` |

**OIDC Akışı:**

```
  Kullanıcı → Blazor WASM → Keycloak Login Page
                                    │
                              OIDC Auth Code + PKCE
                                    │
                              Access Token (JWT)
                                    │
              Blazor WASM ← id_token + access_token
                    │
                    │ Authorization: Bearer <access_token>
                    ▼
              ASP.NET Core API → JWT doğrulama (Keycloak JWKS)
```

### 12.2 Kimlik ve Yetkilendirme Mimarisi

**Dosya Haritası:**

| Katman | Dosya | Sorumluluk |
|--------|-------|------------|
| **Shared/Core/Identity** | IUserContext.cs | Domain-level kullanıcı soyutlaması |
| | ITenantResolver.cs | Tenant çözümleme soyutlaması |
| | Permissions.cs | 20 granüler izin sabiti |
| **Shared/Entities** | UserEntity.cs | Kullanıcı (şifresiz, ExternalIdentityId) |
| | TenantEntity.cs | Kurum bilgisi + OIDC config |
| | TenantRoleEntity.cs | Dış rol → iç izin eşleştirmesi |
| | UserTenantRoleEntity.cs | User-Role many-to-many |
| **Backend/Infrastructure/Identity** | HttpContextUserContext.cs | IUserContext → HttpContext adapter |
| | TenantResolver.cs | Subdomain/header tenant çözümleme |
| | TenantMiddleware.cs | Auth öncesi tenant injection |
| | DynamicOidcEvents.cs | Tenant-bazlı OIDC parametre override |
| | PermissionClaimsTransformation.cs | Rol → izin claim dönüşümü |
| | PermissionPolicyProvider.cs | Dinamik policy üretici |
| | PermissionAuthorizationHandler.cs | Permission claim kontrolü |
| | PermissionRequirement.cs | IAuthorizationRequirement |
| | JitUserProvisioningService.cs | İlk giriş otomatik kullanıcı oluşturma |
| **Backend/Services** | ScimService.cs | SCIM 2.0 User/Group CRUD |
| **Backend/Controllers/Scim** | ScimUsersController.cs | SCIM Users REST API |
| | ScimGroupsController.cs | SCIM Groups REST API |
| **Frontend/Components** | LoginDisplay.razor | Kullanıcı giriş/çıkış UI bileşeni |
| | RedirectToLogin.razor | Yetkisiz kullanıcıyı login'e yönlendirir |
| **Frontend/Pages** | Authentication.razor | OIDC login/logout/callback handler |

### 12.3 Frontend OIDC Mimarisi

Blazer WASM tarafındaki kimlik doğrulama akışı:

```
Program.cs
  └── AddOidcAuthentication()
        ├── Authority: Keycloak realm URL
        ├── ClientId: sonofanton-frontend (public client)
        ├── ResponseType: code (Authorization Code Flow)
        ├── DefaultScopes: openid, profile, email
        ├── RoleClaim: "roles" (Keycloak rol mapping)
        └── NameClaim: "preferred_username" (Keycloak kullanıcı adı)

  └── AddHttpClient("BackendApi")
        └── AuthorizationMessageHandler
              → Her API çağrısına otomatik Bearer token ekler
              → authorizedUrls: [backendUrl]
```

**Blazor Bileşenleri:**
- `LoginDisplay.razor` — `<AuthorizeView>` ile giriş yapmış/yapmamış kullanıcıları gösterir
- `RedirectToLogin.razor` — Yetkisiz sayfaya erişimde `authentication/login`'e yönlendirir
- `Authentication.razor` — `<RemoteAuthenticatorView>` ile OIDC callback'leri işler

**Frontend `wwwroot/appsettings.json`:**
```json
{
  "BackendUrl": "https://localhost:5002",
  "Oidc": {
    "Authority": "http://localhost:8080/realms/sonofanton",
    "ClientId": "sonofanton-frontend",
    "ResponseType": "code",
    "DefaultScopes": ["openid", "profile", "email"]
  }
}
```

### 12.4 İzin Matrisi

```
Permissions.cs sabitleri:
─ Workflow İzinleri ─
  Workflows.Read, Workflows.Create, Workflows.Edit,
  Workflows.Delete, Workflows.Execute

─ Execution İzinleri ─
  Executions.Read, Executions.Cancel, Executions.Pause

─ Kullanıcı Yönetimi ─
  Users.Read, Users.Manage

─ Sistem Yönetimi ─
  System.Admin (tüm izinleri bypass eder), Tenants.Manage

─ Diğer ─
  Webhooks.Read, Webhooks.Manage
  Folders.Read, Folders.Manage
  Agents.Read, Agents.Manage
  Search.Execute, Chat.Use
```

### 12.5 Multi-Tenancy

Tenant çözümleme stratejisi appsettings.json:
```json
{
  "MultiTenancy": {
    "ResolveBy": "Header",        // "Header" | "Subdomain" | fallback
    "DefaultTenantId": "default"
  }
}
```

- **Header:** X-Tenant-ID: kurumA
- **Subdomain:** kurumA.uygulama.com
- Tüm iş entity'lerinde (workflows, folders) `tenant_id` FK bulunur
- Tenant başına farklı OIDC Authority/ClientId tanımlanabilir (Keycloak multi-realm)

### 12.6 JIT User Provisioning

1. Kullanıcı Keycloak üzerinden OIDC ile giriş yapar
2. DynamicOidcEvents.OnTokenValidated tetiklenir
3. JitUserProvisioningService.ProvisionUserAsync() çağrılır
4. Token'daki sub/oid → DB'de ExternalIdentityId ile aranır
5. Yoksa → yeni UserEntity + TenantEntity oluşturulur
6. Varsa → LastLoginAt, Email, DisplayName güncellenir

### 12.7 SCIM 2.0 Provisioning

Kurumsal kimlik sağlayıcıları (Keycloak, Entra ID, Okta vb.) için:

- POST /scim/v2/Users → Kullanıcı oluştur
- GET /scim/v2/Users → Kullanıcı listesi
- PATCH /scim/v2/Users/{id} → Deaktivasyon vb.
- DELETE /scim/v2/Users/{id} → Soft delete
- POST /scim/v2/Groups → Grup oluştur
- GET /scim/v2/Groups → Grup listesi
- DELETE /scim/v2/Groups/{id} → Grup sil

---

## 13. Test Stratejisi

**Test Piramidi:**

```
         ??
        ?  |         UI Testleri (az)
       ???|--        - Blazor component testleri
      |      ?
     -------|--      Entegrasyon Testleri (orta)
    |          |     - Handler zincirleri
   -----------|--    - Node etkileşimleri
  |              ?
 ---------------|--  Birim Testleri (çok)
|                  ? - Graph, Edge, Node
----------------------- Handler'lar, Servisler
```

**Test çalıştırma:**
```bash
# Tüm testler
dotnet test

# Coverage ile
dotnet test --collect:"XPlat Code Coverage"

# Belirli test
dotnet test --filter "ClassName=GraphTests"
```

**Test dosya konumları:**
- `tests/Unit/GraphEngine/` — Graph, Edge, NodeBase testleri
- `tests/Unit/Handlers/` — Her handler grubu için testler
- `tests/Unit/Services/` — NodeRegistry, CronValidation testleri
- `tests/Integration/` — Çoklu handler etkileşim testleri

---

## 14. Sık Yapılan Görevler

### Yeni bir Node Handler eklemek
Bkz: [Node Handler Sistemi](#7-node-handler-sistemi) bölümü

### Migration oluşturmak
```bash
cd src/SonOfAnton.Backend
dotnet ef migrations add MigrationAdi
dotnet ef database update
```

### Yeni bir API endpoint eklemek
1. İlgili Controller'a method ekle
2. XML dokümantasyonu yaz (`/// <summary>`)
3. `[ProducesResponseType]` attribute'ları ekle
4. Test yaz

### Projeyi çalıştırmak
```bash
# Gereksinimler: PostgreSQL, Ollama (opsiyonel)
# 1. Backend
cd src/SonOfAnton.Backend
dotnet run

# 2. Frontend (ayrı terminal)
cd src/SonOfAnton.Frontend
dotnet run
```

### Test çalıştırmak
```bash
cd tests/SonOfAnton.Tests
dotnet test --verbosity normal
```

---

> **Soruların mı var?** Kodu okurken takıldığın bir yer olursa
> önce bu dokümanın ilgili bölümüne bak, sonra dosya başlarındaki
> XML yorumlarını oku. Yine anlamadıysan takım arkadaşlarına sor!

---

## 15. Yeni Mimari Bilesenler (v2)

Asagidaki bilesenler ikinci fazda eklenmistir:

### Expression Engine
- `Engine/Expressions/ExpressionEvaluator.cs` - `{{variable}}` template cozumleme
- `Engine/Expressions/VariableStore.cs` - Degisken deposu
- Her node'un parametreleri calistirilmadan once resolve edilir

### Pause / Resume Mekanizmasi
- `Execution/PauseToken.cs` - ManualResetEventSlim tabanli duraklatma
- `PauseTokenRegistry` - Execution ID'ye gore PauseToken yonetimi
- WorkflowRunner her node oncesi `WaitIfPausedAsync()` cagirir

### Snapshot Sistemi
- `Services/Snapshots/SnapshotService.cs` - Otomatik/manuel snapshot
- Hata durumunda otomatik snapshot alinir
- Snapshot'tan execution geri yuklenebilir

### A2A Protocol
- `A2A/IA2AAgent.cs` - Agent arayuzu
- `A2A/AgentRegistry.cs` - Agent kesif ve kayit
- `A2A/A2ATaskManager.cs` - Asenkron task yonetimi
- `A2A/A2AAgentAdapters.cs` - Mevcut AgentService'i A2A'ya adapte eden siniflar

### Arama Altyapisi
- `Services/Search/FullTextSearchService.cs` - PostgreSQL tsvector ile tam metin arama
- `Services/Search/HybridSearchService.cs` - Vektor + full-text birlestirmesi

### Workflow Chat Asistan
- `Agents/WorkflowAssistantService.cs` - Dogal dil -> workflow graph cevirici
- Semantic Kernel ile Ollama LLM entegrasyonu
- JSON ve YAML blok parse edici

### Klasor Sistemi
- `FolderEntity` - Hiyerarsik, self-referencing entity
- `FoldersController` - CRUD + agac yapisi endpoint'leri

### YAML Serialization
- `Shared/Serialization/WorkflowYamlSerializer.cs` - YamlDotNet ile serialize/deserialize
- Validation destegi (hata listesi doner)
