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

        // ------------------- 辅助方法（私有）-------------------

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
    }
}