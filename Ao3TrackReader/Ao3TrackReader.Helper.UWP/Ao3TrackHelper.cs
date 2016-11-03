﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Core;

namespace Ao3TrackReader.Helper
{
    public interface IWorkChapter
    {
        long number { get; set; }
        long chapterid { get; set; }
        long? location { get; set; }
    }

    [AllowForWeb]
    public sealed class WorkChapter : IWorkChapter
    {
        public long number { get; set; }
        public long chapterid { get; set; }
        public long? location { get; set; }
    }

    public delegate void MainThreadAction();
    public delegate object MainThreadFunc();

    public interface IEventHandler
    {
        [DefaultOverload]
        object DoOnMainThread(MainThreadFunc function);
        void DoOnMainThread(MainThreadAction function);

        IAsyncOperation<IDictionary<long, IWorkChapter>> GetWorkChaptersAsync([ReadOnlyArray] long[] works);
        void SetWorkChapters(IDictionary<long, IWorkChapter> works);
        bool JumpToLastLocationEnabled { get; set; }
        bool canGoBack { get; }
        bool canGoForward { get; }
        double leftOffset { get; set; }
        double opacity { get; set; }
        bool showPrevPageIndicator { get; set; }
        bool showNextPageIndicator { get; set; }
        string[] scriptsToInject { get; }
        string[] cssToInject { get; }
        int FontSizeMax { get; }
        int FontSizeMin { get; }
        int FontSize { get; set; }
        double realWidth { get; }
        double realHeight { get; }
    }

    [AllowForWeb]
    public sealed class Ao3TrackHelper
    {
        IEventHandler handler;
        public Ao3TrackHelper(IEventHandler handler)
        {
            this.handler = handler;
        }

        public string[] scriptsToInject { get { return handler.scriptsToInject; } }
        public string[] cssToInject { get { return handler.cssToInject; } }


        public event EventHandler<bool> JumpToLastLocationEvent;
        public void OnJumpToLastLocation(bool pagejump)
        {
            Task<object>.Run(() =>
            {
                JumpToLastLocationEvent?.Invoke(this, pagejump);
            });
        }

        public event EventHandler<object> AlterFontSizeEvent;
        public void OnAlterFontSizeEvent()
        {
            Task<object>.Run(() =>
            {
                AlterFontSizeEvent?.Invoke(this, null);
            });
        }

        public IAsyncOperation<object> getWorkChaptersAsync([ReadOnlyArray] long[] works)
        {
            return Task.Run(async () =>
            {
                return (object) await handler.GetWorkChaptersAsync(works);
            }).AsAsyncOperation();           
        }

        public IDictionary<long, IWorkChapter> createWorkChapterMap()
        {
            return new Dictionary<long, IWorkChapter>();
        }

        public IWorkChapter createWorkChapter(long number, long chapterid, long? location)
        {
            return new WorkChapter {
                number = number,
                chapterid = chapterid,
                location = location
            };

        }

        public void setWorkChapters(IDictionary<long, IWorkChapter> works)
        {
            Task.Run(() => 
            {
                handler.SetWorkChapters(works);
            });
        }

        public bool jumpToLastLocationEnabled
        {
            get { return (bool) handler.DoOnMainThread(() => handler.JumpToLastLocationEnabled); }
            set { handler.DoOnMainThread(() => handler.JumpToLastLocationEnabled = value); }
        }
        public bool canGoBack {
            get { return (bool)handler.DoOnMainThread(() => handler.canGoBack); }
        }
        public bool canGoForward {
            get { return (bool) handler.DoOnMainThread(() => handler.canGoForward); }
        }
        public double leftOffset {
            get { return (double) handler.DoOnMainThread(() => handler.leftOffset); }
            set { handler.DoOnMainThread(() => handler.leftOffset = value); }
        }
        public double opacity {
            get { return (double) handler.DoOnMainThread(() => handler.opacity); }
            set { handler.DoOnMainThread(() => handler.opacity = value); }
        }
        public int fontSizeMax { get { return handler.FontSizeMax; } }
        public int fontSizeMin { get { return handler.FontSizeMin; } }
        public int fontSize {
            get { return (int) handler.DoOnMainThread(() => handler.FontSize); }
            set { handler.DoOnMainThread(() => handler.FontSize = value); }
        }
        public bool showPrevPageIndicator {
            get { return (bool) handler.DoOnMainThread(() => handler.showPrevPageIndicator );  }
            set { handler.DoOnMainThread(() => handler.showPrevPageIndicator = value ); } }
        public bool showNextPageIndicator {
            get { return (bool) handler.DoOnMainThread(() => handler.showNextPageIndicator); }
            set { handler.DoOnMainThread(() => handler.showNextPageIndicator = value); }
        }

        public double realWidth {
            get { return (double)handler.DoOnMainThread(() => handler.realWidth); }

        }
        public double realHeight
        {
            get { return (double)handler.DoOnMainThread(() => handler.realHeight); }
        }

    }
}