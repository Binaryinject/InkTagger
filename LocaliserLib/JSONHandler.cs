using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace InkLocaliser {
    public class JSONHandler(Localiser localiser, JSONHandler.Options options) {
        public class Options {
            public string outputFilePath = "";
        }

        public bool WriteStrings() {
            try {
                if (!Directory.Exists(options.outputFilePath)) Directory.CreateDirectory(options.outputFilePath);
                var jsonOption = new JsonSerializerOptions {WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,};

                var outputs = new Dictionary<string, Dictionary<string, string>>();

                foreach (var locID in localiser.GetStringKeys()) {
                    var path = localiser.GetStringPath(locID);
                    if (!outputs.TryGetValue(path, out var output)) {
                        output = new Dictionary<string, string>();
                        outputs.Add(path, output);
                    }

                    output.Add(locID, localiser.GetString(locID));
                }

                foreach (var output in outputs) {
                    var path = Path.GetFileNameWithoutExtension(output.Key);
                    var fileContents = JsonSerializer.Serialize(output.Value, jsonOption);

                    File.WriteAllText($"{options.outputFilePath}\\{path}.json", fileContents, new UTF8Encoding(true));
                    Console.WriteLine($"CSV file written: {options.outputFilePath}\\{path}.json");
                }
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Error writing out JSON file: {options.outputFilePath}" + ex.Message);
                return false;
            }

            return true;
        }
    }
}