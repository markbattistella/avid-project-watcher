param(
    [int]$Port = 47821,
    [int]$DiscoveryPort = 47822,
    [int]$TimeoutMs = 800,
    [int]$DiscoveryWaitMs = 3000,
    [string]$OutputPath
)

$ErrorActionPreference = "SilentlyContinue"
$DiscoveryProbe = "avid-project-watcher-discovery-v1"

function Get-LocalIPv4Addresses {
    $addresses = @()

    if (Get-Command Get-NetIPAddress -ErrorAction SilentlyContinue) {
        $addresses = Get-NetIPAddress -AddressFamily IPv4 |
            Where-Object {
                $_.IPAddress -and
                $_.IPAddress -notlike "127.*" -and
                $_.IPAddress -notlike "169.254.*"
            } |
            Select-Object -ExpandProperty IPAddress
    }

    if (-not $addresses -or $addresses.Count -eq 0) {
        $addresses = Get-WmiObject Win32_NetworkAdapterConfiguration |
            Where-Object { $_.IPEnabled } |
            ForEach-Object { $_.IPAddress } |
            Where-Object {
                $_ -match "^\d+\.\d+\.\d+\.\d+$" -and
                $_ -notlike "127.*" -and
                $_ -notlike "169.254.*"
            }
    }

    return @($addresses | Sort-Object -Unique)
}

function Get-CandidateAddresses {
    param([string[]]$LocalAddresses)

    $localLookup = @{}
    foreach ($address in $LocalAddresses) {
        $localLookup[$address] = $true
    }

    $prefixes = $LocalAddresses |
        ForEach-Object {
            $parts = $_.Split(".")
            if ($parts.Count -eq 4) {
                "$($parts[0]).$($parts[1]).$($parts[2])"
            }
        } |
        Where-Object { $_ } |
        Sort-Object -Unique

    foreach ($prefix in $prefixes) {
        foreach ($hostId in 1..254) {
            $candidate = "$prefix.$hostId"
            if (-not $localLookup.ContainsKey($candidate)) {
                $candidate
            }
        }
    }
}

function Find-DaemonsByUdp {
    param([string[]]$LocalAddresses)

    $results = @()
    $seen = @{}
    $localLookup = @{}
    foreach ($address in $LocalAddresses) {
        $localLookup[$address] = $true
    }

    $client = [System.Net.Sockets.UdpClient]::new()

    try {
        $client.EnableBroadcast = $true
        $client.Client.SetSocketOption(
            [System.Net.Sockets.SocketOptionLevel]::Socket,
            [System.Net.Sockets.SocketOptionName]::ReuseAddress,
            $true)
        $client.Client.Bind([System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, $DiscoveryPort))

        $payload = [System.Text.Encoding]::UTF8.GetBytes($DiscoveryProbe)
        $endpoint = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Broadcast, $DiscoveryPort)
        $client.Send($payload, $payload.Length, $endpoint) | Out-Null

        $deadline = [DateTime]::UtcNow.AddMilliseconds($DiscoveryWaitMs)
        while ([DateTime]::UtcNow -lt $deadline) {
            $remaining = [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds
            if ($remaining -le 0) {
                break
            }

            $async = $client.BeginReceive($null, $null)
            if (-not $async.AsyncWaitHandle.WaitOne($remaining)) {
                break
            }

            $remote = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, 0)
            $buffer = $client.EndReceive($async, [ref]$remote)
            $remoteAddress = $remote.Address.ToString()
            if ($remote.Address.Equals([System.Net.IPAddress]::Loopback) -or $localLookup.ContainsKey($remoteAddress)) {
                continue
            }

            $json = [System.Text.Encoding]::UTF8.GetString($buffer)

            if ($json.Trim() -eq $DiscoveryProbe) {
                continue
            }

            $advertisement = $json | ConvertFrom-Json
            if (-not $advertisement.instanceId -or $seen.ContainsKey($advertisement.instanceId)) {
                continue
            }

            $seen[$advertisement.instanceId] = $true
            $results += [pscustomobject]@{
                Address = $remoteAddress
                MachineName = $advertisement.machineName
                InstanceId = $advertisement.instanceId
            }
        }
    }
    catch {
    }
    finally {
        $client.Dispose()
    }

    return @($results)
}

function Find-DaemonsByHttp {
    param([string[]]$Addresses)

    Add-Type -AssemblyName System.Net.Http

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMilliseconds($TimeoutMs)

    try {
        $requests = foreach ($address in $Addresses) {
            [pscustomobject]@{
                Address = $address
                Task = $client.GetStringAsync("http://${address}:$Port/api/status")
            }
        }

        if (-not $requests -or $requests.Count -eq 0) {
            return @()
        }

        [System.Threading.Tasks.Task[]]$tasks = $requests | ForEach-Object { $_.Task }
        try {
            [System.Threading.Tasks.Task]::WaitAll($tasks, $TimeoutMs + 1500) | Out-Null
        }
        catch {
        }

        foreach ($request in $requests) {
            if (-not $request.Task.IsCompleted -or $request.Task.IsFaulted -or $request.Task.IsCanceled) {
                continue
            }

            $json = $request.Task.Result | ConvertFrom-Json
            if (-not $json.instanceId) {
                continue
            }

            [pscustomobject]@{
                Address = $request.Address
                MachineName = $json.machineName
                InstanceId = $json.instanceId
            }
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

$localAddresses = Get-LocalIPv4Addresses
$candidates = @(Get-CandidateAddresses -LocalAddresses $localAddresses)
$udpDaemons = @(Find-DaemonsByUdp -LocalAddresses $localAddresses)
$httpDaemons = @(Find-DaemonsByHttp -Addresses $candidates)
$seenDaemons = @{}
$daemons = foreach ($daemon in @($udpDaemons + $httpDaemons)) {
    $key = if ($daemon.InstanceId) { $daemon.InstanceId } else { $daemon.Address }
    if (-not $seenDaemons.ContainsKey($key)) {
        $seenDaemons[$key] = $true
        $daemon
    }
}

$lines = foreach ($daemon in $daemons) {
    "$($daemon.MachineName) at $($daemon.Address) ($($daemon.InstanceId))"
}

$lines = @($lines)

if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Set-Content -Path $OutputPath -Value $lines -Encoding UTF8
}
else {
    $lines
}
