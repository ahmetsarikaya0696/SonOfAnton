# SonOfAnton — Kullanım rehberi

Kurulum: [DEPLOYMENT.md](DEPLOYMENT.md). Bu rehber desteklenen çekirdek kapsam içindir.

1. Editörü açıp Keycloak ile giriş yapın. İlk giriş kişisel workspace oluşturur.
2. Workspace seçin, “Boş Yeni” ile taslak açın, akışa ad verin.
3. Paletten **Manuel Tetikleyici** ekleyin. Yalnız çalışan sunucuda kayıtlı node'lar görünür.
4. İsterseniz **Deger Ata** ve debug ekleyip çıkış–giriş portlarını bağlayın; parametreleri node panelinde düzenleyin.
5. **Kaydet**, sonra **Çalıştır** seçin. Monitör sonucu gösterir; execution ekranı kalıcı logları sunar.

Kaydetme hatası editörde gösterilir. Başka workspace'in Viewer üyesi olmanız düzenleme/çalıştırma yetkisi vermez. Header değiştirmekle yetki kazanılmaz.

Test çalıştırması node sınırlarında bekler; adım komutu ilerletir. Pause/resume aynı backend sürecinde çalışır. Sunucu restart'ından sonra kaldığı yerden devam garantisi yoktur. Snapshot alınabilir; geri yükleme henüz desteklenmez. Yarım kalmış işi yeniden başlatmadan önce dış etkileri kontrol edin.

AI flag kapalıysa AI node'ları görünmez. AI chat, marketplace, chatbot, public webhook/API publishing, credential kasası, HTTP/script/plugin/subflow, RAG ve veri tabloları/formları bu çekirdekte kapalıdır. Veri silinmez. Gerçek model ve production kabulü ayrı yürütülür.

Durum ve kalan işler: [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).
