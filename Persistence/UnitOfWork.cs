// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using Microsoft.AspNetCore.DataProtection.Repositories;
using ILogger = Serilog.ILogger;

namespace Faucet.Persistence;

/// <summary>
/// </summary>
public interface IUnitOfWork
{
    IStoreDb StoreDb { get; }
    IXmlRepository DataProtectionKeys { get; }
    IDataProtectionRepository DataProtectionPayload { get; }
    IBlockMinerRepository BlockMinerRepository { get; }
    IBlockMinerProofRepository BlockMinerProofRepository { get; }
    IBlockMinerProofWinnerRepository BlockMinerProofWinnerRepository { get; }
    void Dispose();
}

/// <summary>
/// </summary>
public class UnitOfWork : IUnitOfWork, IDisposable
{
    /// <summary>
    /// </summary>
    /// <param name="folderDb"></param>
    /// <param name="logger"></param>
    public UnitOfWork(string folderDb, ILogger logger)
    {
        StoreDb = new StoreDb(folderDb);
        var log = logger.ForContext("SourceContext", nameof(UnitOfWork));
        DataProtectionPayload = new DataProtectionRepository(StoreDb, log);
        BlockMinerRepository = new BlockMinerRepository(StoreDb, log);
        BlockMinerProofRepository = new BlockMinerProofRepository(StoreDb, log);
        BlockMinerProofWinnerRepository = new BlockMinerProofWinnerRepository(StoreDb, log);
    }

    public IStoreDb StoreDb { get; }

    public IXmlRepository DataProtectionKeys { get; }
    public IDataProtectionRepository DataProtectionPayload { get; }
    public IBlockMinerRepository BlockMinerRepository { get; }
    public IBlockMinerProofRepository BlockMinerProofRepository { get; }
    public IBlockMinerProofWinnerRepository BlockMinerProofWinnerRepository { get; }

    /// <summary>
    /// </summary>
    public void Dispose()
    {
        StoreDb.Rocks.Dispose();
    }
}