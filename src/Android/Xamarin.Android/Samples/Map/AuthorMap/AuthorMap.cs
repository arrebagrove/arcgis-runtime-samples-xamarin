// Copyright 2016 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Auth;

namespace ArcGISRuntimeXamarin.Samples.AuthorMap
{
    [Activity]
    public class AuthorMap : Activity
    {
        // Create and hold reference to the used MapView
        private MapView _myMapView = new MapView();
        
        // TaskCompletionSource for asynchronously authenticating the user
        private TaskCompletionSource<Credential> _tcs;

        // String array to store basemap constructor types
        private string[] _basemapTypes = new string[]
        {
            "Topographic",
            "Streets",
            "Imagery",
            "Oceans"
        };

        // Dictionary of operational layer names and URLs
        private Dictionary<string, string> _operationalLayerUrls = new Dictionary<string, string>
        {
            {"World Elevations", "http://sampleserver5.arcgisonline.com/arcgis/rest/services/Elevation/WorldElevations/MapServer"},
            {"World Cities", "http://sampleserver6.arcgisonline.com/arcgis/rest/services/SampleWorldCities/MapServer/" },
            {"US Census Data", "http://sampleserver5.arcgisonline.com/arcgis/rest/services/Census/MapServer"}
        };

        // Constants for OAuth-related values ...
        // URL of the server to authenticate with
        private const string ServerUrl = "https://www.arcgis.com/sharing/rest";

        // TODO: Add Client ID for an app registered with the server
        private const string AppClientId = "2Gh53JRzkPtOENQq";

        // TODO: Add URL for redirecting after a successful authorization
        //       Note - this must be a URL configured as a valid Redirect URI with your app
        private const string OAuthRedirectUrl = "http://myapps.portalmapapp";

        // URL used by the server for authorization
        private const string AuthorizeUrl = "https://www.arcgis.com/sharing/oauth2/authorize";

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Title = "Author and save a map";

            // Create the UI, setup the control references and execute initialization 
            CreateLayout();
            Initialize();
            UpdateAuthenticationManager();
        }

        private void Initialize()
        {
            // Create new Map with basemap
            Map myMap = new Map(Basemap.CreateTopographic());

            // Provide used Map to the MapView
            _myMapView.Map = myMap;
        }

        private void CreateLayout()
        {
            // Create a horizontal layout for the buttons at the top
            var buttonLayout = new LinearLayout(this) { Orientation = Orientation.Horizontal };

            // Create button to show available basemap
            var basemapButton = new Button(this);
            basemapButton.Text = "Choose Basemap";
            basemapButton.Click += OnBasemapsClicked;

            // Create a button to show operational layers
            var layersButton = new Button(this);
            layersButton.Text = "Choose Layers";
            layersButton.Click += OnLayersClicked;

            // Create a button to save the map
            var saveMapButton = new Button(this);
            saveMapButton.Text = "Save ...";
            saveMapButton.Click += OnSaveMapClicked;

            // Add basemap, layers, and save buttons to the layout
            buttonLayout.AddView(basemapButton);
            buttonLayout.AddView(layersButton);
            buttonLayout.AddView(saveMapButton);

            // Create a new vertical layout for the app (buttons followed by map view)
            var mainLayout = new LinearLayout(this) { Orientation = Orientation.Vertical };

            // Add the button layout
            mainLayout.AddView(buttonLayout);

            // Add the map view to the layout
            mainLayout.AddView(_myMapView);

            // Show the layout in the app
            SetContentView(mainLayout);
        }

        private void OnSaveMapClicked(object sender, EventArgs e)
        {
            // Create a dialog to show save options (title, description, and tags)
            SaveDialogFragment saveMapDialog = new SaveDialogFragment();
            saveMapDialog.OnSaveClicked += SaveMapAsync;
            // Begin a transaction to show a UI fragment (the save dialog)
            FragmentTransaction trans = FragmentManager.BeginTransaction();
            saveMapDialog.Show(trans, "save map");

        }

        private async void SaveMapAsync(object sender, OnSaveMapEventArgs e)
        {
            var alertBuilder = new AlertDialog.Builder(_myMapView.Context);

            try
            {
                // Show the progress bar so the user knows work is happening
                //SaveProgressBar.Visibility = Visibility.Visible;

                // Get information entered by the user for the new portal item properties
                var title = e.MapTitle;
                var description = e.MapDescription;
                var tags = e.Tags;

                // Get the current map
                var myMap = _myMapView.Map;

                // Apply the current extent as the map's initial extent
                myMap.InitialViewpoint = _myMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                // See if the map has already been saved (has an associated portal item)
                if (myMap.PortalItem == null)
                {
                    // Call a function to save the map as a new portal item
                    await SaveNewMapAsync(myMap, title, description, tags);

                    // Report a successful save
                    alertBuilder.SetTitle("Map Saved");
                    alertBuilder.SetMessage("Saved '" + title + "' to ArcGIS Online!");
                    alertBuilder.Show();
                }
                else
                {
                    // This is not the initial save, call SaveAsync to save changes to the existing portal item
                    await myMap.SaveAsync();

                    // Report update was successful
                    alertBuilder.SetTitle("Updates Saved");
                    alertBuilder.SetMessage("Saved changes to '" + myMap.PortalItem.Title + "'");
                    alertBuilder.Show();
                }
            }
            catch (Exception ex)
            {
                // Show the exception message 
                alertBuilder.SetTitle("Unable to save map");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
            }
            finally
            {
                // Hide the progress bar
                //SaveProgressBar.Visibility = Visibility.Hidden;
            }
        }

        private async Task SaveNewMapAsync(Map myMap, string title, string description, string[] tags)
        {
            // Challenge the user for portal credentials (OAuth credential request for arcgis.com)
            CredentialRequestInfo loginInfo = new CredentialRequestInfo();

            // Use the OAuth implicit grant flow
            loginInfo.GenerateTokenOptions = new GenerateTokenOptions
            {
                TokenAuthenticationType = TokenAuthenticationType.OAuthImplicit
            };

            // Indicate the url (portal) to authenticate with (ArcGIS Online)
            loginInfo.ServiceUri = new Uri("http://www.arcgis.com/sharing/rest");

            try
            {
                // Get a reference to the (singleton) AuthenticationManager for the app
                AuthenticationManager thisAuthenticationManager = AuthenticationManager.Current;

                // Call GetCredentialAsync on the AuthenticationManager to invoke the challenge handler
                await thisAuthenticationManager.GetCredentialAsync(loginInfo, false);
            }
            catch (System.OperationCanceledException)
            {
                // user canceled the login
                throw new Exception("Portal log in was canceled.");
            }

            // Get the ArcGIS Online portal (will use credential from login above)
            ArcGISPortal agsOnline = await ArcGISPortal.CreateAsync();

            // Save the current state of the map as a portal item in the user's default folder
            await myMap.SaveAsAsync(agsOnline, null, title, description, tags, null);
        }

        #region Basemaps Button
        private void OnBasemapsClicked(object sender, EventArgs e)
        {
            var mapsButton = sender as Button;

            // Create a menu to show basemaps
            var mapsMenu = new PopupMenu(mapsButton.Context, mapsButton);
            mapsMenu.MenuItemClick += OnBasemapsMenuItemClicked;

            // Create a menu option for each basemap type
            foreach (var basemapType in _basemapTypes)
            {
                mapsMenu.Menu.Add(basemapType);
            }

            // Show menu in the view
            mapsMenu.Show();
        }

        private void OnBasemapsMenuItemClicked(object sender, PopupMenu.MenuItemClickEventArgs e)
        {
            // Get the title of the selected item
            var selectedBasemapType = e.Item.TitleCondensedFormatted.ToString();

            // Apply the chosen basemap
            switch (selectedBasemapType)
            {
                case "Topographic":
                    // Set the basemap to Topographic
                    _myMapView.Map.Basemap = Basemap.CreateTopographic();
                    break;
                case "Streets":
                    // Set the basemap to Streets
                    _myMapView.Map.Basemap = Basemap.CreateStreets();
                    break;
                case "Imagery":
                    // Set the basemap to Imagery
                    _myMapView.Map.Basemap = Basemap.CreateImagery();
                    break;
                case "Oceans":
                    // Set the basemap to Oceans
                    _myMapView.Map.Basemap = Basemap.CreateOceans();
                    break;
            }
        }
        #endregion

        #region Layers Button
        private void OnLayersClicked(object sender, EventArgs e)
        {
            var layerButton = sender as Button;

            // Create menu to show layers
            var layerMenu = new PopupMenu(layerButton.Context, layerButton);
            layerMenu.MenuItemClick += OnLayerMenuItemClicked;

            // Create menu options
            foreach (var layerInfo in _operationalLayerUrls)
            {
                layerMenu.Menu.Add(layerInfo.Key);
            }

            // Show menu in the view
            layerMenu.Show();
        }

        private void OnLayerMenuItemClicked(object sender, PopupMenu.MenuItemClickEventArgs e)
        {
            // Get the title of the selected item
            var selectedLayerName = e.Item.TitleCondensedFormatted.ToString();

            // See if the layer already exists
            ArcGISMapImageLayer layer = _myMapView.Map.OperationalLayers.FirstOrDefault(l => l.Name == selectedLayerName) as ArcGISMapImageLayer;

            // If the layer is in the map, remove it
            if (layer != null)
            {
                _myMapView.Map.OperationalLayers.Remove(layer);
            }
            else
            {
                // Get the URL for this layer
                var layerUrl = _operationalLayerUrls[selectedLayerName];
                var layerUri = new Uri(layerUrl);

                // Create a new map image layer
                layer = new ArcGISMapImageLayer(layerUri);
                layer.Name = selectedLayerName;

                // Set it 50% opaque, and add it to the map
                layer.Opacity = 0.5;
                _myMapView.Map.OperationalLayers.Add(layer);
            }
        }
        #endregion


        #region OAuth helpers
        private void UpdateAuthenticationManager()
        {
            // Register the server information with the AuthenticationManager
            ServerInfo portalServerInfo = new ServerInfo
            {
                ServerUri = new Uri(ServerUrl),
                OAuthClientInfo = new OAuthClientInfo
                {
                    ClientId = AppClientId,
                    RedirectUri = new Uri(OAuthRedirectUrl)
                },
                // Specify OAuthAuthorizationCode if you need a refresh token (and have specified a valid client secret)
                // Otherwise, use OAuthImplicit
                TokenAuthenticationType = TokenAuthenticationType.OAuthImplicit
            };

            // Get a reference to the (singleton) AuthenticationManager for the app
            AuthenticationManager thisAuthenticationManager = AuthenticationManager.Current;

            // Register the server information
            thisAuthenticationManager.RegisterServer(portalServerInfo);
            
            // Assign the method that AuthenticationManager will call to challenge for secured resources
            thisAuthenticationManager.ChallengeHandler = new ChallengeHandler(CreateCredentialAsync);
        }

        // ChallengeHandler function for AuthenticationManager that will be called whenever access to a secured
        // resource is attempted
        private async Task<Credential> CreateCredentialAsync(CredentialRequestInfo info)
        {
            // Create a TaskCompletionSource to contain the result of authorization
            _tcs = new TaskCompletionSource<Credential>();

            // Call a function to authorize with OAuth
            TryAuthenticateOAuth(info.ServiceUri, AppClientId, AuthorizeUrl, OAuthRedirectUrl);

            // Return the asynchronous result of the task when complete
            return await _tcs.Task;
        }

        /// Show a UI for authenticating with OAuth and handle the result (successful authentication or error/failure)
        /// </summary>
        /// <param name="secureUri">The System.Uri of the secured resource</param>
        /// <param name="clientID">Client ID for an app registered with the server hosting the secure resource</param>
        /// <param name="authorizeUrl">URL string of the OAuth authorization end point for the server</param>
        /// <param name="redirectUrl">URL string for the server to direct the response when authentication is complete</param>
        private void TryAuthenticateOAuth(Uri secureUri, string clientID, string authorizeUrl, string redirectUrl)
        {
            // Create a new Xamarin.Auth.OAuth2Authenticator using the information passed in
            Xamarin.Auth.OAuth2Authenticator auth = new OAuth2Authenticator(
                clientId: clientID,
                scope: "",
                authorizeUrl: new Uri(authorizeUrl),
                redirectUrl: new Uri(redirectUrl));

            // Allow the user to cancel the OAuth attempt
            auth.AllowCancel = true;

            // Define a handler for the OAuth2Authenticator.Completed event
            auth.Completed += (sender, authArgs) =>
            {
                try
                {
                    // Throw an exception if the user could not be authenticated
                    if (!authArgs.IsAuthenticated)
                    {
                        throw new Exception("Unable to authenticate user.");
                    }

                    // If authorization was successful, get the user's account
                    Xamarin.Auth.Account authenticatedAccount = authArgs.Account;

                    // Create a Esri.ArcGISRuntime.Security.OAuthTokenCredential for use with the AuthenticationManager
                    // Set properties of the credential using information from the authenticated account
                    Esri.ArcGISRuntime.Security.OAuthTokenCredential cred = new OAuthTokenCredential
                    {
                        Token = authenticatedAccount.Properties["access_token"],
                        UserName = authenticatedAccount.Properties["username"],
                        ServiceUri = secureUri
                    };

                    // Add the OAuthTokenCredential to the AuthenticationManager
                    AuthenticationManager.Current.AddCredential(cred);

                    // Set the result (Credential) for the TaskCompletionSource
                    _tcs.SetResult(cred);
                }
                catch (Exception ex)
                {
                    // If authentication failed, set the exception on the TaskCompletionSource
                    _tcs.SetException(ex);
                }
                finally
                {
                    // End the OAuth login activity
                    this.FinishActivity(99);
                }
            };

            // If an error was encountered when authenticating, set the exception on the TaskCompletionSource
            auth.Error += (sndr, errArgs) =>
            {
                _tcs.SetException(errArgs.Exception);
            };

            // Present the OAuth UI (Activity) so the user can enter user name and password
            var intent = auth.GetUI(this);
            this.StartActivityForResult(intent, 99);
        }
        #endregion
    }

    // A custom DialogFragment class to show input controls for saving a web map
    public class SaveDialogFragment : DialogFragment
    {
        private EditText _mapTitleTextbox;
        private EditText _mapDescriptionTextbox;
        private EditText _tagsTextbox;

        public event EventHandler<OnSaveMapEventArgs> OnSaveClicked;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            base.OnCreateView(inflater, container, savedInstanceState);

            // The container for the dialog is a vertical linear layout
            LinearLayout dialogView = new LinearLayout(this.Context);
            dialogView.Orientation = Orientation.Vertical;

            // Add a text box for entering a title for the new web map
            _mapTitleTextbox = new EditText(this.Context);
            _mapTitleTextbox.Hint = "Title";
            dialogView.AddView(_mapTitleTextbox);

            // Add a text box for entering a description
            _mapDescriptionTextbox = new EditText(this.Context);
            _mapDescriptionTextbox.Hint = "Description";
            dialogView.AddView(_mapDescriptionTextbox);

            // Add a text box for entering tags (populate with some values so the user doesn't have to fill this in)
            _tagsTextbox = new EditText(this.Context);
            _tagsTextbox.Text = "ArcGIS Runtime, Web Map";
            dialogView.AddView(_tagsTextbox);

            // Add a button to save the map
            Button saveMapButton = new Button(this.Context);
            saveMapButton.Text = "Save";
            saveMapButton.Click += SaveMapButtonClick;
            dialogView.AddView(saveMapButton);

            // Return the new view for display
            return dialogView;
        }

        // A click handler for the save map button
        private void SaveMapButtonClick(object sender, EventArgs e)
        {
            try
            {
                // Get information for the new portal item
                var title = _mapTitleTextbox.Text;
                var description = _mapDescriptionTextbox.Text;
                var tags = _tagsTextbox.Text.Split(',');

                // Make sure all required info was entered
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description) || tags.Length == 0)
                {
                    throw new Exception("Please enter a title, description, and some tags to describe the map.");
                }

                // Create a new OnSaveMapEventArgs object to store the information entered by the user
                var mapSavedArgs = new OnSaveMapEventArgs(title, description, tags);

                // Raise the OnSaveClicked event so the main activity can handle the event and save the map
                OnSaveClicked(this, mapSavedArgs);

                // Close the dialog
                this.Dismiss();
            }
            catch(Exception ex)
            {
                // Show the exception message (dialog will stay open so user can try again)
                var alertBuilder = new AlertDialog.Builder(this.Context);
                alertBuilder.SetTitle("Error");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
            }
        }
    }

    // Custom EventArgs class for containing portal item properties when saving a map
    public class OnSaveMapEventArgs : EventArgs
    {
        public string MapTitle { get; set; }
        public string MapDescription { get; set; }
        public string[] Tags { get; set; }

        public OnSaveMapEventArgs(string title, string description, string[] tags) : base()
        {
            MapTitle = title;
            MapDescription = description;
            Tags = tags;
        }
    }

}