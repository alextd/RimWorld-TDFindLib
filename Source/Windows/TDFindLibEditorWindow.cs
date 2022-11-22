using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace TD_Find_Lib
{
	public class TDFindLibEditorWindow : Window
	{
		public readonly FindDescriptionDrawer drawer;
		private Action<FindDescription> onCloseIfChanged;

		public TDFindLibEditorWindow(FindDescription desc, Action<FindDescription> onCloseIfChanged = null)
		{
			drawer = new FindDescriptionDrawer(desc, "Editing") { showNameAfterTitle = true };
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
			if (!drawer.findDesc.OnCancelKeyPressed())
				base.OnCancelKeyPressed();
		}

		public override void PostClose()
		{
			if (drawer.findDesc.changed)
				onCloseIfChanged?.Invoke(drawer.findDesc);
		}


		public virtual Vector2 RequestedSize => new Vector2(600, 600);
		public override Vector2 InitialSize
		{
			get
			{
				Vector2 size = RequestedSize;
				if (size.y > (float)(UI.screenHeight - 35))
				{
					size.y = UI.screenHeight - 35;
				}
				if (size.x > (float)UI.screenWidth)
				{
					size.x = UI.screenWidth;
				}
				return size;
			}
		}

		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = 0;
		}


		public override void DoWindowContents(Rect fillRect)
		{
			drawer.DrawFindDescription(fillRect, Find.CurrentMap == null ? null :
				row =>
				{
					FilterStorageUtil.ButtonChooseExportFilter(row, drawer.findDesc, "Storage");
					if (row.ButtonIcon(FindTex.List, "List things matching this filter"))
					{
						Find.WindowStack.Add(new TDFindLibThingsWindow(drawer.findDesc.CloneForUseSingle()));
					}
				});
		}
	}
	public class TDFindLibViewerWindow : TDFindLibEditorWindow
	{
		public TDFindLibViewerWindow(FindDescription desc):base(desc)
		{
			drawer.permalocked = true;
			drawer.title = "Viewing";
		}
	}

	public class FindDescriptionDrawer
	{ 
		public FindDescription findDesc;
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

		public FindDescriptionDrawer(FindDescription findDesc, string title)
		{
			this.findDesc = findDesc;
			this.title = title;
		}

		protected virtual void DrawHeader(Rect headerRect)
		{
			Rect typeRect = headerRect.LeftPart(.49f);

			Widgets.Label(typeRect, "TD.Listing".Translate() + findDesc.BaseType.TranslateEnum());
			if (!locked)
			{
				Widgets.DrawHighlightIfMouseover(typeRect);
				if (Widgets.ButtonInvisible(typeRect))
				{
					List<FloatMenuOption> types = new List<FloatMenuOption>();
					foreach (BaseListType type in DebugSettings.godMode ? Enum.GetValues(typeof(BaseListType)) : BaseListNormalTypes.normalTypes)
						types.Add(new FloatMenuOption(type.TranslateEnum(), () => findDesc.BaseType = type));

					Find.WindowStack.Add(new FloatMenu(types));
				}
			}


			//Extra options:
			Rect mapTypeRect = headerRect.RightPart(.49f);
			Widgets.Label(mapTypeRect, findDesc.GetMapOptionLabel());
			if(!locked)
			{
				Widgets.DrawHighlightIfMouseover(mapTypeRect);
				if(Widgets.ButtonInvisible(mapTypeRect))
				{
					List<FloatMenuOption> mapOptions = new List<FloatMenuOption>();

					//Current Map
					mapOptions.Add(new FloatMenuOption("Search current map only", () => findDesc.SetSearchCurrentMap()));

					//All maps
					mapOptions.Add(new FloatMenuOption("Search all maps", () => findDesc.SetSearchAllMaps()));

					if (findDesc.active)
					{
						//Toggle each map
						foreach (Map map in Find.Maps)
						{
							mapOptions.Add(new FloatMenuOption(
								map.Parent.LabelCap,
								() =>
								{
									if (Event.current.shift)
										findDesc.SetSearchMap(map);
									else
										findDesc.ToggleSearchMap(map);
								},
								findDesc.ChosenMaps == null ? Widgets.CheckboxPartialTex
								: findDesc.ChosenMaps.Contains(map) ? Widgets.CheckboxOnTex
								: Widgets.CheckboxOffTex,
								Color.white));
						}
					}
					else
					{
						mapOptions.Add(new FloatMenuOption("Search chosen maps (once loaded)", () => findDesc.SetSearchChosenMaps()));
					}

					Find.WindowStack.Add(new FloatMenu(mapOptions));
				}
			}
		}

		//Draw Filters
		public void DrawFindDescription(Rect rect, Action<WidgetRow> extraIconsDrawer = null)
		{
			Listing_StandardIndent listing = new Listing_StandardIndent()
			{ maxOneColumn = true };

			listing.Begin(rect);


			//Filter Name
			Text.Font = GameFont.Medium;
			Rect nameRect = listing.GetRect(Text.LineHeight);
			string titleLabel = title;
			if (showNameAfterTitle)
				titleLabel += ": " + findDesc.name;
			Widgets.Label(nameRect, titleLabel);


			//Buttons
			WidgetRow buttonRow = new WidgetRow(nameRect.xMax - 20, nameRect.yMin, UIDirection.LeftThenDown);

			if (!locked && buttonRow.ButtonIcon(FindTex.Cancel, "ClearAll".Translate()))
				findDesc.Reset();

			if (!permalocked && buttonRow.ButtonIcon(locked ? FindTex.LockOn : FindTex.LockOff, "TD.LockEditing".Translate()))
				locked = !locked;

			if (!locked && showNameAfterTitle && buttonRow.ButtonIcon(TexButton.Rename))
				Find.WindowStack.Add(new Dialog_Name(
					findDesc.name, 
					newName => { findDesc.name = newName; findDesc.changed = true; },
					$"Rename {findDesc.name}"));

			if (DebugSettings.godMode)
				buttonRow.Label(findDesc.active ? "ACTIVE!" : "INACTIVE");

			// Extra custom buttons!
			extraIconsDrawer?.Invoke(buttonRow);


			//Listing Type
			Text.Font = GameFont.Small;

			Rect headerRect = listing.GetRect(Text.LineHeight);
			DrawHeader(headerRect);

			listing.GapLine();


			//Draw Filters!!!
			Rect listRect = listing.GetRemainingRect();

			//Lock out input to filters.
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

			//Draw Filters:
			if(findDesc.Children.DrawFilters(listRect, locked))
				findDesc.RemakeList();

			listing.End();
		}
	}

	public static class BaseListNormalTypes
	{
		public static readonly BaseListType[] normalTypes =
			{ BaseListType.Selectable, BaseListType.Everyone, BaseListType.Items, BaseListType.Buildings, BaseListType.Plants,
			BaseListType.Natural, BaseListType.ItemsAndJunk, BaseListType.All, BaseListType.Inventory};
	}
}
