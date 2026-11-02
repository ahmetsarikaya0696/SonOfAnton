# SonOfAnton — Kod incelemesi ve sadeleştirme planı

İnceleme tarihi: **2 Kasım 2026**. Kaynak: `master`, commit `c91a288cede2bf5069496afa3f1c74cd46b79424`.

## Sonuç ve kapsam

Proje, görsel workflow otomasyon çekirdeğinin çevresine marketplace, özel kod çalıştırma, chatbot, veri tabloları/formları, SCIM, A2A, RAG ve otonom planlama eklemiş. Bu genişlik henüz güvenlik, yürütme güvenilirliği ve testlerle desteklenmiyor. Öncelik yeni özellik eklemek değil, güvenli ve tekrar üretilebilir tek bir workflow çalıştırma yolu elde etmek olmalı.

Bu rapor statik kod incelemesidir; production sızma testi veya tüm özelliklerin uçtan uca doğrulaması değildir. Controller/servis/DI bağlantıları, veri modeli, yürütme ve SSE yolları, proje/Compose dosyaları ve mevcut Markdown belgeleri incelendi. Ortamda `dotnet` bulunmadığından build, migration ve uygulama testleri çalıştırılamadı. Depoda test projesi ve GitHub Actions workflow'u bulunmuyor. `src/openapi_test_app` bir örnek uygulamadır, otomatik test paketi değildir.

**Tarihsel kayıt:** Bulgular `c91a288` içindir. Sonraki uygulama çalışmasının güncel durumu [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) ve [VERIFICATION.md](VERIFICATION.md) içindedir. Aşağıdaki eski kod örnekleri güncel durum iddiası değildir.

| Bulgu | Çekirdek çalışmasındaki karşılık |
|---|---|
| F01–F04 | JWT/fallback doğrulaması, Hangfire paneli/legacy kaldırma, tenant/workspace kaynak kontrolü |
| F05–F06 | Published API ve script/plugin yürütmesi kapalı; özelliklerin tamamlandığı iddia edilmiyor |
| F07–F08 | Rate limiter bağlandı; API key claim sözleşmesi ve scope yönetimi düzeltildi |
| F09–F10 | Tek job yolu, açık bağlam, tekrar teslim koruması, bounded broadcast ve kalıcı log stream |
| F11 | AI node'ları opt-in; embedding DI düzeltildi, gerçek model/RAG kabulü açık |
| F12 | AES-GCM, zorunlu anahtar ve açık legacy geçişi; credential yönetim API'si kapalı |
| F13 | UTC cron, yeni recurring execution ID, fail-fast migration ve eksik şema düzeltmesi |
| F14 | Public chatbot erişimi kapalı; ürünleştirme açık |
| F15 | Plan, çalışma talimatları, API/kurulum/kullanım ve test kanıtı güncellendi |

Kapalı bir endpoint için asıl özelliğin güvenli ve tamamlanmış olduğu sonucunu çıkarmayın.

## Doğrulanmış bulgular

P0: dış erişim/çok kullanıcılı dağıtım öncesi kapat. P1: çekirdek işlev güvenilirliği için düzelt. P2: bakım ve kapsam sadeleştirmesi. Öncelikler bu incelemenin değerlendirmesidir.

### F01 — P0: JWT doğrulama production'da da gevşek

Kanıt: [Program.cs](../src/SonOfAnton.Backend/Program.cs), `AddJwtBearer`.

`ValidateIssuer = false` ve `ValidateAudience = false` ortam koşuluna bağlı değil. Yorumdaki “dev” ifadesi production davranışını değiştirmiyor. İmza ve süre kontrolü bulunması, token'ın bu uygulama/issuer için üretildiğini doğrulamaz. Ayrıca bütün claim'ler Information seviyesinde loglanıyor.

Düzeltme: güvenilen issuer/audience yapılandırmasını zorunlu tut; hatalı yapılandırmada başlangıcı reddet; claim dökümünü kaldır. Kabul: yanlış issuer/audience ve süresi dolmuş token 401, doğru token beklenen policy sonucunu vermeli.

### F02 — P0: Production Hangfire panelinde yetki filtresi yok

Kanıt: [Program.cs](../src/SonOfAnton.Backend/Program.cs), `UseHangfireDashboard`.

Development için local-only filtre var; diğer ortamlarda `Authorization = []`. Panel için ayrıca bir yetki kontrolü tanımlanmıyor. Dışarı açılan backend üzerinde iş yönetim yüzeyi korunmuyor.

Düzeltme: paneli varsayılan kapat veya açık bir yönetici policy'siyle koru. Kabul: anonim ve normal kullanıcı erişimi reddedilmeli; yalnızca yönetici ulaşabilmeli.

### F03 — P0: Workflow verileri tenant/workspace ile sınırlandırılmıyor

Kanıt: [WorkflowsController](../src/SonOfAnton.Backend/Controllers/WorkflowsController.cs), `GetAll`, `Get`, `Save`, `Execute`; [DbContext](../src/SonOfAnton.Backend/Data/SonOfAntonDbContext.cs).

Liste sorgusu tüm `Workflows` üzerinde; ayrıntı ve değişiklik yolları yalnızca ID ile erişiyor. DbContext'te `HasQueryFilter` yok. Yeni workflow oluşturulurken tenant, workspace ve kullanıcı ataması yapılmıyor. Permission kontrolü kaynak sahipliği kontrolünün yerini tutmuyor. Cancel/pause/restore/move gibi bazı mutasyonlar yalnızca controller düzeyindeki read policy ile kalıyor; ayrı yazma/çalıştırma izni de denetlenmeli.

Düzeltme: doğrulanmış kullanıcıdan tenant/workspace bağlamını üret, üyeliği doğrula ve her okuma/yazma/çalıştırma yoluna uygula; background job'a da aynı kapsamı açıkça taşı. Kabul: tenant A kullanıcısı tenant B'nin bilinen ID'siyle listeleme, okuma, güncelleme, silme ve çalıştırma yapamamalı. Header değiştirerek üyelik kazanılamamalı.

### F04 — P0: Anonim yönetim yolları var

Kanıt: [eski WorkflowController](https://github.com/ahmetsarikaya0696/SonOfAnton/blob/c91a288cede2bf5069496afa3f1c74cd46b79424/src/SonOfAnton.Backend/Controllers/WorkflowController.cs), [ChatbotController](../src/SonOfAnton.Backend/Controllers/ChatbotController.cs), [ChatbotService](../src/SonOfAnton.Backend/Services/ChatbotService.cs), [Program.cs](../src/SonOfAnton.Backend/Program.cs).

`/api/workflow` statik bir liste üzerinde CRUD yapıyor ve `[Authorize]` taşımıyor. `/api/chatbots` yönetim uçları da anonim; istemcinin verdiği `X-Workspace-Id` üyelik doğrulanmadan kullanılıyor. `WorkflowChatController` da explicit authorization taşımıyor. Global fallback authorization policy tanımlı değil. Execution SSE, SSE istatistikleri ve metrics minimal endpoint'lerinde de `RequireAuthorization` yok; execution stream sahipliği kontrol edilmiyor.

Düzeltme: eski tekil workflow CRUD API'sini istemci kullanım kontrolünden sonra kaldır; yönetim uçlarına yetki ve kaynak kapsamı ekle. Anonim public chatbot ve webhook davranışını ayrı, açık bir sözleşme olarak tut. Kabul: anonim yönetim ve başkasına ait stream erişimleri reddedilmeli.

### F05 — P0: Published API'de anahtarın hedef kaynağa yetkisi doğrulanmıyor

Kanıt: [DynamicWorkflowEndpointMiddleware](../src/SonOfAnton.Backend/Infrastructure/DynamicWorkflowEndpointMiddleware.cs).

`RequireApiKey` açıkken anahtarın geçerli olması kontrol ediliyor; endpoint tenant'ı, workflow erişimi ve scope eşleşmesi kontrol edilmiyor. Tüm request header'ları trigger verisine aktarılıyor; `Authorization` ve `X-Api-Key` de buna dahil. Input schema doğrulaması TODO olarak kalmış.

Düzeltme: anahtar–tenant–workflow–scope eşleşmesi; hassas header allowlist/redaksiyonu; schema ve body boyutu sınırı. Kabul: başka tenant'ın geçerli anahtarı reddedilmeli, sırlar job argümanlarına/execution memory'ye kaydolmamalı.

### F06 — P0: Özel C# kodu API sürecinde çalışıyor

Kanıt: [CustomNodeHandler](../src/SonOfAnton.Backend/Engine/Handlers/CustomNodeHandler.cs), `ExecuteCSharpScriptAsync`.

Roslyn `CSharpScript.EvaluateAsync` doğrudan backend içinde çağrılıyor; bu yol için ayrı işlem izolasyonu ve zorunlu kaynak sınırı görünmüyor. Import/reference listesi bir güvenlik sandbox'ı değildir.

Düzeltme: MVP'de yalnızca güvenilen yönetici koduna izin ver veya bu özelliği kapat. Güvenilmeyen kod ihtiyacı kanıtlanırsa ayrı, kısıtlı worker tasarla. Kabul: sıradan kullanıcı script yayımlayamamalı/çalıştıramamalı; izolasyon tasarımı olmadan çok kiracılı kullanıma açılmamalı.

### F07 — P1: Rate limit politikaları tanımlı, endpoint'lere bağlanmamış

Kanıt: [Program.cs](../src/SonOfAnton.Backend/Program.cs) ve controller taraması.

`api`, `workflow-execution`, `apikey` adlarıyla policy var; `EnableRateLimiting`, `RequireRateLimiting` veya `GlobalLimiter` kullanımı bulunmadı. `UseRateLimiter` ayrıca authentication'dan önce. Dolayısıyla belgelerdeki genel 100/10 istek garantisi mevcut değil; webhook'un kendi aralık kontrolü ayrı bir mekanizma.

Düzeltme: policy'leri ilgili endpoint'lere bağla; kimliğe bağlı limitlemede authentication sırasını düzelt. Kabul: eşik aşılınca 429 ve iki farklı anahtar için bağımsız sayaç.

### F08 — P1: API key claim sözleşmesi tutarsız

Kanıt: [ApiKeyAuthenticationHandler](../src/SonOfAnton.Backend/Infrastructure/Identity/ApiKeyAuthenticationHandler.cs), [HttpContextUserContext](../src/SonOfAnton.Backend/Infrastructure/Identity/HttpContextUserContext.cs), [Permissions](../src/SonOfAnton.Shared/Core/Identity/Permissions.cs).

Anahtar handler'ı `TenantId` ve `Permission` üretirken ortak bağlam `tenant_id` ve `permission` okuyor. Tenant bağlamı kaybolabilir; izin dönüşümü/karşılaştırması davranışı entegrasyon testiyle sabitlenmeli. `UpdateLastUsedAsync` aynı scoped DbContext üzerinde await edilmeden başlatılıyor; eşzamanlı kullanım/disposal riski taşıyor.

Düzeltme: tek claim sabitleri seti kullan, anahtar sahibini anahtar ID'sinden ayır, son kullanım kaydını güvenli lifecycle ile yaz. Kabul: izinli/yetkisiz anahtar ve tenant bağlamı testleri.

### F09 — P1: Retry aynı execution kaydını yeniden ekliyor

Kanıt: [WorkflowJobService](../src/SonOfAnton.Backend/Services/WorkflowJobService.cs), `ExecuteWorkflowAsync`.

Metot üç retry tanımlıyor; her çağrıda aynı `executionId` ile `Executions.Add` yapıyor. İlk kayıt sonrasında fırlayan bir hata yeniden denendiğinde PK çakışması oluşturabilir. Ayrıca `WorkflowsController.Execute` doğrudan runner'ı kuyruğa eklerken diğer yollar job service kullanıyor; davranışlar ayrışıyor.

Düzeltme: ortak enqueue/run yolu, mevcut execution üzerinde idempotent durum geçişi, dış yan etkiler için idempotency anahtarı. Kabul: kayıt sonrası kontrollü hata ve retry tek execution üretmeli; e-posta/HTTP yan etkisi mükerrer olmamalı.

### F10 — P1: SSE çok istemcili yayın gibi davranmıyor

Kanıt: [SseEventService](../src/SonOfAnton.Backend/Services/SseEventService.cs), [Program.cs](../src/SonOfAnton.Backend/Program.cs), [WorkflowJobService](../src/SonOfAnton.Backend/Services/WorkflowJobService.cs).

Bir execution için tek `Channel` var; her istemci aynı reader'dan okuyor. Okunan olay her aboneye kopyalanmıyor. `CreateChannel` mevcut kanalı değiştiriyor; enqueue ve worker çağrıları önce bağlanan istemciyi eski kanalda bırakabilir. Kanallar sınırsız; heartbeat ve event yazımı aynı writer üzerinde eşzamanlı yürütülüyor, heartbeat task'ı beklenmiyor.

Düzeltme: abone başına bounded channel veya DB durum polling'i; tek writer; tamamlanma/disconnect temizliği. Kabul: iki istemci aynı olayları aynı sırayla almalı, erken bağlanan istemci kopmamalı, yavaş istemci bellek büyütmemeli. Mevcut süreç içi durum yatay ölçekleme garantisi sağlamaz.

### F11 — P1: Embedding servisi ve bazı AI handler'ları DI'a bağlı değil

Kanıt: [Program.cs](../src/SonOfAnton.Backend/Program.cs), [MemoryEmbeddingService](../src/SonOfAnton.Backend/Services/Memory/MemoryEmbeddingService.cs), [AINodeHandlers](../src/SonOfAnton.Backend/Engine/Handlers/AINodeHandlers.cs), [AIAgentHandler](../src/SonOfAnton.Backend/Engine/Handlers/AIAgentHandler.cs).

`ITextEmbeddingService` implementasyonları var fakat uygulama DI kaydı yok; memory embedding çağrısı servis çözümlenemediğinde exception atıyor. Semantic Kernel'in kendi embedding generator kaydı bu arayüzün kaydı değil. `ai.sentiment`, `ai.classify`, `ai.vision`, `ai.agent` handler sınıfları da `INodeHandler` olarak kayıtlı değil. Palette/sınıf varlığı çalışır özellik kanıtı değildir.

Düzeltme: tek embedding sağlayıcısı seç ve doğru arayüze bağla; desteklenen node listesiyle DI kayıtlarını eşleştir. Kabul: kaydedilen her node tipi çözümlenmeli; gerçek embedding üretimi ve vector round-trip testi geçmeli. `vector_store` 768, `memory_entries` 384 boyut kullanıyor; modeller karıştırılmamalı.

### F12 — P1: Credential şifrelemesinde sabit fallback anahtar

Kanıt: [EncryptionService](../src/SonOfAnton.Backend/Services/EncryptionService.cs).

Eksik ayarda kaynakta bulunan sabit anahtar kullanılıyor; anahtar sessizce pad/truncate ediliyor. Decrypt hatası boş string'e çevriliyor. CBC çıktısında ayrıca bütünlük etiketi yok.

Düzeltme: secret store'dan geçerli anahtarı zorunlu yükle, sürümlü authenticated encryption ve mevcut kayıtlar için geçiş planı oluştur. Kabul: yanlış anahtar/tahrif edilmiş ciphertext açık hata üretmeli; rotasyon sonrasında eski kayıtlar kontrollü taşınabilmeli.

### F13 — P1: Zamanlama ve startup davranışları yanıltıcı

Kanıt: [WorkflowJobService](../src/SonOfAnton.Backend/Services/WorkflowJobService.cs), `PauseRecurringWorkflow`; [Program.cs](../src/SonOfAnton.Backend/Program.cs).

Pause metodu `RecurringJob.TriggerJob` çağırıyor; işi duraklatmak yerine tetikliyor. Cron doğrulayıcı özel parser kullanıyor ve altı alanı da kabul ediyor; scheduler ile ortak doğrulama yok. Migration exception'ı loglanıp uygulama devam ediyor. Readiness şema güncelliğini doğrulamıyor.

Düzeltme: gerçek pause semantiği, scheduler ile uyumlu tek cron parser ve timezone sözleşmesi; migration başarısızlığında fail-fast veya readiness başarısızlığı. Kabul: pause sonrası yeni koşum oluşmamalı; uyumsuz şema sağlıklı sayılmamalı.

### F14 — P1: Public chatbot oturumu ve model seçimi eksik

Kanıt: [ChatbotController](../src/SonOfAnton.Backend/Controllers/ChatbotController.cs), [ChatbotService](../src/SonOfAnton.Backend/Services/ChatbotService.cs).

Public oturum oluştururken `IsPublic` kontrol ediliyor; mesaj gönderirken yalnızca session ID aranıyor. Bot sonradan private yapılsa bile bu yol public durumunu yeniden doğrulamıyor. Botun `Model` alanı kaydediliyor fakat completion çağrısında kullanılmıyor; global Kernel servisi çağrılıyor. History sınırsız büyüyor.

Düzeltme: her mesajda public/oturum yetkisi, istek/kota/history sınırı ve gerçekten uygulanan model seçimi. Kabul: private'a alınan botun eski public oturumu mesaj gönderememeli; model tercihi kullanılan servise yansımalı.

### F15 — P2: Dokümantasyon uygulamadan kopmuş

Önceki belgeler olmayan test/CI yolları, `backend` Compose servisi, yanlış portlar, eksik LICENSE dosyası ve bağlanmamış özellikleri tamamlanmış gibi anlatıyordu. Mimari dosyasında düzenleme çıktıları ve yinelenen giriş; kullanım rehberinde yerel Windows ekran görüntüsü yolları vardı. Bu PR bu belge sorunlarını giderir; runtime kusurlarını gidermez.

## Gereksiz karmaşıklık: karar tablosu

Bu bölüm ürün kapsamı önerisidir; kullanıcı talebi/gerçek trafik verisi bulunmayan özellikler “gereksiz olduğu kesin” diye silinmemelidir.

| Alan | Kod kanıtı / maliyet | Öneri | Geri alma / tutma koşulu |
|---|---|---|---|
| RabbitMQ + MassTransit | Program'da bus ve filtreler var; uygulamada `IConsumer`, `IPublishEndpoint` veya `IBus` kullanımı bulunmadı; Compose ek servis taşıyor | MVP bağımlılıklarından kaldırma adayı; Hangfire kalsın | Bağımsız süreçler arası somut mesaj sözleşmesi oluşursa ekle |
| Birden fazla yürütme motoru | Aktif `WorkflowRunner` yanında `Execution/WorkflowExecutor` ve Shared `GraphExecutor`; alternatif executor'lara uygulama çağrısı bulunmadı; WorkflowExecutor AI placeholder içeriyor | Çağrı/harici kullanım kontrolü sonrası kullanılmayan executor'ları kaldır | Shared graph veri tiplerini sırf executor kullanılmıyor diye silme |
| Tekil/çoğul workflow API | Statik liste ve EF kalıcılığı iki ayrı sözleşme | `/api/workflows` üzerinde birleş; eski CRUD'u kaldır | `/api/workflow/chat` ve execution SSE ayrı rotalar, topluca silinmemeli |
| A2A + proje agent orkestrasyonu | Registry, task manager, adapters, orchestrator; startup scope'undan alınan AgentService singleton registry'de tutuluyor | Çekirdek workflow dışında tut; lifecycle'ı düzeltmeden etkin ürün özelliği sayma | Gerçek dış agent entegrasyonu ve uyumluluk testleri |
| Ralph loop + Markdown watcher | Hosted polling ve Hangfire yedek çalıştırıcısı aynı aktif loop'ları işliyor; Program iki ayrı RalphLoopEngine kaydı oluşturuyor | Tek scheduling sahibi; çekirdek MVP dışında tut | Dağıtık sahiplik/lease ve tekrar çalıştırma testleri |
| Marketplace + script + plugin SDK | Yayınlama, kurma, review, Roslyn/Jint/assembly güvenlik yüzeyi | İlk aşamada sürümlenen dahili şablon kataloğu | Harici geliştirici ihtiyacı ve güvenli çalıştırma sınırı |
| DataTables + Forms + Chatbot | Ayrı ürün yüzeyleri, CRUD ve yetkilendirme yükü | Workflow çekirdeği sağlamlaşana kadar geliştirmeyi ertele | Kullanıcı senaryosu ve bakım kapasitesi |
| Çoklu AI sağlayıcı + ONNX/Ollama/RAG | Birden çok model/adapter ve 384/768 boyut sözleşmesi | MVP'de bir LLM ve bir embedding yolu | Ölçülmüş kalite, maliyet veya gizlilik gerekçesi |
| Özel SSE priority/topic/session altyapısı | Tek kanal broadcast hatası çözülmeden metrik ve filtre katmanları büyümüş | Önce basit, doğru abone modeli; sonra ihtiyaçla genişlet | Çok istemci ve backpressure testleri |
| Dinamik tenant OIDC + SCIM | Tenant konfigürasyonu mevcut fakat aktif backend JWT kaydı tek authority kullanıyor | İlk sürümde tek güvenilir issuer; SCIM'i kurumsal ihtiyaçla ele al | Çok issuer doğrulaması ve provisioning uyumluluk testleri |

**Korunacak çekirdek:** Blazor editor, ASP.NET Core monolith, EF Core/PostgreSQL, `WorkflowRunner` + `INodeHandler`, Hangfire, kimlik ve kaynak yetkilendirmesi. Feature klasörleri bakım kolaylığı için kullanılabilir; yeni katmanlar, generic repository, microservice veya yeni broker bu kusurları çözmez.

## Önceliklendirilmiş çalışma planı

- [ ] **1. Yayın engelleri** — F01–F06 ve F12.
  - [ ] JWT/panel/anonim yönetim yollarını kapat.
  - [ ] Kaynak kapsamını workflow, execution, chatbot ve published API boyunca uygula.
  - [ ] Script güven sınırı ve credential geçişini tamamla.
  - [ ] Anonim, read-only kullanıcı, başka tenant ve API key için negatif entegrasyon testleri yaz.
- [ ] **2. Tek güvenilir yürütme yolu** — F07–F11 ve F13–F14.
  - [ ] Enqueue ve retry davranışını birleştir; kayıt/yan etki idempotency'si ekle.
  - [ ] SSE çok istemci ve disconnect testlerini geçir.
  - [ ] Handler/embedding DI sözleşmesini doğrula; eksik node'ları palette'ten gizle veya tamamla.
  - [ ] Pause, cron/timezone, migration hata ve chatbot oturum davranışını düzelt.
- [ ] **3. Tekrarlanabilir doğrulama**.
  - [ ] Gerçek test projesi oluştur; temiz PostgreSQL üzerinde migration ve HTTP entegrasyon testleri ekle.
  - [ ] CI'da restore/build/test çalıştır; SDK/tool sürümlerini sabitle.
  - [ ] Minimal akışı kaydet → çalıştır → log/sonuç → yeniden başlatınca kalıcılık senaryosunu doğrula.
- [ ] **4. Kapsamı küçült**.
  - [ ] RabbitMQ ve kullanılmayan executor'ların referans/harici tüketici kontrolünü yap.
  - [ ] Ertelenen özelliklerin startup, menü ve DI yükünü azalt; veri/migration geçmişini koru.
  - [ ] Tek issuer, tek AI yolu ve tek scheduling sahibiyle kurulum maliyetini ölç.
- [ ] **5. Yayın hazırlığı**.
  - [ ] Yedek/geri yükleme, secret rotasyonu ve upgrade senaryolarını prova et.
  - [ ] Lisansı açıkça belirle; LICENSE dosyası eklenmeden MIT lisanslı olduğunu iddia etme.
  - [ ] OpenAPI ile belgeleri karşılaştır; çalışan build ve test sonuçlarını kaydet.

## Doğrulama sınırları

Kod kusurları bu incelemede çalıştırılarak istismar edilmedi; etkiler kaynak akışından çıkarıldı. DB migration zinciri, sağlayıcı erişimleri, frontend davranışları, performans ve tam SCIM/A2A uyumluluğu ayrıca test edilmeli. Belgelerin bağlantıları, Markdown çitleri ve yalnızca `.md` değişmesi bu PR'da yerel olarak kontrol edildi: yedi Markdown dosyasında kırık yerel bağlantı veya dengesiz kod çiti bulunmadı; `git diff --check` geçti.
