using System;
using System.IO;
using System.Text;
using VKV;
using VKV.Compression;

namespace InkLocaliser
{
    public class VKVHandler(Localiser localiser, VKVHandler.Options options) {

        public class Options {
            public string outputFilePath = "";
            public bool compress = true;
        }

        /// <summary>
        /// Writes strings as VKV binary format.
        /// Uses the VKV library to convert strings to optimized B+Tree based key/value database.
        /// All strings are merged into a single .vkv file with multiple tables (one table per source file).
        /// The format supports:
        /// - B+Tree based efficient key-value lookup
        /// - Multiple tables in one database file
        /// - Zstandard page compression (when enabled)
        /// - Read-only embedded database format
        /// </summary>
        public async Task<bool> WriteStringsAsync() {
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

                // Create a single VKV database file with multiple tables
                var vkvFilePath = Path.Combine(options.outputFilePath, "strings.vkv");
                
                try {
                    // Create VKV database builder
                    var builder = new DatabaseBuilder
                    {
                        PageSize = 4096,
                    };

                    // Add Zstandard compression filter if enabled
                    if (options.compress) {
                        builder.AddPageFilter(x => {
                            x.AddZstandardCompression();
                        });
                        Console.WriteLine("Zstandard compression enabled");
                    }

                    // Create a table for each source file
                    // Note: Using KeyEncoding.Ascii for string keys (UTF-8 values are still supported in values)
                    foreach (var output in outputs) {
                        var tableName = Path.GetFileNameWithoutExtension(output.Key);
                        var table = builder.CreateTable(tableName, KeyEncoding.Ascii);
                        
                        // Append all key-value pairs for this table
                        foreach (var kvp in output.Value) {
                            var valueBytes = Encoding.UTF8.GetBytes(kvp.Value);
                            table.Append(kvp.Key, valueBytes);
                        }
                        
                        Console.WriteLine($"Added table '{tableName}' with {output.Value.Count} entries");
                    }

                    // Build to single file
                    await builder.BuildToFileAsync(vkvFilePath);

                    Console.WriteLine($"VKV database written: {vkvFilePath} (contains {outputs.Count} tables)");
                }
                catch (Exception ex) {
                    Console.Error.WriteLine($"Error writing VKV database {vkvFilePath}: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Error writing out VKV database: {options.outputFilePath}: " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Synchronous wrapper for WriteStringsAsync
        /// </summary>
        public bool WriteStrings() {
            return WriteStringsAsync().GetAwaiter().GetResult();
        }
    }
}
