//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AppUIBasics.Common;
using AppUIBasics.Data;
using AppUIBasics.Helper;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Metadata;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace AppUIBasics
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {

        /// <summary>
        /// 初始化单例应用对象。等同于Main函数。        
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += App_Resuming;
            this.RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 6))
            {
                this.FocusVisualKind = AnalyticsInfo.VersionInfo.DeviceFamily == "Xbox" ? FocusVisualKind.Reveal : FocusVisualKind.HighVisibility;
            }
        }

        public void EnableSound(bool withSpatial = false)
        {
            ElementSoundPlayer.State = ElementSoundPlayerState.On;

            if (!withSpatial)
                ElementSoundPlayer.SpatialAudioMode = ElementSpatialAudioMode.Off;
            else
                ElementSoundPlayer.SpatialAudioMode = ElementSpatialAudioMode.On;
        }

        public static TEnum GetEnum<TEnum>(string text) where TEnum : struct
        {
            if (!typeof(TEnum).GetTypeInfo().IsEnum)
            {
                throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
            }
            return (TEnum)Enum.Parse(typeof(TEnum), text);
        }

        private async void App_Resuming(object sender, object e)
        {
            // We are being resumed, so lets restore our state!
            try
            {
                await SuspensionManager.RestoreAsync();
            }
            finally
            {
                switch (NavigationRootPage.RootFrame?.Content)
                {
                    case ItemPage itemPage:
                        itemPage.SetInitialVisuals();
                        break;
                    case NewControlsPage _:
                    case AllControlsPage _:
                        NavigationRootPage.Current.NavigationView.AlwaysShowHeader = false;
                        break;
                }
            }

        }

        /// <summary>
        /// 最终用户正常启动应用程序时调用。也会被其他入口点调用，如启动应用程序以打开特定文件时。
        /// </summary>
        /// <param name="e">有关启动请求和流程的详细信息 .</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            //在标题栏中绘制
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            await EnsureWindow(args);
        }

        private void DebugSettings_BindingFailed(object sender, BindingFailedEventArgs e)
        {

        }

        protected async override void OnActivated(IActivatedEventArgs args)
        {
            await EnsureWindow(args);

            base.OnActivated(args);
        }

        /// <summary>
        /// 启动窗体
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private async Task EnsureWindow(IActivatedEventArgs args)
        {
            // 无论要达成什么目的，我们都需要加载控件数据 - 让我们现在解决这个问题.
            // 只运行一次就行了.
            await ControlInfoDataSource.Instance.GetGroupsAsync();

            Frame rootFrame = GetRootFrame();

            ThemeHelper.Initialize();

            if (args.PreviousExecutionState == ApplicationExecutionState.Terminated
                    || args.PreviousExecutionState == ApplicationExecutionState.Suspended)
            {
                try
                {
                    await SuspensionManager.RestoreAsync();
                }
                catch (SuspensionManagerException)
                {
                    //Something went wrong restoring state.
                    //Assume there is no state and continue
                }

                Window.Current.Activate();

                UpdateNavigationBasedOnSelectedPage(rootFrame);
                return;
            }

            Type targetPageType = typeof(NewControlsPage);
            string targetPageArguments = string.Empty;

            if (args.Kind == ActivationKind.Launch)
            {
                targetPageArguments = ((LaunchActivatedEventArgs)args).Arguments;
            }
            else if (args.Kind == ActivationKind.Protocol)
            {
                Match match;

                string targetId = string.Empty;

                switch (((ProtocolActivatedEventArgs)args).Uri?.AbsoluteUri)
                {
                    case string s when IsMatching(s, "(/*)category/(.*)"):
                        targetId = match.Groups[2]?.ToString();
                        if (targetId == "AllControls")
                        {
                            targetPageType = typeof(AllControlsPage);
                        }
                        else if (targetId == "NewControls")
                        {
                            targetPageType = typeof(NewControlsPage);
                        }
                        else if (ControlInfoDataSource.Instance.Groups.Any(g => g.UniqueId == targetId))
                        {
                            targetPageType = typeof(SectionPage);
                        }
                        break;

                    case string s when IsMatching(s, "(/*)item/(.*)"):
                        targetId = match.Groups[2]?.ToString();
                        if (ControlInfoDataSource.Instance.Groups.Any(g => g.Items.Any(i => i.UniqueId == targetId)))
                        {
                            targetPageType = typeof(ItemPage);
                        }
                        break;
                }

                targetPageArguments = targetId;

                bool IsMatching(string parent, string expression)
                {
                    match = Regex.Match(parent, expression);
                    return match.Success;
                }
            }

            rootFrame.Navigate(targetPageType, targetPageArguments);

            if (targetPageType == typeof(NewControlsPage))
            {
                ((Microsoft.UI.Xaml.Controls.NavigationViewItem)((NavigationRootPage)Window.Current.Content).NavigationView.MenuItems[0]).IsSelected = true;
            }
            else if (targetPageType == typeof(ItemPage))
            {
                NavigationRootPage.Current.EnsureNavigationSelection(targetPageArguments);
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }

        private static void UpdateNavigationBasedOnSelectedPage(Frame rootFrame)
        {
            // Check if we brought back an ItemPage
            if (rootFrame.Content is ItemPage itemPage)
            {
                // We did, so bring the selected item back into view
                string name = itemPage.Item.Title;
                if (Window.Current.Content is NavigationRootPage nav)
                {
                    // Finally brings back into view the correct item.
                    // But first: Update page layout!
                    nav.EnsureItemIsVisibleInNavigation(name);
                }
            }
        }

        private Frame GetRootFrame()
        {
            Frame rootFrame;
            if (!(Window.Current.Content is NavigationRootPage rootPage))
            {
                rootPage = new NavigationRootPage();
                rootFrame = (Frame)rootPage.FindName("rootFrame");
                if (rootFrame == null)
                {
                    throw new Exception("Root frame not found");
                }
                SuspensionManager.RegisterFrame(rootFrame, "AppFrame");
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
                rootFrame.NavigationFailed += OnNavigationFailed;

                Window.Current.Content = rootPage;
            }
            else
            {
                rootFrame = (Frame)rootPage.FindName("rootFrame");
            }

            return rootFrame;
        }

        /// <summary>
        /// 当导航到某个页面失败时调用 
        /// </summary>
        /// <param name="sender">导航失败的框架</param>
        /// <param name="e">有关导航失败的详细信息</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("加载" + e.SourcePageType.FullName + "页面失败");
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            await SuspensionManager.SaveAsync();
            UpdateNavigationBasedOnSelectedPage(GetRootFrame());
            deferral.Complete();
        }
    }
}
