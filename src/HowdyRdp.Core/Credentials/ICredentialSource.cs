using HowdyRdp.Core.Models;

namespace HowdyRdp.Core.Credentials;

/// <summary>パスワードの取り出し先。ホストの保管先ごとに 1 つ。</summary>
public interface ICredentialSource
{
    CredentialSourceKind Kind { get; }

    /// <summary>画面に出す名前。</summary>
    string DisplayName { get; }

    /// <summary>
    /// このホストのパスワードを取り出す。本人確認 (Windows Hello) はこの中で行う。
    /// 保存されていなければ <see cref="CredentialResult.NotFound"/>。
    /// </summary>
    Task<CredentialResult> GetPasswordAsync(HostEntry host, CancellationToken cancellationToken = default);
}

public enum CredentialStatus
{
    Ok,
    NotFound,
    Cancelled,
    Unavailable,
    Failed,
}

public sealed record CredentialResult(CredentialStatus Status, string? Password = null, string? Message = null)
{
    public static CredentialResult Success(string password) => new(CredentialStatus.Ok, password);

    public static CredentialResult NotFound(string? message = null) => new(CredentialStatus.NotFound, Message: message);

    public static CredentialResult Cancelled() => new(CredentialStatus.Cancelled, Message: "本人確認がキャンセルされました。");

    public static CredentialResult Unavailable(string message) => new(CredentialStatus.Unavailable, Message: message);

    public static CredentialResult Failed(string message) => new(CredentialStatus.Failed, Message: message);
}
