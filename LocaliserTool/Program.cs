using InkLocaliser;

var options = new Localiser.Options();
var csvOptions = new CSVHandler.Options();
var jsonOptions = new JSONHandler.Options();
var kvStreamerOptions = new KVStreamerHandler.Options();
var kvStreamerCsvInput = "";
var kvStreamerCsvOutput = "";
var onlyCsvToBytes = false;

// ----- Simple Args -----
foreach (var arg in args)
{
    if (arg.Equals("--retag"))
        options.retag = true;
    else if (arg.StartsWith("--folder="))
        options.folder = arg.Substring(9);
    else if (arg.StartsWith("--filePattern="))
        options.filePattern = arg.Substring(14);
    else if (arg.StartsWith("--csv="))
        csvOptions.outputFilePath = arg.Substring(6);
    else if (arg.StartsWith("--json="))
        jsonOptions.outputFilePath = arg.Substring(7);
    else if (arg.StartsWith("--bytes="))
        kvStreamerOptions.outputFilePath = arg.Substring(8);
    else if (arg.Equals("--bytes-no-compress"))
        kvStreamerOptions.compress = false;
        else if (arg.StartsWith("--bytes-csv="))
            kvStreamerCsvInput = arg.Substring(12);
        else if (arg.StartsWith("--bytes-csv-out="))
            kvStreamerCsvOutput = arg.Substring(16);
    else if (arg.Equals("--only-csv-to-bytes"))
        onlyCsvToBytes = true;
    else if (arg.Equals("--help") || arg.Equals("-h")) {
        Console.WriteLine("Ink Localiser");
        Console.WriteLine("Arguments:");
        Console.WriteLine("  --folder=<folder> - Root folder to scan for Ink files to localise, relative to working dir.");
        Console.WriteLine("                      e.g. --folder=inkFiles/");
        Console.WriteLine("                      Default is the current working dir.");
        Console.WriteLine("  --filePattern=<folder> - Root folder to scan for Ink files to localise.");
        Console.WriteLine("                           e.g. --filePattern=start-*.ink");
        Console.WriteLine("                           Default is *.ink");
        Console.WriteLine("  --csv - Path to a CSV folder to export");
        Console.WriteLine("                    Default is empty, so no CSV file will be exported.");
        Console.WriteLine("  --json - Path to a JSON folder to export");
        Console.WriteLine("                      Default is empty, so no JSON file will be exported.");
        Console.WriteLine("  --bytes - Path to a KVStreamer binary folder to export");
        Console.WriteLine("                       Default is empty, so no KVStreamer file will be exported.");
        Console.WriteLine("                       Binary files use GZip compression by default (60-70% size reduction).");
        Console.WriteLine("  --bytes-no-compress - Disable GZip compression for KVStreamer binary files.");
        Console.WriteLine("                        Use with --bytes parameter.");
            Console.WriteLine("  --bytes-csv=<folder> - Scan a folder for CSV files and convert each to .bytes");
            Console.WriteLine("                         Example: --bytes-csv=translations/csvs");
            Console.WriteLine("  --bytes-csv-out=<folder> - Optional output folder for converted .bytes files.");
        Console.WriteLine("  --only-csv-to-bytes - Only run CSV->.bytes conversion and exit (skip Localiser run).");
        Console.WriteLine("  --retag - Regenerate all localisation tag IDs, rather than keep old IDs.");
        return 0;
    }
}

// Local function to convert CSV folder to KVStreamer .bytes
bool ConvertCsvFolder(string inputFolder, string outputFolder, bool compress) {
    try {
        if (!System.IO.Directory.Exists(inputFolder)) {
            Console.Error.WriteLine($"CSV input folder does not exist: {inputFolder}");
            return false;
        }
        if (!System.IO.Directory.Exists(outputFolder)) System.IO.Directory.CreateDirectory(outputFolder);

        // Default to recursive scan through all subfolders for CSV files
        var csvFiles = System.IO.Directory.GetFiles(inputFolder, "*.csv", System.IO.SearchOption.AllDirectories);
        foreach (var csvFile in csvFiles) {
            try {
                // Preserve relative subdirectory structure when writing to the output folder.
                var relativePath = System.IO.Path.GetRelativePath(inputFolder, csvFile);
                var relativeDir = System.IO.Path.GetDirectoryName(relativePath);
                var outDirForFile = string.IsNullOrEmpty(relativeDir) ? outputFolder : System.IO.Path.Combine(outputFolder, relativeDir);
                if (!System.IO.Directory.Exists(outDirForFile)) System.IO.Directory.CreateDirectory(outDirForFile);

                var fileName = System.IO.Path.GetFileNameWithoutExtension(csvFile);
                var outPath = System.IO.Path.Combine(outDirForFile, fileName + ".bytes");

                global::FSTGame.KVStreamer.CreateBinaryFromCSV(csvFile, outPath, compress: compress);
                Console.WriteLine($"Converted CSV to .bytes: {csvFile} -> {outPath}");
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Error converting {csvFile}: {ex.Message}");
                return false;
            }
        }
    }
    catch (Exception ex) {
        Console.Error.WriteLine($"Error scanning CSV folder: {ex.Message}");
        return false;
    }

    return true;
}

// If user requested only CSV->bytes conversion, perform it now and exit.
if (onlyCsvToBytes)
{
    if (string.IsNullOrWhiteSpace(kvStreamerCsvInput)) {
        Console.Error.WriteLine("--only-csv-to-bytes requires --bytes-csv=<folder> to be specified.");
        return -1;
    }

    var inputFolder = kvStreamerCsvInput;
    var outputFolder = string.IsNullOrWhiteSpace(kvStreamerCsvOutput) ? kvStreamerCsvInput : kvStreamerCsvOutput;
    if (!ConvertCsvFolder(inputFolder, outputFolder, kvStreamerOptions.compress)) return -1;
    return 0;
}

// ----- Parse Ink, Update Tags, Build String List -----
var localiser = new Localiser(options);
if (!localiser.Run()) {
    Console.Error.WriteLine("Not localised.");
    return -1;
}
Console.WriteLine($"Localised - found {localiser.GetStringKeys().Count} strings.");

// ----- CSV Output -----
if (!string.IsNullOrEmpty(csvOptions.outputFilePath))
{
    var csvHandler = new CSVHandler(localiser, csvOptions);
    if (!csvHandler.WriteStrings()) {
        Console.Error.WriteLine("Database not written.");
        return -1;
    }
}

// ----- JSON Output -----
if (!string.IsNullOrEmpty(jsonOptions.outputFilePath))
{
    var jsonHandler = new JSONHandler(localiser, jsonOptions);
    if (!jsonHandler.WriteStrings()) {
        Console.Error.WriteLine("Database not written.");
        return -1;
    }
}

// ----- KVStreamer Binary Output -----
if (!string.IsNullOrEmpty(kvStreamerOptions.outputFilePath))
{
    var kvStreamerHandler = new KVStreamerHandler(localiser, kvStreamerOptions);
    if (!kvStreamerHandler.WriteStrings()) {
        Console.Error.WriteLine("KVStreamer binary file not written.");
        return -1;
    }
}

// ----- CSV -> KVStreamer .bytes Conversion -----
if (!string.IsNullOrEmpty(kvStreamerCsvInput))
{
    var inputFolder = kvStreamerCsvInput;
    var outputFolder = string.IsNullOrWhiteSpace(kvStreamerCsvOutput) ? kvStreamerCsvInput : kvStreamerCsvOutput;

    if (!ConvertCsvFolder(inputFolder, outputFolder, kvStreamerOptions.compress)) return -1;
}

return 0;
