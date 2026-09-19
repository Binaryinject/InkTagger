using System.Text;

namespace InkLocaliser {
    /// <summary>
    /// Minimal CSV helpers for the localisation CSV files.
    /// Keeps the layout InkTagger has always produced: an "ID,Text" header line, then one row per string.
    /// </summary>
    public static class CsvFile {
        public const string IdHeader = "ID";
        public const string TextHeader = "Text";

        /// <summary>
        /// Parse a CSV file into rows of fields.
        /// Handles quoted fields, escaped quotes ("") and embedded newlines.
        /// Returns an empty list when the file does not exist.
        /// </summary>
        public static List<string[]> ReadRows(string path) {
            var rows = new List<string[]>();
            if (!File.Exists(path)) return rows;

            var text = File.ReadAllText(path, Encoding.UTF8);
            var field = new StringBuilder();
            var row = new List<string>();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++) {
                var c = text[i];

                if (inQuotes) {
                    if (c == '"') {
                        if (i + 1 < text.Length && text[i + 1] == '"') {
                            field.Append('"');
                            i++; // Skip the escaped quote.
                        } else {
                            inQuotes = false;
                        }
                    } else {
                        field.Append(c);
                    }
                    continue;
                }

                switch (c) {
                    case '"' when field.Length == 0:
                        inQuotes = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Clear();
                        rows.Add(row.ToArray());
                        row.Clear();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (field.Length > 0 || row.Count > 0) {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }

        /// <summary>
        /// Write rows out as CSV. UTF-8 with BOM and CRLF line endings, matching the CSV files this
        /// pipeline has always produced, so re-running it does not rewrite every line of every file.
        /// The header is written bare (ID,Text) and data rows are written as ID,"Text".
        /// </summary>
        public static void WriteRows(string path, IReadOnlyList<string> header,
            IEnumerable<IReadOnlyList<string>> rows) {
            var sb = new StringBuilder();
            sb.Append(JoinRow(header, false));
            sb.Append("\r\n");

            foreach (var row in rows) {
                sb.Append(JoinRow(row, true));
                sb.Append("\r\n");
            }

            var fullPath = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(true));
        }

        // First column stays bare whenever it can (IDs never need quoting); with forceQuote the remaining
        // columns are always quoted, which is how "ID,Text" files have always looked.
        private static string JoinRow(IReadOnlyList<string> fields, bool forceQuote) {
            var parts = new string[fields.Count];
            for (var i = 0; i < fields.Count; i++) {
                var value = fields[i] ?? "";
                parts[i] = i == 0 || !forceQuote ? QuoteIfNeeded(value) : Quote(value);
            }

            return string.Join(",", parts);
        }

        private static string Quote(string value) {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string QuoteIfNeeded(string value) {
            if (value.Length == 0) return value;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return Quote(value);
            if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
                return Quote(value);
            return value;
        }

        /// <summary>
        /// Comparison-friendly form of a string: invisible/format-only differences (zero width characters,
        /// non-breaking spaces, CRLF, surrounding whitespace) are normalised away. Case and actual content
        /// are kept, so two genuinely different lines never compare equal.
        /// </summary>
        public static string Normalise(string? text) {
            if (string.IsNullOrEmpty(text)) return "";

            var source = text.Normalize(NormalizationForm.FormC);
            var sb = new StringBuilder(source.Length);
            foreach (var c in source) {
                switch (c) {
                    case '\u00A0': // no-break space
                    case '\u3000': // ideographic space
                        sb.Append(' ');
                        break;
                    case '\u200B': // zero width space
                    case '\u200C':
                    case '\u200D':
                    case '\uFEFF': // BOM / zero width no-break space
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString().Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }
    }

    /// <summary>
    /// An ordered "ID -> Text" table backed by a single CSV file, i.e. one localisation file.
    /// </summary>
    public sealed class CsvStringTable {
        private readonly List<string> _order = [];
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public IReadOnlyList<string> Order => _order;
        public int Count => _order.Count;

        public bool TryGetValue(string id, out string value) => _values.TryGetValue(id, out value!);

        public string GetValueOrDefault(string id) => _values.GetValueOrDefault(id) ?? "";

        /// <summary>Set a value, keeping the original insertion order for existing IDs.</summary>
        public void Set(string id, string value) {
            if (!_values.ContainsKey(id)) _order.Add(id);
            _values[id] = value;
        }

        public static CsvStringTable Read(string path) {
            var table = new CsvStringTable();

            foreach (var row in CsvFile.ReadRows(path)) {
                if (row.Length == 0) continue;

                var id = row[0].Trim();
                if (id.Length == 0) continue;

                // Header line.
                if (id.Equals(CsvFile.IdHeader, StringComparison.OrdinalIgnoreCase)) continue;

                var text = row.Length > 1 ? row[1] : "";
                table.Set(id, text);
            }

            return table;
        }

        public void Write(string path) {
            var rows = _order.Select(id => (IReadOnlyList<string>)new[] {id, _values[id]});
            CsvFile.WriteRows(path, [CsvFile.IdHeader, CsvFile.TextHeader], rows);
        }
    }
}
