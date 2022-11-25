using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;


namespace TD_Find_Lib
{
	public class TDFindLibGameComp : GameComponent
	{
		public TDFindLibGameComp(Game g) : base() { }

		//continuousRefresh
		public List<RefreshQuerySearch> searchRefreshers = new();

		public bool RemoveRefresh(QuerySearch search) =>
			searchRefreshers.RemoveAll(r => r.search == search) > 0;

		public void RegisterRefresh(RefreshQuerySearch refSearch)
		{
			RemoveRefresh(refSearch.search);
			int insert = searchRefreshers.FindLastIndex(r => r.tag == refSearch.tag);
			if(insert == -1)
				searchRefreshers.Add(refSearch);
			else
				searchRefreshers.Insert(insert + 1, refSearch);
		}

		public bool IsRefreshing(QuerySearch search) =>
			searchRefreshers.Any(r => r.search == search);

		public T GetRefresher<T>(QuerySearch search) where T : RefreshQuerySearch =>
			searchRefreshers.FirstOrDefault(r => r.search == search) as T;


		public override void GameComponentTick()
		{
			foreach (var refreshSearch in searchRefreshers)
				if (Find.TickManager.TicksGame % refreshSearch.period == 0)
				{
					Log.Message($"Refreshing {refreshSearch.search.name}");
					refreshSearch.search.RemakeList();
				}
		}


		public override void ExposeData()
		{
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				var savedR = searchRefreshers.FindAll(r => r.permanent);
				Scribe_Collections.Look(ref savedR, "refreshers");
			}
			else
			{
				Scribe_Collections.Look(ref searchRefreshers, "refreshers");
			}
		}
	}

	public abstract class RefreshQuerySearch : IExposable
	{
		public QuerySearch search;
		public string tag;
		public int period;
		public bool permanent;

		public RefreshQuerySearch(QuerySearch search, string tag, int period = 1, bool permanent = false)
		{
			this.search = search;
			this.tag = tag;
			this.period = period;
			this.permanent = permanent;
		}

		public void ExposeData()
		{
			Scribe_Deep.Look(ref search, "search");
			Scribe_Deep.Look(ref tag, "tag");
			Scribe_Values.Look(ref period, "period");
			permanent = true;//ofcourse.
		}

		public abstract void OpenUI(QuerySearch search);
	}
}
