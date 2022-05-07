using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Numerics;

namespace Faucet.Helpers;

public static class Utils
{
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }
    
    public static bool Xor(this byte[] a, byte[] b)
    {
        var x = a.Length ^ b.Length;
        for (var i = 0; i < a.Length && i < b.Length; ++i) x |= a[i] ^ b[i];
        return x == 0;
    }
    
    public static DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
    
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
    
    public static string ByteToHex(this byte[] data)
    {
        return Convert.ToHexString(data);
    }
    
    public static SecureString ToSecureString(this string value)
    {
        var secureString = new SecureString();
        Array.ForEach(value.ToArray(), secureString.AppendChar);
        secureString.MakeReadOnly();
        return secureString;
    }
    
    public static byte[] HexToByte(this string hex)
    {
        return Convert.FromHexString(hex);
    }
    
    public static ulong MulWithNanoTan(this ulong value)
    {
        return value * 1000_000_000;
    }

    private static readonly DateTimeOffset UnixRef = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static DateTimeOffset UnixTimeToDateTime(long timestamp)
    {
        var span = TimeSpan.FromSeconds(timestamp);
        return UnixRef + span;
    }
    
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
    
    public static byte[] HexToByte<T>(this T hex)
    {
        return Convert.FromHexString(hex.ToString()!);
    }
    
    public static byte[] ToBytes(this string value)
    {
        return Encoding.UTF8.GetBytes(value ?? string.Empty, 0, value!.Length);
    }
    

    public static DateTime GetAdjustedTime()
    {
        return GetUtcNow().Add(TimeSpan.Zero);
    }

    public static long GetAdjustedTimeAsUnixTimestamp()
    {
        return new DateTimeOffset(GetAdjustedTime()).ToUnixTimeSeconds();
    }
    
    public static BigInteger Mod(BigInteger a, BigInteger n)
    {
        var result = a % n;
        if (result < 0 && n > 0 || result > 0 && n < 0)
        {
            result += n;
        }

        return result;
    }
}