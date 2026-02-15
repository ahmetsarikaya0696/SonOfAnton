#  SonOfAnton — Veritabanı ve Konfigürasyon Kılavuzu

> **Bu belge kime hitap ediyor?**  
> Veritabanı şemasını değiştirmek, migration oluşturmak veya
> konfigürasyon ayarlarını anlamak isteyen herkes.

---

##  İçindekiler

1. [Migration Nedir?](#1-migration-nedir)
2. [Migration İş Akışı](#2-migration-iş-akışı)
3. [Adım Adım Migration Oluşturma](#3-adım-adım-migration-oluşturma)
4. [Migration Geri Alma](#4-migration-geri-alma)
5. [Konfigürasyon Yapısı](#5-konfigürasyon-yapısı)
6. [Ortam Değişkenleri](#6-ortam-değişkenleri)
7. [Konfigürasyon Versiyonlama](#7-konfigürasyon-versiyonlama)
8. [Sık Karşılaşılan Sorunlar](#8-sık-karşılaşılan-sorunlar)

---

## 1. Migration Nedir?

**Migration**, veritabanı şemasını kod üzerinden yönetmenin yoludur.

Normal hayatta:
- SQL dosyasını elle yazarsın
- Sunucuya bağlanıp çalıştırırsın
- Hangi SQL'in hangi sunucuda çalıştığını takip edemezsin

Migration ile:
- C# kodu yazarsın (veya otomatik üretilir)
- `dotnet ef database update` çalıştırırsın
- EF Core hangi migration'ların uygulandığını otomatik takip eder

**Mevcut Migration'lar:**

```
migrations/
|-- 20260124055211_InitialCreate.cs          ? İlk tablo oluşturma
|-- 20260124095427_UpdateVectorDimension768.cs ? Vector boyutunu 768'e güncelleme
|-- SonOfAntonDbContextModelSnapshot.cs       ? Güncel şema snapshot'ı
```

---

## 2. Migration İş Akışı

```
1. Entity Sınıfını Değiştir
   (Entities/WorkflowEntities.cs veya yeni entity)
        ?
        ?
2. DbContext'e Ekle (gerekiyorsa)
   (Data/PsyAIDbContext.cs ? DbSet + OnModelCreating)
        ?
        ?
3. Migration Oluştur
   dotnet ef migrations add MigrationAdi
        ?
        ?
4. Oluşan Kodu İncele
   (Migrations/ klasörüne bak, Up/Down metodlarını kontrol et)
        ?
        ?
5. Veritabanını Güncelle
   dotnet ef database update
        ?
        ?
6. Test Et
   (Uygulama çalışıyor mu? Yeni alan doğru kaydediliyor mu?)
```

---

## 3. Adım Adım Migration Oluşturma

### Senaryo: "Workflow'a `Version` alanı eklemek istiyorum"

**Adım 1:** Entity sınıfını güncelle

```csharp
// Shared/Entities/WorkflowEntities.cs
public class WorkflowEntity
{
    // ... mevcut alanlar ...
    
    /// <summary>
    /// Workflow şema versiyonu (breaking change takibi için)
    /// </summary>
    public int Version { get; set; } = 1;  // ? Yeni alan
}
```

**Adım 2:** DbContext'te mapping ekle (gerekiyorsa)

```csharp
// Backend/Data/PsyAIDbContext.cs ? OnModelCreating
modelBuilder.Entity<WorkflowEntity>(entity =>
{
    // ... mevcut mapping'ler ...
    entity.Property(e => e.Version)
        .HasColumnName("version")
        .HasDefaultValue(1);  // ? Yeni mapping
});
```

**Adım 3:** Terminal'de migration oluştur

```bash
# Backend proje klasörüne git
cd src/SonOfAnton.Backend

# Migration oluştur (isim anlamlı olmalı!)
dotnet ef migrations add AddWorkflowVersion

# ? İyi isimler:    AddWorkflowVersion, CreateWebhookTable, UpdateVectorDimension
# ? Kötü isimler:   Migration1, Fix, Update, Deneme
```

**Adım 4:** Oluşan dosyayı kontrol et

```csharp
// Otomatik oluşan Up() metodu şöyle olmalı:
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "version",
        table: "workflows",
        type: "integer",
        nullable: false,
        defaultValue: 1);
}

// Down() metodu geri almayı sağlar:
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "version",
        table: "workflows");
}
```

**Adım 5:** Veritabanını güncelle

```bash
# Geliştirme ortamı
dotnet ef database update

# Belirli bir migration'a güncelle
dotnet ef database update AddWorkflowVersion
```

---

## 4. Migration Geri Alma

**Son migration'ı geri al:**
```bash
dotnet ef database update PreviousMigrationName
```

**Migration dosyasını sil (henüz uygulanmamışsa):**
```bash
dotnet ef migrations remove
```

**?? DİKKAT:** Production'da uygulanan migration ASLA silinmez!
Bunun yerine yeni bir migration ile değişikliği geri alırsın.

---

## 5. Konfigürasyon Yapısı

Uygulama ayarları `appsettings.json` ve ortam değişkenlerinden okunur.

### appsettings.json Yapısı

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=sonofanton_db;Username=sonofanton;Password=sonofanton_dev_password"
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Model": "qwen2.5-coder:14b",
    "EmbeddingModel": "nomic-embed-text"
  },
  "Authentication": {
    "DefaultAuthority": "https://login.microsoftonline.com/common/v2.0",
    "DefaultClientId": "<your-client-id>",
    "DefaultClientSecret": "<your-client-secret>",
    "AllowExternalBearer": true
  },
  "MultiTenancy": {
    "ResolveBy": "Header",
    "DefaultTenantId": "default"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Development ortamı override'ları (appsettings.Development.json):**

```json
{
  "Authentication": {
    "DefaultAuthority": "http://localhost:8080/realms/sonofanton",
    "DefaultClientId": "sonofanton-backend",
    "DefaultClientSecret": "backend-dev-secret"
  }
}
```

**Frontend OIDC yapılandırması (wwwroot/appsettings.json):**

```json
{
  "BackendUrl": "https://localhost:5002",
  "Oidc": {
    "Authority": "http://localhost:8080/realms/sonofanton",
    "ClientId": "sonofanton-frontend",
    "ResponseType": "code",
    "DefaultScopes": ["openid", "profile", "email"]
  }
}
```

### Hangi ayar ne işe yarar?

| Ayar | Varsayılan | Açıklama |
|------|-----------|----------|
| `ConnectionStrings:DefaultConnection` | localhost | PostgreSQL bağlantı dizgisi |
| `Ollama:Endpoint` | `http://localhost:11434` | Ollama API adresi |
| `Ollama:Model` | `qwen2.5-coder:14b` | LLM model adı |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | Embedding model adı |
| `Authentication:DefaultAuthority` | MS login URL | OIDC Authority (Keycloak realm URL) |
| `Authentication:DefaultClientId` | - | OIDC Client ID |
| `Authentication:DefaultClientSecret` | - | OIDC Client Secret |
| `MultiTenancy:ResolveBy` | `Header` | Tenant çözümleme stratejisi |
| `MultiTenancy:DefaultTenantId` | `default` | Varsayılan tenant ID |

### Ortam bazlı override

```
appsettings.json                ? Tüm ortamlar için temel
appsettings.Development.json    ? Sadece geliştirme ortamı
appsettings.Production.json     ? Sadece production ortamı
```

**Öncelik sırası (sonraki öncekini ezer):**
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Ortam değişkenleri
4. Komut satırı argümanları

---

## 6. Ortam Değişkenleri

Ortam değişkenleri ile appsettings.json'daki her ayarı override edebilirsin.
`__` (çift alt çizgi) ile iç içe yapıları belirtirsin.

```bash
# Linux/macOS
export ConnectionStrings__DefaultConnection="Host=prod-server;Database=sonofanton;..."
export Ollama__Endpoint="http://gpu-server:11434"

# Windows PowerShell
$env:ConnectionStrings__DefaultConnection = "Host=prod-server;..."
$env:Ollama__Endpoint = "http://gpu-server:11434"

# Docker
docker run -e ConnectionStrings__DefaultConnection="Host=db;..." myapp
```

**?? Gizli bilgileri (şifre, API key vb.) ASLA appsettings.json'a yazma!**
Ortam değişkeni veya User Secrets kullan:

```bash
# Development ortamında User Secrets kullan
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=..."
```

---

## 7. Konfigürasyon Versiyonlama

Konfigürasyon değişikliklerini takip etmek için aşağıdaki kuralları uygula:

### Kurallar

1. **appsettings.json değişiklikleri commit'e dahil olmalı**
   - Yeni bir ayar eklediğinde varsayılan değerle birlikte ekle
   - PR açıklamasında "Yeni konfigürasyon: X eklendi" yaz

2. **Breaking change'lerde versiyon numarasını artır**
   ```json
   {
     "AppVersion": "2.0.0",
     "ConfigVersion": 3
   }
   ```

3. **Eski ayarları hemen silme, deprecated işaretle**
   ```json
   {
     "OldSetting_DEPRECATED": "Bunu kullanma, NewSetting'e geç",
     "NewSetting": "aktif değer"
   }
   ```

4. **Her ortam için örnek konfigürasyon dosyası tut**
   ```
   appsettings.Example.json     ? Git'te (şifresiz)
   appsettings.Production.json  ? .gitignore'da (şifreli)
   ```

---

## 8. Sık Karşılaşılan Sorunlar

### "Migration oluşturulamıyor"

```bash
# EF Core tools yüklü mü?
dotnet tool install --global dotnet-ef

# Doğru klasörde misin?
cd src/SonOfAnton.Backend

# Design-time package var mı? (csproj'da kontrol et)
# Microsoft.EntityFrameworkCore.Design olmalı
```

### "Pending migration var ama update başarısız"

```bash
# Hangi migration'lar bekliyor?
dotnet ef migrations list

# Veritabanı bağlantısını kontrol et
# PostgreSQL çalışıyor mu?
docker ps  # veya pg_isready
```

### "Connection string bulunamıyor"

Program.cs'deki varsayılan değeri kontrol et:
```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=sonofanton_db;Username=sonofanton;Password=sonofanton_dev_password";
```

Varsayılan değer varsa appsettings.json olmadan da çalışır.
Ama production'da mutlaka ortam değişkeni kullan!

---

## 9. Yeni Tablolar (v2)

Asagidaki tablolar ikinci fazda eklenmistir:

### folders

Hiyerarsik workflow organizasyonu (self-referencing).

| Kolon | Tip | Aciklama |
|-------|-----|----------|
| id | UUID (PK) | Klasor ID |
| name | VARCHAR(255) | Klasor adi |
| icon | VARCHAR(50) | Ikon adi |
| color | VARCHAR(20) | Renk kodu |
| sort_order | INT | Siralama |
| parent_folder_id | UUID (FK, nullable) | Ust klasor (self-reference) |
| created_at | TIMESTAMP | Olusturulma tarihi |

**Indeksler:** parent_folder_id

### execution_snapshots

Execution durumunun belirli bir andaki kopyasi.

| Kolon | Tip | Aciklama |
|-------|-----|----------|
| id | UUID (PK) | Snapshot ID |
| execution_id | UUID (FK) | Iliskili execution |
| label | VARCHAR(255) | Etiket (opsiyonel) |
| node_states | JSONB | Node durumlari |
| memory_snapshot | JSONB | Memory kopyasi |
| at_node_id | UUID | Hangi node'daydi |
| is_automatic | BOOLEAN | Otomatik mi alinmis |
| created_at | TIMESTAMP | Olusturulma tarihi |

**Indeksler:** execution_id

### chat_history

Workflow chat gecmisi.

| Kolon | Tip | Aciklama |
|-------|-----|----------|
| id | UUID (PK) | Kayit ID |
| workflow_id | UUID (FK, nullable) | Iliskili workflow |
| role | VARCHAR(20) | user / assistant |
| content | TEXT | Mesaj icerigi |
| artifacts | JSONB | Ek veriler (graph, yaml vb.) |
| timestamp | TIMESTAMP | Zaman damgasi |

**Indeksler:** workflow_id

### workflows tablosuna eklenen yeni kolonlar

| Kolon | Tip | Aciklama |
|-------|-----|----------|
| run_count | INT | Toplam calistirma sayisi |
| success_count | INT | Basarili calistirma sayisi |
| failure_count | INT | Basarisiz calistirma sayisi |
| avg_duration_ms | DOUBLE | Ortalama sure (ms) |
| last_run_at | TIMESTAMP | Son calistirma tarihi |
| folder_id | UUID (FK, nullable) | Ait oldugu klasor |

---

> **Altın Kural:** Migration oluşturduktan sonra mutlaka Up() ve Down()
> metodlarını gözden geçir. Otomatik üretilen kod bazen gereksiz veya
> yanlış olabilir. `Down()` metodunun çalıştığından emin ol — bir gün
> geri almak zorunda kalabilirsin!


## 10. Kimlik ve Multi-Tenancy Tabloları (v3)

Aşağıdaki tablolar kimlik, yetkilendirme ve çok kiracılık (multi-tenancy) altyapısı için eklenmiştir.

### tenants

Kurum (tenant) bilgilerini ve opsiyonel OIDC konfigürasyonunu tutar.

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| id | VARCHAR(100) (PK) | Tenant ID ("kurumA", "default") |
| name | VARCHAR(255) | Kurum adı |
| oidc_authority | VARCHAR(500) | Tenant-özel OIDC Authority URL |
| oidc_client_id | VARCHAR(255) | OIDC Client ID |
| oidc_client_secret | VARCHAR(500) | OIDC Client Secret (şifreli) |
| is_active | BOOLEAN | Aktif mi |
| created_at | TIMESTAMP | Oluşturulma tarihi |

### users

Uygulama kullanıcıları  şifre tutmaz, dış kimlik sağlayıcısına bağlıdır.

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| id | UUID (PK) | Kullanıcı ID |
| external_identity_id | VARCHAR(255) | OIDC sub/oid claim değeri |
| email | VARCHAR(255) | E-posta |
| display_name | VARCHAR(255) | Görünen ad |
| tenant_id | VARCHAR(100) (FK) | Ait olduğu tenant |
| is_active | BOOLEAN | Aktif mi |
| created_at | TIMESTAMP | Oluşturulma tarihi |
| last_login_at | TIMESTAMP | Son giriş tarihi |

**İndeksler:** external_identity_id (UNIQUE), email, tenant_id

### tenant_roles

Kuruma özel roller  dış kimlik sağlayıcısındaki rolleri iç izinlere eşleştirir.

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| id | UUID (PK) | Rol ID |
| tenant_id | VARCHAR(100) (FK) | Ait olduğu tenant |
| external_role_name | VARCHAR(255) | Dış sistemdeki rol adı ("Manager", "Viewer") |
| mapped_permissions | TEXT[] | İç izin dizisi (["Workflows.Read", "Workflows.Execute"]) |
| created_at | TIMESTAMP | Oluşturulma tarihi |

**İndeksler:** tenant_id + external_role_name (UNIQUE)

### user_tenant_roles

Kullanıcı-Rol ilişkisi (many-to-many join tablosu).

| Kolon | Tip | Açıklama |
|-------|-----|----------|
| id | UUID (PK) | Kayıt ID |
| user_id | UUID (FK) | Kullanıcı |
| tenant_role_id | UUID (FK) | Tenant rolü |

**İndeksler:** user_id + tenant_role_id (UNIQUE)

### workflows ve folders tablolarına eklenen multi-tenant kolonlar

| Tablo | Kolon | Tip | Açıklama |
|-------|-------|-----|----------|
| workflows | tenant_id | VARCHAR(100) (FK, nullable) | Workflow'un ait olduğu tenant |
| workflows | created_by_user_id | UUID (FK, nullable) | Oluşturan kullanıcı |
| folders | tenant_id | VARCHAR(100) (FK, nullable) | Klasörün ait olduğu tenant |

### ER Diyagramı (Kimlik)

```
tenants 1* users
tenants 1* tenant_roles
users   ** tenant_roles  (via user_tenant_roles)
tenants 1* workflows     (tenant_id FK)
tenants 1* folders       (tenant_id FK)
users   1* workflows     (created_by_user_id FK)
```

### Migration Komutu

```bash
cd src/SonOfAnton.Backend
dotnet ef migrations add AddIdentityEntities
dotnet ef database update
```
