using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Numerics;
using System.Reflection;
using Microsoft.IO;

namespace Faucet.Helpers;

public static class Utils
{
    public static readonly RecyclableMemoryStreamManager Manager = new();
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool Xor(this byte[] a, byte[] b)
    {
        var x = a.Length ^ b.Length;
        for (var i = 0; i < a.Length && i < b.Length; ++i) x |= a[i] ^ b[i];
        return x == 0;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="secureString"></param>
    /// <returns></returns>
    public static string ToUnSecureString(this SecureString secureString)
    {
        var unmanagedString = IntPtr.Zero;
        try
        {
            unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
            return Marshal.PtrToStringUni(unmanagedString);
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public static void ZeroString(this string value)
    {
        var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
        unsafe
        {
            var pValue = (char*)handle.AddrOfPinnedObject();
            for (var index = 0; index < value.Length; index++) pValue![index] = char.MinValue;
        }

        handle.Free();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string ByteToHex(this byte[] data)
    {
        return Convert.ToHexString(data);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static SecureString ToSecureString(this string value)
    {
        var secureString = new SecureString();
        Array.ForEach(value.ToArray(), secureString.AppendChar);
        secureString.MakeReadOnly();
        return secureString;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="hex"></param>
    /// <returns></returns>
    public static byte[] HexToByte(this string hex)
    {
        return Convert.FromHexString(hex);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static ulong MulWithNanoTan(this ulong value)
    {
        return value * 1000_000_000;
    }

    /// <summary>
    /// 
    /// </summary>
    private static readonly DateTimeOffset UnixRef = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    public static DateTimeOffset UnixTimeToDateTime(long timestamp)
    {
        var span = TimeSpan.FromSeconds(timestamp);
        return UnixRef + span;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="list"></param>
    /// <typeparam name="T"></typeparam>
    public static void Shuffle<T>(this IList<T> list)
    {
        var random = new Random();
        var count = list.Count;
        while (count > 1)
        {
            --count;
            var index = random.Next(count + 1);
            (list[index], list[count]) = (list[count], list[index]);
        }
    }
    
    /// <summary>
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static uint DateTimeToUnixTime(DateTimeOffset dt)
    {
        return (uint)DateTimeToUnixTimeLong(dt);
    }

    /// <summary>
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static ulong DateTimeToUnixTimeLong(DateTimeOffset dt)
    {
        dt = dt.ToUniversalTime();
        if (dt < UnixRef)
            throw new ArgumentOutOfRangeException(nameof(dt));
        var result = (dt - UnixRef).TotalSeconds;
        if (result > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(dt));
        return (ulong)result;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="hex"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static byte[] HexToByte<T>(this T hex)
    {
        return Convert.FromHexString(hex.ToString()!);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static byte[] ToBytes(this string value)
    {
        return Encoding.UTF8.GetBytes(value ?? string.Empty, 0, value!.Length);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static byte[] ToBytes(this long value)
    {
        return Encoding.UTF8.GetBytes(value.ToString(), 0, value!.ToString().Length);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static byte[] ToBytes(this ulong value)
    {
        return Encoding.UTF8.GetBytes(value.ToString(), 0, value!.ToString().Length);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static byte[] ToBytes(this int value)
    {
        return Encoding.UTF8.GetBytes(value.ToString(), 0, value!.ToString().Length);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string FromBytes(this byte[] data)
    {
        return Encoding.UTF8.GetString(data);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static DateTime GetAdjustedTime()
    {
        return GetUtcNow().Add(TimeSpan.Zero);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static long GetAdjustedTimeAsUnixTimestamp()
    {
        return new DateTimeOffset(GetAdjustedTime()).ToUnixTimeSeconds();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="n"></param>
    /// <returns></returns>
    public static BigInteger Mod(BigInteger a, BigInteger n)
    {
        var result = a % n;
        if (result < 0 && n > 0 || result > 0 && n < 0)
        {
            result += n;
        }

        return result;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static string EntryAssemblyPath()
    {
        return Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="arrays"></param>
    /// <returns></returns>
    public static byte[] Combine(params byte[][] arrays)
    {
        var ret = new byte[arrays.Sum(x => x.Length)];
        var offset = 0;
        foreach (var data in arrays)
        {
            Buffer.BlockCopy(data, 0, ret, offset, data.Length);
            offset += data.Length;
        }

        return ret;
    }
}