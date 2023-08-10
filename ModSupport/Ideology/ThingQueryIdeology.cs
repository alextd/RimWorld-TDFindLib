using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TD_Find_Lib;
using Verse;
using RimWorld;
using UnityEngine;

namespace TDFindLib_Ideology
{

	//helper class for category
	public class ThingQueryStyleCategory : ThingQueryCategorizedDropdownHelper<ThingStyleDef, StyleCategoryDef, ThingQueryThingStyle, ThingQueryStyleCategory>
	{
		public override bool AppliesDirectlyTo(Thing thing) =>
			// Not ?.StyleDef because null .Category is an option
			thing.StyleDef is ThingStyleDef styleDef && styleDef.Category == sel;


		public override string NameFor(StyleCategoryDef cat) => cat.LabelCap;
		public override string NullOption() => "TD.OtherCategory".Translate();

		public override Texture2D IconTexFor(StyleCategoryDef cat) => cat?.Icon;
	}

	public class ThingQueryThingStyle : ThingQueryCategorizedDropdown<ThingStyleDef, StyleCategoryDef, ThingQueryThingStyle, ThingQueryStyleCategory>
	{
		public ThingQueryThingStyle() => extraOption = 1;

		public override bool AppliesDirectly2(Thing thing)
		{
			if (extraOption == 1)
				return thing.StyleDef != null;

			// redundant with check below
			//if (sel == null)
			//	return thing.StyleDef == null;

			return thing.StyleDef == sel;
		}

		public static readonly Dictionary<ThingStyleDef, string> styleNames = new();
		public static readonly Dictionary<ThingStyleDef, ThingDef> baseDefForStyle = new();
		static ThingQueryThingStyle()
		{
			foreach (StyleCategoryDef styleDef in DefDatabase<StyleCategoryDef>.AllDefsListForReading)
				foreach (ThingDefStyle style in styleDef.thingDefStyles)
				{
					baseDefForStyle[style.StyleDef] = style.ThingDef;

					styleNames[style.StyleDef] = styleDef.LabelCap + " " + style.ThingDef.LabelCap;
				}


			foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
				if (thingDef.randomStyle != null)
					foreach (ThingStyleChance styleChance in thingDef.randomStyle)
					{
						baseDefForStyle[styleChance.StyleDef] = thingDef;

						// Probably redundant since this seems to be overriden below:
						styleNames[styleChance.StyleDef] = thingDef.LabelCap;
					}

			// overrides
			foreach (ThingStyleDef styleDef in DefDatabase<ThingStyleDef>.AllDefsListForReading)
				if (styleDef.overrideLabel != null)
					styleNames[styleDef] = styleDef.overrideLabel.CapitalizeFirst();
		}
		public override string NameFor(ThingStyleDef def) => styleNames[def];


		public override string NullOption() => "None".Translate();
		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
		public override StyleCategoryDef CategoryFor(ThingStyleDef def) => def.Category;

		public override Texture2D IconTexFor(ThingStyleDef def) => def.UIIcon;
		public override Color IconColorFor(ThingStyleDef def)
		{
			if (def.color != default)
				return def.color; // though def.color is never set without mods..

			ThingDef baseDef = baseDefForStyle[def];
			if (baseDef.MadeFromStuff)
				return baseDef.GetColorForStuff(GenStuff.DefaultStuffFor(baseDef));
			return baseDef.uiIconColor;
		}

		public override IEnumerable<ThingStyleDef> AvailableOptions() =>
			ContentsUtility.AvailableInGame(t => t.StyleDef);
	}


	public class ThingQueryIdeoligion : ThingQueryDropDown<Ideo>
	{
		public ThingQueryIdeoligion() => extraOption = 1;

		protected override Ideo ResolveRef(Map map) =>
			Find.IdeoManager.ideos.FirstOrDefault(i => NameFor(i) == selName);

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Ideo ideo = pawn.Ideo;
			if (ideo == null) return false;

			if (extraOption == 1)
				return ideo == Faction.OfPlayer.ideos.PrimaryIdeo;

			if (extraOption == 2)
				return ideo != Faction.OfPlayer.ideos.PrimaryIdeo;

			return ideo == sel;
		}

		public override IEnumerable<Ideo> AllOptions() =>
			Current.Game?.World?.ideoManager?.IdeosInViewOrder;

		public override Color IconColorFor(Ideo ideo) => ideo.Color;
		public override Texture2D IconTexFor(Ideo ideo) => ideo.Icon;

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.PlayersIdeoligion".Translate() : "TD.OtherIdeoligion".Translate();
	}


	//Non-structure memes.
	public class ThingQueryMeme : ThingQueryDropDown<MemeDef>
	{
		public ThingQueryMeme() => sel = MemeDefOf.Transhumanist;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Ideo ideo = pawn.Ideo;
			if (ideo == null) return false;


			return pawn.Ideo.memes.Contains(sel);
		}

		public override Texture2D IconTexFor(MemeDef d) => d.Icon;

		public override IEnumerable<MemeDef> AllOptions() =>
				// Only Structore or Normal, ehhhh
				base.AllOptions().Where(m => m.category != MemeCategory.Structure);
	}

	public class ThingQueryPrecept : ThingQueryCategorizedDropdown<PreceptDef, IssueDef>
	{
		public ThingQueryPrecept() => sel = PreceptDefOf.Slavery_Honorable;

		public override string NameForCat(IssueDef cat) => cat?.LabelCap ?? "TD.OtherCategory".Translate();

		public static readonly HashSet<PreceptDef> singlePreceptIssues =
			DefDatabase<PreceptDef>.AllDefs.Where(p => !DefDatabase<PreceptDef>.AllDefs.Any(other => p != other && other.issue == p.issue)).ToHashSet();
		public override IssueDef CategoryFor(PreceptDef def) => singlePreceptIssues.Contains(def) ? null : def.issue;

		public override Texture2D IconTexForCat(IssueDef cat) => cat?.Icon ?? null;
		public override Color IconColorFor(PreceptDef p) => IdeoUIUtility.GetIconAndLabelColor(p.impact);

		public override bool OrderedCat => true;
		//public override IComparable OrderByCat(IssueDef cat) => cat.order;
		//The order of precepts is inconsistent, and the precepts are ordered, not the Issues. Ugh.

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Ideo ideo = pawn.Ideo;
			if (ideo == null) return false;


			return pawn.Ideo.HasPrecept(sel);
		}

		public override Texture2D IconTexFor(PreceptDef d) => d.Icon;
		public override string NameFor(PreceptDef d) => d.tipLabelOverride ?? ((string)(d.issue.LabelCap + ": " + d.LabelCap)); //Precept.TipLabel

		public override bool Ordered => true;
		public override IEnumerable<PreceptDef> AllOptions() =>
			base.AllOptions().Where(p => p.visible && p.preceptClass == typeof(Precept));
	}

	public abstract class ThingQueryPreceptOther<T> : ThingQueryDropDown<PreceptDef> where T : Precept
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			Ideo ideo = pawn.Ideo;
			if (ideo == null) return false;

			return AppliesToIdeo(pawn, ideo);
		}

		public abstract bool AppliesToIdeo(Pawn pawn, Ideo ideo);

		public override Texture2D IconTexFor(PreceptDef d) => d.Icon;
		public override string NameFor(PreceptDef d) => d.tipLabelOverride ?? d.LabelCap;

		public override IEnumerable<PreceptDef> AllOptions() =>
			base.AllOptions().Where(pdef => pdef.visible && typeof(T).IsAssignableFrom(pdef.preceptClass));
	}

	public class ThingQueryRole : ThingQueryPreceptOther<Precept_Role>
	{
		public bool canBe; // default checks "is role"

		public ThingQueryRole() => sel = PreceptDefOf.IdeoRole_Leader;

		public bool RoleFitsSel(Precept_Role role) =>
			extraOption switch
			{
				3 => role is Precept_RoleMulti,
				2 => role is Precept_RoleSingle,
				1 => role != null, //aka role is Precept_Role
				_ => role?.def == sel
			};

		public override bool AppliesToIdeo(Pawn pawn, Ideo ideo)
		{
			var role = ideo.GetRole(pawn);

			if (extraOption == 4)
			{
				if (role == null || role.apparelRequirements.NullOrEmpty())
					return false;

				return role.apparelRequirements
					.Select(r => r.requirement)
					.Any(r => r.IsActive(pawn) && !r.IsMet(pawn));
				// For multi-roles missing apparel, The -mood doesn't actually take effect
				// But they'll still choose to qear required apparel over others it seems
				// So whatever, even if there's no mood debuff, include them in this filter.
				// Otherwise if role == single return false
			}

			if (canBe)
				return ideo.RolesListForReading.Where(RoleFitsSel).Any(r => r.RequirementsMet(pawn));

			return RoleFitsSel(role);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref canBe, "canBe");
		}

		protected override ThingQuery Clone()
		{
			ThingQueryRole clone = (ThingQueryRole)base.Clone();
			clone.canBe = canBe;
			return clone;
		}

		public override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			bool changed = base.DrawMain(rect, locked, fullRect);

			if (extraOption != 4)
				changed |= row.ButtonTextToggleBool(ref canBe, "TD.CanBe".Translate(), "TD.HasRole".Translate());

			return changed;
		}

		public override string NullOption() => "None".Translate();
		public override int ExtraOptionsCount => 4;
		public override string NameForExtra(int ex) =>
			ex switch
			{
				4 => "TD.WantsApparelForRole".Translate(),
				3 => "TD.AnyNonLeaderRole".Translate(),
				2 => "TD.AnyLeaderRole".Translate(),
				_ => "TD.AnyOption".Translate()
			};

	}

	/*
	 * Aeh rituals got awfully complex with ... obligations and whatnot.
	 * And it's awkard to have to target buildings that have rituals attached anyway
	// Not PreceptOther, it doesn't target pawns ideo and it has invisible precepts
	public enum RitualFilterType { Has, Available, InProgress}
	public class ThingQueryRitual : ThingQueryDropDown<PreceptDef>
	{
		public RitualFilterType filterType;

		public ThingQueryRitual() => sel = PreceptDefOf.RoleChange;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref filterType, "filterType");
		}

		protected override ThingQuery Clone()
		{
			ThingQueryRitual clone = (ThingQueryRitual)base.Clone();
			clone.filterType = filterType;
			return clone;
		}

		public static IEnumerable<Precept_Ritual> RitualsAt(Building building)
		{

			foreach (Ideo ideo in Faction.OfPlayer.ideos.AllIdeos)
			{
				foreach (Precept precept in ideo.PreceptsListForReading)
				{
					if (precept is not Precept_Ritual ritual) continue;

					if (!ritual.activeObligations.NullOrEmpty())
						foreach (RitualObligation obl in ritual.activeObligations)
							if (ritual.CanUseTarget(building, obl).canUse)
								yield return ritual;
				}
			}
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			Building building = thing as Building;
			if (building == null) return false;

			if(extraOption == 1)
			{
				return filterType switch
				{
					RitualFilterType.Has => RitualsAt(building).Any(),
					RitualFilterType.Available => RitualsAt(building).Any(r => r.CanUseTarget(building, ,
					RitualFilterType.InProgress=> building.TargetOfRitual() is LordJob_Ritual,
					_ => false // I guess
				}
				return ;
			}

			return building.TargetOfRitual() is LordJob_Ritual lordjob && lordjob.Ritual.def == sel;
		}

		public override Texture2D IconTexFor(PreceptDef d) => d.Icon;
		public override string NameFor(PreceptDef d) => d.tipLabelOverride ?? d.LabelCap;

		public override IEnumerable<PreceptDef> AllOptions() =>
			base.AllOptions().Where(pdef => typeof(Precept_Ritual).IsAssignableFrom(pdef.preceptClass));

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}
	*/

	[DefOf]
	public class ThingQueryWeaponClass : ThingQueryDropDown<WeaponClassDef>
	{
		public static WeaponClassDef Ultratech;
		//public bool despised; // false means check noble

		public ThingQueryWeaponClass() => sel = Ultratech;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			ThingWithComps weapon =
				thing is Pawn pawn ?
				pawn.equipment?.Primary :
				thing as ThingWithComps; //if it's not a weapon it won't pass check anyway

			if (weapon == null)
				return false;

			return weapon.def.weaponClasses?.Contains(sel) ?? false;
		}
	}

	//Check if item is noble to player ideo ; or held pawn's ideo. ; or pawn's held weapon
	public class ThingQueryWeaponDisposition : ThingQueryDropDown<IdeoWeaponDisposition>
	{
		public ThingQueryWeaponDisposition() => sel = IdeoWeaponDisposition.Noble;

		public override bool AppliesDirectlyTo(Thing thing)
		{
			// The weapon itself, or the equipped weapon of a pawn.
			ThingWithComps weapon =
				thing is Pawn pawn
				? pawn.equipment?.Primary
				: thing as ThingWithComps; //if it's not a weapon it won't pass check anyway

			if (weapon == null || !weapon.def.IsWeapon)
				return false;

			// The ideo of the person holding this item, or... I guess just the Player's ideo. Items don't have factions.
			Ideo ideo =
				weapon.ParentHolder is Pawn_EquipmentTracker tracker
				? tracker.pawn.Ideo
				: Faction.OfPlayer.ideos.PrimaryIdeo;

			if (ideo == null)
				return false;

			return ideo.GetDispositionForWeapon(weapon.def) == sel;
		}

		public override string NameFor(IdeoWeaponDisposition o) =>
			base.NameFor(o).CapitalizeFirst();
	}

	public class ThingQueryVeneratedAnimal : ThingQueryIdeoligion // conveniently same options but different filter
	{
		public override bool AppliesDirectlyTo(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			// Which ideo this animal is venerated for
			Ideo ideo = sel;

			if (extraOption == 1)
				ideo = pawn.Faction?.ideos?.PrimaryIdeo; // An animal's owner's faction.

			if (extraOption == 2)
				ideo = Faction.OfPlayer.ideos.PrimaryIdeo;

			if (ideo == null) return false;

			return ideo.VeneratedAnimals.Contains(thing.def);
		}

		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.OwnersIdeoligion".Translate() : "TD.PlayersIdeoligion".Translate();
	}


	public enum GuaranlenFilterType { Connection, Mode, Strength, StrengthDesired, MaxDryads, CurDryads, TimeToDryad }
	public class ThingQueryGuaranlen : ThingQueryDropDown<GuaranlenFilterType>
	{
		public GauranlenTreeModeDef modeDef;
		public FloatRangeUB strength;
		public IntRangeUB numDryads;
		public IntRangeUB timeToDryad; // ticks, shown as days

		public ThingQueryGuaranlen()
		{
			modeDef = GauranlenTreeModeDefOf.Gaumaker;
			strength = new(0, 1, .5f, 1); // todo find consts
			numDryads = new(0, 4, 1, 2);
			timeToDryad = new(0, 8*GenDate.TicksPerDay, 0, GenDate.TicksPerDay);
		}
		public override void ExposeData()
		{
			base.ExposeData();

			switch (sel)
			{
				//				case GuaranlenFilterType.Connection:
				//					break;
				case GuaranlenFilterType.Mode:
					Scribe_Values.Look(ref modeDef, "modeDef");
					break;
				case GuaranlenFilterType.Strength:
				case GuaranlenFilterType.StrengthDesired:
					Scribe_Values.Look(ref strength.range, "strength");
					break;
				case GuaranlenFilterType.MaxDryads:
				case GuaranlenFilterType.CurDryads:
					Scribe_Values.Look(ref numDryads.range, "numDryads");
					break;
				case GuaranlenFilterType.TimeToDryad:
					Scribe_Values.Look(ref timeToDryad.range, "timeToDryad");
					break;
			}
		}
		protected override ThingQuery Clone()
		{
			ThingQueryGuaranlen clone = (ThingQueryGuaranlen)base.Clone();
			clone.modeDef = modeDef;
			clone.strength = strength;
			clone.numDryads = numDryads;
			clone.timeToDryad = timeToDryad;
			return clone;
		}


		public override bool AppliesDirectlyTo(Thing thing)
		{
			if (thing is not Plant tree ||
				tree.GetComp<CompTreeConnection>() is not CompTreeConnection connection)
				return false;

			switch (sel)
			{
				case GuaranlenFilterType.Connection:
					return connection.Connected;
				case GuaranlenFilterType.Mode:
					return connection.Mode == modeDef;
				case GuaranlenFilterType.Strength:
					return strength.Includes(connection.ConnectionStrength);
				case GuaranlenFilterType.StrengthDesired:
					return strength.Includes(connection.DesiredConnectionStrength);
				case GuaranlenFilterType.MaxDryads:
					return numDryads.Includes(connection.MaxDryads);
				case GuaranlenFilterType.CurDryads:
					return numDryads.Includes(connection.dryads.Count);
				case GuaranlenFilterType.TimeToDryad:
					return timeToDryad.Includes(connection.spawnTick - Find.TickManager.TicksGame);
			}

			return false;
		}

		public override bool DrawCustom(Rect fullRect)
		{
			switch (sel)
			{
				case GuaranlenFilterType.Connection:
					return false;

				case GuaranlenFilterType.Mode:
					RowButtonFloatMenuDef(modeDef, newValue => modeDef = newValue);
					return false;

				case GuaranlenFilterType.Strength:
				case GuaranlenFilterType.StrengthDesired:
					return TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref strength, ToStringStyle.PercentZero);

				case GuaranlenFilterType.MaxDryads:
				case GuaranlenFilterType.CurDryads:
					return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref numDryads);

				case GuaranlenFilterType.TimeToDryad:
					return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(row.FinalX), id, ref timeToDryad, ticks => $"{ticks * 1f / GenDate.TicksPerDay:0.0}");
			}

			return false;
		}
	}



	[StaticConstructorOnStartup]
	public static class ExpansionHider
	{
		static ExpansionHider()
		{
			if (!ModsConfig.IdeologyActive)
				foreach (ThingQuerySelectableDef def in DefDatabase<ThingQuerySelectableDef>.AllDefsListForReading)
					if (def.mod == ModContentPack.IdeologyModPackageId)
						def.devOnly = true;
		}
	}
}
