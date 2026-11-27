using System.Diagnostics.CodeAnalysis;

namespace SpaceBattle.Lib
{
    public interface ICommandMessageDecoding
    {
        bool TryDecode(ReadOnlySpan<byte> utf8Json, [MaybeNullWhen(false)] out CommandDescription message, out string errorCode);
        ReadOnlyMemory<byte> EncodeError(string code, string gameId, string message);
    }
}
