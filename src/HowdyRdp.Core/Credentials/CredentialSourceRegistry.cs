using HowdyRdp.Core.Models;

namespace HowdyRdp.Core.Credentials;

/// <summary>保管先の種類から取り出し先を引く。</summary>
public sealed class CredentialSourceRegistry
{
    private readonly Dictionary<CredentialSourceKind, ICredentialSource> _sources;

    public CredentialSourceRegistry(IEnumerable<ICredentialSource> sources)
    {
        _sources = sources.ToDictionary(s => s.Kind);
    }

    public IEnumerable<ICredentialSource> All => _sources.Values;

    public ICredentialSource Get(CredentialSourceKind kind) =>
        _sources.TryGetValue(kind, out var source)
            ? source
            : throw new InvalidOperationException($"保管先 {kind} は登録されていません。");

    public static string DisplayNameOf(CredentialSourceKind kind) => kind switch
    {
        CredentialSourceKind.BuiltIn => "内蔵",
        CredentialSourceKind.KeePassXc => "KeePassXC",
        _ => kind.ToString(),
    };
}
