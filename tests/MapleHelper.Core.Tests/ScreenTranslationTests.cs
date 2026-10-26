using MapleHelper.Core.Ai;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.Settings;

namespace MapleHelper.Core.Tests;

public class OcrTextTests
{
    [Fact]
    public void Joins_wrapped_lines_and_keeps_sentence_breaks()
    {
        string[] lines =
        [
            "Hey, you there! I need you to bring",
            "me 10 Orange Mushroom Caps.",
            "Can you do it?",
            "- Reward: 500 EXP",
        ];

        Assert.Equal(
            "Hey, you there! I need you to bring me 10 Orange Mushroom Caps.\nCan you do it?\n- Reward: 500 EXP",
            OcrText.JoinLines(lines));
    }

    [Fact]
    public void Skips_blank_lines() => Assert.Equal("a b", OcrText.JoinLines(["a", "  ", "b"]));
}

public class ScreenTranslationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Google_mode_translates_joined_ocr_text_en_to_vi()
    {
        var machine = new FakeMachine();
        var service = new TranslationService(new NoAi(), machine, new FakeOcr(["Bring me", "10 caps."]), new AiServiceTests.FakeSettings());

        var result = await service.TranslateScreenAsync([1], Ct);

        Assert.Equal("Bring me 10 caps.", result.SourceText);
        Assert.Equal("vi:Bring me 10 caps.", result.Translation);
        Assert.Equal(("auto", "vi"), machine.LastLanguages);
    }

    [Fact]
    public async Task Google_mode_reports_when_no_text_found()
    {
        var service = new TranslationService(new NoAi(), new FakeMachine(), new FakeOcr([]), new AiServiceTests.FakeSettings());

        var result = await service.TranslateScreenAsync([1], Ct);

        Assert.Equal(TranslationService.NoTextFound, result.Translation);
    }

    [Fact]
    public async Task Ai_mode_does_not_use_ocr()
    {
        var settings = new AiServiceTests.FakeSettings();
        settings.Current.Overlay.ScreenTranslateEngine = ScreenTranslateEngine.Ai;
        var ai = new EchoAi("""{"source": "Hi", "translation": "Chào"}""");
        var service = new TranslationService(ai, new FakeMachine(), new ThrowingOcr(), settings);

        var result = await service.TranslateScreenAsync([1], Ct);

        Assert.Equal("Chào", result.Translation);
    }

    private sealed class FakeOcr(IReadOnlyList<string> lines) : ITextRecognizer
    {
        public Task<IReadOnlyList<string>> RecognizeLinesAsync(byte[] png, CancellationToken ct = default) => Task.FromResult(lines);
    }

    private sealed class ThrowingOcr : ITextRecognizer
    {
        public Task<IReadOnlyList<string>> RecognizeLinesAsync(byte[] png, CancellationToken ct = default) =>
            throw new InvalidOperationException("OCR không được gọi ở chế độ AI");
    }

    private sealed class FakeMachine : IMachineTranslator
    {
        public (string, string)? LastLanguages { get; private set; }

        public Task<string> TranslateAsync(string text, string from, string to, CancellationToken ct = default)
        {
            LastLanguages = (from, to);
            return Task.FromResult($"{to}:{text}");
        }
    }

    private sealed class NoAi : IAiService
    {
        public IAiProvider ActiveProvider => throw new NotSupportedException();
        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("AI không được gọi ở chế độ Google");
    }

    private sealed class EchoAi(string reply) : IAiService
    {
        public IAiProvider ActiveProvider => throw new NotSupportedException();
        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken ct = default) =>
            Task.FromResult(AiResponse.FromText(reply));
    }
}
