using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace TD_Find_Lib
{
	public class TDFindLibGameComp : GameComponent
	{
		public TDFindLibGameComp(Game g) : base() { }

		//continuousRefresh
		public List<(FindDescription, int)> findDescRefreshers = new();

		public void RemoveRefresh(FindDescription d) =>
			findDescRefreshers.RemoveAll(tuple => tuple.Item1 == d);

		public void RegisterRefresh(FindDescription d, int p)
		{
			RemoveRefresh(d);
			findDescRefreshers.Add((d, p));
		}

		public bool IsRefreshing(FindDescription desc) =>
			findDescRefreshers.Any(tuple => desc == tuple.Item1);

		public override void GameComponentTick()
		{
			foreach ((var desc, int period) in findDescRefreshers)
				if (Find.TickManager.TicksGame % period == 0)
				{
					Log.Message($"Refreshing {desc.name}");
					desc.RemakeList();
				}
		}
	}
}
