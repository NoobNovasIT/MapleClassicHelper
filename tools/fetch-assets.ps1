# Tải sprite quái, NPC và icon item từ maplestory.io (dữ liệu GMS v83, gần với bản Classic) vào Assets/.
# Chạy lại khi muốn thêm hình: thêm id vào các bảng bên dưới rồi chạy
#   powershell -ExecutionPolicy Bypass -File tools/fetch-assets.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'Assets'
$api = 'https://maplestory.io/api/GMS/83'

# tên file => id quái. Mỗi con tải toàn bộ frame của animation "move" (không có thì "stand") để làm sprite động
$mobs = [ordered]@{
    'orange-mushroom' = 1210102
    'green-mushroom'  = 1110100
    'blue-mushroom'   = 2220100
    'snail'           = 100100
    'blue-snail'      = 100101
    'red-snail'       = 130101
    'slime'           = 210100
    'pig'             = 1210100
    'ribbon-pig'      = 1210101
    'stump'           = 130100
    'mushmom'         = 6130101
}

$npcs = [ordered]@{
    'maple-administrator' = 9010000
}

$items = [ordered]@{
    'red-potion'          = 2000000
    'blue-potion'         = 2000003
    'orange-mushroom-cap' = 4000001
    'maple-leaf'          = 4001126
    'snail-shell'         = 4000019
}

# maplestory.io thỉnh thoảng trả 503 khi bị gọi dồn, thử lại vài lần
function Invoke-WithRetry([scriptblock]$action) {
    for ($attempt = 1; ; $attempt++) {
        try { return & $action }
        catch {
            if ($attempt -ge 4) { throw }
            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}

function Save-Url($url, $path) {
    New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
    Invoke-WithRetry { Invoke-WebRequest $url -OutFile $path -UseBasicParsing -TimeoutSec 60 }
}

foreach ($name in $mobs.Keys) {
    $id = $mobs[$name]
    $info = Invoke-WithRetry { Invoke-RestMethod "$api/mob/$id" -TimeoutSec 60 }
    $animation = if ($info.framebooks.move) { 'move' } else { 'stand' }
    $count = [int]$info.framebooks.$animation
    $folder = Join-Path $assets "Sprites/Mobs/$name"
    if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
    for ($frame = 0; $frame -lt $count; $frame++) {
        Save-Url "$api/mob/$id/render/$animation/$frame" (Join-Path $folder "$frame.png")
    }
    Write-Host "mob  $name ($id): $count frame"
}

foreach ($name in $npcs.Keys) {
    Save-Url "$api/npc/$($npcs[$name])/render/stand/0" (Join-Path $assets "Sprites/Npcs/$name.png")
    Write-Host "npc  $name"
}

foreach ($name in $items.Keys) {
    Save-Url "$api/item/$($items[$name])/icon" (Join-Path $assets "Icons/Items/$name.png")
    Write-Host "item $name"
}
