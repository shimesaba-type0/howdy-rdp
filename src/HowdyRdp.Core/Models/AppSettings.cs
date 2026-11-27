namespace HowdyRdp.Core.Models;

public sealed class AppSettings
{
    /// <summary>Hello の確認を省略する分数として選べる値。0 は「接続のたびに確認する」。</summary>
    public static readonly IReadOnlyList<int> AllowedHelloSkipMinutes = [0, 1, 5, 15];

    /// <summary>一度 Hello を通したあと、確認を省略する分数。既定は 0 (毎回確認)。</summary>
    public int HelloSkipMinutes { get; set; }

    public AppSettings Normalize()
    {
        if (!AllowedHelloSkipMinutes.Contains(HelloSkipMinutes))
        {
            HelloSkipMinutes = 0;
        }

        return this;
    }
}
