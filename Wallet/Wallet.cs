// CypherNetwork by Matthew Hellyer is licensed under CC BY-NC-ND 4.0.
// To view a copy of this license, visit https://creativecommons.org/licenses/by-nc-nd/4.0

using System.Diagnostics;
using Dawn;
using Faucet.Data;
using Faucet.Helpers;
using Faucet.Models;
using Libsecp256k1Zkp.Net;
using MessagePack;
using NBitcoin;
using NBitcoin.Stealth;
using Newtonsoft.Json.Linq;
using Transaction = Faucet.Models.Transaction;
using Utils = Faucet.Helpers.Utils;

namespace Faucet.Wallet;

/// <summary>
/// </summary>
public struct Balance
{
    public ulong Total { get; init; }
    public Output Commitment { get; init; }
}

/// <summary>
/// </summary>
public struct WalletTransaction
{
    public readonly Transaction Transaction;
    public readonly string Message;

    public WalletTransaction(Transaction transaction, string message)
    {
        Transaction = transaction;
        Message = message;
    }
}

public interface IWallet
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    Task<byte[]> Payout(string address);
}

/// <summary>
/// 
/// </summary>
public class Wallet : IWallet
{
    private const string HardwarePath = "m/44'/847177'/0'/0/";
    private readonly IWalletSession _walletSession;
    private readonly DataService _dataService;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<Wallet> _logger;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="walletSession"></param>
    /// <param name="dataService"></param>
    /// <param name="applicationLifetime"></param>
    /// <param name="logger"></param>
    public Wallet(IWalletSession walletSession, DataService dataService, IHostApplicationLifetime applicationLifetime,
        ILogger<Wallet> logger)
    {
        _walletSession = walletSession;
        _dataService = dataService;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public async Task<byte[]> Payout(string address)
    {
        var rng = new Random();
        var amount = rng.Next(5);
        var walletTransaction = CreateTransaction((ulong)amount, 0, address);
        var result = await _dataService.SendTransaction(walletTransaction.Transaction);
        if (!result)
        {
            _walletSession.Notify(new[] { walletTransaction.Transaction });
        }
        
        return result ? walletTransaction.Transaction.TxnId : null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="reward"></param>
    /// <param name="address"></param>
    /// <returns></returns>
    private WalletTransaction CreateTransaction(ulong amount, ulong reward, string address)
    {
        Guard.Argument(amount, nameof(amount)).NotNegative();
        Guard.Argument(reward, nameof(reward)).NotNegative();
        Guard.Argument(address, nameof(address)).NotNull().NotEmpty().NotWhiteSpace();
        try
        {
            if (_walletSession.KeySet is null) return new WalletTransaction(null, "Node wallet login required");
            if (_walletSession.CacheTransactions.Count == 0)
                return new WalletTransaction(null, "Node wallet payments required");
            var (spendKey, scanKey) = Unlock();
            if (spendKey == null || scanKey == null)
                return new WalletTransaction(null, "Unable to unlock node wallet");
            _logger.LogInformation("Coinstake Amount: [{@amount}]", amount);
            _walletSession.Amount = amount.MulWithNanoTan();
            _walletSession.Reward = reward;
            _walletSession.RecipientAddress = address;
            var (commitment, total) = GetSpending(_walletSession.Amount);
            if (commitment is null)
                return new WalletTransaction(null, "No available commitment for this payment. Please load more funds");
            _walletSession.Spending = commitment;
            _walletSession.Change = total - _walletSession.Amount;
            if (_walletSession.Amount > total)
                return new WalletTransaction(null, "The stake amount exceeds the available commitment amount");
            var (transaction, message) = RingConfidentialTransaction(_walletSession);
            if (transaction is null) return new WalletTransaction(null, message);
            foreach (var vout in transaction.Vout)
            {
                var output = new Output
                {
                    C = vout.C.ByteToHex(), E = vout.E.ByteToHex(), N = vout.N.ByteToHex(), T = (sbyte)vout.T
                };
                _walletSession.CacheTransactions.Add(vout.C, output);
            }

            return new WalletTransaction(transaction, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }

        return new WalletTransaction(null, "Coinstake transaction failed");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="session"></param>
    /// <returns></returns>
    private Tuple<Transaction, string> RingConfidentialTransaction(IWalletSession session)
    {
        using var secp256K1 = new Secp256k1();
        using var pedersen = new Pedersen();
        using var mlsag = new MLSAG();
        var blinds = new Span<byte[]>(new byte[3][]);
        var sk = new Span<byte[]>(new byte[2][]);
        const int nRows = 2; // last row sums commitments
        const int nCols = 22; // ring size
        var index = Util.Rand(0, nCols) % nCols;
        var m = new byte[nRows * nCols * 33];
        var pcmIn = new Span<byte[]>(new byte[nCols * 1][]);
        var pcmOut = new Span<byte[]>(new byte[2][]);
        var randSeed = secp256K1.Randomize32();
        var preimage = secp256K1.Randomize32();
        var pc = new byte[32];
        var ki = new byte[33 * 1];
        var ss = new byte[nCols * nRows * 32];
        var blindSum = new byte[32];
        var pkIn = new Span<byte[]>(new byte[nCols * 1][]);
        m = RingMembers(ref session, blinds, sk, nRows, nCols, index, m, pcmIn, pkIn);
        if (m == null) return new Tuple<Transaction, string>(null, "Unable to create ring members");
        blinds[1] = pedersen.BlindSwitch(session.Amount, secp256K1.CreatePrivateKey());
        blinds[2] = pedersen.BlindSwitch(session.Change, secp256K1.CreatePrivateKey());
        pcmOut[0] = pedersen.Commit(session.Amount, blinds[1]);
        pcmOut[1] = pedersen.Commit(session.Change, blinds[2]);
        var commitSumBalance = pedersen.CommitSum(new List<byte[]> { pcmOut[0], pcmOut[1] }, new List<byte[]>());
        if (!pedersen.VerifyCommitSum(new List<byte[]> { commitSumBalance }, new List<byte[]> { pcmOut[0], pcmOut[1] }))
            return new Tuple<Transaction, string>(null, "Verify commit sum failed");
        var bulletChange = BulletProof(session.Change, blinds[2], pcmOut[1]);
        if (!bulletChange.Success) return new Tuple<Transaction, string>(null, bulletChange.Exception.Message);
        var success = mlsag.Prepare(m, blindSum, pcmOut.Length, pcmOut.Length, nCols, nRows, pcmIn, pcmOut, blinds);
        if (!success) return new Tuple<Transaction, string>(null, "MLSAG Prepare failed");
        sk[nRows - 1] = blindSum;
        success = mlsag.Generate(ki, pc, ss, randSeed, preimage, nCols, nRows, index, sk, m);
        if (!success) return new Tuple<Transaction, string>(null, "MLSAG Generate failed");
        success = mlsag.Verify(preimage, nCols, nRows, m, ki, pc, ss);
        if (!success) return new Tuple<Transaction, string>(null, "MLSAG Verify failed");
        var offsets = Offsets(pcmIn, nCols);
        var generateTransaction = GenerateTransaction(ref session, m, nCols, pcmOut, blinds, preimage, pc, ki, ss,
            bulletChange.Value.proof, offsets);
        session.Amount = 0;
        session.Reward = 0;
        session.Change = 0;
        return !generateTransaction.Success
            ? new Tuple<Transaction, string>(null,
                $"Unable to create the transaction. Inner error message {generateTransaction.NonSuccessMessage.message}")
            : new Tuple<Transaction, string>(generateTransaction.Value, null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private (Key, Key) Unlock()
    {
        try
        {
            var keySet = _walletSession.KeySet;
            var masterKey = MasterKey(MessagePackSerializer.Deserialize<KeySet>(keySet.ToUnSecureString().HexToByte()));
            var spendKey = masterKey.Derive(new KeyPath($"{HardwarePath}0")).PrivateKey;
            var scanKey = masterKey.Derive(new KeyPath($"{HardwarePath}1")).PrivateKey;
            return (spendKey, scanKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to unlock master key");
        }

        return (null, null);
    }

    /// <summary>
    /// </summary>
    /// <param name="keySet"></param>
    /// <returns></returns>
    private static ExtKey MasterKey(KeySet keySet)
    {
        Guard.Argument(keySet, nameof(keySet)).NotNull();
        var extKey = new ExtKey(new Key(keySet.RootKey.HexToByte()), keySet.ChainCode.HexToByte());
        keySet.ChainCode.ZeroString();
        keySet.KeyPath.ZeroString();
        keySet.RootKey.ZeroString();
        return extKey;
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    private Tuple<Output, ulong> GetSpending(ulong amount)
    {
        try
        {
            var freeBalances = new List<Balance>();
            var (_, scan) = Unlock();
            var balances = GetBalances();
            freeBalances.AddRange(balances.Where(balance => amount <= balance.Total).OrderByDescending(x => x.Total));
            if (!freeBalances.Any()) return new Tuple<Output, ulong>(null, 0);
            var spendAmount = freeBalances.Select(x => x.Total).Aggregate((x, y) => x - amount < y - amount ? x : y);
            var spendingBalance = freeBalances.First(a => a.Total == spendAmount);
            var commitmentTotal = Amount(spendingBalance.Commitment.N.HexToByte(), scan);
            return amount > commitmentTotal
                ? new Tuple<Output, ulong>(null, 0)
                : new Tuple<Output, ulong>(spendingBalance.Commitment, commitmentTotal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating the node wallet change");
            return new Tuple<Output, ulong>(null, 0);
        }
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    private Balance[] GetBalances()
    {
        var balances = new List<Balance>();
        try
        {
            var (_, scan) = Unlock();
            var outputs = _walletSession.CacheTransactions.GetItems()
                .Where(x => !_walletSession.Consumed.Any(c => x.C.HexToByte().Xor(c.Commit))).ToArray();
            if (!outputs.Any()) return Enumerable.Empty<Balance>().ToArray();
            balances.AddRange(from vout in outputs.ToArray()
                let coinType = (CoinType)vout.T
                where coinType is CoinType.Change or CoinType.Payment or CoinType.Coinstake
                let amount = Amount(vout.N.HexToByte(), scan)
                where amount != 0
                select new Balance { Commitment = vout, Total = amount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve node wallet balances");
        }

        return balances.ToArray();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="session"></param>
    /// <param name="blinds"></param>
    /// <param name="sk"></param>
    /// <param name="nRows"></param>
    /// <param name="nCols"></param>
    /// <param name="index"></param>
    /// <param name="m"></param>
    /// <param name="pcmIn"></param>
    /// <param name="pkIn"></param>
    /// <returns></returns>
    private unsafe byte[]? RingMembers(ref IWalletSession session, Span<byte[]> blinds, Span<byte[]> sk, int nRows,
        int nCols, int index, byte[] m, Span<byte[]> pcmIn, Span<byte[]> pkIn)
    {
        Guard.Argument(session, nameof(session)).NotNull();
        Guard.Argument(nRows, nameof(nRows)).NotNegative();
        Guard.Argument(nCols, nameof(nCols)).NotNegative();
        Guard.Argument(index, nameof(index)).NotNegative();
        Guard.Argument(m, nameof(m)).NotNull().NotEmpty();
        using var pedersen = new Pedersen();
        var (spendKey, scanKey) = Unlock();
        var transactions = _walletSession.GetSafeGuardBlocks().SelectMany(x => x.Txs).ToList();
        transactions.Shuffle();
        for (var k = 0; k < nRows - 1; ++k)
        for (var i = 0; i < nCols; ++i)
        {
            if (index == i)
                try
                {
                    var message = Message(session.Spending.N.HexToByte(), scanKey);
                    var oneTimeSpendKey = spendKey.Uncover(scanKey, new PubKey(session.Spending.E));
                    sk[0] = oneTimeSpendKey.ToHex().HexToByte();
                    blinds[0] = message.Blind;
                    pcmIn[i + k * nCols] = pedersen.Commit(message.Amount, message.Blind);
                    session.Consumed.Add(new Consumed(pcmIn[i + k * nCols], DateTime.UtcNow));
                    pkIn[i + k * nCols] = oneTimeSpendKey.PubKey.ToBytes();
                    fixed (byte* mm = m, pk = pkIn[i + k * nCols])
                    {
                        Util.MemCpy(&mm[(i + k * nCols) * 33], pk, 33);
                    }

                    continue;
                }
                catch (Exception)
                {
                    _logger.LogError("Unable to create inner ring member");
                    return null;
                }

            try
            {
                var ringMembers = (from tx in transactions
                    let vtime = tx.Vtime
                    where vtime != null
                    let verifyLockTime =
                        VerifyLockTime(new LockTime(Utils.UnixTimeToDateTime(tx.Vtime.L)),
                            System.Text.Encoding.UTF8.GetString(tx.Vtime.S))
                    where verifyLockTime != false
                    select tx).ToArray();
                ringMembers.Shuffle();
                ringMembers.ElementAt(0).Vout.Shuffle();
                Vout vout;
                if (!ContainsCommitment(pcmIn, ringMembers.ElementAt(0).Vout[0].C))
                {
                    vout = ringMembers.ElementAt(0).Vout[0];
                }
                else
                {
                    ringMembers.ElementAt(1).Vout.Shuffle();
                    vout = ringMembers.ElementAt(1).Vout[0];
                }

                pcmIn[i + k * nCols] = vout.C;
                pkIn[i + k * nCols] = vout.P;
                fixed (byte* mm = m, pk = pkIn[i + k * nCols])
                {
                    Util.MemCpy(&mm[(i + k * nCols) * 33], pk, 33);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Unable to create outer ring members");
                return null;
            }
        }

        return m;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="session"></param>
    /// <param name="m"></param>
    /// <param name="nCols"></param>
    /// <param name="pcmOut"></param>
    /// <param name="blinds"></param>
    /// <param name="preimage"></param>
    /// <param name="pc"></param>
    /// <param name="ki"></param>
    /// <param name="ss"></param>
    /// <param name="bp"></param>
    /// <param name="offsets"></param>
    /// <returns></returns>
    private TaskResult<Transaction> GenerateTransaction(ref IWalletSession session, byte[] m, int nCols,
        Span<byte[]> pcmOut, Span<byte[]> blinds, byte[] preimage, byte[] pc, byte[] ki, byte[] ss, byte[] bp,
        byte[] offsets)
    {
        Guard.Argument(session, nameof(session)).NotNull();
        Guard.Argument(m, nameof(m)).NotNull().NotEmpty();
        Guard.Argument(nCols, nameof(nCols)).NotNegative();
        Guard.Argument(preimage, nameof(preimage)).NotNull().NotEmpty();
        Guard.Argument(pc, nameof(pc)).NotNull().NotEmpty();
        Guard.Argument(ki, nameof(ki)).NotNull().NotEmpty();
        Guard.Argument(ss, nameof(ss)).NotNull().NotEmpty();
        Guard.Argument(bp, nameof(bp)).NotNull().NotEmpty();
        Guard.Argument(offsets, nameof(offsets)).NotNull().NotEmpty();
        try
        {
            var (outPkPayment, stealthPayment) = StealthPayment(session.RecipientAddress);
            var (outPkChange, stealthChange) = StealthPayment(session.SenderAddress);
            var tx = new Transaction
            {
                Bp = new[] { new Bp { Proof = bp } },
                Mix = nCols,
                Rct = new[] { new Rct { I = preimage, M = m, P = pc, S = ss } },
                Ver = 2,
                Vin = new[] { new Vin { Image = ki, Offsets = offsets } },
                Vout = new[]
                {
                    new Vout
                    {
                        A = 0,
                        C = pcmOut[0],
                        D = blinds[1],
                        E = stealthPayment.Metadata.EphemKey.ToBytes(),
                        N = ScanPublicKey(session.RecipientAddress)
                            .Encrypt(Message(session.Amount, 0, blinds[1], "faucet")),
                        P = outPkPayment.ToBytes(),
                        S = Array.Empty<byte>(),
                        T = CoinType.Payment
                    },
                    new Vout
                    {
                        A = 0,
                        D = Array.Empty<byte>(),
                        C = pcmOut[1],
                        E = stealthChange.Metadata.EphemKey.ToBytes(),
                        N = ScanPublicKey(session.SenderAddress).Encrypt(Message(session.Change,
                            session.Amount, blinds[2], string.Empty)),
                        P = outPkChange.ToBytes(),
                        S = Array.Empty<byte>(),
                        T = CoinType.Change
                    }
                }
            };
            var vTime = GenerateTransactionTime(tx.ToHash(), ref tx, 5);
            if (!vTime.Success)
            {
                throw new Exception(vTime.NonSuccessMessage);
            }

            tx.Vtime = vTime.Value;
            tx.TxnId = tx.ToHash();
            return TaskResult<Transaction>.CreateSuccess(tx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return TaskResult<Transaction>.CreateFailure(JObject.FromObject(new
            {
                success = false, message = ex.Message
            }));
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="hash"></param>
    /// <param name="transaction"></param>
    /// <param name="delay"></param>
    /// <returns></returns>
    private TaskResult<Vtime> GenerateTransactionTime(in byte[] hash, ref Transaction transaction, in int delay)
    {
        Vtime vTime;
        try
        {
            var x = System.Numerics.BigInteger.Parse(hash.ByteToHex(),
                System.Globalization.NumberStyles.AllowHexSpecifier);
            if (x.Sign <= 0)
            {
                x = -x;
            }

            var size = transaction.GetSize() / 1024;
            var timer = new Stopwatch();
            var t = (int)(delay * decimal.Round(size, 2, MidpointRounding.ToZero) * 600 * (decimal)1.6);
            timer.Start();
            var nonce = Cryptography.Sloth.Eval(t, x);
            timer.Stop();
            var y = System.Numerics.BigInteger.Parse(nonce);
            var success = Cryptography.Sloth.Verify(t, x, y);
            if (!success)
            {
                {
                    return TaskResult<Vtime>.CreateFailure(JObject.FromObject(new
                    {
                        success = false, message = "Unable to verify the verified delayed function"
                    }));
                }
            }

            if (timer.Elapsed.Ticks < TimeSpan.FromSeconds(5).Ticks)
            {
                return TaskResult<Vtime>.CreateFailure(JObject.FromObject(new
                {
                    success = false,
                    message = "Verified delayed function elapsed seconds is lower the than the default amount"
                }));
            }

            var lockTime = Helpers.Utils.GetAdjustedTimeAsUnixTimestamp() & ~timer.Elapsed.Seconds;
            vTime = new Vtime
            {
                I = t,
                M = hash,
                N = nonce.ToBytes(),
                W = timer.Elapsed.Ticks,
                L = lockTime,
                S = new Script(Op.GetPushOp(lockTime), OpcodeType.OP_CHECKLOCKTIMEVERIFY).ToString().ToBytes()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return TaskResult<Vtime>.CreateFailure(JObject.FromObject(new { success = false, message = ex.Message }));
        }

        return TaskResult<Vtime>.CreateSuccess(vTime);
    }

    /// <summary>
    /// </summary>
    /// <param name="commitIn"></param>
    /// <param name="nCols"></param>
    /// <returns></returns>
    private static byte[] Offsets(Span<byte[]> commitIn, int nCols)
    {
        Guard.Argument(nCols, nameof(nCols)).NotNegative();
        var i = 0;
        const int k = 0;
        var offsets = new byte[nCols * 33];
        var commits = commitIn.GetEnumerator();
        while (commits.MoveNext())
        {
            Buffer.BlockCopy(commits.Current, 0, offsets, (i + k * nCols) * 33, 33);
            i++;
        }

        return offsets;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="balance"></param>
    /// <param name="blindSum"></param>
    /// <param name="commitSum"></param>
    /// <returns></returns>
    private static TaskResult<ProofStruct> BulletProof(ulong balance, byte[] blindSum, byte[] commitSum)
    {
        Guard.Argument(balance, nameof(balance)).NotNegative();
        Guard.Argument(blindSum, nameof(blindSum)).NotNull().NotEmpty();
        Guard.Argument(commitSum, nameof(commitSum)).NotNull().NotEmpty();
        try
        {
            using var bulletProof = new BulletProof();
            using var sec256K1 = new Secp256k1();
            var proofStruct = bulletProof.GenProof(balance, blindSum, sec256K1.RandomSeed(32), null!, null!, null!);
            var success = bulletProof.Verify(commitSum, proofStruct.proof, null!);
            if (success) return TaskResult<ProofStruct>.CreateSuccess(proofStruct);
        }
        catch (Exception ex)
        {
            return TaskResult<ProofStruct>.CreateFailure(JObject.FromObject(new
            {
                success = false, message = ex.Message
            }));
        }

        return TaskResult<ProofStruct>.CreateFailure(JObject.FromObject(new
        {
            success = false, message = "Bulletproof Verify failed"
        }));
    }

    /// <summary>
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    private PubKey ScanPublicKey(string address)
    {
        Guard.Argument(address, nameof(address)).NotNull().NotEmpty().NotWhiteSpace();
        var stealth = new BitcoinStealthAddress(address, Network.TestNet);
        return stealth.ScanPubKey;
    }

    /// <summary>
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="paid"></param>
    /// <param name="blind"></param>
    /// <param name="memo"></param>
    /// <returns></returns>
    private static byte[] Message(ulong amount, ulong paid, byte[] blind, string memo)
    {
        Guard.Argument(amount, nameof(amount)).NotNegative();
        Guard.Argument(paid, nameof(paid)).NotNegative();
        Guard.Argument(blind, nameof(blind)).NotNull().NotEmpty();
        Guard.Argument(memo, nameof(memo)).NotNull();
        return MessagePackSerializer.Serialize(new TransactionMessage
        {
            Amount = amount,
            Blind = blind,
            Memo = memo,
            Date = DateTime.UtcNow,
            Paid = paid
        });
    }

    private (PubKey, StealthPayment) StealthPayment(string address)
    {
        Guard.Argument(address, nameof(address)).NotNull().NotEmpty().NotWhiteSpace();
        var ephemeralKey = new Key();
        var stealth = new BitcoinStealthAddress(address, Network.TestNet);
        var payment = stealth.CreatePayment(ephemeralKey);
        var outPk = stealth.SpendPubKeys[0].UncoverSender(ephemeralKey, stealth.ScanPubKey);
        return (outPk, payment);
    }

    /// <summary>
    /// </summary>
    /// <param name="commitIn"></param>
    /// <param name="commit"></param>
    /// <returns></returns>
    private static bool ContainsCommitment(Span<byte[]> commitIn, byte[] commit)
    {
        Guard.Argument(commit, nameof(commit)).NotEmpty().NotEmpty().MaxCount(33);
        var commits = commitIn.GetEnumerator();
        while (commits.MoveNext())
        {
            if (commits.Current == null) break;
            if (commits.Current.Xor(commit)) return true;
        }

        return false;
    }

    /// <summary>
    /// </summary>
    /// <param name="message"></param>
    /// <param name="scan"></param>
    /// <returns></returns>
    private static ulong Amount(byte[] message, Key scan)
    {
        Guard.Argument(message, nameof(message)).NotNull().NotEmpty();
        Guard.Argument(scan, nameof(scan)).NotNull();
        try
        {
            var amount = MessagePackSerializer.Deserialize<TransactionMessage>(scan.Decrypt(message)).Amount;
            return amount;
        }
        catch (Exception)
        {
            // Ignore
        }

        return 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="scan"></param>
    /// <returns></returns>
    private static TransactionMessage Message(byte[] message, Key scan)
    {
        Guard.Argument(message, nameof(message)).NotNull().NotEmpty();
        Guard.Argument(scan, nameof(scan)).NotNull();
        try
        {
            var transactionMessage = MessagePackSerializer.Deserialize<TransactionMessage>(scan.Decrypt(message));
            return transactionMessage;
        }
        catch (Exception)
        {
            // Ignore
        }

        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="script"></param>
    /// <returns></returns>
    private static bool VerifyLockTime(LockTime target, string script)
    {
        Guard.Argument(target, nameof(target)).NotDefault();
        Guard.Argument(script, nameof(script)).NotNull().NotEmpty().NotWhiteSpace();
        var sc1 = new Script(Op.GetPushOp(target.Value), OpcodeType.OP_CHECKLOCKTIMEVERIFY);
        var sc2 = new Script(script);
        if (!sc1.ToString().Equals(sc2.ToString())) return false;
        var tx = NBitcoin.Network.Main.CreateTransaction();
        tx.Outputs.Add(new TxOut { ScriptPubKey = new Script(script) });
        var spending = NBitcoin.Network.Main.CreateTransaction();
        spending.LockTime = new LockTime(DateTimeOffset.UtcNow);
        spending.Inputs.Add(new TxIn(tx.Outputs.AsCoins().First().Outpoint, new Script()));
        spending.Inputs[0].Sequence = 1;
        return spending.Inputs.AsIndexedInputs().First().VerifyScript(tx.Outputs[0]);
    }
}