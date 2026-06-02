
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace TeknikServisTakip.Helpers
{
    public static class EnumExtensions
    {

        // Enum üzerindeki display name değerlerini okumak için yazılan extension
        public static string GetDisplayName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? value.ToString();
        }
    }
}
