using QuickSType.Core;
using QuickSType.Core.Config;

namespace QuickSType.Core.Tests;

public class LanguageValidationTests
{
    [Fact]
    public void Common_languages_round_trip_through_find()
    {
        foreach (var info in Languages.Common)
        {
            Languages.Find(info.Code).ShouldNotBeNull();
            Languages.Find(info.Code)!.Code.ShouldBe(info.Code);
        }
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("En")]
    [InlineData("eN")]
    public void Find_is_case_insensitive(string code)
    {
        Languages.Find(code).ShouldNotBeNull();
        Languages.Find(code)!.Code.ShouldBe("en");
    }

    [Theory]
    [InlineData("zz_unknown")]
    [InlineData("klingon")]
    [InlineData("xx")]
    [InlineData("")]
    public void Find_returns_null_for_unknown(string code)
    {
        Languages.Find(code).ShouldBeNull();
    }

    [Fact]
    public void With_language_normalises_case_to_lowercase()
    {
        var c = new AppConfig { ActiveLanguage = "en", Languages = new() { "en" }, AutoLanguage = false };
        var c2 = c.WithLanguage("HE");
        c2.ActiveLanguage.ShouldBe("he");
        c2.Languages.ShouldContain("he");
    }

    [Fact]
    public void With_language_trims_whitespace()
    {
        var c = new AppConfig();
        c.WithLanguage("  he  ").ActiveLanguage.ShouldBe("he");
    }

    [Theory]
    [InlineData("zz_unknown")]
    [InlineData("klingon")]
    [InlineData("")]
    [InlineData("   ")]
    public void With_language_no_ops_on_unknown_or_empty(string bad)
    {
        var c = new AppConfig { ActiveLanguage = "en", Languages = new() { "en" }, AutoLanguage = false };
        var c2 = c.WithLanguage(bad);
        c2.ShouldBe(c);
    }

    [Fact]
    public void With_language_clears_auto_language_flag()
    {
        var c = new AppConfig { AutoLanguage = true };
        c.WithLanguage("he").AutoLanguage.ShouldBeFalse();
    }

    [Fact]
    public void With_language_adds_to_languages_list_if_missing()
    {
        var c = new AppConfig { Languages = new() { "en" } };
        var c2 = c.WithLanguage("ja");
        c2.Languages.ShouldContain("en");
        c2.Languages.ShouldContain("ja");
    }

    [Fact]
    public void With_language_does_not_duplicate_in_languages_list()
    {
        var c = new AppConfig { Languages = new() { "en", "he" } };
        var c2 = c.WithLanguage("he");
        c2.Languages.Count(x => x == "he").ShouldBe(1);
    }

    [Fact]
    public void With_auto_language_sets_flag_only()
    {
        var c = new AppConfig { ActiveLanguage = "en", Languages = new() { "en" }, AutoLanguage = false };
        var c2 = c.WithAutoLanguage();
        c2.AutoLanguage.ShouldBeTrue();
        c2.ActiveLanguage.ShouldBe("en");
        c2.Languages.ShouldBe(new[] { "en" });
    }

    [Fact]
    public void With_auto_language_bool_false_clears_flag()
    {
        var c = new AppConfig { AutoLanguage = true };
        c.WithAutoLanguage(false).AutoLanguage.ShouldBeFalse();
        c.WithAutoLanguage(true).AutoLanguage.ShouldBeTrue();
    }

    [Fact]
    public void With_enabled_languages_normalises_and_dedupes()
    {
        var c = new AppConfig { Languages = new() { "en" }, ActiveLanguage = "en" };
        var c2 = c.WithEnabledLanguages(new[] { " EN ", "he", "HE", "ja" });
        c2.Languages.ShouldBe(new[] { "en", "he", "ja" });
    }

    [Fact]
    public void With_enabled_languages_filters_unknown()
    {
        var c = new AppConfig();
        var c2 = c.WithEnabledLanguages(new[] { "en", "klingon", "he", "" });
        c2.Languages.ShouldBe(new[] { "en", "he" });
    }

    [Fact]
    public void With_enabled_languages_defaults_to_english_when_empty()
    {
        var c = new AppConfig { Languages = new() { "he" }, ActiveLanguage = "he" };
        var c2 = c.WithEnabledLanguages(new[] { "klingon", "  " });
        c2.Languages.ShouldBe(new[] { "en" });
        c2.ActiveLanguage.ShouldBe("en");
    }

    [Fact]
    public void With_enabled_languages_keeps_active_if_still_enabled()
    {
        var c = new AppConfig { Languages = new() { "en", "he", "ar" }, ActiveLanguage = "he" };
        var c2 = c.WithEnabledLanguages(new[] { "en", "he", "ja" });
        c2.ActiveLanguage.ShouldBe("he");
    }

    [Fact]
    public void With_enabled_languages_shifts_active_when_removed()
    {
        var c = new AppConfig { Languages = new() { "en", "he" }, ActiveLanguage = "he" };
        var c2 = c.WithEnabledLanguages(new[] { "en", "ja" });
        c2.ActiveLanguage.ShouldBe("en");
    }

    [Fact]
    public void With_enabled_languages_shifts_active_to_first_remaining()
    {
        var c = new AppConfig { Languages = new() { "en", "he" }, ActiveLanguage = "en" };
        var c2 = c.WithEnabledLanguages(new[] { "ja", "es" });
        c2.ActiveLanguage.ShouldBe("ja");
    }

    [Fact]
    public void With_enabled_languages_preserves_order()
    {
        var c = new AppConfig();
        var c2 = c.WithEnabledLanguages(new[] { "ja", "en", "he", "ar" });
        c2.Languages.ShouldBe(new[] { "ja", "en", "he", "ar" });
    }

    [Fact]
    public void With_enabled_languages_preserves_auto_language_flag()
    {
        var c = new AppConfig { AutoLanguage = true };
        var c2 = c.WithEnabledLanguages(new[] { "en", "he" });
        c2.AutoLanguage.ShouldBeTrue();
    }
}
