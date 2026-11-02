# SonOfAnton — Uygulama planı

Başlangıç: 2 Kasım 2026. Referans: [statik inceleme](REVIEW.md). Amaç: güvenli varsayılanlarla çalışan, build ve entegrasyon testleriyle doğrulanan workflow çekirdeği. İşaretler yalnızca kanıtlandıktan sonra tamamlanır. Genişletilmiş ürün özellikleri çekirdekten ayrı değerlendirilir.

## 1. Tekrarlanabilir geliştirme ve doğrulama

- [ ] 1.1 .NET 10 ile solution derlemesini çalıştır.
  - [ ] Restore ve derleme hatalarını gider.
  - [ ] SDK sürümünü ve CI build/test komutlarını sabitle.
- [ ] 1.2 Gerçek PostgreSQL üzerinde uygulamayı başlat.
  - [ ] Temiz veritabanına migration zincirini uygula.
  - [ ] Backend health/OpenAPI ve frontend sunumunu doğrula.
- [ ] 1.3 Test projesi ve regresyon senaryolarını ekle.

## 2. Güvenlik sınırları

- [ ] 2.1 JWT issuer/audience doğrulaması, güvenli fallback policy ve yönetici paneli.
  - [ ] Claim/token loglarını kaldır; header tenant uyuşmazlığını reddet.
  - [ ] JWT ve API key claim sözleşmelerini birleştir.
- [ ] 2.2 Workflow/execution kaynak erişimini kullanıcı, tenant ve workspace ile sınırla.
  - [ ] CRUD, execution, snapshot, log ve stream erişimini kapsa.
  - [ ] Read-only kullanıcının mutasyon yapmasını engelle.
  - [ ] Job scope'unu HTTP bağlamından bağımsız taşı.
- [ ] 2.3 Legacy anonim workflow CRUD'u kaldır; genişletilmiş özellikleri varsayılan kapat.
- [ ] 2.4 Script/plugin yürütmesini güvenli varsayılanla kapat; credential şifrelemesini düzelt.
- [ ] 2.5 Endpoint rate limit'lerini bağla.

## 3. Çalıştırma güvenilirliği

- [ ] 3.1 Tek enqueue/runner yolu ve execution kimliği.
  - [ ] Aynı job tekrar geldiğinde yeni kayıt veya otomatik yan etki tekrarı oluşturma.
  - [ ] Başlangıç verisini node'a taşı; terminal durumları koru.
  - [ ] İptal/pause ve schedule davranışlarını düzelt.
- [ ] 3.2 SSE'yi abone başına bounded yayın ve tek writer ile düzelt.
- [ ] 3.3 Node kayıtlarını doğrula; AI/embedding yalnızca etkinse bağla.
- [ ] 3.4 Migration hatasında startup'ı durdur.

## 4. Kapsam sadeleştirmesi

- [ ] 4.1 Kullanılmayan RabbitMQ startup bağımlılığını kaldır.
- [ ] 4.2 Kullanılmayan alternatif executor/legacy kodu kaldır.
- [ ] 4.3 A2A, Ralph loop, marketplace, chatbot, veri formu/tablosu ve published API'yi çekirdek dışı deneysel kapsam olarak açıkça sınırla.
  - [ ] Eksik güvenlik/entegrasyon testleri tamamlanana kadar varsayılan erişimi kapat.
  - [ ] Veriyi ve migration geçmişini koru.

## 5. Kabul ve belgeler

- [ ] 5.1 Minimal workflow oluştur/kaydet/çalıştır/sonuç/log senaryosu.
- [ ] 5.2 Anonim, başka tenant/workspace ve read-only negatif testleri.
- [ ] 5.3 Tekrar çalıştırma ve çok istemcili SSE testleri.
- [ ] 5.4 README, API, deployment ve geliştirici talimatlarını gerçek davranışa göre güncelle.
- [ ] 5.5 Test kanıtını ve doğrulanamayan sınırları kaydet; atomik commit'ler ve PR oluştur.

## Çekirdek sürümün dışında

Gerçek harici AI sağlayıcıları, güvensiz kod sandbox'ı, tam SCIM/A2A uyumluluğu, marketplace ve public chatbot ürünleştirmesi ayrı kabul gerektirir. Bu alanları test edilmeden tamamlandı sayma. Production yedek/restore, kurumun secret yönetimi ve lisans seçimi depo sahibinin dağıtım ortamıyla doğrulanmalıdır.
