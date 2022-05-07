// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using MessagePack;

namespace Faucet.Models;

[MessagePackObject]
public record Output 
{
    [Key(0)] public string C { get; set; }
    [Key(1)] public string E { get; set; }
    [Key(2)] public string N { get; set; }
    [Key(3)] public sbyte T { get; set; }
}