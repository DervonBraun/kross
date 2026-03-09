using System.Text;
using System.Text.RegularExpressions;

namespace AN_
{
    /// <summary>
    /// Конвертирует упрощённый markdown в TMP Rich Text теги.
    ///
    /// Поддерживаемый синтаксис:
    ///   # Заголовок        → крупный bold (130%)
    ///   ## Подзаголовок    → средний bold (115%)
    ///   **text**           → <b>text</b>
    ///   *text*             → <i>text</i>
    ///   `code`             → моноширинный текст с цветом
    ///   - item             → • item (список)
    ///   ---                → декоративный разделитель
    ///
    /// Использование:
    ///   string richText = ANMarkdownParser.Parse(rawText);
    ///   _chatBody.SetText(richText);
    ///
    /// Настройки цветов/размеров — через статические поля ниже.
    /// </summary>
    public static class ANMarkdownParser
    {
        // ── Настройки (меняй под свой дизайн) ───────────────────────────

        /// <summary>Размер заголовка # в % от базового размера шрифта.</summary>
        public static float H1SizePercent = 130f;

        /// <summary>Размер подзаголовка ## в % от базового размера шрифта.</summary>
        public static float H2SizePercent = 115f;

        /// <summary>Hex-цвет inline кода (без #). Например: "A8D8A8" — мягкий зелёный.</summary>
        public static string CodeColor = "A8D8A8";

        /// <summary>Hex-цвет горизонтального разделителя.</summary>
        public static string SeparatorColor = "555555";

        /// <summary>Символ разделителя (повторяется SeparatorRepeat раз).</summary>
        public static string SeparatorChar   = "─";
        public static int    SeparatorRepeat = 32;

        /// <summary>Отступ перед пунктом списка.</summary>
        public static string ListIndent = "  ";

        /// <summary>Символ маркера списка.</summary>
        public static string ListBullet = "•";

        // ── Regex (compile once) ─────────────────────────────────────────

        // Порядок важен: сначала более специфичные паттерны
        private static readonly Regex _bold   = new(@"\*\*(.+?)\*\*",          RegexOptions.Compiled);
        private static readonly Regex _italic = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex _code   = new(@"`([^`]+)`",              RegexOptions.Compiled);

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        /// Парсит текст и возвращает строку с TMP rich text тегами.
        /// Безопасно вызывать с null — вернёт пустую строку.
        /// </summary>
        public static string Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var sb = new StringBuilder(input.Length * 2);

            // Разбиваем по строкам, обрабатываем каждую отдельно
            var lines = input.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string processed = ProcessLine(line);

                sb.Append(processed);

                // Добавляем перевод строки между строками (не после последней)
                if (i < lines.Length - 1)
                    sb.Append('\n');
            }

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        private static string ProcessLine(string line)
        {
            // ── Горизонтальный разделитель ────────────────────────────
            if (line.Trim() == "---")
            {
                string sep = new string(' ', 0) +
                    $"<color=#{SeparatorColor}>" +
                    RepeatString(SeparatorChar, SeparatorRepeat) +
                    "</color>";
                return sep;
            }

            // ── Заголовок H1: # Текст ─────────────────────────────────
            if (line.StartsWith("# "))
            {
                string content = line[2..].Trim();
                content = ApplyInline(content);
                return $"<size={H1SizePercent:0}%><b>{content}</b></size>";
            }

            // ── Заголовок H2: ## Текст ────────────────────────────────
            if (line.StartsWith("## "))
            {
                string content = line[3..].Trim();
                content = ApplyInline(content);
                return $"<size={H2SizePercent:0}%><b>{content}</b></size>";
            }

            // ── Пункт списка: - Текст ─────────────────────────────────
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                string content = line[2..].Trim();
                content = ApplyInline(content);
                return $"{ListIndent}{ListBullet} {content}";
            }

            // ── Обычная строка — только inline замены ─────────────────
            return ApplyInline(line);
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>Применяет inline-паттерны: bold, italic, code.</summary>
        private static string ApplyInline(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Порядок: bold → italic → code
            // (italic regex специально не захватывает **)
            text = _bold.Replace(text,   "<b>$1</b>");
            text = _italic.Replace(text, "<i>$1</i>");
            text = _code.Replace(text,   $"<color=#{CodeColor}><noparse>$1</noparse></color>");

            return text;
        }

        // ─────────────────────────────────────────────────────────────────
        private static string RepeatString(string s, int count)
        {
            var sb = new StringBuilder(s.Length * count);
            for (int i = 0; i < count; i++) sb.Append(s);
            return sb.ToString();
        }
    }
}
