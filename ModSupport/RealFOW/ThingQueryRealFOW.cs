using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;
using RimWorldRealFoW.Utils;

namespace TDFindLib_RealFOW
{
	public class ThingQueryRealFOW : ThingQuery
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			thing.fowIsVisible();
	}
}
