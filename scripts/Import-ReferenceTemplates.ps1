[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$true)][uri]$BaseUri,
    [Parameter(Mandatory=$true)][guid]$FacilityId,
    [Parameter(Mandatory=$true)][string]$AccessToken,
    [string]$DefinitionsPath = (Join-Path $PSScriptRoot '..\data\seeds\report-template-definitions.json'),
    [switch]$Apply,
    [string]$DevelopmentUser,
    [guid]$DevelopmentTenant
)
$ErrorActionPreference = 'Stop'
if ($BaseUri.Scheme -ne 'https' -and -not $BaseUri.IsLoopback) { throw 'Use HTTPS outside loopback.' }
if ($FacilityId -eq [guid]::Empty) { throw 'A real facility ID is required.' }
$definitions = Get-Content -LiteralPath $DefinitionsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $Apply) {
    $definitions | Select-Object name, language, modality, bodyRegion, version
    Write-Output 'Preview only. No API calls, migrations or database changes were performed.'
    return
}
$headers = @{ Authorization = 'Bearer ' + $AccessToken }
if ($DevelopmentUser) {
    if (-not $BaseUri.IsLoopback) { throw 'Development identity is local only.' }
    $headers['X-RadIntel-User'] = $DevelopmentUser
    $headers['X-RadIntel-Tenant'] = $DevelopmentTenant.ToString()
}
$rootUri = $BaseUri.AbsoluteUri.TrimEnd('/')
$endpoint = '{0}/api/v1/facilities/{1}/templates' -f $rootUri,$FacilityId
$existing = @(Invoke-RestMethod -Uri $endpoint -Headers $headers -Method Get)
foreach ($definition in $definitions) {
    if ($existing | Where-Object { $_.name -eq $definition.name -and $_.version -eq $definition.version }) { continue }
    if ($PSCmdlet.ShouldProcess($definition.name, 'Create an unpublished reference template version')) {
        $json = $definition | ConvertTo-Json -Depth 5
        $bytes = [Text.Encoding]::UTF8.GetBytes($json)
        $created = Invoke-RestMethod -Uri $endpoint -Headers $headers -Method Post -ContentType 'application/json; charset=utf-8' -Body $bytes
        Write-Output ('Created unpublished template: ' + $created.id)
    }
}
# Publication is deliberately separate and requires clinical review through the API.
