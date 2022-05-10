// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Security;
using Faucet.Models;
using Faucet.Persistence;

namespace Faucet.Wallet;

public interface IWalletSession
{
    public Caching<Output> CacheTransactions { get; }
    public Caching<Consumed> CacheConsumed { get; }
    public Output Spending { get; set; }
    public SecureString Seed { get; set; }
    public SecureString Passphrase { get; set; }
    public string SenderAddress { get; set; }
    public string RecipientAddress { get; set; }
    public SecureString KeySet { get; set; }
    public ulong Amount { get; set; }
    public ulong Change { get; set; }
    public ulong Reward { get; set; }
    void Notify(Transaction[] transactions);
    Task<Tuple<bool, string>> Login(string seed, string passphrase);
    Task<Tuple<bool, string>> InitializeWallet(Output[] outputs);
    IReadOnlyList<Block> GetSafeGuardBlocks();
    int GetNextAmount();
}