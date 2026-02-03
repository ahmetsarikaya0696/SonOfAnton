# 🤖 SonOfAnton AI Workflow Platform

![SonOfAnton Banner](https://via.placeholder.com/800x200?text=SonOfAnton+AI+Automation)

> **"Kendi yapay zeka asistanını lego parçaları gibi birleştirerek oluştur!"**

---

## 👋 Bu Proje Nedir? (Basit Anlatım)

Merhaba! **SonOfAnton**, bilgisayarınızda çalışan akıllı bir otomasyon fabrikasıdır. 

Bunu dijital bir **Lego seti** gibi düşünebilirsiniz:
*   🧱 **Parçalar (Node'lar):** Her bir parça farklı bir iş yapar. Biri "PDF oku" der, diğeri "Özet çıkar", bir başkası "Email gönder".
*   🔗 **Bağlantılar (Kablolar):** Parçaları birbirine bağlayarak verinin nereden nereye akacağını belirlersiniz.
*   🚀 **İş Akışı (Workflow):** Parçaları birleştirip "Çalıştır" tuşuna bastığınızda, kurduğunuz sistem otomatik olarak çalışır.

### ✨ Neler Yapabilirsiniz?
*   📄 Uzun belgeleri otomatik özetleyen sistemler kurabilirsiniz.
*   🔍 Kendi bilgisayarınızdaki belgeler içinde "akıllı arama" yapabilirsiniz (Kelime eşleşmesi değil, anlam eşleşmesi!).
*   ⏰ Her sabah saat 08:00'de çalışıp size gündem özeti geçen botlar yapabilirsiniz.
*   🔗 Web sitenizden gelen formları otomatik analiz edip sınıflandırabilirsiniz.

**En güzel yanı:** Bunların hepsini **kod yazmadan**, kutucukları sürükleyip bırakarak yaparsınız! Ayrıca yapay zeka modelleri (AI) tamamen **kendi bilgisayarınızda** çalışır, verileriniz dışarı çıkmaz.

---

## 🛠️ Yazılımcılar İçin (Teknik Bakış)

SonOfAnton, **Local First AI** prensibiyle geliştirilmiş, modern ve yüksek performanslı bir .NET çözümüdür. Harici API bağımlılığı olmadan (OpenAI vb. gerektirmeden) kendi makinenizde LLM ve Embedding modellerini çalıştırabilir.

### 🏗️ Teknoloji Yığını (Tech Stack)

*   **Backend:**
    *   **.NET 10 (Preview):** En son teknoloji, yüksek performans.
    *   **ASP.NET Core API:** RESTful servisler.
    *   **Hangfire:** Arka plan iş yönetimi (Job Scheduling), Cron joblar ve kuyruk yönetimi.
    *   **Entity Framework Core:** Veritabanı ORM.
    *   **PostgreSQL + pgvector:** Vektör veritabanı. RAG (Retrieval-Augmented Generation) için HNSW indeksleme kullanır.
    *   **ONNX Runtime:** Yerel model çalıştırma (Local Inference). `all-MiniLM-L6-v2` vb. modelleri CPU/GPU üzerinde çalıştırır.
    *   **Semantic Kernel:** AI orkestrasyonu.

*   **Frontend:**
    *   **MAUI Blazor Hybrid:** Cross-platform masaüstü uygulaması (Windows/Mac/Linux).
    *   **Blazor.Diagrams:** Node editör görselleştirmesi.
    *   **Fabric.js:** Canvas manipülasyonu.
    *   **Server-Sent Events (SSE):** Real-time execution takibi ve loglama.

### 🧩 Temel Özellikler

1.  **Görsel Node Editörü:**
    *   Tamamen özelleştirilebilir node ve port yapısı.
    *   Dinamik parametre yönetimi.
    *   C# tabanlı güçlü graph motoru (DAG - Directed Acyclic Graph).

2.  **Vektör Veritabanı & RAG:**
    *   Dökümanları otomatik olarak parçalar (chunking).
    *   Embedding vektörlerine çevirir (ONNX ile lokalde).
    *   PostgreSQL üzerinde semantik arama yapar.

3.  **Gelişmiş Execution Engine:**
    *   **Trigger Sistemi:** Manual, Zamanlanmış (Cron), Webhook.
    *   **Execution Monitoring:** İş akışını anlık olarak izleme, hangi node'un çalıştığını görme.
    *   **Hata Yönetimi:** Hata alan node'ları yakalama, loglama.

---

## 🚀 Kurulum ve Çalıştırma

Projeyi kendi bilgisayarınızda çalıştırmak için aşağıdaki adımları izleyin.

### Gereksinimler
*   [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Veritabanı için)
*   [.NET 9.0 veya 10.0 SDK](https://dotnet.microsoft.com/download)
*   Visual Studio 2022 veya VS Code

### Adım 1: Projeyi İndirin
```bash
git clone https://github.com/yourusername/SonOfAnton.git
cd SonOfAnton
```

### Adım 2: Altyapıyı Hazırlayın (Docker)
PostgreSQL ve pgvector içeren konteynerı ayağa kaldırın:
```bash
docker-compose up -d
```
*Bu komut `docker-compose.yml` dosyasını kullanarak gerekli veritabanı ortamını hazırlar.*

### Adım 3: Backend'i Çalıştırın
Terminalde backend klasörüne gidin ve uygulamayı başlatın:
```bash
cd src/SonOfAnton.Backend
dotnet run
```
*Backend ilk açılışta veritabanı tablolarını otomatik oluşturacak (Migration) ve gerekli AI modellerini (ONNX) internetten indirip `models/` klasörüne kaydedecektir.*

### Adım 4: Frontend'i Çalıştırın
Yeni bir terminal açın ve frontend klasörüne gidin:
```bash
cd src/SonOfAnton.Frontend
dotnet run
```
*Uygulama masaüstü penceresi olarak açılacaktır.*

---

## 📚 Proje Yapısı

```
SonOfAnton/
├── src/
│   ├── SonOfAnton.Backend/       # API, AI Motoru, Veritabanı Servisleri
│   │   ├── Controllers/          # API Uç noktaları
│   │   ├── Services/             # İş mantığı (AI, Job, SSE)
│   │   ├── Engine/               # Workflow çalıştırma motoru
│   │   └── Data/                 # Veritabanı modelleri
│   │
│   ├── SonOfAnton.Frontend/      # Masaüstü Arayüzü (Blazor Hybrid)
│   │   ├── Components/           # UI Bileşenleri (Editor, Monitor)
│   │   ├── Services/             # Backend ile iletişim
│   │   └── wwwroot/              # CSS ve JS dosyaları
│   │
│   └── SonOfAnton.Shared/        # Ortak Veri Modelleri (DTOs)
│       ├── Entities/             # Veritabanı varlıkları
│       └── Models/               # Node tanımları
└── docker-compose.yml            # Veritabanı konfigürasyonu
```

---

## 🤝 Katkıda Bulunma

Projeye katkıda bulunmak isterseniz çok mutlu oluruz!
1.  Bu repository'i Fork edin.
2.  Yeni bir Branch oluşturun (`git checkout -b feature/HarikaOzellik`).
3.  Değişikliklerinizi Commit edin (`git commit -m 'Harika özellik eklendi'`).
4.  Branch'inizi Push edin (`git push origin feature/HarikaOzellik`).
5.  Bir Pull Request açın.

---

## 📝 Lisans

Bu proje [MIT Lisansı](LICENSE) ile lisanslanmıştır. Kısaca: İstediğiniz gibi kullanabilir, değiştirebilir ve dağıtabilirsiniz.

---

<div align="center">
  Made with ❤️ by <b>SonOfAnton Team</b>
</div>
