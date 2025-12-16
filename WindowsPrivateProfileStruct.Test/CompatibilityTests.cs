using IniParser;
using IniParser.Model;
using IniParser.Model.Configuration;
using IniParser.Parser;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WindowsPrivateProfileStruct;
using Xunit;
using Xunit.Abstractions;

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
    public int format;
    public SIZE size;
    public POSITION pos;
    public int colordeep;
}
public class WindowsCompatibilityTests : IClassFixture<TempIniFileFixture>
{
    private readonly string _iniPath;
    private readonly ITestOutputHelper _output;

    public WindowsCompatibilityTests(TempIniFileFixture fixture, ITestOutputHelper output)
    {
        _iniPath = fixture.IniPath;
        _output = output;
    }

    [Fact]
    public void Your_ToHex_ShouldMatch_Windows_WritePrivateProfileStructA_Output()
    {
        // Arrange
        var original = new DatasetInfo
        {
            format = 100,
            size = new SIZE { width = 1920, height = 1080 },
            pos = new POSITION { left = 10, top = 20 },
            colordeep = 32
        };

        // Act 1: 用你的库序列化
        string yourHex = Struct.ToHex(original);

        // Act 2: 用 Windows API 写入同一个结构体
        int size = Marshal.SizeOf<DatasetInfo>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(original, ptr, false);
            bool written = Interop.WritePrivateProfileStructA("Test", "Data", ptr, (uint)size, _iniPath);
            Assert.True(written, "WritePrivateProfileStructA failed");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        // Act 3: 读取 Windows 写入的 hex 字符串（原样）
        var sb = new StringBuilder(1024);
        uint len = Interop.GetPrivateProfileStringA("Test", "Data", "", sb, (uint)sb.Capacity, _iniPath);
        string windowsHex = sb.ToString();

        // Assert: 两者应完全相同（包括校验和）
        Assert.Equal(yourHex, windowsHex);
    }

    [Fact]
    public void Your_FromHex_CanRead_Windows_WritePrivateProfileStructA_Output()
    {
        // Arrange
        var original = new DatasetInfo
        {
            format = 200,
            size = new SIZE { width = 1280, height = 720 },
            pos = new POSITION { left = 5, top = 15 },
            colordeep = 24
        };

        // Act: 用 Windows 写入
        int size = Marshal.SizeOf<DatasetInfo>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(original, ptr, false);
            bool written = Interop.WritePrivateProfileStructA("Test", "Data", ptr, (uint)size, _iniPath);
            Assert.True(written, "WritePrivateProfileStructA failed");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        // 读取 hex 字符串
        var sb = new StringBuilder(1024);
        Interop.GetPrivateProfileStringA("Test", "Data", "", sb, (uint)sb.Capacity, _iniPath);
        string windowsHex = sb.ToString();

        // 用你的库反序列化
        bool success = Struct.FromHex(windowsHex, out DatasetInfo restored);
        Assert.True(success, "Your FromHex failed on Windows-written data");

        // Assert content
        Assert.Equal(original.format, restored.format);
        Assert.Equal(original.size.width, restored.size.width);
        Assert.Equal(original.size.height, restored.size.height);
        Assert.Equal(original.pos.left, restored.pos.left);
        Assert.Equal(original.pos.top, restored.pos.top);
        Assert.Equal(original.colordeep, restored.colordeep);
    }

    [Fact]
    public void Roundtrip_YourWrite_Then_WindowsRead_Via_GetPrivateProfileStructA()
    {
        // Arrange
        var original = new DatasetInfo
        {
            format = 300,
            size = new SIZE { width = 800, height = 600 },
            pos = new POSITION { left = 0, top = 0 },
            colordeep = 16
        };

        // Act: 用你的 ToHex + ini-parser 写入（模拟 Windows 格式）
        string hex = Struct.ToHex(original);

        // 手动写入 INI 文件（格式：[Section]\nKey=HEX\n）
        File.WriteAllText(_iniPath, $"[Test]{Environment.NewLine}Data={hex}{Environment.NewLine}");

        // 用 Windows API 读取（它会自动校验并填充结构体）
        var readBack = new DatasetInfo();
        int size = Marshal.SizeOf<DatasetInfo>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(readBack, ptr, false); // 初始化内存
            bool success = Interop.GetPrivateProfileStructA("Test", "Data", ptr, (uint)size, _iniPath);
            Assert.True(success, "GetPrivateProfileStructA failed to read your-written data");

            readBack = Marshal.PtrToStructure<DatasetInfo>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        // Assert
        Assert.Equal(original.format, readBack.format);
        Assert.Equal(original.size.width, readBack.size.width);
        Assert.Equal(original.size.height, readBack.size.height);
        Assert.Equal(original.pos.left, readBack.pos.left);
        Assert.Equal(original.pos.top, readBack.pos.top);
        Assert.Equal(original.colordeep, readBack.colordeep);
    }

    // ====== 新增：模拟 ini-parser 的 INI 读写委托（用于测试） ======
    private static bool WriteIniValue(string section, string key, string value, string iniFile)
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

    private static string? ReadIniValue(string section, string key, string iniFile)
    {
        if (!File.Exists(iniFile)) return null;
        var parser = new FileIniDataParser();
        var data = parser.ReadFile(iniFile, Encoding.Default);
        return data[section][key];
    }

    // ====== 新增测试：Roundtrip via Struct.WritePrivateProfileStruct / GetPrivateProfileStruct ======
    [Fact]
    public void Roundtrip_Using_Struct_WritePrivateProfileStruct_And_GetPrivateProfileStruct()
    {
        // Arrange
        var original = new DatasetInfo
        {
            format = 400,
            size = new SIZE { width = 1024, height = 768 },
            pos = new POSITION { left = 30, top = 40 },
            colordeep = 32
        };

        // Act: Write using new high-level API
        bool written = Struct.WritePrivateProfileStruct(
            "Test", "Data", original, WriteIniValue, _iniPath);
        Assert.True(written);

        // Act: Read back using new high-level API
        bool readSuccess = Struct.GetPrivateProfileStruct(
            "Test", "Data", out DatasetInfo restored, ReadIniValue, _iniPath);
        Assert.True(readSuccess);

        // Assert
        Assert.Equal(original.format, restored.format);
        Assert.Equal(original.size.width, restored.size.width);
        Assert.Equal(original.size.height, restored.size.height);
        Assert.Equal(original.pos.left, restored.pos.left);
        Assert.Equal(original.pos.top, restored.pos.top);
        Assert.Equal(original.colordeep, restored.colordeep);
    }

    // ====== 新增测试：Your Struct.WritePrivateProfileStruct → Windows GetPrivateProfileStructA ======
    [Fact]
    public void Your_Struct_WritePrivateProfileStruct_CanBeReadBy_Windows_GetPrivateProfileStructA()
    {
        // Arrange
        var original = new DatasetInfo
        {
            format = 500,
            size = new SIZE { width = 1600, height = 900 },
            pos = new POSITION { left = 100, top = 200 },
            colordeep = 24
        };

        // Act: Write using your new high-level API (which uses ToHex internally)
        bool written = Struct.WritePrivateProfileStruct(
            "Test", "Data", original, WriteIniValue, _iniPath);
        Assert.True(written);

        // 🔍 手动读取写入的 HEX 内容用于诊断
        string? hexWritten = ReadIniValue("Test", "Data", _iniPath);

        // Act: Read using Windows API
        var readBack = new DatasetInfo();
        int size = Marshal.SizeOf<DatasetInfo>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(readBack, ptr, false); // init
            bool success = Interop.GetPrivateProfileStructA("Test", "Data", ptr, (uint)size, _iniPath);
            // ❌ 如果失败，输出详细上下文
            if (!success)
            {
                // 输出诊断信息
                _output.WriteLine("=== DIAGNOSTIC INFO ===");
                _output.WriteLine($"INI file path: {_iniPath}");
                _output.WriteLine($"Original struct: format={original.format}, size=({original.size.width},{original.size.height}), pos=({original.pos.left},{original.pos.top}), colordeep={original.colordeep}");
                _output.WriteLine($"Expected size (bytes): {size}");
                _output.WriteLine($"HEX written to INI: {hexWritten ?? "(null)"}");
                _output.WriteLine($"HEX length (chars): {hexWritten?.Length ?? 0} → bytes: {(hexWritten?.Length ?? 0) / 2}");

                // 可选：输出整个 INI 文件内容
                if (File.Exists(_iniPath))
                {
                    _output.WriteLine("--- Full INI Content ---");
                    _output.WriteLine(File.ReadAllText(_iniPath));
                    _output.WriteLine("------------------------");
                }

                // 明确失败
                Assert.True(success, "Windows GetPrivateProfileStructA failed. See diagnostic output above.");
            }
            readBack = Marshal.PtrToStructure<DatasetInfo>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        // Assert
        Assert.Equal(original.format, readBack.format);
        Assert.Equal(original.size.width, readBack.size.width);
        Assert.Equal(original.size.height, readBack.size.height);
        Assert.Equal(original.pos.left, readBack.pos.left);
        Assert.Equal(original.pos.top, readBack.pos.top);
        Assert.Equal(original.colordeep, readBack.colordeep);
    }

    // ====== 新增测试：Windows WritePrivateProfileStructA → Your Struct.GetPrivateProfileStruct ======
    [Fact]
    public void Windows_WritePrivateProfileStructA_CanBeReadBy_Your_Struct_GetPrivateProfileStruct()
    {
        // Arrange
        var original = new DatasetInfo
        {
            format = 600,
            size = new SIZE { width = 1366, height = 768 },
            pos = new POSITION { left = 50, top = 60 },
            colordeep = 16
        };

        // Act: Write using Windows API
        int size = Marshal.SizeOf<DatasetInfo>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(original, ptr, false);
            bool written = Interop.WritePrivateProfileStructA("Test", "Data", ptr, (uint)size, _iniPath);
            Assert.True(written, "WritePrivateProfileStructA failed");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }

        // Act: Read using your new high-level API
        bool success = Struct.GetPrivateProfileStruct(
            "Test", "Data", out DatasetInfo restored, ReadIniValue, _iniPath);
        Assert.True(success, "Your Struct.GetPrivateProfileStruct failed");

        // Assert
        Assert.Equal(original.format, restored.format);
        Assert.Equal(original.size.width, restored.size.width);
        Assert.Equal(original.size.height, restored.size.height);
        Assert.Equal(original.pos.left, restored.pos.left);
        Assert.Equal(original.pos.top, restored.pos.top);
        Assert.Equal(original.colordeep, restored.colordeep);
    }
}

// Fixture for temp INI file
public class TempIniFileFixture : IDisposable
{
    public string IniPath { get; }

    public TempIniFileFixture()
    {
        IniPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
    }

    public void Dispose()
    {
        if (File.Exists(IniPath))
            File.Delete(IniPath);
    }
}