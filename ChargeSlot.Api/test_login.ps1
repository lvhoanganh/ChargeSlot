$apiUrl = 'http://localhost:5162'
$headers = @{ 'Content-Type' = 'application/json' }

try {
    $body = @{ phoneNumber = "0900000001"; password = "Admin@123" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Headers $headers -Body $body
    Write-Host "Success!"
} catch {
    $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
    Write-Host "Admin Login Error: $($reader.ReadToEnd())"
}

try {
    $body = @{ phoneNumber = "0933333331"; password = "Driver@123" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Headers $headers -Body $body
    Write-Host "Success!"
} catch {
    $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
    Write-Host "Driver Login Error: $($reader.ReadToEnd())"
}
