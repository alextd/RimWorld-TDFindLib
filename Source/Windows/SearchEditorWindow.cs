using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace TD_Find_Lib
{
	public class SearchEditorWindow : Window
	{
		public readonly QuerySearchDrawer drawer;
		private Action<QuerySearch> onCloseIfChanged;
		private string transferTag;

		public SearchEditorWindow(QuerySearch search, string transferTag, Action<QuerySearch> onCloseIfChanged = null)
		{
			this.transferTag = transferTag;

			drawer = new QuerySearchDrawer(search, "TD.Editing".Translate()) { showNameAfterTitle = true };
			onlyOneOfTypeAllowed = false;
			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			//closeOnAccept = false;
			//closeOnCancel = false;
			doCloseX = true;
			this.onCloseIfChanged = onCloseIfChanged;
		}

		public override void OnCancelKeyPressed()
		{
			if (!drawer.search.OnCancelKeyPressed())
				base.OnCancelKeyPressed();
		}

		public override void PostOpen()
		{
			base.PostOpen();

			if (Find.WindowStack.Windows.FirstOrDefault(w => w != this && w is SearchEditorWindow sw && sw.drawer.search == drawer.search) is Window duplicate)
			{
				Close();
				Find.WindowStack.Notify_ClickedInsideWindow(duplicate);
			}
		}

		public override void PostClose()
		{
			if (drawer.search.changed)
				onCloseIfChanged?.Invoke(drawer.search);
		}


		public override Vector2 InitialSize => new Vector2(600, 600);

		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = 0;
		}


		public virtual QuerySearch.CloneArgs ImportArgs => default;
		public virtual void Import(QuerySearch search)
		{
			drawer.search = search;
			drawer.search.changed = true;
		}

		public override void DoWindowContents(Rect fillRect)
		{
			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.V && Event.current.control)
			{
				ClipboardTransfer clippy = new();
				if (clippy.ProvideMethod() == ISearchProvider.Method.Single)
				{
					Import(clippy.ProvideSingle());
					Event.current.Use();
				}
			}

			drawer.DrawQuerySearch(fillRect, Find.CurrentMap == null ? null :
				row =>
				{
					
					SearchStorage.ButtonChooseImportSearch(row, Import, transferTag, ImportArgs);
					
					SearchStorage.ButtonChooseExportSearch(row, drawer.search, transferTag);

					if (row.ButtonIcon(FindTex.List, "TD.ListThingsMatchingThisSearch".Translate()))
					{
						Find.WindowStack.Add(new ResultThingListWindow(drawer.search.CloneForUseSingle()));
					}
#if DEBUG
					if (DebugSettings.godMode && row.ButtonIcon(FindTex.Infinity))
						UnitTests.Run();
#endif
				});
		}
	}
	public class TDFindLibViewerWindow : SearchEditorWindow
	{
		public TDFindLibViewerWindow(QuerySearch search, string tag):base(search, tag)
		{
			drawer.permalocked = true;
			drawer.title = "TD.Viewing".Translate();
		}
	}

	public class QuerySearchDrawer
	{ 
		public QuerySearch search;
		private bool _locked;
		public bool locked
		{
			get => _locked || permalocked;
			set => _locked = value;
		}
		public bool permalocked;

		//Pick one or the other.
		public bool showNameAfterTitle;
		public string title;

		public QuerySearchDrawer(QuerySearch search, string title)
		{
			this.search = search;
			this.title = title;
		}

		protected virtual void DrawHeader(Rect headerRect)
		{
			// List Type
			Rect typeRect = headerRect.LeftPart(.32f);
			Widgets.Label(typeRect, "TD.Listing".Translate() + search.ListType.TranslateEnum());

			if (!locked)
			{
				Widgets.DrawHighlightIfMouseover(typeRect);
				if (Widgets.ButtonInvisible(typeRect))
				{
					List<FloatMenuOption> types = new List<FloatMenuOption>();
					foreach (SearchListType type in DebugSettings.godMode ? Enum.GetValues(typeof(SearchListType)) : SearchListNormalTypes.normalTypes)
					{
						if (!DebugSettings.godMode && type >= SearchListType.Haulables)
							continue;

						if (Event.current.control)
						{
							types.Add(new FloatMenuOption(
								type.TranslateEnum(),
								() => {
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


			// Matching All or Any
			Rect matchRect = typeRect.CenteredOnXIn(headerRect);

			Widgets.Label(matchRect, search.MatchAllQueries ? "TD.MatchingAllFilters".Translate() : "TD.MatchingAnyFilter".Translate());
			if (!locked)
			{
				Widgets.DrawHighlightIfMouseover(matchRect);
				if (Widgets.ButtonInvisible(matchRect))
					search.MatchAllQueries = !search.MatchAllQueries;
			}


			// Searching Map selection:
			Rect mapTypeRect = headerRect.RightPart(.32f);
			Widgets.Label(mapTypeRect, search.GetMapOptionLabel());

			bool forceCurMap = search.ForceCurMap();
			if (!locked && !forceCurMap)
			{
				Widgets.DrawHighlightIfMouseover(mapTypeRect);
				if(Widgets.ButtonInvisible(mapTypeRect))
				{
					List<FloatMenuOption> mapOptions = new List<FloatMenuOption>();

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
			if(forceCurMap)
			{
				TooltipHandler.TipRegion(mapTypeRect, "TD.AFilterIsForcingThisSearchToRunOnTheCurrentMapOnly".Translate());
			}
		}



		//Draw Search
		private Vector2 scrollPosition;
		private float scrollHeight;

		public void DrawQuerySearch(Rect rect, Action<WidgetRow> extraIconsDrawer = null)
		{
			Listing_StandardIndent listing = new Listing_StandardIndent()
			{ maxOneColumn = true };

			listing.Begin(rect);


			//Search Name
			Text.Font = GameFont.Medium;
			Rect nameRect = listing.GetRect(Text.LineHeight);
			string titleLabel = title;
			if (showNameAfterTitle)
				titleLabel += ": " + search.name;
			Widgets.Label(nameRect, titleLabel);


			//Buttons
			WidgetRow buttonRow = new WidgetRow(nameRect.xMax - 20, nameRect.yMin, UIDirection.LeftThenDown);


			// Reset button
			if (locked)
				buttonRow.IncrementPosition(WidgetRow.IconSize); //not Gap because that checks for 0 and doesn't actually gap
			else if (buttonRow.ButtonIcon(FindTex.Cancel, "ClearAll".Translate()))
				search.Reset();

			// Locked button
			if (!permalocked && buttonRow.ButtonIcon(locked ? FindTex.LockOn : FindTex.LockOff, "TD.LockEditing".Translate()))
				locked = !locked;

			// Rename button
			if (!locked && showNameAfterTitle && buttonRow.ButtonIcon(TexButton.Rename))
				Find.WindowStack.Add(new Dialog_Name(
					search.name, 
					newName => { search.name = newName; search.changed = true; },
					"TD.Rename0".Translate(search.name)));

			// Show ACTIVE when godmode?
			if (DebugSettings.godMode)
				buttonRow.Label(search.active ? "ACTIVE!" : "INACTIVE");

			SearchStorage.ButtonOpenLibrary(buttonRow);

			// Extra custom buttons!
			extraIconsDrawer?.Invoke(buttonRow);


			// Listing Type
			Text.Font = GameFont.Small;

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
			if(search.Children.DrawQueriesInRect(listRect, locked, ref scrollPosition, ref scrollHeight))
				search.Changed();

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
