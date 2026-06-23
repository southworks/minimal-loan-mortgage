param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceId,

    [Parameter(Mandatory = $true)]
    [string]$LakehouseId,

    [string]$DatasetSeedPath = '',

    [string]$OneLakeEndpoint = 'https://onelake.dfs.fabric.microsoft.com'
)

$ErrorActionPreference = 'Stop'

function Get-RepositoryRootFromInfraScripts {
    $infraDir = Split-Path -Parent $PSScriptRoot
    return (Resolve-Path (Join-Path $infraDir '..')).ProviderPath
}

function Resolve-DatasetSeedPath {
    param([string]$Candidate)

    if (-not [string]::IsNullOrWhiteSpace($Candidate)) {
        return (Resolve-Path -LiteralPath $Candidate).ProviderPath
    }

    $defaultPath = Join-Path (Get-RepositoryRootFromInfraScripts) 'dataset-seed'
    if (-not (Test-Path -LiteralPath $defaultPath)) {
        throw "dataset-seed path not found: $defaultPath"
    }

    return (Resolve-Path -LiteralPath $defaultPath).ProviderPath
}

function Get-OneLakeAccessToken {
    $resource = 'https://storage.azure.com'

    if (-not [string]::IsNullOrWhiteSpace($env:AZURE_CLIENT_ID)) {
        try {
            $uri = "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=$resource&client_id=$($env:AZURE_CLIENT_ID)"
            $resp = Invoke-RestMethod -Uri $uri -Headers @{ Metadata = 'true' }
            if (-not [string]::IsNullOrWhiteSpace($resp.access_token)) {
                return $resp.access_token
            }
        }
        catch {
            Write-Verbose "IMDS token request failed: $_"
        }
    }

    try {
        return (Get-AzAccessToken -ResourceUrl $resource).Token
    }
    catch {}

    $token = az account get-access-token --resource $resource --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($token)) {
        return $token
    }

    throw "Unable to acquire access token for '$resource'."
}

Write-Host '=== Fabric structured files seed ==='
Write-Host "WorkspaceId: $WorkspaceId"
Write-Host "LakehouseId: $LakehouseId"
Write-Host "OneLake endpoint: $OneLakeEndpoint"

$seedRoot = Resolve-DatasetSeedPath -Candidate $DatasetSeedPath
$folders = @('02_identity', '03_income', '04_employment', '05_banking', '06_credit', '07_collateral')

$allFiles = @()
foreach ($folder in $folders) {
    $sourceFolder = Join-Path $seedRoot $folder
    if (-not (Test-Path -LiteralPath $sourceFolder)) {
        Write-Warning "Skipping missing folder: $sourceFolder"
        continue
    }

    $jsonFiles = Get-ChildItem -Path $sourceFolder -Filter '*.json' -File | Sort-Object -Property Name
    foreach ($file in $jsonFiles) {
        $allFiles += [pscustomobject]@{
            FullName = $file.FullName
            Name = $file.Name
            Folder = $folder
            RelativePath = "$folder/$($file.Name)"
        }
    }
}

if ($allFiles.Count -eq 0) {
    throw "No .json structured files found in $seedRoot"
}

Write-Host "Found $($allFiles.Count) JSON files across $($folders.Count) folders."

$token = Get-OneLakeAccessToken

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $allFiles | ForEach-Object -ThrottleLimit 10 -Parallel {
        $file = $_
        $wsId = $using:WorkspaceId
        $lhId = $using:LakehouseId
        $endpoint = $using:OneLakeEndpoint
        $token = $using:token

        $relativeTargetPath = $file.RelativePath
            $targetPath = "$lhId/Files/bronze/$relativeTargetPath"
        $baseUri = "$endpoint/$wsId/$targetPath"
        $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)

        $handler = [System.Net.Http.HttpClientHandler]::new()
        $client = [System.Net.Http.HttpClient]::new($handler)
        try {
            $bearer = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)

            $create = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Put, "$baseUri`?resource=file")
            $create.Headers.Authorization = $bearer
            $resp = $client.SendAsync($create).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Create failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            $content = [System.Net.Http.ByteArrayContent]::new($fileBytes)
            $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
            $content.Headers.ContentLength = $fileBytes.LongLength

            $append = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Patch, "$baseUri`?action=append&position=0")
            $append.Headers.Authorization = $bearer
            $append.Content = $content
            $resp = $client.SendAsync($append).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Append failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            $flush = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Patch, "$baseUri`?action=flush&position=$($fileBytes.LongLength)")
            $flush.Headers.Authorization = $bearer
            $resp = $client.SendAsync($flush).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Flush failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            Write-Host "Uploaded: $relativeTargetPath"
        }
        catch {
            throw "Upload failed for '$($file.FullName)': $($_.Exception.Message) [URI: $baseUri]"
        }
        finally {
            $client.Dispose()
            $handler.Dispose()
        }
    }
}
else {
    Write-Host 'PowerShell 5.x detected. Uploading structured files sequentially (parallel mode requires PowerShell 7+).'

    foreach ($file in $allFiles) {
        $relativeTargetPath = $file.RelativePath
        $targetPath = "$LakehouseId/Files/bronze/$relativeTargetPath"
        $baseUri = "$OneLakeEndpoint/$WorkspaceId/$targetPath"
        $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)

        $handler = [System.Net.Http.HttpClientHandler]::new()
        $client = [System.Net.Http.HttpClient]::new($handler)
        try {
            $bearer = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)

            $create = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Put, "$baseUri`?resource=file")
            $create.Headers.Authorization = $bearer
            $resp = $client.SendAsync($create).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Create failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            $content = [System.Net.Http.ByteArrayContent]::new($fileBytes)
            $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
            $content.Headers.ContentLength = $fileBytes.LongLength

            $append = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Patch, "$baseUri`?action=append&position=0")
            $append.Headers.Authorization = $bearer
            $append.Content = $content
            $resp = $client.SendAsync($append).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Append failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            $flush = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Patch, "$baseUri`?action=flush&position=$($fileBytes.LongLength)")
            $flush.Headers.Authorization = $bearer
            $resp = $client.SendAsync($flush).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Flush failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            Write-Host "Uploaded: $relativeTargetPath"
        }
        catch {
            throw "Upload failed for '$($file.FullName)': $($_.Exception.Message) [URI: $baseUri]"
        }
        finally {
            $client.Dispose()
            $handler.Dispose()
        }
    }
}

Write-Host "Structured files upload completed. Files uploaded: $($allFiles.Count)"
exit 0
