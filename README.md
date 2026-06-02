# TeknikServisTakip
# Teknik Servis & Depo Yönetim Sistemi

**.NET 8 | SignalR | EF Core | SQL Server | Multi-Role Dashboard**

> Kurumsal teknik servis süreçlerini, depo-stok takibini, teklif yönetimini ve müşteri iletişimini tek platformda yöneten tam kapsamlı çözüm.

---

## 🎯 Sistem Özeti

| Modül | Açıklama |
|--------|------------|
| **Müşteri Yönetimi** | Excel'den toplu müşteri ekleme, arıza kaydı, SignalR ile canlı mesaj |
| **Teknik Servis** | Tamir süreci, malzeme takibi, her aşamada mail bildirimi |
| **Teklif Yönetimi** | Teklif oluşturma, revize etme, onaylama, arşivleme |
| **Depo & Stok** | Min/max takibi, giriş-çıkış hareketleri, otomatik stok düşme |
| **Rol Bazlı Paneller** | SuperAdmin / Admin / İdari / Depo / Sevkiyat / Personel / Müşteri |
| **Log Sistemi** | Action / Product / Mail / Hata logları |
| **Çoklu Resim** | Tamir öncesi & sonrası (5+5 resim) |

---

## 🔥 Öne Çıkanlar

✅ 7 farklı rol ve her role özel dashboard
✅ SignalR ile gerçek zamanlı bildirim & mesajlaşma
✅ Her ürün durum değişiminde müşteriye otomatik mail
✅ **Teklif oluşturma, revize etme ve onaylama süreci**
✅ **Onaylanmış teklifler arşivi (JSON snapshot ile)**
✅ **Revize edilen tekliflerin arşivlenmesi**
✅ Tamirde kullanılan ürün depodan otomatik düşer
✅ Excel'den toplu veri ekleme (müşteri + ürün + stok giriş)
✅ Server-side listeleme - binlerce kayıtta performans
✅ Stok min/max uyarı sistemi
✅ Action, Product, Mail, Hata logları
✅ .NET 8 - en güncel framework

---

## 📋 Teklif Yönetimi

| Özellik | Açıklama |
|---------|----------|
| **Teklif Oluşturma** | Ekspertiz kalemlerinden veya sıfırdan teklif oluşturma |
| **Revize Teklif** | Mevcut teklif üzerinden yeni versiyon oluşturma |
| **Teklif Onaylama** | Onaylanan teklif otomatik olarak arşivlenir |
| **Revize Arşivi** | Eski versiyon teklifler JSON snapshot ile saklanır |
| **Para Birimi Desteği** | TL, USD, EUR, GBP - her teklif kendi para biriminde |
| **KDV & İndirim** | Otomatik KDV ve indirim hesaplama |
| **İşçilik Maliyeti** | Ürün bazlı işçilik ekleme |

### Teklif Durumları

| Durum | Açıklama |
|-------|----------|
| Teklif Hazırlanıyor | Ekspertiz sonrası teklif hazırlanma aşaması |
| Teklif Gönderildi | Teklif müşteriye iletildi |
| Teklif Onaylandı | Müşteri teklifi onayladı, fiyat tamir kaydına aktarıldı |

---

## 🛠 Teknolojiler

| Alan | Teknoloji |
|------|-----------|
| Backend | ASP.NET Core MVC (.NET 8) |
| Frontend | Razor View Engine, Bootstrap 5, jQuery, Ajax |
| ORM | Entity Framework Core (Code First) |
| Veritabanı | SQL Server (LocalDB / Production) |
| Gerçek Zamanlı | SignalR |
| Excel | EPPlus |
| Mail | MailKit (SMTP) |
| PDF | PuppeteerSharp |
| Tablolama | Server-side DataTables |

---

## 🔒 Güvenlik

- ✅ CSRF (Anti-Forgery Token) koruması
- ✅ Role-Based Authorization (7 rol)
- ✅ XSS koruması (Razor auto-encoding)
- ✅ SQL Injection koruması (EF Core)
- ✅ Şifreler hashlenmiş (PBKDF2)
- ✅ Tüm işlemler loglanıyor
- ✅ Server-side input validation

---

## 👥 Rol ve Yetkiler

| Rol | Yetkileri |
|-----|-----------|
| **SuperAdmin** | Tüm sistem yetkisi, log izleme, rol atama |
| **Admin** | Kullanıcı yönetimi, raporlar, sistem ayarları |
| **İdari Personel** | Arıza takibi, personel görevlendirme, **teklif yönetimi** |
| **Depo Sorumlusu** | Stok giriş/çıkış, ürün yönetimi |
| **Sevkiyat** | Kargo süreçleri, teslimat takibi |
| **Teknik Servis Personeli** | Tamir süreci, malzeme kullanımı |
| **Müşteri** | Arıza kaydı, takip, mesajlaşma |

---

## 📊 Veritabanı Şeması (Özet)

| Tablo | Açıklama |
|-------|----------|
| RepairItems | Tamir kayıtları, durum, ücret, para birimi |
| Offers | Teklif ana tablosu, versiyon, para birimi, tutarlar |
| OfferLines | Teklif kalemleri (ürün bazlı) |
| OfferArchives | Onaylanmış teklif arşivi (JSON snapshot) |
| ReviseArchives | Revize edilmiş teklif arşivi |
| ExpertiseLines | Ekspertiz kalemleri |
| Products | Stok ürünleri, para birimi, fiyatlar |
| Deliveries | Teslimat kayıtları, kargo bilgileri |
| RepairMaterials | Tamirde kullanılan malzemeler |

---

## 🔐 Varsayılan Test Hesapları

| Rol | Email | Şifre |
|-----|-------|-------|
| SuperAdmin | superadmin@teknikservis.com | SuperAdmin123. |

> ⚠️ Production ortamında şifreler değiştirilmelidir.

---

## 📦 Kurulum

```bash
git clone [GitHub linkini buraya yapıştır]
cd proje-klasoru
dotnet restore
dotnet ef database update
dotnet run
