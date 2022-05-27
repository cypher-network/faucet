// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using MessagePack;

namespace Faucet.Models;

/// <summary>
/// 
/// </summary>
/// <param name="TxId"></param>
/// <param name="Amount"></param>
[MessagePackObject]
public record Reward([property: Key(0)] byte[] TxId, [property:Key(1)] int Amount);