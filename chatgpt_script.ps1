param (
    [string]$Prompt
)

# 1. Your OpenAI API Configuration
$ApiKey = "YOUR_CHATGPT_API_KEY_HERE"
$Url = "https://openai.com"

# 2. Build the request payload
$Body = @{
    model = "gpt-4o"
    messages = @(
        @{ role = "user"; content = $Prompt }
    )
} | ConvertTo-Json -Depth 10

# 3. Headers required by OpenAI
$Headers = @{
    "Authorization" = "Bearer $ApiKey"
    "Content-Type"  = "application/json"
}

# 4. Send request and return just the text answer to Hermes
try {
    $Response = Invoke-RestMethod -Uri $Url -Method Post -Headers $Headers -Body $Body
    Write-Output $Response.choices[0].message.content
} catch {
    Write-Error "ChatGPT Script Error: $_"
}
