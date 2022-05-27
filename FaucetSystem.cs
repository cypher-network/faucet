// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Security;
using Faucet.Cryptography;
using Faucet.Extensions;
using Faucet.Helpers;
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
    AsyncLazy<IUnitOfWork> UnitOfWork();
    ICrypto Crypto();
    public SecureString PrivateKey { get; init; }
    public byte[] PublicKey { get; init; }
    IWallet Wallet();
    AsyncLazy<IWalletSession> WalletSession();
}

/// <summary>
/// 
/// </summary>
public class FaucetSystem : IFaucetSystem
{
    private readonly ILogger _logger;
    private IUnitOfWork _unitOfWork;
    private IWalletSession _walletSession;
    
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
    public AsyncLazy<IUnitOfWork> UnitOfWork() => new(() =>
    {
        _unitOfWork ??= GetUnitOfWork();
        return Task.FromResult(_unitOfWork);
    });
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ICrypto Crypto()
    {
        try
        {
            using var scope = ServiceScopeFactory.CreateAsyncScope();
            var crypto = scope.ServiceProvider.GetRequiredService<ICrypto>();
            return crypto;
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
    
    public AsyncLazy<IWalletSession> WalletSession() => new(() =>
    {
        _walletSession ??= GetWalletSession();
        return Task.FromResult(_walletSession);
    });
    
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