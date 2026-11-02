# SonOfAnton — Kurulum ve dağıtım

Bu kılavuz `c91a288` kaynak durumuna göredir. Uygulama bu incelemede çalıştırılmadı; ortamda .NET SDK yoktu. Production yayınından önce [REVIEW.md](REVIEW.md) içindeki P0/P1 kabul kriterleri tamamlanmalıdır.

## Yerel kurulum

.NET 10 SDK ve Docker Compose kurulu olmalı. AI akışları ayrıca Ollama/model gerektirir. ONNX Runtime NuGet bağımlılığıdır; model dosyaları ayrı indirilir. İşletim sisteminizin desteklediği şekilde geliştirme HTTPS sertifikasına güven tanımlayın.

Depo kökünde:

```bash
docker compose up -d
docker compose ps
dotnet restore SonOfAnton.slnx
dotnet build SonOfAnton.slnx --no-restore
dotnet dev-certs https --trust
dotnet run --project src/SonOfAnton.Backend --launch-profile https
```

Ayrı terminalde:

```bash
dotnet run --project src/SonOfAnton.Frontend --launch-profile https
```

`dotnet run` için HTTPS profili açıkça seçilir; frontend'in `BackendUrl` değeri `https://localhost:5002` ile uyumlu olmalıdır. Backend Development ayarı Keycloak authority'sini `http://localhost:8080/realms/sonofanton` olarak değiştirir. Production temel ayarındaki placeholder değerleri gerçek dağıtım ayarları değildir.

| Adres / port | Kaynak / kullanım |
|---|---|
| `https://localhost:5002`, `http://localhost:5003` | Backend `Properties/launchSettings.json` |
| `https://localhost:5001`, `http://localhost:5000` | Frontend `Properties/launchSettings.json` |
| `localhost:5432` | PostgreSQL/pgvector |
| `http://localhost:5050` | pgAdmin |
| `http://localhost:8080` | Keycloak |
| `localhost:5672`, `http://localhost:15672` | RabbitMQ ve yönetim arayüzü |
| `http://localhost:11434` | Yerel Ollama; Compose içinde değil |

Compose, backend/frontend servisi veya Dockerfile içermez. `docker compose logs -f backend` geçerli değildir; uygulama logları `dotnet run` terminalindedir. Altyapı logları:

```bash
docker compose logs -f postgres keycloak rabbitmq
```

Compose parolaları geliştirme örnekleridir; Keycloak `start-dev` kullanır. Realm kaynağı [sonofanton-realm.json](../keycloak/sonofanton-realm.json); import dizini `./keycloak` olarak bağlanır. Gerçek parolaları repoya kaydetmeyin. Varsayılan Compose port eşlemeleri yalnızca loopback'e bağlanmış değildir; yerel makine ağ erişimini dikkate alın.

## AI hazırlığı ve mevcut eksikler

Ollama kurulup çalıştırıldıktan sonra, varsayılan model adlarıyla:

```bash
ollama pull qwen2.5-coder:14b
ollama pull nomic-embed-text
ollama list
```

Bu komutlar modeli indirir; donanım kapasitesi ayrıca değerlendirilmelidir. `OllamaStartupService` ve `ModelDownloadService` her başlangıçta kayıtlıdır. AI kullanmayacak geliştirme senaryosu için bu servisleri kapatan genel bir özellik bayrağı varmış gibi davranmayın.

**Bilinen engel:** Uygulama DI'ında `ITextEmbeddingService` kaydı eksik. Model indirmek tek başına Memory/RAG yolunu düzeltmez. Bazı AI node handler kayıtları da eksiktir; rapor F11.

## Veritabanı

Backend başlangıçta `MigrateAsync` çağırır. Exception loglanıp uygulama devam ettiği için “HTTP açıldı” başarılı migration kanıtı değildir. Mevcut migration'ları uygulayın; kurulum için yeni migration üretmeyin. Ayrıntılı komutlar [DATABASE.md](DATABASE.md) içinde.

## Yapılandırma

| Anahtar / ortam değişkeni | Mevcut tüketici ve sınır |
|---|---|
| `ConnectionStrings__DefaultConnection` | EF Core, Hangfire, vector servisleri |
| `Authentication__DefaultAuthority` | Backend JwtBearer authority; issuer/audience doğrulamasını tek başına düzeltmez |
| `MultiTenancy__ResolveBy`, `MultiTenancy__DefaultTenantId` | Tenant resolver/middleware; kaynak izolasyonu garantisi değildir |
| `Encryption__Key` | Credential encryption; mevcut kod 32 byte doğrulaması yerine pad/truncate yapar, düzeltilmeli |
| `RabbitMQ__Host`, `RabbitMQ__Port`, `RabbitMQ__Username`, `RabbitMQ__Password` | MassTransit bağlantısı; mevcut startup'a kayıtlı |
| `Ollama__Endpoint`, `Ollama__Model`, `Ollama__EmbeddingModel` | Semantic Kernel konfigürasyonu |
| `ASPNETCORE_ENVIRONMENT` | Ortam seçimi |
| `ASPNETCORE_URLS` | Profil dışında host dinleme adresi; launch profile ile çakışmayı önleyin |

Frontend statik `wwwroot/appsettings.json` içindeki `BackendUrl` ve `Oidc` ayarları ayrı yapılandırılır. Tarayıcıya servis edilen dosyaya client secret/API key koymayın. Backend `DefaultClientId/DefaultClientSecret` alanlarının bulunması, aktif JWT akışının bunları audience olarak doğruladığı anlamına gelmez.

## Sağlık ve doğrulama

HTTPS profilinde:

```bash
curl https://localhost:5002/health/live
curl https://localhost:5002/health/ready
curl https://localhost:5002/openapi/v1.json
```

OpenAPI ve Scalar (`/scalar/v1`) yalnızca Development ortamında eşlenir. Liveness süreç canlılığını, readiness PostgreSQL bağlantısını ve Hangfire storage nesnesinin varlığını kontrol eder. Worker'ın gerçekten iş işlediği, migration güncelliği, RabbitMQ ve AI sağlığı bu yanıtla kanıtlanmaz.

Depoda otomatik test projesi bulunmuyor; `dotnet test` sonucu tek başına test kapsamı kanıtı olamaz. Test projesi eklendikten sonra CI ile restore/build/test ve gerçek PostgreSQL entegrasyon senaryoları çalıştırılmalıdır.

## Production için tamamlanması gerekenler

- [ ] JWT issuer/audience, Hangfire yetkilendirmesi, anonim yönetim yolları ve tenant/workspace izolasyonu düzeltildi.
- [ ] Published API scope kontrolü, credential redaksiyonu ve script güven sınırı tamamlandı.
- [ ] Rate limit'ler endpoint'lere bağlandı; CORS gerçek frontend origin'iyle yapılandırıldı.
- [ ] Migration başarısızlığında trafik engelleniyor; yedek ve geri yükleme provası yapıldı.
- [ ] Secrets, encryption key rotasyonu, gerçek Keycloak production ayarı ve TLS yapılandırıldı.
- [ ] Retry/idempotency, iptal/pause ve çok istemcili SSE testleri geçti.
- [ ] Reverse proxy SSE buffering/timeout ayarları doğrulandı; süreç içi state nedeniyle çok replica garantisi varsayılmadı.
- [ ] Uygulama için gerçek publish/hosting yöntemi tanımlandı; mevcut Compose dosyası production deployment olarak kullanılmadı.

Sorun giderirken endpoint yetkilerini kaldırmayın; token, policy, tenant ve logları inceleyin. Bu liste bir dağıtım yapıldığı veya kusurların giderildiği anlamına gelmez.
