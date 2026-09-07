using ImGuiNET;

namespace FezEditor.Structure;

public enum Language
{
    English,
    French,
    Italian,
    German,
    Spanish,
    Portuguese,
    Japanese,
    Korean,
    Chinese
}

public static class LanguageExtensions
{
    private static readonly Dictionary<string, Language> LanguageKeys = new()
    {
        [""] = Language.English,
        ["fr"] = Language.French,
        ["it"] = Language.Italian,
        ["de"] = Language.German,
        ["es"] = Language.Spanish,
        ["pt"] = Language.Portuguese,
        ["ja"] = Language.Japanese,
        ["ko"] = Language.Korean,
        ["zh"] = Language.Chinese
    };

    extension(Language language)
    {
        public string GetId()
        {
            return LanguageKeys.FirstOrDefault(kv => kv.Value == language).Key;
        }

        public string GetFont()
        {
            return language switch
            {
                Language.Japanese => "Fonts/NotoSansJP",
                Language.Korean => "Fonts/NotoSansKR",
                Language.Chinese => "Fonts/NotoSansTC",
                _ => "Fonts/NotoSans"
            };
        }

        public nint GetGlyphRange(ImFontAtlasPtr fonts)
        {
            return language switch
            {
                Language.Japanese => fonts.GetGlyphRangesJapanese(),
                Language.Korean => fonts.GetGlyphRangesKorean(),
                Language.Chinese => fonts.GetGlyphRangesChineseFull(),
                _ => fonts.GetGlyphRangesDefault()
            };
        }
    }
}