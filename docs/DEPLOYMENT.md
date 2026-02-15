#  SonOfAnton — Dağıtım ve Çalıştırma Kılavuzu

> **Bu belge kime hitap ediyor?**  
> Projeyi kendi bilgisayarında çalıştırmak isteyen veya
> sunucuya dağıtmak isteyen herkes.

---

##  İçindekiler

1. [Gereksinimler](#1-gereksinimler)
2. [Yerel Geliştirme Ortamı](#2-yerel-geliştirme-ortamı)
3. [Docker ile Çalıştırma](#3-docker-ile-çalıştırma)
4. [Production Dağıtımı](#4-production-dağıtımı)
5. [Ortam Değişkenleri](#5-ortam-değişkenleri)
6. [İzleme ve Sağlık Kontrolleri](#6-izleme-ve-sağlık-kontrolleri)
7. [Sorun Giderme](#7-sorun-giderme)

---

## 1. Gereksinimler

### Zorunlu

| Bileşen | Minimum Versiyon | Ne İşe Yarar? |
|---------|------------------|---------------|
| .NET SDK | 10.0 | Projeyi derler ve çalıştırır |
| PostgreSQL | 15+ | Ana veritabanı |
| pgvector uzantısı | 0.5+ | Vektör arama (AI embedding) |
| Docker | 20+ | Keycloak ve PostgreSQL konteyner yönetimi |
| Keycloak | 26.1+ | OIDC kimlik doğrulama sağlayıcısı |

### Opsiyonel (AI özellikleri için)

| Bileşen | Minimum Versiyon | Ne İşe Yarar? |
|---------|------------------|---------------|
| Ollama | 0.1+ | Yerel LLM çalıştırma |
| ONNX Runtime | 1.23+ | Yerel embedding üretimi |

### Portlar

| Port | Servis |
|------|--------|
| 5000 | Backend API (HTTP) |
| 5001/5002 | Backend API (HTTPS) |
| 5173 | Frontend (geliştirme) |
| 5432 | PostgreSQL |
| 5050 | pgAdmin (veritabanı yönetim aracı) |
| 8080 | Keycloak (kimlik doğrulama) |
| 11434 | Ollama |

---

## 2. Yerel Geliştirme Ortamı

### Adım 1: PostgreSQL Kur

**Docker ile (önerilen):**
```bash
docker run -d \
  --name sonofanton-db \
  -e POSTGRES_USER=sonofanton \
  -e POSTGRES_PASSWORD=sonofanton_dev_password \
  -e POSTGRES_DB=sonofanton_db \
  -p 5432:5432 \
  pgvector/pgvector:pg16
```

**Manuel kurulum:**
```bash
# PostgreSQL kur ve pgvector uzantısını ekle
sudo apt install postgresql-16 postgresql-16-pgvector

# Veritabanı ve kullanıcı oluştur
sudo -u postgres psql
CREATE USER sonofanton WITH PASSWORD 'sonofanton_dev_password';
CREATE DATABASE sonofanton_db OWNER sonofanton;
\c sonofanton_db
CREATE EXTENSION vector;
```

### Adım 2: Projeyi Kur

```bash
# Repoyu klonla
git clone https://github.com/ahmetsarikaya0696/SonOfAnton.git
cd SonOfAnton

# Bağımlılıkları yükle
dotnet restore

# Veritabanı migration'larını uygula
cd src/SonOfAnton.Backend
dotnet ef database update

# Backend'i başlat
dotnet run
```

### Adım 3: Frontend'i Başlat (ayrı terminal)

```bash
cd src/SonOfAnton.Frontend
dotnet run
```

### Adım 4: Ollama Kur (opsiyonel — AI özellikleri için)

```bash
# Ollama kur (macOS/Linux)
curl -fsSL https://ollama.com/install.sh | sh

# Model indir
ollama pull qwen2.5-coder:14b
ollama pull nomic-embed-text
```

### Adım 5: Doğrula

```bash
# API çalışıyor mu?
curl https://localhost:5001/health/live

# Veritabanı bağlantısı sağlıklı mı?
curl https://localhost:5001/health/ready

# OpenAPI dökümantasyonu
# Tarayıcıda: https://localhost:5001/openapi/v1.json
```

---

## 3. Docker ile Çalıştırma

### docker-compose.yml

Projenin kökünde bulunan `docker-compose.yml` dosyası tüm bağımlı servisleri yönetir:

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    container_name: sonofanton-postgres
    environment:
      POSTGRES_USER: sonofanton
      POSTGRES_PASSWORD: ${DB_PASSWORD:-sonofanton_dev_password}
      POSTGRES_DB: sonofanton_db
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U sonofanton -d sonofanton_db"]
      interval: 10s
      timeout: 5s
      retries: 5

  pgadmin:
    image: dpage/pgadmin4:latest
    container_name: sonofanton-pgadmin
    environment:
      PGADMIN_DEFAULT_EMAIL: admin@sonofanton.local
      PGADMIN_DEFAULT_PASSWORD: admin
    ports:
      - "5050:80"
    depends_on:
      - postgres

  keycloak:
    image: quay.io/keycloak/keycloak:26.1
    container_name: sonofanton-keycloak
    command: start-dev --import-realm
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: admin
      KC_HTTP_PORT: "8080"
    ports:
      - "8080:8080"
    volumes:
      - ./keycloak:/opt/keycloak/data/import
    healthcheck:
      test: ["CMD-SHELL", "exec 3<>/dev/tcp/localhost/8080"]
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s

volumes:
  postgres_data:
```

> **Not:** Keycloak `start-dev` modunda çalışır ve `./keycloak` klasöründeki
> realm export dosyasını otomatik import eder. Production'da `start` modu kullanılmalıdır.

```bash
# Çalıştır
docker compose up -d

# Logları izle
docker compose logs -f backend
```

---

## 4. Production Dağıtımı

### Kontrol Listesi

- [ ] Veritabanı şifresi değiştirildi (güçlü şifre)
- [ ] HTTPS sertifikası yapılandırıldı
- [ ] Rate limiting ayarları gözden geçirildi
- [ ] CORS origin'leri production URL'lerine güncellendi
- [ ] Hangfire dashboard'u güvenli hale getirildi
- [ ] Log seviyesi ayarlandı (Warning veya Error)
- [ ] Veritabanı yedekleme planı yapıldı
- [ ] Health check monitoring kuruldu

### Önerilen Mimari

```
                    ---------------
                    |   Nginx /   ?
                    |   Reverse   ?
Kullanıcılar -----|--   Proxy     ?
                    ?  (SSL/TLS)  ?
                    ---------------
                           ?
                    ---------------
                    ?  Backend    ?
                    ?  (Kestrel)  ?
                    ---------------
                           ?
              ---------------------------
              |            |            ?
        ---------|-- ---------|-- ------------
        ?PostgreSQL? ? Hangfire ? ?  Ollama  ?
        ?  (Ana)   ? ? (Worker) ? ?  (GPU)   ?
        ---------|-- ---------|-- ------------
```

---

## 5. Ortam Değişkenleri

| Değişken | Zorunlu | Varsayılan | Açıklama |
|----------|---------|-----------|----------|
| `ConnectionStrings__DefaultConnection` | Evet | localhost | PostgreSQL bağlantı dizgisi |
| `Ollama__Endpoint` | Hayır | `http://localhost:11434` | Ollama API adresi |
| `Ollama__Model` | Hayır | `qwen2.5-coder:14b` | LLM model adı |
| `Ollama__EmbeddingModel` | Hayır | `nomic-embed-text` | Embedding model adı |
| `ASPNETCORE_ENVIRONMENT` | Hayır | `Production` | Ortam adı |
| `ASPNETCORE_URLS` | Hayır | `http://+:5000` | Dinlenecek URL'ler |

---

## 6. İzleme ve Sağlık Kontrolleri

### Health Check Endpoint'leri

```bash
# Liveness: Uygulama ayakta mı?
# Kubernetes liveness probe olarak kullanılabilir
curl http://localhost:5000/health/live

# Readiness: Trafiğe hazır mı? (DB + Hangfire kontrol eder)
# Kubernetes readiness probe olarak kullanılabilir
curl http://localhost:5000/health/ready
```

### Performans Metrikleri

```bash
# Uygulama metrikleri
curl http://localhost:5000/api/metrics
```

Dönen JSON yapısı:
```json
{
  "uptimeSeconds": 3600.5,
  "totalWorkflowExecutions": 150,
  "totalNodeExecutions": 1200,
  "totalFailures": 3,
  "activeWorkflows": 2,
  "failureRate": 2.0,
  "nodeTypeStats": [
    { "nodeType": "logic.if", "executionCount": 300, "averageDurationMs": 1.2 },
    { "nodeType": "transform.set", "executionCount": 250, "averageDurationMs": 0.5 }
  ],
  "hourlyCounts": [
    { "hour": "2025-01-24-14", "count": 12 },
    { "hour": "2025-01-24-15", "count": 8 }
  ],
  "recentErrors": [
    { "workflowId": "...", "error": "Node timeout", "occurredAt": "..." }
  ]
}
```

### SSE İstatistikleri

```bash
curl http://localhost:5000/api/sse/stats
```

### Hangfire Dashboard

Tarayıcıda: `http://localhost:5000/hangfire`

> **Not:** Production'da Hangfire dashboard'u kimlik doğrulama ile
> korunmalıdır.

---

## 7. Sorun Giderme

### "Connection refused" — Veritabanına bağlanamıyor

```bash
# PostgreSQL çalışıyor mu?
pg_isready -h localhost -p 5432

# Docker ile mi çalıştırıyorsun?
docker ps | grep postgres

# Connection string doğru mu?
echo $ConnectionStrings__DefaultConnection
```

### "Ollama is not running" — AI özellikleri çalışmıyor

```bash
# Ollama çalışıyor mu?
curl http://localhost:11434/api/tags

# Çalışmıyorsa başlat
ollama serve

# Model yüklü mü?
ollama list
```

### "Migration pending" — Veritabanı eski

```bash
cd src/SonOfAnton.Backend
dotnet ef database update
```

### "Port already in use" — Port çakışması

```bash
# Hangi işlem portu kullanıyor? (Linux/macOS)
lsof -i :5000

# Windows
netstat -ano | findstr :5000

# İşlemi sonlandır
kill -9 <PID>        # Linux/macOS
taskkill /PID <PID>  # Windows
```

### Test'ler başarısız oluyor

```bash
# Tüm testleri verbose çalıştır
dotnet test tests/SonOfAnton.Tests --verbosity detailed

# Belirli bir test
dotnet test --filter "FullyQualifiedName~GraphTests"

# Coverage raporu
dotnet test --collect:"XPlat Code Coverage"
```

---

> **Altın Kural:** Production'a dağıtmadan önce:
> 1. Tüm testlerin geçtiğinden emin ol (`dotnet test`)
> 2. Health check endpoint'lerini kontrol et
> 3. Veritabanı migration'larını uygula
> 4. Ortam değişkenlerini doğrula


---

## 8. Keycloak Kimlik Doğrulama Konfigürasyonu

### Keycloak Kurulumu

SonOfAnton, varsayılan kimlik sağlayıcısı olarak **Keycloak** kullanır. Docker Compose ile otomatik başlatılır:

```bash
# Tüm servisleri başlat (PostgreSQL + pgAdmin + Keycloak)
docker compose up -d

# Keycloak Admin Console: http://localhost:8080/admin
# Kullanıcı: admin / Şifre: admin
```

### Keycloak Realm Yapılandırması

Realm export dosyası `./keycloak/` klasöründe bulunur ve Docker başlangıcında otomatik import edilir.

| Ayar | Değer |
|------|-------|
| **Realm** | `sonofanton` |
| **Frontend Client** | `sonofanton-frontend` (public, PKCE) |
| **Backend Client** | `sonofanton-backend` (confidential) |
| **Redirect URIs** | `https://localhost:5001/*`, `http://localhost:5173/*` |
| **Web Origins** | `https://localhost:5001`, `http://localhost:5173` |

### Backend appsettings.json — Authentication Bölümü

```json
{
  "Authentication": {
    "DefaultAuthority": "https://login.microsoftonline.com/common/v2.0",
    "DefaultClientId": "<your-client-id>",
    "DefaultClientSecret": "<your-client-secret>",
    "AllowExternalBearer": true
  },
  "MultiTenancy": {
    "ResolveBy": "Header",
    "DefaultTenantId": "default"
  }
}
```

### Backend appsettings.Development.json — Keycloak Override

```json
{
  "Authentication": {
    "DefaultAuthority": "http://localhost:8080/realms/sonofanton",
    "DefaultClientId": "sonofanton-backend",
    "DefaultClientSecret": "backend-dev-secret"
  }
}
```

### Frontend wwwroot/appsettings.json — OIDC Yapılandırması

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

### Ortam Değişkenleri

```bash
# Backend OIDC (Keycloak)
Authentication__DefaultAuthority=http://localhost:8080/realms/sonofanton
Authentication__DefaultClientId=sonofanton-backend
Authentication__DefaultClientSecret=backend-dev-secret

# Multi-Tenancy
MultiTenancy__ResolveBy=Header
MultiTenancy__DefaultTenantId=default
```

### Multi-Tenant Keycloak Kurulumu

Her tenant için ayrı OIDC konfigürasyonu desteklenir. Tenant bilgileri `tenants` tablosunda saklanır:

```sql
INSERT INTO tenants (id, name, oidc_authority, oidc_client_id, oidc_client_secret, is_active, created_at)
VALUES (
  'kurumA',
  'Kurum A',
  'http://localhost:8080/realms/kurumA',
  'kurumA-client-id',
  'kurumA-client-secret',
  true,
  NOW()
);
```

> **Not:** Multi-realm kullanımda Keycloak'ta her kurum için ayrı realm oluşturulabilir.

### SCIM Provisioning Kurulumu

Keycloak veya diğer kimlik sağlayıcınızda SCIM provisioning yapılandırın:

| Ayar | Değer |
|------|-------|
| **SCIM Base URL** | `https://your-domain.com/scim/v2` |
| **Authentication** | Bearer Token |
| **Users endpoint** | `/scim/v2/Users` |
| **Groups endpoint** | `/scim/v2/Groups` |

### Yerel Geliştirme İçin Keycloak Kullanıcı Oluşturma

1. `http://localhost:8080/admin` adresine gidin (admin/admin)
2. `sonofanton` realm'ini seçin
3. Users → Add user ile yeni kullanıcı ekleyin
4. Credentials sekmesinden şifre belirleyin
5. Frontend'den `http://localhost:5173` adresine gidip "Giriş Yap" butonuna tıklayın

> **Not:** OIDC sağlayıcı yapılandırılmadığında uygulama başlar ama auth gerektiren endpoint'ler `401` döner.
> Geliştirme sırasında auth'u devre dışı bırakmak için controller'lardaki `[Authorize]` attribute'larını yorum satırına alabilirsiniz.

### Veritabanı Migration (Kimlik Tabloları)

```bash
cd src/SonOfAnton.Backend
dotnet ef migrations add AddIdentityEntities
dotnet ef database update
```

Bu migration aşağıdaki tabloları oluşturur:
- `tenants` → Kurum bilgileri
- `users` → Kullanıcılar
- `tenant_roles` → Rol-izin eşleştirmesi
- `user_tenant_roles` → Kullanıcı-rol ilişkisi
- `workflows.tenant_id`, `workflows.created_by_user_id` → Multi-tenant FK'ler
- `folders.tenant_id` → Multi-tenant FK
