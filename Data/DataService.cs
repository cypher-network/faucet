// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Models;
using MessagePack;
using Newtonsoft.Json.Linq;

namespace Faucet.Data;

/// <summary>
/// 
/// </summary>
public enum TransactionType
{
    Cain,
    Mempool
}

/// <summary>
/// 
/// </summary>
public class DataService
{
    private static readonly object Locking = new();
    
    private readonly List<BlockView> _blocks = new();
    private readonly List<TransactionView> _transactionViews = new();
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _url;
    private readonly Serilog.ILogger _logger;
    private ulong _blockHeight;

    private const int Take = 15;
    private const long Coin = 1000_000_000;
    
    public DataService(IHostApplicationLifetime applicationLifetime, IHttpClientFactory httpClientFactory, string url, Serilog.ILogger logger)
    {
        _applicationLifetime = applicationLifetime;
        _httpClientFactory = httpClientFactory;
        _url = url;
        _logger = logger;
        HandleGetBlocks();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public BlockView[] GetBlocks()
    {
        lock (Locking)
        {
            return _blocks.ToArray();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public TransactionView[] GetTxs()
    {
        lock (Locking)
        {
            return _transactionViews.TakeLast(Take).ToArray();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="txId"></param>
    /// <returns></returns>
    public async Task<bool> ConfirmTransaction(byte[] txId)
    {
        var transaction = await GetTransaction(txId, TransactionType.Cain);
        return transaction is not null && transaction.TxnId.Xor(txId);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="txId"></param>
    /// <returns></returns>
    public async Task<bool> IsTransactionInMemPool(byte[] txId)
    {
        var transaction = await GetTransaction(txId, TransactionType.Mempool);
        return transaction is not null && transaction.TxnId.Xor(txId);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="txId"></param>
    /// <param name="transactionType"></param>
    /// <returns></returns>
    private async Task<Transaction?> GetTransaction(byte[] txId, TransactionType transactionType)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var path = transactionType == TransactionType.Cain ? "chain" : "mempool";
        var url = $"{_url}/{path}/transaction/{txId.ByteToHex()}";
        using var httpResponseMessage = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(url)));
        using var stream = httpResponseMessage.Content.ReadAsStringAsync();
        var read = await stream;
        var jObject = JObject.Parse(read);
        var jToken = jObject.GetValue("transaction");
        if (!httpResponseMessage.IsSuccessStatusCode) return null;
        var transaction = jToken?.ToObject<Transaction>();
        return transaction;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="skip"></param>
    /// <param name="take"></param>
    /// <returns></returns>
    private async Task<Block[]?> GetBlocks(int skip, int take)
    {
        return await GetBlocks($"{_url}/chain/blocks/{skip}/{take}");
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<Block[]?> GetSafeGuardBlocks()
    {
        return await GetBlocks($"{_url}/chain/safeguards");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="uri"></param>
    /// <returns></returns>
    private async Task<Block[]?> GetBlocks(string uri)
    {
        var blocks = Array.Empty<Block>();
        var httpClient = _httpClientFactory.CreateClient();
        using var httpResponseMessage = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(uri)));
        using var stream = httpResponseMessage.Content.ReadAsStringAsync();
        var read = await stream;
        var jObject = JObject.Parse(read);
        var jToken = jObject.GetValue("blocks");
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            blocks = jToken?.ToObject<Block[]>();
        }
        return blocks;
    }
    
    /// <summary>
    /// 
    /// </summary>
    private void HandleGetBlocks()
    {
        Task.Run(async () =>
        {
            while (!_applicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                try
                {
                    await LatestTop();
                }
                catch (Exception e)
                {
                    _logger.Here().Error("{@Message}", e.Message);
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
    /// <param name="transaction"></param>
    public async Task<bool> SendTransaction(Transaction transaction)
    {
        var bytes = MessagePackSerializer.Serialize(transaction);
        var httpClient = _httpClientFactory.CreateClient();
        using var httpResponseMessage = await httpClient.PostAsJsonAsync(
            new Uri($"{_url}/mempool/transaction"), bytes,
            _applicationLifetime.ApplicationStopped);
        return httpResponseMessage.IsSuccessStatusCode;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<ulong> BlockCount()
    {
        var httpClient = _httpClientFactory.CreateClient();
        using var httpResponseMessage = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get,
            new Uri($"{_url}/chain/height")));
        using var stream = httpResponseMessage.Content.ReadAsStringAsync();
        var read = await stream;
        var jObject = JObject.Parse(read);
        return (ulong)jObject.GetValue("height")!;
    }

    /// <summary>
    /// 
    /// </summary>
    private async Task LatestTop()
    {
        var height = await BlockCount();
        if (height > _blockHeight)
        {
            _blockHeight = height;
            var skip = (int)height - Take;
            if (skip < 0) skip = 0;
            var blocks = await GetBlocks(skip, Take);
            if (blocks is null) return;
            lock (Locking)
            {
                _blocks.Clear();
                _blocks.AddRange(blocks.Select(block => new BlockView
                {
                    Height = block.Height,
                    Size = block.Size,
                    NrTx = block.NrTx,
                    Staked = block.BlockPos.Bits,
                    Reward = Convert.ToDecimal(block.Txs[0].Vout[0].A) / Coin
                }));

                _transactionViews.Clear();
                foreach (var txs in blocks.Select(x => x.Txs))
                {
                    foreach (var tx in txs)
                    {
                        var index = 0;
                        if (tx.Vout.Length == 3)
                        {
                            index = 1;
                        }
                        var txView = new TransactionView
                        {
                            TxnId = Convert.ToHexString(tx.TxnId),
                            To = Convert.ToHexString(tx.Vout[index].P)
                        };
                    
                        _transactionViews.Add(txView);
                    }
                }
            }
        }
    }
}