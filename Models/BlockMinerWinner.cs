// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

namespace Faucet.Models;

public record BlockMinerWinner
{
    public string Id { get; init; }
    public ulong Height { get; init; }
    public string Hash { get; init; }   
    public string PublicKey { get; init; }
    public decimal Reward { get; init; }
    public ulong Solution { get; init; }
}