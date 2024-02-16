using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	[StaticConstructorOnStartup]
	public static class ModStartup
	{
		static ModStartup()
		{
			// initialize settings, wait for defs and anything to load.
			Mod.settings = LoadedModManager.GetMod<Mod>().GetSettings<Settings>();

			if (Mod.settings.firstUse)
			{
				List<SearchGroup> groups = DefaultSearches.CopyLibrary;

				Mod.settings.firstUse = false;
				foreach (var group in groups)
					Mod.settings.Add(group);
				Mod.settings.Write();
			}
		}
	}

	public class Mod : Verse.Mod
	{
		public static Settings settings;
		public Mod(ModContentPack content) : base(content)
		{
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
			settings.DoWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "TD.TDFindLibrary".Translate();
		}
	}

}