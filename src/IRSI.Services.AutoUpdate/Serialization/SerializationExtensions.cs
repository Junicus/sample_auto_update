using System.Linq;
using System.Text.RegularExpressions;

namespace IRSI.Services.AutoUpdate.Serialization
{
    public static class SerializationExtensions
    {
        public static string ToSnakeCase(this string str)
        {
            var pattern =
                new Regex(@"[A-Z]{2,}(?=[A-Z][a-z]+[0-9]*|\b)|[A-Z]?[a-z]+[0-9]*|[A-Z]|[0-9]+");

            return str == null
                ? null
                : string
                    .Join("_", pattern.Matches(str).Select(m => m.Value))
                    .ToLower();
        }
    }
}