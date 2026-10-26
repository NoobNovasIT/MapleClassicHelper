using System.Text.Json.Nodes;
using MapleHelper.Core.Ai;
using MapleHelper.Infrastructure.Ai;

namespace MapleHelper.Infrastructure.Tests;

public class GeminiParsingTests
{
    [Fact]
    public void Parses_text_skips_thoughts_and_collects_grounding_sources()
    {
        var json = JsonNode.Parse("""
            {"candidates":[{"finishReason":"STOP","content":{"parts":[
                {"text":"thinking...","thought":true},
                {"text":"Nấm Cam rơi ra "},{"text":"Mũ Nấm Cam."}]},
              "groundingMetadata":{"groundingChunks":[
                {"web":{"uri":"https://a.example","title":"a.example"}},
                {"web":{"uri":"https://a.example","title":"a.example"}},
                {"web":{"uri":"https://b.example","title":"b.example"}}]}}]}
            """)!;

        var response = GeminiProvider.ParseGenerateResponse(json);

        Assert.Equal("Nấm Cam rơi ra Mũ Nấm Cam.", response.Text);
        Assert.Equal(["https://a.example", "https://b.example"], response.Sources.Select(s => s.Url));
    }

    [Fact]
    public void Safety_finish_reason_is_refused()
    {
        var json = JsonNode.Parse("""{"candidates":[{"finishReason":"SAFETY"}]}""")!;
        var ex = Assert.Throws<AiException>(() => GeminiProvider.ParseGenerateResponse(json));
        Assert.Equal(AiErrorKind.Refused, ex.Kind);
    }

    [Fact]
    public void Model_list_keeps_only_generateContent_models_without_prefix()
    {
        var json = JsonNode.Parse("""
            {"models":[
              {"name":"models/gemini-2.5-flash","supportedGenerationMethods":["generateContent","countTokens"]},
              {"name":"models/text-embedding-004","supportedGenerationMethods":["embedContent"]}]}
            """)!;

        Assert.Equal(["gemini-2.5-flash"], GeminiProvider.ParseModelList(json));
    }

    [Fact]
    public void Body_includes_image_system_and_search_tool()
    {
        var body = GeminiProvider.BuildGenerateBody(new AiRequest
        {
            Prompt = "dịch", SystemPrompt = "sys", ImagePng = [1, 2, 3], UseWebSearch = true,
        });

        Assert.Equal("AQID", body["contents"]![0]!["parts"]![1]!["inline_data"]!["data"]!.GetValue<string>());
        Assert.Equal("sys", body["system_instruction"]!["parts"]![0]!["text"]!.GetValue<string>());
        Assert.NotNull(body["tools"]![0]!["google_search"]);
    }
}

public class OpenAiParsingTests
{
    [Fact]
    public void Parses_output_text_and_url_citations()
    {
        var json = JsonNode.Parse("""
            {"status":"completed","output":[
              {"type":"web_search_call","status":"completed"},
              {"type":"message","content":[{"type":"output_text","text":"Xin chào",
                "annotations":[{"type":"url_citation","url":"https://x.example","title":"X"}]}]}]}
            """)!;

        var response = OpenAiProvider.ParseGenerateResponse(json);

        Assert.Equal("Xin chào", response.Text);
        Assert.Equal(new AiSource("X", "https://x.example"), Assert.Single(response.Sources));
    }

    [Fact]
    public void Refusal_content_is_refused()
    {
        var json = JsonNode.Parse("""
            {"output":[{"type":"message","content":[{"type":"refusal","refusal":"no"}]}]}
            """)!;
        var ex = Assert.Throws<AiException>(() => OpenAiProvider.ParseGenerateResponse(json));
        Assert.Equal(AiErrorKind.Refused, ex.Kind);
    }

    [Theory]
    [InlineData("gpt-5-mini", true)]
    [InlineData("o4-mini", true)]
    [InlineData("gpt-4.1", false)]
    public void Reasoning_param_only_for_reasoning_models(string model, bool expected)
    {
        var body = OpenAiProvider.BuildGenerateBody(model, new AiRequest { Prompt = "hi" });
        Assert.Equal(expected, body.ContainsKey("reasoning"));
    }

    [Fact]
    public void Model_list_drops_non_chat_models()
    {
        var json = JsonNode.Parse("""
            {"data":[{"id":"gpt-5-mini"},{"id":"gpt-4o-audio-preview"},{"id":"text-embedding-3-small"},{"id":"o3"}]}
            """)!;

        Assert.Equal(["gpt-5-mini", "o3"], OpenAiProvider.ParseModelList(json));
    }
}

public class ClaudeParsingTests
{
    [Fact]
    public void Collects_text_blocks_and_citations_ignoring_tool_blocks()
    {
        var json = JsonNode.Parse("""
            {"stop_reason":"end_turn","content":[
              {"type":"server_tool_use","id":"s1","name":"web_search","input":{"query":"q"}},
              {"type":"web_search_tool_result","tool_use_id":"s1","content":[]},
              {"type":"text","text":"Quest này ","citations":[
                {"type":"web_search_result_location","url":"https://w.example","title":"W","cited_text":"..."}]},
              {"type":"text","text":"nhận ở Henesys."}]}
            """)!;
        var result = new ClaudeProvider.ClaudeResult();

        var stop = ClaudeProvider.AppendResponse(json, result);
        var response = result.ToResponse();

        Assert.Equal("end_turn", stop);
        Assert.Equal("Quest này nhận ở Henesys.", response.Text);
        Assert.Equal("https://w.example", Assert.Single(response.Sources).Url);
    }

    [Fact]
    public void Refusal_stop_reason_is_refused()
    {
        var json = JsonNode.Parse("""{"stop_reason":"refusal","content":[]}""")!;
        var ex = Assert.Throws<AiException>(() => ClaudeProvider.AppendResponse(json, new ClaudeProvider.ClaudeResult()));
        Assert.Equal(AiErrorKind.Refused, ex.Kind);
    }

    [Fact]
    public void Current_generation_model_gets_effort_fallbacks_and_new_search_tool()
    {
        var body = ClaudeProvider.BuildGenerateBody("claude-opus-5-5", new AiRequest { Prompt = "hi", UseWebSearch = true });

        Assert.Equal("low", body["output_config"]!["effort"]!.GetValue<string>());
        Assert.Equal("default", body["fallbacks"]!.GetValue<string>());
        Assert.Equal("web_search_20260209", body["tools"]![0]!["type"]!.GetValue<string>());
        Assert.False(body.ContainsKey("temperature"));
    }

    [Fact]
    public void Older_haiku_gets_no_effort_and_basic_search_tool()
    {
        var body = ClaudeProvider.BuildGenerateBody("claude-haiku-4-5", new AiRequest { Prompt = "hi", UseWebSearch = true });

        Assert.False(body.ContainsKey("output_config"));
        Assert.False(body.ContainsKey("fallbacks"));
        Assert.Equal("web_search_20250305", body["tools"]![0]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Image_block_comes_before_text()
    {
        var body = ClaudeProvider.BuildGenerateBody("claude-opus-5-5", new AiRequest { Prompt = "dịch", ImagePng = [1] });
        var content = body["messages"]![0]!["content"]!.AsArray();

        Assert.Equal("image", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("text", content[1]!["type"]!.GetValue<string>());
    }
}

public class DeepSeekParsingTests
{
    [Fact]
    public void Parses_first_choice_content()
    {
        var json = JsonNode.Parse("""
            {"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":" Anyone want to party? "}}]}
            """)!;

        Assert.Equal("Anyone want to party?", DeepSeekProvider.ParseGenerateResponse(json).Text);
    }

    [Fact]
    public void Body_puts_system_prompt_first()
    {
        var body = DeepSeekProvider.BuildGenerateBody("deepseek-chat", new AiRequest { Prompt = "u", SystemPrompt = "s" });
        var messages = body["messages"]!.AsArray();

        Assert.Equal("system", messages[0]!["role"]!.GetValue<string>());
        Assert.Equal("user", messages[1]!["role"]!.GetValue<string>());
    }
}
