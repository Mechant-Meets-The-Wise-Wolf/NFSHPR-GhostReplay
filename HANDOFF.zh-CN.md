# HPRGhostReplay 开发交接

更新时间：2026-10-21（中国时区）。本文件供没有前序对话的新开发助手接手。它是阅读入口，不是玩家说明书，也不能代替实际代码核对。

## 1. 下一位助手先做什么

1. 把工作目录设为 `E:\DevTools-LoushShinny\Projects\HPRGhostReplay`，不要再到游戏目录寻找开发源码。该目录已是 git 仓库，`main` 上的标签 `v0.1` 是可运行 0.1 测试版首次构建的存档点；动手前先确认工作区干净。
2. 读本文件，然后按第4节顺序读 `src/` 下的当前源码；核对版本、运行中的程序、已安装插件和用户最新问题。
3. 以当前代码、实际录像和新的采样为依据独立判断。旧报告只是证据线索，不要直接继承旧诊断结论。
4. 当前没有用户指定的未完成新功能。迁移、清理、源码审阅包都已完成；先理解项目，再根据用户下一项要求开发。
5. 未经用户明确要求，不要启动或重启游戏、重新安装插件、恢复其他模组，也不要为了“验证”要求用户重复跑完整场。

## 2. 项目目标与现状

这是《Need for Speed Hot Pursuit Remastered》的赛事幽灵录制与回放工具。自动识别赛事、发车及完赛，保存完整赛程中的车辆位置、方向、车型和时间；用户载入录像并进入对应赛事后，自动同步播放半透明实际车型。录像可分享。

当前结构是 **C# x64 主工具 + 游戏进程内 D3D11 原生绘制 ASI**。主工具界面使用 WinForms。WPF 仍用于模型数据对象及开发检查，但发行版不创建 WPF 透明覆盖窗口。

原生回放在游戏 Present 中绘制额外模型，不创建可碰撞的游戏车辆实体，不参与AI或物理。不要称它为“把原生车辆实体塞进游戏”。

没有时间差HUD、行车路径、教学提示，也没有要求新增这些功能。当前没有额外播放平滑补偿，不要重新加回旧的“平滑车影”开关来掩盖时序问题。

## 3. 路径、版本和安装边界

| 用途 | 当前位置 |
| --- | --- |
| 唯一活动开发根目录 | `E:\DevTools-LoushShinny\Projects\HPRGhostReplay` |
| 实际游戏目录 | `G:\SteamLibrary\steamapps\common\Need For Speed Hot Pursuit Remastered` |
| 用户日常使用版 | `C:\Users\LoughShinny\Desktop\HPRGhostReplay` |
| 正式发行包 | 项目内 `releases/` |
| 定向源码审阅包 | 项目内 `source-share/` |
| 项目本地录像 | `app/Recordings/`（Windows下与代码中的Recordings大小写等价） |
| 研究与历史备份 | `research/` |
| 游戏根目录的旧启动脚本、说明、清单 | `archive/game-root/`，仅归档 |
| 迁移报告与文件摘要 | `archive/migration-20261020/` |
| 最近清理记录 | `archive/cleanup-20261020-235426/` |

逐目录的"本地 / 仓库 / 发行包"归属、可再生成性，见 `docs/文件索引.md`；三层文件的关系与校验方式见 `docs/版本与产物.md`。

产品版本固定为 **0.1**，状态为测试版，1.0 之前不称正式版，也不再使用 beta 后缀；当前为 0.1 首次构建，构建序号 1。版本号以根目录 `VERSION` 为唯一来源；`AppPaths.cs / AppInfo` 已同步为 `0.1` 与文件版本 `0.1.0.1`，改版本时两处必须一起改，不要只改文档。

游戏目录中的本项目开发目录和旧入口已清理。只保留本项目运行所需的：

- `HPRGhostNative.asi`：本项目绘制组件。
- `dinput8.dll`：官方 Ultimate ASI Loader x64。
- `HPRGhost-loader.sha256`：自动安装的加载器归属标记，卸载需要它；不是遗留开发垃圾。

游戏中的街车工具、赛事补丁、延迟修复、其他模组和共享研究目录属于其他项目，不要改动。`C:\mnt\g\...` 不是可用路径。E盘只是开发位置独立，运行时仍需读取使用者本机游戏资源。

主工具每次运行以自己的EXE所在目录存放Recordings、配置和错误报告。本项目的 `app/` 副本与桌面使用版是独立实例目录：不要把其中一个的录像数量、配置或日志当成另一个的状态。

可运行本体集中在 `app/`：正式 `HPRGhostReplay.exe`、启动器 `StartHPRGhost.cmd` 和运行时目录 `Recordings/`。旧的 `-next`、过时 `-dev`、重复 `-release` 已删除，需要开发版请重新构建到 `app/HPRGhostReplay-dev.exe`。

## 4. 建议阅读顺序与代码地图

顶层 `.cs` 已全部移入 `src/`，下表文件名均指 `src/` 下的文件；数据表在 `data/`，打包源文档在 `packaging/`，历史归档在 `archive/`。

| 文件 | 职责及阅读重点 |
| --- | --- |
| `Program.cs` | Release/Development入口、进程互斥、异常处理。正常打开会启动界面；发行版拒绝开发命令。 |
| `AppPaths.cs`、`SteamGameDiscovery.cs` | 版本、用户数据位置、运行中游戏优先、已保存路径、Steam多库自动发现。 |
| `ControlWindow.cs` | 启动准备、录制/回放开关、16ms调度、录像库动作、安装完成提示、卸载入口。 |
| `UiControls.cs`、`UiText.cs` | 卡片、自绘按钮、中英文。`IsLit`与`IsRunning`含义不同。 |
| `GameReader.cs`、`GameLayout.cs` | 进程只读读取、车辆/相机/时钟/赛事结构、已知布局的运行时代码锚点。 |
| `FrameSampling.cs`、`Timing.cs` | 高精度采样泵、FrameFence、快照一致性。修改前先理解物理更新与时钟发布顺序。 |
| `Lifecycle.cs` | 起跑前缓存、整场录制、完赛落盘、重开丢弃、赛事匹配。 |
| `Core.cs` | 姿态和四元数插值、Replay.At、录像格式与校验。 |
| `ReplayLibrary.cs` | 文件命名、导入、重命名、删除；用户文件保护边界。 |
| `ModelResources.cs` | 从本机VEHICLES和SHADERS.BNDL读取实际车型，处理车体/轮胎分件变换。 |
| `NativePlayback.cs` | 模型导出、内嵌ASI安装、共享内存发布、修订号/令牌/心跳、回放控制。 |
| `native/src/Channel.hpp` | 有界载荷校验、资源切换、所有者租期。 |
| `native/src/NativeGhost.cpp` | 布局读取、原生姿态插值、Present/ResizeBuffers钩子、自身深度预绘制与透明绘制。 |
| `AsiLoader.cs`、`ComponentDialog.cs` | 官方加载器内嵌安装、未知文件保护、归属标记、卸载及默认保留共享加载器。 |
| `Build.ps1`、`native/Build.ps1`、`Package.ps1` | 实际编译单元、嵌入资源、原生编译工具和发行白名单。 |

`ModelOverlay.cs`、DevelopmentTools/各Diagnostics/Tests只属于Development构建。文件还存在不代表发行版还在用旧覆盖窗口。编译单元以Build.ps1为准。

## 5. 当前产品行为：不要回退

- 录制和回放每次启动默认关闭；不是最早版本的“自动录制默认开启”。开启录制后才自动识别整场。
- 两个模块可独立开启、同时运行。开启后等待、就绪和运行都亮起；关闭或回放未导入时灰色。
- `ActivityCard.IsLit`驱动视觉启用态；`IsRunning`保留真实采集/绘制状态和日志用途，不要混用。
- 启动不自动选择录像；未载入显示“未导入”。手动“载入回放”会开启该份录像回放。
- “载入回放”在录像库标题旁，与导入、刷新和打开文件夹同一工具栏；不要放回列表下面。
- UI采用简洁Windows应用风格。字号适中、列表条目紧凑，不要加蓝色装饰描边、繁复效果或无用内部ID。
- 时间显示 `分:秒.厘秒`；显示的是录像轨迹时长，不把未经结算页核实的内部字段当成正式成绩。
- 新文件默认命名：`赛事-车辆-用时（分秒厘秒）-记录人.hprghost`；默认记录人LoughShinny302可编辑。
- 中英文切换不重置录制、回放或选中项，用户文件名作为原文数据，不参与翻译。

### 启动自动安装与卸载

正常主界面Shown时 `PreparePlayback()` 自动定位并准备组件，不等用户点回放。

1. 找运行中的游戏；其次已保存路径和程序所在目录的祖先；最后按Steam注册位置、libraryfolders.vdf和1328660安装清单寻找。
2. 找不到时提示用户选择游戏目录内的 `NFS11Remastered.exe`。
3. 缺失组件直接写入。若游戏已经运行，完成后提醒重启；已有旧原生组件占用中则等待游戏退出再更新。
4. 实际安装/更新完成才弹窗：“已经在游戏目录安装必备插件。”，再说明通过下方“回放组件”卸载。已有匹配文件不反复弹完成通知。
5. 加载器官方9.7.4 x64内嵌在EXE里，离线安装。已识别的现有x64加载器直接复用，不强行升级；未知同名DLL不覆盖。
6. “回放组件”里的卸载必须先退出游戏；默认只移除本项目可识别的未修改插件，共享加载器默认保留。
7. 只有本工具安装、归属收据匹配且内容未改变的加载器才能勾选移除；已有或改动文件保留。
8. 卸载写入EXE旁 `PlaybackDisabled.txt`，重启工具也不装回；用户主动载入录像才恢复。保留该配置。

安装、卸载和运行状态分别由代码维护，不要仅凭文件存在就显示“正在回放”。

## 6. 数据路径与时间问题

### 录制链路

`GameReader.TryReadSnapshot → FramePump/FrameFence → ControlWindow.Tick批量消费 → RaceLifecycle → WholeRaceCapture → ReplayLibrary.Save`。

`GameReader`外部进程句柄只申请查询和读取权限。FrameFence对同一模拟tick持续更新候选，满足稳定观察后在后一个tick发布前一候选，避免把更新前的姿态配到新时钟。这不等于对位置做平均或平滑。

代码中存在多个频率：采样等待器目标约1ms、界面调度16ms、`WholeRaceCapture.Push`普通存点间隔门槛30,000微秒、原生回放每次Present取样绘制。它们不是同一个“60帧”。实际采样率还取决于游戏更新和稳定性检查，不能只凭定时器配置断言实际帧率。

### 录像与结果

- `.hprghost`文件魔数 `HPRGHOST`，当前写版本4；Core.cs仍包含较旧格式的读取分支。
- 每条采样保存double时间、Right/Up/Forward/Position四个向量及速度，采样记录60字节。
- 保存赛事ID、车型资源ID、记录人、创建时间及来源EXE哈希。
- `Duration`取最后一个轨迹点；`FinalTime`是单独字段，不要用后者裁剪轨迹。
- 历史SSAS有过内部字段03:11.60、轨迹03:20.17的区别；用户明确说结算约03:20。未验证FinalTime可代表正式结算成绩。
- 新渲染器不会修复旧文件已经写入的时钟/位置错配，历史丢失的尾段也不能凭空恢复。
- 用户不接受用平滑掩盖根因。若再次出现前后闪动，需重新区分文件本身、采样发布、回放计时、相机一致性和渲染性能。
- EXE哈希是来源元数据，不是连接白名单。兼容性依靠已知结构的运行时锚点及数据校验；不是任意版本、任意布局都能通用。

## 7. 原生回放及协议

主工具读取本机实际车型，导出应用过分件/轮胎变换的网格和录像。通过命名共享内存传给ASI；不把车辆网格或其他游戏资产装进发行包。

- 控制区：`Local\HPRGhostReplayNative_v3`，512字节，版本3。
- 资产区：按所有者PID和GUID创建的独立命名映射；载荷魔数 `HPRNAT03`。
- 包头含长度、车型、赛事、顶点数、索引数、采样数。顶点40字节、索引4字节、采样60字节。
- 发布使用序列号、修订号及所有者令牌，插件确认加载后才能Ready；切换录像先停止旧绘制。
- 插件后台线程负责校验和加载；Present回调不读磁盘、不解析资源。
- 原生Present中读取当前赛事、赛事时钟和相机；用录像插值姿态后绘制。赛事不匹配、失去前台、租期失效等情况下不画。
- 主工具约2秒心跳失效后停止；退出工具会显式撤销播放所有权。共享内存Ready/IsDrawing不是“选中一份文件”就为真。
- 钩住Present和ResizeBuffers；使用自己的D32深度先做模型预绘制，再画透明颜色，解决远侧轮胎与车身叠层。
- 使用命令列表并恢复图形状态；不改变Present的同步间隔或flags，不改游戏帧队列。
- 当前只有模型自身遮挡，没有道路、山体等场景深度遮挡；材质简化。
- 当前已验证窗口/无边框。不要把少量测试泛化成所有显卡、其他插件组合、独占全屏或设备丢失路径均验证完成。
- ASI会修改绘制函数入口的钩子跳转；整个项目不能再描述成“完全不改游戏进程”。当前功能没有修改车辆物理、成绩、AI或氮气的逻辑。

若改IPC布局、模型导出约定或插值，需同步检查C#和原生两端；不要只改其中一边。核心格式在 `src/NativePlayback.cs`、`native/src/Channel.hpp` 和 `native/src/NativeGhost.cpp`，不在旧原型配置中。

## 8. 构建与最小验证

在项目根目录使用PowerShell：

```powershell
Set-Location -LiteralPath 'E:\DevTools-LoushShinny\Projects\HPRGhostReplay'
.\Build.ps1
.\Build.ps1 -Configuration Development
```

主程序使用Windows自带.NET Framework 4.x x64 C#编译器。没有必须迁移成.NET 8、NuGet或大型框架的需求。当前脚本的WPF引用仍写着C:\Windows；这是跨系统安装路径的潜在限制，不是项目必须放在C盘。

原生工具已随项目迁移，默认无需下载：

- 编译器：`research/native-ghost-prototype/tools/zig-windows-x86_64-0.13.0/zig.exe`
- MinHook：`research/native-ghost-prototype/vendor/minhook-1.3.4/`
- 原生构建入口：`native/Build.ps1`

**不要因为目录名带prototype就整目录删除它。当前构建仍依赖上述工具和vendor。**

```powershell
.\native\Build.ps1
.\Build.ps1
```

主Build会在原生输出缺失或native/src比ASI更新时触发原生编译。修改原生Build脚本或第三方依赖时不一定触发时间戳判断，应明确执行native/Build.ps1。原生重建后还需重建主EXE，才能更新内嵌组件。

普通构建和开发自检不安装游戏插件；**正常打开主界面会自动安装**。做纯离线测试时不要随手打开正常界面。

开发自检：

```powershell
$p = Start-Process -FilePath '.\app\HPRGhostReplay-dev.exe' -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
Get-Content '.\research\selftest-results.txt'
```

Release接受开发参数会返回2；这不是测试失败。新目录、源码分享包等没有两项实机样本时是188项，包含 `research/camera-data.bin` 和 `research/module.bin` 的完整检查历史为190项。读结果和退出码，不机械比较计数。

目前根research中保留了上述两份实机样本，重复module.bin已删。不要把这些样本随发行包或私发源码包发出去。

原生协议检查需先有开发测试生成的合成载荷：

```powershell
.\native\build\NativeSmoke.exe --payload .\research\native-payload.bin .\research\native-expected.bin
```

清理后native/build不存在，需先重新构建。原生图形smoke的另一种入口依赖旧原型网格与selected.hprghost，不能把那份真实资产/录像拷进发行包。

单个实际车型读取检查（读取本机资源；会导出开发预览）：

```powershell
$p = Start-Process -FilePath '.\app\HPRGhostReplay-dev.exe' -ArgumentList '--model-check','329012' -Wait -PassThru
$p.ExitCode
```

只做与改动相关的必要检查。修改文案不需要让玩家跑完整场；修改时序时先离线分析、再说明只需10–15秒行驶的具体采样目的。

## 9. 发行、说明书与源码包

正式包白名单仅为EXE、使用说明、第三方许可及空Recordings目录。程序内嵌名称表、原生插件和官方加载器；玩家无需再下载Python、SDK或C++运行库。

```powershell
.\Package.ps1
# 候选打包可用：
.\Package.ps1 -Executable 'app\HPRGhostReplay.exe' -OutputDirectory '.\candidate-package'
```

默认输出已改成项目内 `releases/`。脚本拒绝覆盖同名ZIP/暂存目录；不是构建坏了。开发构建不允许打包。

同版说明书曾按用户要求多次修订ZIP，beta11的历史哈希不唯一。文档修订不等于程序更新，当前正式ZIP及.sha256、LATEST要一致。不要拿旧报告中的哈希宣称当前包损坏。

- `packaging/使用说明.txt`：玩家指南，中文已由用户亲自改写，英文按该中文同步。只讲使用操作，不加源码原理、格式版本、哈希、历史文件、防御性长篇措辞或免责声明。
- 顶层README及research文档是开发历史，不等于当前玩家说明；`archive/发布说明建议.zh-CN.md`含旧WPF描述，不能原样用于新发布。
- 中英文按钮名要与UiText一致。用户修改中文后，以其桌面最新版本为内容依据，不擅自重写中文。
- 用户要求发行第三方说明不提及ZMenu，不给该插件宣传机会。当前release说明和THIRD-PARTY-NOTICES已移除相关参考段落；其他实际组件许可要保留。
- 发行图标使用游戏Steam标识加工；它不是本项目原创游戏素材。私发源码包已换成自制简单占位图标，不能认为该包与正式EXE所有资产字节相同。

定向源码包：`source-share/HPRGhostReplay-v0.1-source-review.zip`，71项文件，约554KB。另有桌面同名副本。包括本项目源码、相对路径构建脚本、离线测试、MinHook源码和官方加载器DLL及许可，不含游戏EXE、网格、录像、日志或内存样本，不附带157MB编译器。

该ZIP是2026-10-17的审阅快照；迁移后的目录调整及本交接文档不会自动出现在旧ZIP里。需再次分享新版时重新整理，不要直接压缩整个项目。

用户现阶段选择定向私发源码交流，未要求公开开源。包内SOURCE-NOTICE限制自有部分的公开转发，并保留第三方许可；不要自行改成整体MIT授权或替用户发布仓库。私发也不等于已经完成全部代码来源/游戏授权核实。

## 10. 验证事实与不能替代它的东西

### 用户已经明确确认的体验

- 整场自动录制、对应赛事自动回放可以工作。
- SL65实际车型的尺寸、位置、方向曾确认正确；车型选择、透明度、分享已实现。
- 原生旁影修正自身遮挡后，用户说“恢复正常，操作流畅”。
- 原生录像回放，用户说“比原来流畅太多了，操作也不卡”。
- 主界面整合后，用户说“我测试了，很正常，包括别的录像。”

这些是对应版本和场景下的用户反馈，不等于所有旧录像都被修复或所有硬件都已覆盖。

### 自动或只读验证

- 2026-10-17相关完整检查历史190项；2026-10-20迁移的隔离检查188项通过。
- E盘Release、Development、原生重建和打包通过；旧G盘开发目录删除后仍可构建。
- E盘自动定位到G盘游戏并读取SL65：40,565顶点、29,658三角面、4轮胎。
- 原生载荷边界与12个姿态参考点对照通过，最大误差0；所有者租期检查通过。
- WARP独立图形检查曾覆盖绘制、Present/ResizeBuffers和图形状态恢复。Present调用率不是屏幕显示帧率，也不是输入延迟测量。

### 尚未补齐或需重新确认

- 曾按用户要求将游戏插件备份移除，以便用户手动模拟首次安装。现磁盘确有官方9.7.4加载器、回放组件及收据，但没有后续明确的“干净安装后游戏内成功”文字反馈；不要代替用户确认这一项。
- 旧录像的偶发前后闪动曾出现过，后续又正常；不要靠一次自检通过、平滑指标改善或历史推测宣布所有根因已修复。
- 完整来源审计、跨布局、全硬件、全插件组合、独占全屏、全部设备重建路径没有全面验证结论。

## 11. 历史关键问题及证据位置

- 早期WPF透明覆盖窗口会让用户感到键盘迟钝。自动模式对照时用户明确说车影消失更顺畅；后改原生Present绘制。不要把主程序还引用WPF模型类型误判为仍使用旧窗口。
- 原型最初透明层叠导致远侧轮胎/车身异常，通过独立模型深度预绘制修正。当前正确实现位于native/src，不要拿旧research/src覆盖它。
- CHARGED ATTACK尾段曾因结果与轨迹终点混用被截断，格式4已拆开；丢掉的数据不重造。
- EXE哈希不同曾被拒绝，已改已知运行时布局匹配；SupportedHash常量存在不代表仍在执行EXE白名单。
- 早期采样和时序问题保留在frame-fence、timing、playback-overhead等研究资料里；应结合当前FrameSampling/GameReader重新核对。

证据目录：

| 目录 | 内容 |
| --- | --- |
| `research/frame-fence-verification` | 采样归属诊断和检查资料 |
| `research/native-integration-20261017` | 原生主界面整合验证 |
| `research/native-ghost-prototype/validation-archive` | 清理时保留的原型源码快照、安装记录、图像、局内对照摘要 |
| `research/loader-auto-install-20261017` | 加载器安装卸载检查 |
| `research/startup-install-20261017` | 启动准备、目录发现及完成提示检查 |
| `research/card-states-20261017` | 默认关闭、就绪亮起的UI验证 |
| `research/first-install-backup-20261017-141719` | 用户原有插件与配置备份，含其他项目文件，不可当废物删除 |
| `archive/migration-20261020` | 全量迁移清单、校验及报告 |
| `archive/cleanup-20261020-235426` | 本次清理清单和受保护文件摘要 |

已删除编译缓存、源码包解压构建目录、旧EXE、beta10/11之外的旧安装包和发行解压目录、7份重复module.bin、旧日志及临时迁移检查产物，约671.5MB。相关历史报告提到的部分构建产物不再存在是正常的。独有研究样本和关键记录仍保留。

## 12. 协作习惯与工作边界

- 用户重视实际局内体验，接受精简测试和试用崩溃，但不能随意损坏/删除文件。
- 用户讨厌反复确认、复杂审核流程、过多防御性措辞。已授权的实现应做完；需要真实玩家输入时，说明目的和最少操作。
- 不反复要求完整跑赛事。能用只读10秒采样定位的，就不要要求重录完整场。
- 用户文件、录像、其他模组保留；删除需用户授权并核对范围。本次清理授权已经执行完，不等于继续删除所有旧文件的长期许可。
- 不把历史备份、源码审阅ZIP或研究目录中的副本当活动源码。
- 说明事实来源：代码事实、离线结果、用户实测和推测分别说清楚。
- 不把“读取内存”本身简单定义为作弊；当前没有提供改成绩/物理功能，但结构定位和绘制源码可能被别人复用。不要向用户承诺私下分享一定不存在许可问题，也不要代替用户公开分享。

### 版本号与 Git

- 仓库只跟踪源码、构建脚本、已构建的 EXE/ASI、数据表、文档和许可；`research/`、迁移清单、清理清单、录像与运行时文件按 `.gitignore` 排除，仍保留在本机。
- 改版本：先改 `VERSION`，再同步 `src/AppPaths.cs` 的 `AppInfo` 与 `src/UiText.cs` 里的版本键，最后更新 `docs/存档-<版本>.md` 的哈希。三处不一致会让界面语言回退，或让 `Package.ps1` 拒绝打包。
- 提交按主题拆分，源码与对应文档放在同一个提交里；不要把 `research/` 里的样本或玩家录像加进仓库。
- 只在“确认可运行”的节点打标签（如 `v0.1`），标签一旦推送不要改写。日常开发用普通提交，不轻易打标签。
- 仓库为私有，未授权公开；`source-share/` 的分发约定仍按第 9 节执行。不要因为建了 Git 仓库就改成公开或整体 MIT。

## 13. 交接时文件指纹

指纹只用于识别交接快照，不是兼容性白名单；后续重编译会变化。

| 文件 | SHA256 |
| --- | --- |
| `app/HPRGhostReplay.exe` | `D6F2112644683A1AAAA91D9DCABEF4C4D9EA28FA5F7344388E8E78B48FFE682C` |
| 桌面使用版 `HPRGhostReplay.exe`（仍是旧构建，未替换） | `47309E0F2A01866FD1B9831E6F56E1FBE07E136FE4D5D51E99A81FF24BA096FE` |
| 内嵌与游戏已安装 `HPRGhostNative.asi` | `F92F86B3A41591D6968DE6E17AC20C865B6B6CDDC6862EA530BB98F2AA7245CC` |
| 官方9.7.4 x64加载器/游戏dinput8.dll | `031A3E5576D91DCE1E438D36B9A3D462C7334AB4791990A8FF1E3DDC0E132DAF` |
| 当前 0.1 发行ZIP | `0FB598EA2EC7DAD6E7D9303D5B0594723B31E7F9A3756AA24C02EF9B500A2FD9` |
| 源码审阅ZIP | `B466EFEC231C429006F9C671CCCD402A4738F081857ED973C323996ADD4D995C` |

上述哈希与根目录 `VERSION` 一致；存档范围与复现方式见 `docs/存档-0.1.md`。

运行中的进程、桌面录像数和配置会变化；下次接手应重新检查，不沿用旧PID或旧日志数量。

## 14. 可直接发给下一次对话的开场

> 请接手 HPRGhostReplay。项目已迁移到 `E:\DevTools-LoushShinny\Projects\HPRGhostReplay`。先读取该目录的 `HANDOFF.zh-CN.md`，再按里面的顺序读取实际代码，核对版本和运行状态后独立理解项目。当前使用 C# 主工具加原生 D3D11 ASI 回放，游戏目录只保留运行插件；不要回到旧的 mod-research/ghost-system 开发。先不要改代码或启动游戏，读完后向我说明你理解的当前实现与需要确认的事项，再按我的新要求继续。