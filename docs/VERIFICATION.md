# SonOfAnton — Doğrulama kaydı

Güncelleme: 27 Kasım 2026. İlk yerel doğrulama: 2 Kasım 2026. Kapsam: varsayılan, AI kapalı workflow çekirdeği.

## 27 Kasım canlı QA regresyonları

[CI koşusu 45999315493](https://github.com/ahmetsarikaya0696/SonOfAnton/actions/runs/45999315493), `132788c` commit'inde temiz Release build, 12 .NET testi (atlanan yok), 2 JS testi, EF model kontrolü ve gerçek Keycloak/Chromium kabulünü geçti. Sonraki commit'lerin durumu [PR #5 kontrollerindedir](https://github.com/ahmetsarikaya0696/SonOfAnton/pull/5/checks).

| QA bulgusu | Tarayıcı / entegrasyon kanıtı |
|---|---|
| F1 Node ayarları | Fareyle node seçimi, ad ve JSON parametresi düzenleme, kaydedilen graph doğrulaması |
| F2 Yeniden açma | Kalıcı URL/reload, klasör ve workspace bağlantısı; boş editör ve yüklenen akış gereksiz kayıt üretmiyor |
| F3 Workspace hatası | Genel bilgi ve içerik sekmeleri açılıyor; Blazor render exception kontrolü |
| F4 Değişkenler | Ad/değer kaydı ve tam reload sonrası korunma |
| F5 YAML | Hatalı YAML graph'ı koruyor; geçerli YAML iki node ve bağlantıyı aktarıyor; başarısız API isteği yok |
| F6 Monitor | İki node'lu yürütmede 2/2 ve pozitif süre; PostgreSQL testinde tamamlanmadan sonra bağlanan SSE istemcisi üç node sonucunu alıyor |
| F7 Revizyon paneli | Panel viewport içinde; Diff düğmesi fareyle açılıyor |
| F8 Kullanıcı rozeti | API anahtarları sayfasından normal fare tıklamasıyla editöre dönüş |
| F9 Türkçe loglar | Manuel tetikleyici mesajında `başlatıldı`; replacement character yok |

`browser-acceptance` artifact'i beş yürütme, dokuz QA kontrolü ve ekran görüntüsünü içerir. Production hesabı/verisi kullanılmadı. Yerel 8 unit ve 2 JS testi de geçti; yerel WebAssembly task-host kısıtı devam ediyor, paket veya ürün koduna workaround uygulanmadı. Sonraki regresyonlar bağlantı kimliklerinin reload sonrası sabit kalmasını, son kaydın Diff sonucunu ve Çalıştır öncesi bekleyen düzenlemelerin kaydedilmesini de sınar.

## 6 Kasım çekirdek kabulü (tarihsel)

| Kontrol | Sonuç | Kanıt / kapsam |
|---|---|---|
| Solution restore/build | Başarılı | .NET SDK 10.0.100; 0 hata, 5 mevcut uyarı |
| .NET testleri | 12 başarılı | 8 unit vakası ve 4 geniş entegrasyon senaryosu; adım/devam/iptal dahil |
| JavaScript stream testleri | 2 başarılı | SSE frame ayrıştırma ve bağlantı davranışı |
| Temiz DB migration | Başarılı | PostgreSQL 17.11 + pgvector 0.8; eksik altı tablonun migration'ı dahil |
| EF model kontrolü | Başarılı | `No changes have been made to the model since the last migration.` |
| Servis başlangıcı | Başarılı | PostgreSQL, Keycloak 26.1.0, backend ve frontend birlikte başladı; readiness, frontend ve OIDC metadata erişildi |
| Tarayıcı uçtan uca kabul | Başarılı | Temiz GitHub runner: gerçek Keycloak girişi, paletten node ekleme, graph kaydı, iki normal execution, step ve normal devam; konsolda kalıcı log |
| GitHub Actions | Başarılı | Release build, 12 .NET + 2 JS testi, EF model kontrolü ve Compose/Chromium kabulü |

Kanıt: [başarılı CI koşusu](https://github.com/ahmetsarikaya0696/SonOfAnton/actions/runs/42173113502). `browser-acceptance` artifact'i dört execution sonucunu, ekran görüntüsünü ve servis loglarını içerir. Tarayıcı testi graph'ta node bulunduğunu, konsola log geldiğini ve API çağrılarında hata olmadığını doğrular.

## Test kapsamı

- Gerçek imzalı JWT: yanlış issuer, audience ve süresi dolmuş token reddediliyor. Eşzamanlı ilk giriş tek kullanıcı/workspace oluşturuyor.
- Anonim erişim, sahte tenant header, başka tenant, aynı tenant içinde üye olunmayan workspace ve salt okunur rol negatif senaryoları.
- Workflow kaydetme, manual → set → debug yürütme, başlangıç verisinin korunması, Hangfire üzerinden tamamlama ve kalıcı log.
- Aynı execution'ın tekrar yürütülmesinin log/yan etki çoğaltmaması; tekrarlayan tetikleyicilerde yeni execution kimlikleri.
- Zamanlanmış işi iptal etme, terminal execution stream'i, iki log abonesinin aynı kalıcı logları alması, yabancı execution stream'inin reddi.
- Eşzamanlı aynı graph kaydında gereksiz revizyon oluşmaması; desteklenmeyen node'un reddi.
- Adımlı yürütmede node sınırında `Paused`, tek adım sonrası yeniden bekleme, resume ile adım modundan çıkma ve duraklatılmış işi iptal etme.
- API key rate limit ve credential şifreleme; bounded SSE yayın sözleşmesi.

## Ortam notu

Yerel sandbox'ta WebAssembly SDK görev host'u çalışamadığı için yalnız kurulu `Microsoft.NET.Sdk.WebAssembly.Pack/10.0.0` paketindeki Browser.targets dosyasının `TaskFactory="TaskHostFactory"` öznitelikleri kaldırıldı. Bu değişiklik repoya dahil değildir. Build sonucu bu workaround ile alındı; 6 Kasım temiz GitHub runner build'i bu workaround olmadan da geçti. Beş uyarı mevcut NER değişkeni, üç CA2024 uyarısı ve MarkdownEditorPanel alanıyla ilgilidir.

Yerel PostgreSQL/Keycloak doğrudan süreç olarak çalıştırıldı; ilk yerel ortamda Docker Compose sınanmadı; 6 Kasım CI tarayıcı işi Compose PostgreSQL 16/Keycloak ile geçti. CI PostgreSQL 16/pgvector kullanır.

## Kalan sınırlar

- Tarayıcı kabulü masaüstü Chromium içindir. Manuel node yanında YAML ile oluşturulan iki node ve bağlantısı kapsanır. Portları sürükleyerek bağlama, normal koşum sırasında Duraklat/Devam, dar ekran ve diğer tarayıcılar ayrıca genişletilebilir.
- API entegrasyonu çok node'lu veri aktarımı ve step/resume/cancel davranışını kapsar; UI testi bunların tüm varyasyonlarını kapsamaz.
- Production öncesi gerçek TLS/issuer/secret yapılandırması, eski veriyle migration, yedek–restore ve tek instance sınırlarını doğrulayın.
- Gerçek Ollama modelleri, dış sağlayıcılar, çok instance çalışması, crash sonrası replay ve kapatılan deneysel özellikler kabul edilmedi.

Bu kayıt production-ready veya bütün eski özellikler çalışıyor iddiası değildir.
