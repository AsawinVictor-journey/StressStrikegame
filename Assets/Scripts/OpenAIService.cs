using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class OpenAIService : MonoBehaviour
{
    private string apiKey;
    private const string ApiEndpoint = "https://api.openai.com/v1/chat/completions";

    private void Start()
    {
        apiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("OPENAI_API_KEY environment variable not set!");
        }
    }

    public IEnumerator Ask(string prompt, System.Action<string> onSuccess, System.Action<string> onError = null)
    {
        var requestBody = new ChatCompletionRequest
        {
            model = "gpt-4",
            messages = new[] { new Message { role = "user", content = prompt } },
            response_format = new ResponseFormat { type = "json_object" }
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(ApiEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                var response = JsonUtility.FromJson<ChatCompletionResponse>(responseText);
                string messageContent = response.choices[0].message.content;
                onSuccess?.Invoke(messageContent);
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
    private class ResponseFormat
    {
        public string type = "json_object";
    }

    [System.Serializable]
    private class ChatCompletionRequest
    {
        public string model;
        public Message[] messages;
        public ResponseFormat response_format;
    }

    [System.Serializable]
    private class ChatCompletionResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    private class Choice
    {
        public Message message;
    }
}
