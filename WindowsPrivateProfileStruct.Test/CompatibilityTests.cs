using IniParser;
using IniParser.Model;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WindowsPrivateProfileStruct;
using Xunit;

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

    public WindowsCompatibilityTests(TempIniFileFixture fixture)
    {
        _iniPath = fixture.IniPath;
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