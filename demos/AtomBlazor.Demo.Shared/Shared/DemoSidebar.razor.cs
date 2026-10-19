using AtomBlazor.Demo.Services;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Demo.Shared;

public partial class DemoSidebar : ComponentBase
{

    /// <summary>显示语言快照（MainLayout 级联下发；缺省回落中文）。</summary>
    [CascadingParameter] public DemoCultureState L { get; set; } = DemoCultureState.Default;

    [Inject]
    private CollapsibleStateService StateService { get; set; } = null!;

    private bool _primitivesMenuOpen;
    private bool _componentsMenuOpen;
    private bool _chartsMenuOpen;
    private bool _iconsMenuOpen;
    private bool _guidesMenuOpen;
    private bool _recipesMenuOpen;

    private const string primitivesMenuKey = "sidebar-primitives-menu";
    private const string componentsMenuKey = "sidebar-components-menu";
    private const string chartsMenuKey = "sidebar-charts-menu";
    private const string iconsMenuKey = "sidebar-icons-menu";
    private const string guidesMenuKey = "sidebar-guides-menu";
    private const string recipesMenuKey = "sidebar-recipes-menu";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _primitivesMenuOpen = await StateService.GetStateAsync(primitivesMenuKey, defaultValue: false);
            _componentsMenuOpen = await StateService.GetStateAsync(componentsMenuKey, defaultValue: false);
            _chartsMenuOpen = await StateService.GetStateAsync(chartsMenuKey, defaultValue: false);
            _iconsMenuOpen = await StateService.GetStateAsync(iconsMenuKey, defaultValue: false);
            _guidesMenuOpen = await StateService.GetStateAsync(guidesMenuKey, defaultValue: false);
            _recipesMenuOpen = await StateService.GetStateAsync(recipesMenuKey, defaultValue: false);

            StateHasChanged();
        }
    }

    private void OnPrimitivesMenuOpenChanged(bool isOpen)
    {
        _primitivesMenuOpen = isOpen;
        StateService.SetState(primitivesMenuKey, isOpen);
    }

    private void OnComponentsMenuOpenChanged(bool isOpen)
    {
        _componentsMenuOpen = isOpen;
        StateService.SetState(componentsMenuKey, isOpen);
    }

    private void OnChartsMenuOpenChanged(bool isOpen)
    {
        _chartsMenuOpen = isOpen;
        StateService.SetState(chartsMenuKey, isOpen);
    }

    private void OnIconsMenuOpenChanged(bool isOpen)
    {
        _iconsMenuOpen = isOpen;
        StateService.SetState(iconsMenuKey, isOpen);
    }

    private void OnGuidesMenuOpenChanged(bool isOpen)
    {
        _guidesMenuOpen = isOpen;
        StateService.SetState(guidesMenuKey, isOpen);
    }

    private void OnRecipesMenuOpenChanged(bool isOpen)
    {
        _recipesMenuOpen = isOpen;
        StateService.SetState(recipesMenuKey, isOpen);
    }
}
