# SonOfAnton — Veritabanı

Kaynaklar: [DbContext](../src/SonOfAnton.Backend/Data/SonOfAntonDbContext.cs), [migration geçmişi](../src/SonOfAnton.Backend/Migrations), [entity'ler](../src/SonOfAnton.Shared/Entities). PostgreSQL ve pgvector gerekir; Hangfire kendi şemasını kullanır.

## Migration düzeltmesi

`20261102090720_SyncCoreReadinessModel` önceki snapshot'ta eksik altı tabloyu ekler: `censorship_audit_logs`, `censorship_rules`, `loop_execution_states`, `planning_documents`, `memory_collections`, `memory_entries`; index/FK'leriyle birlikte. `Up` mevcut tablo/kolon/veri silmez. `Down` yeni tablolardaki veriyi siler; üretimde otomatik geri alma yöntemi değildir.

Tabloların oluşturulması deneysel API'lerin açıldığı anlamına gelmez. Model/snapshot tutarlı tutulur, controller listesi ayrıca sınırlandırılır. Backend `MigrateAsync` başarısızsa kapanır.

## Veri sözleşmeleri

- Workflow tenant/workspace/oluşturan kullanıcıya bağlıdır. HTTP ve job erişimi kapsamlanır.
- Execution ID tekildir; koşullu `Queued → Running` geçişi tekrar teslimi engeller. Harici sistemlerde exactly-once garantisi yoktur.
- Log/snapshot/revizyon erişimi ana workflow/execution kapsamından gelir.
- Workflow ve graph revizyonu aynı transaction'da; eşzamanlı güncelleme workflow kilidiyle korunur.
- Vector-store 768, memory 384 boyutludur. Model değişimi otomatik backfill değildir; bu API'ler kapalıdır.
- Credential yeni yazımları AES-GCM `v1:` biçimindedir. Eski ciphertext için explicit legacy key gerekir; yanlış anahtar sessizce boş veri üretmez.

Tenant/kullanıcı alanları boş eski kayıtlar görünmeyebilir. Kullanıcı verisini silmeden, onaylı sahiplik eşlemesiyle taşımak gerekir. Tek issuer varsayımı vardır; `ExternalIdentityId` tek başına unique olduğu için çok issuer desteği ayrıca tasarlanmalıdır.

## Komutlar

```bash
dotnet tool restore
dotnet ef migrations list --project src/SonOfAnton.Backend
dotnet ef migrations has-pending-model-changes --project src/SonOfAnton.Backend
dotnet ef migrations script --idempotent --project src/SonOfAnton.Backend
```

Design-time factory `ConnectionStrings__DefaultConnection` kullanır ve yerel fallback içerir; runtime Production bağlantısı zorunludur. Yeni model değişikliğinde migration üretip `Up/Down` etkilerini inceleyin. Uygulanmış geçmişi silmeyin. `init-scripts` yalnız yeni Compose volume'de çalışır.

Temiz DB migration ve uygulama testleri [VERIFICATION.md](VERIFICATION.md) içinde. Üretim yedeğinden upgrade/restore ve anahtar rotasyonu bu ortamda denenmedi.
