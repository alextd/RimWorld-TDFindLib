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
	// Introducin Thing Query: Like a ThingFilter, but more!
	public class ThingQueryDef : ThingQuerySelectableDef
	{
		public Type queryClass;

		public override IEnumerable<string> ConfigErrors()
		{
			if (queryClass == null)
				yield return "ThingQueryDef needs queryClass set";
		}
	}

	public abstract class ThingQuery : IExposable
	{
		public ThingQueryDef def;

		public IQueryHolder parent;
		public IQueryHolder Parent => parent;

		public IQueryHolder RootHolder => parent?.RootHolder;


		protected int id; //For Widgets.draggingId purposes
		private static int nextID = 1;
		protected ThingQuery() { id = nextID++; }


		private bool enabled = true; //simply turn off but keep in list
		public bool Enabled => enabled && DisableReasonCurMap == null;
		public static readonly Color DisabledOverlayColor = Widgets.WindowBGFillColor * new Color(1,1,1,.5f);

		private bool _include = true; //or exclude
		public bool include
		{
			get => _include;
			set
			{
				_include = value;
				_label = null;
			}
		}

		private string _label;
		public string Label
		{
			get {
				if (_label == null)
				{
					_label = def.LabelCap;
					if (!include)
						_label = "TD.NOT".Translate().Colorize(Color.red) + " " + _label;
				}
				return _label;
			}
		}



		// Okay, save/load. The basic gist here is:
		// During ExposeData loading, ResolveName is called for globally named things (defs)
		// But anything with a local reference (Zones) needs to resolve that ref on a map, or a game (Factions)
		// Queries loaded from storage need to be cloned to a map to be used

		public virtual bool UsesResolveName => false;
		public virtual bool UsesResolveRef => false;

		public virtual void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref enabled, "enabled", true);
			Scribe_Values.Look(ref _include, "include", true);

			if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
			{
				DoResolveName();
			}
		}

		// Make clone and possibly postprocess.
		public virtual ThingQuery MakeClone() => Clone();

		// Internal Cloning fields
		protected virtual ThingQuery Clone()
		{
			ThingQuery clone = ThingQueryMaker.MakeQuery(def);
			clone.enabled = enabled;
			clone.include = include;


			//No - Will be set in QueryHolder.Add or QueryHolder's ExposeData on LoadingVars step
			//clone.parent = newHolder; 

			return clone;
		}
		public virtual void DoResolveName() { }
		public virtual void DoResolveRef(Map map) { }


		public void Apply( /* const */ List<Thing> inList, List<Thing> outList)
		{
			outList.Clear();

			foreach (Thing thing in inList)
				if (AppliesTo(thing))
					outList.Add(thing);
		}

		//This can be problematic for minified things: We want the qualities of the inner things,
		// but position/status of outer thing. So it just checks both -- but then something like 'no stuff' always applies. Oh well
		public bool AppliesTo(Thing thing)
		{
			bool applies = AppliesDirectlyTo(thing);
			if (!applies && thing.GetInnerThing() is Thing innerThing && innerThing != thing)
				applies = AppliesDirectlyTo(innerThing);

			return applies == include;
		}

		public abstract bool AppliesDirectlyTo(Thing thing);


		private bool shouldFocus;
		public void Focus() => shouldFocus = true;
		protected virtual void DoFocus() { }
		public virtual bool Unfocus() => false;


		// Seems to be GameFont.Small on load so we're good
		public static float? incExcWidth;
		public static float IncExcWidth =>
			incExcWidth.HasValue ? incExcWidth.Value :
			(incExcWidth = Mathf.Max(Text.CalcSize("TD.IncludeShort".Translate()).x, Text.CalcSize("TD.ExcludeShort".Translate()).x)).Value;

		public Rect usedRect;
		public (bool, bool) Listing(Listing_StandardIndent listing, bool locked)
		{
			Rect rowRect = listing.GetRect(Text.LineHeight + listing.verticalSpacing); //ends up being 22 which is height of Text.CalcSize 


			if (!include)
			{
				Widgets.DrawBoxSolid(rowRect.ContractedBy(2), new Color(1, 0, 0, 0.1f));
				GUI.color = new Color(1, 0, 0, 0.25f);
				Widgets.DrawLineHorizontal(rowRect.x + 2, rowRect.y + Text.LineHeight / 2, rowRect.width - 4);
				GUI.color = Color.white;
			}
			WidgetRow row = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown, rowRect.width);

			bool changed = false;
			bool delete = false;

			if (!locked)
			{
				//Clear button
				if (row.ButtonIcon(TexCommand.ClearPrioritizedWork, "TD.DeleteThisQuery".Translate()))
				{
					delete = true;
					changed = true;
				}

				//Toggle button
				if (row.ButtonIcon(enabled ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, "TD.EnableThisQuery".Translate()))
				{
					enabled = !enabled;
					changed = true;
				}

				//Include/Exclude
				if (row.ButtonText(include ? "TD.IncludeShort".Translate() : "TD.ExcludeShort".Translate(),
					"TD.IncludeOrExcludeThingsMatchingThisQuery".Translate(),
					fixedWidth: IncExcWidth))
				{
					include = !include;
					changed = true;
				}
			}


			//Draw option row
			rowRect.width -= (rowRect.xMax - row.FinalX);
			Rect fullRect = rowRect;
			fullRect.xMin = 0;//Since it's in a listing group, left side is 0. TODO with 2 colunmns??
			changed |= DrawMain(rowRect, locked, fullRect);
			changed |= DrawUnder(listing, locked);
			if (shouldFocus)
			{
				DoFocus();
				shouldFocus = false;
			}

			if (DisableReason is string reason)
			{
				Widgets.DrawBoxSolidWithOutline(rowRect, DisabledBecauseReasonOverlayColor, Color.red);

				TooltipHandler.TipRegion(rowRect, reason);
			}

			if (Event.current.type == EventType.Layout)
			{
				usedRect = rowRect;
				usedRect.yMax = listing.CurHeight;
			}
			if (!enabled)
			{
				Widgets.DrawBoxSolid(usedRect, DisabledOverlayColor);
			}

			listing.Gap(listing.verticalSpacing);
			return (changed, delete);
		}


		public virtual bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			Widgets.Label(rect, Label);
			return false;
		}
		protected virtual bool DrawUnder(Listing_StandardIndent listing, bool locked) => false;

		public virtual bool CurMapOnly => false;

		public virtual string DisableReason => null;
		public virtual string DisableReasonCurMap => null;
		public static readonly Color DisabledBecauseReasonOverlayColor = new Color(0.5f, 0, 0, 0.25f);

		public static void DoFloatOptions(List<FloatMenuOption> options)
		{
			if (options.NullOrEmpty())
				Messages.Message("TD.ThereAreNoOptionsAvailablePerhapsYouShouldUncheckOnlyAvailableThings".Translate(), MessageTypeDefOf.RejectInput, false);
			else
				Find.WindowStack.Add(new FloatMenu(options));
		}

		//Probably a good filter
		public static bool ValidDef(ThingDef def) =>
			(def.category != ThingCategory.Ethereal || def.selectable) && // anything EtherealThingBase that isn't selectable is out. e.g. Flashstorms
			!typeof(Mote).IsAssignableFrom(def.thingClass) &&
			!typeof(Projectile).IsAssignableFrom(def.thingClass) &&
			def.drawerType != DrawerType.None &&  //non-drawers are weird abstract things.
			def.category != ThingCategory.PsychicEmitter; //Solar pinhole why? can't you stay ThingCategory.Ethereal
	}

	public class FloatMenuOptionAndRefresh : FloatMenuOption
	{
		ThingQuery owner;
		Color color = Color.white;
		public FloatMenuOptionAndRefresh(string label, Action action, ThingQuery query, Color? color = null)
			: base(label, action)
		{
			owner = query;
			if(color.HasValue)
				this.color = Color.Lerp(Color.white, color.Value, .2f);
		}
		public FloatMenuOptionAndRefresh(string label, Action action, ThingQuery query, Texture2D itemIcon, Color? iconColor = null)
			: base(label, action, itemIcon, iconColor ?? Color.white)
		{
			owner = query;
		}
		public FloatMenuOptionAndRefresh(string label, Action action, ThingQuery query, ThingDef shownItemForIcon, ThingStyleDef thingStyle = null, bool forceBasicStyle = false)
			: base(label, action, shownItemForIcon, thingStyle, forceBasicStyle)
		{
			owner = query;
		}

		public override bool DoGUI(Rect rect, bool colonistOrdering, FloatMenu floatMenu)
		{
			Color oldColor = GUI.color;
			GUI.color = color;

			bool result = base.DoGUI(rect, colonistOrdering, floatMenu);

			GUI.color = oldColor;

			if (result)
				owner.RootHolder.Root_NotifyUpdated();

			return result;
		}
	}

	//automated ExposeData + Clone 
	public abstract class ThingQueryWithOption<T> : ThingQuery
	{
		// selection
		protected T _sel;
		protected string selName;// if UsesSaveLoadName,  = SaveLoadXmlConstants.IsNullAttributeName;
		private int _extraOption; //0 meaning use _sel, what 1+ means is defined in subclass

		// A subclass with extra fields needs to override ExposeData and Clone to copy them

		// Selection error is probably set on load when selection is invalid (missing mod?)
		// Or it's set when binding to a map and it can't resolve a ref 
		// There's a second selectionErrorCurMap for the current bind only
		// Whereas selectionError is kept around, for an allmaps search that fails only on one map - other maps still use the filter, but the error is remembered for UI
		public string selectionError;
		public string selectionErrorCurMap;
		public bool refErrorReported; // Only report it once - not once each map for a refreshing allmaps search. 
		// (It will not reset when you change the map selection though, only when you set the query selection again)

		public override string DisableReason => selectionError;
		public override string DisableReasonCurMap => selectionErrorCurMap;

		// would like this to be T const * sel;
		// TODO: Is this needed? was _sel private?
		public ref T selByRef => ref _sel;
		public T sel
		{
			get => _sel;
			set
			{
				_sel = value;
				_extraOption = 0;
				selectionError = selectionErrorCurMap = null;
				refErrorReported = false;
				if (SaveLoadByName) selName = MakeSaveName();
				if (UsesResolveRef) RootHolder?.Root_NotifyRefUpdated();
				PostProcess();
				PostChosen();
			}
		}

		// A subclass should often set sel in the constructor
		// which will call the property setter above
		// If the default is null, and there's no PostSelected to do,
		// then it's fine to skip defining a constructor
		protected ThingQueryWithOption()
		{
			if (SaveLoadByName)
				selName = SaveLoadXmlConstants.IsNullAttributeName;
		}


		// PostProcess is called any time the selection is set: even after loading and cloning, etc.
		//  good for setting other fields based on sel: options to select from usually.
		//  such fields should not be saved to file, and are PostProcesses on load
		// PostChosen is called when the user selects the option (after a call to PostProcess)

		// A subclass with fields whose validity depends on the selection should override these
		//  PostProcess: to load extra data about the selection - MUST handle null.
		//   e.g. Specific Thing query sets the abs max range based on the stackLimit
		//   e.g. thoughts that have a range of stages, based on the selected def.
		//   e.g. the hediff query has a range of severity, which depends on the selected hediff, so the selectable range needs to be set here
		//  PostChosen: to set a default value, that is valid for the selection
		//   e.g. Specific Thing query sets the default chosen range based on the stackLimit
		//   e.g. NOT with the skill query which has a range 0-20, but that's valid for all skills, so no need to set per def
		// Few subclasses need PostProcess, and they will often also need PostChosen.
		protected virtual void PostProcess() { }
		protected virtual void PostChosen() { }

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
				selectionError = selectionErrorCurMap = null;
				refErrorReported = false;
				selName = null;
			}
		}

		//Okay, so, references.
		//A simple query e.g. string search is usable everywhere.
		//In-game, as an alert, in a saved search to load in, saved to file to load into another game, etc.
		//ExposeData and Clone can just copy that string, because a string is the same everywhere.
		//But a query that references in-game things can't be used universally.
		//Queries can be saved outside a running world, so even things like defs might not exist when loaded with different mods.
		//When such a query is run in-game, it does of course set 'sel' and reference it like normal
		//But when such a query is saved, it cannot be bound to an instance or even an ILoadReferencable id
		//So ExposeData saves and loads 'string selName' instead of the 'T sel'
		//When editing that query when inactive, that's fine, sel isn't set but selName is - so selName should be readable.
		//TODO: allow editing of selName: e.g. You can't add a "Stockpile Zone 1" query without that zone existing in-game.

		//ThingQuerys have 3 levels of saving, sort of like ExposeData's 3 passes.
		//Raw values can be saved/loaded by value easily in ExposeData.
		//Then there's UsesResolveName, and UsesResolveRef, which both SaveLoadByName
		//All SaveLoadByName queries are simply saved by a string name in ExposeData
		// - So it can be loaded into another game
		// - if that name cannot be resolved, the name is still kept instead of writing null
		//For loading, there's two different times to load:
		//Queries that UsesResolveName can be resolved after the game starts up (e.g. defs),
		// - ResolveName is called from ExposeData, ResolvingCrossRefs
		// - Queries that fail to resolve name are disabled until reset (at least, the DropDown subclasses)
		//Queries that UsesResolveRef must be resolved in-game on a map (e.g. Zones, ILoadReferenceable, Factions)
		// - Queries that are loaded and inactive do not call ResolveRef and only have selName set.
		// - Queries need their refs resolved when a search is performed - e.g. when QuerySearch calls BindToMap()
		// - BindToMap will remember it's bound to that map and not bother to re-bind
		// - A Search that runs on multiple maps will bind to each map and resolve query refs dynamically.
		// - This of course will produce error messages if those can't be resolved on all maps

		protected readonly static bool IsDef = typeof(Def).IsAssignableFrom(typeof(T));
		protected readonly static bool IsRef = typeof(ILoadReferenceable).IsAssignableFrom(typeof(T));
		protected readonly static bool IsEnum = typeof(T).IsEnum;

		public override bool UsesResolveName => IsDef;
		public override bool UsesResolveRef => IsRef;
		private bool SaveLoadByName => UsesResolveName || UsesResolveRef;
		protected virtual string MakeSaveName() => sel?.ToString() ?? SaveLoadXmlConstants.IsNullAttributeName;

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref _extraOption, "ex");
			if (_extraOption > 0)
			{
				if (Scribe.mode == LoadSaveMode.LoadingVars)
					extraOption = _extraOption;	// property setter to set other fields null: TODO: they already are null, right?

				// No need to worry about sel or selName, we're done!
				return;
			}

			//Oh Jesus T can be anything but Scribe doesn't like that much flexibility so here we are:
			//(avoid using property 'sel' setter!)
			if (SaveLoadByName)
			{
				// Of course between games you can't get references so just save by name should be good enough
				// (even if it's from the same game, it can still resolve the reference all the same)

				// Saving a null selName saves "IsNull"
				Scribe_Values.Look(ref selName, "refName");

				// ResolveName() will be called on startup
				// ResolveRefs() will be called when a map is set
			}
			else if (typeof(IExposable).IsAssignableFrom(typeof(T)))
			{
				// TODO: I don't think any query uses this, 
				// It used to store a Query selection in here and it needed this
				// Oh well, might as well keep it around. Anything ILoadReferencable has already been handled and won't get here.
				Scribe_Deep.Look(ref _sel, "sel");
			}
			// Any subclass that RangeUB this has to ExposeData itself because I don't think we can force _sel to ref a struct
			else if (_sel is FloatRangeUB)
			{
				// Scribe_Values.Look(ref sel.range, "sel");
			}
			else if (_sel is IntRangeUB)
			{
				// Scribe_Values.Look(ref sel.range, "sel");
			}
			else
				Scribe_Values.Look(ref _sel, "sel");

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
				PostProcess();
		}

		public override ThingQuery MakeClone()
		{
			ThingQueryWithOption<T> clone = (ThingQueryWithOption<T>)base.MakeClone();
			clone.PostProcess();
			return clone;
		}
		protected override ThingQuery Clone()
		{
			ThingQueryWithOption<T> clone = (ThingQueryWithOption<T>)base.Clone();

			clone.extraOption = extraOption;
			if (extraOption > 0)
				return clone;

			if (SaveLoadByName)
				clone.selName = selName;

			if(!UsesResolveRef)
				clone._sel = _sel;  //todo handle if sel needs to be deep-copied. Perhaps sel should be T const * sel...

			clone.selectionError = selectionError;
			clone.selectionErrorCurMap = selectionErrorCurMap;
			//selectionErrorReported back to false for clone

			return clone;
		}

		// Subclasses where SaveLoadByName is true need to override ResolveName() or ResolveRef()
		// (unless it's just a Def, already handled)
		// return matching object based on selName (selName will not be "null")
		// returning null produces a selection error and the query will be disabled
		public override void DoResolveName()
		{
			if (!UsesResolveName || extraOption > 0) return;

			if (selName == SaveLoadXmlConstants.IsNullAttributeName)
			{
				//if(NullOption() != null)	//Let's not double check on load, it's not too bad.

				_sel = default; //can't use null because generic T isn't bound as reftype
			}
			else
			{
				_sel = ResolveName();

				if(_sel == null)
				{
					selectionError = "TD.Missing01".Translate(def.LabelCap, selName);
					selectionErrorCurMap = selectionError; // Sort of redundant to use "curmap" here but it does apply to whatever the current map is because it always applies
					Verse.Log.Warning("TD.SearchTriedToLoad".Translate(RootHolder.Name, def.LabelCap, selName));
				}
				else
					selectionError = null;
			}
		}

		Map refErrorMap;
		public override void DoResolveRef(Map map)
		{
			if (!UsesResolveRef || extraOption > 0) return;

			if (map == null)
				RefError(null);

			if (selName == SaveLoadXmlConstants.IsNullAttributeName)
			{
				_sel = default; //can't use null because generic T isn't bound as reftype
			}
			else
			{
				_sel = ResolveRef(map);

				if (_sel == null)
					RefError(map);
				else
				{
					selectionErrorCurMap = null;
					if (refErrorMap == map)
					{
						refErrorMap = null;
						selectionError = null;
						refErrorReported = false;
					}
				}
			}

			PostProcess();
		}

		private void RefError(Map map)
		{
			selectionErrorCurMap = map == null ? "TD.Filter01NeedsAMap".Translate(def.LabelCap, selName)
				: "TD.Missing01On2".Translate(def.LabelCap, selName, map.Parent.LabelCap);
			if (!refErrorReported)
			{
				// Report the first one, even if there's many. User will have to deal with them one-by-one.
				Messages.Message(map == null ? "TD.Search0TriedToLoad1FilterButNoMapToFind2".Translate(RootHolder.Name, def.LabelCap, selName) :
					"TD.SearchTriedToLoadOnMap".Translate(RootHolder.Name, def.LabelCap, selName, map?.Parent.LabelCap ?? "TD.NoMap".Translate()),
					MessageTypeDefOf.RejectInput, false);

				refErrorMap = map;
				selectionError = selectionErrorCurMap;
				refErrorReported = true;
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

	public abstract class ThingQueryDropDown<T> : ThingQueryWithOption<T>
	{
		private string GetSelLabel()
		{
			if (selectionError != null) // Not selectionErrorCurMap - globally remembered error.
				return selName;

			if (extraOption > 0)
				return NameForExtra(extraOption);

			if (sel != null)
				return NameFor(sel);

			if (UsesResolveRef && !RootHolder.Root_Active && selName != SaveLoadXmlConstants.IsNullAttributeName)
				return selName;

			return NullOption() ?? "??Null selection??";
		}

		// Subclass Options() don't need to handle simple enums or defs
		// Often they will use Mod.settings.OnlyAvailable to show a subset of options when shift is held
		// That would also use ContentsUtility.AvailableInGame for ease of searching inside all things.
		// This subset should be passed to base.Options().Intersect() so they are consistent ordering.
		public virtual IEnumerable<T> Options()
		{
			if (IsEnum)
				return Enum.GetValues(typeof(T)).OfType<T>();
			if (IsDef)
				return GenDefDatabase.GetAllDefsInDatabaseForDef(typeof(T)).Cast<T>();
			throw new NotImplementedException();
		}

		// Override this to group your T options into submenu categories
		public virtual string CategoryFor(T def) => null;

		private Dictionary<string, List<T>> _optionCategories = new();
		private Dictionary<string, List<T>> OptionCategories()
		{
			_optionCategories.Clear();
			int i = 0;
			foreach (T def in Options())
			{
				i++;
				string cat = CategoryFor(def);

				List<T> options;
				if (!_optionCategories.TryGetValue(cat, out options))
				{
					options = new();
					_optionCategories[cat] = options;
				}

				options.Add(def);
			}
			Log.Message($"There's {i} things for {GetType()}");
			return _optionCategories;
		}

		public virtual string NameFor(T o) => o is Def def ? def.LabelCap.RawText : typeof(T).IsEnum ? o.TranslateEnum() : o.ToString();

		// dropdown menu options
		public virtual bool Ordered => false;
		public virtual string DropdownNameFor(T o) => NameFor(o);
		public virtual Texture2D IconTexFor(T o) => null;
		public virtual ThingDef IconDefFor(T o) => null;
		private FloatMenuOption FloatMenuFor(T o)
		{
			if (IconTexFor(o) is Texture2D tex)
				return new FloatMenuOptionAndRefresh(DropdownNameFor(o), () => sel = o, this, tex == BaseContent.BadTex ? BaseContent.ClearTex : tex);

			if(IconDefFor(o) is ThingDef def)
				return new FloatMenuOptionAndRefresh(DropdownNameFor(o), () => sel = o, this, def);

			return new FloatMenuOptionAndRefresh(DropdownNameFor(o), () => sel = o, this);
		}



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

		public override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			bool changeSelection = false;
			bool changed = false;
			if (HasCustom)
			{
				// Label, Selection option button on left, custom on the remaining rect
				WidgetRow row = new WidgetRow(rect.x, rect.y);
				row.Label(Label);
				changeSelection = row.ButtonText(GetSelLabel());

				Rect customRect = rect;
				customRect.xMin = row.FinalX;
				changed = DrawCustom(customRect, row, fullRect);
			}
			else
			{
				//Just the label on left, and selected option button on right
				base.DrawMain(rect, locked, fullRect);
				Rect buttRect = fullRect.RightPartClamped(0.4f, Text.CalcSize(Label).x);
				string label = GetSelLabel();
				changeSelection = Widgets.ButtonText(buttRect, label);
			}
			if (changeSelection)
			{
				List<FloatMenuOption> options = new();

				if (NullOption() is string nullOption)
					options.Add(new FloatMenuOptionAndRefresh(nullOption, () => sel = default, this, Color.red)); //can't null because T isn't bound as reftype

				if (UsesCategories && Options().Count() > 10)
				{
					Dictionary<string, List<T>> categories = OptionCategories();

					foreach (string catLabel in categories.Keys)
						options.Add(new FloatMenuOption(catLabel, () =>
						{
							List<FloatMenuOption> catOptions = new();
							foreach (T o in Ordered ? categories[catLabel].AsEnumerable().OrderBy(o => NameFor(o)).ToList() : categories[catLabel])
								catOptions.Add(FloatMenuFor(o));
							DoFloatOptions(catOptions);
						}));
				}
				else
				{
					foreach (T o in Ordered ? Options().OrderBy(o => NameFor(o)) : Options())
						options.Add(FloatMenuFor(o));
				}

				foreach (int ex in ExtraOptions())
					options.Add(new FloatMenuOptionAndRefresh(NameForExtra(ex), () => extraOption = ex, this, Color.yellow));

				DoFloatOptions(options);
			}
			return changed;
		}

		// Subclass can override DrawCustom to draw anything custom
		// (otherwise it's just label and option selection button)
		// Use either rect or WidgetRow in the implementation
		public virtual bool DrawCustom(Rect rect, WidgetRow row, Rect fullRect) => throw new NotImplementedException();

		// Auto detection of subclasses that use DrawCustom:
		private static readonly HashSet<Type> customDrawers = null;
		private bool HasCustom => customDrawers?.Contains(GetType()) ?? false;

		// Auto detection of subclasses that use Dropdown Categories:
		private static readonly HashSet<Type> categoryUsers = null;
		private bool UsesCategories => categoryUsers?.Contains(GetType()) ?? false;
		static ThingQueryDropDown()//<T>	//Remember there's a customDrawers for each <T> but functionally that doesn't change anything
		{
			Type baseType = typeof(ThingQueryDropDown<T>);
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

	public abstract class ThingQueryFloatRange : ThingQueryWithOption<FloatRangeUB>
	{
		public virtual float Min => 0f;
		public virtual float Max => 1f;
		public virtual ToStringStyle Style => ToStringStyle.PercentZero;

		public ThingQueryFloatRange() => sel = new FloatRangeUB(Min, Max);


		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref _sel.range, "sel");
		}

		public override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			base.DrawMain(rect, locked, fullRect);
			return TDWidgets.FloatRangeUB(fullRect.RightHalfClamped(Text.CalcSize(Label).x), id, ref selByRef, valueStyle: Style);
		}
	}

	public abstract class ThingQueryIntRange : ThingQueryWithOption<FloatRangeUB>
	{
		public virtual int Min => 0;
		public virtual int Max => 1;

		public ThingQueryIntRange() => sel = new IntRangeUB(Min, Max);


		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref _sel.range, "sel");
		}

		public override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			base.DrawMain(rect, locked, fullRect);
			return TDWidgets.IntRangeUB(fullRect.RightHalfClamped(Text.CalcSize(Label).x), id, ref selByRef);
		}
	}

	public abstract class ThingQueryMask<T> : ThingQuery
	{
		public abstract List<T> Options { get; }
		public abstract string GetOptionLabel(T o);
		public abstract int CompareSelector(T o);

		public string label;
		public List<T> mustHave = new(), cantHave = new();

		protected override ThingQuery Clone()
		{
			ThingQueryMask<T> clone = (ThingQueryMask<T>)base.Clone();
			clone.mustHave = mustHave.ToList();
			clone.cantHave = cantHave.ToList();
			clone.label = label;
			return clone;
		}
		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Collections.Look(ref mustHave, "mustHave");
			Scribe_Collections.Look(ref cantHave, "cannotHave");

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
				SetSelLabel();
		}

		static readonly Color mustColor = Color.Lerp(Color.green, Color.gray, .5f);
		static readonly Color cantColor = Color.Lerp(Color.red, Color.gray, .5f);
		public void SetSelLabel()
		{
			string must = string.Join(", ", mustHave.Select(GetOptionLabel));
			string cant = string.Join(", ", cantHave.Select(GetOptionLabel));

			StringBuilder sb = new();
			if (must.Length > 0)
			{
				sb.Append(must.Colorize(mustColor));
			}
			if (cant.Length > 0)
			{
				if (sb.Length > 0)
					sb.Append(" ; ");
				sb.Append(cant.Colorize(cantColor));
			}
			if (sb.Length == 0)
				label = "TD.AnyOption".Translate();
			else
				label = sb.ToString();
		}

		public override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			base.DrawMain(rect, locked, fullRect);
			if (Widgets.ButtonText(fullRect.RightPart(.7f), label))
			{
				List<FloatMenuOption> layerOption = new List<FloatMenuOption>();
				foreach (T option in Options)
				{
					layerOption.Add(new FloatMenuOptionAndRefresh(
						GetOptionLabel(option),
						() => {
							if (Event.current.button == 1)
							{
								mustHave.Remove(option);
								cantHave.Remove(option);
							}
							else if (mustHave.Contains(option))
							{
								mustHave.Remove(option);

								cantHave.Add(option);
								cantHave.SortBy(CompareSelector);
							}
							else if (cantHave.Contains(option))
							{
								cantHave.Remove(option);
							}
							else
							{
								mustHave.Add(option);
								cantHave.SortBy(CompareSelector);
							}
							SetSelLabel();
						},
						this,
						mustHave.Contains(option) ? Widgets.CheckboxOnTex
						: cantHave.Contains(option) ? Widgets.CheckboxOffTex
						: Widgets.CheckboxPartialTex));
				}

				Find.WindowStack.Add(new FloatMenu(layerOption));
			}
			return false;
		}
	}
}
