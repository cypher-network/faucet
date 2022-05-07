// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using Blake3;
using Faucet.Helpers;
using MessagePack;

namespace Faucet.Models;

[MessagePackObject]
public record Transaction
{
    [Key(0)] public byte[] TxnId { get; set; }
    [Key(1)] public Bp[] Bp { get; set; }
    [Key(2)] public int Ver { get; set; }
    [Key(3)] public int Mix { get; set; }
    [Key(4)] public Vin[] Vin { get; set; }
    [Key(5)] public Vout[] Vout { get; set; }
    [Key(6)] public Rct[] Rct { get; set; }
    [Key(7)] public Vtime? Vtime { get; set; }
    
    /// <summary>
    /// </summary>
    /// <returns></returns>
    public byte[] ToIdentifier()
    {
        return ToHash().ByteToHex().ToBytes();
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public byte[] ToHash()
    {
        return Hasher.Hash(ToStream()).HexToByte();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int GetSize()
    {
        return ToStream().Length;
    }
    
    /// <summary>
    /// </summary>
    /// <returns></returns>
    public byte[] ToStream()
    {
        using var ts = new BufferStream();
        ts
            .Append(Mix)
            .Append(Ver);

        foreach (var bp in Bp) ts.Append(bp.Proof);

        foreach (var vin in Vin)
        {
            ts.Append(vin.Image);
            ts.Append(vin.Offsets);
        }

        foreach (var vout in Vout)
        {
            ts
                .Append(vout.A)
                .Append(vout.C)
                .Append(vout.E)
                .Append(vout.L)
                .Append(vout.N)
                .Append(vout.P)
                .Append(vout.S ?? Array.Empty<byte>())
                .Append(vout.T.ToString());

            if (vout.T is not (CoinType.Coinbase or CoinType.Coinstake))
                ts.Append(vout.D ?? Array.Empty<byte>());
            else
                ts.Append(vout.D);
        }

        foreach (var rct in Rct)
            ts
                .Append(rct.I)
                .Append(rct.M)
                .Append(rct.P)
                .Append(rct.S);

        if (Vtime != null)
            ts
                .Append(Vtime.I)
                .Append(Vtime.L)
                .Append(Vtime.M)
                .Append(Vtime.N)
                .Append(Vtime.S)
                .Append(Vtime.W);

        return ts.ToArray();
    }
}