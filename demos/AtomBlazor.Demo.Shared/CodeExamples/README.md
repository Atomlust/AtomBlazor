# CodeExamples 示例片段目录（基础设施）

组件文档页通过 `<CodeBlock Source="Charts/AreaChart/default.txt" />` 引用此处的示例片段；
片段以嵌入资源方式随程序集分发（见 csproj 的 `EmbeddedResource Include="CodeExamples\**\*.txt"`），
资源前缀为 `AtomBlazor.Demo.CodeExamples.`（源为 `.Demo.CodeExamples.`，按改名规则平移）。

外壳批只建目录与嵌入资源机制，源 200+ 个组件示例文件当时未随批；该缺口已随各文档页
补齐，现与源同名实物数量持平（1263 枚）。
