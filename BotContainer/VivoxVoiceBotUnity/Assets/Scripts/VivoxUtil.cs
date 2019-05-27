using System;
using VivoxUnity;

public static class VivoxUtil {
    public static AccountId GetAccountId(string username) {
        return new AccountId(Config.TokenIssuer, username, Config.Domain);
    }

    public static void Login(this Client client, ILoginSession loginSession, AsyncCallback callback) {
        var loginToken = loginSession.GetLoginToken(Config.TokenKey, Config.TokenExpiration);
        var callbackResult = new AsyncResult<ILoginSession>(callback);
        
        loginSession.BeginLogin(Config.Server, loginToken, ar => {
            try {
                loginSession.EndLogin(ar);
                callbackResult.SetComplete(loginSession);
            } catch (Exception ex) {
                Log.wtf(ex, "Login vivox user failed");
                client.Uninitialize();
                callbackResult.SetComplete(ex);
            }
        });
    }
}
