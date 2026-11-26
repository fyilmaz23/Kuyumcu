using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.Identity;
using Android.Gms.Common.Apis;
using Android.Runtime;
using Kuyumcu.Droid.Services;
using Kuyumcu.Services;

namespace Kuyumcu.Droid.Platform;

/// <summary>
/// Google Identity Services "Authorization API" ile Drive erişim belirteci alır.
///
/// - Uygulamada istemci kimliği/sırrı YOKTUR: Google, isteği paket adı + imza sertifikası SHA-1'i ile
///   Cloud projesindeki "Android" türü OAuth istemcisine eşler. Bu istemci masaüstü istemcisiyle AYNI projede
///   olmalıdır; yoksa "drive.file" kapsamı masaüstünün yüklediği dosyaları göstermez (bkz. docs/YAYIN.md).
/// - İzin bir kez verildikten sonra her çağrı ekran açmadan yeni belirteç döndürür.
/// </summary>
public sealed class GoogleDriveAuthorizer : IGoogleDriveAuthorizer
{
    private const int AuthorizationRequestCode = 0x4B44; // "KD"

    private static TaskCompletionSource<AuthorizationResult>? _pendingConsent;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetAccessTokenAsync(bool interactive, bool chooseAccount = false, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var activity = CurrentActivity();

            var builder = AuthorizationRequest.InvokeBuilder()
                .SetRequestedScopes(new List<Scope> { new(DriveLatestBackupClient.DriveFileScope) });
            if (interactive && chooseAccount)
            {
                // İzin daha önce verilmiş olsa bile hesap seçici açılsın (yanlış hesap seçildiyse düzeltilebilsin)
                builder.SetPrompt(AuthorizationRequest.Prompt.SelectAccount!);
            }

            var result = await AuthorizeAsync(activity, builder.Build());

            if (result.HasResolution)
            {
                // Hesap seçimi / izin ekranı gerekiyor
                if (!interactive)
                    throw new GoogleConsentRequiredException();

                var pendingIntent = result.PendingIntent
                                    ?? throw new GoogleAuthorizationFailedException("Google izin ekranı açılamadı.");

                var consent = new TaskCompletionSource<AuthorizationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingConsent = consent;
                await MainThread.InvokeOnMainThreadAsync(() =>
                    activity.StartIntentSenderForResult(pendingIntent.IntentSender, AuthorizationRequestCode, null, 0, 0, 0));

                using (cancellationToken.Register(() => consent.TrySetCanceled()))
                    result = await consent.Task;
            }

            var token = result.AccessToken;
            if (string.IsNullOrEmpty(token))
                throw new GoogleAuthorizationFailedException("Google Drive erişim izni alınamadı.");
            return token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var activity = CurrentActivity();
            var scopes = new List<Scope> { new(DriveLatestBackupClient.DriveFileScope) };
            var client = Identity.GetAuthorizationClient(activity);

            // Sessiz istek: izin varsa hangi hesaba verildiğini ve belirteci döndürür
            var result = await AuthorizeAsync(activity, AuthorizationRequest.InvokeBuilder().SetRequestedScopes(scopes).Build());
            if (result.HasResolution)
                return; // izin zaten yok

            var account = result.ToGoogleSignInAccount()?.Account;
            if (account != null)
            {
                var revoke = RevokeAccessRequest.InvokeBuilder()
                    .SetAccount(account)
                    .SetScopes(scopes)
                    .Build();
                await AwaitGmsVoidTask(client.RevokeAccess(revoke));
            }

            if (!string.IsNullOrEmpty(result.AccessToken))
                await AwaitGmsVoidTask(client.ClearToken(ClearTokenRequest.InvokeBuilder().SetToken(result.AccessToken).Build()));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task InvalidateTokenAsync(string accessToken)
    {
        var activity = CurrentActivity();
        await AwaitGmsVoidTask(Identity.GetAuthorizationClient(activity)
            .ClearToken(ClearTokenRequest.InvokeBuilder().SetToken(accessToken).Build()));
    }

    private static Activity CurrentActivity() =>
        Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
        ?? throw new GoogleAuthorizationFailedException("Uygulama penceresi hazır değil, tekrar deneyin.");

    private static async Task<AuthorizationResult> AuthorizeAsync(Activity activity, AuthorizationRequest request)
    {
        try
        {
            return await AwaitGmsTask<AuthorizationResult>(Identity.GetAuthorizationClient(activity).Authorize(request));
        }
        catch (ApiException ex)
        {
            throw new GoogleAuthorizationFailedException(Describe(ex), ex);
        }
    }

    /// <summary>MainActivity.OnActivityResult'tan çağrılır: izin ekranının sonucunu bekleyen isteğe iletir.</summary>
    internal static void HandleActivityResult(Activity activity, int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != AuthorizationRequestCode)
            return;

        var consent = Interlocked.Exchange(ref _pendingConsent, null);
        if (consent == null)
            return;

        if (data == null)
        {
            consent.TrySetCanceled();
            return;
        }

        // Google, hesap seçildikten sonraki hataları da (ör. SHA-1 eşleşmeyince DEVELOPER_ERROR = 10) RESULT_CANCELED ile
        // döndürür; gerçek durum intent'in içindedir. resultCode'a bakıp "iptal" saymak bu hatayı sessizce yutuyordu.
        try
        {
            var result = Identity.GetAuthorizationClient(activity).GetAuthorizationResultFromIntent(data);
            if (resultCode != Result.Ok && string.IsNullOrEmpty(result.AccessToken))
                consent.TrySetCanceled();
            else
                consent.TrySetResult(result);
        }
        catch (ApiException ex) when (ex.StatusCode is 16 or 12501)
        {
            consent.TrySetCanceled(); // kullanıcı gerçekten vazgeçti
        }
        catch (ApiException ex)
        {
            Android.Util.Log.Warn("KuyumcuDefteri", $"Google yetkilendirme hatası (kod {ex.StatusCode}): {ex.Message}");
            consent.TrySetException(new GoogleAuthorizationFailedException(Describe(ex), ex));
        }
        catch (Exception ex)
        {
            consent.TrySetException(ex);
        }
    }

    private static string Describe(ApiException ex) => ex.StatusCode switch
    {
        // DEVELOPER_ERROR: Cloud projesinde bu paket adı + SHA-1 için Android OAuth istemcisi yok
        10 => "Google girişi bu uygulama sürümü için yapılandırılmamış (OAuth istemcisi / SHA-1). Geliştiriciye bildirin.",
        7 => "İnternet bağlantısı yok.",
        16 or 12501 => "Google girişi iptal edildi.",
        _ => $"Google girişi başarısız (kod {ex.StatusCode}).",
    };

    private static Task<T> AwaitGmsTask<T>(Android.Gms.Tasks.Task task) where T : Java.Lang.Object
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new GmsTaskListener(
            result =>
            {
                var typed = result?.JavaCast<T>();
                if (typed == null)
                    tcs.TrySetException(new GoogleAuthorizationFailedException("Google'dan boş yanıt geldi."));
                else
                    tcs.TrySetResult(typed);
            },
            error => tcs.TrySetException(error));
        task.AddOnSuccessListener(listener);
        task.AddOnFailureListener(listener);
        return tcs.Task;
    }

    private static Task AwaitGmsVoidTask(Android.Gms.Tasks.Task task)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new GmsTaskListener(_ => tcs.TrySetResult(), error => tcs.TrySetException(error));
        task.AddOnSuccessListener(listener);
        task.AddOnFailureListener(listener);
        return tcs.Task;
    }

    private sealed class GmsTaskListener(Action<Java.Lang.Object?> onSuccess, Action<Exception> onFailure)
        : Java.Lang.Object, Android.Gms.Tasks.IOnSuccessListener, Android.Gms.Tasks.IOnFailureListener
    {
        public void OnSuccess(Java.Lang.Object? result) => onSuccess(result);
        public void OnFailure(Java.Lang.Exception e) => onFailure(e);
    }
}
