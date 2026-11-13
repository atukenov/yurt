namespace Yurt.Application.Features.CardTerminals;

/// <summary>
/// Terminal adapters Yurt knows by name. <c>simulator</c> is for tests and demos. <c>kaspi</c> and <c>halyk</c> are
/// reserved for the real adapters, which wait for the banks' integration documents (see docs/card-terminals.md): until an
/// adapter exists, a terminal using that name works only through the cafe agent, never from the server.
/// </summary>
public static class CardTerminalProviders
{
    public const string Simulator = "simulator", Kaspi = "kaspi", Halyk = "halyk";

    public static readonly string[] All = [Simulator, Kaspi, Halyk];
}
