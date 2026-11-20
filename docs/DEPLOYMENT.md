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

API ve Hangfire worker ayrı süreçlerde çalışabilir. Pause/step komutları PostgreSQL üzerinden paylaşılır; restart'tan sonra checkpoint replay yoktur. Üretim yapılandırması tek Hangfire daemon kullanır. Yarım kalmış işleri harici etkilerini incelemeden tekrar başlatmayın. Reverse proxy altında SSE buffering kapalı olmalıdır.

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

## Vercel + Neon hazırlığı (20 Kasım 2026)

**Durum: production release yapılmadı.** Canlı HTTPS OIDC issuer ve PKCE public client eksik. Repo içindeki `start-dev`, örnek hesaplar veya parolalar production'a taşınmaz. Keycloak'u serverless container'a doğrudan koymak güvenilir bir auth-session/cluster çözümü değildir. Mevcut canlı OIDC sunucusu sağlanmalı; başka kimlik sağlayıcısına geçiş ayrıca uygulanıp doğrulanmalıdır.

Hazırlanan kaynaklar:

- Vercel: `ahmetsarikaya0696-8994/sonofanton`, proje `prj_YYnJLmBhw3zaQWKFZtOjLJPBUvvL`.
- Neon: `super-meadow-44563899`, production branch `br-silent-pond-b9dlxhw4`, PostgreSQL 16, `aws-eu-central-1`, database `sonofanton`.
- Production secret'ları: `ConnectionStrings__DefaultConnection`, `Encryption__Key`, `PORT`. Değerleri repoya veya browser config'e yazılmaz.
- Neon suspend süresini değiştirme denemesi hesap politikası tarafından HTTP 412 ile reddedildi; plan yükseltilmedi.

`vercel.json` iki container service ve bir `fra1` daemon tanımlar. Services/container/daemons Vercel beta özellikleridir. `/api/*` ve `/health/*` API'ye, diğer yollar Blazor statik servis girişine gider. API image'ında Hangfire worker kapalıdır; daemon komutu aynı binary'de `Execution:WorkerEnabled=true` kullanır. `Hosting:PlatformTls=true` yalnız Vercel TLS ingress arkasında kullanılır; doğrudan HTTP yayın için değildir.

Tamamlanması gereken production değişkenleri:

| Değişken | Değer |
|---|---|
| `Authentication__DefaultAuthority` | Güvenilen HTTPS OIDC issuer |
| `Authentication__Audience` | Access token API audience; varsayılan `sonofanton-backend` |
| `PUBLIC_OIDC_AUTHORITY` | Aynı HTTPS issuer; browser tarafından görülebilir |
| `PUBLIC_OIDC_CLIENT_ID` | Authorization Code + PKCE public client; secret içermez |

Public client redirect adresi production origin + `/authentication/login-callback`, logout adresi production origin olmalıdır. Access token `sub`, API audience, `preferred_username`, `email` ve mevcut izin eşlemesine uygun `roles` claim'lerini içermelidir. Gizli client credential'ı frontend'e verilmez.

Frontend container başlarken yalnız public OIDC alanlarını `appsettings.json` içine yazar; issuer/client eksikse başlamayı reddeder. Backend adresi aynı origin'dir. Appsettings cache edilmez. Secret'lar yalnız production hedefindedir; preview'u production DB'ye bağlamayın. Hangfire/advisory lock uyumluluğu için Npgsql doğrudan Neon endpoint'i ve küçük bağlantı havuzu kullanır.

Dağıtım sırası: CI doğrulaması → production OIDC client ayarları → staging production build → health/OIDC/kayıt/çok node çalıştırma/adımlama/iptal kabulü → domain ataması. Migration additive'dir; mevcut veriler silinmez. API ve worker başlangıçta EF migration uygular. Sadece health=200 uygulama kabulü değildir. Canlı kaynaklarla kabul tamamlanmadan release başarılı sayılmaz.

SSE completion ve log replay DB üzerinden işler. Süreç içi node-progress event yayınları API/worker arasında taşınmaz; anlık node animasyonları için ayrıca kalıcı event akışı gerekir. Restart sonrası yarım kalan işlerin otomatik replay'i kapsam dışıdır.
