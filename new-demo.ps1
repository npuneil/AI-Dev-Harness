# new-demo.ps1
# ---------------------------------------------------------------------------
# Interactive generator for AI-Dev-Harness. Asks 9 questions (silicon-aware
# defaults), then stamps out a self-contained Local or Hybrid AI demo
# solution under .\demos\<name>\, wires up your tabs, sets MSIX identity,
# optionally inits a git repo and creates it on GitHub.
#
# This is the "vibe-coding entry point" — teammates shouldn't need to know
# WinUI 3 or this harness's internals to get a customised prototype running.
#
# Usage:
#   .\new-demo.ps1                          # full interactive flow
#   .\new-demo.ps1 -Name MyDemo -Source Local -NonInteractive
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [string] $Name,
    [ValidateSet('Local','Hybrid')] [string] $Source,
    [ValidateSet('x64','ARM64','both')] [string] $Architecture,
    [string] $Industry,
    [ValidateSet('text','vision','both')] [string] $Modality,
    [string] $ModelAlias,
    [string] $VisionModelAlias,
    [string[]] $UseCases,
    [string[]] $Tabs,
    [switch] $MockByDefault,
    [switch] $InitGit,
    [string] $GitHubRepo,
    [switch] $NonInteractive
)

$ErrorActionPreference = 'Stop'

# ---------- helpers ----------------------------------------------------------

function Write-Banner($text) {
    Write-Host ""
    Write-Host ("-" * 70) -ForegroundColor DarkGray
    Write-Host " $text" -ForegroundColor Cyan
    Write-Host ("-" * 70) -ForegroundColor DarkGray
}

function Read-Choice($prompt, $choices, $default) {
    if ($NonInteractive) { return $default }
    $hint = $choices -join '/'
    while ($true) {
        $resp = Read-Host "$prompt [$hint] (default: $default)"
        if ([string]::IsNullOrWhiteSpace($resp)) { return $default }
        $match = $choices | Where-Object { $_ -ieq $resp } | Select-Object -First 1
        if ($match) { return $match }
        Write-Host "Pick one of: $hint" -ForegroundColor Yellow
    }
}

function Read-DefaultedString($prompt, $default) {
    if ($NonInteractive) { return $default }
    $resp = Read-Host "$prompt (default: $default)"
    if ([string]::IsNullOrWhiteSpace($resp)) { return $default }
    return $resp
}

function Detect-Silicon {
    try {
        $cpu = (Get-CimInstance Win32_Processor -ErrorAction Stop | Select-Object -First 1).Name
    } catch {
        return [PSCustomObject]@{ Vendor='Unknown'; Name='Unknown CPU'; DefaultArch='x64'; DefaultModel='phi-4-mini' }
    }
    if     ($cpu -match 'Snapdragon|Qualcomm|Oryon') { return [PSCustomObject]@{ Vendor='Qualcomm'; Name=$cpu; DefaultArch='ARM64'; DefaultModel='qwen2.5-3b' } }
    elseif ($cpu -match 'Intel|Core Ultra')          { return [PSCustomObject]@{ Vendor='Intel';    Name=$cpu; DefaultArch='x64';   DefaultModel='phi-4-mini' } }
    elseif ($cpu -match 'AMD|Ryzen')                 { return [PSCustomObject]@{ Vendor='AMD';      Name=$cpu; DefaultArch='x64';   DefaultModel='phi-4-mini' } }
    else                                             { return [PSCustomObject]@{ Vendor='Unknown';  Name=$cpu; DefaultArch='x64';   DefaultModel='phi-4-mini' } }
}

function Sanitize-Identifier($s) {
    $clean = ($s -replace '[^A-Za-z0-9]','')
    if (-not $clean) { throw "Demo name must include at least one letter or digit." }
    if ($clean -match '^[0-9]') { $clean = "Demo$clean" }
    return $clean
}

# ---------- industry suggestions --------------------------------------------
$IndustryCatalog = @{
    'Healthcare' = @{
        Context = 'You operate inside a regulated clinical environment. Always recommend clinician review and never give definitive diagnoses.'
        Tabs    = @(
            @{ Name='Triage';        Prompt='Summarise the chart into a triage acuity (ESI 1-5) with one-line reasoning. Output JSON: {esi, reasoning}.' },
            @{ Name='Shift Handoff'; Prompt='Produce an SBAR shift-handoff summary from the visit notes.' },
            @{ Name='Family Update'; Prompt='Translate the latest clinical update into plain language for a worried family member at a 6th-grade reading level.' }
        )
    }
    'Banking & Finance' = @{
        Context = 'You serve retail-banking customers. Do not make lending decisions. Defer all KYC / AML checks to the human operator.'
        Tabs    = @(
            @{ Name='Customer Assistant'; Prompt='Answer the customer question using only the product catalogue context provided. If unsure, say so.' },
            @{ Name='Pre-Qual Helper';    Prompt='Walk the customer through eligibility questions for a personal loan and produce a JSON readiness score (0-100) with rationale.' },
            @{ Name='Money Insights';     Prompt='Summarise the supplied transaction CSV into 3 categories and surface one cash-flow observation.' }
        )
    }
    'Retail' = @{
        Context = 'You are an in-store associate copilot. Be concise; the user is on a tablet in front of a customer.'
        Tabs    = @(
            @{ Name='Product Lookup'; Prompt='Given a product name or SKU, summarise availability and key specs in 3 bullets.' },
            @{ Name='Returns Triage'; Prompt='Decide whether a return qualifies under the policy. Output JSON: {decision, reason, escalate}.' },
            @{ Name='Upsell Suggest'; Prompt='Suggest two complementary items from the catalogue with a one-line pitch each.' }
        )
    }
    'Public Sector' = @{
        Context = 'You assist a citizen-services caseworker. Avoid giving legal advice; cite the source document when answering.'
        Tabs    = @(
            @{ Name='Case Intake'; Prompt='Extract structured fields from the citizen narrative: name, issue category, urgency (low/med/high), needed documents.' },
            @{ Name='Policy QA';   Prompt='Answer using only the supplied policy excerpt. If the excerpt does not cover it, say so and recommend escalation.' }
        )
    }
    'Education' = @{
        Context = 'You support a classroom teacher. Output age-appropriate material; never share personal information about students.'
        Tabs    = @(
            @{ Name='Lesson Outline'; Prompt='Draft a 30-minute lesson outline for the topic and grade level provided.' },
            @{ Name='Feedback Coach'; Prompt='Read the student draft and produce 3 specific, encouraging suggestions for revision.' }
        )
    }
    'Manufacturing & Energy' = @{
        Context = 'You assist a field technician. Safety first - flag any procedure that requires lock-out / tag-out or PPE.'
        Tabs    = @(
            @{ Name='Maintenance QA';     Prompt='Answer the technician question from the supplied procedure document. Cite the section number.' },
            @{ Name='Incident Summariser';Prompt='Convert the freeform incident report into JSON: {category, severity, immediate_action, follow_up}.' }
        )
    }
    'Construction' = @{
        Context = 'You assist a jobsite project manager. Reference the spec by section when possible.'
        Tabs    = @(
            @{ Name='RFI Triage'; Prompt='Classify the RFI: scope, schedule, cost, or coordination. Suggest the right reviewer role.' },
            @{ Name='Spec QA';    Prompt='Answer questions strictly from the supplied spec text. If absent, say so.' }
        )
    }
    'Sports & Entertainment' = @{
        Context = 'You are a venue-operations copilot. Outputs may be read aloud - keep them concise.'
        Tabs    = @(
            @{ Name='Guest Services'; Prompt='Answer guest questions about the venue (gates, food, accessibility) in 1-2 sentences.' },
            @{ Name='Incident Log';   Prompt='Turn radio-call freeform notes into a structured incident JSON.' }
        )
    }
    'Insurance' = @{
        Context = 'You assist a claims adjuster. Never approve or deny a claim - only summarise and recommend next step.'
        Tabs    = @(
            @{ Name='Claim Summary'; Prompt='Summarise the claim file into JSON: {claimant, loss_type, est_severity, recommended_next_step}.' },
            @{ Name='Photo Notes';   Prompt='Describe damage visible in the image and call out anything that needs an in-person re-inspection.'; Vision=$true }
        )
    }
    'Legal' = @{
        Context = 'You assist a paralegal. Output is for internal drafting only and is not legal advice.'
        Tabs    = @(
            @{ Name='Clause Extractor'; Prompt='Extract material clauses (parties, term, fees, termination, IP) from the supplied contract excerpt as JSON.' },
            @{ Name='Drafting Coach';   Prompt='Rewrite the supplied paragraph in plain English while preserving legal intent.' }
        )
    }
}
$IndustryChoices = @('Healthcare','Banking & Finance','Retail','Public Sector','Education','Manufacturing & Energy','Construction','Sports & Entertainment','Insurance','Legal','N/A')

# ---------- intro & silicon detect ------------------------------------------
Write-Banner "AI-Dev-Harness . new-demo.ps1"
$silicon = Detect-Silicon
Write-Host "Detected silicon: " -NoNewline
Write-Host $silicon.Name -ForegroundColor Green
Write-Host "  -> Recommended build arch: $($silicon.DefaultArch)"
Write-Host "  -> Recommended Foundry model alias: $($silicon.DefaultModel)"
Write-Host ""

# ---------- gather answers --------------------------------------------------
if (-not $Name)         { $Name         = Read-DefaultedString "Demo name (e.g. HoustonTriage)" "MyAIDemo" }
$identifier             = Sanitize-Identifier $Name
if (-not $Source)       { $Source       = Read-Choice "Local-only or Hybrid (adds cloud router)" @('Local','Hybrid') 'Local' }
if (-not $Architecture) { $Architecture = Read-Choice "Build architecture" @('x64','ARM64','both') $silicon.DefaultArch }

if (-not $Industry) {
    Write-Host ""
    Write-Host "Pick an industry focus (we will seed system prompts + use cases for you):" -ForegroundColor Cyan
    for ($i=0; $i -lt $IndustryChoices.Count; $i++) { Write-Host ("  [{0,2}] {1}" -f ($i+1), $IndustryChoices[$i]) }
    if ($NonInteractive) { $Industry = 'N/A' }
    else {
        while ($true) {
            $resp = Read-Host "Choose 1-$($IndustryChoices.Count) (default: N/A)"
            if ([string]::IsNullOrWhiteSpace($resp)) { $Industry = 'N/A'; break }
            if ($resp -as [int] -and [int]$resp -ge 1 -and [int]$resp -le $IndustryChoices.Count) { $Industry = $IndustryChoices[[int]$resp - 1]; break }
            $named = $IndustryChoices | Where-Object { $_ -ieq $resp } | Select-Object -First 1
            if ($named) { $Industry = $named; break }
            Write-Host "Pick a number 1-$($IndustryChoices.Count), the industry name, or N/A." -ForegroundColor Yellow
        }
    }
}

if (-not $Modality)   { $Modality   = Read-Choice "Modality (text-only, vision+text, or both)" @('text','vision','both') 'text' }
if (-not $ModelAlias) { $ModelAlias = Read-DefaultedString "Foundry Local TEXT model alias (or N/A for detected default)" $silicon.DefaultModel }
if ($ModelAlias -ieq 'N/A') { $ModelAlias = $silicon.DefaultModel }
if (($Modality -eq 'vision' -or $Modality -eq 'both') -and -not $VisionModelAlias) {
    $VisionModelAlias = Read-DefaultedString "Foundry Local VISION model alias (or N/A for default)" 'phi-3.5-vision'
    if ($VisionModelAlias -ieq 'N/A') { $VisionModelAlias = 'phi-3.5-vision' }
}

if (-not $PSBoundParameters.ContainsKey('MockByDefault')) {
    $mockResp = Read-Choice "Start with Mock mode ON? (safe for live demos)" @('yes','no') 'no'
    $MockByDefault = ($mockResp -eq 'yes')
}

if (-not $UseCases -or $UseCases.Count -eq 0) {
    Write-Host ""
    Write-Host "Identified use cases - one per line (e.g. 'Triage incoming patients')." -ForegroundColor Cyan
    Write-Host "Type 'N/A' (just N/A on its own line) to let the generator pick for you." -ForegroundColor DarkGray
    Write-Host "Press Enter on a blank line when done." -ForegroundColor DarkGray
    $ucCollected = New-Object System.Collections.Generic.List[string]
    if (-not $NonInteractive) {
        while ($true) {
            $line = Read-Host (" use case " + ($ucCollected.Count + 1))
            if ([string]::IsNullOrWhiteSpace($line)) { break }
            if ($line -ieq 'N/A') { $ucCollected.Clear(); $ucCollected.Add('N/A'); break }
            $ucCollected.Add($line)
        }
    }
    if ($ucCollected.Count -eq 0) { $ucCollected.Add('N/A') }
    $UseCases = $ucCollected.ToArray()
}

if (-not $Tabs -or $Tabs.Count -eq 0) {
    $tabsBuilt = New-Object System.Collections.Generic.List[string]
    $industryEntry = if ($Industry -ne 'N/A' -and $IndustryCatalog.ContainsKey($Industry)) { $IndustryCatalog[$Industry] } else { $null }
    $industryContext = if ($industryEntry) { $industryEntry.Context } else { '' }
    if ($UseCases.Count -eq 1 -and $UseCases[0] -ieq 'N/A') {
        if ($industryEntry) {
            foreach ($t in $industryEntry.Tabs) {
                $isVision = $t.ContainsKey('Vision') -and $t.Vision
                if ($Modality -eq 'text' -and $isVision) { continue }
                $prompt = if ($industryContext) { "$industryContext`r`n`r`n$($t.Prompt)" } else { $t.Prompt }
                $tabType = if ($isVision) { 'vision' } else { 'text' }
                $tabsBuilt.Add("$($t.Name)|$tabType|$prompt")
            }
        } else {
            $tabsBuilt.Add("Chat|text|You are a concise, helpful on-device assistant for $Name. Answer in plain prose.")
            if ($Modality -ne 'text') { $tabsBuilt.Add("Image Notes|vision|Describe the image the user attached in 3 bullet points.") }
        }
    } else {
        foreach ($uc in $UseCases) {
            $ucName = ($uc -split ' ' | Select-Object -First 4) -join ' '
            $prompt = "You are an on-device assistant for $Name in the $Industry domain. Help the user with: $uc."
            if ($industryContext) { $prompt = "$industryContext`r`n`r`n$prompt" }
            $tabsBuilt.Add("$ucName|text|$prompt")
        }
        if ($Modality -ne 'text') { $tabsBuilt.Add("Image Notes|vision|Describe the image the user attached in the context of $Name. Be concise.") }
    }
    $Tabs = $tabsBuilt.ToArray()
}

if (-not $PSBoundParameters.ContainsKey('InitGit')) {
    $gitResp = Read-Choice "Initialise git repo for the new demo?" @('yes','no') 'yes'
    $InitGit = ($gitResp -eq 'yes')
}
if ($InitGit -and -not $GitHubRepo -and -not $NonInteractive) {
    $repoResp = Read-DefaultedString "GitHub repo to create (owner/name, blank to skip)" ""
    if ($repoResp) { $GitHubRepo = $repoResp }
}

# ---------- paths -----------------------------------------------------------
$repoRoot   = $PSScriptRoot
$srcDir     = Join-Path $repoRoot 'src'
$demosDir   = Join-Path $repoRoot 'demos'
if (-not (Test-Path $demosDir)) { New-Item -ItemType Directory -Path $demosDir | Out-Null }
$sourceHarness = if ($Source -eq 'Local') { 'LocalAIDevHarness' } else { 'HybridAIDevHarness' }
$sourceDir = Join-Path $srcDir $sourceHarness
$targetDir = Join-Path $demosDir $identifier
if (Test-Path $targetDir) { throw "$targetDir already exists. Pick a different name or delete it." }

Write-Banner "Generating $identifier from $sourceHarness ..."

# ---------- copy + rename ---------------------------------------------------
# Use robocopy explicitly for speed and exclusion control. /MIR removed because
# it has been observed to delete files outside the destination in odd repo
# layouts; /E is safer (copies subtrees including empties).
$null = robocopy $sourceDir $targetDir /E /XD bin obj AppPackages BundleArtifacts .vs /NFL /NDL /NJH /NJS /NC /NS /NP
# robocopy returns non-zero for "files copied" - that is success. Anything > 7 is a real error.
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
$global:LASTEXITCODE = 0

Get-ChildItem $targetDir -Recurse -File | Where-Object { $_.Name -like "$sourceHarness*" } | ForEach-Object {
    Rename-Item $_.FullName ($_.Name -replace [regex]::Escape($sourceHarness), $identifier)
}
Get-ChildItem $targetDir -Recurse -Directory | Where-Object { $_.Name -like "$sourceHarness*" } | Sort-Object FullName -Descending | ForEach-Object {
    Rename-Item $_.FullName ($_.Name -replace [regex]::Escape($sourceHarness), $identifier)
}

$exts = @('.cs','.xaml','.csproj','.sln','.json','.appxmanifest','.md')
Get-ChildItem $targetDir -Recurse -File | Where-Object { $exts -contains $_.Extension.ToLower() } | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace [regex]::Escape($sourceHarness), $identifier
    Set-Content -Path $_.FullName -Value $content -NoNewline
}

# Fix Shared paths: demos\<Name>\<Name>.csproj references ..\..\..\src\Shared\Shared.csproj
$csproj = Join-Path $targetDir "$identifier\$identifier.csproj"
if (Test-Path $csproj) {
    (Get-Content $csproj -Raw) -replace '\.\.\\\.\.\\Shared\\Shared\.csproj', '..\..\..\src\Shared\Shared.csproj' | Set-Content $csproj -NoNewline
}
$sln = Join-Path $targetDir "$identifier.sln"
if (Test-Path $sln) {
    (Get-Content $sln -Raw) -replace '\.\.\\Shared\\Shared\.csproj', '..\..\src\Shared\Shared.csproj' | Set-Content $sln -NoNewline
}

# ---------- mock-by-default + model alias --------------------------------
$appHost = Join-Path $targetDir "$identifier\AppHost.cs"
if ((Test-Path $appHost) -and $MockByDefault) {
    $ah = Get-Content $appHost -Raw
    if ($ah -notmatch 'Settings\.UseMock\s*=\s*true') {
        if ($ah -match 'static AppHost\(\)\s*\{') {
            $ah = [regex]::Replace($ah, '(static AppHost\(\)\s*\{)', "`$1`r`n        Settings.UseMock = true; // generator: mock-on by default", 1)
        } else {
            $pattern = '(public static AppSettings Settings \{ get; \} = new\(\);)'
            $replacement = "`$1`r`n`r`n    static AppHost() { Settings.UseMock = true; /* generator: mock-on by default */ }"
            $ah = $ah -replace $pattern, $replacement
        }
        Set-Content $appHost -Value $ah -NoNewline
    }
}
if ((Test-Path $appHost) -and $ModelAlias) {
    $ah = Get-Content $appHost -Raw
    $ah = $ah -replace 'ModelCatalog\.DefaultSmallFor\(SiliconDetector\.Current\)', "`"$ModelAlias`" /* generator: per-demo default */"
    Set-Content $appHost -Value $ah -NoNewline
}

# ---------- prune inherited harness pages -----------------------------------
$pagesDir = Join-Path $targetDir "$identifier\Pages"
$keep = @('AboutPage','SettingsPage')
$plannedBaseNames = @()
foreach ($entry in $Tabs) {
    $tName = ($entry -split '\|',3)[0].Trim()
    $plannedBaseNames += (Sanitize-Identifier $tName) + 'Page'
}
Get-ChildItem $pagesDir -File -ErrorAction SilentlyContinue | ForEach-Object {
    $base = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
    # Strip trailing .xaml from base if file is *.xaml.cs
    $baseNoXaml = $base -replace '\.xaml$',''
    $isKept = $keep -contains $baseNoXaml
    $isPlanned = $plannedBaseNames -contains $baseNoXaml
    if (-not $isKept -and -not $isPlanned) { Remove-Item $_.FullName -Force }
}

# ---------- generate tab pages ----------------------------------------------
$mainXaml = Join-Path $targetDir "$identifier\MainWindow.xaml"
$mainCs   = Join-Path $targetDir "$identifier\MainWindow.xaml.cs"
$navItems = New-Object System.Collections.Generic.List[string]
$switchArms = New-Object System.Collections.Generic.List[string]

foreach ($entry in $Tabs) {
    $parts = $entry -split '\|', 3
    $tabName = ($parts[0]).Trim()
    if ($parts.Count -ge 3) { $tabType = ($parts[1]).Trim().ToLower(); $prompt = ($parts[2]).Trim() }
    else { $tabType = 'text'; $prompt = if ($parts.Count -gt 1) { $parts[1].Trim() } else { 'You are a helpful assistant.' } }
    if ($tabType -notin @('text','vision')) { $tabType = 'text' }
    $tabId   = Sanitize-Identifier $tabName
    $pageCls = "${tabId}Page"
    $tag     = $tabId.ToLower()
    $escPrompt = $prompt -replace '"','""'

    if ($tabType -eq 'vision') {
        $pageXaml = @"
<?xml version="1.0" encoding="utf-8"?>
<Page x:Class="$identifier.Pages.$pageCls"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Padding="16" RowSpacing="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>
        <TextBlock Grid.Row="0" Text="$tabName" FontSize="22" FontWeight="SemiBold" />
        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="8">
            <Button x:Name="PickButton" Content="Pick image..." Click="OnPick" />
            <TextBlock x:Name="PathText" VerticalAlignment="Center" Opacity="0.7" />
        </StackPanel>
        <Image Grid.Row="2" x:Name="Preview" Stretch="Uniform" MaxHeight="380" HorizontalAlignment="Left" />
        <TextBox Grid.Row="3" x:Name="Answer" IsReadOnly="True" TextWrapping="Wrap" MinHeight="120"
                 PlaceholderText="Vision-model output will stream here." />
    </Grid>
</Page>
"@
        $pageCs = @"
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;

namespace $identifier.Pages;

public sealed partial class $pageCls : Page
{
    private const string VisionModelAlias = "$VisionModelAlias";
    private const string SystemPrompt = @"$escPrompt";

    public $pageCls() { InitializeComponent(); }

    private async void OnPick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        PathText.Text = file.Path;
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        var bmp = new BitmapImage();
        await bmp.SetSourceAsync(stream);
        Preview.Source = bmp;
        Answer.Text = "(vision-model integration stub) - wire FoundryLocalChatClient vision overload " +
                      "or your preferred OpenVINO GenAI path. System prompt and image are ready.";
    }
}
"@
    } else {
        $pageXaml = @"
<?xml version="1.0" encoding="utf-8"?>
<Page x:Class="$identifier.Pages.$pageCls"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:controls="using:LocalAiDemos.Shared.Controls">
    <Grid Padding="16" RowSpacing="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>
        <TextBlock Grid.Row="0" Text="$tabName" FontSize="22" FontWeight="SemiBold" />
        <controls:StreamingChatView Grid.Row="1" x:Name="Chat" />
    </Grid>
</Page>
"@
        $pageCs = @"
using Microsoft.UI.Xaml.Controls;

namespace $identifier.Pages;

public sealed partial class $pageCls : Page
{
    public $pageCls()
    {
        InitializeComponent();
        Chat.Client = AppHost.Chat;
        Chat.Settings = AppHost.Settings;
        Chat.SystemPrompt = @"$escPrompt";
    }
}
"@
    }

    Set-Content (Join-Path $pagesDir "$pageCls.xaml") -Value $pageXaml -NoNewline
    Set-Content (Join-Path $pagesDir "$pageCls.xaml.cs") -Value $pageCs -NoNewline
    $navItems.Add("            <NavigationViewItem Content=`"$tabName`" Tag=`"$tag`" />")
    $switchArms.Add("            case `"$tag`": NavFrame.Navigate(typeof(Pages.$pageCls)); break;")
}

# ---------- patch MainWindow nav + selection handler ------------------------
if ((Test-Path $mainXaml) -and (Test-Path $mainCs)) {
    $mwXaml = Get-Content $mainXaml -Raw
    if ($mwXaml -match '<NavigationView\.MenuItems>') {
        $newMenu = "<NavigationView.MenuItems>`r`n" + ($navItems -join "`r`n") + "`r`n        </NavigationView.MenuItems>"
        $mwXaml = [regex]::Replace($mwXaml, '<NavigationView\.MenuItems>.*?</NavigationView\.MenuItems>', $newMenu, [System.Text.RegularExpressions.RegexOptions]::Singleline)
        Set-Content $mainXaml -Value $mwXaml -NoNewline
    }

    # Replace the whole MainWindow.xaml.cs - simpler and more robust than
    # regex-patching the inherited harness file (whose switch body contains
    # string-interpolation braces that confuse a brace-balanced regex).
    $arms = ($switchArms -join "`r`n")
    $newMain = @"
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using $identifier.Pages;

namespace $identifier;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
        => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    private void TitleBar_BackRequested(TitleBar sender, object args)
        => NavFrame.GoBack();

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected) { NavFrame.Navigate(typeof(SettingsPage)); return; }
        if (args.SelectedItem is not NavigationViewItem item) return;
        switch (item.Tag)
        {
$arms
            case "about": NavFrame.Navigate(typeof(AboutPage)); break;
            default: NavFrame.Navigate(typeof(AboutPage)); break;
        }
    }
}
"@
    Set-Content $mainCs -Value $newMain -NoNewline
}

# ---------- patch RuntimeIdentifiers ----------------------------------------
if (Test-Path $csproj) {
    $rids = switch ($Architecture) {
        'x64'   { 'win-x64' }
        'ARM64' { 'win-arm64' }
        'both'  { 'win-x64;win-arm64' }
    }
    $cs = Get-Content $csproj -Raw
    $cs = $cs -replace '<RuntimeIdentifiers>.*?</RuntimeIdentifiers>', "<RuntimeIdentifiers>$rids</RuntimeIdentifiers>"
    if ($cs -notmatch '<RuntimeIdentifiers>') {
        $cs = [regex]::Replace($cs, '</PropertyGroup>', "  <RuntimeIdentifiers>$rids</RuntimeIdentifiers>`r`n  </PropertyGroup>", 1)
    }
    Set-Content $csproj -Value $cs -NoNewline
}

# ---------- per-demo README -------------------------------------------------
$readmePath = Join-Path $targetDir 'README.md'
$tabsList = ($Tabs | ForEach-Object {
    $p = $_ -split '\|', 3
    $tName = $p[0].Trim()
    $tType = if ($p.Count -ge 3) { $p[1].Trim() } else { 'text' }
    $tPrompt = if ($p.Count -ge 3) { $p[2].Trim() } elseif ($p.Count -ge 2) { $p[1].Trim() } else { '' }
    "- **$tName** ($tType) - $tPrompt"
}) -join "`r`n"
$visionLine = if ($Modality -ne 'text') { "`r`n- Vision model alias: ``$VisionModelAlias``" } else { '' }
$generatedReadme = @"
# $Name

Generated from **npuneil/AI-Dev-Harness** ($Source harness) on $(Get-Date -Format 'yyyy-MM-dd').

- Industry focus: **$Industry**
- Modality: **$Modality**
- Silicon target: **$Architecture** (detected: $($silicon.Name))
- Foundry Local text model alias: ``$ModelAlias``$visionLine
- Mock mode default: **$MockByDefault** - flip in Settings tab or via ``AppHost.Settings.UseMock``

## Tabs

$tabsList

## Run

``````powershell
dotnet build $identifier.sln -c Debug -p:Platform=x64
dotnet run --project $identifier\$identifier.csproj -c Debug -p:Platform=x64
``````

## On-Device AI Prototypes & Sample Code

This repository contains prototypes, demos, and sample code that illustrate
patterns for building on-device + hybrid AI solutions. The content is provided
for educational and demonstration purposes only.

This repository does not contain Microsoft products and is not a supported or
production-ready offering. Generated from the
[AI-Dev-Harness](https://github.com/npuneil/AI-Dev-Harness) skunkworks pack.

- All code and demos are experimental prototypes or samples.
- AI / ML outputs may be non-deterministic, incomplete, or incorrect.
- Any cost figures, savings percentages, or token-spend projections are illustrative only.
- Nothing produced by this application constitutes professional advice in any
  regulated discipline (medical, financial, legal, educational, engineering,
  safety, or other).
"@
Set-Content $readmePath -Value $generatedReadme -NoNewline

# ---------- git + GitHub ----------------------------------------------------
if ($InitGit) {
    Write-Banner "Initialising git repo ..."
    Push-Location $targetDir
    try {
        git init -b main 2>&1 | Out-Null
        @'
bin/
obj/
*.user
AppPackages/
BundleArtifacts/
.vs/
'@ | Set-Content '.gitignore' -NoNewline
        git add -A 2>&1 | Out-Null
        git -c user.email='generator@ai-dev-harness' -c user.name='AI-Dev-Harness' commit -m "Generated $identifier from AI-Dev-Harness ($Source)" 2>&1 | Out-Null

        if ($GitHubRepo) {
            $ghAvailable = $null -ne (Get-Command gh -ErrorAction SilentlyContinue)
            if ($ghAvailable) {
                Write-Host "Creating GitHub repo $GitHubRepo ..."
                gh repo create $GitHubRepo --public --source=. --push 2>&1 | Write-Host
            } else {
                Write-Host "gh CLI not found. Skipping GitHub repo creation; run manually:" -ForegroundColor Yellow
                Write-Host "  gh repo create $GitHubRepo --public --source=. --push" -ForegroundColor Yellow
            }
        }
    } finally { Pop-Location }
}

# ---------- summary ---------------------------------------------------------
Write-Banner "Done."
Write-Host "  Location:     $targetDir"
Write-Host "  Solution:     $sln"
Write-Host "  Source:       $Source harness"
Write-Host "  Industry:     $Industry"
Write-Host "  Modality:     $Modality"
Write-Host "  Architecture: $Architecture"
Write-Host "  Text model:   $ModelAlias"
if ($Modality -ne 'text') { Write-Host "  Vision model: $VisionModelAlias" }
Write-Host "  Mock default: $MockByDefault"
Write-Host "  Tabs:         $($Tabs.Count)"
Write-Host ""
Write-Host "Build it:" -ForegroundColor Cyan
Write-Host "  dotnet build `"$sln`" -c Debug -p:Platform=x64"
Write-Host ""
Write-Host "Open in VS:" -ForegroundColor Cyan
Write-Host "  start `"$sln`""
