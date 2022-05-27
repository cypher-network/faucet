// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using Faucet.Models;
using ILogger = Serilog.ILogger;

namespace Faucet.Persistence;


/// <summary>
/// </summary>
public interface IBlockMinerProofWinnerRepository : IRepository<BlockMinerProofWinner>
{

}

/// <summary>
/// </summary>
public class BlockMinerProofWinnerRepository : Repository<BlockMinerProofWinner>, IBlockMinerProofWinnerRepository
{
    private readonly ILogger _logger;
    private readonly IStoreDb _storeDb;
    private readonly ReaderWriterLockSlim _sync = new();

    /// <summary>
    /// </summary>
    /// <param name="storeDb"></param>
    /// <param name="logger"></param>
    public BlockMinerProofWinnerRepository(IStoreDb storeDb, ILogger logger)
        : base(storeDb, logger)
    {
        _storeDb = storeDb;
        _logger = logger.ForContext("SourceContext", nameof(BlockMinerProofWinnerRepository));

        SetTableName(StoreDb.BlockMinerProofWinnerTable.ToString());
    }
}