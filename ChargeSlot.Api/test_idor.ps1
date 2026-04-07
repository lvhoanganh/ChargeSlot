$apiUrl = 'http://localhost:5162'
$headers = @{ 'Content-Type' = 'application/json' }

function Log($msg) { Write-Host $msg }

function Login($phone, $password) {
    try {
        $body = @{ phoneNumber = $phone; password = $password } | ConvertTo-Json
        $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Headers $headers -Body $body
        return $res.accessToken
    } catch {
        Log "Login Failed for $phone : $($_.Exception.Message)"
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
$adminToken = Login "0900000001" "Admin@123"
$driver1Token = Login "0922222221" "Driver@123"
$driver2Token = Login "0933333331" "Driver@123"
$owner1Token = Login "0911111111" "Owner@123"
$owner2Token = Login "0912222222" "Owner@123"

# Admin fetches all
$authHeaders = @{ 'Authorization' = "Bearer $adminToken"; 'Content-Type' = 'application/json' }
$all = Invoke-RestMethod -Uri "$apiUrl/api/dispute/all" -Method Get -Headers $authHeaders

if ($all.Length -eq 0) {
    Log "No disputes found to test!"
    exit
}

$target = $all[0]
$id = $target.id
$bookingId = $target.bookingId

Log "Target Dispute ID: $id (Booking: $bookingId)"

# 1. Admin access
$r1 = Get-Dispute "/api/dispute/$id" $adminToken
Log "1. Admin access: $(if($r1.success) {"OK"} else {"FAIL($($r1.status))"}) - EXPECT: OK"

# 2. Driver 1 access (Did NOT create the dispute, it was Driver 2)
# Wait, let's see who created it, but driver 2 in test data created it. I'll test both.
$rD1 = Get-Dispute "/api/dispute/$id" $driver1Token
Log "2. Driver 1 access: $(if($rD1.success) {"OK"} else {"FAIL($($rD1.status))"})"

$rD2 = Get-Dispute "/api/dispute/$id" $driver2Token
Log "3. Driver 2 access: $(if($rD2.success) {"OK"} else {"FAIL($($rD2.status))"})"

$rO1 = Get-Dispute "/api/dispute/$id" $owner1Token
Log "4. Owner 1 access (Owns the station): $(if($rO1.success) {"OK"} else {"FAIL($($rO1.status))"})"

$rO2 = Get-Dispute "/api/dispute/$id" $owner2Token
Log "5. Owner 2 access (Does not own): $(if($rO2.success) {"OK"} else {"FAIL($($rO2.status))"})"

