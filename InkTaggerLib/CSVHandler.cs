namespace InkLocaliser
{
    public class CSVHandler(Localiser localiser, CSVHandler.Options options) {

        public class Options {
            public string outputFilePath = "";

            // Merge into existing CSV files instead of overwriting them: keep the text that is already
            // there for existing IDs, add new IDs, drop IDs that no longer exist in the Ink.
            public bool sync = false;
        }
        
        public bool WriteStrings() {
            try {
                if (!Directory.Exists(options.outputFilePath)) Directory.CreateDirectory(options.outputFilePath);

                // Group the strings by their source Ink file, keeping the order they were found in.
                var groups = new Dictionary<string, List<(string Id, string Text)>>(StringComparer.Ordinal);
                var order = new List<string>();
                foreach (var locID in localiser.GetStringKeys()) {
                    var path = localiser.GetStringPath(locID);
                    if (!groups.TryGetValue(path, out var list)) {
                        list = [];
                        groups[path] = list;
                        order.Add(path);
                    }

                    list.Add((locID, localiser.GetString(locID)));
                }

                foreach (var path in order) {
                    var name = Path.GetFileNameWithoutExtension(path);
                    var csvPath = Path.Combine(options.outputFilePath, name + ".csv");
                    var entries = groups[path];

                    if (!options.sync) {
                        var table = new CsvStringTable();
                        foreach (var (id, text) in entries) table.Set(id, text);

                        table.Write(csvPath);
                        Console.WriteLine($"CSV file written: {csvPath}");
                        continue;
                    }

                    WriteMerged(csvPath, entries);
                }
            }
            catch (Exception ex) {
                 Console.Error.WriteLine($"Error writing out CSV file: {options.outputFilePath}" + ex.Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Write one CSV file, merging into the file that is already there.
        /// Text that is already present wins (it may be a translation), new IDs are added with the source
        /// text, and IDs that disappeared from the Ink are dropped.
        /// </summary>
        private static void WriteMerged(string csvPath, List<(string Id, string Text)> entries) {
            var existing = CsvStringTable.Read(csvPath);
            var merged = new CsvStringTable();
            var kept = 0;
            var added = 0;
            var filled = 0;

            foreach (var (id, text) in entries) {
                if (existing.TryGetValue(id, out var cell) && CsvFile.Normalise(cell).Length > 0) {
                    merged.Set(id, cell);
                    if (CsvFile.Normalise(cell) != CsvFile.Normalise(text)) kept++;
                    continue;
                }

                merged.Set(id, text);
                if (existing.TryGetValue(id, out _)) filled++;
                else added++;
            }

            var removed = 0;
            foreach (var id in existing.Order) {
                if (merged.TryGetValue(id, out _)) continue;
                removed++;
            }

            merged.Write(csvPath);
            Console.WriteLine($"CSV file merged: {csvPath} ({merged.Count} entries)");
            Console.WriteLine($"Sync summary: kept {kept} translated entries, {added} new entries, {filled} filled, {removed} removed");
        }
    }
}
