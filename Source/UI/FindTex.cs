﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	[StaticConstructorOnStartup]
	public static class FindTex
	{
		//Names of objects in vanilla are haphazard so I might as well just declare these here.
		public static readonly Texture2D LockOn = ContentFinder<Texture2D>.Get("Locked", true);
		public static readonly Texture2D LockOff = ContentFinder<Texture2D>.Get("Unlocked", true);
		public static readonly Texture2D Cancel = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);
		public static readonly Texture2D Edit = ContentFinder<Texture2D>.Get("UI/Buttons/OpenSpecificTab");
		public static readonly Texture2D Copy = ContentFinder<Texture2D>.Get("UI/Buttons/Copy");
		public static readonly Texture2D Paste = ContentFinder<Texture2D>.Get("UI/Buttons/Paste");
		public static readonly Texture2D Import = ContentFinder<Texture2D>.Get("Import");
		public static readonly Texture2D Export = ContentFinder<Texture2D>.Get("Export");
		public static readonly Texture2D ImportGroup = ContentFinder<Texture2D>.Get("ImportGroup");
		public static readonly Texture2D ExportGroup = ContentFinder<Texture2D>.Get("ExportGroup");
		public static readonly Texture2D Trash = ContentFinder<Texture2D>.Get("UI/Buttons/Dismiss");
		public static readonly Texture2D SelectAll = ContentFinder<Texture2D>.Get("UI/Commands/SelectNextTransporter", true);
		public static readonly Texture2D List = ContentFinder<Texture2D>.Get("UI/Buttons/ResourceReadoutCategorized", true);
		public static readonly Texture2D Book = ContentFinder<Texture2D>.Get("UI/Buttons/MainButtons/History", true);
		public static readonly Texture2D GreyPlus = ContentFinder<Texture2D>.Get("GreyPlus", true);
#if DEBUG
		public static readonly Texture2D Infinity = ContentFinder<Texture2D>.Get("UI/Buttons/Infinity");
#endif
	}
}
