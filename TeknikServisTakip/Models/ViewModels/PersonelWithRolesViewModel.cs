using Entities.Concrete;

namespace TeknikServisTakip.Models.ViewModels
{
    public class PersonelWithRolesViewModel
    {
        public Personel Personel { get; set; }
        public bool HasAnyRole { get; set; }
        public string Roles { get; set; }
    }
}
