using Dawn;
using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Models;
using Microsoft.AspNetCore.Mvc;
using ILogger = Serilog.ILogger;

namespace Faucet.Controllers;

[Route("faucet")]
[ApiController]
public class FaucetController : Controller
{
    private readonly IFaucetSystem _faucetSystem;
    private readonly ILogger _logger;
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="faucetSystem"></param>
    /// <param name="logger"></param>
    public FaucetController(IFaucetSystem faucetSystem, ILogger logger)
    {
        _faucetSystem = faucetSystem;
        _logger = logger.ForContext("SourceContext", nameof(FaucetController));
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpGet("supply", Name = "GetSupply")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupply()
    {
        try
        {
            return new ObjectResult(new { supply = _faucetSystem.Blockchain().Supply });
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex, "Unable to get the supply");
        }

        return NotFound();
    }
    
    /// <summary>
    /// </summary>
    /// <returns></returns>
    [HttpGet("height", Name = "GetBlockHeight")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlockHeightAsync()
    {
        try
        {
            var blockMiners =
                await _faucetSystem.UnitOfWork().BlockMinerRepository.OrderByRangeAsync(x => x.Height, 0, 2);
            return new ObjectResult(new { height = blockMiners.Last().Height });
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex, "Unable to get the block height");
        }

        return NotFound();
    }
    
    /// <summary>
    /// </summary>
    /// <param name="skip"></param>
    /// <param name="take"></param>
    /// <returns></returns>
    [HttpGet("winners/{skip}/{take}", Name = "GetWinners")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBlocksAsync(int skip, int take)
    {
        Guard.Argument(skip, nameof(skip)).NotNegative();
        Guard.Argument(take, nameof(take)).NotNegative();
        try
        {
            var winners =
                (await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.OrderByRangeAsync(
                    x => x.Height, skip, take)).Select(x => new BlockMinerWinner()
                {
                    Id = x.Id.ByteToHex(), Hash = x.Hash.ByteToHex(), Height = x.Height, Reward = x.Reward.DivCoin(), PublicKey = x.PublicKey.ByteToHex(), Solution = x.Solution
                });
            return new ObjectResult(new { winners });
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex, "Unable to get winners");
        }

        return NotFound();
    }
    
    /// <summary>
    /// </summary>
    /// <param name="pubkey"></param>
    /// <returns></returns>
    [HttpGet("winner/{pubkey}", Name = "GetWinner")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionAsync(string pubkey)
    {
        Guard.Argument(pubkey, nameof(pubkey)).NotNull().NotEmpty().NotWhiteSpace().MaxLength(66);
        try
        {
            var winner =
                await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.GetAsync(x =>
                    new ValueTask<bool>(x.PublicKey.Xor(pubkey.HexToByte())));
            return new ObjectResult(new
            {
                winner = new BlockMinerWinner
                {
                    Hash = winner.Hash.ByteToHex(), PublicKey = winner.PublicKey.ByteToHex(),
                    Reward = winner.Reward.DivCoin(), Height = winner.Height, Solution = winner.Solution
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex, "Unable to get the transaction");
        }

        return NotFound();
    }
}