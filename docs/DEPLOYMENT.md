# ?? SonOfAnton — Daðýtým ve Çalýþtýrma Kýlavuzu

> **Bu belge kime hitap ediyor?**  
> Projeyi kendi bilgisayarýnda çalýþtýrmak isteyen veya
> sunucuya daðýtmak isteyen herkes.

---

## ?? Ýçindekiler

1. [Gereksinimler](#1-gereksinimler)
2. [Yerel Geliþtirme Ortamý](#2-yerel-geliþtirme-ortamý)
3. [Docker ile Çalýþtýrma](#3-docker-ile-çalýþtýrma)
4. [Production Daðýtýmý](#4-production-daðýtýmý)
5. [Ortam Deðiþkenleri](#5-ortam-deðiþkenleri)
6. [Ýzleme ve Saðlýk Kontrolleri](#6-izleme-ve-saðlýk-kontrolleri)
7. [Sorun Giderme](#7-sorun-giderme)

---

## 1. Gereksinimler

### Zorunlu

| Bileþen | Minimum Versiyon | Ne Ýþe Yarar? |
|---------|------------------|---------------|
| .NET SDK | 10.0 | Projeyi derler ve çalýþtýrýr |
| PostgreSQL | 15+ | Ana veritabaný |
| pgvector uzantýsý | 0.5+ | Vektör arama (AI embedding) |

### Opsiyonel (AI özellikleri için)

| Bileþen | Minimum Versiyon | Ne Ýþe Yarar? |
|---------|------------------|---------------|
| Ollama | 0.1+ | Yerel LLM çalýþtýrma |
| ONNX Runtime | 1.23+ | Yerel embedding üretimi |

### Portlar

| Port | Servis |
|------|--------|
| 5000 | Backend API (HTTP) |
| 5001 | Backend API (HTTPS) |
| 5173 | Frontend (geliþtirme) |
| 5432 | PostgreSQL |
| 11434 | Ollama |

---

## 2. Yerel Geliþtirme Ortamý

### Adým 1: PostgreSQL Kur

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
# PostgreSQL kur ve pgvector uzantýsýný ekle
sudo apt install postgresql-16 postgresql-16-pgvector

# Veritabaný ve kullanýcý oluþtur
sudo -u postgres psql
CREATE USER sonofanton WITH PASSWORD 'sonofanton_dev_password';
CREATE DATABASE sonofanton_db OWNER sonofanton;
\c sonofanton_db
CREATE EXTENSION vector;
```

### Adým 2: Projeyi Kur

```bash
# Repoyu klonla
git clone https://github.com/ahmetsarikaya0696/SonOfAnton.git
cd SonOfAnton

# Baðýmlýlýklarý yükle
dotnet restore

# Veritabaný migration'larýný uygula
cd src/SonOfAnton.Backend
dotnet ef database update

# Backend'i baþlat
dotnet run
```

### Adým 3: Frontend'i Baþlat (ayrý terminal)

```bash
cd src/SonOfAnton.Frontend
dotnet run
```

### Adým 4: Ollama Kur (opsiyonel — AI özellikleri için)

```bash
# Ollama kur (macOS/Linux)
curl -fsSL https://ollama.com/install.sh | sh

# Model indir
ollama pull qwen2.5-coder:14b
ollama pull nomic-embed-text
```

### Adým 5: Doðrula

```bash
# API çalýþýyor mu?
curl https://localhost:5001/health/live

# Veritabaný baðlantýsý saðlýklý mý?
curl https://localhost:5001/health/ready

# OpenAPI dökümantasyonu
# Tarayýcýda: https://localhost:5001/openapi/v1.json
```

---

## 3. Docker ile Çalýþtýrma

### docker-compose.yml

```yaml
version: '3.8'

services:
  db:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_USER: sonofanton
      POSTGRES_PASSWORD: ${DB_PASSWORD:-sonofanton_dev_password}
      POSTGRES_DB: sonofanton_db
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U sonofanton"]
      interval: 5s
      timeout: 5s
      retries: 5

  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    # GPU kullanmak için:
    # deploy:
    #   resources:
    #     reservations:
    #       devices:
    #         - driver: nvidia
    #           count: 1
    #           capabilities: [gpu]

  backend:
    build:
      context: .
      dockerfile: src/SonOfAnton.Backend/Dockerfile
    ports:
      - "5001:5001"
    environment:
      - ConnectionStrings__DefaultConnection=Host=db;Database=sonofanton_db;Username=sonofanton;Password=${DB_PASSWORD:-sonofanton_dev_password}
      - Ollama__Endpoint=http://ollama:11434
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      db:
        condition: service_healthy

volumes:
  pgdata:
  ollama_data:
```

```bash
# Çalýþtýr
docker compose up -d

# Loglarý izle
docker compose logs -f backend
```

---

## 4. Production Daðýtýmý

### Kontrol Listesi

- [ ] Veritabaný þifresi deðiþtirildi (güçlü þifre)
- [ ] HTTPS sertifikasý yapýlandýrýldý
- [ ] Rate limiting ayarlarý gözden geçirildi
- [ ] CORS origin'leri production URL'lerine güncellendi
- [ ] Hangfire dashboard'u güvenli hale getirildi
- [ ] Log seviyesi ayarlandý (Warning veya Error)
- [ ] Veritabaný yedekleme planý yapýldý
- [ ] Health check monitoring kuruldu

### Önerilen Mimari

```
                    ???????????????
                    ?   Nginx /   ?
                    ?   Reverse   ?
Kullanýcýlar ????????   Proxy     ?
                    ?  (SSL/TLS)  ?
                    ???????????????
                           ?
                    ???????????????
                    ?  Backend    ?
                    ?  (Kestrel)  ?
                    ???????????????
                           ?
              ???????????????????????????
              ?            ?            ?
        ???????????? ???????????? ????????????
        ?PostgreSQL? ? Hangfire ? ?  Ollama  ?
        ?  (Ana)   ? ? (Worker) ? ?  (GPU)   ?
        ???????????? ???????????? ????????????
```

---

## 5. Ortam Deðiþkenleri

| Deðiþken | Zorunlu | Varsayýlan | Açýklama |
|----------|---------|-----------|----------|
| `ConnectionStrings__DefaultConnection` | Evet | localhost | PostgreSQL baðlantý dizgisi |
| `Ollama__Endpoint` | Hayýr | `http://localhost:11434` | Ollama API adresi |
| `Ollama__Model` | Hayýr | `qwen2.5-coder:14b` | LLM model adý |
| `Ollama__EmbeddingModel` | Hayýr | `nomic-embed-text` | Embedding model adý |
| `ASPNETCORE_ENVIRONMENT` | Hayýr | `Production` | Ortam adý |
| `ASPNETCORE_URLS` | Hayýr | `http://+:5000` | Dinlenecek URL'ler |

---

## 6. Ýzleme ve Saðlýk Kontrolleri

### Health Check Endpoint'leri

```bash
# Liveness: Uygulama ayakta mý?
# Kubernetes liveness probe olarak kullanýlabilir
curl http://localhost:5000/health/live

# Readiness: Trafiðe hazýr mý? (DB + Hangfire kontrol eder)
# Kubernetes readiness probe olarak kullanýlabilir
curl http://localhost:5000/health/ready
```

### Performans Metrikleri

```bash
# Uygulama metrikleri
curl http://localhost:5000/api/metrics
```

Dönen JSON yapýsý:
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

### SSE Ýstatistikleri

```bash
curl http://localhost:5000/api/sse/stats
```

### Hangfire Dashboard

Tarayýcýda: `http://localhost:5000/hangfire`

> **Not:** Production'da Hangfire dashboard'u kimlik doðrulama ile
> korunmalýdýr.

---

## 7. Sorun Giderme

### "Connection refused" — Veritabanýna baðlanamýyor

```bash
# PostgreSQL çalýþýyor mu?
pg_isready -h localhost -p 5432

# Docker ile mi çalýþtýrýyorsun?
docker ps | grep postgres

# Connection string doðru mu?
echo $ConnectionStrings__DefaultConnection
```

### "Ollama is not running" — AI özellikleri çalýþmýyor

```bash
# Ollama çalýþýyor mu?
curl http://localhost:11434/api/tags

# Çalýþmýyorsa baþlat
ollama serve

# Model yüklü mü?
ollama list
```

### "Migration pending" — Veritabaný eski

```bash
cd src/SonOfAnton.Backend
dotnet ef database update
```

### "Port already in use" — Port çakýþmasý

```bash
# Hangi iþlem portu kullanýyor? (Linux/macOS)
lsof -i :5000

# Windows
netstat -ano | findstr :5000

# Ýþlemi sonlandýr
kill -9 <PID>        # Linux/macOS
taskkill /PID <PID>  # Windows
```

### Test'ler baþarýsýz oluyor

```bash
# Tüm testleri verbose çalýþtýr
dotnet test tests/SonOfAnton.Tests --verbosity detailed

# Belirli bir test
dotnet test --filter "FullyQualifiedName~GraphTests"

# Coverage raporu
dotnet test --collect:"XPlat Code Coverage"
```

---

> **Altýn Kural:** Production'a daðýtmadan önce:
> 1. Tüm testlerin geçtiðinden emin ol (`dotnet test`)
> 2. Health check endpoint'lerini kontrol et
> 3. Veritabaný migration'larýný uygula
> 4. Ortam deðiþkenlerini doðrula
