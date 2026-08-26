using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Talks to the StressStrike backend's Gemini proxy (server/server.js, POST
// /api/gemini/generate) - never calls Google's API directly from the client.
// A build can always be extracted and its strings read, so the Gemini API key
// must never ship inside the game; the backend holds it in server/.env
// (GEMINI_API_KEY) the same way it already holds the MongoDB connection
// string for CloudSyncService. Same server for dev (localhost) and prod
// (deploy it once, point BaseUrl at it) - no local model server required.
public static class GeminiClient
{
    private const string DefaultModel = "gemini-3.5-flash-lite";
    private const int DefaultTimeoutSeconds = 15;

    // Override via the STRESSSTRIKE_API_BASE_URL environment variable for a
    // build that should talk to a deployed backend instead of localhost.
    private static string BaseUrl
    {
        get
        {
            string url = Environment.GetEnvironmentVariable("STRESSSTRIKE_API_BASE_URL");
            return string.IsNullOrEmpty(url) ? "http://localhost:3000" : url.TrimEnd('/');
        }
    }

    [Serializable]
    private class Request
    {
        public string model;
        public string prompt;
        public int maxOutputTokens;
        public float temperature;
    }

    [Serializable]
    private class Response
    {
        public string text;
        public string error;
    }

    public static IEnumerator Generate(string prompt, Action<string> onSuccess, Action<string> onError = null)
        => Generate(DefaultModel, prompt, DefaultTimeoutSeconds, onSuccess, onError);

    public static IEnumerator Generate(string model, string prompt, Action<string> onSuccess, Action<string> onError = null)
        => Generate(model, prompt, DefaultTimeoutSeconds, onSuccess, onError);

    public static IEnumerator Generate(string prompt, float timeoutSeconds, Action<string> onSuccess, Action<string> onError = null)
        => Generate(DefaultModel, prompt, timeoutSeconds, onSuccess, onError);

    public static IEnumerator Generate(string model, string prompt, float timeoutSeconds, Action<string> onSuccess, Action<string> onError = null)
    {
        var request = new Request
        {
            model = model,
            prompt = prompt,
            maxOutputTokens = 64,
            temperature = 0.2f,
        };
        string body = JsonUtility.ToJson(request);
        byte[] bytes = Encoding.UTF8.GetBytes(body);

        using (var req = new UnityWebRequest(BaseUrl + "/api/gemini/generate", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Gemini request failed: {req.error}. Is the StressStrike API running at {BaseUrl}?");
                yield break;
            }

            Response parsed;
            try
            {
                parsed = JsonUtility.FromJson<Response>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to parse Gemini response: {e.Message}");
                yield break;
            }

            if (!string.IsNullOrEmpty(parsed?.error))
            {
                onError?.Invoke($"Gemini error: {parsed.error}");
                yield break;
            }

            onSuccess?.Invoke(parsed?.text?.Trim() ?? "");
        }
    }
}
