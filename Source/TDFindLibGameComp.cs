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
		public List<RefreshFindDesc> findDescRefreshers = new();

		public void RemoveRefresh(FindDescription desc) =>
			findDescRefreshers.RemoveAll(r => r.desc == desc);

		public void RegisterRefresh(RefreshFindDesc refDesc)
		{
			RemoveRefresh(refDesc.desc);
			int insert = findDescRefreshers.FindLastIndex(r => r.tag == refDesc.tag);
			if(insert == -1)
				findDescRefreshers.Add(refDesc);
			else
				findDescRefreshers.Insert(insert + 1, refDesc);
		}

		public bool IsRefreshing(FindDescription desc) =>
			findDescRefreshers.Any(r => r.desc == desc);


		public override void GameComponentTick()
		{
			foreach (var rDesc in findDescRefreshers)
				if (Find.TickManager.TicksGame % rDesc.period == 0)
				{
					Log.Message($"Refreshing {rDesc.desc.name}");
					rDesc.desc.RemakeList();
				}
		}


		public override void ExposeData()
		{
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				var savedR = findDescRefreshers.FindAll(r => r.permanent);
				Scribe_Collections.Look(ref savedR, "refreshers");
			}
			else
			{
				Scribe_Collections.Look(ref findDescRefreshers, "refreshers");
			}
		}
	}

	public abstract class RefreshFindDesc : IExposable
	{
		public FindDescription desc;
		public string tag;
		public int period;
		public bool permanent;

		public RefreshFindDesc(FindDescription desc, string tag, int period = 1, bool permanent = false)
		{
			this.desc = desc;
			this.tag = tag;
			this.period = period;
			this.permanent = permanent;
		}

		public void ExposeData()
		{
			Scribe_Deep.Look(ref desc, "desc");
			Scribe_Deep.Look(ref tag, "tag");
			Scribe_Values.Look(ref period, "period");
			permanent = true;//ofcourse.
		}

		public abstract void OpenUI(FindDescription desc);
	}
}
