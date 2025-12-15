using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class Interop
{
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern bool WritePrivateProfileStructA(
        string lpSection,
        string lpKey,
        IntPtr lpStruct,
        uint uSizeStruct,
        string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern bool GetPrivateProfileStructA(
        string lpSection,
        string lpKey,
        IntPtr lpStruct,
        uint uSizeStruct,
        string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern uint GetPrivateProfileStringA(
        string lpSection,
        string lpKey,
        string lpDefault,
        StringBuilder lpReturnedString,
        uint nSize,
        string lpFileName);
}