﻿using PixivFSCS;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using FSharp.Data;
using System.Web;

namespace PixivFSUWP.Data
{
    public class BookmarkIllustsCollection : ObservableCollection<ViewModels.WaterfallItemViewModel>, ISupportIncrementalLoading
    {
        readonly string userID;
        string nexturl = "begin";
        bool _busy = false;
        bool _emergencyStop = false;
        EventWaitHandle pause = new ManualResetEvent(true);

        public BookmarkIllustsCollection(string UserID)
        {
            userID = UserID;
        }

        public BookmarkIllustsCollection() : this(OverAll.GlobalBaseAPI.user_id) { }

        public bool HasMoreItems
        {
            get => nexturl != "";
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            if (_busy)
                throw new InvalidOperationException("Only one operation in flight at a time");
            _busy = true;
            return AsyncInfo.Run((c) => LoadMoreItemsAsync(c, count));
        }

        public void StopLoading()
        {
            if (_busy)
            {
                _emergencyStop = true;
                ResumeLoading();
            }
        }

        public void PauseLoading()
        {
            pause.Reset();
        }

        public void ResumeLoading()
        {
            pause.Set();
        }

        protected async Task<LoadMoreItemsResult> LoadMoreItemsAsync(CancellationToken c, uint count)
        {
            try
            {
                if (!HasMoreItems) return new LoadMoreItemsResult() { Count = 0 };
                LoadMoreItemsResult toret = new LoadMoreItemsResult() { Count = 0 };
                JsonValue bookmarkres = null;
                if (nexturl == "begin")
                    bookmarkres = await Task.Run(() => new PixivFS
                        .PixivAppAPI(OverAll.GlobalBaseAPI)
                        .csfriendly_user_bookmarks_illust(userID));
                else
                {
                    Uri next = new Uri(nexturl);
                    string getparam(string param) => HttpUtility.ParseQueryString(next.Query).Get(param);
                    bookmarkres = await Task.Run(() => new PixivFS
                        .PixivAppAPI(OverAll.GlobalBaseAPI)
                        .csfriendly_user_bookmarks_illust(userID, getparam("restrict"),
                        getparam("filter"), getparam("max_bookmark_id")));
                }
                nexturl = bookmarkres.Item("next_url").AsString();
                foreach (var recillust in bookmarkres.Item("illusts").AsArray())
                {
                    if (_emergencyStop)
                    {
                        _emergencyStop = false;
                        return toret;
                    }
                    await Task.Run(() => pause.WaitOne());
                    Data.WaterfallItem recommendi = Data.WaterfallItem.FromJsonValue(recillust);
                    var recommendmodel = ViewModels.WaterfallItemViewModel.FromItem(recommendi);
                    await recommendmodel.LoadImageAsync();
                    Add(recommendmodel);
                    toret.Count++;
                }
                return toret;
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
