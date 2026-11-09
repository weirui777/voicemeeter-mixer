# VMixer 开发文档

面向想读代码、改代码、复现问题的人。**普通用户请看 [README.md](README.md)**，
那里只有介绍、功能和安装使用。

---

## 技术栈与构建约束

| 项目 | 说明 |
| --- | --- |
| 语言 | **C# 5**（.NET Framework 4.x），只用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 编译 |
| 平台 | x64，Win10 / Win11 |
| 依赖 | **无第三方依赖**，不用 NuGet、不用 WinForms Designer 生成的代码 |
| 第三方 | `winmm`（提示音）、`user32`/`gdi32`（分层窗口、图标、热键） |

几条必须遵守的硬约束（踩过坑的）：

- **只能写 C# 5 语法**：不能用字符串插值、`nameof`、`out var`、表达式体成员、局部函数。
- **源码存成 UTF-8 无 BOM**，编译时必须带 `/codepage:65001`，否则中文全乱码。
- **`PowerShell 5.1` 读 `.ps1` 按 ANSI(GBK) 解释**，
  所以本仓库的 `.ps1` 里**不能出现任何中文**（包括注释），
  需要中文的地方用 `[char]0xXXXX` 拼。`package.ps1` 里的「使用说明.md」就是这么拼出来的。
- `Add-Type` 的 C# 片段必须纯 ASCII；引用 `System.Drawing` 要显式 `-ReferencedAssemblies System.Drawing`。
- 删除文件一律 `rm -- "<绝对路径>"`（可恢复），`Remove-Item` 属于永久删除，会被安全策略拦下。

## 目录结构

```
VMixer/
├─ src/            C# 源码（.NET Framework 4.x，C# 5）
│  ├─ VmApi.cs         Remote API 封装、通道发现、参数泵线程、会话重建、写入审计
│  ├─ MixerPanel.cs    混音面板（全自绘：竖排布局、命中测试、拖拽、右键菜单、提示音接线）
│  ├─ LayeredSurface.cs 分层窗口基类（UpdateLayeredWindow、圆角、柔影、文字绘制）
│  ├─ ChannelControl.cs 图标磁贴 / 静音徽标 / 扁平按钮
│  ├─ FaderControl.cs  横向音量条的绘制与坐标换算（越往右越响）
│  ├─ SoundFx.cs       提示音：winmm waveOut 现场合成 + 按增益缩放
│  ├─ IconStore.cs     通道图标：8 种内置字形表
│  ├─ Dialogs.cs       通道设置对话框（三块分区 + 拖动排序 + 提示音选择）、重命名对话框
│  ├─ DragListView.cs  支持拖动排序的 ListView（按下→移动→松手，带插入指示线）
│  ├─ VmConfig.cs      配置持久化（INI）
│  ├─ Theme.cs         深/浅色配色（跟随系统 AppsUseLightTheme），含托盘图标配色
│  ├─ Glyphs.cs        波形图标绘制（托盘 / 标题栏 / exe 图标）
│  ├─ Ui.cs            DPI 缩放辅助
│  └─ Program.cs       托盘、热键、单实例、自检
├─ build.ps1      编译脚本（只出 dist\VMixer.exe）
├─ package.ps1    打包脚本（编译 + 生成 ico + 组装 release\ + 压 zip）
├─ docs/          README 里引用的界面截图
├─ tools/         研发 / 验证用脚本
│  ├─ *.ps1        界面抓图、点击自动化、自测（Windows 侧）
│  ├─ *.py        Voicemeeter Remote API 的探测 / 读写脚本
│  └─ *.rs        Rust FFI 绑定（研究 Remote API 用）
├─ dist/          编译产物（.gitignore 忽略）
└─ release/       发布包（.gitignore 忽略）
```

> `dist/` 和 `release/` 都被 `.gitignore` 排除，**仓库里只有源码、文档和调研脚本**。
> 打包出来的 `dist\VMixer.zip`（含 exe / ico / 使用说明 / 截图）**不提交到 Git**，
> 发布时手动上传到 GitHub 的 **Releases** 页面供人下载。

## 编译与打包

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1     # 只编译，产出 dist\VMixer.exe
powershell -ExecutionPolicy Bypass -File .\package.ps1   # 编译 + 生成 ico + 组装 release\ + 压 zip
```

`package.ps1` 的顺序是：**编译 → 生成 ico → 再编译一次把 ico 嵌进 exe → 组装 `release\` → 压 zip**。
过程中会自动停掉正在运行的 VMixer（否则 exe 被锁住拷不动），
并且会把仓库根的 `README.md` 复制成发布包里的《使用说明.md》（去掉 `docs/` 图片路径前缀）。

编译参数（`build.ps1`）：

```
/target:winexe /platform:x64 /optimize+ /codepage:65001 /warn:2
```

## 命令行参数

`VMixer.exe` 是无窗口的托盘程序，下列参数都用于自检和排查：

| 参数 | 作用 |
| --- | --- |
| `VMixer.exe` | 正常启动（托盘常驻） |
| `VMixer.exe --show` | 启动即弹出面板 |
| `VMixer.exe --test` | 自检：打印通道列表，并对主音量做一次写入/回读测试后自动还原 |
| `VMixer.exe --layout` | 导出面板各通道滑块的屏幕坐标到 `%APPDATA%\VMixer\layout.txt`（排查/自动化用） |
| `VMixer.exe --icon [路径]` | 把托盘图标导出成 PNG（默认写到 `%APPDATA%\VMixer\icon.png`），用于预览/做图标资源；路径以 `.ico` 结尾则导出多尺寸 ico |
| `VMixer.exe --render [路径] [悬停行号] [off]` | 把面板离屏渲染成 PNG（不依赖屏幕），用于排查界面问题；第三个参数填 `off` 可模拟「Voicemeeter 未运行」时的状态栏 |
| `VMixer.exe --tone [音色] [dB]` | 提示音自检：打印波形设备能否打开，并按指定音色/dB 播一次提示音（音色：0-3 或 clear/bright/soft/blip，默认清脆叮 0 dB） |
| `VMixer.exe --towav [路径] [音色]` | 把指定音色的提示音导出成 WAV（默认 `%APPDATA%\VMixer\tone.wav`），用来客观比对波形 |
| `VMixer.exe --settings` | 单独打开「通道设置」窗口（调试 / 不经过托盘直接改设置用） |
| `VMixer.exe --dragtest` | 拖动排序自测：模拟两次拖动并把结果写进 `config.ini` 的 `[Order]`（会改动顺序，仅用于验证） |
| `VMixer.exe --glyphs [路径]` | 把 8 个内置图标字形画成一张对照图（默认 `%APPDATA%\VMixer\glyphs.png`），用于检查图标 |

> `--dragtest` 会真的改写 `config.ini` 的 `[Order]`，只在验证时用。

## 配置文件

`%APPDATA%\VMixer\config.ini`

| 段 / 键 | 含义 |
| --- | --- |
| `[General] DllPath=` | 手动指定 `VoicemeeterRemote64.dll` 路径，留空 = 自动找 |
| `[General] SoundFeedback=1` | 「调节音量时播放提示音」的开关 |
| `[General] ToneStyle=0` | 提示音音色（0 清脆叮 / 1 明亮叮 / 2 柔和钟 / 3 短促滴） |
| `[General] TipChannel=S4` | 托盘气泡里显示哪条通道，留空 = 不显示 |
| `[General] AutoStart=1` | 开机自启（写 HKCU Run，无需管理员） |
| `[General] OpaquePanel=0` | 0 = 半透明亚克力面板，1 = 不透明 |
| `[General] VolumeKeys=1` | 是否接管系统音量键 |
| `[Channels] Visible=` | 要显示的通道（如 `S4,B0,S0,S2`），留空表示默认 6 条 |
| `[Order] Order=` | 通道显示顺序（拖动排序的结果），留空表示默认顺序 |
| `[Order] OrderCustom=1` | 这个顺序是用户自己拖出来的（据此区分「你排的」和「机器排的」） |
| `[Names] S4=AUX` | 通道自定义名称 |
| `[Icons] S4=b:disc` | 通道图标（`b:` 前缀 = 内置字形） |

**写入审计日志**：`%APPDATA%\VMixer\vm.log`，只记录本程序对 Voicemeeter 的写入，形如
`10:32:34.920  SET Bus[0].Gain = -8.80`；提示音也会记 `TONE db=...`。
方便核对「什么时候响过、响的时候增益是多少」，怀疑音量被改时直接对账这个文件。

## 通道对应关系

Voicemeeter 的 Remote API（`VoicemeeterRemote64.dll`）用字符串参数名寻址，
本工具自动探测当前 Voicemeeter 里实际存在的通道，映射如下（Banana / Pro 布局）：

| 面板名称 | API 参数 | 说明 |
| --- | --- | --- |
| 硬件输入 1 / 2 / 3 | `Strip[0].Gain` ~ `Strip[2].Gain` | 声卡输入（麦克风等） |
| 虚拟输入 | `Strip[3].Gain` | Voicemeeter Input（放应用的虚拟输入） |
| 虚拟 AUX | `Strip[4].Gain` | Voicemeeter AUX |
| 主音量 A1 | `Bus[0].Gain` | 主输出总线，对应 Voicemeeter 里的 HARDWARE OUT A1 |
| A2 / A3 | `Bus[1]` / `Bus[2]` | 第二、三路物理输出 |
| B1 / B2 | `Bus[3]` / `Bus[4]` | 虚拟输出总线 |

> Banana / Pro 的界面上的 MASTER SECTION 只有 A1/A2/A3/B1/B2 五个总线，
> **没有独立的 MASTER 推子**，Voicemeeter 主输出就是 A1，所以本工具把 A1 标成「主音量」。

## 实现要点（踩坑记录）

- **必须调用 `VBVMR_IsParametersDirty()`**：官方 SDK 文档写明这个函数会「让 Voicemeeter 处理客户端推送过去的参数」。
  只写不调用它，写入的参数会一直挂着不生效（实测能挂十几秒甚至更久）。
  本工具在后台线程里每 10 ms 调一次，写入到实际生效约 30 ms。
  （自己写脚本改参数时也一样：写完要泵一会儿 `VBVMR_IsParametersDirty()` 再读，否则读到的还是旧值。）
- **所有 API 调用都在同一条线程**：Remote API 不是完全线程安全的，因此读取、写入统一放在后台线程，UI 只读缓存。
- **通道索引是探测出来的**，不是写死的：不同版本（Banana / Potato）条数不同，程序启动时逐个探测 `Strip[i].Gain` /
  `Bus[i].Gain` 是否存在；参数名大小写敏感，必须写 `.Gain` / `.Mute`。
- **亚克力效果是自己做的**（没走 DWM 模糊）：面板是 `WS_EX_LAYERED` 分层窗口，内容画在 32 位 ARGB 位图上，
  再用 `UpdateLayeredWindow` 贴到桌面，所以才能同时做到圆角 + 每像素透明 + 柔和投影。
  毛玻璃的做法是：显示前把自己藏起来 → 抓取将要显示区域的屏幕画面 → 两级降采样模糊 → 作为卡片底图贴上去 →
  再叠一层白色蒙版。刷新时重新抓一次背景即可。
- **没有背景时的兜底卡片也要跟主题**：抓不到背景（不透明模式 / 离屏渲染）时曾经硬编码画浅色卡片，
  结果深色主题下变成「浅底白字」完全看不清。现在兜底分支也按深/浅色分别取色。
- **透明层上不能用 ClearType**：`TextRenderer`（GDI）在带 alpha 的位图上会渲染出红蓝描边，
  面板文字统一改用 `Graphics.DrawString` + `AntiAliasGridFit`。
- **Remote API 是共享会话**：如果别的程序也调用了 Remote API 并执行了 `VBVMR_Logout()`，
  本进程读回来的参数会停在旧值上（写入其实生效了，但显示会慢一拍）。
  程序每次写入后都会回读校验，发现不一致就自动重建会话（重新加载 DLL 并登录）并重发，从而恢复同步。
  同理，**本程序运行期间别的进程可能登录不上 Remote API**（登录返回非 0），
  这时用脚本读写参数会全部返回 `ERR`——需要先把 VMixer 退出再操作。
- **⚠️ `VBVMR_GetLevel()` 有破坏性副作用，已彻底弃用**：这个「读取电平」的接口会把电平数值
  写进共享内存里对应的**增益槽位**（例如主总线电平 2.092 会出现在 `Bus[0].Gain`，
  AUX 电平 -22.298 会出现在 `Strip[4].Gain`）。因为本工具每 10 ms 泵一次参数，
  这些电平值会被当成音量提交给 Voicemeeter，**直接把用户的音量设置改坏**。
  因此本工具完全不调用 `VBVMR_GetLevel()`，面板上也就**没有实时电平条**——
  这是刻意舍弃的功能，保留它就等于随时会篡改你的音量。
- **后台唤出面板要抢前台焦点**：Windows 的前台锁会让后台程序调 `SetForegroundWindow` 失效，
  于是「失焦自动隐藏」会在 0.6 秒后把刚弹出的面板收掉。
  程序现在用 `AttachThreadInput` 临时接入前台线程再设置焦点，保证弹出后能稳定停留并接收点击。
- **重复启动通过命名事件通知**：第二个实例不新建窗口，而是 `Set()` 一个
  `Global\VMixer_ShowPanel` 事件；主实例在后台线程 `WaitOne` 到信号后，
  通过 UI 定时器在 UI 线程执行弹面板，避免跨线程碰窗口。
- **`StringFormatFlags.LineLimit` 会把「装不下」的一行整行丢掉**（不是裁剪）。
  面板按 125% 缩放后，8.5pt 中文字体的行高约 24.7 px，
  原来的通道名那行只有 22 px 高，于是**文字被整个丢弃，界面上一个字都看不到**。
  现在那行加高到 28 px，并且在 `DrawTextEllipsis` 里加了兜底：
  量出来放不下就临时缩小字号重画，保证任何情况下文字都可见。
- **滚轮事件的坐标不能直接用**：`WM_MOUSEWHEEL` 的 lParam 在不同路径下
  会被解释成屏幕坐标或客户区坐标，WinForms 传出来的 `e.Location` 也不可靠。
  现在统一用 `Cursor.Position`（光标当前屏幕位置）换算，命中哪条通道就调哪条。
- **横向滑条的方向要和直觉一致**：滑条越往右越响（−60 dB → +12 dB）。
  绘制函数 `XFromValue` / `ValueFromPoint` 必须用同一套换算，否则旋钮和实际增益会反着来。
- **提示音自己合成**：用 `winmm` 的 `waveOut` 直接播一段 16 bit 单声道 PCM。
  音色用**铃铛分音模型**：基频 + 2 / 2.76 / 4.07 / 5.43 倍泛音，第 k 个分音的衰减常数按 `0.42^k` 递减
  （高次分音先死，金属味干净），3 ms 起音斜坡防爆音，末尾 30 ms 淡出，最后归一化到峰值 0.72。
  音量按 `10^(dB/20)` 线性缩放，≤ −60 dB 直接静音。
  播放放在线程池里，不阻塞界面；`WAVEHDR` 用 `Marshal.AllocHGlobal` 分配、
  播完 `waveOutReset` + `waveOutUnprepareHeader` + 释放。
  ⚠️ 一开始用「987.77 Hz + 1318.51 Hz 两个正弦叠加」当提示音，结果是个大三度，
  拍频出来的「呜」声非常难听——**提示音必须用单一基频的铃铛分音，不能靠多音叠加**。
- **设置窗口必须自己做 DPI 和布局**：本程序是 system-DPI 感知（`SetProcessDPIAware`），
  WinForms 不会再帮你缩放，`AutoScaleMode` 设成 `Dpi` 反而会二次放大，
  所以设置窗口改成 `AutoScaleMode.None` + 全部坐标走 `Ui.S()`；
  窗口改成可缩放 / 可最大化，列表铺满、底部操作区钉在下面，
  位置在 `LayoutControls()` 里统一算（窗口一变就重排）。
- **普通对话框里不能用 `Color.Transparent` 的子控件**：
  「控件不支持透明的背景色」会直接抛异常。用 `SystemColors.Control` 代替。
- **TopMost 的浮窗会挡住托盘气泡**：右下角浮窗本身是 TopMost，
  正好压在托盘图标正上方时，气泡弹出来会被它盖住。
  ⚠️ 该浮窗已按需求删除，现在只用 Windows 自带的托盘气泡，不再有遮挡问题。
- **写入审计日志**：每一次真正写进 Voicemeeter 的参数都会记到 `%APPDATA%\VMixer\vm.log`，  形如 `10:32:34.920  SET Bus[0].Gain = -8.80`；提示音也会记 `TONE db=...`，
  方便核对「什么时候响过、响的时候增益是多少」。
  以后再遇到「我的音量怎么变了」，直接看这个文件对账就能知道是不是本程序改的。
- **拖动阈值必须取绝对值**：判断「是不是在拖动」时写的是 `dx < 4 && dy < 4`，
  结果**向上拖的时候 `dy` 是负数，永远达不到阈值，一拖就断**——
  也就是「往下拖能排、往上拖排不了」。现在改成 `Math.Abs(dx) < 4 && Math.Abs(dy) < 4`。
  这个 bug 很隐蔽：向下拖是好的，只有向上拖才失效。
- **旧版本的「点列头排序」已经去掉**：WinForms 的 `ListView` 点了列头会自动排序，
  和用户想要的「自己拖出来的顺序」是两种东西，而且自动排序会把主音量甩到最后。
  现在排序完全由 `DragListView` 自己实现（按下 → 移动超过阈值 → 松手换位），
  列头不再响应点击。
- **旧配置里的顺序要能识别**：老版本保存的顺序是机器生成的「输入条全在前、输出总线全在后」，
  主音量会掉到末尾。程序加载时会把这种顺序识别出来丢掉（`OrderCustom` 标记区分），
  让主音量回到最前面；用户自己拖过的顺序则原样保留。
- **.NET Framework 4.x 下 `GraphicsState` 不实现 `IDisposable`**，
  `using (GraphicsState st = ...)` 编译不过，要用 `g.Save()` / `g.Restore(st)`。
- **旋转时的中心点是 Translate 之后的原点**：`GraphicsState` / `TranslateTransform` 配合旋转时，
  要先把图形 Translate 到中心再 Rotate。

## 已知限制

- 面板显示的数值来自 Voicemeeter 的实际状态，Voicemeeter 未运行时底部状态栏会显示「· 未运行」并自动重试。
- 系统音量键接管需要注册全局热键，若被别的软件占用则不生效（不影响面板本身）。
- 毛玻璃背景是「显示瞬间」的截图快照，面板长时间停留时背景不会跟着桌面动画变化。
- 提示音走**系统默认播放设备**（如果默认设备被 Voicemeeter 接管，就会从 Voicemeeter 的输出里出来）。
- **面板不显示实时电平条**：Voicemeeter 的 `VBVMR_GetLevel()` 会把电平值写回增益槽位，
  配合本工具的参数泵会污染你的音量设置，因此宁可少这个功能也不用它（详见上文「实现要点」）。
