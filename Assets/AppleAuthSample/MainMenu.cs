using System;
using System.Text;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    private const string AppleUserIdKey = "AppleUserId";
    
    private IAppleAuthManager _appleAuthManager;

    private event Action<string> onErrorMessage;
    public event Action<string> onLogin;
    private event Action onCompleted;

    // Nhận token sau khi đăng nhập
    public void OnLogin(Action<string> onLogin){
        this.onLogin = onLogin;
    }

    // Nhận thông báo lỗi
    public void OnErrorMessage(Action<string> onErrorMessage){
        this.onErrorMessage = onErrorMessage;
    }
    // Nhận thông báo khi hoàn thành
    public void OnCompleted(Action onCompleted){
        this.onCompleted = onCompleted;
    }

    private void Start()
    {
        // If the current platform is supported
        if (AppleAuthManager.IsCurrentPlatformSupported)
        {
            // Creates a default JSON deserializer, to transform JSON Native responses to C# instances
            var deserializer = new PayloadDeserializer();
            // Creates an Apple Authentication manager with the deserializer
            this._appleAuthManager = new AppleAuthManager(deserializer);    
        }

        this.InitializeLoginMenu();
    }

    private void Update()
    {
        // Updates the AppleAuthManager instance to execute
        // pending callbacks inside Unity's execution loop
        if (this._appleAuthManager != null)
        {
            this._appleAuthManager.Update();
        }
    }

    public void SignInWithAppleButtonPressed()
    {
        this.SignInWithApple();
    }

    public void SignOutButtonPressed()
    {
        this.SignOut();
    }

    private void InitializeLoginMenu()
    {
        // Check if the current platform supports Sign In With Apple
        if (this._appleAuthManager == null)
        {
            return;
        }
        
        // If at any point we receive a credentials revoked notification, we delete the stored User ID, and go back to login
        this._appleAuthManager.SetCredentialsRevokedCallback(result =>
        {
            Debug.Log("Received revoked callback " + result);
            PlayerPrefs.DeleteKey(AppleUserIdKey);
        });

        // If we have an Apple User Id available, get the credential status for it
        if (PlayerPrefs.HasKey(AppleUserIdKey))
        {
            var storedAppleUserId = PlayerPrefs.GetString(AppleUserIdKey);
            // this.SetupLoginMenuForCheckingCredentials();
            this.CheckCredentialStatusForUserId(storedAppleUserId);
        }
        // If we do not have an stored Apple User Id, attempt a quick login
        else
        {
            this.AttemptQuickLogin();
        }
    }

    private void CheckCredentialStatusForUserId(string appleUserId)
    {
        // If there is an apple ID available, we should check the credential state
        this._appleAuthManager.GetCredentialState(
            appleUserId,
            state =>
            {
                switch (state)
                {
                    // If it's authorized, login with that user id
                    case CredentialState.Authorized:
                        return;
                    
                    // If it was revoked, or not found, we need a new sign in with apple attempt
                    // Discard previous apple user id
                    case CredentialState.Revoked:
                    case CredentialState.NotFound:
                        PlayerPrefs.DeleteKey(AppleUserIdKey);
                        return;
                }
            },
            error =>
            {
                var authorizationErrorCode = error.GetAuthorizationErrorCode();
                string errorMessage = "Error while trying to get credential state " + authorizationErrorCode.ToString() + " " + error.ToString();
                Debug.LogWarning(errorMessage);
            });
    }
    
    private void AttemptQuickLogin()
    {
        var quickLoginArgs = new AppleAuthQuickLoginArgs();
        
        // Quick login should succeed if the credential was authorized before and not revoked
        this._appleAuthManager.QuickLogin(
            quickLoginArgs,
            credential =>
            {
                // If it's an Apple credential, save the user ID, for later logins
                var appleIdCredential = credential as IAppleIDCredential;
                if (appleIdCredential != null)
                {
                    PlayerPrefs.SetString(AppleUserIdKey, credential.User);
                    string token = Encoding.UTF8.GetString(appleIdCredential.IdentityToken, 0, appleIdCredential.IdentityToken.Length);
                    Debug.Log("Quick Login Succeeded " + appleIdCredential.User + " " + token);
                    onLogin?.Invoke(token);
                    onCompleted?.Invoke();
                }
            },
            error =>
            {
                string errorMessage = "Quick Login Failed " + error.GetAuthorizationErrorCode().ToString() + " " + error.ToString();
                // If Quick Login fails, we should show the normal sign in with apple menu, to allow for a normal Sign In with apple
                var authorizationErrorCode = error.GetAuthorizationErrorCode();
                Debug.LogWarning(errorMessage);
                onErrorMessage?.Invoke(errorMessage);
                onCompleted?.Invoke();
            });
    }
    
    private void SignInWithApple()
    {
        var loginArgs = new AppleAuthLoginArgs(LoginOptions.IncludeEmail | LoginOptions.IncludeFullName);
        
        this._appleAuthManager.LoginWithAppleId(
            loginArgs,
            credential =>
            {
                // If a sign in with apple succeeds, we should have obtained the credential with the user id, name, and email, save it
                PlayerPrefs.SetString(AppleUserIdKey, credential.User);
                string token = Encoding.UTF8.GetString((credential as IAppleIDCredential).IdentityToken, 0, (credential as IAppleIDCredential).IdentityToken.Length);
                onLogin?.Invoke(token);
                onCompleted?.Invoke();
            },
            error =>
            {
                string errorMessage = "Sign in with Apple failed " + error.GetAuthorizationErrorCode().ToString() + " " + error.ToString();
                var authorizationErrorCode = error.GetAuthorizationErrorCode();
                Debug.LogWarning(errorMessage);
                onErrorMessage?.Invoke(errorMessage);
                onCompleted?.Invoke();
            });
    }

    private void SignOut()
    {
        // If we sign out, we should delete the stored Apple User Id
        PlayerPrefs.DeleteKey(AppleUserIdKey);
    }
}
