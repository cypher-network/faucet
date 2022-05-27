// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Models;
using Libsecp256k1Zkp.Net;
using libsignal.ecc;
using Microsoft.AspNetCore.SignalR;
using NBitcoin;
using ILogger = Serilog.ILogger;
using Utils = Faucet.Helpers.Utils;

namespace Faucet.Hubs;

/// <summary>
/// 
/// </summary>
public class MinerHub : Hub
{
    private readonly IFaucetSystem _faucetSystem;
    private readonly ILogger _logger;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="faucetSystem"></param>
    /// <param name="logger"></param>
    public MinerHub(IFaucetSystem faucetSystem, ILogger logger)
    {
        _faucetSystem = faucetSystem;
        _logger = logger;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task BlockProof(byte[] proof)
    {
        var msg = _faucetSystem.Crypto().BoxSealOpen(proof,
            _faucetSystem.PrivateKey.ToUnSecureString().HexToByte(), _faucetSystem.PublicKey[1..33]);
        if (msg.Length == 0) return;
        await using var stream = Utils.Manager.GetStream(msg);
        var blockMinerProof = await MessagePack.MessagePackSerializer.DeserializeAsync<BlockMinerProof>(stream);
        var blockMiners =
            await (await _faucetSystem.UnitOfWork()).BlockMinerRepository.OrderByRangeAsync(x => x.Height, 0, 2);
        if (blockMiners[1].Height != blockMinerProof.Height) return;
        var kernel = Validator.Kernel(blockMiners[0].Hash, blockMiners[1].Hash, blockMinerProof.Locktime);
        var verifyKernel = Validator.VerifyKernel(blockMinerProof.VrfSig, kernel);
        if (!verifyKernel) return;
        var blockProof = await (await _faucetSystem.UnitOfWork()).BlockMinerProofRepository.GetAsync(x =>
            new ValueTask<bool>(x.Address.Xor(blockMinerProof.Address) && x.Height == blockMinerProof.Height));
        if (blockProof is not null) return;
        try
        {
            _faucetSystem.Crypto().GetVerifyVrfSignature(Curve.decodePoint(blockMinerProof.PublicKey, 0), kernel,
                blockMinerProof.VrfProof);
        }
        catch (Exception ex)
        {
            _logger.Here().Error("{@Message}", ex.Message);
            return;
        }

        if (!Validator.VerifySolution(blockMinerProof.VrfProof, kernel, blockMinerProof.Solution)) return;
        if (!Validator.VerifySloth(Validator.Bits(blockMinerProof.Solution), blockMinerProof.VrfSig,
                blockMinerProof.Nonce)) return;
        if (!Validator.VerifyLockTime(new LockTime(Utils.UnixTimeToDateTime(blockMinerProof.Locktime)),
                blockMinerProof.LocktimeScript)) return;
        var updateBlockMinerProof = blockMinerProof with { Hash = new Secp256k1().Randomize32() };
        await (await _faucetSystem.UnitOfWork()).BlockMinerProofRepository.PutAsync(updateBlockMinerProof.Hash,
            updateBlockMinerProof);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<byte[]> PublicKey()
    {
        var pubKey = _faucetSystem.PublicKey[1..33];
        await Clients.Caller.SendAsync("PublicKey", pubKey);
        return pubKey;
    }
}