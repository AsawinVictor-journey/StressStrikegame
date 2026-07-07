using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class OpenAIService : MonoBehaviour
{
    private const string OllamaEndpoint = "http://localhost:11434/api/generate";
    private const string DefaultModel = "mistral";
    private string modelId = DefaultModel;

    private void Start()
    {
        string envModel = System.Environment.GetEnvironmentVariable("OLLAMA_MODEL");
        if (!string.IsNullOrEmpty(envModel))
        {
            modelId = envModel;
        }
    }

    public IEnumerator Ask(string prompt, System.Action<string> onSuccess, System.Action<string> onError = null)
    {
        var requestBody = new OllamaRequest
        {
            model = modelId,
            prompt = prompt,
            stream = false
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(OllamaEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 60;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                var response = JsonUtility.FromJson<OllamaResponse>(responseText);
                onSuccess?.Invoke(response.response);
            }
            else
            {
                string error = $"Ollama Error: {request.error}\nMake sure Ollama is running on localhost:11434";
                Debug.LogError(error);
                onError?.Invoke(error);
            }
        }
    }

    [System.Serializable]
    private class OllamaRequest
    {
        public string model;
        public string prompt;
        public bool stream;
    }

    [System.Serializable]
    private class OllamaResponse
    {
        public string response;
    }
}
