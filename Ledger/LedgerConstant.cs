namespace Faucet.Ledger;

/// <summary>
/// 
/// </summary>
public static class LedgerConstant
{
    public const ulong SolutionThrottle = 7_000_000;
    public const int Coin = 1000_000_000;
    public const decimal Distribution = 1_105_000M; // 1105000/5 years = 221,0000 per year.
    public const decimal Reward = 0.07007864028M; // 0.0700786402*6*60*24*365 = 220,999.999987008 per year.
}