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
using System.Text;

namespace Ao3TrackReader.Data
{
    public class WorkEvents
    {
        static Dictionary<long, WorkEvents> events = new Dictionary<long, WorkEvents>();

        public static WorkEvents TryGetEvent(long workid)
        {
            WorkEvents e;
            if (events.TryGetValue(workid, out e))
                return e;
            return null;
        }

        public static WorkEvents GetEvent(long workid)
        {
            WorkEvents e;
            if (events.TryGetValue(workid, out e))
                return e;
            return events[workid] = new WorkEvents();
        }

        public event EventHandler<EventArgs<Work>> ChapterNumChanged;

        public void OnChapterNumChanged(object sender,Work w)
        {
            ChapterNumChanged?.Invoke(sender, new EventArgs<Work>(w));
        }

        public event EventHandler<EventArgs<Work>> LocationChanged;

        public void OnLocationChanged(object sender, Work w)
        {
            LocationChanged?.Invoke(sender, new EventArgs<Work>(w));
        }

    }


}
