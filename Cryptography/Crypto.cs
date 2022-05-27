// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Security.Cryptography;
using Faucet.Extensions;
using Faucet.Helpers;
using Faucet.Models;
using Faucet.Persistence;
using libsignal.ecc;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;
using NitraLibSodium.Box;
using ILogger = Serilog.ILogger;

namespace Faucet.Cryptography;

/// <summary>
/// 
/// </summary>
public interface ICrypto
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    Task<KeyPair?> GetOrUpsertKeyName();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ecPrivateKey"></param>
    /// <param name="msg"></param>
    /// <returns></returns>
    byte[] GetCalculateVrfSignature(ECPrivateKey ecPrivateKey, byte[] msg);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ecPublicKey"></param>
    /// <param name="msg"></param>
    /// <param name="sig"></param>
    /// <returns></returns>
    byte[] GetVerifyVrfSignature(ECPublicKey ecPublicKey, byte[] msg, byte[] sig);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cipher"></param>
    /// <param name="secretKey"></param>
    /// <param name="publicKey"></param>
    /// <returns></returns>
    byte[] BoxSealOpen(byte[] cipher, byte[] secretKey, byte[] publicKey);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="publicKey"></param>
    /// <returns></returns>
    byte[] BoxSeal(byte[] msg, byte[] publicKey);
}

/// <summary>
/// 
/// </summary>
public class Crypto : ICrypto
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;
    
    private IDataProtector _dataProtector;
    private DataProtection _dataProtection;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dataProtectionProvider"></param>
    /// <param name="unitOfWork"></param>
    /// <param name="logger"></param>
    public Crypto(IDataProtectionProvider dataProtectionProvider, IUnitOfWork unitOfWork, ILogger logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _unitOfWork = unitOfWork;
        _logger = logger.ForContext("SourceContext", nameof(Crypto));
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<KeyPair?> GetOrUpsertKeyName()
    {
        const string keyName = "Faucet.Default.Key";
        KeyPair? kp = null;
        try
        {
            _dataProtector = _dataProtectionProvider.CreateProtector(keyName);
            _dataProtection = await _unitOfWork.DataProtectionPayload.GetAsync(keyName.ToBytes());
            if (_dataProtection is null)
            {
                _dataProtection = new DataProtection
                {
                    FriendlyName = keyName,
                    Payload = _dataProtector.Protect(JsonConvert.SerializeObject(GenerateKeyPair()))
                };
                var saved = await _unitOfWork.DataProtectionPayload.PutAsync(keyName.ToBytes(), _dataProtection);
                if (!saved)
                {
                    _logger.Here().Error("Unable to save protection key payload for: {@KeyName}", keyName);
                    return null;
                }
            }

            kp = GetKeyPair();
        }
        catch (CryptographicException ex)
        {
            _logger.Here().Fatal(ex, "Cannot get keypair");
        }
        catch (Exception ex)
        {
            _logger.Here().Error(ex, "Cannot get keypair");
        }

        return kp;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ecPrivateKey"></param>
    /// <param name="msg"></param>
    /// <returns></returns>
    public byte[] GetCalculateVrfSignature(ECPrivateKey ecPrivateKey, byte[] msg)
    {
        var calculateVrfSignature = Curve.calculateVrfSignature(ecPrivateKey, msg);
        return calculateVrfSignature;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="ecPublicKey"></param>
    /// <param name="msg"></param>
    /// <param name="sig"></param>
    /// <returns></returns>
    public byte[] GetVerifyVrfSignature(ECPublicKey ecPublicKey, byte[] msg, byte[] sig)
    {
        var vrfSignature = Curve.verifyVrfSignature(ecPublicKey, msg, sig);
        return vrfSignature;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="cipher"></param>
    /// <param name="secretKey"></param>
    /// <param name="publicKey"></param>
    /// <returns></returns>
    public byte[] BoxSealOpen(byte[] cipher, byte[] secretKey, byte[] publicKey)
    {
        var msg = new byte[cipher.Length];
        return Box.SealOpen(msg, cipher, (ulong)cipher.Length, publicKey, secretKey) != 0
            ? Array.Empty<byte>()
            : msg;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="publicKey"></param>
    /// <returns></returns>
    public byte[] BoxSeal(byte[] msg, byte[] publicKey)
    {
        var cipher = new byte[msg.Length + (int)Box.Sealbytes()];
        return Box.Seal(cipher, msg, (ulong)msg.Length, publicKey) != 0
            ? Array.Empty<byte>()
            : cipher;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static KeyPair GenerateKeyPair()
    {
        var keys = Curve.generateKeyPair();
        return new Models.KeyPair(keys.getPrivateKey().serialize(), keys.getPublicKey().serialize());
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private KeyPair GetKeyPair()
    {
        var unprotectedPayload = _dataProtector.Unprotect(_dataProtection.Payload);
        var definition = new { PrivateKey = string.Empty, PublicKey = string.Empty };
        var message = JsonConvert.DeserializeAnonymousType(unprotectedPayload, definition);
        return new Models.KeyPair(Convert.FromBase64String(message.PrivateKey),
            Convert.FromBase64String(message.PublicKey));
    }
}