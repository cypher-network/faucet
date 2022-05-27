// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Reactive.Linq;
using Blake3;
using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Hubs;
using Faucet.Models;
using Faucet.Services;
using Libsecp256k1Zkp.Net;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using ILogger = Serilog.ILogger;

namespace Faucet.Ledger;

/// <summary>
/// 
/// </summary>
public interface IBlockchain
{
}

/// <summary>
/// 
/// </summary>
public class Blockchain: IDisposable, IBlockchain
{
    private readonly IFaucetSystem _faucetSystem;
    private readonly IHubContext<MinerHub> _hubContext;
    private readonly IBackgroundWorkerQueue _backgroundWorkerQueue;
    private readonly ILogger _logger;
    private readonly Random _random = new();
    private readonly CancellationTokenSource _cancellationToken = new();
    
    private IDisposable _disposableNewBlock;
    private IDisposable _disposableDecideWinners;
    private int _countDown = 11;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="faucetSystem"></param>
    /// <param name="hubContext"></param>
    /// <param name="backgroundWorkerQueue"></param>
    /// <param name="logger"></param>
    public Blockchain(IFaucetSystem faucetSystem, IHubContext<MinerHub>  hubContext, IBackgroundWorkerQueue backgroundWorkerQueue, ILogger logger)
    {
        _faucetSystem = faucetSystem;
        _hubContext = hubContext;
        _backgroundWorkerQueue = backgroundWorkerQueue;
        _logger = logger; 
        Init();
    }

    /// <summary>
    /// 
    /// </summary>
    private void Init()
    {
        try
        {
            var countBlockMiners = AsyncHelper.RunSync(async delegate
            {
                var unitOfWork = await _faucetSystem.UnitOfWork();
                var count = await unitOfWork.BlockMinerRepository.CountAsync();
                return count;
            });
            if (countBlockMiners != 0) return;
            var prevHash = Hasher.Hash("This is the beginning of cypherpunks write code faucet".ToBytes()).HexToByte();
            var hash = Hasher.Hash(Utils.Combine(prevHash, 0.ToBytes())).HexToByte();
            var blockMiner = new BlockMiner
            {
                Hash = hash,
                PrevHash = prevHash,
                Height = 0
            };

            AsyncHelper.RunSync(async delegate
            {
                await (await _faucetSystem.UnitOfWork()).BlockMinerRepository.PutAsync(blockMiner.Hash, blockMiner);
            });
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}", ex.Message);
        }
        finally
        {
            Task.WaitAll(NextBlock(), DecideWinners());
            PayBlockMiners(); 
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private void PayBlockMiners()
    {
        Task.Run(async () =>
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var winner in await (await _faucetSystem.UnitOfWork()).BlockMinerProofWinnerRepository
                                 .WhereAsync(x => new ValueTask<bool>(x.TxId is null)))
                    {
                        var amount = (int)Math.Round(Convert.ToDecimal(int.MaxValue) / winner.Reward,
                            MidpointRounding.ToEven);
                        var txId = await _faucetSystem.Wallet().Payout(winner.Address.FromBytes(), amount);
                        if (txId is null) return;
                        var reward = MessagePackSerializer.Serialize(new Reward(txId, amount));
                        var cipher = _faucetSystem.Crypto().BoxSeal(reward, winner.PublicKey[1..33]);
                        await _hubContext.Clients.All.SendAsync("Reward", cipher);
                        var updateWinner = winner with { Reward = amount, TxId = txId };
                        await (await _faucetSystem.UnitOfWork()).BlockMinerProofWinnerRepository.PutAsync(
                            updateWinner.Hash, updateWinner);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Here().Error("{@Message}", ex.Message);
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
    private Task NextBlock()
    {
        _disposableNewBlock = Observable.Interval(TimeSpan.FromSeconds(1)).Subscribe(_ =>
        {
            try
            {
                if (_countDown != 0) return;
                var blockMiners = AsyncHelper.RunSync(async () =>
                    await (await _faucetSystem.UnitOfWork()).BlockMinerRepository.OrderByRangeAsync( x => x.Height,  0, 2));
                var storedBlockMiner = blockMiners.Last();
                var height = storedBlockMiner.Height + 1;
                var hash = Hasher.Hash(Utils.Combine(storedBlockMiner.Hash, height.ToBytes())).HexToByte();
                var blockMiner = new BlockMiner { Hash = hash, PrevHash = storedBlockMiner.Hash, Height = height };
                if (blockMiners.Count == 1)
                {
                    AsyncHelper.RunSync(async () =>
                        (await _faucetSystem.UnitOfWork()).BlockMinerRepository.PutAsync(blockMiner.Hash, blockMiner));
                }
                else
                {
                    foreach (var miner in blockMiners)
                    {
                        AsyncHelper.RunSync(async () =>
                            (await _faucetSystem.UnitOfWork()).BlockMinerRepository.Delete(miner.Hash));
                    }

                    AsyncHelper.RunSync(async () =>
                    {
                        await (await _faucetSystem.UnitOfWork()).BlockMinerRepository.PutAsync(storedBlockMiner.Hash,
                            storedBlockMiner);
                        await (await _faucetSystem.UnitOfWork()).BlockMinerRepository.PutAsync(blockMiner.Hash,
                            blockMiner);
                    });
                }

                _hubContext.Clients.All.SendAsync("NewBlock", blockMiner).GetAwaiter();
                _countDown = 11;
            }
            catch (Exception ex)
            {
                _logger.Here().Error("{@Message}", ex.Message);
                _countDown = 11;
            }
            finally
            {
                if (_countDown < 0) _countDown = 11;
                _countDown--;
                _hubContext.Clients.All.SendAsync("CountDown", _countDown).GetAwaiter();
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// 
    /// </summary>
    private Task DecideWinners()
    {
        _disposableDecideWinners = Observable.Interval(TimeSpan.FromSeconds(10)).Subscribe(_ =>
        {
            try
            {
                AsyncHelper.RunSync(async () =>
                {
                    var blockMinerProofs = await (await _faucetSystem.UnitOfWork()).BlockMinerProofRepository
                        .IterateAsync().ToArrayAsync();
                    foreach (var proofGroup in blockMinerProofs.GroupBy(x => x.Height))
                    {
                        var minerProofsSolutions = proofGroup
                            .Where(x => x.Solution == proofGroup.Select(n => n.Solution).Min()).ToArray();
                        var blockMinerWinners = minerProofsSolutions.Select(x =>
                            new BlockMinerProofWinner
                            {
                                Hash = new Secp256k1().Randomize32(),
                                PublicKey = x.PublicKey,
                                Address = x.Address,
                                Reward = _random.Next(1, int.MaxValue)
                            }).ToArray();
                        var winners = blockMinerWinners.Where(x => x.Reward == blockMinerWinners.Max(m => m.Reward))
                            .ToArray();
                        foreach (var winner in winners)
                            await (await _faucetSystem.UnitOfWork()).BlockMinerProofWinnerRepository.PutAsync(
                                winner.Hash, winner);
                        foreach (var blockMinerProof in proofGroup)
                            (await _faucetSystem.UnitOfWork()).BlockMinerProofRepository.Delete(blockMinerProof.Hash);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Here().Error("{@Message}", ex.Message);
            }
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// 
    /// </summary>
    public void Dispose()
    {
        _cancellationToken.Cancel();
        _cancellationToken.Dispose();
        _disposableNewBlock.Dispose();
        _disposableDecideWinners.Dispose();
    }
}