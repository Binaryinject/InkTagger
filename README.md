# InkTagger

[English](README.en.md) | [中文](README.md)

**一个简单的工具，帮助 Ink 项目实现本地化和语音配音。**

![Tagged Ink File](docs/demo-tagged.png)

![Generated CSV File](docs/demo-csv.png)

## 目录
- [概述](#概述)
- [命令行工具](#命令行工具)
- [多语言同步](#多语言同步)
- [DryDB 数据库](#drydb-数据库)
- [限制](#限制)
- [开发流程](#开发流程)
- [ID 格式](#id-格式)
- [发布版本](#发布版本)
- [注意事项](#注意事项)
- [技术实现](#技术实现)
- [相关项目](#相关项目)
- [致谢](#致谢)
- [许可证](#许可证)

## 概述

Inkle 的 Ink 是一门优秀的叙事脚本语言，非常适合制作分支剧情游戏。

Ink 的设计初衷是拼接文本片段，因此原生并不支持本地化，也没有将语音文件与文本行关联的机制。

但实际上，很多工作室只是用 Ink 来编写完整的对话文本，并不使用高级的文本拼接功能。这就带来了一个问题：怎么翻译这些文本？怎么播放对应的配音？

本工具会扫描 Ink 文件，为每行文本生成唯一的本地化 ID，并以标签形式写回文件末尾。这样，每行文本都有了唯一标识，可以用于本地化查询或触发对应的语音。

以 `@` 开头的行会被忽略，不会生成 ID。可以用来标记命令行或元数据。

工具还支持导出 CSV 或 JSON 文件，包含所有 ID 和对应文本，方便翻译工作。

每次运行时，工具会保留已有的 ID，只为新增的文本行生成新 ID。

举个例子，原始文件：
![Source Ink File](docs/demo-plain.png)

处理后变成：
![Tagged Ink File](docs/demo-tagged.png)

同时生成 CSV 文件：
![Generated CSV File](docs/demo-csv.png)

以及 JSON 文件：
![Generated JSON File](docs/demo-json.png)

## 命令行工具

几个简单示例：

扫描 `inkFiles` 文件夹下所有 Ink 文件，生成 ID 并导出到 `output/strings.json`：

`InkTagger.exe --folder=inkFiles/ --json=output/strings.json`

扫描 `inkFiles` 文件夹下以 `start` 开头的 Ink 文件，导出到 `output/strings.csv`：

`InkTagger.exe --folder=inkFiles/ --filePattern=start*.ink --csv=output/strings.csv`

### 参数说明

- `--folder=<folder>`：Ink 文件所在的根目录（相对路径）。默认为当前目录。

- `--filePattern=<pattern>`：文件匹配模式，如 `--filePattern=start-*.ink`。默认为 `*.ink`。

- `--csv=<csvPath>`：CSV 导出路径，如 `--csv=output/strings.csv`。不指定则不导出。

- `--csv-sync`：把结果**合并**进 `--csv` 目录里已有的 CSV，而不是整体覆盖：已存在 ID 保留原有文本、新 ID 追加、Ink 中已删除的 ID 移除。

- `--csv-root=<folder>`：多语言导出根目录，结构为 `<root>/<语言>/<章节>.csv`。指定后用**一次 Ink 解析**导出全部语言，比每种语言跑一遍快得多。

- `--csv-source=<language>`：源语言目录名（配合 `--csv-root`）。该目录整体覆盖写入，同时充当下一轮判断的基准；其余语言做合并以保留已有译文。

- `--csv-languages=<a,b,c>`：配合 `--csv-root` 要导出的全部语言（含源语言），如 `chinesesimplified,english,thai`。不存在的语言目录会自动创建并填满源文占位；不指定时自动扫描 `<root>` 下已有的语言目录。

- `--csv-sync-hold`：配合 `--csv-root` 的安全模式：不自动更新仍是源文副本的格子，只在报告里列出。

- `--sync-report=<path>`：把 `--csv-root` 的合并明细写成 CSV（语言、动作、章节、ID、旧源文、新源文、当前值）。默认只在标准输出打印摘要。

- `--json=<jsonPath>`：JSON 导出路径，如 `--json=output/strings.json`。不指定则不导出。

- `--drydb=<path>`：DryDB 数据库输出目录。配合本地化流程使用时，会基于 CSV 生成 `.drydb` 文件，默认启用 Zstandard 压缩。

- `--drydb-no-compress`：禁用 DryDB 的 Zstandard 压缩。

- `--drydb-table-prefix=<prefix>`：为 DryDB 表名添加前缀，如 `--drydb-table-prefix=loc_`。

- `--drydb-csv=<csvFolder>`：跳过 Ink 处理，直接将指定目录下的 CSV 文件转换为 DryDB。默认递归搜索子目录。

- `--drydb-csv-out=<outFolder>`：指定 DryDB 输出目录。不指定则输出到 CSV 同级目录。

- `--drydb-split`：CSV 转 DryDB 时，每个 CSV（即一个章节）单独产出一个 `strings_<章节>.drydb`，而不是合并成单个 `strings.drydb`。这样只改一章时只重建那一个文件。

- `--drydb-csv-filter=<pattern>`：只转换文件名匹配的 CSV，如 `Chapter1.csv`。配合 `--drydb-split` 可只重建那一章的数据库。默认为 `*.csv`。

- `--only-csv-to-drydb`：仅执行 CSV 转 DryDB，跳过 Ink 处理。需配合 `--drydb-csv` 使用。

说明：
- `--drydb-csv` 默认递归搜索（使用 `SearchOption.AllDirectories`）。如需非递归，请指定只包含目标 CSV 的单一目录。
- 单独使用 `--drydb`（不带 `--drydb-csv`）时，会在正常流程中基于生成的 CSV 产出 DryDB 文件。

- `--retag`：重新生成所有 ID，不保留旧 ID。

- `--help`：显示帮助。

## 多语言同步

### 目录结构

每种语言一个子目录，目录名就是语言标识；每个 Ink 文件（章节）在语言目录里对应一份 CSV，格式与普通导出一致（`ID,Text`）：

```
Assets/_StaticGroups/Ink/
├── Chapter0.ink
├── Chapter1.ink
└── csv/
    ├── chinesesimplified/     # 源语言：整目录覆盖写入，同时充当下一轮的判断基准
    │   ├── Chapter0.csv
    │   └── Chapter1.csv
    ├── chinesetraditional/    # 译文目录：只做合并，人工内容永不覆盖
    │   ├── Chapter0.csv
    │   └── Chapter1.csv
    ├── english/
    ├── japanese/
    ├── korean/
    └── thai/
```

语言目录名建议与 Unity 的 `SystemLanguage` 枚举名小写一致（如 `chinesesimplified`、`english`）；它同时也是 DryDB 的表名前缀。

### 一次导出全部语言

```bash
InkTagger.exe --folder=./ --csv-root=./csv --csv-source=chinesesimplified \
  --csv-languages=chinesesimplified,chinesetraditional,english,japanese,korean,thai
```

只处理某一章时加上 `--filePattern`：

```bash
InkTagger.exe --folder=./ --filePattern=Chapter1.ink --csv-root=./csv --csv-source=chinesesimplified \
  --csv-languages=chinesesimplified,chinesetraditional,english,japanese,korean,thai
```

每次调用只解析一遍 Ink，语言数量不会成倍增加耗时。缺失的语言目录会自动创建并填满源文占位，新增语言不需要手工建目录。

### 怎么识别「人工译文」和「没翻译的占位」

ID 稳定只能说明"还是同一行"，不能说明格子里是不是人工翻译过的内容。判断依据是**源语言目录本身**——它保存着上一次运行时的源文，也就是天然的快照基准：

| 格子里的内容 | 判定 | 处理 |
| --- | --- | --- |
| 不存在 / 为空 | 新条目 | 填入当前源文（占位） |
| 等于**当前**源文 | 未翻译 | 保持同步，不动 |
| 等于**上一版**源文（源语言目录里的旧值） | 没翻译过的占位 | 更新为当前源文 |
| 其余 | 人工译文 | **永远保留**，若源文已改动则列进报告 |
| Ink 里已经没有这个 ID | 已删除 | 从 CSV 中移除，并列进报告 |

以「你好 → 你好啊」为例：简体中文目录更新为 `你好啊`；英文格子若是 `Hello` 则保留，若是 `你好`（当初复制的占位）则跟随变成 `你好啊`。

摘要示例：

```
Sync summary [english]: kept 15, followed 2
```

含义：`kept` 保留了 15 条人工译文，`followed` 有 2 条占位跟随了新源文。除此之外还可能出现 `new`（新增）、`filled`（空格子填占位）、`removed`（Ink 中已删除）、`held`（安全模式下扣住不跟随）。

查看逐条明细：

```bash
InkTagger.exe --folder=./ --csv-root=./csv --csv-source=chinesesimplified \
  --csv-languages=chinesesimplified,english --sync-report=./sync-report.csv
```

想先审阅再让占位跟随，用安全模式 `--csv-sync-hold`：不自动改任何格子，只把"仍是占位但源文已变"的条目列进报告。

### 生成运行时数据库

```bash
# 全量：每章一个 strings_<章节>.drydb，内含该章所有语言的表
InkTagger.exe --only-csv-to-drydb --drydb-csv=./csv --drydb-csv-out=../../StreamingAssets --drydb-split

# 只重建某一章
InkTagger.exe --only-csv-to-drydb --drydb-csv=./csv --drydb-csv-out=../../StreamingAssets \
  --drydb-split --drydb-csv-filter=Chapter1.csv
```

表名规则：CSV 所在的相对目录 + 文件名，即 `csv/english/Chapter1.csv` → 表 `english_Chapter1`、文件 `strings_Chapter1.drydb`。运行时按当前语言取表即可：

```csharp
var database = await ReadOnlyDatabase.OpenFileAsync($"{Application.streamingAssetsPath}/strings_{chapter}.drydb");
var table = database.GetTable($"{language}_{chapter}");   // 如 english_Chapter1
var text = Encoding.UTF8.GetString(table.Get(stringID));
```

### 注意事项

- **首次接入**：如果语言目录里的 CSV 是历史遗留的（源文改过、但用旧版本工具跑过），那些"已经不是当前源文、又确实是没翻译的旧占位"的格子无法被自动识别，会被当作译文保留。跑一次后基准就建立好了，之后的改动都能正确跟随。
- **与源文完全相同的译文**（专有名词、数字、同形词）：内容与源文一致时会被按"未翻译"处理，源文改动时会跟随。需要锁定的内容请先让它与源文不同，或使用 `--csv-sync-hold`。
- **重新翻译**：把格子清空即可，下次运行会重新填入当前源文并视为未翻译。
- **删除语义**：Ink 中删掉的行会从所有语言的 CSV 中移除，译文不会以孤儿形式留存（与 `--csv-sync` 的行为一致），译文历史请依赖版本控制。
- 源文未改动、译文未改动时，工具**不会**重写文件内容（输出与现有文件逐字节一致），SVN/Git 不会产生无意义 diff。

## DryDB 数据库

### 简介

DryDB 是一种基于 B+Tree 的键值数据库格式，专为只读场景优化，非常适合游戏运行时的本地化数据存储。

项目地址：[hadashiA/DryDB](https://github.com/hadashiA/DryDB)

说明：当前本项目已统一使用 `DryDB` 包名、命令行参数与 `.drydb` 文件后缀。

### 优势

相比 CSV 或 JSON：

- **查询快**：B+Tree 结构，O(log n) 时间复杂度，远快于线性搜索
- **内存省**：按需加载，无需全量载入内存
- **体积小**：内置 Zstandard 压缩
- **只读优化**：专为不可变数据设计

### 使用示例

**本地化流程中同时生成 DryDB：**
```bash
InkTagger.exe --folder=inkFiles/ --csv=output/strings.csv --drydb=output/
```

**单独将 CSV 转为 DryDB：**
```bash
InkTagger.exe --only-csv-to-drydb --drydb-csv=localization/ --drydb-csv-out=output/
```

**生成无压缩的 DryDB 并添加表前缀：**
```bash
InkTagger.exe --folder=inkFiles/ --csv=output/strings.csv --drydb=output/ --drydb-no-compress --drydb-table-prefix=loc_
```

### 文件结构

每个 CSV 对应一个 `.drydb` 文件，表名取自文件名（可加前缀）。运行时用本地化 ID 作为 key 查询即可获取对应文本。

## 限制

Ink 支持文本片段拼接，例如：
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

**本工具不支持这种拼接**，原因如下：

* **本地化需求**：翻译拼接的文本片段非常困难，不同语言的语法结构差异很大。
* **配音需求**：演员无法自然地朗读拼接的句子片段。

如果单行存在多个文本片段或使用了 `<>` 拼接，工具会报错。

## 开发流程

正常开发 Ink 脚本，把它当作游戏的主文本源。

用 InkTagger 为文本添加 ID 并导出翻译文件。每次修改 Ink 后重新运行即可，已有 ID 会保留。

运行时加载 Ink 和对应语言的 JSON/CSV。推进剧情时，不要直接用 Ink 返回的文本，而是读取标签中的 ID，再从翻译文件中查询实际文本。同样的 ID 也可以用来触发对应的语音文件。

简单说：**运行时只用 Ink 做流程控制，文本内容从外部文件读取。**

**伪代码：**
```csharp
var story = new Story(storyJsonAsset);

// 加载 DryDB 数据库
var dryDBPath = Path.Combine(Application.streamingAssetsPath, "strings.drydb");
var database = await ReadOnlyDatabase.OpenFileAsync(dryDBPath);
var locTable = database.GetTable("your_table_name"); // 表名来自 Ink 文件名

while (story.canContinue) {
    story.Continue();

    // 从标签中提取 ID，如 #id:Main_Intro_45EW
    var stringID = extractIDFromTags(story.currentTags);

    // 用 ID 查询本地化文本（直接使用字符串 key）
    var valueBytes = locTable.Get(stringID);
    var localizedText = Encoding.UTF8.GetString(valueBytes);
    DisplayText(localizedText);

    // 也可以用同一个 ID 播放语音
    PlayAudio(stringID);

    // 处理选项
    foreach (var choice in story.currentChoices) {
        var choiceID = extractIDFromTags(choice.tags);
        var choiceBytes = locTable.Get(choiceID);
        var localizedChoice = Encoding.UTF8.GetString(choiceBytes);
        DisplayChoice(localizedChoice);
    }
}
```

## ID 格式

ID 结构：`<filename>_<knot>(_<stitch>)_<code>`

* `filename`：Ink 文件名
* `knot`：所在 knot 名称
* `stitch`：所在 stitch 名称（如有）
* `code`：4 位随机码，保证在当前 knot/stitch 内唯一

这种格式方便开发时定位文本来源。ID 一旦生成就不会变，即使移动了文本位置。如果想重新生成，删掉旧 ID 再运行工具即可。

## 发布版本

各平台的发布版本在[这里](https://github.com/Binaryinject/InkTagger/releases)。

也提供 Lib 版本，可作为 DLL 集成到工具链中。依赖 Inkle 的 `ink_compiler.dll` 和 `ink-engine-runtime.dll`。

## 注意事项

这个工具比较简单，可能有未覆盖的边界情况。

**警告**：工具会直接修改 `.ink` 文件！强烈建议使用版本控制。

**Inky 不会自动刷新**：如果在 Inky 打开时运行工具，需要按 Ctrl-R（Mac 上是 CMD-R）手动刷新。

## 技术实现

使用 .NET / C# 开发。

内部使用 Inkle 的 **Ink Parser** 解析文件，提取文本内容。测试覆盖有限，如果遇到问题欢迎反馈。

## 相关项目

- [InkCommandStyle](https://github.com/Binaryinject/InkCommandStyle) - VSCode 的 Ink 语法高亮插件，功能包括：
  - 完整语法高亮：对话格式、选择标记、跳转符号、Knot/Stitch 定义、`@` 自定义命令等
  - 智能跳转：Ctrl+点击跳转到 Knot/Stitch 定义
  - 可视化调试面板：树形展示结构、选项统计、点击跳转源码
  - 故事预览：交互式运行测试、自动隐藏 `@` 命令、实时更新
  - 大纲视图：快速浏览文件结构

## 致谢

感谢 [Inkle](https://www.inklestudios.com/) 和 **Joseph Humfrey** 创造了 [Ink](https://www.inklestudios.com/ink/) 这么好用的工具。

## 许可证

MIT 许可证，详见根目录。欢迎反馈使用体验！
