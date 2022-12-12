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
	public class ResultThingListWindow : Window
	{
		private ThingListDrawer drawer;

		public ResultThingListWindow(QuerySearch search)
		{
			drawer = new ThingListDrawer(search);
			preventCameraMotion = false;
			draggable = true;
			resizeable = true;
			closeOnAccept = false;
			//closeOnCancel = false;
			doCloseX = true;
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
		public QuerySearch search;

		const int RowHeight = 34;

		public ThingListDrawer(QuerySearch search)
		{
			this.search = search;
		}

		public virtual void DrawIconButtons(WidgetRow row)
		{
			//Select All
			selectAll = row.ButtonIcon(FindTex.SelectAll, "TD.SelectAllGameAllowsUpTo".Translate(Selector.MaxNumSelected));
		}

		private Vector2 scrollPositionList = Vector2.zero;
		ThingDef selectAllDef;
		bool selectAll;
		public void DrawThingList(Rect inRect)
		{
			Text.Font = GameFont.Small;

			//Top-row buttons
			WidgetRow row = new WidgetRow(inRect.x, inRect.y, UIDirection.RightThenDown, inRect.width);

			DrawIconButtons(row);

			//Godmode showing fogged
			if (search.result.godMode)
			{
				row.Icon(Verse.TexButton.GodModeEnabled, "TD.GodModeIsAllowingYouToSeeThingsInFoggedAreasThingsYouCantNormallyKnowAndVariousOtherWeirdThings".Translate());
			}



			//Count text
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(inRect, LabelCountThings(search.result));
			Text.Anchor = default;

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
			Rect listRect = inRect;
			listRect.yMin += 34; //Also RowHeight but doesn't need to be equal necessarily

			GUI.color = Color.gray;
			Widgets.DrawBox(listRect);
			GUI.color = Color.white;

			//Nudge in so it's not touching box
			listRect = listRect.ContractedBy(1);
			//contract horizontally another pixel . . . 
			listRect.width -= 2; listRect.x += 1;

			//Keep full width if nothing to scroll:
			float scrollViewListHeight = search.result.allThings.Count * RowHeight;
			float viewWidth = listRect.width;
			if (scrollViewListHeight > listRect.height)
				viewWidth -= 16f;

			//Draw Scrolling list:
			Rect viewRect = new Rect(0f, 0f, viewWidth, scrollViewListHeight);
			Widgets.BeginScrollView(listRect, ref scrollPositionList, viewRect);

			//Be smart about drawing only what's visible.
			//For: Set up 3 starting variables woahwoahwoah
			int i = (int)scrollPositionList.y / RowHeight;
			int iMax = Math.Min(1 + (int)(scrollPositionList.y+listRect.height) / RowHeight, search.result.allThings.Count);
			Rect thingRect = new Rect(viewRect.x, i*RowHeight, viewRect.width, 32);
			for (; i < iMax; thingRect.y += RowHeight, i++)
				DrawThingRow(search.result.allThings[i], thingRect);

			//Select all 
			Map currentMap = Find.CurrentMap;
			if (selectAll)
				foreach (Thing t in search.result.allThings)
					if(t.Map == currentMap)
						TrySelect.Select(t);

			//Select all for double-click
			if (selectAllDef != null)
				foreach (Thing t in search.result.allThings)
					if (t.Map == currentMap && t.def == selectAllDef)
						TrySelect.Select(t);

			Widgets.EndScrollView();

			// Deselect by clicking anywhere else
			if (Widgets.ButtonInvisible(listRect, false))
				Find.Selector.ClearSelection();
		}

		bool dragSelect = false;
		bool dragDeselect = false;
		bool dragJump = false;
		private void DrawThingRow(Thing thing, Rect rect)
		{
			//Highlight selected
			if (Find.Selector.IsSelected(thing))
				Widgets.DrawHighlightSelected(rect);

			//Draw
			DrawThing(rect, thing);

			if (Mouse.IsOver(rect))
			{
				//Draw arrow pointing to hovered thing
				Vector3 center = UI.UIToMapPosition((float)(UI.screenWidth / 2), (float)(UI.screenHeight / 2));
				bool arrow = !thing.Spawned || (center - thing.DrawPos).MagnitudeHorizontalSquared() >= 121f;//Normal arrow is 9^2, using 11^2 seems good too.
				TargetHighlighter.Highlight(thing, arrow, true, true);
			

				//Mouse event: select.
				if (Event.current.type == EventType.MouseDown)
				{
					if (!thing.def.selectable || !thing.Spawned)
					{
						CameraJumper.TryJump(thing);
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
					{
						if (thing.Map == Find.CurrentMap)
							TrySelect.Select(thing, false);
					}
					else if (dragDeselect)
						Find.Selector.Deselect(thing);

					Event.current.Use();
				}
			}
		}

		protected virtual void DrawThing(Rect rect, Thing thing)
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

		// Obsolete
		public static string LabelCountThings(IEnumerable<Thing> things)
		{
			return "TD.LabelCountThings".Translate(things.Sum(t => t.stackCount));
		}

		public static string LabelCountThings(SearchResult result)
		{
			return "TD.LabelCountThings".Translate(result.allThingsCount);
		}
	}
}
