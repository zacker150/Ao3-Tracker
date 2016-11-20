﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Ao3TrackReader
{
    public interface IGroupable<in T> : IComparable<T>
    {
        event EventHandler CompareChanged;
        string Group { get; }
        string GroupType { get; }
    }

    public class GroupSubList<T> : ObservableCollection<T>, INotifyPropertyChanged, INotifyPropertyChanging
        where T : IGroupable<T>
    {
        public GroupSubList(string group)
        {
            Group = group;
        }

        public string Group { get; protected set; }
        string grouptype;
        public string GroupType
        {
            get { return grouptype; }
            set
            {
                if (grouptype == value) return;
                OnPropertyChanging(new PropertyChangingEventArgs("GroupType"));
                grouptype = value;
                OnPropertyChanged(new PropertyChangedEventArgs("GroupType"));
            }
        }

        public event PropertyChangingEventHandler PropertyChanging;
        void OnPropertyChanging(PropertyChangingEventArgs args)
        {
            PropertyChanging?.Invoke(this, args);
        }

        public void AddSorted(T item)
        {
            int i = 0;
            for (; i < Count; i++)
            {
                if (item.CompareTo(this[i]) <= 0)
                {
                    break;
                }
            }

            InsertItem(i, item);
        }

        private void Item_CompareChanged(object sender, EventArgs e)
        {
            T item = (T) sender;
            int i = 0, c = -1, n = -1;
            for (; i < Count; i++)
            {
                T check = this[i];
                if (object.ReferenceEquals(item,check))
                {
                    c = i;
                }
                else if (n == -1 && item.CompareTo(this[i]) < 0)
                {
                    n = i;
                }
                if (c != -1 && n != -1)
                    break;
            }
            if (c == n-1) return;
            if (c != -1 && n != -1)
            {
                MoveItem(c, n); // Is this correct??
            }
        }

        protected override void InsertItem(int index, T item)
        {
            item.CompareChanged += Item_CompareChanged;
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            this[index].CompareChanged -= Item_CompareChanged;
            base.RemoveItem(index);
        }
    }

    public class GroupList<T> : ObservableCollection<GroupSubList<T>>
        where T : IGroupable<T>, INotifyPropertyChanged, INotifyPropertyChanging
    { 
        public void Add(T item)
        {
            // Add the item to the correct list
            item.PropertyChanging += Item_PropertyChanging;
            item.PropertyChanged += Item_PropertyChanged;

            AddToGroup(item);   
        }

        public void Remove(T item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
            item.PropertyChanging -= Item_PropertyChanging;

            RemoveFromGroup(item);
        }

        public T Find(Predicate<T> pred)
        {
            foreach (var g in this)
            {
                foreach (var e in g)
                {
                    if (pred(e))
                        return e;
                }
            }
            return default(T);
        }

        private void AddToGroup(T item)
        {
            string groupName = item.Group;
            if (string.IsNullOrWhiteSpace(groupName)) groupName = "<Other>";
            GroupSubList<T> g = null;

            int i = 0;
            for (; i < Count; i++)
            {
                int c = String.Compare(this[i].Group, groupName);
                if (c == 0) {
                    g = this[i];
                    break;
                }
                else if (c > 0 && groupName != "<Other>")
                {
                    break;
                }
            }
            if (g == null)
            {
                g = new GroupSubList<T>(groupName);
                if (!string.IsNullOrWhiteSpace(item.GroupType)) g.GroupType = item.GroupType;
                g.AddSorted(item);
                Insert(i, g);
            }
            else
            {
                g.AddSorted(item);
                if (!string.IsNullOrWhiteSpace(item.GroupType)) g.GroupType = item.GroupType;
            }

        }

        private void RemoveFromGroup(T item)
        {
            string groupName = item.Group;
            if (string.IsNullOrWhiteSpace(groupName)) groupName = "<Other>";

            var g = this.Where((l) => l.Group == groupName).FirstOrDefault();
            if (g != null)
            {
                g.Remove(item);
                if (g.Count == 0) Remove(g);
            }
        }

        private void Item_PropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            if (String.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Group") {
                // Remove from its group
                RemoveFromGroup((T)sender);
            }
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (String.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Group")
            {
                // Add into group
                AddToGroup((T)sender);
            }
            if (String.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "GroupType")
            {
                var item = (T)sender;
                if (!string.IsNullOrWhiteSpace(item.GroupType))
                {
                    string groupName = item.Group;
                    if (string.IsNullOrWhiteSpace(groupName)) groupName = "<Other>";
                    var g = this.Where((l) => l.Group == groupName).FirstOrDefault();
                    if (g != null) g.GroupType = item.GroupType;
                }
            }
        }
    }
}
