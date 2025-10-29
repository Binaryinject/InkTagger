using System.Text;
using System.Text.RegularExpressions;
using Ink;
using Ink.Parsed;

namespace InkLocaliser {
    public class Localiser(Localiser.Options? options = null) {
        private const string TAG_LOC = "id:";
        private const bool DEBUG_RETAG_FILES = false;

        public class Options {
            // If true, retag everything.
            public bool retag = false;

            // Root folder. If empty, uses current working dir.
            public string folder = "";

            // Files to include. Will search subfolders of the working dir.
            public string filePattern = "*.ink";
        }

        private readonly Options _options = options ?? new Options();

        protected struct TagInsert {
            public Text text;
            public string locID;
        }

        private readonly IFileHandler _fileHandler = new DefaultFileHandler();
        private bool _inkParseErrors = false;
        private readonly HashSet<string> _filesVisited = [];
        private readonly Dictionary<string, List<TagInsert>> _filesTagsToInsert = new();
        private readonly HashSet<string> _existingIDs = [];

        private readonly List<string> _stringKeys = [];
        private readonly Dictionary<string, string> _stringPaths = [];
        private readonly Dictionary<string, string> _stringValues = new();
        private string _previousCWD = "";

        public bool Run() {
            var success = true;

            // ----- Figure out which files to include -----
            List<string> inkFiles = [];

            // We'll restore this later.
            _previousCWD = Environment.CurrentDirectory;

            var folderPath = _options.folder;
            if (String.IsNullOrWhiteSpace(folderPath))
                folderPath = _previousCWD;
            folderPath = System.IO.Path.GetFullPath(folderPath);

            // Need this for InkParser to work properly with includes and such.
            Directory.SetCurrentDirectory(folderPath);

            try {
                var dir = new DirectoryInfo(folderPath);
                foreach (var file in dir.GetFiles(_options.filePattern, SearchOption.AllDirectories)) {
                    inkFiles.Add(file.FullName);
                }
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Error finding files to process: {folderPath}: " + ex.Message);
                success = false;
            }

            // ----- For each file... -----
            if (success) {
                foreach (var inkFile in inkFiles) {
                    var content = _fileHandler.LoadInkFileContents(inkFile);
                    if (content == null) {
                        success = false;
                        break;
                    }

                    var parser = new InkParser(content, inkFile, OnError, _fileHandler);

                    var story = parser.Parse();
                    if (_inkParseErrors) {
                        Console.Error.WriteLine($"Error parsing ink file.");
                        success = false;
                        break;
                    }

                    // Go through the parsed story extracting existing localised lines, and lines still to be localised...
                    if (!ProcessStory(story, inkFile)) {
                        success = false;
                        break;
                    }
                }
            }

            // If new tags need to be added, add them now.
            if (success) {
                if (!InsertTagsToFiles()) {
                    success = false;
                }
            }

            // Restore current directory.
            Directory.SetCurrentDirectory(_previousCWD);

            return success;
        }

        // List all the locIDs for every string we found, in order.
        public IList<string> GetStringKeys() {
            return _stringKeys;
        }
        
        public string GetStringPath(string locID) {
            return _stringPaths[locID];
        }

        // Return the text of a string, by locID
        public string GetString(string locID) {
            return _stringValues[locID];
        }

        private bool ProcessStory(Story story, string filePath) {
            HashSet<string> newFilesVisited = [];

            // ---- Find all the things we should localise ----
            List<Text> validTextObjects = [];
            var lastLineNumber = -1;
            foreach (var text in story.FindAll<Text>()) {
                //command Ignore.
                if (text.text.Contains('@'))
                    continue;

                // Just a newline? Ignore.
                if (text.text.Trim() == "")
                    continue;

                // If it's a tag, ignore.
                if (IsTextTag(text))
                    continue;

                // Is this inside some code? In which case we can't do anything with that.
                if (text.parent is VariableAssignment || text.parent is StringExpression) {
                    continue;
                }

                // Have we already visited this source file i.e. is it in an include we've seen before?
                // If so, skip.
                var fileID = System.IO.Path.GetFileNameWithoutExtension(text.debugMetadata.fileName);
                if (_filesVisited.Contains(fileID)) {
                    continue;
                }

                newFilesVisited.Add(fileID);

                // More than one text chunk on a line? We only deal with individual lines of stuff.
                if (lastLineNumber == text.debugMetadata.startLineNumber) {
                    Console.Error.WriteLine(
                        $"Error in file {fileID} line {lastLineNumber} - two chunks of text when localiser can only work with one per line.");
                    return false;
                }

                lastLineNumber = text.debugMetadata.startLineNumber;

                validTextObjects.Add(text);
            }

            if (newFilesVisited.Count > 0)
                _filesVisited.UnionWith(newFilesVisited);

            // ---- Scan for existing IDs ----
            // Build list of existing IDs (so we don't duplicate)
            if (!_options.retag) {
                // Don't do this if we want to retag everything.
                foreach (var text in validTextObjects) {
                    var locTag = FindLocTagID(text);
                    if (locTag != null)
                        _existingIDs.Add(locTag);
                }
            }

            // ---- Sort out IDs ----
            // Now we've got our list of text, let's iterate through looking for IDs, and create them when they're missing.
            // IDs are stored as tags in the form #id:file_knot_stitch_xxxx

            foreach (var text in validTextObjects) {
                // Does the source already have a #id: tag?
                var locID = FindLocTagID(text);

                // Skip if there's a tag and we aren't forcing a retag 
                if (locID != null && !_options.retag) {
                    // Add existing string to localisation strings.
                    AddString(locID, text.text, filePath);
                    continue;
                }

                // Generate a new ID
                var fileName = text.debugMetadata.fileName;
                var fileID = System.IO.Path.GetFileNameWithoutExtension(fileName);
                var pathPrefix = fileID + "_";
                var locPrefix = pathPrefix + MakeLocPrefix(text);
                locID = GenerateUniqueID(locPrefix);

                // Add the ID and text object to a list of things to fix up in this file.
                if (!_filesTagsToInsert.ContainsKey(fileName))
                    _filesTagsToInsert[fileName] = [];
                var insert = new TagInsert {text = text, locID = locID};
                _filesTagsToInsert[fileName].Add(insert);

                // Add new string to localisation strings.
                AddString(locID, text.text, filePath);
            }

            return true;
        }

        private void AddString(string locID, string value, string filePath) {
            if (_stringKeys.Contains(locID)) {
                Console.Error.WriteLine(
                    $"Unexpected behaviour - trying to add content for a string named {locID}, but one already exists? Have you duplicated a tag?");
                return;
            }

            // Keeping the order of strings.
            _stringKeys.Add(locID);
            _stringPaths[locID] = filePath;
            _stringValues[locID] = value.Trim();
        }

        // Go through every Ink file that needs a tag insertion, and insert!
        private bool InsertTagsToFiles() {
            foreach (var (fileName, workList) in _filesTagsToInsert) {
                if (workList.Count == 0)
                    continue;

                Console.WriteLine($"Updating IDs in file: {fileName}");

                if (!InsertTagsToFile(fileName, workList))
                    return false;
            }

            return true;
        }

        // Do the tag inserts for one specific file.
        private bool InsertTagsToFile(string fileName, List<TagInsert> workList) {
            try {
                var filePath = _fileHandler.ResolveInkFilename(fileName);
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);

                foreach (var item in workList) {
                    // Tag
                    var newTag = $"#{TAG_LOC}{item.locID}";

                    // Find out where we're supposed to do the insert.
                    var lineNumber = item.text.debugMetadata.endLineNumber - 1;
                    var oldLine = lines[lineNumber];
                    var newLine = "";

                    if (oldLine.Contains($"#{TAG_LOC}")) {
                        // Is there already a tag called #id: in there? In which case, we just want to replace that.

                        // Regex pattern to find "#id:" followed by any alphanumeric characters or underscores
                        var pattern = $@"(#{TAG_LOC})\w+";

                        // Replace the matched text
                        newLine = Regex.Replace(oldLine, pattern, $"{newTag}");
                    }
                    else {
                        // No tag, add a new one.
                        var charPos = item.text.debugMetadata.endCharacterNumber - 1;

                        // Pad between other tags or previous item
                        if (!char.IsWhiteSpace(oldLine[charPos - 1]))
                            newTag = " " + newTag;
                        if (oldLine.Length > charPos && (oldLine[charPos] == '#' || oldLine[charPos] == '/'))
                            newTag += " ";

                        newLine = oldLine.Insert(charPos, newTag);
                    }

                    lines[lineNumber] = newLine;
                }

                // Write out to the input file.
                var output = String.Join("\n", lines);
                var outputFilePath = filePath;
                if (DEBUG_RETAG_FILES) // Debug purposes, copy to a different file instead.
                    outputFilePath += ".txt";
                File.WriteAllText(outputFilePath, output, Encoding.UTF8);
                return true;
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Error replacing tags in {fileName}: " + ex.Message);
                return false;
            }
        }

        // Checking it's a tag. Is there a StartTag earlier in the parent content?        
        private bool IsTextTag(Text text) {
            var inTag = 0;
            foreach (var sibling in text.parent.content) {
                if (sibling == text)
                    break;
                if (sibling is Tag tag) {
                    if (tag.isStart)
                        inTag++;
                    else
                        inTag--;
                }
            }

            return inTag > 0;
        }

        private string? FindLocTagID(Text text) {
            var tags = GetTagsAfterText(text);
            if (tags.Count > 0) {
                foreach (var tag in tags) {
                    if (tag.StartsWith(TAG_LOC)) {
                        return tag.Substring(TAG_LOC.Length);
                    }
                }
            }

            return null;
        }

        private List<string> GetTagsAfterText(Text text) {
            var tags = new List<string>();

            var afterText = false;
            var inTag = 0;

            foreach (var sibling in text.parent.content) {
                // Have we hit the text we care about yet? If not, carry on.
                if (sibling == text) {
                    afterText = true;
                    continue;
                }

                if (!afterText)
                    continue;

                // Have we hit an end-of-line marker? If so, stop looking, no tags here.   
                if (sibling is Text text1 && text1.text == "\n")
                    break;

                // Have we found the start or end of a tag?
                if (sibling is Tag tag) {
                    if (tag.isStart)
                        inTag++;
                    else
                        inTag--;
                    continue;
                }

                // Have we hit the end of a tag? Add it to our tag list!
                if (inTag > 0 && sibling is Text sibling1) {
                    tags.Add(sibling1.text.Trim());
                }
            }

            return tags;
        }

        // Constructs a prefix from knot / stitch
        private string MakeLocPrefix(Text text) {
            var prefix = "";
            foreach (var obj in text.ancestry) {
                if (obj is Knot knot)
                    prefix += knot.name + "_";
                if (obj is Stitch stitch)
                    prefix += stitch.name + "_";
            }

            return prefix;
        }

        private string GenerateUniqueID(string locPrefix) {
            // Repeat a lot to try and get options. Should be hard to fail at this but
            // let's set a limit to stop locks.
            for (var i = 0; i < 100; i++) {
                var locID = locPrefix + GenerateID();
                if (!_existingIDs.Contains(locID)) {
                    _existingIDs.Add(locID);
                    return locID;
                }
            }

            throw new Exception("Couldn't generate a unique ID! Really unlikely. Try again!");
        }

        private static readonly Random _random = new Random();

        private static string GenerateID(int length = 4) {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var stringChars = new char[length];
            for (var i = 0; i < length; i++) {
                stringChars[i] = chars[_random.Next(chars.Length)];
            }

            return new String(stringChars);
        }

        void OnError(string message, ErrorType type) {
            _inkParseErrors = true;
            Console.Error.WriteLine("Ink Parse Error: " + message);
        }
    }
}