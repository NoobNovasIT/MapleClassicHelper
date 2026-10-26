namespace MapleHelper.Core.Ai;

public enum AiErrorKind
{
    MissingApiKey,
    InvalidApiKey,
    QuotaExceeded,
    ModelNotFound,
    NotSupported,
    Refused,
    Network,
    Unknown,
}

/// <summary>Lỗi đã chuẩn hoá từ mọi provider. Message là câu tiếng Việt hiển thị thẳng cho người dùng.</summary>
public sealed class AiException : Exception
{
    public AiErrorKind Kind { get; }

    public AiException(AiErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }
}
