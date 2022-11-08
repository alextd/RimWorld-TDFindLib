using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	public class ListFilterDef : ListFilterSelectableDef
	{
		public Type filterClass;

		public override IEnumerable<string> ConfigErrors()
		{
			if (filterClass == null)
				yield return "ListFilterDef needs filterClass set";
		}
	}

	public abstract class ListFilter : IExposable
	{
		public ListFilterDef def;

		public IFilterHolder parent;

		public FindDescription RootFindDesc => parent?.RootFindDesc;


		protected int id; //For Widgets.draggingId purposes
		private static int nextID = 1;
		protected ListFilter() { id = nextID++; }


		private bool enabled = true; //simply turn off but keep in list
		public bool Enabled => enabled && DisableReason == null;

		private bool include = true; //or exclude


		// Okay, save/load. The basic gist here is:
		// During ExposeData loading, ResolveName is called for globally named things (defs)
		// But anything with a local reference (Zones) needs to resolve that ref on a map
		// Filters loaded from storage need to be cloned to a map to be used

		public virtual void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref enabled, "enabled", true);
			Scribe_Values.Look(ref include, "include", true);

			if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
			{
				DoResolveName();
				if (RootFindDesc.active)
					DoResolveRef();
			}
		}

		public virtual ListFilter Clone()
		{
			ListFilter clone = ListFilterMaker.MakeFilter(def);
			clone.enabled = enabled;
			clone.include = include;


			//No - Will be set in FilterHolder.Add or FilterHolder's ExposeData on LoadingVars step
			//clone.parent = newHolder; 

			return clone;
		}
		public virtual void DoResolveName() { }
		public virtual void DoResolveRef() { }


		public IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return Enabled ? list.Where(t => AppliesTo(t)) : list;
		}

		//This can be problematic for minified things: We want the qualities of the inner things,
		// but position/status of outer thing. So it just checks both -- but then something like 'no stuff' always applies. Oh well
		public bool AppliesTo(Thing thing)
		{
			bool applies = FilterApplies(thing);
			if (!applies && thing.GetInnerThing() is Thing innerThing && innerThing != thing)
				applies = FilterApplies(innerThing);

			return applies == include;
		}

		protected abstract bool FilterApplies(Thing thing);


		private bool shouldFocus;
		public void Focus() => shouldFocus = true;
		protected virtual void DoFocus() { }


		// Seems to be GameFont.Small on load so we're good
		public static float? incExcWidth;
		public static float IncExcWidth =>
			incExcWidth.HasValue ? incExcWidth.Value :
			(incExcWidth = Mathf.Max(Text.CalcSize("TD.IncludeShort".Translate()).x, Text.CalcSize("TD.ExcludeShort".Translate()).x)).Value;

		public (bool, bool) Listing(Listing_StandardIndent listing, bool locked)
		{
			Rect rowRect = listing.GetRect(Text.LineHeight);


			if (!include)
			{
				GUI.color = Color.red;
				Widgets.DrawLineHorizontal(rowRect.x + 1, rowRect.y + Text.LineHeight / 2 - 2, 8);
				GUI.color = Color.white;

				rowRect.xMin += 12;
			}
			WidgetRow row = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown, rowRect.width);

			bool changed = false;
			bool delete = false;

			if (!locked)
			{
				//Clear button
				if (row.ButtonIcon(TexCommand.ClearPrioritizedWork, "TD.DeleteThisFilter".Translate()))
				{
					delete = true;
					changed = true;
				}

				//Toggle button
				if (row.ButtonIcon(enabled ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, "TD.EnableThisFilter".Translate()))
				{
					enabled = !enabled;
					changed = true;
				}

				//Include/Exclude
				if (row.ButtonText(include ? "TD.IncludeShort".Translate() : "TD.ExcludeShort".Translate(),
					"TD.IncludeOrExcludeThingsMatchingThisFilter".Translate(),
					fixedWidth: IncExcWidth))
				{
					include = !include;
					changed = true;
				}
			}


			//Draw option row
			rowRect.width -= (rowRect.xMax - row.FinalX);
			changed |= DrawMain(rowRect, locked);
			changed |= DrawUnder(listing, locked);
			if (shouldFocus)
			{
				DoFocus();
				shouldFocus = false;
			}
			if (DisableReason is string reason)
			{
				Widgets.DrawBoxSolid(rowRect, new Color(0.5f, 0, 0, 0.25f));

				TooltipHandler.TipRegion(rowRect, reason);
			}

			listing.Gap(listing.verticalSpacing);
			return (changed, delete);
		}


		public virtual bool DrawMain(Rect rect, bool locked)
		{
			Widgets.Label(rect, def.LabelCap);
			return false;
		}
		protected virtual bool DrawUnder(Listing_StandardIndent listing, bool locked) => false;

		public virtual bool ValidForAllMaps => true && !CurrentMapOnly;
		public virtual bool CurrentMapOnly => false;

		public virtual string DisableReason =>
			!ValidForAllMaps && RootFindDesc.allMaps
				? "TD.ThisFilterDoesntWorkWithAllMaps".Translate()
				: null;

		public static void DoFloatOptions(List<FloatMenuOption> options)
		{
			if (options.NullOrEmpty())
				Messages.Message("TD.ThereAreNoOptionsAvailablePerhapsYouShouldUncheckOnlyAvailableThings".Translate(), MessageTypeDefOf.RejectInput);
			else
				Find.WindowStack.Add(new FloatMenu(options));
		}
	}

	public class FloatMenuOptionAndRefresh : FloatMenuOption
	{
		ListFilter owner;
		public FloatMenuOptionAndRefresh(string label, Action action, ListFilter f) : base(label, action)
		{
			owner = f;
		}

		public override bool DoGUI(Rect rect, bool colonistOrdering, FloatMenu floatMenu)
		{
			bool result = base.DoGUI(rect, colonistOrdering, floatMenu);

			if (result)
				owner.RootFindDesc.RemakeList();

			return result;
		}
	}

	public class ListFilterName : ListFilterWithOption<string>
	{
		public ListFilterName() => sel = "";

		protected override bool FilterApplies(Thing thing) =>
			//thing.Label.Contains(sel, CaseInsensitiveComparer.DefaultInvariant);	//Contains doesn't accept comparer with strings. okay.
			sel == "" || thing.Label.IndexOf(sel, StringComparison.OrdinalIgnoreCase) >= 0;

		public static readonly string namedLabel = "Named: ";
		public static float? namedLabelWidth;
		public static float NamedLabelWidth =>
			namedLabelWidth.HasValue ? namedLabelWidth.Value :
			(namedLabelWidth = Text.CalcSize(namedLabel).x).Value;

		public override bool DrawMain(Rect rect, bool locked)
		{
			Widgets.Label(rect, namedLabel);
			rect.xMin += NamedLabelWidth;

			if(locked)
			{
				Widgets.Label(rect, '"' + sel + '"');
				return false;
			}

			if (GUI.GetNameOfFocusedControl() == $"LIST_FILTER_NAME_INPUT{id}" &&
				Mouse.IsOver(rect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
			{
				GUI.FocusControl("");
				Event.current.Use();
			}

			GUI.SetNextControlName($"LIST_FILTER_NAME_INPUT{id}");
			string newStr = Widgets.TextField(rect.LeftPart(0.9f), sel);
			if (newStr != sel)
			{
				sel = newStr;
				return true;
			}
			if (Widgets.ButtonImage(rect.RightPartPixels(rect.height), TexUI.RotLeftTex))
			{
				GUI.FocusControl("");
				sel = "";
				return true;
			}
			return false;
		}

		protected override void DoFocus()
		{
			GUI.FocusControl($"LIST_FILTER_NAME_INPUT{id}");
		}
	}

	public enum ForbiddenType{ Forbidden, Allowed, Forbiddable}
	public class ListFilterForbidden : ListFilterDropDown<ForbiddenType>
	{
		protected override bool FilterApplies(Thing thing)
		{
			bool forbiddable = thing.def.HasComp(typeof(CompForbiddable)) && thing.Spawned;
			if (!forbiddable) return false;
			bool forbidden = thing.IsForbidden(Faction.OfPlayer);
			switch (sel)
			{
				case ForbiddenType.Forbidden: return forbidden;
				case ForbiddenType.Allowed: return !forbidden;
			}
			return true;  //forbiddable
		}
	}

	//automated ExposeData + Clone 
	public abstract class ListFilterWithOption<T> : ListFilter
	{
		// selection
		private T _sel;
		protected string selName;// if UsesSaveLoadName,  = SaveLoadXmlConstants.IsNullAttributeName;
		private int _extraOption; //0 meaning use _sel, what 1+ means is defined in subclass

		// A subclass with extra fields needs to override ExposeData and Clone to copy them

		public string selectionError; // Probably set on load when selection is invalid (missing mod?)
		public override string DisableReason => base.DisableReason ?? selectionError;

		// would like this to be T const * sel;
		public T sel
		{
			get => _sel;
			set
			{
				_sel = value;
				_extraOption = 0;
				selectionError = null;
				if (SaveLoadByName) selName = MakeSaveName();
				PostSelected();
			}
		}

		// A subclass should often set sel in the constructor
		// which will call the property setter above
		// If the default is null, and there's no PostSelected to do,
		// then it's fine to skip defining a constructor
		protected ListFilterWithOption()
		{
			if (SaveLoadByName)
				selName = SaveLoadXmlConstants.IsNullAttributeName;
		}
		protected virtual void PostSelected()
		{
			// A subclass with fields whose validity depends on the selection should override this
			// Most common usage is to set a default value that is valid for the selection
			// e.g. the skill filter has a range 0-20, but that's valid for all skills, so no need to reset here
			// e.g. the hediff filter has a range too, but that depends on the selected hediff, so the selected range needs to be set here
		}

		// This method works double duty:
		// Both telling if Sel can be set to null, and the string to show for null selection
		public virtual string NullOption() => null;

		protected int extraOption
		{
			get => _extraOption;
			set
			{
				_extraOption = value;
				_sel = default;
				selectionError = null;
				selName = null;
			}
		}

		//Okay, so, references.
		//A simple filter e.g. string search is usable everywhere.
		//In-game, as an alert, as a saved filter to load in, saved to file to load into another game, etc.
		//ExposeData and Clone can just copy that string, because a string is the same everywhere.
		//But a filter that references in-game things can't be used universally.
		//Filters are saved outside a running world, so even things like defs might not exist when loaded with different mods.
		//When such a filter is run in-game, it does of course set 'sel' and reference it like normal
		//But when such a filter is saved, it cannot be bound to an instance or even an ILoadReferencable id
		//So ExposeData saves and loads 'string selName' instead of the 'T sel'
		//When editing that filter when active, that's fine, sel isn't set but selName is - so selName should be readable.

		//ListFilters have 3 levels of saving, sort of like ExposeData's 3 passes.
		//Raw values can be saved/loaded by value easily in ExposeData.
		//Filters that SaveLoadByName, are saved  by name in ExposeData
		// - so they can be loaded into another game, 
		// - if that name cannot be resolved, the name is still saved instead of saving null
		//For loading there's two different times to load:
		//Filters that UsesResolveName can be resolved after the game starts up (e.g. defs),
		// - ResolveName is called from ExposeData, ResolvingCrossRefs
		//Filters that UsesResolveRef must be resolved on a map (e.g. Zones, ILoadReferenceable)
		// - Filters that are loaded and active also call ResolveRef in ExposeData, ResolvingCrossRefs
		// - Filters that are loaded and inactive do not call ResolveRef and only have selName set.
		// - Filters that are Cloned from a saved filter, which was inactive, will have ResolveRef called after the Clone.
		// - Filters that are Cloned from an active filter, onto a new map, will have ResolveRef called after the Clone.

		protected readonly static bool IsDef = typeof(Def).IsAssignableFrom(typeof(T));
		protected readonly static bool IsRef = typeof(ILoadReferenceable).IsAssignableFrom(typeof(T));
		protected readonly static bool IsEnum = typeof(T).IsEnum;

		public virtual bool UsesResolveName => IsDef;
		public virtual bool UsesResolveRef => IsRef;
		private bool SaveLoadByName => UsesResolveName || UsesResolveRef;
		protected virtual string MakeSaveName() => sel?.ToString() ?? SaveLoadXmlConstants.IsNullAttributeName;

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref _extraOption, "ex");
			if (_extraOption > 0)
			{
				if (Scribe.mode == LoadSaveMode.LoadingVars)
					extraOption = _extraOption;	// property setter to set other fields null

				// No need to worry about sel or refname, we're done!
				return;
			}

			//Oh Jesus T can be anything but Scribe doesn't like that much flexibility so here we are:
			//(avoid using property 'sel' so it doesn't MakeRefName())
			if (SaveLoadByName)
			{
				// Of course between games you can't get references so just save by name should be good enough
				// (even if it's from the same game, it can still resolve the reference all the same)

				// Saving a null selName saves "IsNull"
				Scribe_Values.Look(ref selName, "refName");

				// ResolveName() will be called when loaded onto a map for actual use
			}
			else if (typeof(IExposable).IsAssignableFrom(typeof(T)))
			{
				//This might just be to handle ListFilterSelection
				Scribe_Deep.Look(ref _sel, "sel");
			}
			else
				Scribe_Values.Look(ref _sel, "sel");
		}
		public override ListFilter Clone()
		{
			ListFilterWithOption<T> clone = (ListFilterWithOption<T>)base.Clone();

			clone.extraOption = extraOption;
			if (extraOption > 0)
				return clone;

			if (SaveLoadByName)
				clone.selName = selName;

			if(!UsesResolveRef)
				clone._sel = _sel;  //todo handle if sel needs to be deep-copied. Perhaps sel should be T const * sel...

			clone.selectionError = selectionError;

			return clone;
		}

		// Subclasses where SaveLoadByName is true need to implement ResolveName() or ResolveRef()
		// (unless it's just a Def)
		// return matching object based on refName (refName will not be "null")
		// returning null produces a selection error and the filter will be disabled
		public override void DoResolveName()
		{
			if (!UsesResolveName || extraOption > 0) return;

			if (selName == SaveLoadXmlConstants.IsNullAttributeName)
			{
				_sel = default; //can't use null because generic T isn't bound as reftype
			}
			else
			{
				_sel = ResolveName();

				if (_sel == null)
				{
					selectionError = $"Missing {def.LabelCap}: {selName}?";
					Verse.Log.Warning("TD.TriedToLoad0FilterNamed1ButCouldNotBeFound".Translate(def.LabelCap, selName));
				}
				else selectionError = null;
			}
		}
		public override void DoResolveRef()
		{
			if (!UsesResolveRef || extraOption > 0) return;

			if (selName == SaveLoadXmlConstants.IsNullAttributeName)
			{
				_sel = default; //can't use null because generic T isn't bound as reftype
			}
			else
			{
				_sel = ResolveRef(RootFindDesc.map);

				if (_sel == null)
				{
					selectionError = $"Missing {def.LabelCap}: {selName}?";
					Messages.Message("TD.TriedToLoad0FilterNamed1ButCouldNotBeFound".Translate(def.LabelCap, selName), MessageTypeDefOf.RejectInput);
				}
				else selectionError = null;
			}
		}

		protected virtual T ResolveName()
		{
			if (IsDef)
			{
				//Scribe_Defs.Look doesn't work since it needs the subtype of "Def" and T isn't boxed to be a Def so DefFromNodeUnsafe instead
				//_sel = ScribeExtractor.DefFromNodeUnsafe<T>(Scribe.loader.curXmlParent["sel"]);

				//DefFromNodeUnsafe also doesn't work since it logs errors - so here's custom code copied to remove the logging:

				return (T)(object)GenDefDatabase.GetDefSilentFail(typeof(T), selName, false);
			}

			throw new NotImplementedException();
		}
		protected virtual T ResolveRef(Map map)
		{
			throw new NotImplementedException();
		}
	}

	public abstract class ListFilterDropDown<T> : ListFilterWithOption<T>
	{
		private string GetLabel()
		{
			if (selectionError != null)
				return selName;

			if (extraOption > 0)
				return NameForExtra(extraOption);

			if (sel != null)
				return NameFor(sel);

			if (UsesResolveRef && !RootFindDesc.active)
				return selName;

			return NullOption() ?? "??Null selection??";
		}

		public virtual IEnumerable<T> Options()
		{
			if (IsEnum)
				return Enum.GetValues(typeof(T)).OfType<T>();
			if (IsDef)
				return GenDefDatabase.GetAllDefsInDatabaseForDef(typeof(T)).Cast<T>();
			throw new NotImplementedException();
		}

		// Override this to group your T options into categories
		public virtual string CategoryFor(T def) => null;

		private Dictionary<string, List<T>> OptionCategories()
		{
			Dictionary<string, List<T>> result = new();
			foreach (T def in Options())
			{
				string cat = CategoryFor(def);

				List<T> options;
				if (!result.TryGetValue(cat, out options))
				{
					options = new();
					result[cat] = options;
				}

				options.Add(def);
			}
			return result;
		}


		public virtual bool Ordered => false;
		public virtual string NameFor(T o) => o is Def def ? def.LabelCap.RawText : typeof(T).IsEnum ? o.TranslateEnum() : o.ToString();
		public virtual string DropdownNameFor(T o) => NameFor(o);
		protected override string MakeSaveName()
		{
			if (sel is Def def)
				return def.defName;

			// Many subclasses will just use NameFor, so do it here.
			return sel != null ? NameFor(sel) : base.MakeSaveName();
		}

		public virtual int ExtraOptionsCount => 0;
		private IEnumerable<int> ExtraOptions() => Enumerable.Range(1, ExtraOptionsCount);
		public virtual string NameForExtra(int ex) => throw new NotImplementedException();

		public override bool DrawMain(Rect rect, bool locked)
		{
			bool changeSelection = false;
			bool changed = false;
			if (HasCustom)
			{
				// Label, Selection option button on left, custom on the remaining rect
				WidgetRow row = new WidgetRow(rect.x, rect.y);
				row.Label(def.LabelCap);
				changeSelection = row.ButtonText(GetLabel());

				rect.xMin = row.FinalX;
				changed = DrawCustom(rect, row);
			}
			else
			{
				//Just the label on left, and selected option button on right
				base.DrawMain(rect, locked);
				string label = GetLabel();
				Rect buttRect = rect.RightPart(0.4f);
				buttRect.xMin -= Mathf.Max(buttRect.width, Text.CalcSize(label).x) - buttRect.width;
				changeSelection = Widgets.ButtonText(buttRect, label);
			}
			if (changeSelection)
			{
				List<FloatMenuOption> options = new();

				if (NullOption() is string nullOption)
					options.Add(new FloatMenuOptionAndRefresh(nullOption, () => sel = default, this)); //can't null because T isn't bound as reftype

				if (UsesCategories)
				{
					Dictionary<string, List<T>> categories = OptionCategories();

					foreach (string catLabel in categories.Keys)
						options.Add(new FloatMenuOption(catLabel, () =>
						{
							List<FloatMenuOption> catOptions = new();
							foreach (T o in Ordered ? categories[catLabel].AsEnumerable().OrderBy(o => NameFor(o)).ToList() : categories[catLabel])
								catOptions.Add(new FloatMenuOptionAndRefresh(DropdownNameFor(o), () => sel = o, this));
							DoFloatOptions(catOptions);
						}));
				}
				else
				{
					foreach (T o in Ordered ? Options().OrderBy(o => NameFor(o)) : Options())
						options.Add(new FloatMenuOptionAndRefresh(DropdownNameFor(o), () => sel = o, this));
				}

				foreach (int ex in ExtraOptions())
					options.Add(new FloatMenuOptionAndRefresh(NameForExtra(ex), () => extraOption = ex, this));

				DoFloatOptions(options);
			}
			return changed;
		}

		// Subclass can override DrawCustom to draw anything custom
		// (otherwise it's just label and option selection button)
		// Use either rect or WidgetRow in the implementation
		public virtual bool DrawCustom(Rect rect, WidgetRow row) => throw new NotImplementedException();

		// Auto detection of subclasses that use DrawCustom:
		private static readonly HashSet<Type> customDrawers = null;
		private bool HasCustom => customDrawers?.Contains(GetType()) ?? false;

		// Auto detection of subclasses that use Dropdown Categories:
		private static readonly HashSet<Type> categoryUsers = null;
		private bool UsesCategories => categoryUsers?.Contains(GetType()) ?? false;
		static ListFilterDropDown()//<T>	//Remember there's a customDrawers for each <T> but functionally that doesn't change anything
		{
			Type baseType = typeof(ListFilterDropDown<T>);
			foreach (Type subclass in baseType.AllSubclassesNonAbstract())
			{
				if (subclass.GetMethod(nameof(DrawCustom)).DeclaringType != baseType)
				{
					if (customDrawers == null)
						customDrawers = new HashSet<Type>();

					customDrawers.Add(subclass);
				}

				if (subclass.GetMethod(nameof(CategoryFor)).DeclaringType != baseType)
				{
					if (categoryUsers == null)
						categoryUsers = new HashSet<Type>();

					categoryUsers.Add(subclass);
				}
			}
		}
	}

	public class ListFilterDesignation : ListFilterDropDown<DesignationDef>
	{
		protected override bool FilterApplies(Thing thing) =>
			sel != null ?
			(sel.targetType == TargetType.Thing ? thing.MapHeld.designationManager.DesignationOn(thing, sel) != null :
			thing.MapHeld.designationManager.DesignationAt(thing.PositionHeld, sel) != null) :
			(thing.MapHeld.designationManager.DesignationOn(thing) != null ||
			thing.MapHeld.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0);

		public override string NullOption() => "TD.AnyOption".Translate();

		public override bool Ordered => true;
		public override IEnumerable<DesignationDef> Options() =>
			Mod.settings.OnlyAvailable ?
				Find.CurrentMap.designationManager.AllDesignations.Select(d => d.def).Distinct() :
				base.Options();

		public override string NameFor(DesignationDef o) => o.defName; // no labels on Designation def
	}

	public class ListFilterFreshness : ListFilterDropDown<RotStage>
	{
		protected override bool FilterApplies(Thing thing)
		{
			CompRottable rot = thing.TryGetComp<CompRottable>();
			return 
				extraOption == 1 ? rot != null : 
				extraOption == 2 ? GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) is float r && r>0 && r<1 : 
				extraOption == 3 ? GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) <= 0 : 
				rot?.Stage == sel;
		}

		public override string NameFor(RotStage o) => ("RotState"+o.ToString()).Translate();

		public override int ExtraOptionsCount => 3;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.Spoils".Translate() :
			ex == 2 ? "TD.Refrigerated".Translate() : 
			"TD.Frozen".Translate();
	}

	public class ListFilterTimeToRot : ListFilter
	{
		IntRange ticksRange = new IntRange(0, GenDate.TicksPerDay * 10);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref ticksRange, "ticksRange");
		}
		public override ListFilter Clone()
		{
			ListFilterTimeToRot clone = (ListFilterTimeToRot)base.Clone();
			clone.ticksRange = ticksRange;
			return clone;
		}

		protected override bool FilterApplies(Thing thing) =>
			thing.TryGetComp<CompRottable>()?.TicksUntilRotAtCurrentTemp is int t && ticksRange.Includes(t);

		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);

			IntRange newRange = ticksRange;
			Widgets.IntRange(rect.RightPart(0.5f), id, ref newRange, 0, GenDate.TicksPerDay * 20,
				$"{ticksRange.min*1f/GenDate.TicksPerDay:0.0} - {ticksRange.max * 1f / GenDate.TicksPerDay:0.0}");
			if (newRange != ticksRange)
			{
				ticksRange = newRange;
				return true;
			}
			return false;
		}
	}

	public class ListFilterGrowth : ListFilterWithOption<FloatRange>
	{
		public ListFilterGrowth() => sel = FloatRange.ZeroToOne;

		protected override bool FilterApplies(Thing thing) =>
			thing is Plant p && sel.Includes(p.Growth);
		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);
			FloatRange newRange = sel;
			Widgets.FloatRange(rect.RightPart(0.5f), id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (sel != newRange)
			{
				sel = newRange;
				return true;
			}
			return false;
		}
	}

	public class ListFilterPlantHarvest : ListFilterDropDown<ThingDef>
	{
		public ListFilterPlantHarvest() => extraOption = 1;

		protected override bool FilterApplies(Thing thing)
		{
			Plant plant = thing as Plant;
			if (plant == null)
				return false;

			ThingDef yield = plant.def.plant.harvestedThingDef;

			if (extraOption == 1)
				return yield != null;
			if (extraOption == 2)
				return yield == null;

			return sel == yield;
		}

		public static List<ThingDef> allHarvests;
		static ListFilterPlantHarvest()
		{
			HashSet<ThingDef> singleDefs = new();
			foreach(ThingDef def in DefDatabase<ThingDef>.AllDefs)
			{
				if(def.plant?.harvestedThingDef is ThingDef harvestDef)
					singleDefs.Add(harvestDef);
			}
			allHarvests = singleDefs.OrderBy(d => d.label).ToList();
		}
		public override IEnumerable<ThingDef> Options()
		{
			if (Mod.settings.OnlyAvailable)
			{
				HashSet<ThingDef> available = new HashSet<ThingDef>();
				foreach (Map map in Find.Maps)
					foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant))
						if ((t as Plant)?.def.plant.harvestedThingDef is ThingDef harvestDef)
							available.Add(harvestDef);

				return allHarvests.Intersect (available);
			}
			return allHarvests;
		}
		public override bool Ordered => true;

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) => // or FleshTypeDef but this works
			ex == 1 ? "TD.AnyOption".Translate() :
			"None".Translate();
	}
	

	public class ListFilterPlantHarvestable : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing is Plant plant && plant.HarvestableNow;
	}

	public class ListFilterPlantCrop : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing is Plant plant && plant.IsCrop;
	}

	public class ListFilterPlantDies : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing is Plant plant && (plant.def.plant?.dieIfLeafless ?? false);
	}

	public class ListFilterFaction : ListFilterDropDown<FactionRelationKind>
	{
		public bool host;	// compare host faction instead of thing's faction
		public ListFilterFaction() => extraOption = 1;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref host, "host");
		}
		public override ListFilter Clone()
		{
			ListFilterFaction clone = (ListFilterFaction)base.Clone();
			clone.host = host;
			return clone;
		}

		protected override bool FilterApplies(Thing thing)
		{
			Faction fac = thing.Faction;
			if (host)
			{
				if (thing is Pawn p && p.guest != null)
					fac = p.guest.HostFaction;
				else
					return false;
			}

			return
				extraOption == 1 ? fac == Faction.OfPlayer :
				extraOption == 2 ? fac == Faction.OfMechanoids :
				extraOption == 3 ? fac == Faction.OfInsects :
				extraOption == 4 ? fac != null && !fac.def.hidden :
				extraOption == 5 ? fac == null || fac.def.hidden :
				(fac != null && fac != Faction.OfPlayer && fac.PlayerRelationKind == sel);
		}

		public override string NameFor(FactionRelationKind o) => o.GetLabel();

		public override int ExtraOptionsCount => 5;
		public override string NameForExtra(int ex) => // or FleshTypeDef but this works
			ex == 1 ? "TD.Player".Translate() :
			ex == 2 ? "TD.Mechanoid".Translate() :
			ex == 3 ? "TD.Insectoid".Translate() :
			ex == 4 ? "TD.AnyOption".Translate() :
			"TD.NoFaction".Translate();

		public override bool DrawMain(Rect rect, bool locked)
		{
			bool changed = base.DrawMain(rect, locked);

			Rect hostRect = rect.LeftPart(0.6f);
			hostRect.xMin = hostRect.xMax - 60;
			if (Widgets.ButtonText(hostRect, host ? "Host Is" : "Is"))
			{
				host = !host;
				changed = true;
			}

			return changed;
		}

	}

	// This includes most things but not minifiable buildings.
	public class ListFilterItemCategory : ListFilterDropDown<ThingCategoryDef>
	{
		public ListFilterItemCategory() => sel = ThingCategoryDefOf.Root;

		protected override bool FilterApplies(Thing thing) =>
			thing.def.IsWithinCategory(sel);

		public override IEnumerable<ThingCategoryDef> Options() =>
			Mod.settings.OnlyAvailable ?
				base.Options().Intersect(ContentsUtility.AvailableInGame(ThingCategoryDefsOfThing)) :
				base.Options();

		public static IEnumerable<ThingCategoryDef> ThingCategoryDefsOfThing(Thing thing)
		{
			if (thing.def.thingCategories == null)
				yield break;
			foreach (var def in thing.def.thingCategories)
			{
				yield return def;
				foreach (var pDef in def.Parents)
					yield return pDef;
			}
		}

		public override string DropdownNameFor(ThingCategoryDef def) =>
			string.Concat(Enumerable.Repeat("- ", def.Parents.Count())) + base.NameFor(def);
	}

	public class ListFilterSpecialFilter : ListFilterDropDown<SpecialThingFilterDef>
	{
		public ListFilterSpecialFilter() => sel = SpecialThingFilterDefOf.AllowFresh;

		protected override bool FilterApplies(Thing thing) =>
			sel.Worker.Matches(thing);
	}

	public enum MineableType { Resource, Rock, All }
	public class ListFilterMineable : ListFilterDropDown<MineableType>
	{
		protected override bool FilterApplies(Thing thing)
		{
			switch (sel)
			{
				case MineableType.Resource: return thing.def.building?.isResourceRock ?? false;
				case MineableType.Rock: return (thing.def.building?.isNaturalRock ?? false) && (!thing.def.building?.isResourceRock ?? true);
				case MineableType.All: return thing.def.mineable;
			}
			return false;
		}
	}

	public class ListFilterHP : ListFilterWithOption<FloatRange>
	{
		public ListFilterHP() => sel = FloatRange.ZeroToOne;

		protected override bool FilterApplies(Thing thing)
		{
			float? pct = null;
			if (thing is Pawn pawn)
				pct = pawn.health.summaryHealth.SummaryHealthPercent;
			if (thing.def.useHitPoints)
				pct = (float)thing.HitPoints / thing.MaxHitPoints;
			return pct != null && sel.Includes(pct.Value);
		}

		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);
			FloatRange newRange = sel;
			Widgets.FloatRange(rect.RightPart(0.5f), id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (sel != newRange)
			{
				sel = newRange;
				return true;
			}
			return false;
		}
	}

	public class ListFilterQuality : ListFilterWithOption<QualityRange>
	{
		public ListFilterQuality() => sel = QualityRange.All;

		protected override bool FilterApplies(Thing thing) =>
			thing.TryGetQuality(out QualityCategory qc) &&
			sel.Includes(qc);

		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);
			QualityRange newRange = sel;
			Widgets.QualityRange(rect.RightPart(0.5f), id, ref newRange);
			if (sel != newRange)
			{
				sel = newRange;
				return true;
			}
			return false;
		}
	}

	public class ListFilterStuff : ListFilterDropDown<ThingDef>
	{
		protected override bool FilterApplies(Thing thing)
		{
			ThingDef stuff = thing is IConstructible c ? c.EntityToBuildStuff() : thing.Stuff;
			return 
				extraOption == 1 ? !thing.def.MadeFromStuff :
				extraOption > 1 ?	stuff?.stuffProps?.categories?.Contains(DefDatabase<StuffCategoryDef>.AllDefsListForReading[extraOption - 2]) ?? false :
				sel == null ? stuff != null :
				stuff == sel;
		}
		
		public override string NullOption() => "TD.AnyOption".Translate();
		private static List<ThingDef> stuffList = DefDatabase<ThingDef>.AllDefs.Where(d => d.IsStuff).ToList();
		public override IEnumerable<ThingDef> Options() =>
			Mod.settings.OnlyAvailable
				? stuffList.Intersect(ContentsUtility.AvailableInGame(t => t.Stuff))
				: stuffList;
		
		public override int ExtraOptionsCount => DefDatabase<StuffCategoryDef>.DefCount + 1;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.NotMadeFromStuff".Translate() : 
			DefDatabase<StuffCategoryDef>.AllDefsListForReading[ex-2]?.LabelCap;
	}

	public class ListFilterMissingBodyPart : ListFilterDropDown<BodyPartDef>
	{
		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				sel == null ? !pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				pawn.RaceProps.body.GetPartsWithDef(sel).Any(r => pawn.health.hediffSet.PartIsMissing(r));
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<BodyPartDef> Options() =>
			Mod.settings.OnlyAvailable
				? base.Options().Intersect(ContentsUtility.AvailableInGame(
					t => (t as Pawn)?.health.hediffSet.GetMissingPartsCommonAncestors().Select(h => h.Part.def) ?? Enumerable.Empty<BodyPartDef>()))
				: base.Options();

		public override string NameFor(BodyPartDef def)
		{
			string name = def.LabelCap;
			string special = def.defName; //best we got
			if (name == special)
				return name;

			return $"{name} ({special})";
		}

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "None".Translate();
	}


	public enum BaseAreas { Home, BuildRoof, NoRoof, SnowClear };
	public class ListFilterArea : ListFilterDropDown<Area>
	{
		public ListFilterArea()
		{
			extraOption = 1;
		}

		protected override Area ResolveRef(Map map) =>
			map.areaManager.GetLabeled(selName);

		public override bool ValidForAllMaps => extraOption > 0 || sel == null;

		protected override bool FilterApplies(Thing thing)
		{
			Map map = thing.MapHeld;
			IntVec3 pos = thing.PositionHeld;

			if (extraOption == 5)
				return pos.Roofed(map);

			if(extraOption == 0)
				return sel != null ? sel[pos] :
				map.areaManager.AllAreas.Any(a => a[pos]);

			switch((BaseAreas)(extraOption - 1))
			{
				case BaseAreas.Home:			return map.areaManager.Home[pos];
				case BaseAreas.BuildRoof: return map.areaManager.BuildRoof[pos];
				case BaseAreas.NoRoof:		return map.areaManager.NoRoof[pos];
				case BaseAreas.SnowClear: return map.areaManager.SnowClear[pos];
			}
			return false;
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<Area> Options() => Find.CurrentMap?.areaManager.AllAreas.Where(a => a is Area_Allowed) ?? Enumerable.Empty<Area>();
		public override string NameFor(Area o) => o.Label;

		public override int ExtraOptionsCount => 5;
		public override string NameForExtra(int ex)
		{
			if (ex == 5) return "Roofed".Translate().CapitalizeFirst();
			switch((BaseAreas)(ex - 1))
			{
				case BaseAreas.Home: return "Home".Translate();
				case BaseAreas.BuildRoof: return "BuildRoof".Translate().CapitalizeFirst();
				case BaseAreas.NoRoof: return "NoRoof".Translate().CapitalizeFirst();
				case BaseAreas.SnowClear: return "SnowClear".Translate().CapitalizeFirst();
			}
			return "???";
		}
	}

	public class ListFilterZone : ListFilterDropDown<Zone>
	{
		protected override Zone ResolveRef(Map map) =>
			map.zoneManager.AllZones.FirstOrDefault(z => z.label == selName);

		public override bool ValidForAllMaps => extraOption != 0 || sel == null;

		protected override bool FilterApplies(Thing thing)
		{
			IntVec3 pos = thing.PositionHeld;
			Zone zoneAtPos = thing.MapHeld.zoneManager.ZoneAt(pos);
			return 
				extraOption == 1 ? zoneAtPos is Zone_Stockpile :
				extraOption == 2 ? zoneAtPos is Zone_Growing :
				sel != null ? zoneAtPos == sel :
				zoneAtPos != null;
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<Zone> Options() => Find.CurrentMap?.zoneManager.AllZones ?? Enumerable.Empty<Zone>();

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) => ex == 1 ? "TD.AnyStockpile".Translate() : "TD.AnyGrowingZone".Translate();
	}

	public class ListFilterDeterioration : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			SteadyEnvironmentEffects.FinalDeteriorationRate(thing) >= 0.001f;
	}

	public enum DoorOpenFilter { Open, Close, HoldOpen, BlockedOpenMomentary }
	public class ListFilterDoorOpen : ListFilterDropDown<DoorOpenFilter>
	{
		protected override bool FilterApplies(Thing thing)
		{
			Building_Door door = thing as Building_Door;
			if (door == null) return false;
			switch (sel)
			{
				case DoorOpenFilter.Open: return door.Open;
				case DoorOpenFilter.Close: return !door.Open;
				case DoorOpenFilter.HoldOpen: return door.HoldOpen;
				case DoorOpenFilter.BlockedOpenMomentary: return door.BlockedOpenMomentary;
			}
			return false;//???
		}
		public override string NameFor(DoorOpenFilter o)
		{
			switch (o)
			{
				case DoorOpenFilter.Open: return "TD.Opened".Translate();
				case DoorOpenFilter.Close: return "VentClosed".Translate();
				case DoorOpenFilter.HoldOpen: return "CommandToggleDoorHoldOpen".Translate().CapitalizeFirst();
				case DoorOpenFilter.BlockedOpenMomentary: return "TD.BlockedOpen".Translate();
			}
			return "???";
		}
	}

	public class ListFilterThingDef : ListFilterDropDown<ThingDef>
	{
		public IntRange stackRange;
		public ListFilterThingDef()
		{
			sel = ThingDefOf.WoodLog;
		}
		protected override void PostSelected()
		{
			stackRange.min = 1;
			stackRange.max = sel.stackLimit;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref stackRange, "stackRange");
		}
		public override ListFilter Clone()
		{
			ListFilterThingDef clone = (ListFilterThingDef)base.Clone();
			clone.stackRange = stackRange;
			return clone;
		}


		protected override bool FilterApplies(Thing thing) =>
			sel == thing.def &&
			(sel.stackLimit <= 1 || stackRange.Includes(thing.stackCount));


		public override bool Ordered => true;

		public override IEnumerable<ThingDef> Options() =>
			(Mod.settings.OnlyAvailable ?
				base.Options().Intersect(ContentsUtility.AvailableInGame(t => t.def)) :
				base.Options())
			.Where(def => FindDescription.ValidDef(def));

		public override string CategoryFor(ThingDef def)
		{
			if (def.IsBlueprint)
				return "(Blueprint)";

			if (def.IsFrame)
				return "(Frame)";

			if (def.FirstThingCategory?.LabelCap.ToString() is string label)
			{
				if (label == "Misc")
					return $"{label} ({def.FirstThingCategory.parent.LabelCap})";
				return label;
			}

			//catchall for unminifiable buildings.
			if (def.designationCategory?.LabelCap.ToString() is string label2)
			{
				if (label2 == "Misc")
					return $"{label2} ({ThingCategoryDefOf.Buildings.LabelCap})";
				return label2;
			}

			if (typeof(Pawn).IsAssignableFrom(def.thingClass))
				return "Living";

			if (typeof(Mineable).IsAssignableFrom(def.thingClass))
				return "Mineable";

			return "(Other)";
		}


		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			if (sel.stackLimit > 1)
			{
				IntRange newRange = stackRange;
				Widgets.IntRange(rect, id, ref newRange, 1, sel.stackLimit);
				if (newRange != stackRange)
				{
					stackRange = newRange;
					return true;
				}
			}
			return false;
		}
	}


	public class ListFilterModded : ListFilterDropDown<ModContentPack>
	{
		public ListFilterModded()
		{
			sel = LoadedModManager.RunningMods.First(mod => mod.IsCoreMod);
		}


		public override bool UsesResolveName => true;
		protected override string MakeSaveName() => sel.PackageIdPlayerFacing;

		protected override ModContentPack ResolveName() =>
			LoadedModManager.RunningMods.FirstOrDefault(mod => mod.PackageIdPlayerFacing == selName);


		protected override bool FilterApplies(Thing thing) =>
			sel == thing.ContentSource;

		public override IEnumerable<ModContentPack> Options() =>
			LoadedModManager.RunningMods.Where(mod => mod.AllDefs.Any(d => d is ThingDef));

		public override string NameFor(ModContentPack o) => o.Name;
	}


	public class ListFilterOnScreen : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing.OccupiedRect().Overlaps(Find.CameraDriver.CurrentViewRect);

		public override bool CurrentMapOnly => true;
	}


	public class ListFilterStat : ListFilterDropDown<StatDef>
	{
		FloatRange valueRange;

		public ListFilterStat()
		{
			sel = StatDefOf.GeneralLaborSpeed;
		}

		protected override void PostSelected()
		{
			valueRange = new FloatRange(sel.minValue, sel.maxValue);
			lBuffer = rBuffer = null;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref valueRange, "valueRange");
		}
		public override ListFilter Clone()
		{
			ListFilterStat clone = (ListFilterStat)base.Clone();
			clone.valueRange = valueRange;
			return clone;
		}

		protected override bool FilterApplies(Thing t) =>
			sel.Worker.ShouldShowFor(StatRequest.For(t)) &&
			valueRange.Includes(t.GetStatValue(sel, cacheStaleAfterTicks: 1));


		public override IEnumerable<StatDef> Options() =>
			base.Options().Where(d => !d.alwaysHide);


		public override string CategoryFor(StatDef def) =>
			def.category.LabelCap;


		public override string NameFor(StatDef def) =>
			def.LabelForFullStatListCap;


		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			FloatRange newRange = valueRange;

			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect, 
				$"{valueRange.min.ToStringByStyle(sel.toStringStyle, sel.toStringNumberSense)} - {valueRange.max.ToStringByStyle(sel.toStringStyle, sel.toStringNumberSense)}");
			Text.Anchor = TextAnchor.UpperLeft;
			
			return false;
		}

		private string lBuffer, rBuffer;
		private string controlNameL, controlNameR;
		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			if (locked) return false;

			listing.Gap(listing.verticalSpacing);

			Rect rect = listing.GetRect(Text.LineHeight);
			Rect lRect = rect.LeftPart(.45f);
			Rect rRect = rect.RightPart(.45f);

			//From inner TextFieldNumeric
			controlNameL = "TextField" + lRect.y.ToString("F0") + lRect.x.ToString("F0");
			controlNameR = "TextField" + rRect.y.ToString("F0") + rRect.x.ToString("F0");

			FloatRange oldRange = valueRange;
			if (sel.toStringStyle == ToStringStyle.PercentOne || sel.toStringStyle == ToStringStyle.PercentTwo || sel.toStringStyle == ToStringStyle.PercentZero)
			{
				Widgets.TextFieldPercent(lRect, ref valueRange.min, ref lBuffer, float.MinValue, float.MaxValue);
				Widgets.TextFieldPercent(rRect, ref valueRange.max, ref rBuffer, float.MinValue, float.MaxValue);
			}
/*			else if(sel.toStringStyle == ToStringStyle.Integer)
			{
				Widgets.TextFieldNumeric<int>(lRect, ref valueRangeI.min, ref lBuffer, float.MinValue, float.MaxValue);
				Widgets.TextFieldNumeric<int>(rRect, ref valueRangeI.max, ref rBuffer, float.MinValue, float.MaxValue);
			}*/
			else
			{
				Widgets.TextFieldNumeric<float>(lRect, ref valueRange.min, ref lBuffer, float.MinValue, float.MaxValue);
				Widgets.TextFieldNumeric<float>(rRect, ref valueRange.max, ref rBuffer, float.MinValue, float.MaxValue);
			}

			/* TODO: figure this out. Unity seems to have override the tab.
			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab)
			{
				//Why wrong place.
				GUI.FocusControl(controlNameR);
				Event.current.Use();
			}
			*/

			return valueRange != oldRange;
		}

		protected override void DoFocus()
		{
			GUI.FocusControl(controlNameL);
		}
	}
}
