using System.Runtime.CompilerServices;
using ILogger = Serilog.ILogger;

namespace Faucet.Extensions;

public static class LoggerExtension
{
    public static ILogger Here(this ILogger logger,
        [CallerMemberName] string memberName = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        return logger
            .ForContext("MemberName", memberName)
            .ForContext("LineNumber", sourceLineNumber.ToString());
    }
}