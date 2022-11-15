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

			if (UsesResolveRef && !RootFindDesc.active && selName != SaveLoadXmlConstants.IsNullAttributeName)
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
}
