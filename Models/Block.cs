// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using MessagePack;

namespace Faucet.Models;

[MessagePackObject]
public record Block
{
    [Key(0)] public byte[] Hash { get; set; }
    [Key(1)] public ulong Height { get; init; }
    [Key(2)] public ushort Size { get; set; }
    [Key(3)] public BlockHeader BlockHeader { get; init; }
    [Key(4)] public ushort NrTx { get; init; }
    [Key(5)] public IList<Transaction> Txs { get; init; } = new List<Transaction>();
    [Key(6)] public BlockPoS BlockPos { get; init; }
}