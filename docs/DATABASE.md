# ??? SonOfAnton — Veritabaný ve Konfigürasyon Kýlavuzu

> **Bu belge kime hitap ediyor?**  
> Veritabaný þemasýný deðiþtirmek, migration oluþturmak veya
> konfigürasyon ayarlarýný anlamak isteyen herkes.

---

## ?? Ýçindekiler

1. [Migration Nedir?](#1-migration-nedir)
2. [Migration Ýþ Akýþý](#2-migration-iþ-akýþý)
3. [Adým Adým Migration Oluþturma](#3-adým-adým-migration-oluþturma)
4. [Migration Geri Alma](#4-migration-geri-alma)
5. [Konfigürasyon Yapýsý](#5-konfigürasyon-yapýsý)
6. [Ortam Deðiþkenleri](#6-ortam-deðiþkenleri)
7. [Konfigürasyon Versiyonlama](#7-konfigürasyon-versiyonlama)
8. [Sýk Karþýlaþýlan Sorunlar](#8-sýk-karþýlaþýlan-sorunlar)

---

## 1. Migration Nedir?

**Migration**, veritabaný þemasýný kod üzerinden yönetmenin yoludur.

Normal hayatta:
- SQL dosyasýný elle yazarsýn
- Sunucuya baðlanýp çalýþtýrýrsýn
- Hangi SQL'in hangi sunucuda çalýþtýðýný takip edemezsin

Migration ile:
- C# kodu yazarsýn (veya otomatik üretilir)
- `dotnet ef database update` çalýþtýrýrsýn
- EF Core hangi migration'larýn uygulandýðýný otomatik takip eder

**Mevcut Migration'lar:**

```
migrations/
??? 20260124055211_InitialCreate.cs          ? Ýlk tablo oluþturma
??? 20260124095427_UpdateVectorDimension768.cs ? Vector boyutunu 768'e güncelleme
??? SonOfAntonDbContextModelSnapshot.cs       ? Güncel þema snapshot'ý
```

---

## 2. Migration Ýþ Akýþý

```
1. Entity Sýnýfýný Deðiþtir
   (Entities/WorkflowEntities.cs veya yeni entity)
        ?
        ?
2. DbContext'e Ekle (gerekiyorsa)
   (Data/PsyAIDbContext.cs ? DbSet + OnModelCreating)
        ?
        ?
3. Migration Oluþtur
   dotnet ef migrations add MigrationAdi
        ?
        ?
4. Oluþan Kodu Ýncele
   (Migrations/ klasörüne bak, Up/Down metodlarýný kontrol et)
        ?
        ?
5. Veritabanýný Güncelle
   dotnet ef database update
        ?
        ?
6. Test Et
   (Uygulama çalýþýyor mu? Yeni alan doðru kaydediliyor mu?)
```

---

## 3. Adým Adým Migration Oluþturma

### Senaryo: "Workflow'a `Version` alaný eklemek istiyorum"

**Adým 1:** Entity sýnýfýný güncelle

```csharp
// Shared/Entities/WorkflowEntities.cs
public class WorkflowEntity
{
    // ... mevcut alanlar ...
    
    /// <summary>
    /// Workflow þema versiyonu (breaking change takibi için)
    /// </summary>
    public int Version { get; set; } = 1;  // ? Yeni alan
}
```

**Adým 2:** DbContext'te mapping ekle (gerekiyorsa)

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

**Adým 3:** Terminal'de migration oluþtur

```bash
# Backend proje klasörüne git
cd src/SonOfAnton.Backend

# Migration oluþtur (isim anlamlý olmalý!)
dotnet ef migrations add AddWorkflowVersion

# ? Ýyi isimler:    AddWorkflowVersion, CreateWebhookTable, UpdateVectorDimension
# ? Kötü isimler:   Migration1, Fix, Update, Deneme
```

**Adým 4:** Oluþan dosyayý kontrol et

```csharp
// Otomatik oluþan Up() metodu þöyle olmalý:
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "version",
        table: "workflows",
        type: "integer",
        nullable: false,
        defaultValue: 1);
}

// Down() metodu geri almayý saðlar:
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "version",
        table: "workflows");
}
```

**Adým 5:** Veritabanýný güncelle

```bash
# Geliþtirme ortamý
dotnet ef database update

# Belirli bir migration'a güncelle
dotnet ef database update AddWorkflowVersion
```

---

## 4. Migration Geri Alma

**Son migration'ý geri al:**
```bash
dotnet ef database update PreviousMigrationName
```

**Migration dosyasýný sil (henüz uygulanmamýþsa):**
```bash
dotnet ef migrations remove
```

**?? DÝKKAT:** Production'da uygulanan migration ASLA silinmez!
Bunun yerine yeni bir migration ile deðiþikliði geri alýrsýn.

---

## 5. Konfigürasyon Yapýsý

Uygulama ayarlarý `appsettings.json` ve ortam deðiþkenlerinden okunur.

### appsettings.json Yapýsý

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
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Hangi ayar ne iþe yarar?

| Ayar | Varsayýlan | Açýklama |
|------|-----------|----------|
| `ConnectionStrings:DefaultConnection` | localhost | PostgreSQL baðlantý dizgisi |
| `Ollama:Endpoint` | `http://localhost:11434` | Ollama API adresi |
| `Ollama:Model` | `qwen2.5-coder:14b` | LLM model adý |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | Embedding model adý |

### Ortam bazlý override

```
appsettings.json                ? Tüm ortamlar için temel
appsettings.Development.json    ? Sadece geliþtirme ortamý
appsettings.Production.json     ? Sadece production ortamý
```

**Öncelik sýrasý (sonraki öncekini ezer):**
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Ortam deðiþkenleri
4. Komut satýrý argümanlarý

---

## 6. Ortam Deðiþkenleri

Ortam deðiþkenleri ile appsettings.json'daki her ayarý override edebilirsin.
`__` (çift alt çizgi) ile iç içe yapýlarý belirtirsin.

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

**?? Gizli bilgileri (þifre, API key vb.) ASLA appsettings.json'a yazma!**
Ortam deðiþkeni veya User Secrets kullan:

```bash
# Development ortamýnda User Secrets kullan
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=..."
```

---

## 7. Konfigürasyon Versiyonlama

Konfigürasyon deðiþikliklerini takip etmek için aþaðýdaki kurallarý uygula:

### Kurallar

1. **appsettings.json deðiþiklikleri commit'e dahil olmalý**
   - Yeni bir ayar eklediðinde varsayýlan deðerle birlikte ekle
   - PR açýklamasýnda "Yeni konfigürasyon: X eklendi" yaz

2. **Breaking change'lerde versiyon numarasýný artýr**
   ```json
   {
     "AppVersion": "2.0.0",
     "ConfigVersion": 3
   }
   ```

3. **Eski ayarlarý hemen silme, deprecated iþaretle**
   ```json
   {
     "OldSetting_DEPRECATED": "Bunu kullanma, NewSetting'e geç",
     "NewSetting": "aktif deðer"
   }
   ```

4. **Her ortam için örnek konfigürasyon dosyasý tut**
   ```
   appsettings.Example.json     ? Git'te (þifresiz)
   appsettings.Production.json  ? .gitignore'da (þifreli)
   ```

---

## 8. Sýk Karþýlaþýlan Sorunlar

### "Migration oluþturulamýyor"

```bash
# EF Core tools yüklü mü?
dotnet tool install --global dotnet-ef

# Doðru klasörde misin?
cd src/SonOfAnton.Backend

# Design-time package var mý? (csproj'da kontrol et)
# Microsoft.EntityFrameworkCore.Design olmalý
```

### "Pending migration var ama update baþarýsýz"

```bash
# Hangi migration'lar bekliyor?
dotnet ef migrations list

# Veritabaný baðlantýsýný kontrol et
# PostgreSQL çalýþýyor mu?
docker ps  # veya pg_isready
```

### "Connection string bulunamýyor"

Program.cs'deki varsayýlan deðeri kontrol et:
```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=sonofanton_db;Username=sonofanton;Password=sonofanton_dev_password";
```

Varsayýlan deðer varsa appsettings.json olmadan da çalýþýr.
Ama production'da mutlaka ortam deðiþkeni kullan!

---

> **Altýn Kural:** Migration oluþturduktan sonra mutlaka Up() ve Down()
> metodlarýný gözden geçir. Otomatik üretilen kod bazen gereksiz veya
> yanlýþ olabilir. `Down()` metodunun çalýþtýðýndan emin ol — bir gün
> geri almak zorunda kalabilirsin!
