// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using MessagePack;

namespace Faucet.Models;

/// <summary>
/// 
/// </summary>

[MessagePackObject]
public record DataProtection
{
    [Key(0)] public string FriendlyName { get; set; }
    [Key(1)] public string Payload { get; set; }
}