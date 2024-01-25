using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace TD_Find_Lib
{
	// Your standard popup editor.
	public class SearchEditorWindow : SearchViewerWindow
	{
		private Action<QuerySearch> onCloseIfChanged;
		public SearchEditorWindow(QuerySearch search, string transferTag, Action<QuerySearch> onCloseIfChanged = null) : base(search, transferTag)
		{
			title = "TD.Editing".Translate();
			permalocked = false;

			this.onCloseIfChanged = onCloseIfChanged;
		}

		public override void PostClose()
		{
			if (Search.changed)
				onCloseIfChanged?.Invoke(Search);
		}
	}

	// Just view, no editing.
	public class SearchViewerWindow : QueryDrawerWindow
	{
		protected QuerySearch Search => filter as QuerySearch;

		public SearchViewerWindow(QuerySearch search, string tag) : base(search, tag)
		{
			title = "TD.Viewing".Translate();
			showNameAfterTitle = true;

			permalocked = true;
		}

		public override void DrawIcons(WidgetRow row)
		{
			base.DrawIcons(row);

			if (Find.CurrentMap != null &&
				row.ButtonIcon(FindTex.List, "TD.ListThingsMatchingThisSearch".Translate()))
			{
				Find.WindowStack.Add(new ResultThingListWindow(Search.CloneForUseSingle()));
			}

#if DEBUG
			if (DebugSettings.godMode && row.ButtonIcon(FindTex.Infinity))
				UnitTests.Run();
#endif
		}
	}


	// Editor window with closing confirmation to revert, restoring the filters and listtype
	// Doesn't change name or map types.
	// For mods to use.
	public abstract class SearchEditorRevertableWindow : SearchEditorWindow
	{
		QuerySearch originalSearch;
		public SearchEditorRevertableWindow(QuerySearch search, string transferTag) : base(search, transferTag)
		{
			search.changed = false;
			originalSearch = search.CloneInactive();
		}

		public override void Import(QuerySearch newSearch)
		{
			ImportInto(newSearch, Search);
		}
			
		public static void ImportInto(QuerySearch sourceSeach, QuerySearch destSearch)
		{
			// Keep name and map type, only take these:
			destSearch.parameters.listType = sourceSeach.parameters.listType;
			destSearch.Children.Import(sourceSeach.Children);

			destSearch.UnbindMap();

			destSearch.changedSinceRemake = true;
			destSearch.changed = true;
		}

		public override void PostClose()
		{
			if (filter.changed)
			{
				Verse.Find.WindowStack.Add(new Dialog_MessageBox(
					null,
					"Confirm".Translate(), null,
					"No".Translate(), () => Import(originalSearch),
					"TD.KeepChanges".Translate(),
					true, null,
					delegate () { }// I dunno who wrote this class but this empty method is required so the window can close with esc because its logic is very different from its base class
					)); ;
			}
		}
	}

	public class HolderEditorWindow : QueryDrawerWindow
	{
		//Actually all that's needed.
		public HolderEditorWindow(QueryHolder holder, string transferTag) : base(holder, transferTag)
		{ }
	}

	public class HolderEditorRevertableWindow : HolderEditorWindow
	{
		QuerySearch originalSearch;	// roundabout but works
		public HolderEditorRevertableWindow(QueryHolder holder, string transferTag) : base(holder, transferTag)
		{
			holder.changed = false;
			originalSearch = holder.CloneAsSearch();
		}
		public override void PostClose()
		{
			if (filter.changed)
			{
				Verse.Find.WindowStack.Add(new Dialog_MessageBox(
					null,
					"Confirm".Translate(), null,
					"No".Translate(), () => Import(originalSearch),
					"TD.KeepChanges".Translate(),
					true, null,
					delegate () { }// I dunno who wrote this class but this empty method is required so the window can close with esc because its logic is very different from its base class
					)); ;
			}
		}
	}

	// Base class for mods to subclass, also SearchEditorWindow does.
	public abstract class QueryDrawerWindow : Window
	{
		public QueryHolder filter;
		public string transferTag;

		private bool _locked;
		public bool locked
		{
			get => _locked || permalocked;
			set => _locked = value;
		}

		public bool permalocked;

		public bool showNameAfterTitle;
		public string title;


		public QueryDrawerWindow()
		{
			onlyOneOfTypeAllowed = false;
			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			doCloseX = true;
			closeOnAccept = false;
		}
		public QueryDrawerWindow(QueryHolder search, string transferTag) : this()
		{
			this.filter = search;
			this.transferTag = transferTag;
		}

		public override void OnCancelKeyPressed()
		{
			if (!filter.Unfocus())
				base.OnCancelKeyPressed();
		}

		public override void Notify_ClickOutsideWindow()
		{
			filter.Unfocus();
		}

		public override void PostOpen()
		{
			base.PostOpen();

			if (Find.WindowStack.Windows.FirstOrDefault(w => w != this && w is QueryDrawerWindow dw && GetType() == dw.GetType() && dw.filter == filter) is Window duplicate)
			{
				Close();
				Find.WindowStack.Notify_ClickedInsideWindow(duplicate);
			}
		}

		public override Vector2 InitialSize => new(600, 600);

		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = 0;
		}


		public virtual QuerySearch.CloneArgs ImportArgs => default;
		public virtual void Import(QuerySearch search)
		{
			filter.name = search.name;
			filter.Children.Import(search.Children);

			filter.UnbindMap();
			filter.changed = true;
		}

		private string layoutFocus;
		public override void DoWindowContents(Rect fillRect)
		{
//			if(Event.current.type is not EventType.Repaint and not EventType.Layout)
//				Log.Message($"{Event.current} ({GUI.GetNameOfFocusedControl()})");

			if (!locked && 
				Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.V && Event.current.control)
			{
				ClipboardTransfer clippy = new();
				if (clippy.ProvideMethod() == ISearchProvider.Method.Single)
				{
					Import(clippy.ProvideSingle().Clone(ImportArgs));
					Event.current.Use();
				}
			}

			Draw(fillRect);

			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.C && Event.current.control)
			{
				ClipboardTransfer clippy = new();
				clippy.Receive(filter);
				Event.current.Use();
			}

			if (Event.current.type == EventType.MouseDown && fillRect.Contains(Event.current.mousePosition))
			{
				UI.UnfocusCurrentControl();
			}

			// workaround for unity GUI bug where Unity windows seems to, I guess, bind GUI control ids and doesn't handle them dynamically being removed/reordered
			// In the end the GUI.GetNameOfFocusedControl is different between layout and repaint events in the same frame.
			// Seems to not happen in normal operation.
			if (Event.current.type == EventType.Layout)
				layoutFocus = GUI.GetNameOfFocusedControl();
			else if (Event.current.type == EventType.Repaint)
			{
				if (layoutFocus != GUI.GetNameOfFocusedControl())
				{
					Verse.Log.Warning($"TDFindLib detected a Unity GUI bug and is fixing it by re-opening the window (Repaint focus \"{GUI.GetNameOfFocusedControl()}\" != Layout focus \"{layoutFocus}\")");
					Rect pos = windowRect;
					Close(false);
					Find.WindowStack.Add(this);
					windowRect = pos;

					filter.Children.ForEach((ThingQuery q) => q.Focus());
					// would love to GUI.FocusControl but the name of the Widget.TextField are by COORDINATES ugh.
				}
			}
		}

		protected virtual void DrawHeader(Rect headerRect)
		{
			//Think a lot harder about how this QuerySearch/QueryHolder difference should be handled.
			QuerySearch search = filter as QuerySearch;
			
			Rect typeRect = headerRect.LeftPart(.32f);

			// List Type
			if (search != null)
			{
				Widgets.Label(typeRect, "TD.Listing".Translate() + search.ListType.TranslateEnum());

				if (!locked)
				{
					Widgets.DrawHighlightIfMouseover(typeRect);
					if (Widgets.ButtonInvisible(typeRect))
					{
						List<FloatMenuOption> types = new();
						foreach (SearchListType type in DebugSettings.godMode ? Enum.GetValues(typeof(SearchListType)) : SearchListNormalTypes.normalTypes)
						{
							if (!DebugSettings.godMode && type >= SearchListType.Haulables)
								continue;

							if (Event.current.control)
							{
								types.Add(new FloatMenuOption(
									type.TranslateEnum(),
									() =>
									{
										if (Event.current.shift)
											search.SetListType(type);
										else
											search.ToggleListType(type);
									},
									search.ListType.HasFlag(type) ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex,
									Color.white));
							}
							else
							{
								types.Add(new FloatMenuOption(type.TranslateEnum(), () => search.SetListType(type)));
							}
						}

						Find.WindowStack.Add(new FloatMenu(types));
					}
				}
			}


			// Matching All or Any
			Rect matchRect = typeRect.CenteredOnXIn(headerRect);

			Widgets.Label(matchRect, filter.MatchAllQueries ? "TD.MatchingAllFilters".Translate() : "TD.MatchingAnyFilter".Translate());
			if (!locked)
			{
				Widgets.DrawHighlightIfMouseover(matchRect);
				if (Widgets.ButtonInvisible(matchRect))
					filter.MatchAllQueries = !filter.MatchAllQueries;
			}


			// Searching Map selection:
			if (search != null)
			{
				Rect mapTypeRect = headerRect.RightPart(.32f);
				Widgets.Label(mapTypeRect, search.GetMapOptionLabel());

				bool forceCurMap = search.ForceCurMap();
				if (!locked && !forceCurMap)
				{
					Widgets.DrawHighlightIfMouseover(mapTypeRect);
					if (Widgets.ButtonInvisible(mapTypeRect))
					{
						List<FloatMenuOption> mapOptions = new();

						//Current Map
						mapOptions.Add(new FloatMenuOption("TD.SearchCurrentMapOnly".Translate(), () => search.SetSearchCurrentMap()));

						//All maps
						mapOptions.Add(new FloatMenuOption("TD.SearchAllMaps".Translate(), () => search.SetSearchAllMaps()));

						if (search.active)
						{
							//Toggle each map
							foreach (Map map in Find.Maps)
							{
								mapOptions.Add(new FloatMenuOption(
									map.Parent.LabelCap,
									() =>
									{
										if (Event.current.shift)
											search.SetSearchMap(map);
										else
											search.ToggleSearchMap(map);
									},
									search.ChosenMaps == null ? Widgets.CheckboxPartialTex
									: search.ChosenMaps.Contains(map) ? Widgets.CheckboxOnTex
									: Widgets.CheckboxOffTex,
									Color.white));
							}
						}
						else
						{
							mapOptions.Add(new FloatMenuOption("TD.SearchChosenMapsOnceLoaded".Translate(), () => search.SetSearchChosenMaps()));
						}

						Find.WindowStack.Add(new FloatMenu(mapOptions));
					}
				}
				if (forceCurMap)
				{
					TooltipHandler.TipRegion(mapTypeRect, "TD.AFilterIsForcingThisSearchToRunOnTheCurrentMapOnly".Translate());
				}
			}
		}

		public virtual void DrawIcons(WidgetRow row)
		{
			if (!permalocked)
			{
				// Reset button
				if (locked)
					row.IncrementPosition(WidgetRow.IconSize); //not Gap because that checks for 0 and doesn't actually gap
				else if (row.ButtonIcon(FindTex.Cancel, "ClearAll".Translate()))
					filter.Reset();

				// Locked button
				if (row.ButtonIcon(locked ? FindTex.LockOn : FindTex.LockOff, "TD.LockEditing".Translate()))
					locked = !locked;
			}

			// Rename button
			if (!locked && showNameAfterTitle && row.ButtonIcon(TexButton.Rename))
				Find.WindowStack.Add(new Dialog_Name(
					filter.name,
					newName => { filter.name = newName; filter.changed = true; },
					"TD.Rename0".Translate(filter.name)));

			// Library button
			row.ButtonOpenLibrary();

			// Export button
			if(filter is QuerySearch search)
				row.ButtonChooseExportSearch(search, transferTag);
			else
				row.ButtonChooseExportQueryHolder(filter, transferTag);

			// Import button
			if (!permalocked)
				row.ButtonChooseImportSearch(Import, transferTag, ImportArgs);
		}



		//Draw Search
		private Vector2 scrollPosition;
		private float scrollHeight;

		public void Draw(Rect rect)
		{
			Listing_StandardIndent listing = new()
			{ maxOneColumn = true };

			listing.Begin(rect);


			//Search Name
			Text.Font = GameFont.Medium;
			Rect nameRect = listing.GetRect(Text.LineHeight);
			string titleLabel = title;
			if (showNameAfterTitle)
				titleLabel += ": " + filter.name;
			Widgets.Label(nameRect, titleLabel);
			Text.Font = GameFont.Small;


			//Buttons
			WidgetRow buttonRow = new (nameRect.xMax - 20, nameRect.yMin, UIDirection.LeftThenDown);
			DrawIcons(buttonRow);


			// Header (Listing/Any/Map)
			Rect headerRect = listing.GetRect(Text.LineHeight);
			DrawHeader(headerRect);

			listing.GapLine();


			//Draw Queries!!!
			Rect listRect = listing.GetRemainingRect();

			//Lock out input to queries.
			if (locked &&
				Event.current.type != EventType.Repaint &&
				Event.current.type != EventType.Layout &&
				Event.current.type != EventType.Ignore &&
				Event.current.type != EventType.Used &&
				Event.current.type != EventType.ScrollWheel &&
				Mouse.IsOver(listRect))
			{
				Event.current.Use();
			}

			//Draw Queries:
			if(filter.Children.DrawQueriesInRect(listRect, locked, ref scrollPosition, ref scrollHeight))
				filter.Changed();

			listing.End();
		}
	}

	public static class SearchListNormalTypes
	{
		public static readonly SearchListType[] normalTypes =
			{ SearchListType.Selectable, SearchListType.Everyone, SearchListType.Items, SearchListType.Buildings, SearchListType.Plants,
			SearchListType.Natural, SearchListType.Junk, SearchListType.All, SearchListType.Inventory};
	}
}
