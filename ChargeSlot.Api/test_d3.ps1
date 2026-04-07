$apiUrl = 'http://localhost:5162'
$headers = @{ 'Content-Type' = 'application/json' }

$driver3Body = @{ phoneNumber = "0922222223"; password = "Driver@123" } | ConvertTo-Json
$driver3Token = (Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Headers $headers -Body $driver3Body).accessToken
$authHeaders = @{ 'Authorization' = "Bearer $driver3Token"; 'Content-Type' = 'application/json' }
$res = Invoke-RestMethod -Uri "$apiUrl/api/dispute/1" -Method Get -Headers $authHeaders
Write-Host "Driver 3 access (ID 6): $(if ($res.id) {'OK'} else {'FAIL'})"
