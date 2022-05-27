// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using Faucet.Models;
using ILogger = Serilog.ILogger;

namespace Faucet.Persistence;

/// <summary>
/// </summary>
public interface IBlockMinerProofRepository : IRepository<BlockMinerProof>
{

}

/// <summary>
/// </summary>
public class BlockMinerProofRepository : Repository<BlockMinerProof>, IBlockMinerProofRepository
{
    private readonly ILogger _logger;
    private readonly IStoreDb _storeDb;

    /// <summary>
    /// </summary>
    /// <param name="storeDb"></param>
    /// <param name="logger"></param>
    public BlockMinerProofRepository(IStoreDb storeDb, ILogger logger)
        : base(storeDb, logger)
    {
        _storeDb = storeDb;
        _logger = logger.ForContext("SourceContext", nameof(BlockMinerProofRepository));

        SetTableName(StoreDb.BlockMinerProofTable.ToString());
    }
}