# SonOfAnton — Kurulum

.NET 10 SDK ve Docker Compose gerekir. Yerel başlangıç:

```bash
docker compose up -d --wait
dotnet restore SonOfAnton.slnx
dotnet build SonOfAnton.slnx -m:1 --no-restore
dotnet dev-certs https --trust
dotnet run --project src/SonOfAnton.Backend --launch-profile https
# Ayrı terminal:
dotnet run --project src/SonOfAnton.Frontend --launch-profile https
```

| Adres | Servis |
|---|---|
| `https://localhost:5001` | Frontend |
| `https://localhost:5002` | Backend |
| `http://localhost:8080` | Keycloak |
| `localhost:5432` | PostgreSQL/pgvector |
| `http://localhost:5050` | Opsiyonel pgAdmin (`docker compose --profile tools up -d pgadmin`) |

Compose yalnız altyapıyı başlatır; backend/frontend logları kendi terminallerindedir. Portlar loopback'e bağlıdır. Keycloak `start-dev` ve örnek parolalar yerel geliştirme içindir.

## Giriş ve mevcut realm

Geliştirme hesabı `dev@sonofanton.local`; parolası `keycloak/sonofanton-realm.json` içindeki `users[].credentials[].value` alanındadır. Gerçek kullanıcı parolalarını repoya eklemeyin.

Fresh import frontend client için `sonofanton-backend` audience mapper içerir. **Mevcut realm import ile üzerine yazılmaz.** Keycloak client mapper ayarına `oidc-audience-mapper`, `included.client.audience=sonofanton-backend`, `access.token.claim=true` ekleyip yeniden giriş yapın. Ayrıca frontend client dedicated scope içine `Subject (sub)` (`oidc-sub-mapper`) ekleyip `Add to access token` ayarını açın. Özel realm scope listesi Keycloak 26 varsayılan `basic` scope'unu içermediğinden bu mapper açıkça tanımlanır. Mevcut volume/veri silinmez.

## Yapılandırma

| Ortam değişkeni | Sözleşme |
|---|---|
| `ConnectionStrings__DefaultConnection` | EF/Hangfire bağlantısı; Production'da zorunlu, Development örneği ayrı dosyada |
| `Authentication__DefaultAuthority` | Tek güvenilen issuer; Production metadata HTTPS |
| `Authentication__Audience` | Varsayılan `sonofanton-backend` |
| `Features__AI` | Varsayılan false; yalnız Ollama node'ları |
| `Ollama__Endpoint`, `Ollama__Model`, `Ollama__EmbeddingModel` | Operatörün hazırladığı model bağlantısı; otomatik indirme yok |
| `Encryption__Key` | Servis kullanılırsa base64 kodlu tam 32 byte; fallback yok |
| `Encryption__LegacyKey` | Eski CBC verinin kontrollü taşınması için isteğe bağlı 32 UTF-8 byte; yeni yazım AES-GCM |

Frontend `wwwroot/appsettings.json` içindeki `BackendUrl` ve `Oidc` alanları ayrı yapılandırılır. Bu dosya public'tir, secret konmaz. Yeni adreslerde frontend URL, backend CORS ve Keycloak redirect/web-origin ayarları birlikte değiştirilir. Varsayılan embedding 768 boyutlu `nomic-embed-text` içindir; farklı boyut açık hata verir. RAG/memory API'leri açılmaz.

## Veritabanı ve test

Backend migration hatasında başlatmayı durdurur. PostgreSQL pgvector extension'ı kurulu olmalıdır. Kurulumda yeni migration üretmeyin.

```bash
docker compose exec postgres createdb -U sonofanton sonofanton_test
export SONOFANTON_TEST_DB='Host=localhost;Database=sonofanton_test;Username=sonofanton;Password=sonofanton_dev_password'
dotnet test tests/SonOfAnton.Tests -m:1
node --test tests/frontend/*.test.mjs
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/SonOfAnton.Backend
```

Test DB'si gerçek kullanıcı verisi içermemelidir. Testler migration uygular ve veri yazar; bağlantı ayarı yoksa sessizce atlanmak yerine hata verir. JS testleri için Node 22+ gerekir, uygulamanın çalışması için gerekmez.

`/health/live` süreç, `/health/ready` PostgreSQL ve Hangfire storage durumunu bildirir; anonimdir. `/openapi/v1.json` ve `/scalar/v1` yalnız Development'da açıktır. AI/harici iş başarısını health cevabından çıkarmayın.

## Dağıtım sınırları

Çekirdek tek backend instance içindir. Pause/step süreç içindedir; restart'tan sonra checkpoint replay yoktur. Yarım kalmış işleri harici etkilerini incelemeden tekrar başlatmayın. Reverse proxy altında SSE buffering kapalı olmalıdır.

Production dağıtımı yapılmadı. Gerçek ortam TLS/issuer/secrets, yedek–restore, eski üretim verisiyle upgrade, saklama/redaksiyon politikası ve harici entegrasyon kabulü gerekir. Eski tenant'sız/sahipsiz kayıtlar otomatik herkese açılmaz; açık sahiplik taşıma planı gerekir. Kanıt: [VERIFICATION.md](VERIFICATION.md).

## Tarayıcı kabul testi

GitHub Actions `browser` işi ayrı temiz runner'da Compose PostgreSQL/Keycloak, Release backend/frontend ve Chromium başlatır. `tests/browser/core.mjs` gerçek OIDC girişi, editörde kayıt, ardışık çalıştırma ve adım kontrolünü sınar. Ekran görüntüleri ve servis logları `browser-acceptance` artifact'inde tutulur. Playwright yalnız test aracıdır; uygulama runtime bağımlılığı değildir.

Yerelde yalnız izole geliştirme veritabanı/realm ile çalıştırın; test workflow oluşturur. .NET Release build sonrası Node 22+, Docker ve Playwright Chromium gerekir:

```bash
npm install --no-save --package-lock=false playwright@1.56.1
npx playwright install --with-deps chromium
export RUNNER_TEMP="$(mktemp -d)"
bash tests/browser/run.sh
```

Test script'i standart geliştirme portlarını kullanır, frontend/backend süreçlerini ve Compose servislerini sonunda durdurur. Aktif geliştirme oturumuyla aynı anda çalıştırmayın.
