namespace Faucet.Helpers;

public static class MurMurHash3
{
    private const uint seed = 144;

    public static int Hash(Stream stream)
    {
        uint x = 144;
        uint num1 = 0;
        using (BinaryReader binaryReader = new BinaryReader(stream))
        {
            for (byte[] numArray = binaryReader.ReadBytes(4); numArray.Length != 0; numArray = binaryReader.ReadBytes(4))
            {
                num1 += (uint) numArray.Length;
                switch (numArray.Length)
                {
                    case 1:
                        uint num2 = MurMurHash3.Rotl32((uint) numArray[0] * 3432918353U, (byte) 15) * 461845907U;
                        x ^= num2;
                        break;
                    case 2:
                        uint num3 = MurMurHash3.Rotl32(((uint) numArray[0] | (uint) numArray[1] << 8) * 3432918353U, (byte) 15) * 461845907U;
                        x ^= num3;
                        break;
                    case 3:
                        uint num4 = MurMurHash3.Rotl32((uint) ((int) numArray[0] | (int) numArray[1] << 8 | (int) numArray[2] << 16) * 3432918353U, (byte) 15) * 461845907U;
                        x ^= num4;
                        break;
                    case 4:
                        uint num5 = MurMurHash3.Rotl32((uint) ((int) numArray[0] | (int) numArray[1] << 8 | (int) numArray[2] << 16 | (int) numArray[3] << 24) * 3432918353U, (byte) 15) * 461845907U;
                        x ^= num5;
                        x = MurMurHash3.Rotl32(x, (byte) 13);
                        x = (uint) ((int) x * 5 - 430675100);
                        break;
                }
            }
        }
        return (int) MurMurHash3.Fmix(x ^ num1);
    }

    private static uint Rotl32(uint x, byte r) => x << (int) r | x >> 32 - (int) r;

    private static uint Fmix(uint h)
    {
        h ^= h >> 16;
        h *= 2246822507U;
        h ^= h >> 13;
        h *= 3266489909U;
        h ^= h >> 16;
        return h;
    }
}