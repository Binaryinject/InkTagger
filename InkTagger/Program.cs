using System.CommandLine;
using InkLocaliser;
using DryDB.Compression;

// ----- Options -----
var retagOption = new Option<bool>("--retag")
{
    Description = "Regenerate all localisation tag IDs, rather than keep old IDs.",
};

var folderOption = new Option<string>("--folder")
{
    Description = "Root folder to scan for Ink files to localise, relative to working dir.",
    DefaultValueFactory = _ => "",
};

var filePatternOption = new Option<string>("--filePattern")
{
    Description = "File pattern for Ink files to localise.",
    DefaultValueFactory = _ => "*.ink",
};

var csvOption = new Option<string>("--csv")
{
    Description = "Path to a CSV folder to export. Default: no CSV file will be exported.",
    DefaultValueFactory = _ => "",
};

var csvSyncOption = new Option<bool>("--csv-sync")
{
    Description =
        "Merge into the CSV folder given by --csv instead of overwriting it: keep the text already stored " +
        "for existing IDs, add new IDs, drop IDs that disappeared from the Ink.",
};

var csvRootOption = new Option<string>("--csv-root")
{
    Description =
        "Export one CSV folder per language under this root: <root>/<language>/<chapter>.csv. " +
        "The Ink is parsed only once for every language, which is far cheaper than one run per language.",
    DefaultValueFactory = _ => "",
};

var csvSourceOption = new Option<string>("--csv-source")
{
    Description =
        "Source language for --csv-root. It is written with a full overwrite; every other language is " +
        "merged so existing translations survive.",
    DefaultValueFactory = _ => "",
};

var csvLanguagesOption = new Option<string>("--csv-languages")
{
    Description =
        "Comma separated languages to export with --csv-root, e.g. chinesesimplified,english,thai. " +
        "A language folder that does not exist yet is created and filled with the source text.",
    DefaultValueFactory = _ => "",
};

var csvSyncHoldOption = new Option<bool>("--csv-sync-hold")
{
    Description =
        "Safe mode for --csv-root: do not update cells that are still an untouched copy of the source text, " +
        "only report them.",
};

var syncReportOption = new Option<string>("--sync-report")
{
    Description = "Optional CSV report of everything --csv-root merged. Default: summary on stdout only.",
    DefaultValueFactory = _ => "",
};

var jsonOption = new Option<string>("--json")
{
    Description = "Path to a JSON folder to export. Default: no JSON file will be exported.",
    DefaultValueFactory = _ => "",
};

var drydbOption = new Option<string>("--drydb")
{
    Description = "Path to a DryDB (.drydb) output folder. Default: no DryDB file will be exported.",
    DefaultValueFactory = _ => "",
};

var drydbNoCompressOption = new Option<bool>("--drydb-no-compress")
{
    Description = "Disable page compression for DryDB binary files.",
};

var drydbTablePrefixOption = new Option<string>("--drydb-table-prefix")
{
    Description = "Add a prefix to all table names in the DryDB database.",
    DefaultValueFactory = _ => "",
};

var drydbCsvOption = new Option<string>("--drydb-csv")
{
    Description = "Scan a folder for CSV files and convert each to .drydb.",
    DefaultValueFactory = _ => "",
};

var drydbCsvOutOption = new Option<string>("--drydb-csv-out")
{
    Description = "Optional output folder for converted .drydb files.",
    DefaultValueFactory = _ => "",
};

var drydbSplitOption = new Option<bool>("--drydb-split")
{
    Description =
        "Write one strings_{source}.drydb per source file (chapter) instead of a single strings.drydb, " +
        "so rebuilding one chapter only rewrites that file.",
};

var drydbCsvFilterOption = new Option<string>("--drydb-csv-filter")
{
    Description =
        "Only convert CSV files whose file name matches this pattern (e.g. Chapter1.csv). " +
        "Combined with --drydb-split this rebuilds just that chapter's database.",
    DefaultValueFactory = _ => "*.csv",
};

var onlyCsvToDrydbOption = new Option<bool>("--only-csv-to-drydb")
{
    Description = "Only run CSV->.drydb conversion and exit (skip Localiser run).",
};

var rootCommand = new RootCommand("InkTagger - localise Ink files by tagging strings and exporting them.");
rootCommand.Options.Add(retagOption);
rootCommand.Options.Add(folderOption);
rootCommand.Options.Add(filePatternOption);
rootCommand.Options.Add(csvOption);
rootCommand.Options.Add(csvSyncOption);
rootCommand.Options.Add(csvRootOption);
rootCommand.Options.Add(csvSourceOption);
rootCommand.Options.Add(csvLanguagesOption);
rootCommand.Options.Add(csvSyncHoldOption);
rootCommand.Options.Add(syncReportOption);
rootCommand.Options.Add(jsonOption);
rootCommand.Options.Add(drydbOption);
rootCommand.Options.Add(drydbNoCompressOption);
rootCommand.Options.Add(drydbTablePrefixOption);
rootCommand.Options.Add(drydbCsvOption);
rootCommand.Options.Add(drydbCsvOutOption);
rootCommand.Options.Add(drydbSplitOption);
rootCommand.Options.Add(drydbCsvFilterOption);
rootCommand.Options.Add(onlyCsvToDrydbOption);

// ----- CSV -> DryDB conversion helper -----
async Task<bool> ConvertCsvFolderAsync(string inputFolder, string outputFolder, bool compress, string tablePrefix,
    bool split, string csvFilter) {
    try {
        if (!System.IO.Directory.Exists(inputFolder)) {
            Console.Error.WriteLine($"CSV input folder does not exist: {inputFolder}");
            return false;
        }

        if (!System.IO.Directory.Exists(outputFolder)) System.IO.Directory.CreateDirectory(outputFolder);

        var pattern = string.IsNullOrWhiteSpace(csvFilter) ? "*.csv" : csvFilter;
        var csvFiles = System.IO.Directory.GetFiles(inputFolder, pattern, System.IO.SearchOption.AllDirectories);
        if (csvFiles.Length == 0) {
            Console.WriteLine($"No CSV files matching '{pattern}' found.");
            return true;
        }

        // Table blocks are named after the CSV location so that a language folder becomes a table prefix:
        // csv/english/Chapter1.csv -> table "english_Chapter1", which is what the runtime looks up.
        string TableNameFor(string csvFile) {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(csvFile);
            var relativePath = System.IO.Path.GetRelativePath(inputFolder, csvFile);
            var relativeDir = System.IO.Path.GetDirectoryName(relativePath);

            var tableName = string.IsNullOrEmpty(relativeDir)
                ? fileName
                : $"{relativeDir.Replace("\\", "_").Replace("/", "_")}_{fileName}";

            return string.IsNullOrEmpty(tablePrefix) ? tableName : tablePrefix + tableName;
        }

        DryDB.DatabaseBuilder NewBuilder() {
            var newBuilder = new DryDB.DatabaseBuilder {PageSize = 4096};
            if (compress) newBuilder.AddPageFilter(x => { x.AddZstandardCompression(); });
            return newBuilder;
        }

        async Task AppendCsvTableAsync(DryDB.DatabaseBuilder builder, string csvFile) {
            var tableName = TableNameFor(csvFile);
            var table = builder.CreateTable(tableName, DryDB.KeyEncoding.Ascii);
            var entryCount = 0;

            using (var reader = new System.IO.StreamReader(csvFile)) {
                await reader.ReadLineAsync(); // Skip the header line (ID,Text).

                string? line;
                while ((line = await reader.ReadLineAsync()) != null) {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = ParseCsvLine(line);
                    if (parts.Length < 2) continue;

                    table.Append(parts[0], System.Text.Encoding.UTF8.GetBytes(parts[1]));
                    entryCount++;
                }
            }

            Console.WriteLine($"Added table '{tableName}' from {csvFile} ({entryCount} entries)");
        }

        if (compress) Console.WriteLine("Zstandard compression enabled");

        if (split) {
            // One database per source file (chapter): <out>/strings_{chapter}.drydb, holding every language
            // table of that chapter. Rebuilding one chapter therefore rewrites only that file.
            foreach (var group in csvFiles.GroupBy(f => System.IO.Path.GetFileName(f),
                         StringComparer.OrdinalIgnoreCase)) {
                var chapter = System.IO.Path.GetFileNameWithoutExtension(group.Key);
                var builder = NewBuilder();

                foreach (var csvFile in group) await AppendCsvTableAsync(builder, csvFile);

                var splitFilePath = System.IO.Path.Combine(outputFolder, $"strings_{chapter}.drydb");
                await builder.BuildToFileAsync(splitFilePath);
                Console.WriteLine($"DryDB database written: {splitFilePath} (contains {group.Count()} tables)");
            }
        }
        else {
            // Single database holding every table of every CSV that was found.
            var dryDBFilePath = System.IO.Path.Combine(outputFolder, "strings.drydb");
            var builder = NewBuilder();

            foreach (var csvFile in csvFiles) await AppendCsvTableAsync(builder, csvFile);

            await builder.BuildToFileAsync(dryDBFilePath);
            Console.WriteLine($"Converted {csvFiles.Length} CSV files to {dryDBFilePath}");
        }
    }
    catch (Exception ex) {
        Console.Error.WriteLine($"Error scanning CSV folder: {ex.Message}");
        return false;
    }

    return true;
}

// Simple CSV line parser that handles quoted fields
string[] ParseCsvLine(string line) {
    var result = new List<string>();
    var current = new System.Text.StringBuilder();
    var inQuotes = false;

    for (int i = 0; i < line.Length; i++) {
        var c = line[i];

        if (c == '"') {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') {
                current.Append('"');
                i++; // Skip next quote
            } else {
                inQuotes = !inQuotes;
            }
        } else if (c == ',' && !inQuotes) {
            result.Add(current.ToString());
            current.Clear();
        } else {
            current.Append(c);
        }
    }

    result.Add(current.ToString());
    return result.ToArray();
}

// ----- Action -----
rootCommand.SetAction(async (parseResult, cancellationToken) => {
    var options = new Localiser.Options {
        retag = parseResult.GetValue(retagOption),
        folder = parseResult.GetValue(folderOption) ?? "",
        filePattern = parseResult.GetValue(filePatternOption) ?? "*.ink",
    };
    var csvOptions = new CSVHandler.Options {
        outputFilePath = parseResult.GetValue(csvOption) ?? "",
        sync = parseResult.GetValue(csvSyncOption),
    };
    var csvRoot = parseResult.GetValue(csvRootOption) ?? "";
    var csvLanguages = parseResult.GetValue(csvLanguagesOption) ?? "";
    var jsonOptions = new JSONHandler.Options {
        outputFilePath = parseResult.GetValue(jsonOption) ?? "",
    };
    var dryDBOptions = new DryDBHandler.Options {
        outputFilePath = parseResult.GetValue(drydbOption) ?? "",
        compress = !parseResult.GetValue(drydbNoCompressOption),
        tablePrefix = parseResult.GetValue(drydbTablePrefixOption) ?? "",
    };
    var dryDBCsvInput = parseResult.GetValue(drydbCsvOption) ?? "";
    var dryDBCsvOutput = parseResult.GetValue(drydbCsvOutOption) ?? "";
    var dryDBSplit = parseResult.GetValue(drydbSplitOption);
    var dryDBCsvFilter = parseResult.GetValue(drydbCsvFilterOption) ?? "*.csv";
    var onlyCsvToDryDB = parseResult.GetValue(onlyCsvToDrydbOption);

    // If user requested only CSV->DryDB conversion, perform it now and exit.
    if (onlyCsvToDryDB) {
        if (string.IsNullOrWhiteSpace(dryDBCsvInput)) {
            Console.Error.WriteLine("--only-csv-to-drydb requires --drydb-csv=<folder> to be specified.");
            return 1;
        }

        var inputFolder = dryDBCsvInput;
        var outputFolder = string.IsNullOrWhiteSpace(dryDBCsvOutput) ? dryDBCsvInput : dryDBCsvOutput;
        if (!await ConvertCsvFolderAsync(inputFolder, outputFolder, dryDBOptions.compress, dryDBOptions.tablePrefix,
                dryDBSplit, dryDBCsvFilter)) {
            return 1;
        }
        return 0;
    }

    // ----- Parse Ink, Update Tags, Build String List -----
    var localiser = new Localiser(options);
    if (!localiser.Run()) {
        Console.Error.WriteLine("Not localised.");
        return 1;
    }
    Console.WriteLine($"Localised - found {localiser.GetStringKeys().Count} strings.");

    // ----- CSV Output -----
    if (!string.IsNullOrEmpty(csvOptions.outputFilePath)) {
        var csvHandler = new CSVHandler(localiser, csvOptions);
        if (!csvHandler.WriteStrings()) {
            Console.Error.WriteLine("Database not written.");
            return 1;
        }
    }

    // ----- One CSV folder per language -----
    if (!string.IsNullOrWhiteSpace(csvRoot)) {
        var csvRootOptions = new CsvRootHandler.Options {
            root = csvRoot,
            sourceLang = parseResult.GetValue(csvSourceOption) ?? "",
            languages = csvLanguages
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            holdPlaceholders = parseResult.GetValue(csvSyncHoldOption),
            reportPath = parseResult.GetValue(syncReportOption) ?? "",
        };

        var csvRootHandler = new CsvRootHandler(localiser, csvRootOptions);
        if (!csvRootHandler.Run()) {
            Console.Error.WriteLine("Language CSV folders not written.");
            return 1;
        }
    }

    // ----- JSON Output -----
    if (!string.IsNullOrEmpty(jsonOptions.outputFilePath)) {
        var jsonHandler = new JSONHandler(localiser, jsonOptions);
        if (!jsonHandler.WriteStrings()) {
            Console.Error.WriteLine("Database not written.");
            return 1;
        }
    }

    // ----- DryDB Binary Output -----
    if (!string.IsNullOrEmpty(dryDBOptions.outputFilePath)) {
        var dryDBHandler = new DryDBHandler(localiser, dryDBOptions);
        if (!dryDBHandler.WriteStrings()) {
            Console.Error.WriteLine("DryDB binary file not written.");
            return 1;
        }
    }

    // ----- CSV -> DryDB .drydb Conversion -----
    if (!string.IsNullOrEmpty(dryDBCsvInput)) {
        var inputFolder = dryDBCsvInput;
        var outputFolder = string.IsNullOrWhiteSpace(dryDBCsvOutput) ? dryDBCsvInput : dryDBCsvOutput;

        if (!await ConvertCsvFolderAsync(inputFolder, outputFolder, dryDBOptions.compress, dryDBOptions.tablePrefix,
                dryDBSplit, dryDBCsvFilter)) {
            return 1;
        }
    }

    return 0;
});

return rootCommand.Parse(args).Invoke();
