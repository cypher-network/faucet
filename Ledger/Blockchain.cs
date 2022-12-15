// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Reactive.Linq;
using Blake3;
using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Hubs;
using Faucet.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IO;
using ILogger = Serilog.ILogger;
using Utils = Faucet.Helpers.Utils;

namespace Faucet.Ledger;

/// <summary>
/// 
/// </summary>
public interface IBlockchain
{
    public decimal Supply { get; }
}

/// <summary>
/// 
/// </summary>
public class Blockchain: IDisposable, IBlockchain
{
    private readonly IFaucetSystem _faucetSystem;
    private readonly IHubContext<MinerHub> _hubContext;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cancellationToken = new();

    private IDisposable _disposablePayBlockMiners;
    private IDisposable _disposableNewBlock;
    private IDisposable _disposableDecideWinners;
    private int _countDown = 11;

    public decimal Supply { get; private set; }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="faucetSystem"></param>
    /// <param name="hubContext"></param>
    /// <param name="logger"></param>
    public Blockchain(IFaucetSystem faucetSystem, IHubContext<MinerHub>  hubContext, ILogger logger)
    {
        _faucetSystem = faucetSystem;
        _hubContext = hubContext;
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
            Supply = AsyncHelper.RunSync(async delegate
            {
                var supply = LedgerConstant.Distribution;
                var winners = await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.WhereAsync(x =>
                    new ValueTask<bool>(x.Reward != 0));
                if (winners is { })
                {
                    supply -= winners.Sum(x => x.Reward.DivCoin());
                }

                return supply;
            });
            
            var countBlockMiners = AsyncHelper.RunSync(async delegate
            {
                var count = await _faucetSystem.UnitOfWork().BlockMinerRepository.CountAsync();
                return count;
            });
            if (countBlockMiners != 0) return;
            using var secp256K1 = new Libsecp256k1Zkp.Net.Secp256k1();
            var prevHash = Hasher.Hash("This is the beginning of cypherpunks write code faucet".ToBytes()).HexToByte();
            var hash = Hasher.Hash(Utils.Combine(prevHash, 0.ToBytes(), secp256K1.Randomize32())).HexToByte();
            var blockMiner = new BlockMiner
            {
                Hash = hash,
                PrevHash = prevHash,
                Height = 0
            };
            
            AsyncHelper.RunSync(async delegate
            {
                await _faucetSystem.UnitOfWork().BlockMinerRepository.PutAsync(blockMiner.Hash, blockMiner);
            });
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}", ex.Message);
        }
        finally
        {
            Task.WaitAll(NextBlock(), DecideWinners());
            CalculateBlockMinersRewardInterval();
            PayoutInterval();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private void CalculateBlockMinersRewardInterval()
    {
        _disposablePayBlockMiners = Observable.Interval(TimeSpan.FromSeconds(60)).Subscribe(_ =>
        {
            if (_cancellationToken.IsCancellationRequested) return;
            Reward().Wait();
        });
    }
    
    /// <summary>
    /// 
    /// </summary>
    private void PayoutInterval()
    {
        _disposablePayBlockMiners = Observable.Interval(TimeSpan.FromDays(7)).Subscribe(_ =>
        {
            if (_cancellationToken.IsCancellationRequested) return;
            Payout().Wait();
        });
    }

    /// <summary>
    /// 
    /// </summary>
    private async Task Payout()
    {
        try
        {
            var start = Utils.UnixTimeToDateTime(Utils.GetAdjustedTimeAsUnixTimestamp()).AddDays(-7).ToUnixTimeSeconds();
            var blockMinerProofWinner = (await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.WhereAsync(x =>
                new ValueTask<bool>(x.Timestamp >= start & !x.Paid))).GroupBy(g => g.PublicKey).First();
            var amount = blockMinerProofWinner.Sum(x => x.Reward.DivCoin());
            var address = blockMinerProofWinner.First().Address;
            var transaction = await _faucetSystem.Wallet().Payout(address.FromBytes(), amount.ConvertToUInt64());
            if (transaction is { })
            {
                foreach (var winner in blockMinerProofWinner)
                {
                    var win = winner with
                    {
                        Paid = true, PayoutTimestamp = Utils.GetAdjustedTimeAsUnixTimestamp(), TxId = transaction.TxnId
                    };
                    await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.PutAsync(winner.Id, win);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex.Message);
        }
    }
    
    /// <summary>
    ///     
    /// </summary>
    private async Task Reward()
    {
        try
        {
            var blockMiner = await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.GetAsync(x =>
                new ValueTask<bool>(x.Reward == 0));
            if (blockMiner is null) return;
            var amount = LedgerConstant.Reward;
            var reward = await Data.DataService.SerializeAsync(new Reward(null!, blockMiner.Hash, blockMiner.Height, amount.ConvertToUInt64()));
            var cipher = _faucetSystem.Crypto().BoxSeal(reward.First, blockMiner.PublicKey.AsMemory()[1..33]);
            await _hubContext.Clients.All.SendAsync("Reward", cipher.ToArray());
            var updateWinner = blockMiner with { Reward = amount.ConvertToUInt64()};
            if (_faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.Delete(updateWinner.Id))
            {
                await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.PutAsync(updateWinner.Id,
                    updateWinner);
                Supply -= amount;
            }
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}", ex.Message);
        }
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
                    await _faucetSystem.UnitOfWork().BlockMinerRepository.OrderByRangeAsync( x => x.Height,  0, 2));
                var storedBlockMiner = blockMiners.Last();
                var height = storedBlockMiner.Height + 1;
                using var secp256K1 = new Libsecp256k1Zkp.Net.Secp256k1();
                using var stream = Utils.Manager.GetStream() as RecyclableMemoryStream;
                stream.Write(storedBlockMiner.PrevHash);
                stream.Write(height.ToBytes());
                stream.Write(secp256K1.Randomize32());
                var hash = Validator.IncrementHash(storedBlockMiner.PrevHash, stream.GetSpan());
                var blockMiner = new BlockMiner { Hash = hash, PrevHash = storedBlockMiner.Hash, Height = height };
                if (blockMiners.Count == 1)
                {
                    AsyncHelper.RunSync(async () =>
                        await _faucetSystem.UnitOfWork().BlockMinerRepository.PutAsync(blockMiner.Hash, blockMiner));
                }
                else
                {
                    foreach (var miner in blockMiners)
                    {
                        AsyncHelper.RunSync(async () =>
                            _faucetSystem.UnitOfWork().BlockMinerRepository.Delete(miner.Hash));
                    }

                    AsyncHelper.RunSync(async () =>
                    {
                        await _faucetSystem.UnitOfWork().BlockMinerRepository.PutAsync(storedBlockMiner.Hash,
                            storedBlockMiner);
                        await _faucetSystem.UnitOfWork().BlockMinerRepository.PutAsync(blockMiner.Hash,
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
                if (_countDown < 0)
                {   
                    _countDown = 11;
                }
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
        _disposableDecideWinners = Observable.Interval(TimeSpan.FromSeconds(45)).Subscribe(_ =>
        {
            try
            {
                AsyncHelper.RunSync(async () =>
                {
                    var blockMinerProofs = await _faucetSystem.UnitOfWork().BlockMinerProofRepository
                        .IterateAsync().ToArrayAsync();
                    if (blockMinerProofs.Length == 0) return;
                    foreach (var proofGroup in blockMinerProofs.GroupBy(x => x.Height))
                    {
                        var minerProofsSolutions = proofGroup
                            .Where(x => x.Solution == proofGroup.Select(n => n.Solution).Min()).ToArray();
                        var blockMinerWinners = minerProofsSolutions.Select(x =>
                            new BlockMinerProofWinner
                            {
                                Hash = x.Hash,
                                Height = x.Height,
                                PublicKey = x.PublicKey,
                                Address = x.Address,
                                Solution = x.Solution
                            }).ToArray();
                        var winners = blockMinerWinners.Where(x => x.Solution == blockMinerWinners.Max(m => m.Solution))
                            .ToArray();
                        var winner = winners.Length switch
                        {
                            > 2 => winners.FirstOrDefault(winner =>
                                winner.Solution >= blockMinerWinners.Select(x => x.Solution).Max()),
                            _ => winners[0]
                        };
                        await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.PutAsync(winner.Id, winner);
                        foreach (var blockMinerProof in proofGroup)
                            _faucetSystem.UnitOfWork().BlockMinerProofRepository.Delete(blockMinerProof.Hash);
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
        _disposablePayBlockMiners.Dispose();
        _disposableNewBlock.Dispose();
        _disposableDecideWinners.Dispose();
    }
}