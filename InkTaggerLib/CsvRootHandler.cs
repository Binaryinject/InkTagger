using System.Text;

namespace InkLocaliser {
    /// <summary>
    /// Exports one CSV folder per language underneath a common root:
    /// <c>&lt;root&gt;/&lt;language&gt;/&lt;chapter&gt;.csv</c>.
    ///
    /// The Ink files are parsed once and every language is written in that single run, which is much
    /// cheaper than running the tool once per language.
    ///
    /// The source language folder is a full overwrite. Every other language is merged so that human
    /// translations survive - and, unlike a plain "keep whatever is already there" merge, a cell that is
    /// still an untouched copy of the source text follows the new source text:
    ///
    ///   The baseline is the source language folder itself: it holds the source text as it was on the
    ///   previous run. A cell equal to that previous source text was never translated, while a cell that
    ///   differs from it is human content. That is what makes the two cases distinguishable without any
    ///   extra state file next to the CSVs.
    ///
    /// Rules per language cell:
    ///   - ID missing / empty cell          -> filled with the current source text (placeholder)
    ///   - cell equals the current text     -> left alone
    ///   - cell equals the previous source  -> untouched copy: updated to the current source text
    ///                                         (or only reported when placeholders are held)
    ///   - anything else                    -> human translation, always kept
    ///   - ID gone from the Ink             -> removed from the CSV, listed in the report
    /// </summary>
    public class CsvRootHandler(Localiser localiser, CsvRootHandler.Options options) {
        public class Options {
            // Root folder holding one sub-folder per language.
            public string root = "";

            // Language folder that mirrors the Ink text (full overwrite).
            public string sourceLang = "";

            // Every language to export, source language included. Empty means: source language plus
            // whatever language folders already exist under the root.
            public List<string> languages = [];

            // Safe mode: leave untouched copies alone and only report them.
            public bool holdPlaceholders = false;

            // Optional CSV report of everything that was merged. Empty means: summary on stdout only.
            public string reportPath = "";
        }

        private sealed record Entry(string Id, string Chapter, string Text);

        private sealed record ReportRow(string Language, string Action, string Chapter, string Id, string OldSource,
            string NewSource, string Current);

        private readonly List<Entry> _entries = [];
        private readonly List<string> _chapters = [];
        private readonly Dictionary<string, List<Entry>> _byChapter = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _idsByChapter = new(StringComparer.Ordinal);

        public bool Run() {
            try {
                if (string.IsNullOrWhiteSpace(options.root)) {
                    Console.Error.WriteLine("CSV root export requires --csv-root=<folder>.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(options.sourceLang)) {
                    Console.Error.WriteLine("CSV root export requires --csv-source=<language>.");
                    return false;
                }

                var root = Path.GetFullPath(options.root);
                Directory.CreateDirectory(root);

                CollectEntries();

                var languages = ResolveLanguages(root);
                Console.WriteLine(
                    $"CSV root export: {_entries.Count} string(s) in {_chapters.Count} chapter(s), source language '{options.sourceLang}', languages: {string.Join(", ", languages)}");

                // Read the source language folder BEFORE overwriting it: it is the baseline that tells an
                // untouched copy apart from a real translation.
                var previousSource = ReadSourceLanguage(root);

                WriteSourceLanguage(root);

                var report = new List<ReportRow>();
                foreach (var language in languages) {
                    if (language.Equals(options.sourceLang, StringComparison.Ordinal)) continue;
                    SyncLanguage(root, language, previousSource, report);
                }

                WriteReport(root, report);
                return true;
            } catch (Exception ex) {
                Console.Error.WriteLine("Error exporting language CSV folders: " + ex.Message);
                return false;
            }
        }

        private void CollectEntries() {
            foreach (var locID in localiser.GetStringKeys()) {
                var inkPath = localiser.GetStringPath(locID);
                var chapter = Path.GetFileNameWithoutExtension(inkPath);

                if (!_byChapter.TryGetValue(chapter, out var list)) {
                    list = [];
                    _byChapter[chapter] = list;
                    _idsByChapter[chapter] = new HashSet<string>(StringComparer.Ordinal);
                    _chapters.Add(chapter);
                }

                var entry = new Entry(locID, chapter, localiser.GetString(locID));
                _entries.Add(entry);
                list.Add(entry);
                _idsByChapter[chapter].Add(locID);
            }
        }

        private List<string> ResolveLanguages(string root) {
            var languages = new List<string>();

            void Add(string language) {
                var value = language.Trim();
                if (value.Length == 0) return;
                if (!languages.Contains(value, StringComparer.Ordinal)) languages.Add(value);
            }

            Add(options.sourceLang);
            foreach (var language in options.languages) Add(language);

            if (options.languages.Count == 0) {
                foreach (var dir in Directory.GetDirectories(root)) {
                    var name = Path.GetFileName(dir);
                    if (name.StartsWith('.')) continue;
                    Add(name);
                }
            }

            return languages;
        }

        // The source language folder as it is right now, i.e. the source text of the previous run.
        private Dictionary<string, CsvStringTable> ReadSourceLanguage(string root) {
            var previous = new Dictionary<string, CsvStringTable>(StringComparer.Ordinal);
            foreach (var chapter in _chapters) {
                var path = Path.Combine(root, options.sourceLang, chapter + ".csv");
                if (File.Exists(path)) previous[chapter] = CsvStringTable.Read(path);
            }

            return previous;
        }

        private void WriteSourceLanguage(string root) {
            var dir = Path.Combine(root, options.sourceLang);
            Directory.CreateDirectory(dir);

            foreach (var chapter in _chapters) {
                var table = new CsvStringTable();
                foreach (var entry in _byChapter[chapter]) table.Set(entry.Id, entry.Text);

                var path = Path.Combine(dir, chapter + ".csv");
                table.Write(path);
                Console.WriteLine($"CSV file written: {path} ({table.Count} entries)");
            }
        }

        private void SyncLanguage(string root, string language, Dictionary<string, CsvStringTable> previousSource,
            List<ReportRow> report) {
            var languageDir = Path.Combine(root, language);
            Directory.CreateDirectory(languageDir);

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            void Count(string action) => counts[action] = counts.GetValueOrDefault(action) + 1;

            foreach (var chapter in _chapters) {
                var path = Path.Combine(languageDir, chapter + ".csv");
                var existing = CsvStringTable.Read(path);
                var previous = previousSource.GetValueOrDefault(chapter);
                var merged = new CsvStringTable();

                foreach (var entry in _byChapter[chapter]) {
                    var hasCell = existing.TryGetValue(entry.Id, out var cell);
                    var cellNorm = CsvFile.Normalise(cell);
                    var newNorm = CsvFile.Normalise(entry.Text);
                    var previousText = previous != null && previous.TryGetValue(entry.Id, out var value)
                        ? value
                        : null;

                    if (!hasCell || cellNorm.Length == 0) {
                        // Placeholder for a brand new (or emptied) string.
                        merged.Set(entry.Id, entry.Text);
                        var action = hasCell ? "filled" : "new";
                        Count(action);
                        report.Add(new ReportRow(language, action, chapter, entry.Id, previousText ?? "",
                            entry.Text, cell ?? ""));
                        continue;
                    }

                    if (cellNorm == newNorm) {
                        merged.Set(entry.Id, cell!);
                        continue;
                    }

                    var isPlaceholder = previousText != null && CsvFile.Normalise(previousText) == cellNorm;
                    if (isPlaceholder) {
                        if (options.holdPlaceholders) {
                            merged.Set(entry.Id, cell!);
                            Count("held");
                            report.Add(new ReportRow(language, "held", chapter, entry.Id, previousText!, entry.Text,
                                cell!));
                        } else {
                            merged.Set(entry.Id, entry.Text);
                            Count("followed");
                            report.Add(new ReportRow(language, "followed", chapter, entry.Id, previousText!, entry.Text,
                                cell!));
                        }

                        continue;
                    }

                    // A human translation: keep it, always.
                    merged.Set(entry.Id, cell!);
                    if (previousText != null && CsvFile.Normalise(previousText) != newNorm) {
                        Count("kept");
                        report.Add(new ReportRow(language, "kept", chapter, entry.Id, previousText, entry.Text,
                            cell!));
                    }
                }

                // Strings that no longer exist in the Ink are dropped, matching the single-folder --csv-sync behaviour.
                foreach (var id in existing.Order) {
                    if (_idsByChapter[chapter].Contains(id)) continue;

                    Count("removed");
                    var previousText = previous != null && previous.TryGetValue(id, out var value) ? value : "";
                    report.Add(new ReportRow(language, "removed", chapter, id, previousText, "",
                        existing.GetValueOrDefault(id)));
                }

                merged.Write(path);
                Console.WriteLine($"CSV file written: {path} ({merged.Count} entries)");
            }

            var summary = counts.Count == 0
                ? "no changes"
                : string.Join(", ", counts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));
            Console.WriteLine($"Sync summary [{language}]: {summary}");
        }

        private void WriteReport(string root, List<ReportRow> report) {
            if (string.IsNullOrWhiteSpace(options.reportPath)) return;

            var rows = report.Select(r => (IReadOnlyList<string>)new[]
                {r.Language, r.Action, r.Chapter, r.Id, r.OldSource, r.NewSource, r.Current});

            CsvFile.WriteRows(options.reportPath,
                ["Language", "Action", "Chapter", "ID", "OldSource", "NewSource", "Current"], rows);
            Console.WriteLine($"Sync report: {Path.GetFullPath(options.reportPath)} ({report.Count} row(s))");
        }
    }
}
