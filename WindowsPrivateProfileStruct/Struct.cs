using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsPrivateProfileStruct
{
    /// <summary>
    /// 提供结构体与带校验和的十六进制字符串之间的序列化与反序列化功能。
    /// 用于模拟 Windows API <c>WritePrivateProfileStructA</c>/<c>GetPrivateProfileStructA</c> 的跨平台行为。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 此类适用于纯值类型结构体（blittable types），不支持包含引用类型（如 string、数组、类）的结构体。
    /// </para>
    /// <para>
    /// 序列化格式：原始结构体字节 + 1 字节校验和（所有字节之和模 256），以大写十六进制字符串表示（无分隔符）。
    /// </para>
    /// <para>
    /// ⚠️ 结构体必须使用确定的内存布局（建议标注 <see cref="StructLayoutAttribute"/>），
    /// 并注意平台字节序（endianness）可能影响跨架构兼容性。
    /// </para>
    /// </remarks>
    public static class Struct
    {
        #region 主方法
        /// <summary>
        /// 将指定的结构体序列化为带校验和的十六进制字符串。
        /// </summary>
        /// <typeparam name="T">要序列化的结构体类型。必须是 blittable 的值类型。</typeparam>
        /// <param name="value">要序列化的结构体实例。</param>
        /// <returns>表示该结构体的十六进制字符串，末尾附加一字节校验和。</returns>
        /// <exception cref="ArgumentException">
        /// 当 <typeparamref name="T"/> 不是 blittable 类型，或包含非固定大小字段时可能抛出（由底层 Marshal 引发）。
        /// </exception>
        /// <example>
        /// <code>
        /// [StructLayout(LayoutKind.Sequential, Pack = 1)]
        /// struct Config { public int Width; public float Scale; }
        /// 
        /// var cfg = new Config { Width = 100, Scale = 1.5f };
        /// string hex = Struct.ToHex(cfg); // e.g., "640000000000C03F63"
        /// </code>
        /// </example>
        public static string ToHex<T>(T value) where T : struct
        {
            byte[] data = StructToBytes(value);
            byte checksum = ComputeChecksum(data);
            byte[] dataWithChecksum = new byte[data.Length + 1];
            Buffer.BlockCopy(data, 0, dataWithChecksum, 0, data.Length);
            dataWithChecksum[data.Length] = checksum;
            return BytesToHex(dataWithChecksum);
        }

        /// <summary>
        /// 从带校验和的十六进制字符串尝试反序列化为指定结构体。
        /// </summary>
        /// <typeparam name="T">目标结构体类型。必须与序列化时的类型一致。</typeparam>
        /// <param name="hex">由 <see cref="ToHex{T}"/> 生成的十六进制字符串。</param>
        /// <param name="value">成功时输出反序列化后的结构体；失败时为 default(T)。</param>
        /// <returns>
        /// <see langword="true"/> 如果字符串格式有效、长度匹配且校验和正确；
        /// 否则返回 <see langword="false"/>。
        /// </returns>
        /// <remarks>
        /// 验证包括：
        /// <list type="bullet">
        ///   <item><description>字符串为偶数长度且为有效十六进制；</description></item>
        ///   <item><description>数据长度等于 <c>Marshal.SizeOf&lt;T&gt;() + 1</c>；</description></item>
        ///   <item><description>末尾校验和与前 N 字节计算结果一致。</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// if (Struct.FromHex(hexString, out Config cfg))
        /// {
        ///     Console.WriteLine($"Width: {cfg.Width}");
        /// }
        /// </code>
        /// </example>
        public static bool FromHex<T>(string hex, out T value) where T : struct
        {
            value = default(T);

            if (string.IsNullOrEmpty(hex))
                return false;

            if (!TryParseHex(hex, out byte[] dataWithChecksum))
                return false;

            if (dataWithChecksum.Length < 1)
                return false;

            int expectedSize = Marshal.SizeOf<T>();
            if (dataWithChecksum.Length != expectedSize + 1)
                return false;

            byte receivedChecksum = dataWithChecksum[dataWithChecksum.Length - 1];
            byte[] originalData = new byte[expectedSize];
            Buffer.BlockCopy(dataWithChecksum, 0, originalData, 0, expectedSize);

            byte computedChecksum = ComputeChecksum(originalData);
            if (computedChecksum != receivedChecksum)
                return false;

            return TryBytesToStruct(originalData, out value);
        }
        #endregion

        #region 模仿 Windows 原版写法

        /// <summary>
        /// 将结构体写入 INI 文件中的指定键，模拟 Windows API WritePrivateProfileStructA 的行为。
        /// </summary>
        /// <typeparam name="T">要写入的结构体类型（必须为 blittable 且使用 Sequential 布局）。</typeparam>
        /// <param name="lpszSection">INI 文件中的节名称（例如 "Window"）。</param>
        /// <param name="lpszKey">INI 文件中的键名称（例如 "Info"）。</param>
        /// <param name="value">要写入的结构体实例。</param>
        /// <param name="writeIniValue">
        /// 用于实际写入 INI 文件的委托。  
        /// 签名：(section, key, hexString, iniFilePath) => 是否成功  
        /// 你可在此委托内部使用 ini-parser、自定义解析器等。
        /// </param>
        /// <param name="szFile">INI 文件的完整路径。</param>
        /// <returns>如果成功写入并序列化，则返回 true；否则返回 false。</returns>
        /// <exception cref="ArgumentException">当节名、键名或文件路径为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">当 writeIniValue 委托为 null 时抛出。</exception>
        public static bool WritePrivateProfileStruct<T>(
            string lpszSection,
            string lpszKey,
            T value,
            IniWriteDelegate writeIniValue,
            string szFile)
            where T : struct
        {
            if (writeIniValue == null)
                throw new ArgumentNullException(nameof(writeIniValue));

            try
            {
                string hex = ToHex(value);
                return writeIniValue(lpszSection, lpszKey, hex, szFile);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从 INI 文件中的指定键读取结构体，模拟 Windows API GetPrivateProfileStructA 的行为。
        /// </summary>
        /// <typeparam name="T">要读取的结构体类型（必须与写入时一致）。</typeparam>
        /// <param name="lpszSection">INI 文件中的节名称。</param>
        /// <param name="lpszKey">INI 文件中的键名称。</param>
        /// <param name="value">成功时输出的结构体实例。</param>
        /// <param name="readIniValue">
        /// 用于实际从 INI 文件读取字符串的委托。  
        /// 签名：(section, key, iniFilePath) => 十六进制字符串 或 null（未找到时）  
        /// 请确保返回的是原始 HEX 字符串（如 "64000000..."）。
        /// </param>
        /// <param name="szFile">INI 文件的完整路径。</param>
        /// <returns>
        /// 如果成功读取、长度匹配且校验和正确，则返回 true；  
        /// 否则（包括键不存在、HEX 格式错误、校验失败等）返回 false。
        /// </returns>
        /// <exception cref="ArgumentException">当节名、键名或文件路径为空时抛出。</exception>
        /// <exception cref="ArgumentNullException">当 readIniValue 委托为 null 时抛出。</exception>
        public static bool GetPrivateProfileStruct<T>(
            string lpszSection,
            string lpszKey,
            out T value,
            IniReadDelegate readIniValue,
            string szFile)
            where T : struct
        {
            value = default(T);

            if (readIniValue == null)
                throw new ArgumentNullException(nameof(readIniValue));

            try
            {
                string hex = readIniValue(lpszSection, lpszKey, szFile);
                if (string.IsNullOrEmpty(hex))
                    return false;

                return FromHex(hex, out value);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 定义写入 INI 文件的委托。
        /// </summary>
        /// <param name="section">节名称。</param>
        /// <param name="key">键名称。</param>
        /// <param name="value">要写入的字符串值（通常是十六进制）。</param>
        /// <param name="iniFile">INI 文件路径。</param>
        /// <returns>操作是否成功。</returns>
        public delegate bool IniWriteDelegate(string section, string key, string value, string iniFile);

        /// <summary>
        /// 定义从 INI 文件读取值的委托。
        /// </summary>
        /// <param name="section">节名称。</param>
        /// <param name="key">键名称。</param>
        /// <param name="iniFile">INI 文件路径。</param>
        /// <returns>读取到的字符串，若未找到则返回 null 或空字符串。</returns>
        public delegate string IniReadDelegate(string section, string key, string iniFile);
        #endregion

        #region 辅助方法（私有）

        private static byte[] StructToBytes<T>(T value) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(value, ptr, false);
                byte[] data = new byte[size];
                Marshal.Copy(ptr, data, 0, size);
                return data;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static bool TryBytesToStruct<T>(byte[] data, out T value) where T : struct
        {
            value = default(T);
            if (data == null || data.Length != Marshal.SizeOf<T>())
                return false;

            IntPtr ptr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, ptr, data.Length);
                value = Marshal.PtrToStructure<T>(ptr);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static byte ComputeChecksum(byte[] data)
        {
            byte sum = 0;
            foreach (byte b in data)
                sum += b;
            return sum;
        }

        private static string BytesToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        private static bool TryParseHex(string hex, out byte[] result)
        {
            result = null;
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
                return false;

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                    return false;
            }
            result = bytes;
            return true;
        }
        #endregion
    }
}