// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

namespace Faucet.Models;

public record BlockView
{
    public decimal Reward { get; set; }
    public ulong Height { get; set; }
    public ushort Size { get; set; }
    public ushort NrTx { get; set; }
    public uint Staked { get; set; }
}