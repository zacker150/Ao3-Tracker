﻿using System;

namespace Ao3TrackReader
{
    [Flags]
    public enum NavigateBehaviour
    {
        History = 1,
        Page = 2,

        HistoryFirst = 4,

        HistoryThenPage = History | Page | HistoryFirst,
        PageThenHistory = History | Page,
    }
}