// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using Blake3;
using Faucet.Helpers;
using MessagePack;

namespace Faucet.Models;

/// <summary>
/// 
/// </summary>
[MessagePackObject]
public record BlockMinerProofWinner
{
    [Key(0)] public byte[] Id { get; init; } = Hasher.Hash(Guid.NewGuid().ToByteArray()).HexToByte();
    [Key(1)] public ulong Height { get; init; }
    [Key(2)] public byte[] Hash { get; init; }   
    [Key(3)] public byte[] PublicKey { get; init; }
    [Key(4)] public  byte[] Address { get; init; }
    [Key(5)] public ulong Reward { get; init; }
    [Key(6)] public byte[] TxId { get; init; }
    [Key(7)] public ulong Solution { get; init; }
    [Key(8)] public bool Paid { get; init; }
    [Key(9)] public long PayoutTimestamp { get; init; }
    [Key(10)] public long Timestamp { get; } = Utils.GetAdjustedTimeAsUnixTimestamp();
}