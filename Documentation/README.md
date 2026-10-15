# Banka İçi Ürün / Süreç İlişki ve Bilgi Yönetim Uygulaması

Banka içindeki **ürün, işlem ve süreç başlıklarını** dinamik ağaçlar hâlinde gösteren;
her başlığın altına ilgili **öğeleri** (alt işlem/ekran/süreç) bağlayabildiğiniz ve her
bağlantı için **ekran adı, batch adı ve kısa açıklama** bilgisini yönetebildiğiniz
premium masaüstü uygulamasıdır.

> Not: Bu uygulama daha önce sayısal "etki analizi" aracıydı; o sistem tamamen
> kaldırıldı. Artık amaç, sayısal senaryo/metrik hesabı değil, **başlık–öğe ilişki
> haritası ve dokümantasyonudur.**

## Temel kavramlar

| Kavram | Karşılığı |
|--------|-----------|
| **Başlık** (parent) | Ağacın kökü; soldaki listeden seçilir (ör. "Repo", "Yatırım İşlemleri"). |
| **Öğe** (child) | Bir başlığa bağlanan alt kalem (ör. "Raporlama", "İptal işlemi"). |
| **Bağlantı** | Bir başlık ile bir öğe arasındaki bağ. Bilgi (ekran adı/batch adı/kısa açıklama) her zaman bu bağlantı bağlamında tutulur. |

Aynı öğe birden fazla başlığa bağlanabilir ve her başlık altında **ayrı** bilgiye sahip
olabilir (Repo → Raporlama ile Yatırım Fonu → Raporlama farklı bilgi taşır). Bir node
hem başlık hem öğe olabilir.

## Mimari (katmanlı)

Her katman ayrı bir .NET 9 projesidir; bağımlılık yönü tek yönlüdür. UI doğrudan SQLite
bilmez; yalnızca arayüzleri (`INodeRepository`, `INodeDetailsRepository`,
`IWriteLockService`, `IChildInformationService`) tanır.

| Proje | Sorumluluk |
|-------|------------|
| `Domain` | Alan modeli: `Node`, `NodeRelation`, `NodeRelationDetails`, `NodeDraft`. UI/SQLite bağımlılığı yoktur. |
| `Application` | Servis sözleşmeleri + `ChildInformationService` (bilgi yükle/kaydet, doğrulama, kilit, audit), `WriteLockModels`. |
| `Infrastructure` | SQLite adaptörleri (`SqliteNodeRepository`, `SqliteNodeDetailsRepository`, `SqliteWriteLockService`, şema + migration), bellek-içi eşdeğerleri, dosya loglama. |
| `Presentation` | WPF/MVVM: başlık seçimi, ağaç canvas'ı, öğe havuzu, bilgi paneli + düzenleme formu. |
| `Tests/Unit` | 89 birim/entegrasyon testi (xUnit). |

Kurumsal bir veritabanına (ör. SQL Server) geçişte yalnızca Infrastructure'a yeni bir
adaptör eklenir; Domain, Application ve ekranlar değişmez.

## Veri kaynağı (SQLite)

Varsayılan veri kaynağı SQLite'tır; yol `Presentation/appsettings.json`'dan okunur:

```json
{ "DataSource": "SQLite", "DatabasePath": "Data/ProductImpact.db" }
```

- İlk çalıştırmada veritabanı oluşturulur ve `NodeCatalogSeed`'ten **31 başlık + 17 öğe**
  seed edilir (isimler kullanıcının verdiği hâliyle korunur; başlangıçta hiçbir öğe
  otomatik bağlanmaz — havuzda bekler).
- `PRAGMA journal_mode=WAL`, `foreign_keys=ON`, parametreli sorgular, transaction'lar.
- Şema sürümlüdür (`AppSettings.SchemaVersion`); açılışta **idempotent migration** çalışır.
- SQLite açılamazsa uygulama düşmez; uyarı gösterip **bellek-içi (yerleşik)** veriyle devam eder.
- **Journal modu DELETE'tir (WAL değil).** Bu bilinçlidir: WAL, paylaşılan bir dosyaya birden
  fazla makineden ağ üzerinden erişimi desteklemez; DELETE (klasik rollback journal) ise
  destekler. Değiştirmeyin.

### Paylaşımlı (çok kullanıcılı) kurulum

Ortak veri için tek bir `.db` dosyası, herkesin okuma/yazma erişimi olan bir **ağ
paylaşımında** durur; herkes aynı dosyayı gösterir:

1. `ProductImpact.db`'yi bir **dosya sunucusu paylaşımına** koyun (kapanan bir kullanıcı
   PC'sine değil). Örn. `\\sunucu\ortak\BankaUrunAgaci\ProductImpact.db`.
2. Her kullanıcının `appsettings.json`'ında **aynı UNC yolunu** verin (JSON'da ters bölü
   çiftlenir):
   ```json
   { "DataSource": "SQLite", "DatabasePath": "\\\\sunucu\\ortak\\BankaUrunAgaci\\ProductImpact.db" }
   ```
3. Her kullanıcı **kendi yerel makinesinde** self-contained yayını (exe) çalıştırır; yalnızca
   veritabanı paylaşılır. (Exe'yi paylaşımdan çalıştırmak yavaş olur.)

Neden güvenli çalışır: yazımlar **uygulama-seviyesi tek yazıcı kilidiyle** (ApplicationWriteLock)
sıralanır — aynı anda yalnızca bir kişi düzenler. DELETE journal + kilit birlikte, paylaşılan
SQLite'ı küçük/orta ekipler için kullanılabilir kılar.

Sınırlar ve operasyon:
- Sunucu olmadığından **otomatik yedek yoktur** — paylaşılan `.db`'yi zamanlanmış görevle
  (ör. her gece) kopyalayın. Bozulma nadir ama olursa tek kurtuluş yedektir.
- Uygulama **canlı yenilemez**: biri kaydederken açık olan diğerleri, değişikliği görmek için
  başlığı yeniden seçmeli/uygulamayı yeniden açmalıdır.
- İleride kurumsal DB sunucusu (ör. SQL Server) sağlanırsa geçiş yereldir: yalnızca yeni bir
  Infrastructure adaptörü + bağlantı adresi; Domain/Application/ekranlar değişmez.

### Tablolar (şema sürümü 1.6)

- **Nodes** — ortak node kataloğu (`CanBeRoot` başlık olabilirliğini belirler).
- **NodeRelations** — başlık–öğe bağları. `CHECK(ParentNodeId<>ChildNodeId)`,
  `UNIQUE(ParentNodeId, ChildNodeId)`. Ağaçtan çıkarma **soft-delete**tir (`IsActive=0`).
- **NodeRelationDetails** — bağlantıya özel bilgi: `ScreenName`, `BatchName`,
  `ShortDescription` (her bağlantı için en fazla bir kayıt).
- **ApplicationWriteLock** — tek yazıcı kilidi (kısmi UNIQUE index ile tek aktif kilit).
- **AuditLog** — kim/ne zaman/hangi işlem (eski/yeni JSON, başarı/hata).
- **AppSettings** — şema sürümü vb.

## Tek yazıcı kilidi (YETKİLENDİRME DEĞİLDİR)

Aynı anda yalnızca bir kullanıcının yazabilmesini sağlar; **kim yazabilir** ayrımı
(rol/yetki) YOKTUR. `SqliteWriteLockService`: `BEGIN IMMEDIATE` ile atomik alım,
`OwnerToken` (GUID) ile sahip kimliği, 30 sn TTL + 10 sn heartbeat, süre aşımında yeniden
alım. Yazma gerektiren işlemler: yeni başlık ekleme, öğe ekleme/çıkarma, konum değiştirme,
bilgi kaydetme. Görüntüleme/hover/arama kilit gerektirmez.

## Kullanım

- **Sol panel** — başlık arama, "+ Yeni Başlık Ekle", başlık listesi.
- **Orta alan** — seçili başlık merkezde; bağlı öğeler radyal dallar hâlinde. Düzenleme
  modunda sürükleyerek taşınır (konum kaydedilir), × ile ağaçtan çıkarılır.
- **Sağ panel** — hover ile öğe bilgisi önizlemesi; tıklama ile sabitlenir. "Bilgileri
  Düzenle" ile aynı panelde form açılır (Ekran adı / Batch adı / Kısa açıklama →
  Kaydet/Vazgeç). Düzenleme modunda öğe havuzu (seçerek/sürükleyerek ekleme, yeni öğe).
- **Üst** — seçili başlık adı = pencere başlığı = merkez kutu (tek kaynak), veri kaynağı
  rozeti, "Ağacı Düzenle" düğmesi.

## Çalıştırma

```powershell
dotnet build                       # tüm çözümü derler
dotnet run --project Presentation  # uygulamayı başlatır
dotnet test                        # 89 testi çalıştırır
```

Gereksinim: .NET 9 SDK, Windows (WPF).

## Bilinen sınırlar / sonraki adımlar (banka üretimi öncesi)

Aşağıdakiler **kasıtlı olarak ertelenmiştir**; kurumsal üretime geçmeden önce
değerlendirilmelidir:

1. **Yetkilendirme yok** — uygulamayı açan herkes düzenleyebilir. Rol bazlı (görüntüleme/
   düzenleme) erişim, ör. AD grup üyeliğiyle eklenmelidir.
2. **Migration öncesi otomatik yedek yok** — şema yükseltmeleri geri alınamaz işlemler
   içerebilir; öncesinde `.db` dosyasının yedeklenmesi önerilir.
3. **Ağ paylaşımında SQLite riski** — çok kullanıcılı senaryoda paylaşılan sürücüdeki tek
   `.db` dosyası eşzamanlı yazımda bozulabilir; kurumsal DB sunucusu (SQL Server) önerilir.
4. **Denetim izi yalnızca yazılıyor** — `AuditLog` için görüntüleme/dışa aktarma ekranı yok;
   ayrıca aynı yazılabilir dosyada durduğundan değiştirilemez (immutable) değildir.

Değerler ve seed içeriği temsili/pilot niteliktedir; gerçek kullanım ilgili birimlerin
onayını gerektirir.
