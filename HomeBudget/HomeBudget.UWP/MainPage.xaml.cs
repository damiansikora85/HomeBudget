﻿using HomeBudget.Code;
using HomeBudget.UWP.Pages;
using HomeBudget.UWP.Utils;
using HomeBudgetShared.Code.Synchronize;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace HomeBudget.UWP
{
    public sealed partial class MainPage
    {
        public MainPage()
        {
            InitializeComponent();

            ContentFrame.Content = new ProgressRing { IsActive = true };

            if (!MainBudget.Instance.IsDataLoaded)
                InitBudget();
            else
                OnBudgetLoaded();
        }

        private void InitBudget()
        {
            MainBudget.Instance.BudgetDataChanged += OnBudgetLoaded;
            MainBudget.Instance.Init(new FileManagerUwp(), new BudgetSynchronizer(new DropboxCloudStorage()));
        }

        private void OnBudgetLoaded()
        {
            Task.Run(async () =>
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => 
                {
                    foreach (NavigationViewItemBase item in NavView.MenuItems)
                    {
                        if (item is NavigationViewItem && item.Tag.ToString() == "home")
                        {
                            NavView.SelectedItem = item;
                            break;
                        }
                    }
                }));
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void NavView_SelectionChanged(NavigationView view, NavigationViewSelectionChangedEventArgs args)
        {
            if(args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.SelectedItem is NavigationViewItem item)
            {
                switch (item.Tag)
                {
                    case "home":
                        ContentFrame.Navigate(typeof(HomePage));
                        break;
                    case "analytics":
                        ContentFrame.Navigate(typeof(AnalyticsPage));
                        break;
                    case "plan":
                        ContentFrame.Navigate(typeof(PlanPage));
                        break;
                    case "logs":
                        ContentFrame.Navigate(typeof(LogsPage));
                        break;
                }
            }
        }

        private void HamburgerButton_Click(object obj, RoutedEventArgs args)
        {
            
        }

        private void OnDropboxClick(object obj, RoutedEventArgs args)
        {
            
        }

        private void OnDropboxClicked(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            
            NavView.SelectedItem = sender;
            ContentFrame.Navigate(typeof(DropboxPage));
        }
    }
}
