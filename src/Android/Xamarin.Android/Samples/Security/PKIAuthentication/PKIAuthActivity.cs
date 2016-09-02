using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Esri.ArcGISRuntime.Security;
using System.Threading.Tasks;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI;
using System.Security.Cryptography.X509Certificates;

namespace PKIAuthentication
{
    [Activity(Label = "PKIAuthentication", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        //TODO - Add the URL for your IWA-secured portal
        const string SecuredPortalUrl = "https://my.secure.portal.com/gis/sharing";

        //TODO - Add the ID for a web map item stored on the secure portal 
        const string WebMapId = "";
        
        // Use a TaskCompletionSource to store the result of a login task
        TaskCompletionSource<Credential> _loginTaskCompletionSrc;

        // Store the map view displayed in the app
        MapView _myMapView;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Call a function to create the UI
            CreateLayout();

            // Call a function to initialize the app
            Initialize();
        }

        private void CreateLayout()
        {
            // Create a simple UI that contains a map view and a button
            var mainLayout = new LinearLayout(this) { Orientation = Orientation.Vertical };

            // Create a new map view
            _myMapView = new MapView();

            // Create a button to load a web map and set a click event handler
            Button loadMapButton = new Button(this);
            loadMapButton.Text = "Load secure map";
            loadMapButton.Click += LoadSecureMap;

            // Add the elements to the layout 
            mainLayout.AddView(loadMapButton);
            mainLayout.AddView(_myMapView);

            // Apply the layout to the app
            SetContentView(mainLayout);
        }

        private void Initialize()
        {
            // Show the imagery basemap in the map view initially
            var map = new Map(Basemap.CreateImagery());
            _myMapView.Map = map;

            // Define a challenge handler method for the AuthenticationManager 
            // (this method handles getting credentials when a secured resource is encountered)
            AuthenticationManager.Current.ChallengeHandler = new ChallengeHandler(CreateCredentialAsync);
        }

        // Connect to the portal identified by the SecuredPortalUrl variable and load the web map identified by WebMapId
        private async void LoadSecureMap(object s, EventArgs e)
        {
            // Store messages that describe success or errors connecting to the secured portal and opening the web map
            var messageBuilder = new System.Text.StringBuilder();

            try
            {
                // See if a credential exists for this portal in the AuthenticationManager
                // If a credential is not found, the user will be prompted for login info
                CredentialRequestInfo info = new CredentialRequestInfo
                {
                    ServiceUri = new Uri(SecuredPortalUrl),
                    AuthenticationType = AuthenticationType.NetworkCredential
                };
                Credential cred = await AuthenticationManager.Current.GetCredentialAsync(info, false);

                // Create an instance of the PKI-secured portal
                ArcGISPortal pkiSecuredPortal = await ArcGISPortal.CreateAsync(new Uri(SecuredPortalUrl));

                // Report a successful connection
                messageBuilder.AppendLine("Connected to the portal on " + pkiSecuredPortal.Uri.Host);
                messageBuilder.AppendLine("Version: " + pkiSecuredPortal.CurrentVersion);

                // Report the username for this connection
                if (pkiSecuredPortal.CurrentUser != null)
                {
                    messageBuilder.AppendLine("Connected as: " + pkiSecuredPortal.CurrentUser.UserName);
                }
                else
                {
                    // This shouldn't happen (if the portal is truly secured)!
                    messageBuilder.AppendLine("Connected anonymously");
                }

                // Get the web map (portal item) to display                
                var webMap = await ArcGISPortalItem.CreateAsync(pkiSecuredPortal, WebMapId);
                if (webMap != null)
                {
                    // Create a new map from the portal item and display it in the map view
                    var map = new Map(webMap);
                    _myMapView.Map = map;
                }
            }
            catch (Exception ex)
            {
                // Report error
                messageBuilder.AppendLine("**-Exception: " + ex.Message);
            }
            finally
            {
                // Show an alert dialog with the status messages
                var alertBuilder = new AlertDialog.Builder(this);
                alertBuilder.SetTitle("Status");
                alertBuilder.SetMessage(messageBuilder.ToString());
                alertBuilder.Show();
            }
        }

        // AuthenticationManager.ChallengeHandler function that prompts the user for login information to create a credential
        private async Task<Credential> CreateCredentialAsync(CredentialRequestInfo info)
        {
            Credential cred = null;

            try
            {
                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, true);
                //System.Security.Cryptography.X509Certificates.X509Certificate2UI ui;

                
            }
            catch (Exception ex)
            {
                
            }


            // See if authentication is already in process
            if (_loginTaskCompletionSrc != null) { return null; }

            // Create a new TaskCompletionSource for the login operation
            // (passing the CredentialRequestInfo object to the constructor will make it available from its AsyncState property)
            _loginTaskCompletionSrc = new TaskCompletionSource<Credential>(info);

            // Return the login task, the result will be ready when completed (user provides login info and clicks the "Login" button)
            return await _loginTaskCompletionSrc.Task;
        }        
    }
}

