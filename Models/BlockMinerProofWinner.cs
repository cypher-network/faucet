// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using MessagePack;

namespace Faucet.Models;

/// <summary>
/// 
/// </summary>
[MessagePackObject]
public record BlockMinerProofWinner
{
    [Key(0)] public byte[] Hash { get; init; }   
    [Key(1)] public byte[] PublicKey { get; init; }
    [Key(2)] public  byte[] Address { get; init; }
    [Key(3)] public int Reward { get; init; }
    [Key(4)] public byte[] TxId { get; init; }
}