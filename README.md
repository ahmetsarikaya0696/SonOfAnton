# SonOfAnton AI Workflow Platform

> **Kendi yapay zeka asistanini lego parcalari gibi birlestirerek olustur!**

---

## Bu Proje Nedir?

**SonOfAnton**, bilgisayarinizda calisan akilli bir otomasyon fabrikasidir.

Bunu dijital bir **Lego seti** gibi dusunebilirsiniz:
- **Parcalar (Node'lar):** Her bir parca farkli bir is yapar - "PDF oku", "Ozet cikar", "Email gonder", "Vektor aramasi yap"...
- **Baglantilar (Kablolar):** Parcalari birbirine baglayarak verinin nereden nereye akacagini belirlersiniz.
- **Is Akisi (Workflow):** Parcalari birlestirip "Calistir" tusuna bastiginizda, kurdugunuz sistem otomatik olarak calisir.

### Neler Yapabilirsiniz?
- Uzun belgeleri otomatik ozetleyen sistemler kurabilirsiniz.
- Belgeler icinde **semantik arama** yapabilirsiniz (kelime eslesmesi degil, anlam eslesmesi!).
- Her sabah 08:00'de calisip size gundem ozeti gecen botlar yapabilirsiniz.
- Web sitenizden gelen formlari otomatik analiz edip siniflandirabilirsiniz.
- **AI Asistan** ile dogal dilde konusarak workflow olusturabilirsiniz.
- **ForEach / While / Parallel** donguleri ile karmasik akislar kurabilirsiniz.
- Workflow'larinizi **klasorlerle** organize edebilirsiniz.
- Calisan bir execution'in **snapshot**'ini alip daha sonra geri yukleyebilirsiniz.

**En guzel yani:** Bunlarin hepsini **kod yazmadan**, kutucuklari surukleyip birakarak yaparsiniz! AI modelleri tamamen **kendi bilgisayarinizda** calisir, verileriniz disari cikmaz.

---

## Teknoloji Yigini

| Katman | Teknoloji | Aciklama |
|--------|-----------|----------|
| **Backend** | .NET 10, ASP.NET Core | RESTful API |
| **Frontend** | Blazor WebAssembly | Tarayici tabanli SPA |
| **Diagram** | Z.Blazor.Diagrams | Gorsel node editoru |
| **Veritabani** | PostgreSQL + pgvector | JSONB + vektor arama + HNSW indeks |
| **Arka Plan** | Hangfire | Job scheduling, cron, kuyruk yonetimi |
| **AI** | Ollama + Semantic Kernel | Yerel LLM ve chat completion |
| **Embedding** | ONNX Runtime | Yerel embedding (all-MiniLM-L6-v2) |
| **YAML** | YamlDotNet | Workflow import/export |
| **Gercek Zamanli** | Server-Sent Events (SSE) | Execution takibi ve loglama |

---

## Temel Ozellikler

### Gorsel Workflow Editoru
- Surukle-birak node ekleme, baglanti olusturma
- 30+ node tipi (Tetikleyici, HTTP, Donusum, Mantik, AI, Vector DB, Donguler, Iletisim)
- Dinamik parametre paneli, `{{variable}}` template destegi
- Keyboard shortcut'lar (Ctrl+K - Global Search)

### AI Asistan ve Chat
- Dogal dilde workflow olusturma ve duzenleme
- AI'dan gelen graph onerisini tek tikla uygulama
- YAML preview ile workflow import/export

### Subflow ve Donguler
- **ForEach Loop:** Koleksiyon uzerinde dongu
- **While Loop:** Kosul saglandigi surece tekrar
- **Parallel:** Dallari eszamanli calistirma (WaitAll / WaitAny / FireAndForget)
- **Break / Continue:** Dongu kontrol node'lari

### Execution Engine
- Hangfire tabanli arka plan calistirma
- Node bazinda **retry** ve **timeout** mekanizmasi
- **Pause / Resume:** Calisan execution'i duraklatma ve devam ettirme
- **Snapshot:** Execution durumunu kaydetme ve geri yukleme
- Cevrimsel bagimlilik tespiti

### Vektor Veritabani ve RAG
- pgvector + IVFFlat / HNSW indeksleme
- ONNX ile yerel embedding (768 boyut)
- Cosine / Euclidean / Inner Product benzerlik metrikleri

### Organizasyon ve Istatistik
- **Klasor sistemi:** Hiyerarsik workflow organizasyonu
- **Dashboard:** Toplam workflow, execution, basari orani
- **Workflow Stats:** Gunluk calistirma grafigi, ortalama sure
- **Global Search:** Full-text + hybrid (vektor) arama

### A2A Protocol
- Agent-to-Agent iletisim altyapisi
- Agent Registry ile agent kesfi
- Task-based asenkron iletisim

---

## Kurulum ve Calistirma

### Gereksinimler
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.ai/) (AI ozellikleri icin)

### Adim 1: Projeyi Indirin
```bash
git clone https://github.com/ahmetsarikaya0696/SonOfAnton.git
cd SonOfAnton
```

### Adim 2: Backend'i Calistirin
```bash
cd src/SonOfAnton.Backend
dotnet run
```
> Backend ilk calistirmada Docker uzerinden PostgreSQL + pgvector container'ini otomatik ayaga kaldirir, veritabani tablolarini olusturur ve ONNX modellerini indirir.

### Adim 3: Frontend'i Calistirin
```bash
cd src/SonOfAnton.Frontend
dotnet run
```
> Tarayicida `https://localhost:5001` adresinden erisebilirsiniz.

---

## Proje Yapisi

```
SonOfAnton/
+-- src/
|   +-- SonOfAnton.Backend/          # API, AI Motoru, Veritabani
|   |   +-- Controllers/             # REST endpoint'leri (12 controller)
|   |   +-- Engine/                  # Workflow calistirma motoru
|   |   |   +-- Handlers/            # Node handler'lari (25+ handler)
|   |   |   +-- Expressions/         # template engine
|   |   +-- Services/                # Is servisleri
|   |   |   +-- Search/              # Full-text + Hybrid arama
|   |   |   +-- Snapshots/           # Execution snapshot
|   |   +-- Execution/               # PauseToken, WorkflowExecutor
|   |   +-- A2A/                     # Agent-to-Agent protocol
|   |   +-- Agents/                  # AI Agent servisleri
|   |   +-- Orchestrator/            # Proje orkestrasyon
|   |   +-- Infrastructure/          # Embedding, VectorDB, Docker
|   |   +-- Data/                    # EF Core DbContext
|   |
|   +-- SonOfAnton.Frontend/         # Blazor WebAssembly
|   |   +-- Components/              # 20+ UI bileseni
|   |   +-- Pages/                   # Dashboard, Executions, ExecutionDetail
|   |   +-- Services/                # API, Diagram, SSE, Chat, YAML
|   |   +-- wwwroot/                 # CSS, JS
|   |
|   +-- SonOfAnton.Shared/           # Ortak kod
|       +-- DTOs/                    # Veri transfer objeleri
|       +-- Entities/                # Veritabani entity'leri (7 entity)
|       +-- Enums/                   # ExecutionStatus, SubflowType vb.
|       +-- Models/                  # NodeRegistry, WorkflowTemplates
|       +-- Serialization/           # YAML serializer
|       +-- Core/GraphEngine/        # DAG motoru
|
+-- docs/                            # Dokumantasyon
    +-- API.md                       # API referansi
    +-- ARCHITECTURE.md              # Mimari
    +-- DATABASE.md                  # Veritabani semasi
    +-- DEPLOYMENT.md                # Dagitim kilavuzu
```

---

## Katkida Bulunma

1. Bu repository'i Fork edin.
2. Yeni bir Branch olusturun (`git checkout -b feature/HarikaOzellik`).
3. Degisikliklerinizi Commit edin (`git commit -m 'Harika ozellik eklendi'`).
4. Branch'inizi Push edin (`git push origin feature/HarikaOzellik`).
5. Bir Pull Request acin.

---

## Lisans

Bu proje [MIT Lisansi](LICENSE) ile lisanslanmistir.
