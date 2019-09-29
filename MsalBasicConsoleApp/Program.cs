using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MsalBasicConsoleApp
{
    public class Program
    {
        // This app has http://localhost redirect uri registered with the
        // Microsoft identity platform
        private static readonly string _clientId = "1d18b3b0-251b-4714-a02a-9956cec86c2d";
        private static readonly string _b2CClientId = "841e1190-d73a-450c-9d68-f5cf16b78e81"; //"e3b9ad76-9763-4827-b088-80c7a7888f79";
        private const string B2CAuthority = "https://fabrikamb2c.b2clogin.com/tfp/fabrikamb2c.onmicrosoft.com/b2c_1_susi/";
            //"https://msidlabb2c.b2clogin.com/tfp/msidlabb2c.onmicrosoft.com/B2C_1_SISOPolicy/";
        private static readonly IEnumerable<string> _scopes = new[] { "user.read" };
        private static readonly IEnumerable<string> _b2CScopes = new[] { "https://fabrikamb2c.onmicrosoft.com/helloapi/demo.read" };//{ "https://msidlabb2c.onmicrosoft.com/msidlabb2capi/read" };
        private static readonly string _username = "";
        private const string GraphApiEndpoint = "https://graph.microsoft.com/v1.0/me";

        public static readonly string CacheFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location + ".msalcache.json";

        private static int _currentTid = 0;
        private static readonly string[] _tids = new[]  {
            "common",
            "49f548d0-12b7-4169-a390-bb5304d24462",
            "72f988bf-86f1-41af-91ab-2d7cd011db47" };

        static void Main(string[] args)
        {
            Console.WriteLine("This is a basic console app which uses MSAL.NET...");
            var pca = CreatePublicClientApp();
            RunConsoleAppLogicAsync(pca).Wait();
        }

        private static string GetAuthority()
        {
            string tenant = _tids[_currentTid];
            return $"https://login.microsoftonline.com/{tenant}";
        }

        private static IPublicClientApplication CreatePublicClientApp()
        {
            IPublicClientApplication pca = PublicClientApplicationBuilder
                .Create(_clientId)
                .WithAuthority(GetAuthority())
                .WithLogging(Log, LogLevel.Verbose, true)
                .WithRedirectUri("http://localhost")
                .Build();

            pca.UserTokenCache.SetBeforeAccess(notificationArgs =>
            {
                notificationArgs.TokenCache.DeserializeMsalV3(File.Exists(CacheFilePath)
                ? File.ReadAllBytes(CacheFilePath)
                : null);
            });

            pca.UserTokenCache.SetAfterAccess(notificiationArgs =>
            {
               // if the access operatoin resulted in a cache update
               if (notificiationArgs.HasStateChanged)
                {
                   // reflect changes in the persistent store
                   File.WriteAllBytes(CacheFilePath, notificiationArgs.TokenCache.SerializeMsalV3());
                }
            });

            return pca;
        }

        private static IPublicClientApplication CreateB2CPublicClientApplication()
        {
            IPublicClientApplication pca = PublicClientApplicationBuilder
                .Create(_b2CClientId)
                .WithB2CAuthority(B2CAuthority)
                .WithLogging(Log, LogLevel.Verbose, true)
                .WithRedirectUri("http://localhost")
                .Build();

            pca.UserTokenCache.SetBeforeAccess(notificationArgs =>
            {
                notificationArgs.TokenCache.DeserializeMsalV3(File.Exists(CacheFilePath)
                ? File.ReadAllBytes(CacheFilePath)
                : null);
            });

            pca.UserTokenCache.SetAfterAccess(notificiationArgs =>
            {
                // if the access operatoin resulted in a cache update
                if (notificiationArgs.HasStateChanged)
                {
                    // reflect changes in the persistent store
                    File.WriteAllBytes(CacheFilePath, notificiationArgs.TokenCache.SerializeMsalV3());
                }
            });

            return pca;
        }

        private static void Log(LogLevel level, string message, bool containsPii)
        {
            if(!containsPii)
            {
                Console.BackgroundColor = ConsoleColor.DarkBlue;
            }

            switch(level)
            {
                case LogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Verbose:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                default:
                    break;
            }

            Console.WriteLine($"{level} {message}");
            Console.ResetColor();
        }

        private static async Task RunConsoleAppLogicAsync(IPublicClientApplication pca)
        {
            while(true)
            {
                Console.Clear();

                Console.WriteLine("Authority: " + GetAuthority());
                await DisplayAccountsAsync(pca).ConfigureAwait(false);

                // display acquire token options
                Console.WriteLine(@"
                        1. IWA
                        2. Acquire Token with Username and Password (ROPC)
                        3. Acquire Token Interactive
                        4. Acquire Token Interactive with CustomWebUI
                        5. Acquire Token Silently
                        6. Acquire Token with B2C si_su policy
                        7. Acquire Token with Device Code
                        8. Clear Cache
                        0. Exit App
                    Enter your Selection: ");

                int.TryParse(Console.ReadLine(), out var selection);

                Task<AuthenticationResult> authResult = null;

                try
                {
                    switch (selection)
                    {
                        case 1: // IWA
                            authResult = pca.AcquireTokenByIntegratedWindowsAuth(_scopes)
                                .WithAuthority("https://login.microsoftonline.com/organizations")
                                .WithUsername(_username).ExecuteAsync(CancellationToken.None);
                            await DisplayAccessTokenAndCallGraphAsync(pca, authResult).ConfigureAwait(false);
                           
                            break;
                        case 2: // ROPC
                            break;
                        case 3: // Acquire token interactive

                            var options = new SystemWebViewOptions()
                            {
                                BrowserRedirectSuccess = new Uri("https://www.bing.com?q=why+is+42+the+meaning+of+life")
                            };

                            var cts = new CancellationTokenSource();
                            authResult = pca.AcquireTokenInteractive(_scopes)
                                .WithSystemWebViewOptions(options)
                                .ExecuteAsync(cts.Token);

                            await DisplayAccessTokenAndCallGraphAsync(pca, authResult).ConfigureAwait(false);
                           
                            break;
                        case 4: // Acquire token interaction w/CustomWebUI

                            break;
                        case 5: //Acquire token silent
                            IAccount account = pca.GetAccountsAsync().Result.FirstOrDefault();
                            if (account == null)
                            {
                                Log(LogLevel.Error, "Test App Message - no accounts found, AcquireTokenSilentAsync will fail... ", false);
                            }

                            authResult = pca.AcquireTokenSilent(_scopes, account).ExecuteAsync(CancellationToken.None);
                            await DisplayAccessTokenAndCallGraphAsync(pca, authResult).ConfigureAwait(false);

                            break;
                        case 6: // B2C
                            var b2cPca = CreateB2CPublicClientApplication();

                            options = new SystemWebViewOptions()
                            {
                                BrowserRedirectSuccess = new Uri("https://www.bing.com?q=why+is+42+the+meaning+of+life")
                            };

                            cts = new CancellationTokenSource();
                            authResult = b2cPca.AcquireTokenInteractive(_b2CScopes)
                                //.WithSystemWebViewOptions(options)
                                .ExecuteAsync(cts.Token);

                            await DisplayAccessTokenAndCallGraphAsync(b2cPca, authResult).ConfigureAwait(false);
                            
                            break;
                        case 7: // Device code
                            authResult = pca.AcquireTokenWithDeviceCode(
                           _scopes,
                           deviceCodeResult =>
                           {
                               Console.WriteLine(deviceCodeResult.Message);
                               return Task.FromResult(0);
                           }).ExecuteAsync(CancellationToken.None);
                            await DisplayAccessTokenAndCallGraphAsync(pca, authResult).ConfigureAwait(false);

                            break;
                        case 8: // Clear cache
                            var accounts = await pca.GetAccountsAsync().ConfigureAwait(false);
                            foreach (var acc in accounts)
                            {
                                await pca.RemoveAsync(acc).ConfigureAwait(false);
                            }

                            break;
                        case 0: // Exit app
                            return;
                        default:
                            break;
                    }
                }
                catch(Exception ex)
                {
                    Log(LogLevel.Error, ex.Message, false);
                    Log(LogLevel.Error, ex.StackTrace, false);
                }

                Console.WriteLine("\n\nHit 'ENTER' to continue...");
                Console.ReadLine();
            }
        }

        private static async Task DisplayAccessTokenAndCallGraphAsync(IPublicClientApplication pca, Task<AuthenticationResult> authResult)
        {
            await authResult.ConfigureAwait(false);

            Console.BackgroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine("Token is:\n {0}", authResult.Result.AccessToken);
            Console.ResetColor();

            Console.BackgroundColor = ConsoleColor.DarkGreen;
            var callGraph = CallGraphWithTokenAsync(authResult.Result.AccessToken);
            callGraph.Wait();
            Console.WriteLine("Result from calling the /ME endpoint of the graph:\n" + callGraph.Result);
            Console.ResetColor();
        }

        private static async Task<string> CallGraphWithTokenAsync(string token)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, GraphApiEndpoint);
                // Add the token in the Authorization header
                // Middleware will handle validation on API end
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return content;
            }
            catch(Exception ex)
            {
                return ex.ToString();
            }
        }

        private static async Task DisplayAccountsAsync(IPublicClientApplication pca)
        {
            IEnumerable<IAccount> accounts = await pca.GetAccountsAsync().ConfigureAwait(false);

            Console.WriteLine(
                string.Format(
                    CultureInfo.CurrentCulture, "For the public client, the tokenCache contains {0} token(s)", accounts.Count()));

            foreach (var account in accounts)
            {
                Console.WriteLine("Account for: " + account.Username + "\n");
            }
        }
    }
}
