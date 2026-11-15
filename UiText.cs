using System.Globalization;
using System.Resources;

namespace Luma;

internal static class UiText
{
    private static readonly ResourceManager Resources = new("Luma.Resources.Strings", typeof(UiText).Assembly);
    internal static string T(string text) => Resources.GetString(text, CultureInfo.CurrentUICulture) ?? text;
}
