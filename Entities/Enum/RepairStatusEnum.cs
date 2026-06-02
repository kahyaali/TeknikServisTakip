using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Enum
{
   public enum RepairStatusEnum
    {
        [Display(Name = "Ürün Kaydedildi")]
        UrunKaydedildi = 1,

        [Display(Name = "Expertiz Bekleniyor")]
        ExpertizBekleniyor = 2,

        [Display(Name = "Expertize Gönderildi")]
        ExpertizeGonderildi = 3,

        [Display(Name = "Teklif Hazırlanıyor")]
        TeklifHazirlaniyor = 4,

        [Display(Name = "Teklif Gönderildi")]
        TeklifGonderildi = 5,

        [Display(Name = "Teklif Onaylandı")]
        TeklifOnaylandi = 6,

        [Display(Name = "İşleme Alındı")]
        IslemeAlindi = 7,

        [Display(Name = "Tamamlandı")]
        Tamamlandi = 8,

        [Display(Name = "Teslim Edildi")]
        TeslimEdildi = 9
    }
}
