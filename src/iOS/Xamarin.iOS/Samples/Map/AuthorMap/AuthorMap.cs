﻿// Copyright 2016 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using CoreFoundation;
using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.UI;
using Foundation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using UIKit;
using Xamarin.Auth;

namespace ArcGISRuntimeXamarin.Samples.AuthorMap
{
    [Register("AuthorMap")]
    public class AuthorMap : UIViewController, IOAuthAuthorizeHandler
    {
        // Reference to the MapView used in the app
        private MapView _myMapView;

        // Dictionary of operational layer names and URLs
        private Dictionary<string, string> _operationalLayerUrls = new Dictionary<string, string>
        {
            {"World Elevations", "http://sampleserver5.arcgisonline.com/arcgis/rest/services/Elevation/WorldElevations/MapServer"},
            {"World Cities", "http://sampleserver6.arcgisonline.com/arcgis/rest/services/SampleWorldCities/MapServer/" },
            {"US Census Data", "http://sampleserver5.arcgisonline.com/arcgis/rest/services/Census/MapServer"}
        };

        // Use a TaskCompletionSource to track the completion of the authorization
        private TaskCompletionSource<IDictionary<string, string>> _taskCompletionSource;

        // Overlay with entry controls for map item details (title, description, and tags)
        SaveMapDialogOverlay _mapInfoUI;

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

        public AuthorMap()
        {
            Title = "Author and save a map";
        }
        
        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Create the layout
            CreateLayout();

            // Initialize the app
            Initialize();
        }

        private void Initialize()
        {
            // Create new Map with a light gray canvas basemap
            var myMap = new Map(Basemap.CreateLightGrayCanvas());

            // Add the Map to the MapView
            _myMapView.Map = myMap;
        }

        private void CreateLayout()
        {
            // Define an offset from the top of the page (to account for the iOS status bar)
            var yPageOffset = 60;

            // Create a new MapView control
            _myMapView = new MapView();

            // Define the visual frame for the MapView
            _myMapView.Frame = new CoreGraphics.CGRect(0, yPageOffset, View.Bounds.Width, View.Bounds.Height - yPageOffset);

            // Add a segmented button control
            var segmentButton = new UISegmentedControl();
            segmentButton.BackgroundColor = UIColor.White;
            segmentButton.Frame = new CoreGraphics.CGRect(0, _myMapView.Bounds.Height, View.Bounds.Width, 30);
            segmentButton.InsertSegment("Basemap", 0, false);
            segmentButton.InsertSegment("Layers", 1, false);
            segmentButton.InsertSegment("New", 2, false);
            segmentButton.InsertSegment("Save", 3, false);

            // Handle the "click" for each segment (new segment is selected)
            segmentButton.ValueChanged += SegmentButtonClicked;           

            // Add the MapView and UIButton to the page
            View.AddSubviews(_myMapView, segmentButton);
        }

        private void SegmentButtonClicked(object sender, EventArgs e)
        {
            // Get the segmented button control that raised the event
            var buttonControl = sender as UISegmentedControl;

            // Get the selected segment in the control
            var selectedSegmentId = buttonControl.SelectedSegment;

            // Execute the appropriate action for the control
            if(selectedSegmentId == 0)
            {
                // Show basemap choices
                ShowBasemapList();
            }
            else if(selectedSegmentId == 1)
            {
                // Show a list of available operational layers
                ShowLayerList();
            }
            else if(selectedSegmentId == 2)
            {
                // Clear the map from the map view (allow the user to start over and save as a new portal item)
                _myMapView.Map = new Map(Basemap.CreateLightGrayCanvas());
            }
            else if (selectedSegmentId == 3)
            {
                // Show the save map UI
                ShowSaveMapUI();
            }

            // Unselect all segments (user might want to click the same control twice)
            buttonControl.SelectedSegment = -1;
        }

        private void ShowBasemapList()
        {
            // Create a new Alert Controller
            UIAlertController basemapsActionSheet = UIAlertController.Create("Basemaps", "Choose a basemap", UIAlertControllerStyle.ActionSheet);

            // Add actions to apply each basemap type
            basemapsActionSheet.AddAction(UIAlertAction.Create("Topographic", UIAlertActionStyle.Default, (action) => _myMapView.Map.Basemap = Basemap.CreateTopographic()));
            basemapsActionSheet.AddAction(UIAlertAction.Create("Streets", UIAlertActionStyle.Default, (action) => _myMapView.Map.Basemap = Basemap.CreateStreets()));
            basemapsActionSheet.AddAction(UIAlertAction.Create("Imagery", UIAlertActionStyle.Default, (action) => _myMapView.Map.Basemap = Basemap.CreateImagery()));
            basemapsActionSheet.AddAction(UIAlertAction.Create("Oceans", UIAlertActionStyle.Default, (action) => _myMapView.Map.Basemap = Basemap.CreateOceans()));
            basemapsActionSheet.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, (action) => Console.WriteLine("Canceled")));

            // Required for iPad - You must specify a source for the Action Sheet since it is displayed as a popover
            UIPopoverPresentationController presentationPopover = basemapsActionSheet.PopoverPresentationController;
            if (presentationPopover != null)
            {
                presentationPopover.SourceView = this.View;
                presentationPopover.PermittedArrowDirections = UIPopoverArrowDirection.Up;
            }

            // Display the list of basemaps
            this.PresentViewController(basemapsActionSheet, true, null);
        }

        private void ShowLayerList()
        {
            // Create a new Alert Controller
            UIAlertController layersActionSheet = UIAlertController.Create("Layers", "Choose layers", UIAlertControllerStyle.ActionSheet);

            // Add actions to add or remove each of the available layers
            foreach(var kvp in _operationalLayerUrls)
            {
                layersActionSheet.AddAction(UIAlertAction.Create(kvp.Key, UIAlertActionStyle.Default, (action) => AddOrRemoveLayer(kvp.Key)));
            }

            // Add a choice to cancel
            layersActionSheet.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, (action) => Console.WriteLine("Canceled")));

            // Required for iPad - You must specify a source for the Action Sheet since it is displayed as a popover
            UIPopoverPresentationController presentationPopover = layersActionSheet.PopoverPresentationController;
            if (presentationPopover != null)
            {
                presentationPopover.SourceView = this.View;
                presentationPopover.PermittedArrowDirections = UIPopoverArrowDirection.Up;
            }

            // Display the list of layers to add/remove
            this.PresentViewController(layersActionSheet, true, null);
        }

        private void AddOrRemoveLayer(string layerName)
        {
            // See if the layer already exists
            ArcGISMapImageLayer layer = _myMapView.Map.OperationalLayers.FirstOrDefault(l => l.Name == layerName) as ArcGISMapImageLayer;

            // If the layer is in the map, remove it
            if (layer != null)
            {
                _myMapView.Map.OperationalLayers.Remove(layer);
            }
            else
            {
                // Get the URL for this layer
                var layerUrl = _operationalLayerUrls[layerName];
                var layerUri = new Uri(layerUrl);

                // Create a new map image layer
                layer = new ArcGISMapImageLayer(layerUri);
                layer.Name = layerName;

                // Set it 50% opaque, and add it to the map
                layer.Opacity = 0.5;
                _myMapView.Map.OperationalLayers.Add(layer);
            }
        }

        private void ShowSaveMapUI()
        {
            if(_mapInfoUI != null) { return; }

            // Create a view to show map item info entry controls over the map view
            var ovBounds = _myMapView.Bounds;
            _mapInfoUI = new SaveMapDialogOverlay(ovBounds, 0.75f, UIColor.White);

            // Handle the OnMapInfoEntered event to get the info entered by the user
            _mapInfoUI.OnMapInfoEntered += MapItemInfoEntered;

            // Handle the cancel event when the user closes the dialog without choosing to save
            _mapInfoUI.OnCanceled += SaveCanceled;

            // Add the map item info UI view (will display semi-transparent over the map view)
            View.Add(_mapInfoUI);
        }

        // Handle the OnMapInfoEntered event from the item input UI
        // MapSavedEventArgs contains the title, description, and tags that were entered
        private void MapItemInfoEntered(object sender, MapSavedEventArgs e)
        {
            try
            {
                
            }
            catch (Exception ex)
            {

            }
            finally
            {
                // Get rid of the item input controls
                _mapInfoUI.Hide();
                _mapInfoUI = null;
            }
        }

        private void SaveCanceled(object sender, EventArgs e)
        {
            // Remove the item input UI
            _mapInfoUI.Hide();
            _mapInfoUI = null;
        }

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
            Xamarin.Auth.OAuth2Authenticator auth = new OAuth2Authenticator(
                clientId: AppClientId,
                scope: "",
                authorizeUrl: new Uri(AuthorizeUrl),
                redirectUrl: new Uri(OAuthRedirectUrl));

            // Allow the user to cancel the OAuth attempt
            auth.AllowCancel = true;

            // Define a handler for the OAuth2Authenticator.Completed event
            auth.Completed += (sender, authArgs) =>
            {
                try
                {
                    // Dismiss the OAuth UI when complete
                    this.DismissViewController(true, null);

                    // Throw an exception if the user could not be authenticated
                    if (!authArgs.IsAuthenticated)
                    {
                        throw new Exception("Unable to authenticate user.");
                    }

                    // If authorization was successful, get the user's account
                    Xamarin.Auth.Account authenticatedAccount = authArgs.Account;
                  
                    // Set the result (Credential) for the TaskCompletionSource
                    _taskCompletionSource.SetResult(authenticatedAccount.Properties);
                }
                catch (Exception ex)
                {
                    // If authentication failed, set the exception on the TaskCompletionSource
                    _taskCompletionSource.SetException(ex);
                }
            };

            // If an error was encountered when authenticating, set the exception on the TaskCompletionSource
            auth.Error += (sndr, errArgs) =>
            {
                _taskCompletionSource.SetException(errArgs.Exception);
            };

            // Present the OAuth UI (on the app's UI thread) so the user can enter user name and password
            InvokeOnMainThread(() =>
            {
                this.PresentViewController(auth.GetUI(), true, null);
            });

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

    // View containing "save map" controls (title, description, and tags inputs with save/cancel buttons)
    public class SaveMapDialogOverlay : UIView
    {
        // Event to provide information the user entered when the user dismisses the view
        public event EventHandler<MapSavedEventArgs> OnMapInfoEntered;

        // Event to report that the save was canceled
        public event EventHandler OnCanceled;

        // Store the input controls so the values can be read
        private UITextField _titleTextField;
        private UITextField _descriptionTextField;
        private UITextField _tagsTextField;

        public SaveMapDialogOverlay(CoreGraphics.CGRect frame, nfloat transparency, UIColor color) : base(frame)
        {
            // Create a semi-transparent overlay with the specified background color
            BackgroundColor = color;
            Alpha = transparency;

            // Set size and spacing for controls
            nfloat controlHeight = 25;
            nfloat rowSpace = 11;
            nfloat buttonSpace = 15;
            nfloat textViewWidth = Frame.Width - 60;
            nfloat buttonWidth = 60;

            // Get the total height and width of the control set (four rows of controls, three sets of space)
            nfloat totalHeight = (4 * controlHeight) + (3 * rowSpace);
            nfloat totalWidth = textViewWidth;

            // Find the center x and y of the view
            nfloat centerX = Frame.Width / 2;
            nfloat centerY = Frame.Height / 2;

            // Find the start x and y for the control layout
            nfloat controlX = centerX - (totalWidth / 2);
            nfloat controlY = centerY - (totalHeight / 2);

            // Title text input
            _titleTextField = new UITextField(new CoreGraphics.CGRect(controlX, controlY, textViewWidth, controlHeight));
            _titleTextField.Placeholder = "Title";
            _titleTextField.AutocapitalizationType = UITextAutocapitalizationType.None;
            _titleTextField.BackgroundColor = UIColor.LightGray;

            // Adjust the Y position for the next control
            controlY = controlY + controlHeight + rowSpace;

            // Description text input
            _descriptionTextField = new UITextField(new CoreGraphics.CGRect(controlX, controlY, textViewWidth, controlHeight));
            _descriptionTextField.Placeholder = "Description";
            _descriptionTextField.AutocapitalizationType = UITextAutocapitalizationType.None;
            _descriptionTextField.BackgroundColor = UIColor.LightGray;

            // Adjust the Y position for the next control
            controlY = controlY + controlHeight + rowSpace;

            // Tags text input
            _tagsTextField = new UITextField(new CoreGraphics.CGRect(controlX, controlY, textViewWidth, controlHeight));
            _tagsTextField.Text = "ArcGIS Runtime, Web Map";
            _tagsTextField.AutocapitalizationType = UITextAutocapitalizationType.None;
            _tagsTextField.BackgroundColor = UIColor.LightGray;

            // Adjust the Y position for the next control
            controlY = controlY + controlHeight + rowSpace;

            // Button to save the map 
            UIButton saveButton = new UIButton(new CoreGraphics.CGRect(controlX, controlY, buttonWidth, controlHeight));
            saveButton.SetTitle("Save", UIControlState.Normal);
            saveButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            saveButton.TouchUpInside += SaveButtonClick;

            // Adjust the X position for the next control
            controlX = controlX + buttonWidth + buttonSpace;

            // Button to cancel the save
            UIButton cancelButton = new UIButton(new CoreGraphics.CGRect(controlX, controlY, buttonWidth, controlHeight));
            cancelButton.SetTitle("Cancel", UIControlState.Normal);
            cancelButton.SetTitleColor(UIColor.Blue, UIControlState.Normal);
            cancelButton.TouchUpInside += (s, e) => { OnCanceled.Invoke(this, null); };

            // Add the controls
            AddSubviews(_titleTextField, _descriptionTextField, _tagsTextField, saveButton, cancelButton);
        }

        // Animate increasing transparency to completely hide the view, then remove it
        public void Hide()
        {
            // Action to make the view transparent
            Action makeTransparentAction = () => Alpha = 0;

            // Action to remove the view
            Action removeViewAction = () => RemoveFromSuperview();

            // Time to complete the animation (seconds)
            double secondsToComplete = 0.75;

            // Animate transparency to zero, then remove the view
            Animate(secondsToComplete, makeTransparentAction, removeViewAction);
        }

        private void SaveButtonClick(object sender, EventArgs e)
        {
            // Get the values entered in the text fields
            var title = _titleTextField.Text.Trim();
            var description = _descriptionTextField.Text.Trim();
            var tags = _tagsTextField.Text.Split(',');

            // Make sure all required info was entered
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description) || tags.Length == 0)
            {
                new UIAlertView("Error", "Please enter a title, description, and some tags to describe the map.", null, "OK", null).Show();
                return;
            }

            // Fire the OnMapInfoEntered event and provide the map item values
            if (OnMapInfoEntered != null)
            {
                // Create a new MapSavedEventArgs to contain the user's values
                var mapSaveEventArgs = new MapSavedEventArgs(title, description, tags);

                // Raise the event
                OnMapInfoEntered(sender, mapSaveEventArgs);
            }
        }
    }

    // Custom EventArgs implementation to hold map item information (title, description, and tags)
    public class MapSavedEventArgs : EventArgs
    {
        // Title property
        public string Title { get; set; }

        // Description property
        public string Description { get; set; }

        // Tags property
        public string[] Tags { get; set; }

        // Store map item values passed into the constructor
        public MapSavedEventArgs(string title, string description, string[] tags)
        {
            Title = title;
            Description = description;
            Tags = tags;
        }
    }
}