# SonOfAnton — Uygulama planı

Başlangıç: 2 Kasım 2026. Referans: [statik inceleme](REVIEW.md). Amaç: güvenli varsayılanlarla çalışan, build ve entegrasyon testleriyle doğrulanan workflow çekirdeği. İşaretler yalnızca kanıtlandıktan sonra tamamlanır. Genişletilmiş ürün özellikleri çekirdekten ayrı değerlendirilir.

## 1. Tekrarlanabilir geliştirme ve doğrulama

- [x] 1.1 .NET 10 ile solution derlemesini çalıştır.
  - [x] Restore ve derleme hatalarını gider.
  - [x] SDK sürümünü ve CI build/test komutlarını sabitle.
- [x] 1.2 Gerçek PostgreSQL üzerinde uygulamayı başlat.
  - [x] Temiz veritabanına migration zincirini uygula.
  - [x] Backend health/OpenAPI ve frontend sunumunu doğrula.
- [x] 1.3 Test projesi ve regresyon senaryolarını ekle.

## 2. Güvenlik sınırları

- [x] 2.1 JWT issuer/audience doğrulaması, güvenli fallback policy ve yönetici paneli.
  - [x] Claim/token loglarını kaldır; header tenant uyuşmazlığını reddet.
  - [x] JWT ve API key claim sözleşmelerini birleştir.
- [x] 2.2 Workflow/execution kaynak erişimini kullanıcı, tenant ve workspace ile sınırla.
  - [x] CRUD, execution, snapshot, log ve stream erişimini kapsa.
  - [x] Read-only kullanıcının mutasyon yapmasını engelle.
  - [x] Job scope'unu HTTP bağlamından bağımsız taşı.
- [x] 2.3 Legacy anonim workflow CRUD'u kaldır; genişletilmiş özellikleri varsayılan kapat.
- [x] 2.4 Script/plugin yürütmesini güvenli varsayılanla kapat; credential şifrelemesini düzelt.
- [x] 2.5 Endpoint rate limit'lerini bağla.

## 3. Çalıştırma güvenilirliği

- [x] 3.1 Tek enqueue/runner yolu ve execution kimliği.
  - [x] Aynı job tekrar geldiğinde yeni kayıt veya otomatik yan etki tekrarı oluşturma.
  - [x] Başlangıç verisini node'a taşı; terminal durumları koru.
  - [x] İptal ve schedule davranışlarını düzelt; entegrasyon testiyle doğrula.
  - [x] Adımlı bekleme, step ve normal devam akışını gerçek tarayıcıda doğrula; duraklatılmış işi iptal etmeyi API entegrasyonuyla doğrula.
  - [ ] Normal koşum sırasında Duraklat/Devam ve çok node çizimini tarayıcı kabulüne ekle.
- [x] 3.2 SSE'yi abone başına bounded yayın ve tek writer ile düzelt.
- [x] 3.3 Node kayıtlarını doğrula; AI/embedding yalnızca etkinse bağla.
- [x] 3.4 Migration hatasında startup'ı durdur.

## 4. Kapsam sadeleştirmesi

- [x] 4.1 Kullanılmayan RabbitMQ startup bağımlılığını kaldır.
- [x] 4.2 Kullanılmayan alternatif executor/legacy kodu kaldır.
- [x] 4.3 A2A, Ralph loop, marketplace, chatbot, veri formu/tablosu ve published API'yi çekirdek dışı deneysel kapsam olarak açıkça sınırla.
  - [x] Eksik güvenlik/entegrasyon testleri tamamlanana kadar varsayılan erişimi kapat.
  - [x] Veriyi ve migration geçmişini koru.

## 5. Kabul ve belgeler

- [x] 5.1 Minimal workflow oluştur/kaydet/çalıştır/sonuç/log senaryosunu API ve gerçek Hangfire/PostgreSQL üzerinden doğrula.
  - [x] Keycloak girişi → editör → node ekle → kaydet → çalıştır → sonuç/log akışını gerçek Chromium ile CI'da doğrula.
- [x] 5.2 Anonim, başka tenant/workspace ve read-only negatif testleri.
- [x] 5.3 Tekrar çalıştırma ve çok istemcili SSE testleri.
- [x] 5.4 README, API, deployment ve geliştirici talimatlarını gerçek davranışa göre güncelle.
- [x] 5.5 Test kanıtını ve doğrulanamayan sınırları kaydet; amaç odaklı commit'ler oluştur.
  - [x] Değişiklikleri GitHub inceleme dalına yükle.
  - [x] Çekirdek tarayıcı kabulü ve CI sonucunu doğrula; PR'ı incelemeye hazırla.

## Çekirdek sürümün dışında

Gerçek harici AI sağlayıcıları, güvensiz kod sandbox'ı, tam SCIM/A2A uyumluluğu, marketplace ve public chatbot ürünleştirmesi ayrı kabul gerektirir. Bu alanları test edilmeden tamamlandı sayma. Production yedek/restore, kurumun secret yönetimi ve lisans seçimi depo sahibinin dağıtım ortamıyla doğrulanmalıdır.

## Doğrulama durumu

6 Kasım 2026: 12 .NET ve 2 JavaScript testi, temiz Release build, EF model kontrolü ve dört execution içeren Chromium kabulü başarılı. Ayrıntılar ve ortam sınırlaması: [VERIFICATION.md](VERIFICATION.md). Açık kabul maddeleri varken bütün uygulama veya production hazırlığı tamamlanmış sayılmaz.
