# SonOfAnton AI Workflow Platform

> **Kendi yapay zeka asistanini lego parcalari gibi birlestirerek olustur!**

---

## 🚀 Bu Proje Nedir?

**SonOfAnton**, bilgisayarinizda calisan akilli bir otomasyon fabrikasidir. Dijital bir **Lego seti** gibi dusunebilirsiniz:

- **Parcalar (Node'lar):** Her bir parca farkli bir is yapar - "PDF oku", "Ozet cikar", "Email gonder", "Vektor aramasi yap"...
- **Baglantilar (Kablolar):** Parcalari birbirine baglayarak verinin nereden nereye akacagini belirlersiniz.
- **Is Akisi (Workflow):** Parcalari birlestirip "Calistir" tusuna bastiginizda, kurdugunuz sistem otomatik olarak calisir.

### 🌟 Neler Yapabilirsiniz?
- Uzun belgeleri otomatik ozetleyen sistemler kurabilirsiniz.
- Belgeler icinde **semantik arama** yapabilirsiniz (kelime eslesmesi degil, anlam eslesmesi!).
- Her sabah 08:00'de calisip size gundem ozeti gecen botlar yapabilirsiniz.
- Web sitenizden gelen formlari otomatik analiz edip siniflandirabilirsiniz.
- **AI Asistan** ile dogal dilde konusarak workflow olusturabilirsiniz.
- **ForEach / While / Parallel** donguleri ile karmasik akislar kurabilirsiniz.

---

## 🛠 Teknoloji Yigini

| Katman | Teknoloji | Aciklama |
|--------|-----------|----------|
| **Backend** | .NET 10, ASP.NET Core | RESTful API |
| **Frontend** | Blazor WebAssembly | Tarayici tabanli SPA  |
| **Kimlik Yonetimi** | Keycloak | OIDC/OAuth2 Authentication & Authorization |
| **Diagram** | Z.Blazor.Diagrams | Gorsel node editoru |
| **Veritabani** | PostgreSQL + pgvector | JSONB + vektor arama + HNSW indeks |
| **DB Yonetimi** | pgAdmin 4 | Veritabani yonetim arayuzu |
| **Arka Plan** | Hangfire | Job scheduling, cron, kuyruk yonetimi |
| **AI** | Ollama + Semantic Kernel | Yerel LLM ve chat completion |
| **Embedding** | ONNX Runtime | Yerel embedding (all-MiniLM-L6-v2) |

---

## ⚡ Kurulum ve Calistirma

### Gereksinimler
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) (veya guncel surum)
- [Ollama](https://ollama.ai/) (AI ozellikleri icin)

### Adim 1: Projeyi Indirin
```bash
git clone https://github.com/ahmetsarikaya0696/SonOfAnton.git
cd SonOfAnton
```

### Adim 2: Altyapiyi Ayaga Kaldirin (Docker)
Veritabani, Yonetim Paneli ve Kimlik Servisi'ni baslatin:
```bash
docker-compose up -d
```
Bu komut asagidaki servisleri baslatir:
- **PostgreSQL**: `localhost:5432`
- **pgAdmin**: `localhost:5050`
- **Keycloak**: `localhost:8080`

### Adim 3: Backend'i Calistirin
```bash
cd src/SonOfAnton.Backend
dotnet run
```
Backend `https://localhost:5002` ve `http://localhost:5003` portlarinda calisacaktir. Ilk calistirmada veritabani semasini otomatik olusturur ve gerekli ONNX modellerini indirir.

### Adim 4: Frontend'i Calistirin
Yeni bir terminalde:
```bash
cd src/SonOfAnton.Frontend
dotnet run
```
Tarayicinizda `https://localhost:5001` adresine gidin.

---

## 🔐 Varsayilan Kimlik Bilgileri

Gelistirme ortami icin tanimli varsayilan kullanici ve sifreler:

| Servis | URL | Kullanici Adi | Sifre |
|--------|-----|---------------|-------|
| **Keycloak** | `http://localhost:8080` | `admin` | `admin` |
| **pgAdmin** | `http://localhost:5050` | `admin@sonofanton.local` | `admin` |
| **PostgreSQL** | `localhost:5432` | `sonofanton` | `sonofanton_dev_password` |

> **Not:** Keycloak ilk baslatildiginda `realm-export.json` dosyasindan gerekli ayarlari otomatik yukler (realm, client, users).

---

## 📂 Proje Yapisi

```
SonOfAnton/
+-- src/
|   +-- SonOfAnton.Backend/          # API, AI Motoru, Veritabani
|   +-- SonOfAnton.Frontend/         # Blazor WebAssembly Arayuzu
|   +-- SonOfAnton.Shared/           # Ortak DTO, Model ve Tipler
|
+-- docs/                            # Detayli Dokumantasyon
|   +-- API.md                       # API Endpoint Referansi
|   +-- ARCHITECTURE.md              # Mimari Tasarim ve Prensipler
|   +-- DATABASE.md                  # Veritabani Semasi ve Vektor Yapisi
|   +-- DEPLOYMENT.md                # Dagitim ve Container Kilavuzu
|
+-- init-scripts/                    # Docker ilk kurulum scriptleri (SQL)
+-- keycloak/                        # Keycloak Realm export dosyalari
+-- docker-compose.yml               # Container orkestrasyon dosyasi
```

---

## ✨ Temel Ozellikler

### 🎨 Gorsel Workflow Editoru
- Surukle-birak node ekleme, baglanti olusturma
- 30+ node tipi (Tetikleyici, HTTP, Donusum, Mantik, AI, Vector DB...)
- Dinamik parametre paneli ve `{{variable}}` destegi

### 🤖 AI Asistan ve Chat
- Dogal dilde workflow olusturma
- AI onerilerini tek tikla uygulama

### 🔄 Gelismis Akis Kontrolu
- **Donguler:** ForEach, While
- **Paralel Calisma:** WaitAll, WaitAny, FireAndForget
- **Yonetim:** Break, Continue

### 🛡️ Guvenlik ve Kimlik
- Keycloak entegrasyonu ile merkezi kimlik yonetimi (SSO)
- Rol tabanli erisim kontrolu (RBAC)
- Guvenli API iletisimi

### 📊 Veri ve Analiz
- **Vektor DB:** pgvector ile semantik arama
- **Dashboard:** Workflow istatistikleri ve execution takibi
- **Server-Sent Events (SSE):** Gercek zamanli log izleme

---

## 🤝 Katkida Bulunma

1. Fork edin.
2. Branch olusturun (`git checkout -b feature/YeniOzellik`).
3. Commit edin (`git commit -m 'Yeni ozellik eklendi'`).
4. Push edin (`git push origin feature/YeniOzellik`).
5. Pull Request acin.

---

## 📄 Lisans

Bu proje [MIT Lisansi](LICENSE) ile lisanslanmistir.
