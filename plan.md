# SonOfAnton - YZ Altyapısı Proje Planı

## Proje Yapısı ve Mevcut Teknoloji Yığını (Tech Stack)
Mevcut `.csproj` dosyaları incelendiğinde projenin halihazırda aşağıdaki teknolojileri kullandığı görülmektedir. Plan bu teknolojilere göre optimize edilmiştir:
- **Framework:** .NET 10.0
- **Veritabanı ve Vektör Arama:** PostgreSQL + Entity Framework Core (`Npgsql.EntityFrameworkCore.PostgreSQL`) + `Pgvector.EntityFrameworkCore`
- **YZ Çerçevesi:** Microsoft Semantic Kernel (`Microsoft.SemanticKernel`, `Ollama` bağlayıcıları)
- **Arka Plan İşleri:** Hangfire (`Hangfire.AspNetCore`, `Hangfire.PostgreSql`)
- **Makine Öğrenimi / Yerel Çıkarım:** ONNX Runtime (`Microsoft.ML.OnnxRuntime`)
- **İletişim Altyapısı:** Eklenecek olan RabbitMQ & MassTransit
- **Projeler:**
  - `SonOfAnton.Backend`: Temel YZ servisleri, DB bağlamları (DbContext), Arka plan işleri, API uç noktaları.
  - `SonOfAnton.Shared`: Veri transfer nesneleri (DTO), ortak arayüzler ve ajanlar arası iletişim kontratları (Contracts).
  - `SonOfAnton.NodeSdk` / `SonOfAnton.Frontend`: Arayüz ve UI bileşenleri.

---

## Task 5: MassTransit & RabbitMQ ile A2A İletişimi ve Sansür Katmanı
- [ ] **Subtask 5.1: RabbitMQ ve MassTransit Altyapısının Kurulması**
  - [ ] Subtask 5.1.1: RabbitMQ altyapısının Docker üzerinde management plugin ile hazırlanması (Uygulama dışı bağımlılık).
  - [ ] Subtask 5.1.2: Ajan mesajı kontratlarının (`AnalyzeDataCommand`, `DataAnalyzedEvent` vb.) **`SonOfAnton.Shared`** projesi içerisinde salt C# `record`'ları olarak tasarlanması.
  - [ ] Subtask 5.1.3: `SonOfAnton.Backend` projesine `MassTransit.RabbitMQ` bağımlılığının eklenip `Program.cs` içerisinde Publisher/Consumer (Bus) ayrımının yapılması.
- [ ] **Subtask 5.2: MassTransit Pipeline Filter (Sansür Ara Katmanı) Yazılması**
  - [ ] Subtask 5.2.1: MassTransit'in `IFilter<PublishContext<T>>` arayüzü ile çıkış öncesi (Egress) mesajların yakalanması ve sansürlenmesinin sağlanması.
  - [ ] Subtask 5.2.2: MassTransit'in `IFilter<ConsumeContext<T>>` arayüzü ile mesaj ajana girmeden (Ingress) denetlenmesi.
  - [ ] Subtask 5.2.3: Geliştirilen Regex/NER (ONNX) Sansür Motoru mantığının bu filtrelere DI (Dependency Injection) üzerinden bağlanması.
- [ ] **Subtask 5.3: Hata Yönetimi ve Reddedilen Mesajlar (Fault Tolerance)**
  - [ ] Subtask 5.3.1: Hassas veri taşıyan istekler için filter içerisinde `CensorshipViolationException` throw edilecek yapısının kurgulanması.
  - [ ] Subtask 5.3.2: MassTransit hata yönetimi özelliğinden faydalanarak ihlallerin Dead-Letter (`_error`) kuyruklarına gönderilmesi ve proje geneli Serilog vb. logger'lara kaydedilmesi.

---

### Veri Akış Şeması Özeti
- İstek `SonOfAnton.Backend` API'na düşer.
- İhtiyaca göre doğrudan `Yerel LLM` (Ollama/LM-Kit) ya da ağ geçidi (`OpenAI vb.`) hedeflenir. Eş zamanlı olarak Semantic Kernel üzerinden pgvector üzerinden `RAG` verisi toplanabilir.
- Modelden yanıt istenmeden önce, gönderilecek tüm "veri", oluşturulacak `MassTransit` mesaj filtreleri `IFilter` ile Task 1.2'deki Regex & ONNX NER denetiminden (Sansür) geçer. Güvensizse işlem durarak `CensorshipViolationException` fırlatılır ve Dead Letter Queue'ya bırakılır.
- Veri işlenmek üzere (`SonOfAnton.Shared` kontratı ile) ajanlara (`RabbitMQ`) gönderilirken veya sonucunda kod üretilirken arka planda `Hangfire` süreci yönetip döngüyü kontrol altında tutar.
