﻿/*
Copyright 2017 Alexis Ryan

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Connectivity;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Ao3TrackReader.UWP
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public static ushort UniversalApi { get; }

        static App()
        {
            ushort limit;
#if WINDOWS_FUTURE
            limit = 10;
#elif WINDOWS_16299
            limit = 5;
#elif WINDOWS_15063
            limit = 4;
#elif WINDOWS_14393
            limit = 3;
#elif WINDOWS_10586
            limit = 2;
#else
            limit = 1;
#endif
            for (var i = limit; i >=1; i++)
            {
                if (Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", i))
                {
                    UniversalApi = i;
                    break;
                }
            }

        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            UnhandledException += (sender, e) =>
            {
                Ao3TrackReader.App.Log(e.Exception);
#if DEBUG
                e.Handled = true;
#endif
            };

            switch (Ao3TrackReader.App.Theme) {
                case "light":
                    RequestedTheme = ApplicationTheme.Light;
                    break;

                case "dark":
                    RequestedTheme = ApplicationTheme.Dark;
                    break;
            }
        }

        internal Ao3TrackReader.App XApp { get; private set; }

        Frame CreateRootFrame(IActivatedEventArgs args)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            Uri uri = null;
            if (args.Kind == ActivationKind.Protocol)
            {
                var protocolArgs = (ProtocolActivatedEventArgs) args;
                uri = protocolArgs.Uri;                
            }

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                Xamarin.Forms.Forms.Init(args);

                ConnectionProfile connections = NetworkInformation.GetInternetConnectionProfile();

                XApp = new Ao3TrackReader.App(uri, connections != null && connections.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess);

                NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;

                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///MergeStyles.xaml") });

                if (UniversalApi <= 3)
                    Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///MergeStylesLeq3.xaml") });

                if (UniversalApi <= 2)
                    Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///MergeStylesLeq2.xaml") });

#if WINDOWS_16299
                if (UniversalApi >= 5)
                {
                    Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///MergeStylesGeq5.xaml") });
                }
#endif

                if (args.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }
            else
            {
                WebViewPage.Current.Navigate(uri, false);
            }

            return rootFrame;
        }

        private void NetworkInformation_NetworkStatusChanged(object sender)
        {
            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                ConnectionProfile connections = NetworkInformation.GetInternetConnectionProfile();
                XApp.HaveNetwork = connections != null && connections.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
            });
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif
            var rootFrame = CreateRootFrame(args);

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), args);
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }


        protected override void OnActivated(IActivatedEventArgs args)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif
            var rootFrame = CreateRootFrame(args);

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), args);
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
            deferral.Complete();
        }
    }
}
