// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Reflection;
using System.Security;
using Faucet.Data;
using Faucet.Helpers;
using Faucet.Models;
using Faucet.Persistence;
using Dawn;
using MessagePack;
using NBitcoin;
using Newtonsoft.Json;
using Block = Faucet.Models.Block;
using Transaction = Faucet.Models.Transaction;
using Utils = Faucet.Helpers.Utils;

namespace Faucet.Wallet;

public record Consumed(byte[] Commit, DateTime Time)
{
    public readonly DateTime Time = Time;
    public readonly byte[] Commit = Commit;
}

public class WalletSession : IWalletSession
{
    private const string HardwarePath = "m/44'/847177'/0'/0/";
    
    public Caching<Output> CacheTransactions { get; } = new();
    public Caching<Consumed> CacheConsumed { get; } = new();
    public Output Spending { get; set; }
    public SecureString Seed { get; set; }
    public SecureString Passphrase { get; set; }
    public string SenderAddress { get; set; }
    public string RecipientAddress { get; set; }
    public SecureString KeySet { get; set; }
    public ulong Amount { get; set; }
    public ulong Change { get; set; }
    public ulong Reward { get; set; }

    private static readonly object Locking = new();
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly DataService _dataService;
    private readonly ILogger _logger;
    private LoginData _loginData;
    private readonly object _readOnlySafeGuardLock = new();
    private IReadOnlyList<Block> _readOnlySafeGuardBlocks;
    private readonly Random _random = new();
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="applicationLifetime"></param>
    /// <param name="logger"></param>
    public WalletSession(DataService dataService, IHostApplicationLifetime applicationLifetime, ILogger<WalletSession> logger)
    {
        _dataService = dataService;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        Init();
    }
    
    /// <summary>
    /// 
    /// </summary>
    private void Init()
    {
        InvokeAsync(async () =>
        {
            var path = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "login.json");
            if (File.Exists(path))
            {
                using var reader = new StreamReader(path);
                var json = reader.ReadToEnd();
                _loginData = JsonConvert.DeserializeObject<LoginData>(json);
                await Login(_loginData.Seed, _loginData.Passphrase);
                await InitializeWallet(_loginData.Outputs);
            }
        });
        
        HandleSafeguardBlocks();
        HandelConsumed();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="workItem"></param>
    private static void InvokeAsync(Func<Task> workItem)
    {
        workItem.Invoke();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public int GetNextAmount()
    {
        return _random.Next(1, 5);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="transactions"></param>
    public void Notify(Transaction[] transactions)
    {
        if (KeySet is null) return;
        lock (Locking)
        {
            foreach (var consumed in CacheConsumed.GetItems())
            {
                var transaction = transactions.FirstOrDefault(t => t.Vout.Any(c => c.C.Xor(consumed.Commit)));
                if (transaction is null) continue;
                CacheConsumed.Remove(consumed.Commit);
                CacheTransactions.Remove(consumed.Commit);
                break;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="seed"></param>
    /// <param name="passphrase"></param>
    /// <returns></returns>
    public Task<Tuple<bool, string>> Login(string seed, string passphrase)
    {
        Guard.Argument(seed, nameof(seed)).NotNull().NotWhiteSpace().NotEmpty();
        Guard.Argument(passphrase, nameof(passphrase)).NotNull().NotWhiteSpace().NotEmpty();
        try
        {
            Seed = seed.ToSecureString();
            Passphrase = passphrase.ToSecureString();
            seed.ZeroString();
            passphrase.ZeroString();
            CreateHdRootKey(Seed, Passphrase, out var rootKey);
            var keySet = CreateKeySet(new KeyPath($"{HardwarePath}0"), rootKey.PrivateKey.ToHex().HexToByte(),
                rootKey.ChainCode);
            SenderAddress = keySet.StealthAddress;
            KeySet = MessagePackSerializer.Serialize(keySet).ByteToHex().ToSecureString();
            keySet.ChainCode.ZeroString();
            keySet.KeyPath.ZeroString();
            keySet.RootKey.ZeroString();
            return Task.FromResult(new Tuple<bool, string>(true, "Wallet login successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }

        return Task.FromResult(new Tuple<bool, string>(false, "Unable to login"));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="outputs"></param>
    /// <returns></returns>
    public Task<Tuple<bool, string>> InitializeWallet(Output[] outputs)
    {
        Guard.Argument(outputs, nameof(outputs)).NotNull().NotEmpty();
        try
        {
            if (KeySet is null)
                return Task.FromResult(new Tuple<bool, string>(false, "Node wallet login required"));
            CacheTransactions.Clear();
            foreach (var vout in outputs) CacheTransactions.Add(vout.C.HexToByte(), vout);
            return Task.FromResult(new Tuple<bool, string>(true, "Node wallet received transactions"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }

        return Task.FromResult(new Tuple<bool, string>(false, "Node wallet setup failed"));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<Block> GetSafeGuardBlocks()
    {
        lock (Locking)
        {
            return _readOnlySafeGuardBlocks;   
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="keyPath"></param>
    /// <param name="secretKey"></param>
    /// <param name="chainCode"></param>
    /// <returns></returns>
    private static KeySet CreateKeySet(KeyPath keyPath, byte[] secretKey, byte[] chainCode)
    {
        Guard.Argument(keyPath, nameof(keyPath)).NotNull();
        Guard.Argument(secretKey, nameof(secretKey)).NotNull().MaxCount(32);
        Guard.Argument(chainCode, nameof(chainCode)).NotNull().MaxCount(32);
        var masterKey = new ExtKey(new Key(secretKey), chainCode);
        var spendKey = masterKey.Derive(keyPath).PrivateKey;
        var scanKey = masterKey.Derive(keyPath = keyPath.Increment()).PrivateKey;
        return new KeySet
        {
            ChainCode = masterKey.ChainCode.ByteToHex(),
            KeyPath = keyPath.ToString(),
            RootKey = masterKey.PrivateKey.ToHex(),
            StealthAddress = spendKey.PubKey.CreateStealthAddress(scanKey.PubKey, Network.TestNet).ToString()
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="seed"></param>
    /// <param name="passphrase"></param>
    /// <param name="hdRoot"></param>
    private static void CreateHdRootKey(SecureString seed, SecureString passphrase, out ExtKey hdRoot)
    {
        Guard.Argument(seed, nameof(seed)).NotNull();
        Guard.Argument(passphrase, nameof(passphrase)).NotNull();
        var concatenateMnemonic = string.Join(" ", seed.ToUnSecureString());
        hdRoot = new Mnemonic(concatenateMnemonic).DeriveExtKey(passphrase.ToUnSecureString());
        concatenateMnemonic.ZeroString();
    }
    
    /// <summary>
    /// 
    /// </summary>
    private void HandelConsumed()
    {
        Task.Run(() =>
        {
            while (!_applicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                try
                {
                    lock (Locking)
                    {
                        var removeUnused = Utils.GetUtcNow().AddSeconds(-30);
                        foreach (var consumed in CacheConsumed.GetItems())
                        {
                            if (consumed.Time < removeUnused)
                            {
                                CacheConsumed.Remove(consumed.Commit);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    var msg = e.Message;
                }
                finally
                {
                    Thread.Sleep(10000);
                }
            }
        });
    }
    
    /// <summary>
    /// 
    /// </summary>
    private void HandleSafeguardBlocks()
    {
        Task.Run(async () =>
        {
            while (!_applicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var blocks = await _dataService.GetSafeGuardBlocks();
                if (!blocks.Any()) return;
    
                lock (_readOnlySafeGuardLock)
                {
                    _readOnlySafeGuardBlocks = blocks;
                }
    
                // Wait 1.8 days before we check for new blocks
                await Task.Delay(155520000, _applicationLifetime.ApplicationStopped);
            }
        });
    }
}