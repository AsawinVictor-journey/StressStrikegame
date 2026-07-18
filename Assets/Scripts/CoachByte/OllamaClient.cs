using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Talks to a locally running Ollama server - no Python, no cloud API key.
// Setup (once): install Ollama, then `ollama pull gemma3:4b`. Ollama's local
// server starts automatically and listens on localhost:11434.
public static class OllamaClient
{
    private const string Endpoint = "http://localhost:11434/api/generate";
    private const int TimeoutSeconds = 20;

    [Serializable]
    private class Request
    {
        public string model;
        public string prompt;
        public bool stream;
    }

    [Serializable]
    private class Response
    {
        public string response;
    }

    // Call from any MonoBehaviour: StartCoroutine(OllamaClient.Generate(model, prompt, OnDone, OnError));
    // stream is always false so the whole reply comes back as one JSON object instead
    // of needing an NDJSON streaming parser.
    public static IEnumerator Generate(string model, string prompt, Action<string> onSuccess, Action<string> onError = null)
    {
        string body = JsonUtility.ToJson(new Request { model = model, prompt = prompt, stream = false });
        byte[] bytes = Encoding.UTF8.GetBytes(body);

        using (var req = new UnityWebRequest(Endpoint, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = TimeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Ollama request failed: {req.error}. Is Ollama running with '{model}' pulled? (ollama pull {model})");
                yield break;
            }

            Response parsed;
            try
            {
                parsed = JsonUtility.FromJson<Response>(req.downloadHandler.text);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to parse Ollama response: {e.Message}");
                yield break;
            }

            onSuccess?.Invoke(parsed?.response?.Trim() ?? "");
        }
    }
}
