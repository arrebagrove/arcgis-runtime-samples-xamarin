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
    public class AuthorMap : Activity, IOAuthAuthorizeHandler
    {
        // Create and hold reference to the used MapView
        private MapView _myMapView = new MapView();

        // Progress bar to show when the app is working
        ProgressBar _progressBar;

        // Use a TaskCompletionSource to track the completion of the authorization
        private TaskCompletionSource<IDictionary<string, string>> _taskCompletionSource;

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

            // Create a progress bar to show when the app is working
            _progressBar = new ProgressBar(this);
            _progressBar.Indeterminate = true;
            _progressBar.Visibility = ViewStates.Invisible;
            
            // Create button to show available basemap
            var basemapButton = new Button(this);
            basemapButton.Text = "Basemap";
            basemapButton.Click += OnBasemapsClicked;

            // Create a button to show operational layers
            var layersButton = new Button(this);
            layersButton.Text = "Layers";
            layersButton.Click += OnLayersClicked;

            // Create a button to save the map
            var saveMapButton = new Button(this);
            saveMapButton.Text = "Save ...";
            saveMapButton.Click += OnSaveMapClicked;

            // Add basemap, layers, progress bar, and save buttons to the layout
            buttonLayout.AddView(basemapButton);
            buttonLayout.AddView(layersButton);
            buttonLayout.AddView(_progressBar);
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
            SaveDialogFragment saveMapDialog = new SaveDialogFragment(_myMapView.Map.PortalItem);
            saveMapDialog.OnSaveClicked += SaveMapAsync;

            // Begin a transaction to show a UI fragment (the save dialog)
            FragmentTransaction trans = FragmentManager.BeginTransaction();
            saveMapDialog.Show(trans, "save map");
        }

        private async void SaveMapAsync(object sender, OnSaveMapEventArgs e)
        {
            var alertBuilder = new AlertDialog.Builder(this);

            // Get the current map
            var myMap = _myMapView.Map;

            try
            {
                // Show the progress bar so the user knows work is happening
                _progressBar.Visibility = ViewStates.Visible;

                // Get information entered by the user for the new portal item properties
                var title = e.MapTitle;
                var description = e.MapDescription;
                var tags = e.Tags;

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
                _progressBar.Visibility = ViewStates.Invisible;                
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

        #region Basemap Button
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

            // Set the OAuth authorization handler to this class (Implements IOAuthAuthorize interface)
            thisAuthenticationManager.OAuthAuthorizeHandler = this;
        }

        // ChallengeHandler function for AuthenticationManager, called whenever access to a secured resource is attempted
        private async Task<Credential> CreateCredentialAsync(CredentialRequestInfo info)
        {
            OAuthTokenCredential credential = null;

            try
            {
                // Create generate token options if necessary
                if (info.GenerateTokenOptions == null)
                {
                    info.GenerateTokenOptions = new GenerateTokenOptions();
                }

                // AuthenticationManager will handle challenging the user for credentials
                credential = await AuthenticationManager.Current.GenerateCredentialAsync
                    (
                            info.ServiceUri,
                            info.GenerateTokenOptions
                    ) as OAuthTokenCredential;
            }
            catch (Exception ex)
            {
                // Exception will be reported in calling function
                throw (ex);
            }

            return credential;
        }

        // IOAuthAuthorizeHandler.AuthorizeAsync implementation
        public Task<IDictionary<string, string>> AuthorizeAsync(Uri serviceUri, Uri authorizeUri, Uri callbackUri)
        {
            // If the TaskCompletionSource is not null, authorization is in progress
            if (_taskCompletionSource != null)
            {
                // Allow only one authorization process at a time
                throw new Exception();
            }

            // Create a task completion source
            _taskCompletionSource = new TaskCompletionSource<IDictionary<string, string>>();

            // Create a new Xamarin.Auth.OAuth2Authenticator using the information passed in
            Xamarin.Auth.OAuth2Authenticator authenticator = new OAuth2Authenticator(
                clientId:  AppClientId,
                scope: "",
                authorizeUrl: authorizeUri,
                redirectUrl: callbackUri);

            // Allow the user to cancel the OAuth attempt
            authenticator.AllowCancel = true;

            // Define a handler for the OAuth2Authenticator.Completed event
            authenticator.Completed += (sender, authArgs) =>
            {
                try
                {
                    // Check if the user is authenticated
                    if (authArgs.IsAuthenticated)
                    {
                        // If authorization was successful, get the user's account
                        Xamarin.Auth.Account authenticatedAccount = authArgs.Account;

                        // Set the result (Credential) for the TaskCompletionSource
                        _taskCompletionSource.SetResult(authenticatedAccount.Properties);
                    }
                }
                catch (Exception ex)
                {
                    // If authentication failed, set the exception on the TaskCompletionSource
                    _taskCompletionSource.SetException(ex);
                }
                finally
                {
                    // End the OAuth login activity
                    this.FinishActivity(99);
                }
            };

            // If an error was encountered when authenticating, set the exception on the TaskCompletionSource
            authenticator.Error += (sndr, errArgs) =>
            {
                // If the user cancels, the Error event is raised but there is no exception ... best to check first
                if (errArgs.Exception != null)
                {
                    _taskCompletionSource.SetException(errArgs.Exception);
                }
                else
                {
                    // Login canceled: end the OAuth login activity
                    if(_taskCompletionSource != null)
                    {
                        _taskCompletionSource.TrySetCanceled();
                        this.FinishActivity(99);
                    }
                }
            };

            // Present the OAuth UI (Activity) so the user can enter user name and password
            var intent = authenticator.GetUI(this);
            this.StartActivityForResult(intent, 99);

            // Return completion source task so the caller can await completion
            return _taskCompletionSource.Task;
        }

        private static IDictionary<string, string> DecodeParameters(Uri uri)
        {
            // Create a dictionary of key value pairs returned in an OAuth authorization response URI query string
            var answer = string.Empty;

            // Get the values from the URI fragment or query string
            if (!string.IsNullOrEmpty(uri.Fragment))
            {
                answer = uri.Fragment.Substring(1);
            }
            else
            {
                if (!string.IsNullOrEmpty(uri.Query))
                {
                    answer = uri.Query.Substring(1);
                }
            }

            // Parse parameters into key / value pairs
            var keyValueDictionary = new Dictionary<string, string>();
            var keysAndValues = answer.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var kvString in keysAndValues)
            {
                var pair = kvString.Split('=');
                string key = pair[0];
                string value = string.Empty;
                if (key.Length > 1)
                {
                    value = Uri.UnescapeDataString(pair[1]);
                }

                keyValueDictionary.Add(key, value);
            }

            // Return the dictionary of string keys/values
            return keyValueDictionary;
        }
        #endregion
    }

    // A custom DialogFragment class to show input controls for saving a web map
    public class SaveDialogFragment : DialogFragment
    {
        // Inputs for portal item title, description, and tags
        private EditText _mapTitleTextbox;
        private EditText _mapDescriptionTextbox;
        private EditText _tagsTextbox;

        // Store any existing portal item (for "update" versus "save", e.g.)
        private ArcGISPortalItem _portalItem = null;

        // Raise an event so the listener can access input values when the form has been completed
        public event EventHandler<OnSaveMapEventArgs> OnSaveClicked;

        public SaveDialogFragment(ArcGISPortalItem mapItem)
        {
            this._portalItem = mapItem;
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Dialog to display
            LinearLayout dialogView = null;

            // Get the context for creating the dialog controls
            Android.Content.Context ctx = this.Activity.ApplicationContext;

            // Set a dialog title
            this.Dialog.SetTitle("Save Map to Portal");

            try
            {
                base.OnCreateView(inflater, container, savedInstanceState);

                // The container for the dialog is a vertical linear layout
                dialogView = new LinearLayout(ctx);
                dialogView.Orientation = Orientation.Vertical;

                // Add a text box for entering a title for the new web map
                _mapTitleTextbox = new EditText(ctx);
                _mapTitleTextbox.Hint = "Title";                
                dialogView.AddView(_mapTitleTextbox);

                // Add a text box for entering a description
                _mapDescriptionTextbox = new EditText(ctx);
                _mapDescriptionTextbox.Hint = "Description";
                dialogView.AddView(_mapDescriptionTextbox);

                // Add a text box for entering tags (populate with some values so the user doesn't have to fill this in)
                _tagsTextbox = new EditText(ctx);
                _tagsTextbox.Text = "ArcGIS Runtime, Web Map";
                dialogView.AddView(_tagsTextbox);

                // Add a button to save the map
                Button saveMapButton = new Button(ctx);
                saveMapButton.Text = "Save";
                saveMapButton.Click += SaveMapButtonClick;
                dialogView.AddView(saveMapButton);

                // If there's an existing portal item, configure the dialog for "update" (read-only entries)
                if (this._portalItem != null)
                {
                    _mapTitleTextbox.Text = this._portalItem.Title;
                    _mapTitleTextbox.Enabled = false;

                    _mapDescriptionTextbox.Text = this._portalItem.Description;
                    _mapDescriptionTextbox.Enabled = false;

                    _tagsTextbox.Text = string.Join(",", this._portalItem.Tags);
                    _tagsTextbox.Enabled = false;

                    // Change some of the control text
                    this.Dialog.SetTitle("Save Changes to Map");
                    saveMapButton.Text = "Update";
                }
            }
            catch (Exception ex)
            {
                // Show the exception message 
                var alertBuilder = new AlertDialog.Builder(this.Activity);
                alertBuilder.SetTitle("Error");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
            }

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
                var alertBuilder = new AlertDialog.Builder(this.Activity);
                alertBuilder.SetTitle("Error");
                alertBuilder.SetMessage(ex.Message);
                alertBuilder.Show();
            }
        }
    }

    // Custom EventArgs class for containing portal item properties when saving a map
    public class OnSaveMapEventArgs : EventArgs
    {
        // Portal item title
        public string MapTitle { get; set; }

        // Portal item description
        public string MapDescription { get; set; }

        // Portal item tags
        public string[] Tags { get; set; }

        public OnSaveMapEventArgs(string title, string description, string[] tags) : base()
        {
            MapTitle = title;
            MapDescription = description;
            Tags = tags;
        }
    }

}