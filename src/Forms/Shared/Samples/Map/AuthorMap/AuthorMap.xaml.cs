// Copyright 2016 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an 
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific 
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Portal;
using Esri.ArcGISRuntime.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace ArcGISRuntimeXamarin.Samples.AuthorMap
{
	public partial class AuthorMap : ContentPage
	{
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

        public AuthorMap ()
		{
            InitializeComponent ();

            Title = "Author and save a map";

            // call a function to initialize the app (display a map, etc.)
            Initialize();
		}

        private void Initialize()
        {
            // Call a function to create a new map with a light gray canvas basemap
            CreateNewMap();

            // Change the style of the layer list view for Android and UWP
            Device.OnPlatform(
                Android: () => 
                {
                    // Black background on Android (transparent by default)
                    LayersList.BackgroundColor = Color.Black;
                }, 
                WinPhone: () => 
                {
                    // Semi-transparent background on Windows with a small margin around the control
                    LayersList.BackgroundColor = Color.FromRgba(255, 255, 255, 0.3);
                    LayersList.Margin = new Thickness(50);
                });
        }

        private void LayerSelected(object sender, ItemTappedEventArgs e) 
        {
            // Handle the event when a layer item is selected (tapped) in the layer list
            var selectedItem = e.Item.ToString();
            
            // See if this is one of the layers in the operational layers list 
            if (_operationalLayerUrls.ContainsKey(selectedItem))
            { 
                // Get the service URL from the operational layers dictionary
                var value = _operationalLayerUrls[selectedItem];

                // Call a function to add the chosen operational layer
                AddLayer(selectedItem, value);
            }
            else
            {
                // Add the chosen basemap (replace the current one)
                AddBasemap(selectedItem);
            }
            
            // Hide the layer list
            LayersList.IsVisible = false;
        }

        private void ShowLayerList(object sender, EventArgs e)
        {
            // Clear the items currently shown in the list
            LayersList.ItemsSource = null;

            // See which button was used to show the list and fill it accordingly
            var button = sender as Button;
            if(button.Text == "Base map")
            {
                // Show the basemap list
                LayersList.ItemsSource = _basemapTypes;
            }
            else if (button.Text == "Layers")
            {
                // Show the operational layers list (names)
                LayersList.ItemsSource = _operationalLayerUrls.Keys;
            }

            // Show the layer list view control
            LayersList.IsVisible = true;
        }

        private async void ShowSaveMapUI(object sender, EventArgs e)
        {
            // Create a SaveMapPage page for getting user input for the new web map item
            var mapInputForm = new SaveMapPage();

            // Handle the save button click event on the page
            mapInputForm.OnSaveClicked += SaveMapAsync;

            // Navigate to the SaveMapPage UI
            // Note: in each platform's project, there is a custom PageRenderer class called SaveMapPage that provides
            //       platform-specific logic to challenge the user for OAuth credentials for ArcGIS Online when the page launches
            await Navigation.PushAsync(mapInputForm);
        }

        // Event handler to get information entered by the user and save the map
        private async void SaveMapAsync(object sender, OnSaveMapEventArgs e)
        {
            // Get the current map
            var myMap = MyMapView.Map;

            try
            {
                // Show the progress bar so the user knows work is happening
                SaveMapProgressBar.IsVisible = true;

                // Make sure the user is logged in to ArcGIS Online (should have been prompted when first clicking 'save')
                var cred = await EnsureLoggedInAsync();
                AuthenticationManager.Current.AddCredential(cred);

                // Get information entered by the user for the new portal item properties
                var title = e.MapTitle;
                var description = e.MapDescription;
                var tags = e.Tags;

                // Apply the current extent as the map's initial extent
                myMap.InitialViewpoint = MyMapView.GetCurrentViewpoint(ViewpointType.BoundingGeometry);

                // See if the map has already been saved (has an associated portal item)
                if (myMap.PortalItem == null)
                {
                    // Get the ArcGIS Online portal (will use credential from login above)
                    ArcGISPortal agsOnline = await ArcGISPortal.CreateAsync();

                    // Save the current state of the map as a portal item in the user's default folder
                    await myMap.SaveAsAsync(agsOnline, null, title, description, tags, null);

                    // Report a successful save
                    DisplayAlert("Map Saved","Saved '" + title + "' to ArcGIS Online!", "OK");
                }
                else
                {
                    // This is not the initial save, call SaveAsync to save changes to the existing portal item
                    await myMap.SaveAsync();

                    // Report update was successful
                    DisplayAlert("Updates Saved", "Saved changes to '" + myMap.PortalItem.Title + "'", "OK");
                }
            }
            catch (Exception ex)
            {
                // Show the exception message
                DisplayAlert("Unable to save map", ex.Message, "OK");
            }
            finally
            {
                // Hide the progress bar
                SaveMapProgressBar.IsVisible = false;                
            }
        }
        
        private async Task<Credential> EnsureLoggedInAsync()
        {
            // Challenge the user for portal credentials (OAuth credential request for arcgis.com)
            Credential cred = null;
            CredentialRequestInfo loginInfo = new CredentialRequestInfo();

            // Use the OAuth implicit grant flow
            loginInfo.GenerateTokenOptions = new GenerateTokenOptions
            {
                TokenAuthenticationType = TokenAuthenticationType.OAuthImplicit
            };

            // Indicate the url (portal) to authenticate with (ArcGIS Online)
            loginInfo.ServiceUri = new Uri("http://www.arcgis.com/sharing/rest");

            // Get the users credentials for ArcGIS Online (should have logged in when launching the page)
            cred = await AuthenticationManager.Current.GetCredentialAsync(loginInfo, false);

            return cred;
        }

        private void NewMapButtonClick(object sender, EventArgs e)
        {
            // Call a function to create a new map
            CreateNewMap();
        }

        private void CreateNewMap()
        {
            // Create new Map with a light gray canvas basemap
            var myMap = new Map(Basemap.CreateLightGrayCanvas());

            // Add the Map to the MapView
            MyMapView.Map = myMap;
        }

        private void AddBasemap(string basemapName)
        {
            // Apply the chosen basemap
            switch (basemapName)
            {
                case "Topographic":
                    // Set the basemap to Topographic
                    MyMapView.Map.Basemap = Basemap.CreateTopographic();
                    break;
                case "Streets":
                    // Set the basemap to Streets
                    MyMapView.Map.Basemap = Basemap.CreateStreets();
                    break;
                case "Imagery":
                    // Set the basemap to Imagery
                    MyMapView.Map.Basemap = Basemap.CreateImagery();
                    break;
                case "Oceans":
                    // Set the basemap to Oceans
                    MyMapView.Map.Basemap = Basemap.CreateOceans();
                    break;
            }
        }

        private void AddLayer(string layerName, string url)
        {
            // See if the layer already exists
            ArcGISMapImageLayer layer = MyMapView.Map.OperationalLayers.FirstOrDefault(l => l.Name == layerName) as ArcGISMapImageLayer;

            // If the layer is in the map, remove it
            if (layer != null)
            {
                MyMapView.Map.OperationalLayers.Remove(layer);
            }
            else
            {
                var layerUri = new Uri(url);

                // Create a new map image layer
                layer = new ArcGISMapImageLayer(layerUri);
                layer.Name = layerName;

                // Set it 50% opaque, and add it to the map
                layer.Opacity = 0.5;
                MyMapView.Map.OperationalLayers.Add(layer);
            }
        }
    }
}
