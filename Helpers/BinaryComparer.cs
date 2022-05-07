namespace Faucet.Helpers;

public class BinaryComparer : IEqualityComparer<byte[]>, IComparer<byte[]>
  {
    public static BinaryComparer Default { get; } = new BinaryComparer();

    public unsafe int Compare(byte[] a1, byte[] a2)
    {
      int num = Math.Min(a1.Length, a2.Length);
      fixed (byte* numPtr1 = a1)
        fixed (byte* numPtr2 = a2)
        {
          byte* numPtr3 = numPtr1;
          byte* numPtr4 = numPtr2;
          byte* numPtr5 = numPtr3 + num;
          byte* numPtr6 = numPtr3 + a1.Length;
          byte* numPtr7 = numPtr4 + a2.Length;
          for (; numPtr3 < numPtr5 && (int) *numPtr3 == (int) *numPtr4; ++numPtr4)
            ++numPtr3;
          return numPtr3 == numPtr6 ? (numPtr4 != numPtr7 ? -1 : 0) : (numPtr4 == numPtr7 || (int) *numPtr3 >= (int) *numPtr4 ? 1 : -1);
        }
    }

    public unsafe bool Equals(byte[] a1, byte[] a2)
    {
      if (a1 == a2)
        return true;
      if (a1 == null || a2 == null || a1.Length != a2.Length)
        return false;
      fixed (byte* numPtr1 = a1)
        fixed (byte* numPtr2 = a2)
        {
          byte* numPtr3 = numPtr1;
          byte* numPtr4 = numPtr2;
          int length = a1.Length;
          int num = 0;
          while (num < length / 8)
          {
            if (*(long*) numPtr3 != *(long*) numPtr4)
              return false;
            ++num;
            numPtr3 += 8;
            numPtr4 += 8;
          }
          if ((length & 4) != 0)
          {
            if (*(int*) numPtr3 != *(int*) numPtr4)
              return false;
            numPtr3 += 4;
            numPtr4 += 4;
          }
          if ((length & 2) != 0)
          {
            if ((int) *(short*) numPtr3 != (int) *(short*) numPtr4)
              return false;
            numPtr3 += 2;
            numPtr4 += 2;
          }
          return (length & 1) == 0 || (int) *numPtr3 == (int) *numPtr4;
        }
    }

    public unsafe bool PrefixEquals(byte[] a1, byte[] a2, int prefix)
    {
      if (a1 == a2)
        return true;
      prefix = Math.Min(prefix, Math.Max(a1.Length, a2.Length));
      int num1 = Math.Min(prefix, a1.Length);
      int num2 = Math.Min(prefix, a2.Length);
      if (a1 == null || a2 == null || num1 != num2)
        return false;
      fixed (byte* numPtr1 = a1)
        fixed (byte* numPtr2 = a2)
        {
          byte* numPtr3 = numPtr1;
          byte* numPtr4 = numPtr2;
          int num3 = num1;
          int num4 = 0;
          while (num4 < num3 / 8)
          {
            if (*(long*) numPtr3 != *(long*) numPtr4)
              return false;
            ++num4;
            numPtr3 += 8;
            numPtr4 += 8;
          }
          if ((num3 & 4) != 0)
          {
            if (*(int*) numPtr3 != *(int*) numPtr4)
              return false;
            numPtr3 += 4;
            numPtr4 += 4;
          }
          if ((num3 & 2) != 0)
          {
            if ((int) *(short*) numPtr3 != (int) *(short*) numPtr4)
              return false;
            numPtr3 += 2;
            numPtr4 += 2;
          }
          return (num3 & 1) == 0 || (int) *numPtr3 == (int) *numPtr4;
        }
    }

    public int GetHashCode(byte[] obj) => MurMurHash3.Hash((Stream) new MemoryStream(obj));
  }