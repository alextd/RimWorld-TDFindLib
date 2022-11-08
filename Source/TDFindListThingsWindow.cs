using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;

namespace TD_Find_Lib
{
	public class TDFindListThingsWindow : Window
	{
		private ThingListDrawer drawer;

		public TDFindListThingsWindow(FindDescription desc)
		{
			drawer = new ThingListDrawer(desc);
			onlyOneOfTypeAllowed = false;
			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			doCloseX = true;
		}

		public override void PostClose()
		{
			Current.Game.GetComponent<TDFindLibGameComp>().RemoveRefresh(drawer.findDesc);
		}


		public override Vector2 InitialSize => new Vector2(360, 800);


		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();

			windowRect.x = UI.screenWidth - windowRect.width;
			windowRect.y = (UI.screenHeight - 35) - windowRect.height;
		}


		public override void DoWindowContents(Rect fillRect)
		{
			drawer.DrawThingList(fillRect);
		}
	}

	public class ThingListDrawer
	{
		public FindDescription findDesc;

		public ThingListDrawer(FindDescription d)
		{
			findDesc = d;
		}

		private Vector2 scrollPositionList = Vector2.zero;
		private float scrollViewHeightList;
		ThingDef selectAllDef;
		bool selectAll;
		public void DrawThingList(Rect listRect, Action<WidgetRow> extraIconsDrawer = null)
		{
			Text.Font = GameFont.Small;

			//Top-row buttons
			WidgetRow buttRow = new WidgetRow(listRect.x, listRect.y, UIDirection.RightThenDown, listRect.width);

			//Select All
			selectAll = buttRow.ButtonIcon(FindTex.SelectAll, "TD.SelectAllGameAllowsUpTo80".Translate());

			//Manual refresh
			if (buttRow.ButtonIcon(TexUI.RotRightTex, "TD.Refresh".Translate()))
				findDesc.RemakeList();

			//Continuous refresh
			var comp = Current.Game.GetComponent<TDFindLibGameComp>();
			bool refresh = comp.IsRefreshing(findDesc);
			if (buttRow.ButtonIconColored(TexUI.ArrowTexRight,
				Find.TickManager.Paused	? "(Does not refresh when paused)" : "TD.ContinuousRefreshAboutEverySecond".Translate(),
				refresh ? Color.green : Color.white, 
				Color.Lerp(Color.green, Color.white, 0.5f)))
			{
				if (refresh)
					comp.RemoveRefresh(findDesc);
				else
					comp.RegisterRefresh(findDesc, 60); //every 60 or so
			}

			if (Find.TickManager.Paused)
			{
				// Thank you publicizer
				buttRow.IncrementPosition(-WidgetRow.IconSize);
				GUI.color = new Color(1, 1, 1, .5f);
				buttRow.Icon(FindTex.Cancel);
				GUI.color = Color.white;
			}

			extraIconsDrawer?.Invoke(buttRow);

			//Godmode showing fogged
			if (DebugSettings.godMode)
			{
				buttRow.Icon(Verse.TexButton.GodModeEnabled, "God mode is allowed you to see into fogged areas and various weird things");
			}



			//Count text
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(listRect, LabelCountThings(findDesc.ListedThings));
			Text.Anchor = TextAnchor.UpperLeft;
			listRect.yMin += 34;


			//Handle mouse selection
			if (!Input.GetMouseButton(0))
			{
				dragSelect = false;
				dragDeselect = false;
			}
			if (!Input.GetMouseButton(1))
				dragJump = false;

			selectAllDef = null;

			//Draw Scrolling List:

			//Draw box:
			GUI.color = Color.gray;
			Widgets.DrawBox(listRect);
			GUI.color = Color.white;

			//Nudge in so it's not touching box
			listRect = listRect.ContractedBy(1);
			listRect.width -= 2; listRect.x += 1;

			//Keep full width if nothing to scroll:
			float viewWidth = listRect.width;
			if (scrollViewHeightList > listRect.height)
				viewWidth -= 16f;

			//Draw Scrolling list:
			Rect viewRect = new Rect(0f, 0f, viewWidth, scrollViewHeightList);
			Widgets.BeginScrollView(listRect, ref scrollPositionList, viewRect);
			Rect thingRect = new Rect(viewRect.x, 0, viewRect.width, 32);

			foreach (Thing thing in findDesc.ListedThings)
			{
				//Be smart about drawing only what's shown.
				if (thingRect.y + 32 >= scrollPositionList.y)
					DrawThingRow(thing, ref thingRect);

				thingRect.y += 34;

				if (thingRect.y > scrollPositionList.y + listRect.height)
					break;
			}

			if (Event.current.type == EventType.Layout)
				scrollViewHeightList = findDesc.ListedThings.Count() * 34f;

			//Select all 
			if (selectAll)
				foreach (Thing t in findDesc.ListedThings)
					TrySelect.Select(t, false);

			//Select all for double-click
			if (selectAllDef != null)
				foreach (Thing t in findDesc.ListedThings)
					if (t.def == selectAllDef)
						TrySelect.Select(t, false);

			Widgets.EndScrollView();
		}

		bool dragSelect = false;
		bool dragDeselect = false;
		bool dragJump = false;
		private void DrawThingRow(Thing thing, ref Rect rect)
		{
			//Highlight selected
			if (Find.Selector.IsSelected(thing))
				Widgets.DrawHighlightSelected(rect);

			//Draw
			DrawThing(rect, thing);

			//Draw arrow pointing to hovered thing
			if (Mouse.IsOver(rect))
			{
				Vector3 center = UI.UIToMapPosition((float)(UI.screenWidth / 2), (float)(UI.screenHeight / 2));
				bool arrow = (center - thing.DrawPos).MagnitudeHorizontalSquared() >= 121f;//Normal arrow is 9^2, using 11^1 seems good too.
				TargetHighlighter.Highlight(thing, arrow, true, true);
			}

			//Mouse event: select.
			if (Mouse.IsOver(rect))
			{
				if (Event.current.type == EventType.MouseDown)
				{
					if (!thing.def.selectable || !thing.Spawned)
					{
						CameraJumper.TryJump(thing);
						if (Event.current.alt)
							Find.MainTabsRoot.EscapeCurrentTab(false);
					}
					else if (Event.current.clickCount == 2 && Event.current.button == 0)
					{
						selectAllDef = thing.def;
					}
					else if (Event.current.shift)
					{
						if (Find.Selector.IsSelected(thing))
						{
							dragDeselect = true;
							Find.Selector.Deselect(thing);
						}
						else
						{
							dragSelect = true;
							TrySelect.Select(thing);
						}
					}
					else if (Event.current.alt)
					{
						Find.MainTabsRoot.EscapeCurrentTab(false);
						CameraJumper.TryJumpAndSelect(thing);
					}
					else
					{
						if (Event.current.button == 1)
						{
							CameraJumper.TryJump(thing);
							dragJump = true;
						}
						else if (Find.Selector.IsSelected(thing))
						{
							CameraJumper.TryJump(thing);
							dragSelect = true;
						}
						else
						{
							Find.Selector.ClearSelection();
							TrySelect.Select(thing);
							dragSelect = true;
						}
					}

					Event.current.Use();
				}
				if (Event.current.type == EventType.MouseDrag)
				{
					if (!thing.def.selectable || !thing.Spawned)
						CameraJumper.TryJump(thing);
					else if (dragJump)
						CameraJumper.TryJump(thing);
					else if (dragSelect)
						TrySelect.Select(thing, false);
					else if (dragDeselect)
						Find.Selector.Deselect(thing);

					Event.current.Use();
				}
			}
		}

		public static void DrawThing(Rect rect, Thing thing)
		{
			//Label
			Widgets.Label(rect, thing.LabelCap);

			ThingDef def = thing.def.entityDefToBuild as ThingDef ?? thing.def;
			Rect iconRect = rect.RightPartPixels(32 * (def.graphicData?.drawSize.x / def.graphicData?.drawSize.y ?? 1f));
			//Icon
			if (thing is Frame frame)
			{
				Widgets.ThingIcon(iconRect, def);
			}
			else if (def.graphic is Graphic_Linked && def.uiIconPath.NullOrEmpty())
			{
				Material iconMat = def.graphic.MatSingle;
				Rect texCoords = new Rect(iconMat.mainTextureOffset, iconMat.mainTextureScale);
				GUI.color = thing.DrawColor;
				Widgets.DrawTextureFitted(iconRect, def.uiIcon, 1f, Vector2.one, texCoords);
				GUI.color = Color.white;
			}
			else
			{
				if (thing.Graphic is Graphic_Cluster)
					Rand.PushState(123456);
				Widgets.ThingIcon(iconRect, thing);
				if (thing.Graphic is Graphic_Cluster)
					Rand.PopState();
			}
		}


		//Draw Filters
		public static string LabelCountThings(IEnumerable<Thing> things)
		{
			return "TD.LabelCountThings".Translate(things.Sum(t => t.stackCount));
		}
	}
}
