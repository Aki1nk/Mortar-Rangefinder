param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

$title = 'Mortar Rangefinder 使用说明书'
$sections = @(
    @{ Heading = '1. 安装'; Lines = @(
        '双击“Mortar Rangefinder-Setup.exe”。',
        '按安装向导完成安装；默认安装到当前 Windows 用户的应用目录，无需管理员权限。',
        '安装完成后可从开始菜单或桌面快捷方式启动。'
    )},
    @{ Heading = '2. 首次标定'; Lines = @(
        '打开 PUBG 地图并调整到准备测距时使用的缩放级别。',
        '按默认智能热键 F8，随后在地图上依次点击两个已知实际距离的点。',
        '在弹出的输入框中填写这两个点之间的真实距离（米），完成标定。',
        '需要重新标定时按默认热键 Ctrl+F8，再依次点击两个标定点。',
        '地图缩放、分辨率、DPI 或显示器排列发生变化后，请重新标定。'
    )},
    @{ Heading = '3. 测距'; Lines = @(
        '完成标定后再次按 F8。',
        '先点击迫击炮位置，再点击目标位置。',
        '两点之间会显示黄色细虚线，并在 2 秒后自动擦除。',
        '悬浮结果条会显示直线距离、方位角与射程状态。'
    )},
    @{ Heading = '4. 设置与热键'; Lines = @(
        '点击主窗口的“设置”打开设置界面。',
        '点击热键输入框后直接按下新的组合键，保存后主窗口会立即显示新的键名。',
        '可在设置中调整最小和最大射程；相同的热键不能重复分配。',
        '默认按 Ctrl+F12 播放本地语音提示；可在设置中关闭或改为其他热键。'
    )},
    @{ Heading = '5. 多显示器'; Lines = @(
        '选点遮罩会覆盖所有已连接显示器，鼠标可跨屏点击。',
        '建议保持游戏窗口、地图缩放和显示器布局稳定，以获得一致的测距结果。'
    )},
    @{ Heading = '6. 安全说明'; Lines = @(
        '本工具只使用鼠标手动选点和屏幕坐标比例计算距离。',
        '不读取游戏进程或内存，不注入游戏，不自动控制鼠标，也不访问网络。'
    )},
    @{ Heading = '7. 卸载'; Lines = @(
        '在 Windows“已安装的应用”中找到“Mortar Rangefinder”并卸载。',
        '也可在开始菜单的工具文件夹中选择卸载程序。'
    )}
)

function Escape-Xml([string]$Value) {
    return [System.Security.SecurityElement]::Escape($Value)
}

$paragraphs = [System.Collections.Generic.List[string]]::new()
$paragraphs.Add(('<w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val="36"/></w:rPr><w:t>') + (Escape-Xml $title) + '</w:t></w:r></w:p>') | Out-Null
$paragraphs.Add('<w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:color w:val="666666"/></w:rPr><w:t>版本 0.2.1 · 2026-08-21 · Windows 10/11 x64</w:t></w:r></w:p>') | Out-Null
$paragraphs.Add('<w:p><w:r><w:t xml:space="preserve"> </w:t></w:r></w:p>') | Out-Null

foreach ($section in $sections) {
    $paragraphs.Add(('<w:p><w:r><w:rPr><w:b/><w:sz w:val="28"/></w:rPr><w:t>') + (Escape-Xml $section.Heading) + '</w:t></w:r></w:p>') | Out-Null
    foreach ($line in $section.Lines) {
        $paragraphs.Add(('<w:p><w:pPr><w:ind w:left="360"/></w:pPr><w:r><w:t>• ') + (Escape-Xml $line) + '</w:t></w:r></w:p>') | Out-Null
    }
}

$documentXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    $($paragraphs -join "`n    ")
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440"/>
    </w:sectPr>
  </w:body>
</w:document>
"@

$contentTypesXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
</Types>
'@

$relationshipsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
'@

$docxPath = Join-Path $OutputDirectory '使用说明书.docx'
if (Test-Path $docxPath) {
    Remove-Item -LiteralPath $docxPath -Force
}

$archive = [System.IO.Compression.ZipFile]::Open($docxPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($entryDefinition in @(
        @{ Path = '[Content_Types].xml'; Content = $contentTypesXml },
        @{ Path = '_rels/.rels'; Content = $relationshipsXml },
        @{ Path = 'word/document.xml'; Content = $documentXml }
    )) {
        $entry = $archive.CreateEntry($entryDefinition.Path)
        $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
        try {
            $writer.Write($entryDefinition.Content)
        }
        finally {
            $writer.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}

$htmlSections = foreach ($section in $sections) {
    $items = foreach ($line in $section.Lines) {
        "<li>$([System.Net.WebUtility]::HtmlEncode($line))</li>"
    }
    "<section><h2>$([System.Net.WebUtility]::HtmlEncode($section.Heading))</h2><ul>$($items -join '')</ul></section>"
}

$html = @"
<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>$title</title>
<style>
@page { size: A4; margin: 20mm; }
body { color: #15212a; font-family: "Microsoft YaHei", "Segoe UI", sans-serif; line-height: 1.7; }
h1 { color: #203a4a; font-size: 27px; margin-bottom: 0; text-align: center; }
.meta { color: #63727c; margin: 4px 0 26px; text-align: center; }
h2 { border-bottom: 2px solid #b9e46e; color: #203a4a; font-size: 18px; margin-top: 20px; padding-bottom: 3px; }
ul { margin: 6px 0; padding-left: 24px; }
li { margin-bottom: 5px; }
</style>
</head>
<body>
<h1>$title</h1>
<p class="meta">版本 0.2.1 · 2026-08-21 · Windows 10/11 x64</p>
$($htmlSections -join "`n")
</body>
</html>
"@

[System.IO.File]::WriteAllText((Join-Path $OutputDirectory '使用说明书.html'), $html, [System.Text.UTF8Encoding]::new($false))
