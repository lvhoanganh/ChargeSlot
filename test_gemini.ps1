$body = @{
    contents = @(
        @{ parts = @( @{ text = "Hello" } ) }
    )
} | ConvertTo-Json -Depth 4

$apiKey = "DUMMY" # Since I can't use real key, I will see if error changes for model not found vs bad key.

$url1 = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=$apiKey"
$url2 = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key=$apiKey"
$url3 = "https://generativelanguage.googleapis.com/v1/models/gemini-1.5-flash:generateContent?key=$apiKey"

Write-Host "Testing 1.5-flash v1beta..."
try {
    Invoke-RestMethod -Method Post -Uri $url1 -Body $body -ContentType "application/json"
} catch {
    Write-Host $_.Exception.Response.StatusCode
    Write-Host (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
}

Write-Host "Testing 1.5-flash-latest v1beta..."
try {
    Invoke-RestMethod -Method Post -Uri $url2 -Body $body -ContentType "application/json"
} catch {
    Write-Host $_.Exception.Response.StatusCode
    Write-Host (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
}

Write-Host "Testing 1.5-flash v1..."
try {
    Invoke-RestMethod -Method Post -Uri $url3 -Body $body -ContentType "application/json"
} catch {
    Write-Host $_.Exception.Response.StatusCode
    Write-Host (New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()
}
