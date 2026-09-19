# InkTagger

[English](README.en.md) | [中文](README.md)

**A simple tool to make it easier to localise or attach voice lines to Ink projects.**

![Tagged Ink File](docs/demo-tagged.png)

![Generated CSV File](docs/demo-csv.png)

## Contents
- [Overview](#overview)
- [Command-Line Tool](#command-line-tool)
- [Multi-Language Sync](#multi-language-sync)
- [DryDB Database](#drydb-database)
- [Limitations](#limitations)
- [Use in Development](#use-in-development)
- [The ID format](#the-id-format)
- [Releases](#releases)
- [Caveats](#caveats)
- [Under the Hood](#under-the-hood)
- [Related Projects](#related-projects)
- [Acknowledgements](#acknowledgements)
- [License and Attribution](#license-and-attribution)

## Overview

Inkle's Ink language is a great flow language for stitching together narrative-based games.

Because it's designed to mash small fragments of text together, it's not designed for localisation, or for associating lines of spoken audio to the source file.

But many studios don't use the more advanced text-manipulation features of Ink - they just use it for creating a flow of complete lines of text. It's a great solution for titles that care about branching dialogue. This means there's a problem - how do you translate each line? And how do you play the right audio for each line?

This tool takes a set of raw ink files, scans them for lines of text, and generates a localisation ID to associate with each line. It writes the ink files back out again with these IDs in the form of Ink tags at the end of each line.

This means that every line of meaningful text in the Ink file now has a unique ID attached, as a tag. That means you can use that ID for localisation or for triggering the correct audio.

Lines starting with `@` are ignored and won't get IDs. You can use this to mark command lines or metadata.

The tool also optionally exports CSV or JSON files containing the IDs and their associated text content from all the processed Ink files - which can then be used as a basis for localisation.

Each time the tool is run, it preserves the old IDs, just adding them to any newly appeared lines.

So for example, take this source file:
![Source Ink File](docs/demo-plain.png)

After the tool is run, the source file is rewritten like this:
![Tagged Ink File](docs/demo-tagged.png)

It also creates an optional CSV file like so:
![Generated CSV File](docs/demo-csv.png)

And an optional JSON file like so:
![Generated JSON File](docs/demo-json.png)

## Command-Line Tool
This is a command-line utility with a few arguments. A few simple examples:

Look for every Ink file in the `inkFiles` folder, process them for IDs, and output the data in the file `output/strings.json`:

`InkTagger.exe --folder=inkFiles/ --json=output/strings.json`

Look for every Ink file starting with `start` in the `inkFiles` folder, process them for IDs, and output the data in the file `output/strings.csv`:

`InkTagger.exe --folder=inkFiles/ --filePattern=start*.ink --csv=output/strings.csv`

### Arguments

- `--folder=<folder>`: Root folder to scan for Ink files to localise (relative to working dir). e.g. `--folder=inkFiles/`. Default is the current working dir.

- `--filePattern=<pattern>`: File pattern to match Ink files (e.g. `--filePattern=start-*.ink`). Default is `*.ink`.

- `--csv=<csvFile>`: Path to a CSV file to export all the strings to (relative to working dir). e.g. `--csv=output/strings.csv`. Default is empty (no CSV).

- `--csv-sync`: Merge into the CSV folder given by `--csv` instead of overwriting it: keep the text already stored for existing IDs, add new IDs, drop IDs that disappeared from the Ink.

- `--csv-root=<folder>`: Export one CSV folder per language under this root: `<root>/<language>/<chapter>.csv`. The Ink is parsed only once for every language, far cheaper than one run per language.

- `--csv-source=<language>`: Source language for `--csv-root`. It is written with a full overwrite and doubles as the baseline for the next run; every other language is merged so existing translations survive.

- `--csv-languages=<a,b,c>`: Every language to export with `--csv-root`, source language included, e.g. `chinesesimplified,english,thai`. A language folder that does not exist yet is created and filled with the source text. Default: scan the root for existing language folders.

- `--csv-sync-hold`: Safe mode for `--csv-root`: never update a cell that is still a copy of the source text, only report it.

- `--sync-report=<path>`: Write a CSV report of everything `--csv-root` merged (language, action, chapter, ID, old source, new source, current value). Default: summary on stdout only.

- `--json=<jsonFile>`: Path to a JSON file to export all the strings to (relative to working dir). e.g. `--json=output/strings.json`. Default is empty (no JSON).

- `--drydb=<path>`: Output folder for DryDB `.drydb` database files. When used together with the normal localisation run, the tool will generate `.drydb` artifacts from the CSV data. Files use Zstandard page compression by default. e.g. `--drydb=output/`.

- `--drydb-no-compress`: Disable Zstandard compression for DryDB binary files. Use together with `--drydb`.

- `--drydb-table-prefix=<prefix>`: Add a prefix to all table names in the DryDB database. e.g. `--drydb-table-prefix=loc_`.

- `--drydb-csv=<csvFolder>`: Instead of running the Localiser pipeline, convert all CSV files found in the specified folder into DryDB `.drydb` files. By default the tool searches folders recursively (all subdirectories). e.g. `--drydb-csv=output/`.

- `--drydb-csv-out=<outFolder>`: When using `--drydb-csv`, specify the output folder where the generated `.drydb` files will be written. If omitted, `.drydb` files are written next to their source CSV files (same folder).

- `--drydb-split`: Write one `strings_<chapter>.drydb` per CSV (chapter) instead of a single `strings.drydb`, so rebuilding one chapter only rewrites that file.

- `--drydb-csv-filter=<pattern>`: Only convert CSV files whose file name matches this pattern (e.g. `Chapter1.csv`). Combined with `--drydb-split` this rebuilds just that chapter's database. Default: `*.csv`.

- `--only-csv-to-drydb`: Run only the CSV→`.drydb` conversion and exit (skip processing Ink files). Use together with `--drydb-csv` and optionally `--drydb-csv-out`.

Notes:
- CSV discovery for `--drydb-csv` is recursive by default (the tool uses `SearchOption.AllDirectories`). If you need non-recursive behaviour, run the conversion against a single folder that contains only the CSVs you want to convert.
- `--drydb` (without `--drydb-csv`) will generate `.drydb` as part of the normal localisation run (it uses the CSV output produced from the Ink files).

- `--retag`: Regenerate all localisation tag IDs, rather than keep old IDs.

- `--help`: Show this help.

## Multi-Language Sync

### Folder layout

One sub-folder per language; the folder name is the language. Each Ink file (chapter) maps to one CSV
inside the language folder, using the same `ID,Text` layout as the normal export:

```
Assets/_StaticGroups/Ink/
├── Chapter0.ink
├── Chapter1.ink
└── csv/
    ├── chinesesimplified/     # source language: overwritten on every run, doubles as the baseline
    │   ├── Chapter0.csv
    │   └── Chapter1.csv
    ├── chinesetraditional/    # translation folders: merged only, human text is never overwritten
    │   ├── Chapter0.csv
    │   └── Chapter1.csv
    ├── english/
    ├── japanese/
    ├── korean/
    └── thai/
```

The folder name is both the language code and the DryDB table prefix, so keeping it equal to a lower
case Unity `SystemLanguage` name (`chinesesimplified`, `english`, ...) keeps everything aligned.

### Export every language in one run

```bash
InkTagger.exe --folder=./ --csv-root=./csv --csv-source=chinesesimplified \
  --csv-languages=chinesesimplified,chinesetraditional,english,japanese,korean,thai
```

For a single chapter add `--filePattern`:

```bash
InkTagger.exe --folder=./ --filePattern=Chapter1.ink --csv-root=./csv --csv-source=chinesesimplified \
  --csv-languages=chinesesimplified,chinesetraditional,english,japanese,korean,thai
```

The Ink is parsed once per call, so the cost does not multiply with the number of languages. A language
folder that does not exist yet is created and filled with placeholders.

### How a translation is told apart from an untranslated placeholder

A stable ID only proves it is still the same line, not that the cell holds human content. The baseline is
the **source language folder itself** - it holds the source text of the previous run:

| Cell content | Verdict | What happens |
| --- | --- | --- |
| missing / empty | new entry | filled with the current source text |
| equals the **current** source text | untranslated | kept in sync, no write |
| equals the **previous** source text (what the source folder had) | never translated | updated to the current source text |
| anything else | human translation | **always kept**, reported when the source changed |
| ID gone from the Ink | removed | dropped from the CSV and listed in the report |

For "你好 → 你好啊": the simplified Chinese folder becomes `你好啊`; an English cell holding `Hello` is kept,
an English cell still holding `你好` follows to `你好啊`.

Summary output:

```
Sync summary [english]: kept 15, followed 2
```

`kept` = human translations preserved, `followed` = placeholders that followed the new source text.
Other actions: `new`, `filled`, `removed`, and `held` (safe mode refused to update).

Per-entry detail:

```bash
InkTagger.exe --folder=./ --csv-root=./csv --csv-source=chinesesimplified \
  --csv-languages=chinesesimplified,english --sync-report=./sync-report.csv
```

To review before anything is updated, use the safe mode `--csv-sync-hold`: no cell is modified, and
"still a placeholder but the source changed" is only reported.

### Build the runtime database

```bash
# full rebuild: one strings_<chapter>.drydb per chapter, holding every language of that chapter
InkTagger.exe --only-csv-to-drydb --drydb-csv=./csv --drydb-csv-out=../../StreamingAssets --drydb-split

# rebuild a single chapter only
InkTagger.exe --only-csv-to-drydb --drydb-csv=./csv --drydb-csv-out=../../StreamingAssets \
  --drydb-split --drydb-csv-filter=Chapter1.csv
```

Table names come from the CSV path: `csv/english/Chapter1.csv` becomes table `english_Chapter1` inside
`strings_Chapter1.drydb`, which is exactly what the runtime looks up:

```csharp
var database = await ReadOnlyDatabase.OpenFileAsync($"{Application.streamingAssetsPath}/strings_{chapter}.drydb");
var table = database.GetTable($"{language}_{chapter}");   // e.g. english_Chapter1
var text = Encoding.UTF8.GetString(table.Get(stringID));
```

### Caveats

- **Bringing an existing table in**: if a language CSV is historical (written by an older tool after the
  source text had already changed), a cell that is neither the current source text nor a fresh copy cannot
  be recognised as a placeholder and is kept as a translation. One run establishes the baseline and later
  edits are classified correctly.
- **Translations identical to the source text** (proper nouns, numbers, shared glyphs): a cell equal to the
  source text counts as untranslated and will follow source edits. Make such a cell differ from the source
  text, or use `--csv-sync-hold`.
- **Re-translating**: clear the cell; the next run fills it with the current source text again.
- **Removal semantics**: lines dropped from the Ink are removed from every language CSV, the same as
  `--csv-sync`; rely on version control for translation history.
- When neither the source text nor a translation changed, the tool does not rewrite the file at all
  (byte-identical output), so SVN/Git sees no diff.

## DryDB Database

### What is DryDB?

DryDB is a B+Tree based key-value database format optimized for read-only embedded use. It provides an efficient way to store and retrieve localization strings at runtime, especially suitable for game engines and embedded systems.

Project repository: [hadashiA/DryDB](https://github.com/hadashiA/DryDB)

Note: this project now uses the `DryDB` package name, command-line arguments, and the `.drydb` file extension consistently.

### Why Use DryDB?

Compared to CSV or JSON formats, DryDB offers several advantages:

- **Fast Lookup**: B+Tree structure provides O(log n) lookup time complexity, much faster than linear search in CSV/JSON
- **Low Memory Footprint**: No need to load the entire file into memory; supports on-demand page loading
- **Compression Support**: Built-in Zstandard compression significantly reduces file size
- **Optimized for Read-Only**: Perfect for localization data that doesn't change at runtime

### Usage Examples

**Generate DryDB during normal localization run:**
```bash
InkTagger.exe --folder=inkFiles/ --csv=output/strings.csv --drydb=output/
```

**Convert existing CSV files to DryDB:**
```bash
InkTagger.exe --only-csv-to-drydb --drydb-csv=localization/ --drydb-csv-out=output/
```

**Generate uncompressed DryDB with table prefix:**
```bash
InkTagger.exe --folder=inkFiles/ --csv=output/strings.csv --drydb=output/ --drydb-no-compress --drydb-table-prefix=loc_
```

### DryDB File Structure

Each CSV file is converted to a corresponding `.drydb` file. The table name in the DryDB database is derived from the CSV filename (with optional prefix). At runtime, you can query the DryDB database using the localization ID as the key to retrieve the corresponding text.

## Limitations
As said above, Ink is fully capable of stitching together fragments of sentences, like so:
```
{shuffle:
- Here is a sentence <>
- Here is a different sentence <>
}
that will end up saying the same thing.

* I talked about sailing ships [] and the preponderance of seamonsters.
    -> MarineLife
* I didn't like monkeys [at all.] in any way whatsoever.
    -> MonkeyBusiness
```

This splicing of text fragments **is not supported by the Localiser**, as the Localiser is designed for two main use cases.

* **Producing strings for localisation**. It is really hard as a translator to work stitching text fragments together, as English works very differently from other languages. So if you want your game localised, text fragments are, in general, not a good idea.
* **Producing strings for audio recording**. It is almost impossible to splice together different sections of sentences for an actor to say, so again, we shouldn't be using text fragments.

If a single line contains multiple text fragments or uses `<>` glue, the tool will report an error.

## Use in Development
Develop your Ink as normal! Treat that as the 'master copy' of your game, the source of truth for the flow and your primary language content.

Use InkTagger to add IDs to your Ink file and to extract a file of the content. Get that file localised/translated as you need for your title. Remember that you can re-run InkTagger every time you alter your Ink files and everything will be updated.

At runtime, load your Ink content, and also load the appropriate JSON or CSV (which should depend on your localisation).

Use your Ink flow as normal, but when you progress the story instead of asking Ink for the text content at the current line or option, ask for the list of tags!

Look for any tag starting with #id:, parse the ID from that tag yourself, and ask your CSV or JSON file for the actual string. You can use the same ID to trigger an appropriate voice line, if you've recorded one.

In other words - during runtime, just use Ink for logic, not for content. Grab the tags from Ink, and use your external text file (or WAV filenames!) as appropriate for the relevant language.

**Pseudocode**:
```csharp
var story = new Story(storyJsonAsset);

// Load DryDB database
var dryDBPath = Path.Combine(Application.streamingAssetsPath, "strings.drydb");
var database = await ReadOnlyDatabase.OpenFileAsync(dryDBPath);
var locTable = database.GetTable("your_table_name"); // Table name derived from Ink filename

while (story.canContinue) {

    var textContent = story.Continue();

    // we Can actually IGNORE the textContent, we want the LOCALISED version, let's find it:

    // This function looks for a tag like #id:Main_Intro_45EW
    var stringID = extractIDFromTags(story.currentTags);

    // Query localized text using string key directly
    var valueBytes = locTable.Get(stringID);
    var localisedTextContent = Encoding.UTF8.GetString(valueBytes);

    // We use that localisedTextContent instead!
    DisplayTextSomehow(localisedTextContent);

    // We could also trigger some dialogue...
    PlayAnAudioFileWithID(stringID);

    // Now let's do choices
    if(story.currentChoices.Count > 0)
    {
        for (int i = 0; i < story.currentChoices.Count; ++i) {
            Choice choice = story.currentChoices [i];

            var choiceText = choice.text;
            // Again, we can IGNORE choiceText...

            var choiceStringID = extractIDFromTags(choice.tags);

            var choiceBytes = locTable.Get(choiceStringID);
            var localisedChoiceTextContent = Encoding.UTF8.GetString(choiceBytes);

            // We use that localisedChoiceTextContent instead!
            DisplayChoiceTextSomehow(localisedChoiceTextContent);

        }
    }
}
```


## The ID format

The IDs are constructed like this:

`<filename>_<knot>(_<stitch>)_<code>`

* `filename`: The root name of the Ink file this string is in.
* `knot`: The name of the containing knot this string is in.
* `stitch`: If this is inside a stitch, the name of that stitch
* `code`: A four-character random code which will be unique to this knot or knot/stitch combination.

This is mainly to make it easy during development to figure out where a line originated in the Ink files - it's fairly arbitrary, so IDs can be moved around safely without changing (even if the lookup will then be unhelpful). You can always delete an ID and let it regenerate if you want something more appropriate to the place where you've moved a line.

## Releases
You can find releases for various platforms [here](https://github.com/Binaryinject/InkTagger/releases).

There's also a Lib version if you want to be able to access it via the DLL as part of your toolchain. The DLL depends on Inkle's `ink_compiler.dll` and `ink-engine-runtime.dll`.

## Caveats
This isn't very complicated or sophisticated, so your mileage may vary!

**WARNING**: This rewrites your `.ink` files! And it might break, you never know! It's always good practice to use version control in case a process eats your content, and this is another reason why!

**Inky might not notice**: If for some reason you run this tool while Inky is open, Inky will probably not reload the rebuilt `.ink` file. Use Ctrl-R or CMD-R to reload the file Inky is working on.

## Under the Hood
Developed in .NET / C#.

The tool internally uses Inkle's **Ink Parser** to chunk up the ink file into useful tokens, then sifts through that for textual content. Be warned that this isn't tested in huge numbers of situations - if you spot any weirdness, let me know!

## Related Projects

- [InkCommandStyle](https://github.com/Binaryinject/InkCommandStyle) - VSCode extension for Ink language with:
  - Full syntax highlighting: dialogue format, choice markers, divert symbols, Knot/Stitch definitions, `@` custom commands, etc.
  - Smart navigation: Ctrl+click to jump to Knot/Stitch definitions
  - Visual debug panel: tree view of structure, choice statistics, click to jump to source
  - Story preview: interactive testing, auto-hide `@` commands, live update
  - Outline view: quick file structure navigation

## Acknowledgements
Obviously, huge thanks to [Inkle](https://www.inklestudios.com/) (and **Joseph Humfrey** in particular) for [Ink](https://www.inklestudios.com/ink/) and the ecosystem around it, it's made my life way easier.

## License and Attribution
This is licensed under the MIT license - you should find it in the root folder. If you're successfully or unsuccessfully using this tool, I'd love to hear about it!
