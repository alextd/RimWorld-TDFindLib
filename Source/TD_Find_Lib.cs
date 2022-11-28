using System.Reflection;
using System.Linq;
using System.IO;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	public class Mod : Verse.Mod
	{
		public static Settings settings;
		public Mod(ModContentPack content) : base(content)
		{
			// initialize settings, wait for defs and anything to load.
			LongEventHandler.ExecuteWhenFinished(() =>
			{
				settings = GetSettings<Settings>();

				if (settings.firstUse)
				{
					SearchGroup group =
						ScribeXmlFromString.LoadFromString<SearchGroup>(
							File.ReadAllText(
								GenFile.ResolveCaseInsensitiveFilePath(
									Content.ModMetaData.RootDir.FullName + Path.DirectorySeparatorChar + "About", "DefaultSearches.xml")),
							null, null);

					if (group != null)
					{
						settings.firstUse = false;

						settings.Receive(group);
						settings.Write();
					}
				}
			});
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