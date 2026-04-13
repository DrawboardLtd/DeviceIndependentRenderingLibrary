using System.Text;
using Xunit;

namespace DIR.Lib.Tests;

public class FontInspectionTests
{
    private static readonly string FontPath = Path.Combine("Fonts", "XXTIIT_Arial_subset.ttf");

    [Fact]
    public void DumpFontCmap_And_Glyphs()
    {
        if (!File.Exists(FontPath)) return;

        using var rasterizer = new FreeTypeGlyphRasterizer();
        var fontData = File.ReadAllBytes(FontPath);
        rasterizer.RegisterFontFromMemory("mem:test", fontData);

        // Try Unicode cmap for common chars
        Console.WriteLine("=== Unicode cmap lookup ===");
        foreach (var ch in "wautodesk.ABCDabcd0123456789")
        {
            var bitmap = rasterizer.RasterizeGlyph("mem:test", 24f, new Rune(ch));
            Console.WriteLine($"  U+{(int)ch:X4} '{ch}': {bitmap.Width}x{bitmap.Height}");
        }

        // Try charCode as GID
        Console.WriteLine("\n=== CharCode as GID ===");
        for (uint i = 0; i <= 70; i++)
        {
            var bitmap = rasterizer.RasterizeGlyphWithCharCode("mem:test", 24f, new Rune('?'), i, GlyphMapHint.CharCodeIsGID);
            if (bitmap.Width > 0)
                Console.WriteLine($"  GID {i}: {bitmap.Width}x{bitmap.Height}");
        }

        // Try PUA mapping via EmbeddedSubset hint (Symbol cmap + PUA offset)
        Console.WriteLine("\n=== EmbeddedSubset (Symbol PUA) ===");
        var foundGlyphs = 0;
        for (uint i = 1; i <= 20; i++)
        {
            var bitmap = rasterizer.RasterizeGlyphWithCharCode("mem:test", 24f, new Rune('?'), i, GlyphMapHint.EmbeddedSubset);
            if (bitmap.Width > 0) foundGlyphs++;
            Console.WriteLine($"  cc={i}: {bitmap.Width}x{bitmap.Height}");
        }
        // This subset font has ~10 glyphs — most charCodes should produce non-empty bitmaps
        Assert.True(foundGlyphs >= 8, $"Expected >=8 glyphs via EmbeddedSubset, got {foundGlyphs}");
    }
}
