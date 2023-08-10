using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using UnityEngine;
using Verse;
using RimWorld;

namespace TDFindLib_Biotech
{
	public enum BandwidthFilterType { Total, Used, Available}
	public class ThingQueryMechanitorBandwidth : ThingQueryIntRange
	{
		public override int Max => 40;

		BandwidthFilterType filterType;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref filterType, "filterType");
		}
		protected override ThingQuery Clone()
		{
			ThingQueryMechanitorBandwidth clone = (ThingQueryMechanitorBandwidth)base.Clone();
			clone.filterType = filterType;
			return clone;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Pawn_MechanitorTracker mech = pawn.mechanitor;
			if (mech == null) return false;

			return sel.Includes(filterType switch
			{
				BandwidthFilterType.Total => mech.TotalBandwidth,
				BandwidthFilterType.Used => mech.UsedBandwidth,
				_ => mech.TotalBandwidth - mech.UsedBandwidth
			});
		}

		public override bool DrawMain(UnityEngine.Rect rect, bool locked, UnityEngine.Rect fullRect)
		{
			bool changed = base.DrawMain(rect, locked, fullRect);

			RowButtonFloatMenuEnum(filterType, newValue => filterType = newValue);

			return changed;
		}
	}
}
