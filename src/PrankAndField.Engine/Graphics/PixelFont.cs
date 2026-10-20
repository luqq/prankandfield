using System.Text;

namespace PrankAndField.Engine.Graphics;

/// <summary>
/// A 5x7 arcade-style font rendered as vector rectangles (one per horizontal pixel run), so the
/// HUD keeps the 1983 look while scaling crisply. Latin letters, digits, punctuation and the
/// accented capitals needed by Spanish and Valencian are built in; other scripts (Japanese)
/// fall back to backend text.
/// </summary>
public static class PixelFont
{
    public const double CellW = 6;   // 5 px glyph + 1 px spacing
    public const double CellH = 8;   // 7 px glyph + 1 px line spacing
    public const int GlyphW = 5;
    public const int GlyphH = 7;

    private static readonly Dictionary<char, string[]> Glyphs = Build();
    private static readonly Dictionary<char, (char Base, string[] Accent, bool Below)> Accented = BuildAccents();

    public static bool CanDraw(char ch)
    {
        ch = Normalize(ch);
        return Glyphs.ContainsKey(ch) || Accented.ContainsKey(ch);
    }

    /// <summary>True if every character can be drawn with the pixel font.</summary>
    public static bool CanDraw(string text)
    {
        foreach (char ch in text) if (!CanDraw(ch)) return false;
        return true;
    }

    public static double Measure(string text, double size = 1) => text.Length == 0 ? 0 : (text.Length * CellW - 1) * size;

    private static char Normalize(char ch)
    {
        if (ch >= 'a' && ch <= 'z') return (char)(ch - 32);
        return ch switch
        {
            'á' => 'Á', 'é' => 'É', 'í' => 'Í', 'ó' => 'Ó', 'ú' => 'Ú', 'ü' => 'Ü', 'ñ' => 'Ñ', 'ç' => 'Ç',
            'à' => 'À', 'è' => 'È', 'ò' => 'Ò', 'ï' => 'Ï',
            '’' => '\'',
            _ => ch,
        };
    }

    /// <summary>
    /// Draws text with its top-left at (x, y). Text that the pixel font cannot draw is delegated to
    /// the backend with a comparable size. Returns the width used.
    /// </summary>
    public static double Draw(DrawList dl, double x, double y, string text, Rgba color, double size = 1, TextAlign align = TextAlign.Left)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (!CanDraw(text))
        {
            // Backend text: baseline roughly at the bottom of the glyph box.
            dl.Text(x, y + GlyphH * size, CellH * size, align, text, color);
            return text.Length * CellW * size;
        }

        double width = Measure(text, size);
        double startX = align switch
        {
            TextAlign.Center => x - width / 2,
            TextAlign.Right => x - width,
            _ => x,
        };

        double cx = startX;
        foreach (char raw in text)
        {
            char ch = Normalize(raw);
            if (Accented.TryGetValue(ch, out var acc))
            {
                DrawGlyph(dl, cx, y, Glyphs[acc.Base], color, size, 0);
                DrawGlyph(dl, cx, y, acc.Accent, color, size, acc.Below ? GlyphH : -acc.Accent.Length);
            }
            else if (Glyphs.TryGetValue(ch, out var g))
            {
                DrawGlyph(dl, cx, y, g, color, size, 0);
            }
            cx += CellW * size;
        }
        return width;
    }

    private static void DrawGlyph(DrawList dl, double x, double y, string[] rows, Rgba color, double size, int rowOffset)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            string row = rows[r];
            int c = 0;
            while (c < row.Length)
            {
                if (row[c] != '#') { c++; continue; }
                int start = c;
                while (c < row.Length && row[c] == '#') c++;
                dl.FillRect(x + start * size, y + (r + rowOffset) * size, (c - start) * size, size, color);
            }
        }
    }

    private static string[] G(string packed)
    {
        // rows separated by spaces, 7 rows of 5 chars
        var parts = packed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != GlyphH) throw new InvalidOperationException("Bad glyph: " + packed);
        return parts;
    }

    private static Dictionary<char, string[]> Build()
    {
        var d = new Dictionary<char, string[]>
        {
            ['A'] = G(".###. #...# #...# ##### #...# #...# #...#"),
            ['B'] = G("####. #...# #...# ####. #...# #...# ####."),
            ['C'] = G(".#### #.... #.... #.... #.... #.... .####"),
            ['D'] = G("####. #...# #...# #...# #...# #...# ####."),
            ['E'] = G("##### #.... #.... ####. #.... #.... #####"),
            ['F'] = G("##### #.... #.... ####. #.... #.... #...."),
            ['G'] = G(".#### #.... #.... #.### #...# #...# .####"),
            ['H'] = G("#...# #...# #...# ##### #...# #...# #...#"),
            ['I'] = G("##### ..#.. ..#.. ..#.. ..#.. ..#.. #####"),
            ['J'] = G("..### ...#. ...#. ...#. ...#. #..#. .##.."),
            ['K'] = G("#...# #..#. #.#.. ##... #.#.. #..#. #...#"),
            ['L'] = G("#.... #.... #.... #.... #.... #.... #####"),
            ['M'] = G("#...# ##.## #.#.# #.#.# #...# #...# #...#"),
            ['N'] = G("#...# ##..# #.#.# #..## #...# #...# #...#"),
            ['O'] = G(".###. #...# #...# #...# #...# #...# .###."),
            ['P'] = G("####. #...# #...# ####. #.... #.... #...."),
            ['Q'] = G(".###. #...# #...# #...# #.#.# #..#. .##.#"),
            ['R'] = G("####. #...# #...# ####. #.#.. #..#. #...#"),
            ['S'] = G(".#### #.... #.... .###. ....# ....# ####."),
            ['T'] = G("##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#.."),
            ['U'] = G("#...# #...# #...# #...# #...# #...# .###."),
            ['V'] = G("#...# #...# #...# #...# #...# .#.#. ..#.."),
            ['W'] = G("#...# #...# #...# #.#.# #.#.# ##.## #...#"),
            ['X'] = G("#...# #...# .#.#. ..#.. .#.#. #...# #...#"),
            ['Y'] = G("#...# #...# .#.#. ..#.. ..#.. ..#.. ..#.."),
            ['Z'] = G("##### ....# ...#. ..#.. .#... #.... #####"),
            ['0'] = G(".###. #...# #..## #.#.# ##..# #...# .###."),
            ['1'] = G("..#.. .##.. ..#.. ..#.. ..#.. ..#.. .###."),
            ['2'] = G(".###. #...# ....# ...#. ..#.. .#... #####"),
            ['3'] = G("##### ...#. ..#.. ...#. ....# #...# .###."),
            ['4'] = G("...#. ..##. .#.#. #..#. ##### ...#. ...#."),
            ['5'] = G("##### #.... ####. ....# ....# #...# .###."),
            ['6'] = G("..##. .#... #.... ####. #...# #...# .###."),
            ['7'] = G("##### ....# ...#. ..#.. .#... .#... .#..."),
            ['8'] = G(".###. #...# #...# .###. #...# #...# .###."),
            ['9'] = G(".###. #...# #...# .#### ....# ...#. .##.."),
            [' '] = G("..... ..... ..... ..... ..... ..... ....."),
            ['.'] = G("..... ..... ..... ..... ..... .##.. .##.."),
            [','] = G("..... ..... ..... ..... .##.. .##.. .#..."),
            [':'] = G("..... .##.. .##.. ..... .##.. .##.. ....."),
            [';'] = G("..... .##.. .##.. ..... .##.. .##.. .#..."),
            ['!'] = G("..#.. ..#.. ..#.. ..#.. ..#.. ..... ..#.."),
            ['?'] = G(".###. #...# ....# ...#. ..#.. ..... ..#.."),
            ['¡'] = G("..#.. ..... ..#.. ..#.. ..#.. ..#.. ..#.."),
            ['¿'] = G("..#.. ..... ..#.. .#... #.... #...# .###."),
            ['-'] = G("..... ..... ..... ##### ..... ..... ....."),
            ['+'] = G("..... ..#.. ..#.. ##### ..#.. ..#.. ....."),
            ['/'] = G("....# ...#. ...#. ..#.. .#... .#... #...."),
            ['='] = G("..... ..... ##### ..... ##### ..... ....."),
            ['&'] = G(".##.. #..#. #..#. .##.. #.#.# #..#. .##.#"),
            ['%'] = G("##..# ##..# ...#. ..#.. .#... #..## #..##"),
            ['('] = G("...#. ..#.. .#... .#... .#... ..#.. ...#."),
            [')'] = G(".#... ..#.. ...#. ...#. ...#. ..#.. .#..."),
            ['\''] = G("..#.. ..#.. ..... ..... ..... ..... ....."),
            ['"'] = G(".#.#. .#.#. ..... ..... ..... ..... ....."),
            ['°'] = G(".##.. #..#. .##.. ..... ..... ..... ....."),
            ['º'] = G(".##.. #..#. #..#. .##.. ..... .###. ....."),
            ['ª'] = G(".##.. ...#. .###. #..#. .###. ..... .###."),
            ['·'] = G("..... ..... ..... .##.. .##.. ..... ....."),
            ['*'] = G("..... #.#.# .###. ##### .###. #.#.# ....."),
            ['<'] = G("...#. ..#.. .#... #.... .#... ..#.. ...#."),
            ['>'] = G(".#... ..#.. ...#. ....# ...#. ..#.. .#..."),
            ['←'] = G("..... ..#.. .#... ##### .#... ..#.. ....."),
            ['→'] = G("..... ..#.. ...#. ##### ...#. ..#.. ....."),
            ['↑'] = G("..#.. .###. #.#.# ..#.. ..#.. ..#.. ..#.."),
            ['↓'] = G("..#.. ..#.. ..#.. ..#.. #.#.# .###. ..#.."),
            ['♥'] = G(".#.#. ##### ##### ##### .###. ..#.. ....."),
            ['●'] = G("..... .###. ##### ##### ##### .###. ....."),
            ['○'] = G("..... .###. #...# #...# #...# .###. ....."),
            ['_'] = G("..... ..... ..... ..... ..... ..... #####"),
            ['#'] = G(".#.#. .#.#. ##### .#.#. ##### .#.#. .#.#."),
            ['['] = G(".###. .#... .#... .#... .#... .#... .###."),
            [']'] = G(".###. ...#. ...#. ...#. ...#. ...#. .###."),
            ['©'] = G(".###. #.##. #.#.. #.#.. #.##. #...# .###."),
            ['~'] = G("..... ..... .#..# #.#.# #..#. ..... ....."),
        };
        return d;
    }

    private static Dictionary<char, (char, string[], bool)> BuildAccents()
    {
        string[] acute = ["...#.", "..#.."];
        string[] grave = [".#...", "..#.."];
        string[] diaeresis = [".#.#.", "....."];
        string[] tilde = [".##.#", "#.##."];
        string[] cedilla = ["..#..", ".##.."];
        return new Dictionary<char, (char, string[], bool)>
        {
            ['Á'] = ('A', acute, false),
            ['É'] = ('E', acute, false),
            ['Í'] = ('I', acute, false),
            ['Ó'] = ('O', acute, false),
            ['Ú'] = ('U', acute, false),
            ['À'] = ('A', grave, false),
            ['È'] = ('E', grave, false),
            ['Ò'] = ('O', grave, false),
            ['Ü'] = ('U', diaeresis, false),
            ['Ï'] = ('I', diaeresis, false),
            ['Ñ'] = ('N', tilde, false),
            ['Ç'] = ('C', cedilla, true),
        };
    }

    /// <summary>Upper-cases Latin text the way the arcade HUD would show it (leaves other scripts alone).</summary>
    public static string Upper(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s)
        {
            char n = Normalize(ch);
            sb.Append(n);
        }
        return sb.ToString();
    }
}
