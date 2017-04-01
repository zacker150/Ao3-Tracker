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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Forms;
using Ao3TrackReader.Resources;

namespace Ao3TrackReader.Controls
{
    public partial class ReadingListView : PaneView
    {
        GroupList<Models.Ao3PageViewModel> readingListBacking;
        SemaphoreSlim RefreshSemaphore = new SemaphoreSlim(20);

        public ReadingListView()
        {
            AddToReadingListCommand = new DisableableCommand(() => AddAsync(wvp.CurrentUri.AbsoluteUri));

            InitializeComponent();

            readingListBacking = new GroupList<Models.Ao3PageViewModel>();

            ShowHiddenButton.BackgroundColor = readingListBacking.ShowHidden ? Colors.Highlight.Trans.Medium : Color.Transparent;
            ShowTagsButton.BackgroundColor = TagsVisible ? Colors.Highlight.Trans.Medium : Color.Transparent;
        }

        protected override void OnWebViewPageSet()
        {
            base.OnWebViewPageSet();
            Device.BeginInvokeOnMainThread(async () => await RestoreReadingList().ConfigureAwait(false) );
        }

        async Task RestoreReadingList()
        {
            // Restore the reading list contents!
            var items = new Dictionary<string, Models.ReadingList>();
            foreach (var i in App.Database.GetReadingListItems())
            {
                items[i.Uri] = i;
            }

            List<Task> tasks = new List<Task>();
            var vms = new List<Models.Ao3PageViewModel>();

            if (items.Count == 0)
            {
                tasks.Add(AddAsync("http://archiveofourown.org/"));
            }
            else
            {
                var timestamp = DateTime.UtcNow.ToUnixTime();
                var models = Data.Ao3SiteDataLookup.LookupQuick(items.Keys);
                foreach (var model in models)
                {
                    if (model.Value != null)
                    {
                        var item = items[model.Key];
                        if (string.IsNullOrWhiteSpace(model.Value.Title) || model.Value.Type == Models.Ao3PageType.Work)
                            model.Value.Title = item.Title;
                        if (string.IsNullOrWhiteSpace(model.Value.PrimaryTag) || model.Value.PrimaryTag.StartsWith("<"))
                        {
                            model.Value.PrimaryTag = item.PrimaryTag;
                            var tagdata = Data.Ao3SiteDataLookup.LookupTagQuick(item.PrimaryTag);
                            if (tagdata != null) model.Value.PrimaryTagType = Data.Ao3SiteDataLookup.GetTypeForCategory(tagdata.category);
                            else model.Value.PrimaryTagType = Models.Ao3TagType.Other;
                        }
                        if (model.Value.Details != null && model.Value.Details.Summary == null && !string.IsNullOrEmpty(item.Summary))
                            model.Value.Details.Summary = item.Summary;

                        if (model.Value.Uri.AbsoluteUri != model.Key)
                        {
                            App.Database.DeleteReadingListItems(model.Key);
                            App.Database.SaveReadingListItems(new Models.ReadingList { Uri = model.Value.Uri.AbsoluteUri, PrimaryTag = model.Value.PrimaryTag, Title = model.Value.Title, Timestamp = timestamp, Unread = item.Unread });
                        }

                        if (readingListBacking.FindInAll((m) => m.Uri.AbsoluteUri == model.Value.Uri.AbsoluteUri) == null)
                        {
                            var viewmodel = new Models.Ao3PageViewModel(model.Value, item.Unread)
                            {
                                TagsVisible = tags_visible
                            };
                            viewmodel.PropertyChanged += Viewmodel_PropertyChanged;
                            readingListBacking.Add(viewmodel);
                            vms.Add(viewmodel);
                        }
                    }
                }
            }
            SyncToServerAsync(false);

            wvp.DoOnMainThread(() =>
            {
                ListView.ItemsSource = readingListBacking;
                App.Database.GetVariableEvents("LogFontSizeUI").Updated += LogFontSizeUI_Updated;
            });

            foreach (var viewmodel in vms)
            {
                await RefreshSemaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    await RefreshAsync(viewmodel);
                    RefreshSemaphore.Release();
                }));

            }
            await Task.WhenAll(tasks);

            wvp.DoOnMainThread(() =>
            {
                RefreshButton.IsEnabled = true;
                SyncIndicator.IsRunning = false;
                SyncIndicator.IsVisible = false;
            });
        }

        private void LogFontSizeUI_Updated(object sender, Ao3TrackDatabase.VariableUpdatedEventArgs e)
        {
            wvp.DoOnMainThread(() =>
            {
                ListView.ItemsSource = null;
                ListView.ItemsSource = readingListBacking;
            });
        }

        protected override void OnIsOnScreenChanging(bool newValue)
        {
            if (newValue == true)
            {
                ListView.Focus();
            }
            else
            {
                ListView.Unfocus();
            }
        }

        public DisableableCommand AddToReadingListCommand { get; private set; }


        IEnumerable<Models.IHelpInfo> ButtonBarHelpItems
        {
            get
            {
                foreach (var v in buttonBar.Children)
                {
                    if (v is Models.IHelpInfo info)
                    {
                        if (!string.IsNullOrWhiteSpace(info.Text) && !string.IsNullOrWhiteSpace(info.Group))
                            yield return new Models.HelpInfoAdapter(info);
                    }
                }
            }
        }

        public IEnumerable<Models.IHelpInfo> HelpItems {
            get
            {
                return ExtraHelp.Concat(ButtonBarHelpItems);
            }
        }

        Models.Ao3PageViewModel selectedItem;
        private void UpdateSelectedItem(Models.Ao3PageViewModel newselected)
        {
            if (selectedItem != null && newselected != selectedItem) selectedItem.IsSelected = false;
            selectedItem = null;
            ListView.SelectedItem = null;

            if (newselected != null)
            {
                newselected.IsSelected = true;
                selectedItem = newselected;
            }
            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                if (selectedItem?.IsSelected != false && ListView.SelectedItem != selectedItem)
                {
                    try
                    {
                        ListView.SelectedItem = selectedItem;
                    }
                    catch
                    {
                    }
                }
            });
        }

        private void OnCellAppearing(object sender, EventArgs e)
        {
            var mi = ((Cell)sender);
            var item = mi.BindingContext as Models.Ao3PageViewModel;
            if (item?.IsSelected == true)
            {
                UpdateSelectedItem(item);
            }
        }

        private void OnCellTapped(object sender, EventArgs e)
        {
            var mi = ((Cell)sender);
            if (mi.BindingContext is Models.Ao3PageViewModel item)
            {
                Goto(item,true,false);
            }
        }

        private void OnMenuOpen(object sender, EventArgs e)
        {
            var mi = ((MenuItem)sender);
            if (mi.CommandParameter is Models.Ao3PageViewModel item)
            {
                Goto(item,false,false);
            }
        }

        private void OnMenuRefresh(object sender, EventArgs e)
        {
            var mi = ((MenuItem)sender);
            if (mi.CommandParameter is Models.Ao3PageViewModel item)
            {
                RefreshAsync(item);
            }

        }

        private void OnMenuDelete(object sender, EventArgs e)
        {
            var mi = ((MenuItem)sender);
            if (mi.CommandParameter is Models.Ao3PageViewModel item)
            {
                RemoveAsync(item.Uri.AbsoluteUri);
            }

        }

        private void OnRefresh(object sender, EventArgs e)
        {
            ListView.Focus();
            RefreshButton.IsEnabled = false;
            SyncIndicator.IsRunning = true;
            SyncIndicator.IsVisible = true;
            Task.Run(async () =>
            {
                List<Task> tasks = new List<Task>();
                foreach (var viewmodel in readingListBacking.AllSafe)
                {
                    await RefreshSemaphore.WaitAsync();

                    tasks.Add(Task.Run(async () => {
                        await RefreshAsync(viewmodel);
                        RefreshSemaphore.Release();
                    }));
                }

                await Task.WhenAll(tasks);

                SyncToServerAsync(false);

                wvp.DoOnMainThread(() =>
                {
                    RefreshButton.IsEnabled = true;
                    SyncIndicator.IsRunning = false;
                    SyncIndicator.IsVisible = false;
                    PageChange(wvp.CurrentUri);
                });
            });
        }

        private void OnShowHidden(object sender, EventArgs e)
        {
            readingListBacking.ShowHidden = !readingListBacking.ShowHidden;
            ShowHiddenButton.BackgroundColor = readingListBacking.ShowHidden ? Colors.Highlight.Trans.Medium : Color.Transparent;
        }

        bool tags_visible = false;
        public bool TagsVisible
        {
            get { return tags_visible; }
            set
            {
                if (tags_visible != value)
                {
                    tags_visible = value;
                    readingListBacking.ForEachInAll((item) => { item.TagsVisible = tags_visible; });
                }
            }
        }

        private void OnShowTags(object sender, EventArgs e)
        {
            TagsVisible = !TagsVisible;
            ShowTagsButton.BackgroundColor = TagsVisible ? Colors.Highlight.Trans.Medium : Color.Transparent;
        }

        public void Goto(Models.Ao3PageViewModel item, bool latest, bool fullwork)
        {
            if (item.BaseData.Type == Models.Ao3PageType.Work && item.BaseData.Details != null && item.BaseData.Details.WorkId != 0)
            {
                if (latest) wvp.NavigateToLast(item.BaseData.Details.WorkId, fullwork);
                else wvp.Navigate(item.BaseData.Details.WorkId, fullwork);
            }
            else if (latest && item.BaseData.Type == Models.Ao3PageType.Series && item.BaseData.SeriesWorks?.Count > 0)
            {
                long workid = 0;

                foreach (var workmodel in item.BaseData.SeriesWorks)
                {
                    workid = workmodel.Details.WorkId;
                    int chapters_finished = 0;
                    if (item.WorkChapters.TryGetValue(workid, out var workchap))
                    {
                        chapters_finished = (int)workchap.number;
                        if (workchap.location != null) { chapters_finished--; }
                    }
                    if (chapters_finished < workmodel.Details.Chapters.Available) break;
                }

                if (workid != 0) wvp.NavigateToLast(workid,false);
                else wvp.Navigate(item.Uri);
            }
            else
            {
                wvp.Navigate(item.Uri);
            }
            IsOnScreen = false;
        }

        public void PageChange(Uri uri)
        {
            uri = Data.Ao3SiteDataLookup.ReadingListlUri(uri.AbsoluteUri);
            wvp.DoOnMainThread(() => 
            {
                if (uri == null)
                {
                    UpdateSelectedItem(null);
                    AddToReadingListCommand.IsEnabled = false;
                }
                else
                {
                    var item = readingListBacking.FindInAll((m) => m.HasUri(uri));
                    UpdateSelectedItem(item);
                    AddToReadingListCommand.IsEnabled = item == null;
                }
            });
        }

        public async void SyncToServerAsync(bool newuser)
        {
            var srl = new Models.ServerReadingList();
            if (!newuser && App.Database.TryGetVariable("ReadingList.last_sync", long.TryParse, out long last)) srl.last_sync = last;

            srl.paths = App.Database.GetReadingListItems().ToDictionary(i => i.Uri, i => i.Timestamp);

            srl = await App.Storage.SyncReadingListAsync(srl);
            if (srl != null)
            {
                List<Task> tasks = new List<Task>();
                foreach (var item in srl.paths)
                {
                    await RefreshSemaphore.WaitAsync();

                    tasks.Add(Task.Run(async () => {
                        if (item.Value == -1)
                        {
                            await RemoveAsyncImpl(item.Key);
                        }
                        else
                        {
                            AddAsyncImpl(item.Key, item.Value);
                        }
                        RefreshSemaphore.Release();
                    }));

                }
                await Task.WhenAll(tasks);
                App.Database.SaveVariable("ReadingList.last_sync", srl.last_sync.ToString());
            }
            PageChange(wvp.CurrentUri);
        }

        async Task RemoveAsyncImpl(string href)
        {
            App.Database.DeleteReadingListItems(href);
            var viewmodel = readingListBacking.FindInAll((m) => m.Uri.AbsoluteUri == href);
            if (viewmodel == null) return;
            viewmodel.PropertyChanged -= Viewmodel_PropertyChanged;
            await wvp.DoOnMainThreadAsync(() =>
            {
                if (viewmodel == selectedItem) UpdateSelectedItem(null);
                var uri = Data.Ao3SiteDataLookup.ReadingListlUri(wvp.CurrentUri.AbsoluteUri);
                if (uri == viewmodel.Uri) AddToReadingListCommand.IsEnabled = true;
                readingListBacking.Remove(viewmodel);
                viewmodel.Dispose();
            });
        }

        public Task RemoveAsync(string href)
        {
            return Task.Run(async () => { 
                await RemoveAsyncImpl(href);
                SyncToServerAsync(false);
            });
        }

        void AddAsyncImpl(string href, long timestamp)
        {
            if (readingListBacking.FindInAll((m) => m.Uri.AbsoluteUri == href) != null)
                return;

            var models = Data.Ao3SiteDataLookup.LookupQuick(new[] { href });
            var model = models.SingleOrDefault();
            if (model.Value == null) return;

            if (readingListBacking.FindInAll((m) => m.Uri.AbsoluteUri == model.Value.Uri.AbsoluteUri) != null)
                return;

            App.Database.SaveReadingListItems(new Models.ReadingList { Uri = model.Value.Uri.AbsoluteUri, PrimaryTag = model.Value.PrimaryTag, Title = model.Value.Title, Timestamp = timestamp, Unread = null });
            wvp.DoOnMainThread(() =>
            {
                var viewmodel = new Models.Ao3PageViewModel(model.Value, 0) // Set unread to 0. this is to prevents UI locks when importing huge reading lists during syncs
                {
                    TagsVisible = tags_visible
                };
                viewmodel.PropertyChanged += Viewmodel_PropertyChanged;
                readingListBacking.Add(viewmodel);

                var uri = Data.Ao3SiteDataLookup.ReadingListlUri(wvp.CurrentUri.AbsoluteUri);
                if (uri == viewmodel.Uri) AddToReadingListCommand.IsEnabled = false;

                Task.Run(async () =>
                {
                    await RefreshSemaphore.WaitAsync();
                    await RefreshAsync(viewmodel);
                    RefreshSemaphore.Release();
                });
            });
        }

        public async Task<IDictionary<string, bool>> AreUrlsInListAsync(string[] urls)
        {
            return await Task.Run(() =>
            {
                var urlmap = new List<KeyValuePair<string, Uri>>();
                IDictionary<string, bool> result = new Dictionary<string, bool>();

                foreach (var url in urls)
                {
                    var uri = Data.Ao3SiteDataLookup.ReadingListlUri(url);
                    if (uri != null) urlmap.Add(new KeyValuePair<string, Uri>(url,uri));
                    result[url] = false;
                }

                foreach (var m in readingListBacking.AllSafe)
                {
                    for (var i = 0; i < urlmap.Count; i++)
                    {
                        var kvp = urlmap[i];
                        if (m.HasUri(kvp.Value))
                        {
                            result[kvp.Key] = true;
                            urlmap.RemoveAt(i);
                            i--;
                        }
                    }
                    if (urlmap.Count == 0)
                        break;
                }

                return result;
            });
        }

        private void Viewmodel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var viewmodel = (Models.Ao3PageViewModel)sender;
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Unread" || e.PropertyName == "Summary")
            {
                bool changed = false;
                var dbentry = App.Database.GetReadingListItem(viewmodel.Uri.AbsoluteUri);
                if (dbentry != null)
                {
                    if (dbentry.Unread != viewmodel.Unread)
                    {
                        changed = true;
                        dbentry.Unread = viewmodel.Unread;
                    }

                    if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Summary")
                    {
                        string summary = viewmodel.Summary?.ToString();
                        if (summary != null && dbentry.Summary != summary)
                        {
                            changed = true;
                            dbentry.Summary = summary;
                        }
                    }

                    if (changed) App.Database.SaveReadingListItems(dbentry);
                }
            }
        }

        public Task AddAsync(string href)
        {
            return Task.Run(() =>
            {
                AddAsyncImpl(href, DateTime.UtcNow.ToUnixTime());
                SyncToServerAsync(false);
            });
        }

        public Task RefreshAsync(Models.Ao3PageViewModel viewmodel)
        {
            return Task.Run(async () =>
            {
                var models = await Data.Ao3SiteDataLookup.LookupAsync(new[] { viewmodel.Uri.AbsoluteUri });

                var model = models.SingleOrDefault();
                if (model.Value != null)
                {
                    if (viewmodel.Uri.AbsoluteUri != model.Value.Uri.AbsoluteUri)
                    {
                        App.Database.DeleteReadingListItems(viewmodel.Uri.AbsoluteUri);

                        var pvm = readingListBacking.FindInAll((m) => m.Uri.AbsoluteUri == model.Value.Uri.AbsoluteUri);
                        if (pvm != null)
                        {
                            await wvp.DoOnMainThreadAsync(() =>
                            {
                                readingListBacking.Remove(pvm);
                                pvm.Dispose();
                            });
                        }
                    }
                    App.Database.SaveReadingListItems(new Models.ReadingList { Uri = model.Value.Uri.AbsoluteUri, PrimaryTag = model.Value.PrimaryTag, Title = model.Value.Title, Unread = viewmodel.Unread });
                }
                wvp.DoOnMainThread(() =>
                {
                    viewmodel.BaseData = model.Value;
                    //if (readingListBacking.FindInAll((m) => m.Uri.AbsoluteUri == viewmodel.Uri.AbsoluteUri) == null)
                    //    readingListBacking.Add(viewmodel);
                });
            });
        }
    }
}
