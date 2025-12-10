# Ink-Localiser

[English](README.md) | [中文](README.zh-CN.md)

**一个简单的工具，用于为 Ink 项目的本地化或语音配音添加标记。**

![Tagged Ink File](docs/demo-tagged.png)

![Generated CSV File](docs/demo-csv.png)

## 目录
- [概述](#概述)
- [命令行工具](#命令行工具)
- [限制](#限制)
- [开发中使用](#开发中使用)
- [ID 格式](#id-格式)
- [发布版本](#发布版本)
- [注意事项](#注意事项)
- [工作原理](#工作原理)
- [致谢](#致谢)
- [许可证和署名](#许可证和署名)

## 概述

Inkle 的 Ink 语言是一种很好的流程语言，用于将叙事游戏中的内容拼接在一起。

由于它被设计为将小文本片段拼接在一起，所以它并不是为本地化或将语音配音行与源文件相关联而设计的。

但许多工作室并不使用 Ink 的高级文本处理功能 - 他们只是用它来创建完整的文本行流程。这对于关心分支对话的游戏来说是一个很好的解决方案。这意味着存在一个问题 - 你如何翻译每一行？你如何播放适当的音频？

这个工具会扫描一组原始 ink 文件以查找文本行，并为每一行生成一个本地化 ID。它将 ink 文件写回，这些 ID 以 Ink 标签的形式出现在每一行的末尾。

这意味着 Ink 文件中的每一行有意义的文本现在都有一个唯一的 ID 附加，作为标签。这意味着你可以使用该 ID 进行本地化或触发正确的音频。

该工具还可以选择导出 CSV 或 JSON 文件，其中包含来自所有已处理 Ink 文件的 ID 及其关联的文本内容 - 然后可以用作本地化的基础。

每次运行该工具时，它都会保留旧 ID，只是将它们添加到任何新出现的行中。

例如，以这个源文件为例：
![Source Ink File](docs/demo-plain.png)

运行工具后，源文件被重写为如下所示：
![Tagged Ink File](docs/demo-tagged.png)

它还创建一个可选的 CSV 文件，如下所示：
![Generated CSV File](docs/demo-csv.png)

以及一个可选的 JSON 文件，如下所示：
![Generated JSON File](docs/demo-json.png)

## 命令行工具
这是一个具有几个参数的命令行实用程序。一些简单的示例：

在 `inkFiles` 文件夹中查找每个 Ink 文件，为其处理 ID，并将数据输出到 `output/strings.json` 文件：

`LocaliserTool.exe --folder=inkFiles/ --json=output/strings.json`

在 `inkFiles` 文件夹中查找每个以 `start` 开头的 Ink 文件，为其处理 ID，并将数据输出到 `output/strings.csv` 文件：

`LocaliserTool.exe --folder=inkFiles/ --filePattern=start*.ink --csv=output/strings.csv`

### 参数
* `--folder=<folder>`
    
    要扫描的 Ink 文件的根文件夹，相对于工作目录。
    例如 `--folder=inkFiles/`
    默认为当前工作目录。

* `--filePattern=<pattern>`

    要扫描的 Ink 文件的文件模式。
    例如 `--filePattern=start-*.ink`
    默认为 `*.ink`

* `--csv=<csvPath>`

    导出 CSV 文件的路径（包含所有字符串），相对于工作目录。
    例如 `--csv=output/strings.csv`
    默认为空，因此不会导出 CSV 文件。

* `--json=<jsonPath>`

    导出 JSON 文件的路径（包含所有字符串），相对于工作目录。
    例如 `--json=output/strings.json`
    默认为空，因此不会导出 JSON 文件。

* `--bytes=<path>`

    导出 KVStreamer 二进制格式文件的路径。
    文件使用 GZip 压缩（默认启用，可减少 60-70% 的文件大小）。
    例如 `--bytes=output/`
    默认为空，因此不会导出二进制文件。

* `--bytes-no-compress`

    禁用 KVStreamer 二进制文件的 GZip 压缩。
    与 `--bytes` 参数一起使用。

* `--retag`

    重新生成所有本地化标签 ID，而不是保留旧 ID。

* `--help`

    显示帮助信息。

## 限制
如上所述，Ink 完全有能力将句子片段拼接在一起，例如：
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

文本片段的拼接**不被 Localiser 支持**，因为 Localiser 是为两个主要使用场景设计的。

* **为本地化生成字符串**。作为翻译者来拼接文本片段非常困难，因为英语与其他语言的工作方式非常不同。所以如果你想让你的游戏本地化，通常文本片段不是一个好主意。
* **为音频录制生成字符串**。几乎不可能为演员拼接不同部分的句子来说，所以我们也不应该使用文本片段。

Ink 仍然非常强大，我们将其用于许多其他流程用例。但出于这些原因，如果单行上有多个文本片段，Localiser 将给出错误。

（对于 `<>` 也应该给出错误，但我还没有时间添加该行为。）

## 开发中使用
照常开发你的 Ink！将其视为你游戏的"主副本"，流程和主要语言内容的来源。

使用 LocaliserTool 为你的 Ink 文件添加 ID 并提取内容文件。根据需要为你的游戏翻译该文件。请记住，每次更改 Ink 文件时，你都可以重新运行 LocaliserTool，一切都将被更新。

在运行时，加载你的 Ink 内容，并加载相应的 JSON 或 CSV（应取决于你的本地化）。

照常使用你的 Ink 流程，但当你进行故事时，不要向 Ink 请求当前行或选项的文本内容，而是请求标签列表！

查找以 `#id:` 开头的任何标签，自己解析该标签中的 ID，并向 CSV 或 JSON 文件请求实际字符串。你可以使用相同的 ID 触发适当的语音行（如果你已录制）。

换句话说 - 在运行时，仅使用 Ink 进行逻辑，不涉及内容。从 Ink 获取标签，并根据相关语言适当使用外部文本文件（或 WAV 文件名！）。

**伪代码**：
```
var story = new Story(storyJsonAsset);
var stringTable = new StringTable(tableCSVAsset);

while (story.canContinue) {

    var textContent = story.Continue();
    
    // 我们实际上可以忽略 textContent，我们需要本地化版本，让我们找到它：

    // 这个函数查找像 #id:Main_Intro_45EW 这样的标签
    var stringID = extractIDFromTags(story.currentTags);

    var localisedTextContent = stringTable.GetStringByID(stringID);

    // 我们改用那个 localisedTextContent！
    DisplayTextSomehow(localisedTextContent);

    // 我们也可以触发一些对话...
    PlayAnAudioFileWithID(stringID);

    // 现在让我们选择选项
    if(story.currentChoices.Count > 0)
    {
        for (int i = 0; i < story.currentChoices.Count; ++i) {
            Choice choice = story.currentChoices [i];

            var choiceText = choice.text;
            // 同样，我们可以忽略 choiceText...

            var choiceStringID = extractIDFromTags(choice.tags);

            var localisedChoiceTextContent = stringTable.GetStringByID(choiceStringID);

            // 我们改用那个 localisedChoiceTextContent！
            DisplayChoiceTextSomehow(localisedChoiceTextContent);

        }
    }
}
```

## ID 格式

ID 的构造如下：

`<filename>_<knot>(_<stitch>)_<code>`

* `filename`：此字符串所在 Ink 文件的根名称。
* `knot`：包含此字符串的 knot 的名称。
* `stitch`：如果这在一个 stitch 中，该 stitch 的名称
* `code`：一个四字符随机代码，将对此 knot 或 knot/stitch 组合唯一。

这主要是为了在开发过程中轻松确定一行源自 Ink 文件中的哪个位置 - 它相当任意，所以可以安全地移动 ID 而不会更改（即使查找随后会没有帮助）。如果你想要一些更适合你移动一行位置的东西，你总是可以删除一个 ID 并让它重新生成。

## 发布版本
你可以在[这里](https://github.com/Binaryinject/Ink-Localiser/releases)找到各种平台的发布版本。

如果你想能够将其作为工具链的一部分通过 DLL 访问，也有一个 Lib 版本。DLL 依赖于 Inkle 的 `ink_compiler.dll` 和 `ink-engine-runtime.dll`。

## 注意事项
这不是很复杂或复杂，所以你的里程可能会有所不同！

**警告**：这将重写你的 `.ink` 文件！并且它可能会破坏，你永远不知道！最好使用版本控制以防万一某个过程吞掉你的内容，这是另一个原因！

**Inky 可能不会注意到**：如果由于某种原因你在 Inky 打开时运行此工具，Inky 可能不会重新加载重建的 `.ink` 文件。使用 Ctrl-R 或 CMD-R 重新加载 Inky 正在处理的文件。

## 工作原理
使用 .NET / C# 开发。

该工具在内部使用 Inkle 的 **Ink Parser** 将 ink 文件分块成有用的令牌，然后搜索文本内容。请注意，这在大量情况下未经过测试 - 如果你发现任何奇怪的地方，请告诉我！

## 致谢
显然，非常感谢 [Inkle](https://www.inklestudios.com/)（特别是 **Joseph Humfrey**）为 [Ink](https://www.inklestudios.com/ink/) 和围绕它的生态系统，它让我的生活变得轻松得多。

## 许可证和署名
这是在 MIT 许可证下授权的 - 你应该在根文件夹中找到它。如果你成功或不成功地使用此工具，我很乐意听到！

你可以在[这里](https://wildwinter.medium.com/)找到我的 Medium。
