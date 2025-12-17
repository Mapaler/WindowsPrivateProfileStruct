# WindowsPrivateProfileStruct

> 🧪 **在 .NET 中模拟 Windows [WritePrivateProfileStructA](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-writeprivateprofilestructa) / [GetPrivateProfileStructA](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-getprivateprofilestructa) 行为的轻量级库**

将 C# 结构体（`struct`）序列化为**带校验和的十六进制字符串**，格式与 Windows API `WritePrivateProfileStructA` 在 INI 文件中写入的内容**经测试一致**。支持反向解析并验证校验和。

适用于需要在 .NET（包括非 Windows 平台）上读写传统 Windows INI 配置中嵌入的结构体数据。

> [!WARNING]
> ⚠️ 注意：本库基于实测行为实现，**不保证在所有边缘场景下与 Windows 完全一致**。建议在关键场景中进行兼容性验证。

> [!NOTE]
> 本人2020年用于生成 [**SkyScan/Bruker CTAn (CTAn Analyser)**](https://www.brukersupport.com/) 软件的批处理模式(BatMan)分析列表(*.ctl)文件内的 `Info` 键值时研究的，经实测研究出可用。  
> 2025年12月15日才发现 [WritePrivateProfileStructA](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-writeprivateprofilestructa) 这个官方函数，于是将算法用 AI 优化整理后发布，以便结合 [`ini-parser`](https://github.com/rickyah/ini-parser) 等未使用 Win32API 的跨平台库使用。
> 另外分析程序文件(*.ctt)内的 `Config blob` 键值应该也是相同的方法产生的。

---

## 🚀 快速开始

## 📦 安装

通过 [NuGet](https://www.nuget.org/packages/WindowsPrivateProfileStruct) 安装包:

```bash
dotnet add package WindowsPrivateProfileStruct
```

或通过包管理控制台安装:

```powershell
Install-Package WindowsPrivateProfileStruct
```

### 安装
引用项目源码（暂未发布 NuGet）：
```bash
dotnet add reference ../path/to/WindowsPrivateProfileStruct.csproj
```

### 定义结构体（必须使用 `LayoutKind.Sequential`）

```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct SIZE
{
    public uint width;
    public uint height;
}

[StructLayout(LayoutKind.Sequential)]
public struct POSITION
{
    public uint left;
    public uint top;
}

[StructLayout(LayoutKind.Sequential)]
public struct DatasetInfo
{
    public int format;      // 例如：1 = RGB, 2 = YUV
    public SIZE size;       // { width: 1920, height: 1080 }
    public POSITION pos;    // { left: 100, top: 200 }
    public int colordeep;   // 例如：32
}
```

#### 💡 数据结构示意（类 JSON 表示）
```json
{
  "format": 1,
  "size": { "width": 1920, "height": 1080 },
  "pos": { "left": 100, "top": 200 },
  "colordeep": 32
}
```

### 序列化 → 十六进制字符串（用于写入 INI）

```csharp
var info = new DatasetInfo
{
    format = 1,
    size = new SIZE { width = 1920, height = 1080 },
    pos = new POSITION { left = 100, top = 200 },
    colordeep = 32
};

string hex = Struct.ToHex(info);
// 示例输出: "01000000800700003804000064000000C80000002000000010"
// 格式 = [原始结构体内存][1字节校验和] 的大写 HEX（无分隔符）
```

### 反序列化 ← 十六进制字符串（自动校验）

```csharp
if (Struct.FromHex(hexString, out DatasetInfo restored))
{
    Console.WriteLine($"Resolution: {restored.size.width}x{restored.size.height}");
}
else
{
    Console.WriteLine("校验失败、长度错误或无效 HEX");
}
```

---

## 🛠 完整示例：替代 `WritePrivateProfileStructA` / `GetPrivateProfileStructA`

本库可与 [`ini-parser`](https://github.com/rickyah/ini-parser) 结合，实现跨平台的 INI 结构体读写。

### 安装 ini-parser

```bash
dotnet add package ini-parser
```

---

### ✅ 方法 1：直接十六进制序列化（简洁明了）

```csharp
using IniParser;
using WindowsPrivateProfileStruct;

// 1. 准备数据
var config = new DatasetInfo
{
    format = 1,
    size = new SIZE { width = 1280, height = 720 },
    pos = new POSITION { left = 50, top = 60 },
    colordeep = 24
};

// 2. 序列化为 HEX
string hex = Struct.ToHex(config);

// 3. 写入 INI
var parser = new FileIniDataParser();
var ini = new IniData();
ini["Window"]["Dataset"] = hex;
parser.WriteFile("app.ini", ini);
```

生成的 `app.ini` 内容：
```ini
[Window]
Dataset=0100000000050000D0020000320000003C000000180000005E
```

读取数据：
```csharp
// 1. 读取 INI
var parser = new FileIniDataParser();
var ini = parser.ReadFile("app.ini");
string hex = ini["Window"]["Dataset"];

// 2. 反序列化（自动校验）
if (Struct.FromHex(hex, out DatasetInfo config))
{
    Console.WriteLine($"Loaded: {config.size.width}x{config.size.height} @ ({config.pos.left}, {config.pos.top})");
}
else
{
    throw new InvalidOperationException("INI 数据损坏或格式不匹配");
}
```

---

### 🪟 方法 2：模拟 Windows API（贴近原生调用风格）

如果你希望代码风格更接近原始 Win32 API 的使用方式：

```csharp
// 为 ini-parser 定义 INI 读写适配器
static bool WriteIni(string section, string key, string value, string iniFile)
{
    var config = new IniParserConfiguration
    {
        AssigmentSpacer = string.Empty
    };
    var _parser = new IniDataParser(config);
    var parser = new FileIniDataParser(_parser);
    var data = File.Exists(iniFile) ? parser.ReadFile(iniFile) : new IniData();
    data[section][key] = value;
    parser.WriteFile(iniFile, data, Encoding.Default);
    return true;
}

static string? ReadIni(string section, string key, string iniFile)
{
    if (!File.Exists(iniFile)) return null;
    var parser = new FileIniDataParser();
    return parser.ReadFile(iniFile, Encoding.Default)[section][key];
}

// 现在可以像调用 Windows API 一样使用
var config = new DatasetInfo { /* ... */ };

// 写入（模拟 WritePrivateProfileStructA）
bool written = Struct.WritePrivateProfileStruct(
    "Window", "Dataset", config, WriteIni, "app.ini");

// 读取（模拟 GetPrivateProfileStructA）
if (Struct.GetPrivateProfileStruct("Window", "Dataset", out DatasetInfo restored, ReadIni, "app.ini"))
{
    Console.WriteLine($"Loaded: {restored.size.width}x{restored.size.height}");
}
```

✅ 两种方法生成的 INI 内容完全一致，且都能被原生 Windows 应用通过 `GetPrivateProfileStructA` 正确读取。

---

## 🔑 特性

- 🔄 **双向兼容**：生成的 HEX 可被 Windows 原生 API 读取；也能解析 Windows 写出的数据。
- ✅ **校验和保护**：末尾附加 1 字节校验和（所有结构体字节之和 mod 256），防止数据损坏。
- 📦 **支持嵌套结构体**：如 `struct A { public B b; }`。
- 🌍 **跨平台**：不依赖 Windows API，纯托管代码。
- 🧩 **零外部依赖**：仅需 `System.Runtime.InteropServices`（.NET Standard 2.0+）。
- ✅ **Windows API 风格接口**：提供 `WritePrivateProfileStruct` / `GetPrivateProfileStruct` 静态方法，便于迁移旧代码或保持一致调用风格。

---

## ⚠️ 使用限制

- 结构体必须是 **blittable 类型**（仅包含 `int`, `uint`, `float`, `bool` 等，**不能有 `string`、数组、类**）。
- 必须标记 `[StructLayout(LayoutKind.Sequential)]`。
- 内存布局受平台/编译器影响（建议指定 `Pack = 1` 若需严格对齐）：
  ```csharp
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct MyStruct { ... }
  ```

---

## 🧪 兼容性说明

本库通过以下方式验证与 Windows 行为的一致性：
- 调用 `WritePrivateProfileStructA` 写入结构体；
- 用 `GetPrivateProfileStringA` 读取原始 HEX；
- 比对 `Struct.ToHex()` 输出是否相同；
- 反向测试：用本库写入，用 `GetPrivateProfileStructA` 读取。

**目前在常见结构体（含嵌套）上表现一致，但不排除特殊对齐或平台差异导致偏差。**

---

## 📦 API

| 方法 | 说明 |
| --- | --- |
| `Struct.ToHex<T>(T value)` | 将结构体序列化为带校验和的十六进制字符串 |
| `Struct.FromHex<T>(string hex, out T value)` | 从十六进制字符串反序列化结构体，并验证长度与校验和 |
| `Struct.WritePrivateProfileStruct<T>(string section, string key, T value, IniWriteDelegate writer, string iniFile)` | 模拟 `WritePrivateProfileStructA`：将结构体写入 INI 文件（需提供写入委托） |
| `Struct.GetPrivateProfileStruct<T>(string section, string key, out T value, IniReadDelegate reader, string iniFile)` | 模拟 `GetPrivateProfileStructA`：从 INI 文件读取结构体（需提供读取委托） |

> 💡 后两个方法支持跨平台，不依赖 Windows API，但需要你传入 INI 文件的读写逻辑（如使用 `ini-parser`）。

---

## 📜 许可证

MIT