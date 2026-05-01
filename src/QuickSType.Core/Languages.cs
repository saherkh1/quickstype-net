namespace QuickSType.Core;

public static class Languages
{
    public sealed record LanguageInfo(string Code, string DisplayName, string NativeName);

    public static readonly IReadOnlyList<LanguageInfo> Common = new LanguageInfo[]
    {
        new("en", "English",        "English"),
        new("he", "Hebrew",         "עברית"),
        new("ar", "Arabic",         "العربية"),
        new("es", "Spanish",        "Español"),
        new("fr", "French",         "Français"),
        new("de", "German",         "Deutsch"),
        new("it", "Italian",        "Italiano"),
        new("pt", "Portuguese",     "Português"),
        new("ru", "Russian",        "Русский"),
        new("ja", "Japanese",       "日本語"),
        new("ko", "Korean",         "한국어"),
        new("zh", "Chinese",        "中文"),
        new("nl", "Dutch",          "Nederlands"),
        new("pl", "Polish",         "Polski"),
        new("tr", "Turkish",        "Türkçe"),
        new("hi", "Hindi",          "हिन्दी"),
    };

    public static LanguageInfo? Find(string code) =>
        Common.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    public static string DisplayFor(string code) =>
        Find(code) is { } info ? info.DisplayName : code.ToUpperInvariant();
}
