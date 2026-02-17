using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.VisualElements;

namespace SubashaVentures.Components.Shared.QRCode;

public partial class QRCodeComponent : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private ILogger<QRCodeComponent> Logger { get; set; } = default!;

    [Parameter] public string Data { get; set; } = string.Empty;
    [Parameter] public int Size { get; set; } = 256;
    [Parameter] public string DarkColor { get; set; } = "#000000";
    [Parameter] public string LightColor { get; set; } = "#FFFFFF";
    [Parameter] public bool UseGradient { get; set; } = false;
    [Parameter] public string? GradientDirection { get; set; }
    [Parameter] public string? GradientColor1 { get; set; }
    [Parameter] public string? GradientColor2 { get; set; }
    [Parameter] public string? LogoUrl { get; set; }
    [Parameter] public bool AddLogoBorder { get; set; } = false;
    [Parameter] public string? LogoBorderColor { get; set; }
    [Parameter] public int LogoBorderWidth { get; set; } = 2;
    [Parameter] public int LogoBorderRadius { get; set; } = 5;
    [Parameter] public double LogoSizeRatio { get; set; } = 0.25;
    [Parameter] public bool ShowDownloadButton { get; set; } = false;
    [Parameter] public string CssClass { get; set; } = string.Empty;

    private string ContainerId = $"qr-container-{Guid.NewGuid():N}";
    private bool IsLoading = true;
    private string ErrorMessage = string.Empty;
    private string ErrorIconSvg = string.Empty;
    private string DownloadIconSvg = string.Empty;
    private IJSObjectReference? qrModule;

    // FIX: hold generated SVG here; inject into DOM on the NEXT render pass
    // (after StateHasChanged() has added the container div to the DOM)
    private string? _pendingSvgContent;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            ErrorIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' d='M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z'/>",
                24, 24, "0 0 24 24", "fill='none'"
            );

            DownloadIconSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4'/>",
                24, 24, "0 0 24 24", "fill='none'"
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load SVG icons");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await GenerateQRCode();
            return;
        }

        // FIX: On subsequent renders the container div is guaranteed to be in the DOM.
        // Inject the SVG now if one is waiting.
        if (_pendingSvgContent != null && !IsLoading)
        {
            var svgToInject = _pendingSvgContent;
            _pendingSvgContent = null;

            try
            {
                await qrModule!.InvokeVoidAsync("setSvgContent", ContainerId, svgToInject);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to inject SVG into container {Id}", ContainerId);
            }
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!IsLoading && !string.IsNullOrEmpty(Data))
        {
            await GenerateQRCode();
        }
    }

    private async Task GenerateQRCode()
    {
        if (string.IsNullOrWhiteSpace(Data))
        {
            ErrorMessage = "No data provided for QR code generation";
            IsLoading = false;
            StateHasChanged();
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            _pendingSvgContent = null;
            StateHasChanged();

            qrModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/qrCodeModule.js");

            string svgContent;

            if (UseGradient || !string.IsNullOrEmpty(LogoUrl))
            {
                var options = new
                {
                    useGradient = UseGradient,
                    gradientDirection = GradientDirection ?? "linear-x",
                    gradientColor1 = GradientColor1 ?? DarkColor,
                    gradientColor2 = GradientColor2 ?? DarkColor,
                    logoUrl = LogoUrl,
                    addLogoBorder = AddLogoBorder,
                    logoBorderColor = LogoBorderColor ?? "#FFFFFF",
                    logoBorderWidth = LogoBorderWidth,
                    logoBorderRadius = LogoBorderRadius,
                    logoSizeRatio = LogoSizeRatio,
                    qrMargin = 0,
                    errorLevel = "L"   // low error correction → max data capacity
                };

                svgContent = await qrModule.InvokeAsync<string>(
                    "generateEnhancedQrCode",
                    Data, Size, DarkColor, LightColor, options);
            }
            else
            {
                svgContent = await qrModule.InvokeAsync<string>(
                    "generateQrCode",
                    Data, Size, DarkColor, LightColor);
            }

            // FIX: Do NOT call setSvgContent here — the container div doesn't exist yet
            // because IsLoading is still true. Store it; OnAfterRenderAsync will inject it
            // after the next render cycle exposes the container.
            _pendingSvgContent = svgContent;
            IsLoading = false;
            StateHasChanged(); // triggers re-render → container div appears → OnAfterRenderAsync fires again
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate QR code");
            ErrorMessage = "Failed to generate QR code. Please try again.";
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task DownloadQRCode()
    {
        try
        {
            var svgElement = await JSRuntime.InvokeAsync<string>(
                "eval",
                $"document.getElementById('{ContainerId}').innerHTML");

            var blob = $"data:image/svg+xml;charset=utf-8,{Uri.EscapeDataString(svgElement)}";
            var fileName = $"qrcode-{DateTime.Now:yyyyMMdd-HHmmss}.svg";

            await JSRuntime.InvokeVoidAsync("eval", $@"
                const a = document.createElement('a');
                a.href = '{blob}';
                a.download = '{fileName}';
                a.click();
            ");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to download QR code");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (qrModule != null)
        {
            try
            {
                await qrModule.DisposeAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error disposing QR module");
            }
        }
    }
}
