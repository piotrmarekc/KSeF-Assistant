namespace KSeFAssistant.Core.Models;

public enum KSeFEnvironment
{
    Test,
    Demo,
    Production
}

public static class KSeFEnvironmentExtensions
{
    public static string GetBaseUrl(this KSeFEnvironment env) => env switch
    {
        KSeFEnvironment.Test       => "https://api-test.ksef.mf.gov.pl/",
        KSeFEnvironment.Demo       => "https://api-demo.ksef.mf.gov.pl/",
        KSeFEnvironment.Production => "https://api.ksef.mf.gov.pl/",
        _                          => throw new ArgumentOutOfRangeException(nameof(env))
    };
}

public enum AuthMethod
{
    Token,
    Certificate
}
