using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Talks to a locally running llama.cpp server (`llama-server`) via its
// OpenAI-compatible /v1/chat/completions endpoint - NOT /completion. The
// loaded model (gemma-4-E4B-it) is instruction-tuned and needs its chat
// template applied; /completion skips that, so a bare prompt string isn't a
// valid turn and the model just emits an empty response (verified via curl:
// "content":"", 1 token, immediate EOS). It's also a "thinking" model that
// free-associates a long chain-of-thought into reasoning_content before
// answering unless told not to - the system message + reasoning_format:"none"
// keep it fast and on-format (confirmed: ~1s instead of 20s+ truncated mid-thought).
public static class LlamaCppClient
{
    private const string Endpoint = "http://localhost:8080/v1/chat/completions";
    private const string SystemPrompt = "Answer with only the required format. Do not think step by step. Do not explain.";

    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class Request
    {
        public ChatMessage[] messages;
        public int max_tokens;
        public float temperature;
        public string reasoning_format;
    }

    [Serializable]
    private class Choice
    {
        public ChatMessage message;
    }

    [Serializable]
    private class Response
    {
        public Choice[] choices;
    }

    public static IEnumerator Generate(string prompt, float timeoutSeconds, Action<string> onSuccess, Action<string> onError = null)
    {
        var request = new Request
        {
            messages = new[]
            {
                new ChatMessage { role = "system", content = SystemPrompt },
                new ChatMessage { role = "user", content = prompt },
            },
            max_tokens = 32,
            temperature = 0.2f,
            reasoning_format = "none",
        };
        string body = JsonUtility.ToJson(request);
        byte[] bytes = Encoding.UTF8.GetBytes(body);

        using (var req = new UnityWebRequest(Endpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"llama.cpp request failed: {req.error}. Is the llama.cpp server running on {Endpoint}?");
                yield break;
            }

            Response parsed;
            try
            {
                parsed = JsonUtility.FromJson<Response>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to parse llama.cpp response: {e.Message}");
                yield break;
            }

            string content = parsed?.choices != null && parsed.choices.Length > 0
                ? parsed.choices[0].message?.content
                : null;

            onSuccess?.Invoke(content?.Trim() ?? "");
        }
    }
}
