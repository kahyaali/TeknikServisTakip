namespace TeknikServisTakip.Helpers
{
    public static class PhoneHelper
    {
        // Boşlukları kaldırıyoruz
        public static string CleanPhone(this string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return phone;

            return phone.Replace(" ", "");
        }

        // Formatlayıp boşluklu gösteriyoruz
        public static string FormatPhone(this string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length != 11)
                return phone;

            return $"{phone.Substring(0, 4)} {phone.Substring(4, 3)} {phone.Substring(7, 2)} {phone.Substring(9, 2)}";
        }

        // Doğrulama türkiye telefon formatındamı
        public static bool IsValidTurkishPhone(this string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return false;

            var cleanPhone = phone.CleanPhone();
            var phoneRegex = new System.Text.RegularExpressions.Regex(@"^(0?5\d{9})$");

            return phoneRegex.IsMatch(cleanPhone);
        }

     
        public static string NormalizePhone(this string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return phone;

            var cleanPhone = phone.CleanPhone();

            if (!System.Text.RegularExpressions.Regex.IsMatch(cleanPhone, @"^(0?5\d{9})$"))
                return phone;

            if (!cleanPhone.StartsWith("0"))
                cleanPhone = "0" + cleanPhone;

            return cleanPhone;
        }

        // Formatla (null güvenli)
        public static string FormatPhoneSafe(this string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length != 11)
                return string.Empty;

            return $"{phone.Substring(0, 4)} {phone.Substring(4, 3)} {phone.Substring(7, 2)} {phone.Substring(9, 2)}";
        }
    }
}