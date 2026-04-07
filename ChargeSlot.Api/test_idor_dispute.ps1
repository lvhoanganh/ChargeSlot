$apiUrl = 'http://localhost:5162'
$headers = @{ 'Content-Type' = 'application/json' }

function Log($msg) { Write-Host $msg }

function Login($phone, $password) {
    try {
        $body = @{ phoneNumber = $phone; password = $password } | ConvertTo-Json
        $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Headers $headers -Body $body
        return $res.accessToken
    } catch {
        Log "Login Failed for $phone"
        return $null
    }
}

function Get-Dispute($endpoint, $token) {
    try {
        $authHeaders = @{ 'Authorization' = "Bearer $token"; 'Content-Type' = 'application/json' }
        $res = Invoke-RestMethod -Uri "$apiUrl$endpoint" -Method Get -Headers $authHeaders
        return @{ success = $true; data = $res }
    } catch {
        return @{ success = $false; status = $_.Exception.Response.StatusCode.value__ }
    }
}

Log '--- TEST IDOR DISPUTE DETAILED API ---'
$driver1Token = Login "0922222221" "Driver@123"
$driver2Token = Login "0922222222" "Driver@123"
$owner1Token = Login "0911111111" "Owner@123"
$owner2Token = Login "0911111112" "Owner@123"

# Find a valid dispute first using Owner1 token, since we know Owner1 has 1 dispute!
$authHeaders = @{ 'Authorization' = "Bearer $owner1Token"; 'Content-Type' = 'application/json' }
$all = Invoke-RestMethod -Uri "$apiUrl/api/dispute/owner" -Method Get -Headers $authHeaders

if ($all.Length -eq 0) {
    Log "No disputes found to test!"
    exit
}

$id = $all[0].id
$bookingId = $all[0].bookingId
$creatorId = $all[0].createdByUserId

Log "Target Dispute ID: $id (Booking: $bookingId)"
Log "Created by UserId: $creatorId"

if ($creatorId -eq 4) {
    Log "Driver 1 created this."
} else {
    Log "Driver 2 (or 3) created this."
}

$rD1 = Get-Dispute "/api/dispute/$id" $driver1Token
Log "Driver 1 access (ID 4): $(if($rD1.success) {"OK"} else {"FAIL($($rD1.status))"})"

$rD2 = Get-Dispute "/api/dispute/$id" $driver2Token
Log "Driver 2 access (ID 5): $(if($rD2.success) {"OK"} else {"FAIL($($rD2.status))"})"

$rO1 = Get-Dispute "/api/dispute/$id" $owner1Token
Log "Owner 1 access (Owns the station): $(if($rO1.success) {"OK"} else {"FAIL($($rO1.status))"})"

$rO2 = Get-Dispute "/api/dispute/$id" $owner2Token
Log "Owner 2 access (Does not own): $(if($rO2.success) {"OK"} else {"FAIL($($rO2.status))"})"

