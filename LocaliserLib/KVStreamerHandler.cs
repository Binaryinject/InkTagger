using System;
using System.IO;
using System.Text;
using FSTGame;

namespace InkLocaliser
{
    public class KVStreamerHandler(Localiser localiser, KVStreamerHandler.Options options) {

        public class Options {
            public string outputFilePath = "";
            public bool compress = true;
        }

        /// <summary>
        /// Writes strings as KVStreamer binary format.
        /// Uses the KVStreamer library to convert strings to optimized binary format.
        /// The format supports:
        /// - GZip compression (reduces file size by 60-70%)
        /// - Map header indexing for fast key-value lookup
        /// - Backward compatible format detection
        /// </summary>
        public bool WriteStrings() {
            try {
                if (!Directory.Exists(options.outputFilePath)) 
                    Directory.CreateDirectory(options.outputFilePath);

                var outputs = new Dictionary<string, Dictionary<string, string>>();

                // Group strings by path
                foreach (var locID in localiser.GetStringKeys()) {
                    var path = localiser.GetStringPath(locID);
                    if (!outputs.TryGetValue(path, out var output)) {
                        output = new Dictionary<string, string>();
                        outputs.Add(path, output);
                    }
                    output.Add(locID, localiser.GetString(locID));
                }

                // Write each group to a binary file using KVStreamer
                foreach (var output in outputs) {
                    var path = Path.GetFileNameWithoutExtension(output.Key);
                    var csvFileName = $"{path}_temp.csv";
                    var csvFilePath = Path.Combine(options.outputFilePath, csvFileName);
                    var binaryFilePath = Path.Combine(options.outputFilePath, $"{path}.bytes");
                    
                    try {
                        // Step 1: Generate temporary CSV file with ID and Text columns
                        using (var writer = new StreamWriter(csvFilePath, false, new UTF8Encoding(true))) {
                            writer.WriteLine("ID,Text");
                            foreach (var kvp in output.Value) {
                                var escapedValue = kvp.Value.Replace("\"", "\"\"");
                                writer.WriteLine($"{kvp.Key},\"{escapedValue}\"");
                            }
                        }

                        // Step 2: Use KVStreamer to convert CSV to binary format
                        // As of KVStreamer v1.2.0 the CreateBinaryFromCSV method is static.
                        global::FSTGame.KVStreamer.CreateBinaryFromCSV(csvFilePath, binaryFilePath, compress: options.compress);

                        Console.WriteLine($"KVStreamer file written: {binaryFilePath}");
                    }
                    finally {
                        // Clean up temporary CSV file
                        if (File.Exists(csvFilePath)) {
                            File.Delete(csvFilePath);
                        }
                    }
                }
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Error writing out KVStreamer file: {options.outputFilePath}: " + ex.Message);
                return false;
            }

            return true;
        }
    }
}
