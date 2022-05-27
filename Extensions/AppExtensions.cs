using System.Security.Cryptography.X509Certificates;
using Faucet.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Faucet.Extensions;

/// <summary>
/// 
/// </summary>
public static class AppExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddDataKeysProtection(this IServiceCollection services,
        IConfiguration configuration)
    {
        X509Certificate2? certificate;
        if (!string.IsNullOrEmpty(configuration["X509Certificate:CertPath"]) &&
            !string.IsNullOrEmpty(configuration["X509Certificate:Password"]))
            certificate = new X509Certificate2(configuration["X509Certificate:CertPath"],
                configuration["X509Certificate:Password"]);
        else
            certificate =
                new CertificateResolver().ResolveCertificate(configuration["X509Certificate:Thumbprint"]);

        if (certificate != null)
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Utils.EntryAssemblyPath(),
                    "Keys"))).ProtectKeysWithCertificate(certificate)
                .SetApplicationName("Faucet").SetDefaultKeyLifetime(TimeSpan.FromDays(3650));
        return services;
    }
}