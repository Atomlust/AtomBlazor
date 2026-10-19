# Rename Primitives components with "Primitive" suffix
# Usage: .\tools\rename-primitives.ps1

$ErrorActionPreference = "Stop"
$rootPath = "D:\Source\AtomBlazor"
$primitivesPath = Join-Path $rootPath "src\AtomBlazor.Primitives\Primitives"

# Define component families to rename (main component only)
$families = @(
    @{ Old = "Accordion"; New = "AccordionPrimitive" },
    @{ Old = "AlertDialog"; New = "AlertDialogPrimitive" },
    @{ Old = "Checkbox"; New = "CheckboxPrimitive" },
    @{ Old = "Collapsible"; New = "CollapsiblePrimitive" },
    @{ Old = "ContextMenu"; New = "ContextMenuPrimitive" },
    @{ Old = "DashboardGrid"; New = "DashboardGridPrimitive" },
    @{ Old = "DataGrid"; New = "DataGridPrimitive" },
    @{ Old = "Dialog"; New = "DialogPrimitive" },
    @{ Old = "DropdownMenu"; New = "DropdownMenuPrimitive" },
    @{ Old = "HoverCard"; New = "HoverCardPrimitive" },
    @{ Old = "Label"; New = "LabelPrimitive" },
    @{ Old = "Menubar"; New = "MenubarPrimitive" },
    @{ Old = "NavigationMenu"; New = "NavigationMenuPrimitive" },
    @{ Old = "Popover"; New = "PopoverPrimitive" },
    @{ Old = "Progress"; New = "ProgressPrimitive" },
    @{ Old = "RadioGroup"; New = "RadioGroupPrimitive" },
    @{ Old = "ScrollArea"; New = "ScrollAreaPrimitive" },
    @{ Old = "Select"; New = "SelectPrimitive" },
    @{ Old = "Separator"; New = "SeparatorPrimitive" },
    @{ Old = "Sheet"; New = "SheetPrimitive" },
    @{ Old = "SignaturePad"; New = "SignaturePadPrimitive" },
    @{ Old = "Slider"; New = "SliderPrimitive" },
    @{ Old = "Sortable"; New = "SortablePrimitive" },
    @{ Old = "SwipeArea"; New = "SwipeAreaPrimitive" },
    @{ Old = "Switch"; New = "SwitchPrimitive" },
    @{ Old = "Table"; New = "TablePrimitive" },
    @{ Old = "Tabs"; New = "TabsPrimitive" },
    @{ Old = "Toggle"; New = "TogglePrimitive" },
    @{ Old = "Tooltip"; New = "TooltipPrimitive" },
    @{ Old = "TreeView"; New = "TreeViewPrimitive" }
)

Write-Host "Starting Primitives renaming process..." -ForegroundColor Cyan
Write-Host ""

foreach ($family in $families) {
    $oldName = $family.Old
    $newName = $family.New
    $familyPath = Join-Path $primitivesPath $oldName

    if (-not (Test-Path $familyPath)) {
        Write-Host "Skipping $oldName - directory not found" -ForegroundColor Yellow
        continue
    }

    Write-Host "Processing $oldName -> $newName..." -ForegroundColor Green

    # Step 1: Rename the main component file
    $oldFile = Join-Path $familyPath "$oldName.razor"
    $newFile = Join-Path $familyPath "$newName.razor"

    if (Test-Path $oldFile) {
        Rename-Item -Path $oldFile -NewName "$newName.razor" -Force
        Write-Host "  Renamed file: $oldName.razor -> $newName.razor" -ForegroundColor Gray

        # Update class name and namespace references in the renamed file
        $content = Get-Content $newFile -Raw -Encoding UTF8
        $content = $content -replace "class $oldName\b", "class $newName"
        $content = $content -replace "partial class $oldName\b", "partial class $newName"
        Set-Content -Path $newFile -Value $content -Encoding UTF8 -NoNewline
        Write-Host "  Updated class names in $newName.razor" -ForegroundColor Gray
    }

    # Step 2: Check for .cs files with same name
    $oldCsFile = Join-Path $familyPath "$oldName.razor.cs"
    $newCsFile = Join-Path $familyPath "$newName.razor.cs"

    if (Test-Path $oldCsFile) {
        Rename-Item -Path $oldCsFile -NewName "$newName.razor.cs" -Force
        Write-Host "  Renamed file: $oldName.razor.cs -> $newName.razor.cs" -ForegroundColor Gray

        $content = Get-Content $newCsFile -Raw -Encoding UTF8
        $content = $content -replace "class $oldName\b", "class $newName"
        $content = $content -replace "partial class $oldName\b", "partial class $newName"
        Set-Content -Path $newCsFile -Value $content -Encoding UTF8 -NoNewline
        Write-Host "  Updated class names in $newName.razor.cs" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Phase 1 complete: Main component files renamed" -ForegroundColor Cyan
Write-Host "Next: Update all references in Components layer and Demos" -ForegroundColor Cyan
