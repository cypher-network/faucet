using Dawn;
using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Ledger;
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
    [HttpGet("supply", Name = "GetSupplyAsync")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplyAsync()
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
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpGet("emission", Name = "GetEmissionAsync")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmissionAsync()
    {
        try
        {
            var emission = LedgerConstant.Distribution - _faucetSystem.Blockchain().Supply;
            return new ObjectResult(new { emission });
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex, "Unable to get the emission");
        }

        return NotFound();
    }
    
    /// <summary>
    /// </summary>
    /// <returns></returns>
    [HttpGet("height", Name = "GetBlockHeightAsync")]
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
    [HttpGet("winners/{skip}/{take}", Name = "GetWinnersAsync")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWinnersAsync(int skip, int take)
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
    [HttpGet("winner/{pubkey}", Name = "GetWinnerAsync")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWinnerAsync(string pubkey)
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
            _logger.Here().Error(ex, "Unable to get winner");
        }

        return NotFound();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [HttpGet("winners/count", Name = "GetWinnersCountAsync")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWinnersCountAsync()
    {
        try
        {
            var count =
                await _faucetSystem.UnitOfWork().BlockMinerProofWinnerRepository.CountAsync();
            return new ObjectResult(new { count });
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex, "Unable to get winners count");
        }

        return NotFound();
    }
    
    /// <summary>
    /// </summary>
    /// <returns></returns>
    [HttpGet("usersonline", Name = "GetUserOnlineCountAsync")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserOnlineCountAsync()
    {
        try
        {
            return new ObjectResult(new { onlineUsers = _faucetSystem.UserOnlineCount });
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex, "Unable to get the block height");
        }

        return NotFound();
    }
}