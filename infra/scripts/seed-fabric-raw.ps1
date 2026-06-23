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

function Convert-ApplicationJsonToRawText {
    param(
        [string]$ApplicationPath,
        [pscustomobject]$Application
    )

    $jsonPayload = Get-Content -LiteralPath $ApplicationPath -Raw

    return @"
Loan application request document
Source file: $([IO.Path]::GetFileName($ApplicationPath))
Application ID: $($Application.application_id)
Borrower: $($Application.borrower.full_name)
Loan purpose: $($Application.loan_purpose)
Requested loan amount: $($Application.requested_loan_amount)

=== Application payload ===
$jsonPayload
"@
}

function Ensure-RawFolderFromApplications {
    param([string]$RootPath)

    $rawRoot = Join-Path $RootPath '00_raw'
    if (-not (Test-Path -LiteralPath $rawRoot)) {
        New-Item -ItemType Directory -Path $rawRoot -Force | Out-Null
    }

    $applicationsPath = Join-Path $RootPath '01_application'
    if (-not (Test-Path -LiteralPath $applicationsPath)) {
        throw "01_application path not found: $applicationsPath"
    }

    $applicationFiles = Get-ChildItem -Path $applicationsPath -Filter '*.json' -File | Sort-Object -Property Name
    if ($applicationFiles.Count -eq 0) {
        throw "No application files found in $applicationsPath"
    }

    foreach ($file in $applicationFiles) {
        $app = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($app.application_id)) {
            throw "application_id missing in $($file.FullName)"
        }

        $appRawPath = Join-Path $rawRoot $app.application_id
        New-Item -ItemType Directory -Path $appRawPath -Force | Out-Null

        $outputFile = Join-Path $appRawPath 'loan_application.txt'
        $text = Convert-ApplicationJsonToRawText -ApplicationPath $file.FullName -Application $app
        Set-Content -LiteralPath $outputFile -Value $text -Encoding UTF8
    }

    return $rawRoot
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

Write-Host '=== Fabric raw seed ==='
Write-Host "WorkspaceId: $WorkspaceId"
Write-Host "LakehouseId: $LakehouseId"
Write-Host "OneLake endpoint: $OneLakeEndpoint"

$seedRoot = Resolve-DatasetSeedPath -Candidate $DatasetSeedPath
$rawRootPath = Join-Path $seedRoot '00_raw'

$rawFileExtensions = @('.txt', '.pdf', '.png')
$rawFiles = Get-ChildItem -Path $rawRootPath -File -Recurse |
    Where-Object { $rawFileExtensions -contains $_.Extension.ToLowerInvariant() } |
    Sort-Object -Property FullName
if ($rawFiles.Count -eq 0) {
    throw "No raw files (.txt, .pdf, .png) found in $rawRootPath"
}

$rawRootNormalized = (Resolve-Path -LiteralPath $rawRootPath).ProviderPath
if (-not $rawRootNormalized.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
    $rawRootNormalized += [System.IO.Path]::DirectorySeparatorChar
}

$token = Get-OneLakeAccessToken

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $rawFiles | ForEach-Object -ThrottleLimit 10 -Parallel {
        $file = $_
        $wsId = $using:WorkspaceId
        $lhId = $using:LakehouseId
        $endpoint = $using:OneLakeEndpoint
        $token = $using:token
        $rawRoot = $using:rawRootNormalized

        $fullFilePath = [System.IO.Path]::GetFullPath($file.FullName)
        if (-not $fullFilePath.StartsWith($rawRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Raw file path '$fullFilePath' is outside expected root '$rawRoot'."
        }

        $relativeTargetPath = $fullFilePath.Substring($rawRoot.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [char]'/' ).Replace('\\', '/')

        $targetPath = "$lhId/Files/raw/$relativeTargetPath"
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
    Write-Host 'PowerShell 5.x detected. Uploading raw files sequentially (parallel mode requires PowerShell 7+).'

    foreach ($file in $rawFiles) {
        $fullFilePath = [System.IO.Path]::GetFullPath($file.FullName)
        if (-not $fullFilePath.StartsWith($rawRootNormalized, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Raw file path '$fullFilePath' is outside expected root '$rawRootNormalized'."
        }

        $relativeTargetPath = $fullFilePath.Substring($rawRootNormalized.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [char]'/' ).Replace('\\', '/')

        $targetPath = "$LakehouseId/Files/raw/$relativeTargetPath"
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

Write-Host "Raw upload completed. Files uploaded: $($rawFiles.Count)"
exit 0
