using System.Reflection;

namespace AgentRelay.Core;

public static class RelayVersion
{
    public static string Current => typeof(RelayVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
}
