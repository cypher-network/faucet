// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using Faucet.Models;
using ILogger = Serilog.ILogger;

namespace Faucet.Persistence;

public interface ITransactionRepository : IRepository<Transaction>
{

}

/// <summary>
/// </summary>
public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    private readonly ILogger _logger;
    private readonly IStoreDb _storeDb;

    /// <summary>
    /// </summary>
    /// <param name="storeDb"></param>
    /// <param name="logger"></param>
    public TransactionRepository(IStoreDb storeDb, ILogger logger)
        : base(storeDb, logger)
    {
        _storeDb = storeDb;
        _logger = logger.ForContext("SourceContext", nameof(BlockMinerProofRepository));

        SetTableName(StoreDb.TransactionTable.ToString());
    }
}