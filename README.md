# WindowsPrivateProfileStruct

> 🧪 A lightweight .NET library that **emulates the behavior** of Windows APIs  
> [WritePrivateProfileStructA](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-writeprivateprofilestructa) / [GetPrivateProfileStructA](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-getprivateprofilestructa) for reading and writing structured data in INI files.

Serializes C# `struct` values into a **checksum-protected hexadecimal string**, matching the format written by `WritePrivateProfileStructA` to INI files — **as verified through practical testing**. Supports deserialization with automatic checksum validation.

Useful when you need to read or write legacy Windows INI files containing embedded binary structures — even on non-Windows platforms like Linux or macOS.

> [!WARNING]
> ⚠️ **Note**: This library is based on observed Windows behavior and practical tests.  
> It **does not guarantee 100% compatibility in all edge cases** (e.g., unusual packing, platform-specific alignment). Always validate in your target environment.

> [!NOTE]
> This was originally researched by me in 2020 to generate the `Info` key values inside batch analysis list files (`*.ctl`) used by [**SkyScan/Bruker CTAn (CTAn Analyser)**](https://www.brukersupport.com/) software in its Batch Mode (BatMan). The method was validated through practical testing and confirmed to work.  
> On December 15, 2025, I discovered the official Windows API function [`WritePrivateProfileStructA`](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-writeprivateprofilestructa), and subsequently refined and published this algorithm—optimized with AI assistance—for use with cross-platform libraries that do not rely on Win32 APIs, such as [`ini-parser`](https://github.com/rickyah/ini-parser).  
> Additionally, the `Config blob` key values found in analysis program files (`*.ctt`) appear to be generated using the same method.

---

## 🚀 Quick Start

## 📦 Installation

Install the package via [NuGet](https://www.nuget.org/packages/WindowsPrivateProfileStruct):

```bash
dotnet add package WindowsPrivateProfileStruct
```

Or via Package Manager Console:

```powershell
Install-Package WindowsPrivateProfileStruct
```

### Define Your Struct (must use `LayoutKind.Sequential`)

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
    public int format;      // e.g., 1 = RGB, 2 = YUV
    public SIZE size;       // { width: 1920, height: 1080 }
    public POSITION pos;    // { left: 100, top: 200 }
    public int colordeep;   // e.g., 32
}
```

#### 💡 Data Structure (JSON-like representation)
```json
{
  "format": 1,
  "size": { "width": 1920, "height": 1080 },
  "pos": { "left": 100, "top": 200 },
  "colordeep": 32
}
```

### Serialize → Hex String (for INI storage)

```csharp
var info = new DatasetInfo
{
    format = 1,
    size = new SIZE { width = 1920, height = 1080 },
    pos = new POSITION { left = 100, top = 200 },
    colordeep = 32
};

string hex = Struct.ToHex(info);
// Example output: "0100000080070000B80B000064000000C800000020000000F5"
// Format: [raw struct bytes][1-byte checksum] as uppercase HEX (no separators)
```

### Deserialize ← Hex String (with validation)

```csharp
if (Struct.FromHex(hexString, out DatasetInfo restored))
{
    Console.WriteLine($"Resolution: {restored.size.width}x{restored.size.height}");
}
else
{
    Console.WriteLine("Invalid checksum, length mismatch, or malformed HEX");
}
```

---

## 🛠 Full Example: Cross-Platform INI Struct I/O with `ini-parser`

This library works seamlessly with [`ini-parser`](https://github.com/rickyah/ini-parser) to replace Windows-only APIs.

### Install ini-parser

```bash
dotnet add package ini-parser
```

---

### ✅ Method 1: Direct Hex Serialization (Simple & Explicit)

```csharp
using IniParser;
using WindowsPrivateProfileStruct;

// 1. Prepare data
var config = new DatasetInfo
{
    format = 1,
    size = new SIZE { width = 1280, height = 720 },
    pos = new POSITION { left = 50, top = 60 },
    colordeep = 24
};

// 2. Serialize to HEX
string hex = Struct.ToHex(config);

// 3. Write to INI file
var parser = new FileIniDataParser();
var ini = new IniData();
ini["Window"]["Dataset"] = hex;
parser.WriteFile("app.ini", ini);
```

Resulting `app.ini`:
```ini
[Window]
Dataset=0100000000050000D0020000320000003C000000180000005E
```

Read back:
```csharp
// 1. Read INI
var parser = new FileIniDataParser();
var ini = parser.ReadFile("app.ini");
string hex = ini["Window"]["Dataset"];

// 2. Deserialize (with checksum validation)
if (Struct.FromHex(hex, out DatasetInfo config))
{
    Console.WriteLine($"Loaded: {config.size.width}x{config.size.height} @ ({config.pos.left}, {config.pos.top})");
}
else
{
    throw new InvalidOperationException("INI data corrupted or struct layout mismatch");
}
```

---

### 🪟 Method 2: Windows API Emulation (Drop-in Replacement Style)

For code that closely mimics the original Win32 API usage pattern:

```csharp
// Define INI I/O adapters for ini-parser
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

// Now use Windows-like API calls
var config = new DatasetInfo { /* ... */ };

// Write like WritePrivateProfileStructA
bool written = Struct.WritePrivateProfileStruct(
    "Window", "Dataset", config, WriteIni, "app.ini");

// Read like GetPrivateProfileStructA
if (Struct.GetPrivateProfileStruct("Window", "Dataset", out DatasetInfo restored, ReadIni, "app.ini"))
{
    Console.WriteLine($"Loaded: {restored.size.width}x{restored.size.height}");
}
```

✅ Both methods produce identical INI content and are fully compatible with native Windows applications using `GetPrivateProfileStructA`.

---

## 🔑 Features

- 🔄 **Bidirectional compatibility**:  
  - Output can be read by Windows `GetPrivateProfileStructA`.  
  - Can parse data written by `WritePrivateProfileStructA`.
- ✅ **Checksum protection**: Appends 1-byte checksum (`sum(struct_bytes) % 256`) to detect corruption.
- 📦 **Nested structs supported**: e.g., `struct A { public B b; }`.
- 🌍 **Cross-platform**: Pure managed code, no P/Invoke required at runtime.
- 🧩 **Zero dependencies**: Only uses `System.Runtime.InteropServices` (.NET Standard 2.0+).
- ✅ **Windows API-style interface**: Provides `WritePrivateProfileStruct` / `GetPrivateProfileStruct` static methods for easy migration or consistent calling style.

---

## ⚠️ Limitations

- Structs must be **blittable** (only primitive value types like `int`, `uint`, `float`; **no `string`, arrays, or classes**).
- Must be decorated with `[StructLayout(LayoutKind.Sequential)]`.
- Memory layout may vary by platform/compiler. For strict byte-for-byte control, consider:
  ```csharp
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct MyStruct { ... }
  ```

---

## 🧪 Compatibility Notes

Compatibility has been validated by:
- Calling `WritePrivateProfileStructA` on Windows to write a struct.
- Reading the raw hex via `GetPrivateProfileStringA`.
- Comparing it with `Struct.ToHex()` output (they match in tested cases).
- Writing with this library and successfully reading back with `GetPrivateProfileStructA`.

**Consistency confirmed for common nested structs, but exotic layouts or alignment settings may differ.**

---

## 📦 API

| Method | Description |
| --- | --- |
| `Struct.ToHex<T>(T value)` | Serializes a struct to a checksummed hexadecimal string |
| `Struct.FromHex<T>(string hex, out T value)` | Deserializes a hex string to a struct, validating length and checksum |
| `Struct.WritePrivateProfileStruct<T>(string section, string key, T value, IniWriteDelegate writer, string iniFile)` | Emulates `WritePrivateProfileStructA`: writes a struct to an INI file (requires write delegate) |
| `Struct.GetPrivateProfileStruct<T>(string section, string key, out T value, IniReadDelegate reader, string iniFile)` | Emulates `GetPrivateProfileStructA`: reads a struct from an INI file (requires read delegate) |

> 💡 The last two methods are cross-platform and do not depend on Windows APIs, but require you to provide INI I/O logic (e.g., via `ini-parser`).

---

## 📜 License

MIT