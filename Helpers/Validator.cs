// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Globalization;
using System.Text;
using Blake3;
using Dawn;
using Faucet.Cryptography;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;

namespace Faucet.Helpers;

/// <summary>
/// 
/// </summary>
public static class Validator
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="calculateVrfSig"></param>
    /// <param name="kernel"></param>
    /// <returns></returns>
    public static bool VerifyKernel(ReadOnlySpan<byte> calculateVrfSig, ReadOnlySpan<byte> kernel)
    {
        var v = new BigInteger(Hasher.Hash(calculateVrfSig).HexToByte());
        var T = new BigInteger(kernel.ToArray());
        return v.CompareTo(T) <= 0;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="prevHash"></param>
    /// <param name="hash"></param>
    /// <param name="lockTime"></param>
    /// <returns></returns>
    public static byte[] Kernel(ReadOnlySpan<byte> prevHash, ReadOnlySpan<byte> hash, long lockTime)
    {
        var txHashBig = new BigInteger(1, hash.ToArray()).Multiply(
            new BigInteger(Hasher.Hash(prevHash).HexToByte()).Multiply(
                new BigInteger(Hasher.Hash(lockTime.ToBytes()).HexToByte())));
        var kernel = Hasher.Hash(txHashBig.ToByteArray()).HexToByte();
        return kernel;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="vrfBytes"></param>
    /// <param name="kernel"></param>
    /// <param name="solution"></param>
    /// <returns></returns>
    public static bool VerifySolution(ReadOnlySpan<byte> vrfBytes, ReadOnlySpan<byte> kernel, ulong solution)
    {
        bool isSolution;
        try
        {
            var target = new BigInteger(1, Hasher.Hash(vrfBytes).HexToByte());
            var weight = BigInteger.ValueOf(Convert.ToInt64(solution));
            var hashTarget = new BigInteger(1, kernel.ToArray());
            var weightedTarget = target.Multiply(weight);
            isSolution = hashTarget.CompareTo(weightedTarget) <= 0;
        }
        catch (Exception ex)
        {
            return false;
        }
        return isSolution;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="solution"></param>
    /// <returns></returns>
    public static uint Bits(ulong solution)
    {
        var diff = Math.Truncate(solution * 25.0 / 8192);
        diff = diff == 0 ? 1 : diff;
        return (uint)diff;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="t"></param>
    /// <param name="message"></param>
    /// <param name="nonce"></param>
    /// <returns></returns>
    public static bool VerifySloth(uint t, ReadOnlySpan<byte> message, byte[] nonce)
    {
        try
        {
            var ct = new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token;
            var sloth = new Sloth(0, ct);
            var x = System.Numerics.BigInteger.Parse(message.ToArray().ByteToHex(), NumberStyles.AllowHexSpecifier);
            var y = System.Numerics.BigInteger.Parse(nonce.FromBytes());
            if (x.Sign <= 0) x = -x;
            var verifySloth = sloth.Verify(t, x, y);
            return verifySloth;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="script"></param>
    /// <returns></returns>
    public static bool VerifyLockTime(LockTime target, ReadOnlySpan<byte> script)
    {
        Guard.Argument(target, nameof(target)).NotDefault();
        var scr = Encoding.UTF8.GetString(script);
        var sc1 = new Script(Op.GetPushOp(target.Value), OpcodeType.OP_CHECKLOCKTIMEVERIFY);
        var sc2 = new Script(scr);
        if (!sc1.ToBytes().Xor(sc2.ToBytes())) return false;
        var tx = NBitcoin.Network.Main.CreateTransaction();
        tx.Outputs.Add(new TxOut { ScriptPubKey = new Script(scr) });
        var spending = NBitcoin.Network.Main.CreateTransaction();
        spending.LockTime = new LockTime(DateTimeOffset.UtcNow);
        spending.Inputs.Add(new TxIn(tx.Outputs.AsCoins().First().Outpoint, new Script()));
        spending.Inputs[0].Sequence = 1;
        return spending.Inputs.AsIndexedInputs().First().VerifyScript(tx.Outputs[0]);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="prev"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public static byte[] IncrementHash(ReadOnlySpan<byte> prev, ReadOnlySpan<byte> next)
    {
        var hasher = Hasher.New();
        hasher.Update(prev);
        hasher.Update(next);
        var hash = hasher.Finalize();
        return hash.AsSpanUnsafe().ToArray();
    }

}