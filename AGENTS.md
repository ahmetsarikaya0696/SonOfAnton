# SonOfAnton geliştirme talimatları

- Monolith'i koru; generic repository veya yeni katman ekleme. İş akışı yürütmesinde WorkflowRunner ve Hangfire tek yol olsun.
- Aktif çalışma planı docs/IMPLEMENTATION_PLAN.md; eski bulgular docs/REVIEW.md tarihsel referanstır. Tamamlanan işi test kanıtı ile işaretle.
- Kimlik doğrulama kaynak yetkisi değildir. Workflow/execution isteklerinde tenant, kullanıcı ve workspace üyeliğini doğrula. Background job bağlamını açıkça taşı.
- Varsayılan olarak deneysel API, script/plugin ve dış AI bağımlılıklarını açma. UI desteğini gerçek backend capability ile eşleştir.
- Production'da zayıf auth/secret fallback kullanma. Token, credential veya tüm request header'larını loglama/kaydetme.
- Migration geçmişini ve kullanıcı verisini silme. Startup migration başarısızsa fail-fast davran.
- Build: dotnet build SonOfAnton.slnx -m:1. Test: dotnet test SonOfAnton.slnx -m:1. PostgreSQL entegrasyon testlerinin ön koşullarını docs/DEPLOYMENT.md içinde tut.
- Testleri geçirmeden veya çalıştıramadığın kontrolleri açıkça belirtmeden çalışır/production-ready iddiasında bulunma.
- İlgili davranış değişince README ve API/deployment belgelerini güncelle. Commit'leri amaç odaklı tut.
