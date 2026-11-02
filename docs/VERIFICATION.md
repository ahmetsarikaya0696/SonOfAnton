# SonOfAnton — Doğrulama kaydı

Tarih: 2 Kasım 2026. Kapsam: varsayılan, AI kapalı workflow çekirdeği.

| Kontrol | Sonuç | Kanıt / kapsam |
|---|---|---|
| Solution restore/build | Başarılı | .NET SDK 10.0.100; 0 hata, 5 mevcut uyarı |
| .NET testleri | 11 başarılı | 8 unit vakası ve 3 geniş entegrasyon senaryosu |
| JavaScript stream testleri | 2 başarılı | SSE frame ayrıştırma ve bağlantı davranışı |
| Temiz DB migration | Başarılı | PostgreSQL 17.11 + pgvector 0.8; eksik altı tablonun migration'ı dahil |
| EF model kontrolü | Başarılı | `No changes have been made to the model since the last migration.` |
| Servis başlangıcı | Başarılı | PostgreSQL, Keycloak 26.1.0, backend ve frontend birlikte başladı; readiness, frontend ve OIDC metadata erişildi |
| Tarayıcı uçtan uca kabul | Engellendi | Chromium başlamadan `socket() failed: Operation not permitted` hatası; giriş/editör akışı doğrulanmadı |
| GitHub Actions | Bu kayıtta doğrulanmadı | Workflow eklendi; yerel sonuçlar uzak CI sonucu değildir |

## Test kapsamı

- Gerçek imzalı JWT: yanlış issuer, audience ve süresi dolmuş token reddediliyor. Eşzamanlı ilk giriş tek kullanıcı/workspace oluşturuyor.
- Anonim erişim, sahte tenant header, başka tenant, aynı tenant içinde üye olunmayan workspace ve salt okunur rol negatif senaryoları.
- Workflow kaydetme, manual → set → debug yürütme, başlangıç verisinin korunması, Hangfire üzerinden tamamlama ve kalıcı log.
- Aynı execution'ın tekrar yürütülmesinin log/yan etki çoğaltmaması; tekrarlayan tetikleyicilerde yeni execution kimlikleri.
- Zamanlanmış işi iptal etme, terminal execution stream'i, iki log abonesinin aynı kalıcı logları alması, yabancı execution stream'inin reddi.
- Eşzamanlı aynı graph kaydında gereksiz revizyon oluşmaması; desteklenmeyen node'un reddi.
- API key rate limit ve credential şifreleme; bounded SSE yayın sözleşmesi.

## Ortam notu

Yerel sandbox'ta WebAssembly SDK görev host'u çalışamadığı için yalnız kurulu `Microsoft.NET.Sdk.WebAssembly.Pack/10.0.0` paketindeki Browser.targets dosyasının `TaskFactory="TaskHostFactory"` öznitelikleri kaldırıldı. Bu değişiklik repoya dahil değildir. Build sonucu bu workaround ile alındı; temiz GitHub runner build'i ayrıca kontrol edilmelidir. Beş uyarı mevcut NER değişkeni, üç CA2024 uyarısı ve MarkdownEditorPanel alanıyla ilgilidir.

Yerel PostgreSQL/Keycloak doğrudan süreç olarak çalıştırıldı; Docker Compose dağıtımı bu ortamda uçtan uca sınanmadı. CI PostgreSQL 16/pgvector kullanır.

## Açık kabul işleri

1. Normal Chromium ortamında Keycloak ile giriş yap; manuel tetikleyici → set → debug graph'ını kaydet, çalıştır ve UI/log durumunu kontrol et.
2. UI'da pause/step/resume ve execution değiştirirken eski SSE bağlantısının kapanmasını doğrula.
3. GitHub Actions build/test/model kontrolünün sonucunu incele.
4. Production öncesi gerçek TLS/issuer/secret yapılandırması, eski veriyle migration, yedek–restore ve tek instance sınırlarını doğrula.

Gerçek Ollama modelleri, dış sağlayıcılar, çok instance çalışması, crash sonrası replay ve kapatılan deneysel özellikler kabul edilmedi. Bu kayıt production-ready veya bütün eski özellikler çalışıyor iddiası değildir.
