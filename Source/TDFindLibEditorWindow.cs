﻿using System;
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
		private FindDescription findDesc;
		private bool locked;
		private Action<FindDescription> onCloseIfChanged;

		public TDFindLibEditorWindow(FindDescription desc, Action<FindDescription> onCloseIfChanged = null)
		{
			findDesc = desc;
			onlyOneOfTypeAllowed = false;
			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			doCloseX = true;
			this.onCloseIfChanged = onCloseIfChanged;
		}

		public override void PostClose()
		{
			if (findDesc.changed)
				onCloseIfChanged?.Invoke(findDesc);
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
			DrawFindDescription(fillRect, findDesc);
		}

		//Draw Filters
		public void DrawFindDescription(Rect rect, FindDescription findDesc)
		{
			Listing_StandardIndent listing = new Listing_StandardIndent()
			{ maxOneColumn = true };

			listing.Begin(rect);


			//Filter Name
			Text.Font = GameFont.Medium;
			Rect nameRect = listing.GetRect(Text.LineHeight);
			Widgets.Label(nameRect, "Filter: " + findDesc.name);

			//Buttons
			WidgetRow buttonRow = new WidgetRow(nameRect.xMax - 20, nameRect.yMin, UIDirection.LeftThenDown);


			if (buttonRow.ButtonIcon(FindTex.Cancel, "ClearAll".Translate()))
			{
				findDesc.Reset();
			}

			if (buttonRow.ButtonIcon(locked ? FindTex.LockOn : FindTex.LockOff, "TD.LockEditing".Translate()))
				locked = !locked;

			if (buttonRow.ButtonIcon(TexButton.Rename))
				Find.WindowStack.Add(new Dialog_Name(findDesc.name, newName => { findDesc.name = newName; findDesc.changed = true; }));


			//Listing Type
			Text.Font = GameFont.Small;

			Rect headerRect = listing.GetRect(Text.LineHeight);
			Rect typeRect = headerRect.LeftPart(.6f);
			Rect allMapsRect = headerRect.RightPart(.3f);
			Widgets.DrawHighlightIfMouseover(typeRect);
			Widgets.DrawHighlightIfMouseover(allMapsRect);

			Widgets.Label(typeRect, "TD.Listing".Translate() + findDesc.BaseType.TranslateEnum());
			if (Widgets.ButtonInvisible(typeRect))
			{
				List<FloatMenuOption> types = new List<FloatMenuOption>();
				foreach (BaseListType type in DebugSettings.godMode ? Enum.GetValues(typeof(BaseListType)) : BaseListNormalTypes.normalTypes)
					types.Add(new FloatMenuOption(type.TranslateEnum(), () => findDesc.BaseType = type));

				Find.WindowStack.Add(new FloatMenu(types));
			}


			bool filterChanged = false;

			//Extra options:
			bool allMaps = findDesc.allMaps;
			Widgets.CheckboxLabeled(allMapsRect,
				"TD.AllMaps".Translate(),
				ref allMaps);
			TooltipHandler.TipRegion(allMapsRect, "TD.CertainFiltersDontWorkForAllMaps-LikeZonesAndAreasThatAreObviouslySpecificToASingleMap".Translate());

			if(allMaps != findDesc.allMaps)
			{
				findDesc.allMaps = allMaps; //Re-writes map label, remakes list. Hopefully the map is set if allmaps is checked off?
			}

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
			filterChanged |= findDesc.Children.DrawFilters(listRect, locked);

			listing.End();

			//Update if needed
			if (filterChanged)
				findDesc.RemakeList();
		}
	}
}