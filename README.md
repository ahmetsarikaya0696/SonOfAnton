# SonOfAnton AI Workflow Platform

SonOfAnton, Blazor WebAssembly üzerinde görsel workflow oluşturmayı ve ASP.NET Core backend üzerinde çalıştırmayı amaçlayan .NET 10 uygulamasıdır. Workflow yürütme, Hangfire ve PostgreSQL kullanır; AI özellikleri Ollama/Semantic Kernel ve embedding bileşenleri etrafında geliştirilir.

> **Durum: geliştirme/prototip.** 2 Kasım 2026 incelemesi, kimlik doğrulama, tenant izolasyonu ve yürütme güvenilirliği açısından yayın engelleri buldu. Özelliklerin kodda veya menüde bulunması, production'a hazır oldukları anlamına gelmez. [Bulgular ve öncelikli işler](docs/REVIEW.md).

## Başlangıç

Gereksinimler: .NET **10** SDK, Docker Compose; AI senaryoları için yerel Ollama ve uygun modeller. Compose yalnızca PostgreSQL/pgvector, pgAdmin, Keycloak ve RabbitMQ'yu başlatır. Backend/frontend konteynerleri içermez.

```bash
git clone https://github.com/ahmetsarikaya0696/SonOfAnton.git
cd SonOfAnton
docker compose up -d
dotnet restore SonOfAnton.slnx
dotnet build SonOfAnton.slnx --no-restore
dotnet dev-certs https --trust
dotnet run --project src/SonOfAnton.Backend --launch-profile https
```

Ayrı terminalde:

```bash
dotnet run --project src/SonOfAnton.Frontend --launch-profile https
```

Frontend: `https://localhost:5001`; backend: `https://localhost:5002`. HTTPS profilleri açıkça seçilmelidir. Ayrıntılar: [çalıştırma kılavuzu](docs/DEPLOYMENT.md).

Backend başlangıçta migration uygular; hata durumunda loglayıp devam eder. ONNX model indirme ve Ollama startup servisleri kayıtlıdır; internet/model gereksinimleri ve embedding DI eksikleri nedeniyle AI hazır kabul edilmemelidir. Geliştirme varsayılan parolalarını üretimde kullanmayın.

## Kaynak haritası

| Proje / dizin | Sorumluluk |
|---|---|
| `src/SonOfAnton.Backend` | API, EF Core, workflow runner, Hangfire, AI servisleri |
| `src/SonOfAnton.Frontend` | Blazor WebAssembly editor ve API istemcileri |
| `src/SonOfAnton.Shared` | Ortak DTO, model, entity ve graph tipleri |
| `src/SonOfAnton.NodeSdk` | Özel node SDK'sı; solution içinde |
| `templates/SonOfAnton.NodeTemplate` | Özel node proje şablonu |
| `src/openapi_test_app` | OpenAPI örnek uygulaması; otomatik test projesi değil |
| `keycloak/sonofanton-realm.json` | Geliştirme realm import dosyası |

## Kapsam ve sınırlar

Görsel editor, kalıcı workflow CRUD, execution/log takibi ve node handler altyapısı mevcut. RAG/embedding bağlantısı ve bazı AI handler kayıtları eksik; anonim eski workflow API'si, chatbot yönetimi ve eksik tenant filtreleri düzeltilmeli. Marketplace, A2A, SCIM, özel script, Ralph loop ve veri tabloları/formları genişletilmiş kapsamdır; çekirdek doğrulanana kadar geliştirmelerinin ertelenmesi önerilir.

Depoda otomatik test projesi veya CI workflow'u yok. Bu incelemede SDK bulunmadığı için build/test çalıştırılmadı. Çalışan ürün iddiası için [kabul kriterleri](docs/REVIEW.md) tamamlanmalı.

## Belgeler

- [İnceleme, eksikler ve sadeleştirme planı](docs/REVIEW.md)
- [Mevcut mimari](docs/ARCHITECTURE.md)
- [API sözleşmesi ve sınırlamalar](docs/API.md)
- [Veritabanı ve migration](docs/DATABASE.md)
- [Kurulum ve dağıtım](docs/DEPLOYMENT.md)
- [Kullanım rehberi](docs/KULLANIM%20REHBER%C4%B0.md)

## Katkı ve lisans

Değişiklikleri küçük, amaç odaklı PR'larla gönderin. Davranış değişikliklerine uygun test ve belge güncellemesi ekleyin. Öncelikli çalışma sırası inceleme raporundadır.

İncelenen commit'te `LICENSE` dosyası yok. Önceki README'nin MIT lisansı iddiası doğrulanamadığından kaldırılmıştır; lisansın depo sahibi tarafından açıkça belirlenmesi gerekir.
