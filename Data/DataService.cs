// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Buffers;
using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Models;
using MessagePack;
using Microsoft.IO;
using Newtonsoft.Json.Linq;
using Transaction = Faucet.Models.Transaction;

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
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _url;
    private readonly Serilog.ILogger _logger;
    private readonly HttpClient _httpClient;
    
    private BlockHeight? _blockCount = new();
    private ulong _blockHeight;
    
    private const int Take = 15;

    public DataService(IHostApplicationLifetime applicationLifetime, IHttpClientFactory httpClientFactory, string url,
        Serilog.ILogger logger)
    {
        _applicationLifetime = applicationLifetime;
        _httpClientFactory = httpClientFactory;
        _url = url;
        _logger = logger;
        _httpClient = _httpClientFactory.CreateClient();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="txId"></param>
    /// <returns></returns>
    public async Task<bool> ConfirmTransaction(byte[] txId)
    {
        var transaction = await GetTransaction(txId, TransactionType.Cain);
        return transaction is not null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="txId"></param>
    /// <returns></returns>
    public async Task<bool> IsTransactionInMemPool(byte[] txId)
    {
        var transaction = await GetTransaction(txId, TransactionType.Mempool);
        return transaction is not null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="txId"></param>
    /// <param name="transactionType"></param>
    /// <returns></returns>
    private async Task<Transaction?> GetTransaction(byte[] txId, TransactionType transactionType)
    {
        Transaction? transaction = null;

        try
        {
            var path = transactionType == TransactionType.Cain ? "chain" : "mempool";
            var url = $"{_url}/{path}/transaction/{txId.ByteToHex()}";
            using var httpResponseMessage = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(url)));
            using var stream = httpResponseMessage.Content.ReadAsStringAsync();
            var read = await stream;
            var jObject = JObject.Parse(read);
            var jToken = jObject.GetValue("transaction");
            if (!httpResponseMessage.IsSuccessStatusCode) return null;
            transaction = jToken?.ToObject<Transaction>();
            return transaction;
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}", ex.Message);
        }

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
        try
        {
            using var httpResponseMessage = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(uri)));
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
        catch (Exception e)
        {
            _logger.Here().Error("{@Message}", e.Message);
        }

        return blocks;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="transaction"></param>
    public async Task<bool> SendTransaction(Transaction transaction)
    {
        try
        {
            var bytes = await SerializeAsync(transaction);
            using var httpResponseMessage = await _httpClient.PostAsJsonAsync($"{_url}/mempool/transaction", bytes.ToArray(),
                _applicationLifetime.ApplicationStopped);
            return httpResponseMessage.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.Here().Error("@Message", ex.Message);
        }

        return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private async Task<ulong> BlockCount()
    {
        _blockCount = await _httpClient.GetFromJsonAsync<BlockHeight>($"{_url}/chain/height");
        return _blockCount?.Height ?? 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static async Task<ReadOnlySequence<byte>> SerializeAsync<T>(T value)
    {
        await using var stream =
            Utils.Manager.GetStream(MessagePackSerializer.Serialize(value)) as
                RecyclableMemoryStream;
        return stream.GetReadOnlySequence();
    }
}