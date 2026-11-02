# SonOfAnton — Veritabanı ve migration

Kaynak incelemesi: `c91a288`, 2 Kasım 2026. Canlı veritabanı incelenmedi ve migration'lar bu ortamda çalıştırılmadı.

## Kaynakların önceliği

- [SonOfAntonDbContext](../src/SonOfAnton.Backend/Data/SonOfAntonDbContext.cs): güncel EF mapping.
- [Model snapshot](../src/SonOfAnton.Backend/Migrations/SonOfAntonDbContextModelSnapshot.cs): son üretilmiş migration modeli.
- [Migrations](../src/SonOfAnton.Backend/Migrations): uygulanacak şema geçmişi.
- [Entities](../src/SonOfAnton.Shared/Entities): alanlar ve ilişkiler.

Model, snapshot ve kurulu DB'nin aynı olduğu varsayılmamalıdır. Mevcut son migration dosyası `20260228215928_AddNotificationsAndQuotas.cs` olarak görünür. Dosya varlığı migration zincirinin temiz DB'de başarıyla uygulandığını kanıtlamaz.

## Şema grupları

| Alan | Örnek tablolar / amaç |
|---|---|
| Workflow | `workflows`, `workflow_revisions`, `folders` |
| Çalıştırma | `executions`, `execution_logs`, `execution_snapshots`, `execution_checkpoints` |
| Kimlik | `tenants`, `users`, `tenant_roles`, `user_tenant_roles` |
| Erişim / entegrasyon | `workspaces`, `workspace_members`, `api_keys`, `workflow_api_endpoints`, `credentials`, `linked_accounts` |
| AI / arama | `vector_store`, `memory_collections`, `memory_entries`, `chat_history` |
| Ek kapsam | Chatbot, marketplace/custom node, data store/table/form, planlama, censorship, bildirim ve kota entity'leri |

Tam kolon/constraint listesi mapping ve migration dosyalarından okunmalıdır; burada ikinci bir elle kopyalanmış şema tutulmaz.

## Kritik veri sözleşmeleri

| Konu | Mevcut durum | Gerekli iş |
|---|---|---|
| Tenant izolasyonu | Tenant FK/index var; DbContext global query filter içermiyor; workflow sorguları kapsamlanmıyor | Kullanıcı üyeliği ve tenant/workspace kapsamını okuma/yazma/job yollarında uygula |
| Vector store | `embedding` tipi `vector(768)` | 768 boyutlu seçili modelle yaz/ara |
| Memory entries | `embedding` tipi `vector(384)` | 384 boyutlu seçili modelle yaz/ara; 768 ile karıştırma |
| Model değiştirme | Model adı/boyutu değişikliği veriyi otomatik dönüştürmez | Model sürümü ve yeniden embedding/backfill planı |
| Dış kullanıcı kimliği | `ExternalIdentityId` tek başına unique | Çok issuer ihtiyacı varsa issuer + subject + tenant kimlik tasarımını değerlendir |
| Tenant OIDC secret | String kolon mapping'i; bu alanda şifreleme garantisi gösterilmiyor | Secret saklama/rotasyonunu uygulamada doğrula |
| Execution retry | Aynı execution ID tekrar `Add` ediliyor | Tekil kayıt ve idempotent durum geçişi |
| Graph revizyonu | Workflow ve revizyon ayrı SaveChanges çağrıları; sürüm max+1 ile üretiliyor | Atomiklik ve eşzamanlı güncelleme testi |

Genel credential şifreleme servisinde sabit fallback anahtar ve bütünlük doğrulama eksikleri vardır. Bir kolonun “secret” veya “encrypted” diye adlandırılması güvenlik kanıtı değildir. [İnceleme](REVIEW.md).

## Mevcut migration'ları uygulama

Depo kökünde, .NET 10 SDK ve proje EF Core sürümüyle uyumlu `dotnet-ef` kuruluyken:

```bash
dotnet ef migrations list --project src/SonOfAnton.Backend --startup-project src/SonOfAnton.Backend
dotnet ef database update --project src/SonOfAnton.Backend --startup-project src/SonOfAnton.Backend
```

`dotnet-ef` yoksa araç sürümünü projedeki `Microsoft.EntityFrameworkCore.Design` sürümüyle eşleştirerek kurun. Mevcut projede bu paket 10.0.3'tür:

```bash
dotnet tool install --global dotnet-ef --version 10.0.3
```

Kurulum için tekrar `AddIdentityEntities` migration'ı oluşturmayın; kimlik migration geçmişi zaten depodadır. Backend startup da `MigrateAsync` çağırır, ancak hatayı loglayıp devam eder. Deployment başarılı sayılmadan migration sonucu ve şema kontrol edilmelidir.

## Yeni model değişikliği

1. Entity ve `SonOfAntonDbContext` mapping'ini değiştir.
2. Anlamlı adla migration üret.
3. `Up`/`Down`, index, FK, veri kaybı ve veri taşıma gereksinimlerini incele.
4. Temiz DB ve önceki sürüm DB'si üzerinde upgrade testi yap.
5. Gerekirse geri dönüş için yedekten restore veya ileri düzeltme migration'ı hazırla.

```bash
dotnet ef migrations add DescribeActualChange --project src/SonOfAnton.Backend --startup-project src/SonOfAnton.Backend
dotnet ef migrations script --idempotent --project src/SonOfAnton.Backend --startup-project src/SonOfAnton.Backend
```

Üretimde uygulanmış migration'ı silerek geçmişi değiştirmeyin. `Down` veri kaybını otomatik geri getirmez. Bu belge güncellemesi şema değiştirmez veya yeni migration oluşturmaz.

## Konfigürasyon ve işletim

Bağlantı `ConnectionStrings:DefaultConnection` üzerinden okunur; ortam değişkeni `ConnectionStrings__DefaultConnection` ile verilebilir. Gerçek parolaları repoya koymayın. Geliştirme User Secrets kullanılacaksa proje UserSecretsId yapılandırması da bulunmalıdır.

`init-scripts` PostgreSQL Docker volume ilk oluşturulduğunda çalışır; mevcut volume üzerinde her başlangıçta yeniden uygulanmaz. Başlangıç verisi ile EF migration geçmişini karıştırmayın. Yedekleme/restore, log-history saklama süresi, şema upgrade'i ve encryption key yedekleme/rotasyonu yayın öncesi doğrulanmalıdır.
