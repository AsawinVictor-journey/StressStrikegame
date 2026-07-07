using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class OpenAIService : MonoBehaviour
{
    private string apiKey;
    private const string ApiEndpoint = "https://api.anthropic.com/v1/messages";
    private const string ModelId = "claude-opus-4-8";

    private void Start()
    {
        apiKey = System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("ANTHROPIC_API_KEY environment variable not set!");
        }
    }

    public IEnumerator Ask(string prompt, System.Action<string> onSuccess, System.Action<string> onError = null)
    {
        var requestBody = new MessageRequest
        {
            model = ModelId,
            max_tokens = 1024,
            messages = new[] { new Message { role = "user", content = prompt } }
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(ApiEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", apiKey);
            request.SetRequestHeader("anthropic-version", "2023-06-01");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                var response = JsonUtility.FromJson<MessageResponse>(responseText);
                if (response.content != null && response.content.Length > 0)
                {
                    string messageContent = response.content[0].text;
                    onSuccess?.Invoke(messageContent);
                }
                else
                {
                    onError?.Invoke("Empty response from Claude API");
                }
            }
            else
            {
                string error = $"API Error: {request.error}";
                Debug.LogError(error);
                onError?.Invoke(error);
            }
        }
    }

    [System.Serializable]
    private class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class MessageRequest
    {
        public string model;
        public int max_tokens;
        public Message[] messages;
    }

    [System.Serializable]
    private class MessageResponse
    {
        public ContentBlock[] content;
    }

    [System.Serializable]
    private class ContentBlock
    {
        public string type;
        public string text;
    }
}
