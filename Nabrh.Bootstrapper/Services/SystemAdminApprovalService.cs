using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Nabrh.Bootstrapper.Services;

public sealed record SystemAdminApprovalResult(
    bool IsApproved,
    string Message,
    string? ApprovalToken = null,
    DateTimeOffset? ExpiresAtUtc = null,
    string? ApprovalId = null)
{
    public bool IsCurrent =>
        IsApproved &&
        !string.IsNullOrWhiteSpace(ApprovalToken) &&
        ExpiresAtUtc is { } expiry &&
        expiry > DateTimeOffset.UtcNow.AddSeconds(30);
}

internal sealed record SystemAdminApprovalRequest(
    string Product,
    string Version,
    string Action,
    string ApprovalCode,
    string DeviceId,
    string MachineName,
    string UserDomain,
    string UserName);

internal sealed record SystemAdminApprovalResponse(
    bool Approved,
    string? Message,
    string? ApprovalToken,
    DateTimeOffset? ExpiresAtUtc,
    string? ApprovalId);

/// <summary>
/// Fail-closed gate between the installer and the organisation's approval server. The server URL
/// is embedded as a hidden Burn variable at build time; the user cannot choose a different host
/// from the wizard. The returned token is short-lived and never written to the installer log.
/// </summary>
public static class SystemAdminApprovalService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<SystemAdminApprovalResult> RequestAsync(
        string? serverUrl,
        string? approvalCode,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            return new(false, "سيرفر موافقات مشرفي النظام غير مضبوط. تم منع التثبيت.");
        }

        if (endpoint.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
        {
            return new(false, "لم يُحدَّد سيرفر الموافقات الفعلي في إعدادات بناء المثبّت.");
        }

        if (string.IsNullOrWhiteSpace(approvalCode))
        {
            return new(false, "أدخل رمز التصريح الصادر عن مشرف النظام.");
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var request = new SystemAdminApprovalRequest(
                Product: "Nabrh",
                Version: typeof(SystemAdminApprovalService).Assembly.GetName().Version?.ToString() ?? "0.6.0",
                Action: "install",
                ApprovalCode: approvalCode.Trim(),
                DeviceId: CreateDeviceId(),
                MachineName: Environment.MachineName,
                UserDomain: Environment.UserDomainName,
                UserName: Environment.UserName);

            using var response = await client.PostAsJsonAsync(endpoint, request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(false, response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                        "رفض سيرفر مشرفي النظام رمز التصريح.",
                    _ => $"تعذّر الحصول على الموافقة (HTTP {(int)response.StatusCode}).",
                });
            }

            var payload = await response.Content.ReadFromJsonAsync<SystemAdminApprovalResponse>(JsonOptions, cancellationToken);
            if (payload is null || !payload.Approved)
                return new(false, payload?.Message ?? "لم يمنح مشرف النظام تصريح التثبيت.");

            var result = new SystemAdminApprovalResult(
                true,
                payload.Message ?? "تم اعتماد التثبيت من مشرف النظام.",
                payload.ApprovalToken,
                payload.ExpiresAtUtc,
                payload.ApprovalId);

            if (!result.IsCurrent)
                return new(false, "استجابة الموافقة ناقصة أو منتهية الصلاحية. اطلب تصريحاً جديداً.");

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "انتهت مهلة الاتصال بسيرفر الموافقات. تم منع التثبيت.");
        }
        catch (HttpRequestException)
        {
            return new(false, "تعذّر الاتصال الآمن بسيرفر الموافقات. تم منع التثبيت.");
        }
        catch (Exception ex)
        {
            InstallerLogService.LogError("System administrator approval request failed.", ex, "Approval");
            return new(false, "حدث خطأ أثناء التحقق من تصريح مشرف النظام.");
        }
    }

    private static string CreateDeviceId()
    {
        string machineGuid = "unknown";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            machineGuid = key?.GetValue("MachineGuid") as string ?? machineGuid;
        }
        catch
        {
            // The stable machine name remains in the input if registry access is restricted.
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"Nabrh|{machineGuid}|{Environment.MachineName}"));
        return Convert.ToHexString(digest);
    }
}
