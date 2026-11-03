# SonOfAnton

.NET 10, Blazor WebAssembly, ASP.NET Core, PostgreSQL/pgvector ve Hangfire ile görsel workflow uygulaması.

**Çekirdek kapsam:** workspace içinde workflow oluşturma, graph kaydetme, çalıştırma/zamanlama, execution ve log takibi. [Görev planı](docs/IMPLEMENTATION_PLAN.md) tamamlanan işleri, [doğrulama kaydı](docs/VERIFICATION.md) test kanıtını ve sınırları gösterir. İlk statik inceleme [REVIEW.md](docs/REVIEW.md) içinde tarihsel olarak korunur.

## Çalıştırma

.NET 10 SDK ve Docker Compose gerekir. Repo kökünde:

```bash
docker compose up -d --wait
dotnet restore SonOfAnton.slnx
dotnet build SonOfAnton.slnx -m:1 --no-restore
dotnet dev-certs https --trust
dotnet run --project src/SonOfAnton.Backend --launch-profile https
```

Ayrı terminalde:

```bash
dotnet run --project src/SonOfAnton.Frontend --launch-profile https
```

Editör `https://localhost:5001`, backend `https://localhost:5002`. Geliştirme hesabı ve mevcut Keycloak realm güncellemesi: [kurulum](docs/DEPLOYMENT.md).

Compose PostgreSQL/pgvector ve Keycloak başlatır. pgAdmin `tools` profiline taşındı; RabbitMQ kaldırıldı. Backend/frontend yerel .NET süreçleridir. Migration başarısızsa backend başlamaz; başlangıçta model indirmez veya başka servis başlatmaz.

## Doğrulama

```bash
# Yalnız ayrılmış test DB'si kullanın; önce PostgreSQL içinde oluşturun.
export SONOFANTON_TEST_DB='Host=localhost;Database=sonofanton_test;Username=sonofanton;Password=sonofanton_dev_password'
dotnet test tests/SonOfAnton.Tests -m:1
node --test tests/frontend/*.test.mjs
```

CI build, PostgreSQL entegrasyon testleri, JS stream testleri ve model/migration tutarlılığını kontrol eder. Node yalnız frontend JS testleri içindir; uygulamanın runtime bağımlılığı değildir.

## Kapsam

Node paleti sunucuda kayıtlı handler'lardan gelir. Manuel/zamanlı tetikleyici, dönüşüm, koşul, gecikme ve debug/end çekirdektedir. Ollama node'ları `Features:AI=true` ile isteğe bağlıdır; gerçek model/donanım kabulü ayrıdır.

Marketplace, chatbot, public webhook/published API, SCIM, A2A, Ralph loop, RAG/vector-store, veri tabloları/formları, credential yönetimi, HTTP/özel kod/plugin ve subflow yürütmesi kapalıdır. Kaynakları ve veri/migration geçmişi korunur. Snapshot bilgi amaçlı alınabilir; checkpoint replay henüz desteklenmez. Çekirdeğin doğrulanması bütün eski özelliklerin veya production dağıtımının tamamlandığı anlamına gelmez.

## Belgeler

- [Görev ve alt görev planı](docs/IMPLEMENTATION_PLAN.md), [geliştirici talimatları](AGENTS.md)
- [Mimari](docs/ARCHITECTURE.md), [API](docs/API.md), [veritabanı](docs/DATABASE.md)
- [Kurulum](docs/DEPLOYMENT.md), [kullanım rehberi](docs/KULLANIM%20REHBER%C4%B0.md)
- [İlk inceleme](docs/REVIEW.md), [test kanıtı](docs/VERIFICATION.md)

Depoda `LICENSE` dosyası yoktur; lisans seçimi depo sahibine aittir.
