﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace Ao3TrackReader.Controls
{
	[Xamarin.Forms.ContentProperty(nameof(Tabs))]
	public partial class TabbedLayout : ContentView, IViewContainer<TabView>
    {
        public static readonly BindableProperty SpacingProperty = BindableProperty.Create(nameof(Spacing), typeof(double), typeof(TabbedLayout), defaultValue: -1.0,
            propertyChanged: (obj, o, n) => (obj as TabbedLayout).SpacingPropertyChanged((double)o, (double)n));

        public IList<TabView> Tabs => new ListConverter<TabView, View>(TabsContainer.Children);
        IList<TabView> IViewContainer<TabView>.Children => Tabs;

        public double Spacing
        {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        int currentTabIndex = 0;
        TabView currentTab = null;
        double tabWidth = 400;

        public TabbedLayout ()
		{
			InitializeComponent ();
        }

        private void ButtonsScroll_Scrolled(object sender, ScrolledEventArgs e)
        {
            ButtonsLeft.IsEnabled = e.ScrollX > 0;
            ButtonsRight.IsEnabled = e.ScrollX < (ButtonsScroll.ContentSize.Width - ButtonsScroll.Width);
        }

        private void ButtonsScroll_SizeChanged(object sender, EventArgs e)
        {
            bool show = ButtonsScroll.Width < ButtonsScroll.ContentSize.Width;
            ButtonsRight.IsVisible = show;
            ButtonsLeft.IsVisible = show;

            ButtonsLeft.IsEnabled = ButtonsScroll.ScrollX > 0;
            ButtonsRight.IsEnabled = ButtonsScroll.ScrollX < (ButtonsScroll.ContentSize.Width - ButtonsScroll.Width);
        }

        private void ButtonsRight_Pressed(object sender, EventArgs e)
        {

            Device.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await ButtonsScroll.ScrollToAsync(Math.Min(ButtonsScroll.ScrollX + 120, ButtonsScroll.ContentSize.Width - ButtonsScroll.Width), 0, true);
                }
                catch (Exception exp)
                {
                    App.Log(exp);
                }
            });
        }

        private void ButtonsLeft_Pressed(object sender, EventArgs e)
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await ButtonsScroll.ScrollToAsync(Math.Max(ButtonsScroll.ScrollX - 120, 0), 0, true);
                }
                catch (Exception exp)
                {
                    App.Log(exp);
                }
            });
        }

        private void Scroll_SizeChanged(object sender, EventArgs e)
        {
            tabWidth = TabsScroll.Width;
            foreach (TabView tab in Tabs)
            {
                tab.WidthRequest = tabWidth;
            }
            if (currentTab != null)
            {
                Device.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await TabsScroll.ScrollToAsync(currentTab, ScrollToPosition.Start, false);
                    }
                    catch (Exception exp)
                    {
                        App.Log(exp);
                    }
                });
            }
        }

        private int TabFromX(double x)
        {
            x += tabWidth / 2;
            for (int i = 0; i < TabsContainer.Children.Count; i++)
            {
                var tab = TabsContainer.Children[i];
                if ((tab.X + tab.Width) >= x)
                    return i;
            }
            return 0;
        }

        public int CurrentTab
        {
            get => currentTabIndex;

            set
            {
                if (value >= TabsContainer.Children.Count) value = TabsContainer.Children.Count - 1;
                if (value < 0) value = 0;

                PickerTabs.SelectedIndex = value;

                var newTab = value < TabsContainer.Children.Count ? TabsContainer.Children[value] as TabView : null;

                double desiredScroll = newTab?.X ?? 0.0;
                if (desiredScroll != Math.Floor(TabsScroll.ScrollX) && desiredScroll != Math.Round(TabsScroll.ScrollX, MidpointRounding.AwayFromZero))
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            await TabsScroll.ScrollToAsync(desiredScroll, 0, true);
                        }
                        catch (Exception exp)
                        {
                            App.Log(exp);
                        }
                    });
                }
            }
        }

        private void Scroll_ScrollEnd(object sender, EventArgs e)
        {
            // Snap scrolling to a tab point
            CurrentTab = TabFromX(TabsScroll.ScrollX);
        }

        private void Scroll_Scrolled(object sender, ScrolledEventArgs e)
        {
            // Change active tab as we scroll over them
            PickerTabs.SelectedIndex = currentTabIndex = TabFromX(TabsScroll.ScrollX);
            var oldCurrent = currentTab;
            currentTab = currentTabIndex < TabsContainer.Children.Count ? TabsContainer.Children[currentTabIndex] as TabView : null;

            for (int i = 0; i < ButtonsContainer.Children.Count; i++)
            {
                var button = ButtonsContainer.Children[i] as Button;
                if (i == currentTabIndex)
                {
                    button.IsEnabled = false;
                    button.IsActive = true;
                    if (oldCurrent != currentTab)
                    {
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            try
                            {
                                await ButtonsScroll.ScrollToAsync(button, ScrollToPosition.Center, true);
                            }
                            catch (Exception exp)
                            {
                                App.Log(exp);
                            }
                        });
                    }
                }
                else
                {

                    button.IsActive = false;
                    button.IsEnabled = true;
                }
            }
        }

        private void Tabs_ChildAdded(object sender, ElementEventArgs e)
        {
            TabView tab = e.Element as TabView;
            tab.WidthRequest = tabWidth;
            tab.PropertyChanged += Tab_PropertyChanged;

            var button = CreateButton();
            button.BindingContext = tab;
            var index = TabsContainer.Children.IndexOf(tab);
            ButtonsContainer.Children.Insert(index, button);
            PickerTabs.Items.Insert(index, tab.Title);

            if (TabsContainer.Children.Count == 1)
            {
                PickerTabs.SelectedIndex = currentTabIndex = 0;
                currentTab = tab;
                button.IsEnabled = false;
                button.IsActive = true;
            }
            else
            {
                PickerTabs.SelectedIndex = currentTabIndex = TabsContainer.Children.IndexOf(currentTab);
            }
        }

        private void Tab_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == VisualElement.XProperty.PropertyName)
            {
                TabView tab = sender as TabView;
                if (tab == currentTab)
                {
                    PickerTabs.SelectedIndex = currentTabIndex = TabsContainer.Children.IndexOf(tab);
                    double desiredScroll = currentTab.X;
                    if (desiredScroll != TabsScroll.ScrollX)
                    {
                        TabsScroll.ForceLayout();
                        Device.BeginInvokeOnMainThread(async () => 
                        {
                            await Task.Delay(1);
                            try
                            {
                                await TabsScroll.ScrollToAsync(currentTab, ScrollToPosition.Start, false);
                            }
                            catch(Exception exp)
                            {
                                App.Log(exp);
                            }
                        });
                    }
                }
            }
        }

        private void Tabs_ChildRemoved(object sender, ElementEventArgs e)
        {
            TabView tab = e.Element as TabView;
            tab.PropertyChanged -= Tab_PropertyChanged;

            // Remove the button
            for (int i = 0; i < ButtonsContainer.Children.Count; i++)
            {
                if (ButtonsContainer.Children[i].BindingContext == e.Element)
                {
                    ButtonsContainer.Children.RemoveAt(i);
                    PickerTabs.Items.RemoveAt(i);
                    break;
                }
            }

            int newCurrent = currentTabIndex;
            if (currentTab == e.Element)
            {
                if (newCurrent >= TabsContainer.Children.Count) newCurrent--;
            }
            else
            {
                newCurrent = TabsContainer.Children.IndexOf(currentTab);
            }
            CurrentTab = newCurrent;
        }

        private void Tabs_ChildrenReordered(object sender, EventArgs e)
        {
            ButtonsContainer.Children.Clear();
            PickerTabs.Items.Clear();

            int newCurrent = 0;
            for (int i = 0; i < TabsContainer.Children.Count; i++)
            {
                var tab = TabsContainer.Children[i] as TabView;

                var button = CreateButton();
                button.BindingContext = tab;
                ButtonsContainer.Children.Add(button);
                PickerTabs.Items.Add(tab.Title);
                if (tab == currentTab)
                {
                    button.IsEnabled = false;
                    button.IsActive = true;
                    newCurrent = i;
                }
            }

            PickerTabs.SelectedIndex = currentTabIndex = newCurrent;
            if (currentTab != null)
            {
                Device.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await TabsScroll.ScrollToAsync(currentTab, ScrollToPosition.Start, false);
                    }
                    catch (Exception exp)
                    {
                        App.Log(exp);
                    }
                });
            }
        }

        Button CreateButton()
        {
            Button button = new Button { Style = TabbedLayoutButtonStyle };
            button.Clicked += Button_Clicked;
            return button;
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            for (int i = 0; i < TabsContainer.Children.Count; i++)
            {
                if (TabsContainer.Children[i] == button.BindingContext)
                {
                    CurrentTab = i;
                    break;
                }
            }
        }

        public void MoveTab(TabView tab, int index)
        {
            if (index < 0) throw new ArgumentException();
            if (index > TabsContainer.Children.Count) throw new ArgumentException();

            if (index < TabsContainer.Children.Count)
            {
                var layoutController = TabsContainer as ILayoutController;
                if (layoutController.Children is ObservableCollection<Element> obvcol)
                {
                    if (obvcol.Contains(tab))
                    {
                        int oldIndex = obvcol.IndexOf(tab);
                        if (oldIndex != index) obvcol.Move(oldIndex, index);
                        return;
                    }
                }
                else if (TabsContainer.Children.Contains(tab))
                {
                    int oldIndex = TabsContainer.Children.IndexOf(tab);
                    if (oldIndex < index) {
                        TabsContainer.RaiseChild(tab);
                        oldIndex = TabsContainer.Children.Count - 1;
                    }

                    while (index < oldIndex)
                    {
                        TabsContainer.RaiseChild(TabsContainer.Children[index]);
                        oldIndex--;
                    }
                    return;
                }
            }
            TabsContainer.Children.Insert(index, tab);
        }

        private void SpacingPropertyChanged(double oldValue, double newValue)
        {
            if (newValue == -1) OuterLayout.ClearValue(StackLayout.SpacingProperty);
            else OuterLayout.Spacing = newValue;
        }

        private void PickerTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (PickerTabs.SelectedIndex != CurrentTab)
            {
                CurrentTab = PickerTabs.SelectedIndex;
            }
        }
    }
}
