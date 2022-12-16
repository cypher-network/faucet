// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Security;
using Faucet.Cryptography;
using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Ledger;
using Faucet.Persistence;
using Faucet.Wallet;
using ILogger = Serilog.ILogger;

namespace Faucet;

/// <summary>
/// 
/// </summary>
public interface IFaucetSystem
{
    IServiceScopeFactory ServiceScopeFactory { get; }
    IUnitOfWork UnitOfWork();
    ICrypto Crypto();
    public SecureString PrivateKey { get; init; }
    public byte[] PublicKey { get; init; }
    IWallet Wallet();
    IWalletSession WalletSession();
    IBlockchain Blockchain();
    int UserOnlineCount { get; set; }
}

/// <summary>
/// 
/// </summary>
public class FaucetSystem : IFaucetSystem
{
    private readonly ILogger _logger;
    private IUnitOfWork _unitOfWork;
    private IWalletSession _walletSession;
    private IBlockchain _blockchain;
    private ICrypto _crypto;
    
    public SecureString PrivateKey { get; init; }
    public byte[] PublicKey { get; init; }

    public IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="logger"></param>
    public FaucetSystem(IServiceScopeFactory serviceScopeFactory, ILogger logger)
    {
        ServiceScopeFactory = serviceScopeFactory;
        _logger = logger;
        var keyPair =  Crypto().GetOrUpsertKeyName().Result;
        PrivateKey = keyPair.PrivateKey.ByteToHex().ToSecureString();
        PublicKey = keyPair.PublicKey;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IBlockchain Blockchain()
    {
        if (_blockchain != null) return _blockchain;
        using var scope = ServiceScopeFactory.CreateAsyncScope();
        _blockchain = scope.ServiceProvider.GetRequiredService<IBlockchain>();
        return _blockchain;
    }

    /// <summary>
    /// 
    /// </summary>
    public int UserOnlineCount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IUnitOfWork UnitOfWork()
    {
        return _unitOfWork ??= GetUnitOfWork();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ICrypto Crypto()
    {
        try
        {
            if (_crypto != null) return _crypto;
            using var scope = ServiceScopeFactory.CreateAsyncScope();
            _crypto = scope.ServiceProvider.GetRequiredService<ICrypto>();
            return _crypto;
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}",ex.Message);
        }

        return null;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IWallet Wallet()
    {
        try
        {
            using var scope = ServiceScopeFactory.CreateAsyncScope();
            var wallet = scope.ServiceProvider.GetRequiredService<IWallet>();
            return wallet;
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}",ex.Message);
        }

        return null;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IWalletSession WalletSession()
    {
        return _walletSession ??= GetWalletSession();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private IUnitOfWork GetUnitOfWork()
    {
        try
        {
            using var scope = ServiceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return unitOfWork;
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}",ex.Message);
        }

        return null;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private IWalletSession GetWalletSession()
    {
        try
        {
            using var scope = ServiceScopeFactory.CreateAsyncScope();
            var walletSession = scope.ServiceProvider.GetRequiredService<IWalletSession>();
            return walletSession;
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}",ex.Message);
        }

        return null;
    }


}