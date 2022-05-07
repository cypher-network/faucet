// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

namespace Faucet.Models;

public record LoginData
{
    public string Seed { get; set; }
    public string Passphrase { get; set; }
    public Output[] Outputs { get; set; }
}