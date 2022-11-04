using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace TD_Find_Lib
{
	[StaticConstructorOnStartup]
	static class FindTex
	{
		public static readonly Texture2D LockOn = ContentFinder<Texture2D>.Get("Locked", true);
		public static readonly Texture2D LockOff = ContentFinder<Texture2D>.Get("Unlocked", true);
	}

}
