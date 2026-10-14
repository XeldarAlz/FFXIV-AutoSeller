using Dalamud.Game;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Driver;

internal sealed class LanguageCachedText<T>(Func<ClientLanguage, T> resolve)
{
    private ClientLanguage? language;
    private T? value;

    public T Get()
    {
        var current = Svc.ClientState.ClientLanguage;
        if (language == current)
        {
            return value!;
        }

        value = resolve(current);
        language = current;
        return value;
    }
}
