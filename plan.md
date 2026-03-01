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

## Task 1: Temel YZ Altyapısının Kurulması
- [ ] **Subtask 1.1: Yerel LLM Entegrasyonu (LM-Kit.NET / Ollama)**
  - [ ] Subtask 1.1.1: Yerel LLM kütüphanesinin (LM-Kit.NET) veya projedeki mevcut Semantic Kernel `.Connectors.Ollama` yapısının `SonOfAnton.Backend` projesine dahil edilmesi.
  - [ ] Subtask 1.1.2: Gelen metin isteklerini yerel modele iletecek ve yanıtı alacak C# servisinin (`ILocalAIService`) Semantic Kernel'a uyumlu (plugin/service) olarak yazılması.
  - [ ] Subtask 1.1.3: GPU hızlandırma ve donanım limitlerinin .NET ortamında yapılandırılması.
- [ ] **Subtask 1.2: Sansür Kuralları Motorunun (C# Regex & NER) Geliştirilmesi**
  - [ ] Subtask 1.2.1: `RegexOptions.Compiled` ve `CultureInvariant` kullanılarak TC Kimlik, IBAN ve Telefon formatları için C# filtreleme mantığının yazılması.
  - [ ] Subtask 1.2.2: Projede mevcut olan `Microsoft.ML.OnnxRuntime` bağımlılığı kullanılarak yerel NLP/NER modelinin (isim, şirket tespiti) entegre edilmesi.
  - [ ] Subtask 1.2.3: Sansür kurallarını ve ayarlarını PostgreSQL üzerinde tutmak için EF Core Entity ve Migration işlemlerinin uygulanması.
- [ ] **Subtask 1.3: Harici Model Ağ Geçidi (OpenAI, Claude, Gemini)**
  - [ ] Subtask 1.3.1: `IExternalAIGateway` arayüzünün (veya `IChatCompletionService` muadilinin) sağlayıcı bağımsız bir proxy olarak eklenmesi (Özellikler: Vision, Streaming, JSON Mode).
  - [ ] Subtask 1.3.2: Belirtilen modeller için Semantic Kernel tabanlı veya `HttpClient` tabanlı adaptör sınıflarının yazılması.
  - [ ] Subtask 1.3.3: `IAsyncEnumerable<T>` kullanılarak streaming yanıtlarının standart formatta Frontend'e veya istemciye aktarılması.
  - [ ] Subtask 1.3.4: Azure Key Vault (veya uygun Secret Management) entegrasyonu ile API anahtarlarının güvenliğini sağlayan servisin yazılması.

## Task 2: YZ Planlama ve Yürütme Çerçevesi
- [ ] **Subtask 2.1: C# Markdown Planlama Sisteminin Uygulanması**
  - [ ] Subtask 2.1.1: Ajanların `task_plan.md`, `findings.md` ve `progress.md` raporlarını yönetecek (okuma/yazma) dosya sistemi servisinin oluşturulması.
  - [ ] Subtask 2.1.2: .NET `FileSystemWatcher` kullanılarak dosya değişikliklerini dinleyecek ve gerçek zamanlı senkronize edecek event handler'ların yazılması.
  - [ ] Subtask 2.1.3: Eşzamanlı dosya çakışmalarını C# process-level lock veya `SemaphoreSlim` kullanarak engelleyecek yapının oluşturulması.
- [ ] **Subtask 2.2: Ralph Döngüsü (Yinelemeli Yürütme Motoru) Entegrasyonu**
  - [ ] Subtask 2.2.1: Görev tamamlama denetimlerinin .NET `BackgroundService` ve projedeki mevcut `Hangfire` altyapısı birleştirilerek döngü motoru halinde tasarlanması.
  - [ ] Subtask 2.2.2: Döngü içi "State" (durum) bilgilerinin PostgreSQL ve bellek (MemoryCache) üzerinde saklanması.
  - [ ] Subtask 2.2.3: İşlemlerden çıkış kurallarını yönetmek üzere `IValidator` arayüzlerinin (`SonOfAnton.Shared` veya proje içinde) tanımlanması.

## Task 3: Bellek (Memory) ve Halüsinasyon Önleme (RAG)
- [ ] **Subtask 3.1: OpenMemory C# Uyarlaması ve Vektör DB**
  - [ ] Subtask 3.1.1: 5 sektörlü bellek modelini temsil eden C# sınıflarının `SonOfAnton.Backend` içerisinde Entity Framework Code First standartlarında oluşturulması.
  - [ ] Subtask 3.1.2: Projedeki mevcut `Pgvector.EntityFrameworkCore` paketinden faydalanılarak PostgreSQL vektör uzantısının tablo ve sorgu entegrasyonunun yapılması.
  - [ ] Subtask 3.1.3: Metinlerin vektörleştirilmesi için `ITextEmbeddingGeneration` servis uygulamalarının yapılandırılması.
- [ ] **Subtask 3.2: RAG Pipeline Uygulaması**
  - [ ] Subtask 3.2.1: Veritabanındaki vektörler üzerinde vektörel benzerlik araması (Cosine Similarity vb.) yapılarak C# arama mekanizmasının kurulması (Azure AI Search veya yerel pgvector tabanlı sorgu ile).
  - [ ] Subtask 3.2.2: Giren uzun dökümanların işlenmesi için C# Token/Paragraf metin parçalama (Chunking) mantığının yazılması.
  - [ ] Subtask 3.2.3: Semantic Kernel kullanarak (veya manuel) elde edilen RAG verilerinin LLM promptlarına doğru biçimde dahil edilmesi (Prompt Builder).

## Task 4: Platform Entegrasyonu ve İş Akışı Dışa Aktarımı
- [ ] **Subtask 4.1: C# Native İş Akışı Modelleri ve Dışa Aktarım**
  - [ ] Subtask 4.1.1: Arayüz (Frontend/NodeSdk) üzerinden gelen görsel iş akışını (Node graph) JSON'dan ara dile (IR) ayrıştıracak C# parser servisinin yazılması.
  - [ ] Subtask 4.1.2: Ayrıştırılan yapıyı (Roslyn / `Microsoft.CodeAnalysis.CSharp.Scripting` paketinden yararlanarak) somut C# `record`/LINQ tabanlı kodlara dökecek kod üretim (Generator) modülünün oluşturulması.
  - [ ] Subtask 4.1.3: Üretilen dinamik kodlara arka planda `dotnet format` işlemini tetikleyecek sürecin geliştirilmesi.
- [ ] **Subtask 4.2: MCP (Model Context Protocol) Desteği**
  - [ ] Subtask 4.2.1: Microsoft MCP C# SDK'sının referans olarak ilgili arka uç (Backend) süreçlerine dahil edilmesi.
  - [ ] Subtask 4.2.2: `SonOfAnton.Backend` yeteneklerinin (veya özel araçların) MCP server endpoint'leri üzerinden LLM'lere veya merkezi platformlara açılması.

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
