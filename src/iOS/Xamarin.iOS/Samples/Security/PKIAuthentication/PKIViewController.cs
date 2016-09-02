using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.UI;
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using UIKit;

namespace PKIAuthentication
{
    public partial class PKIViewController : UIViewController
    {
        private MapView _myMapView;

        //TODO - Add the URL for your PKI-secured portal
        const string SecuredPortalUrl = "https://my.secure.portal.com/gis/sharing";

        //TODO - Add the ID for a web map item stored on the secure portal 
        const string WebMapId = "";
        private const string CertificateFileName = "Cert/ThadCert3.pfx";
        private const string CertificatePassword = "";

        public PKIViewController() : base("PKIViewController", null)
        {
        }
        
        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            CreateLayout();
            Initialize();
        }

        private void CreateLayout()
        {
            // Setup the visual frame for the MapView
            var mapViewRect = new CoreGraphics.CGRect(0, 90, View.Bounds.Width, View.Bounds.Height - 90);

            // Create a map view with a basemap
            _myMapView = new MapView();
            _myMapView.Map = new Map(Basemap.CreateImagery());
            _myMapView.Frame = mapViewRect;

            // Create a button to load a web map
            var buttonRect = new CoreGraphics.CGRect(40, 50, View.Bounds.Width - 80, 30);
            UIButton loadWebMapButton = new UIButton(buttonRect);
            loadWebMapButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            loadWebMapButton.SetTitle("Load secure web map", UIControlState.Normal);
            loadWebMapButton.TouchUpInside += LoadWebMapButton_TouchUpInside;

            // Add the map view and button to the page
            View.AddSubviews(loadWebMapButton, _myMapView);
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

            return cred;
        }

        private void LoadClientCertificate()
        {
            try
            {
                var path = System.IO.Path.Combine(Foundation.NSBundle.MainBundle.BundlePath, CertificateFileName);
                var certificateData = System.IO.File.ReadAllBytes(path);
                
                var certificate = new X509Certificate(certificateData, CertificatePassword);
                var certificateCredential = new CertificateCredential(certificate);
                certificateCredential.ServiceUri = new Uri(SecuredPortalUrl);
                AuthenticationManager.Current.AddCredential(certificateCredential);
            }
            catch (Exception exp)
            {

            }
        }

        private async void LoadWebMapButton_TouchUpInside(object sender, EventArgs e)
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

                // Create an instance of the IWA-secured portal
                ArcGISPortal iwaSecuredPortal = await ArcGISPortal.CreateAsync(new Uri(SecuredPortalUrl));

                // Report a successful connection
                messageBuilder.AppendLine("Connected to the portal on " + iwaSecuredPortal.Uri.Host);
                messageBuilder.AppendLine("Version: " + iwaSecuredPortal.CurrentVersion);

                // Report the username for this connection
                if (iwaSecuredPortal.CurrentUser != null)
                {
                    messageBuilder.AppendLine("Connected as: " + iwaSecuredPortal.CurrentUser.UserName);
                }
                else
                {
                    // This shouldn't happen (if the portal is truly secured)!
                    messageBuilder.AppendLine("Connected anonymously");
                }

                // Get the web map (portal item) to display                
                var webMap = await ArcGISPortalItem.CreateAsync(iwaSecuredPortal, WebMapId);
                if (webMap != null)
                {
                    // Create a new map from the portal item and display it in the map view
                    var map = new Map(webMap);
                    _myMapView.Map = map;
                }
            }
            catch (TaskCanceledException)
            {
                // Report canceled login
                messageBuilder.AppendLine("Login was canceled");
            }
            catch (Exception ex)
            {
                // Report error
                messageBuilder.AppendLine("Exception: " + ex.Message);
            }
            finally
            {
                // Display the status of the login
                UIAlertView alert = new UIAlertView("Status", messageBuilder.ToString(), null, "OK");
                alert.Show();
            }
        }
    }
}