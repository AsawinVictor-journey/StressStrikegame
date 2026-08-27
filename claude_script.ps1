param (
    [string]$Prompt
)

# 1. Your Claude API Configuration
$ApiKey = "YOUR_CLAUDE_API_KEY_HERE"
$Url = "https://anthropic.com"

# 2. Build the request payload
$Body = @{
    model = "claude-3-5-sonnet-latest"
    max_tokens = 1024
    messages = @(
        @{ role = "user"; content = $Prompt }
    )
} | ConvertTo-Json -Depth 10

# 3. Headers required by Anthropic
$Headers = @{
    "x-api-key"         = $ApiKey
    "anthropic-version" = "2023-06-01"
    "content-type"      = "application/json"
}

# 4. Send request safely and catch detailed errors
try {
    $Response = Invoke-RestMethod -Uri $Url -Method Post -Headers $Headers -Body $Body
    
    # Check if the content property exists before grabbing text
    if ($Response.content -and $Response.content.text) {
        Write-Output $Response.content.text
    } else {
        # Fallback if the structure is different
        Write-Output ($Response | ConvertTo-Json -Depth 3)
    }
} catch {
    # Compatible version for older PowerShell (Using traditional if/else)
    if ($_.Exception.Response) {
        $Reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $ErrorDetails = $Reader.ReadToEnd()
        $Reader.Close()
    } else {
        $ErrorDetails = $_.Exception.Message
    }
    Write-Error "Claude Server Error Details: $ErrorDetails"
}
