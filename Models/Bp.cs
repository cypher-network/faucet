// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using MessagePack;

namespace Faucet.Models;

[MessagePackObject]
public record Bp
{
    [Key(0)] public byte[] Proof { get; set; }
}