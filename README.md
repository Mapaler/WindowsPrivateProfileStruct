# WindowsPrivateProfileStruct

> 🧪 A lightweight .NET library that **emulates the behavior** of Windows APIs  
> [WritePrivateProfileStructA](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-writeprivateprofilestructa) / [GetPrivateProfileStructA](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-getprivateprofilestructa) for reading and writing structured data in INI files.

Serializes C# `struct` values into a **checksum-protected hexadecimal string**, matching the format written by `WritePrivateProfileStructA` to INI files — **as verified through practical testing**. Supports deserialization with automatic checksum validation.

Useful when you need to read or write legacy Windows INI files containing embedded binary structures — even on non-Windows platforms like Linux or macOS.

> [!WARNING]
> ⚠️ **Note**: This library is based on observed Windows behavior and practical tests.  
> It **does not guarantee 100% compatibility in all edge cases** (e.g., unusual packing, platform-specific alignment). Always validate in your target environment.

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

## 🛠 Full Example: Cross-Platform INI Struct I/O with `IniParser`

This library works seamlessly with [`IniParser`](https://github.com/rickyah/ini-parser) to replace Windows-only APIs.

### Install IniParser
```bash
dotnet add package IniParser
```

### Write Struct to INI (simulate `WritePrivateProfileStructA`)

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
Dataset=0100000000050000D0020000320000003C00000018000000E6
```

### Read Struct from INI (simulate `GetPrivateProfileStructA`)

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

> ✅ This approach runs on **Linux, macOS, and Windows**, and produces INI files that native Windows applications can read using `GetPrivateProfileStructA` (verified in tests).

---

## 🔑 Features

- 🔄 **Bidirectional compatibility**:  
  - Output can be read by Windows `GetPrivateProfileStructA`.  
  - Can parse data written by `WritePrivateProfileStructA`.
- ✅ **Checksum protection**: Appends 1-byte checksum (`sum(struct_bytes) % 256`) to detect corruption.
- 📦 **Nested structs supported**: e.g., `struct A { public B b; }`.
- 🌍 **Cross-platform**: Pure managed code, no P/Invoke required at runtime.
- 🧩 **Zero dependencies**: Only uses `System.Runtime.InteropServices` (.NET Standard 2.0+).

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
|-------|-------------|
| `Struct.ToHex<T>(T value)` | Serializes a struct to a checksummed HEX string |
| `Struct.FromHex<T>(string hex, out T value)` | Deserializes HEX to struct, validating length and checksum |

---

## 📜 License

MIT