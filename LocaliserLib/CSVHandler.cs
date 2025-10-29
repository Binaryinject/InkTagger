using System.Text;

namespace InkLocaliser
{
    public class CSVHandler(Localiser localiser, CSVHandler.Options options) {

        public class Options {
            public string outputFilePath = "";
        }
        
        public bool WriteStrings() {
            try {
                if (!Directory.Exists(options.outputFilePath)) Directory.CreateDirectory(options.outputFilePath);
                var outputs = new Dictionary<string, StringBuilder>();

                foreach(var locID in localiser.GetStringKeys()) {
                    var path = localiser.GetStringPath(locID);
                    if (!outputs.TryGetValue(path, out var output)) {
                        output = new StringBuilder();
                        output.AppendLine("ID,Text");
                        outputs.Add(path, output);
                    }
                    var textValue = localiser.GetString(locID);
                    textValue = textValue.Replace("\"", "\"\"");
                    var line = $"{locID},\"{textValue}\"";
                    output.AppendLine(line);
                }

                foreach (var output in outputs) {
                    var path = Path.GetFileNameWithoutExtension(output.Key);
                    var fileContents = output.Value.ToString();
                    File.WriteAllText($"{options.outputFilePath}\\{path}.csv", fileContents, new UTF8Encoding(false));
                    Console.WriteLine($"CSV file written: {options.outputFilePath}\\{path}.csv");
                }
            }
            catch (Exception ex) {
                 Console.Error.WriteLine($"Error writing out CSV file: {options.outputFilePath}" + ex.Message);
                return false;
            }
            return true;
        }
    }
}