using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace TD_Find_Lib
{
	public abstract class MaskFilter<T, TGroup>
	{
		public TGroup mustHave, cantHave;
		public string label;

		public MaskFilter()
		{
			//Subclasses should SetLabel in ctor.
		}
		public virtual MaskFilter<T, TGroup> Clone()
		{
			MaskFilter<T, TGroup> clone = (MaskFilter<T, TGroup>)Activator.CreateInstance(GetType());
			clone.label = label;
			clone.mustHave = mustHave;
			clone.cantHave = cantHave;
			return clone;
		}
		public virtual void PostExposeData()
		{
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				SetLabel();
			}
		}


		public abstract bool AppliesTo(TGroup group);


		public abstract string MakeLabel(T option);
		public abstract string MakeLabelGroup(TGroup group);

		static readonly Color mustColor = Color.Lerp(Color.green, Color.gray, .5f);
		static readonly Color cantColor = Color.Lerp(Color.red, Color.gray, .5f);
		public void SetLabel()
		{
			string mustLabel = MakeLabelGroup(mustHave);
			string cantLabel = MakeLabelGroup(cantHave);

			StringBuilder sb = new();
			if (mustLabel.Length > 0)
			{
				sb.Append(mustLabel.Colorize(mustColor));
			}
			if (cantLabel.Length > 0)
			{
				if (sb.Length > 0)
					sb.Append(" ; ");
				sb.Append(cantLabel.Colorize(cantColor));
			}
			if (sb.Length == 0)
				label = "TD.AnyOption".Translate();
			else
				label = sb.ToString();
		}

		protected abstract bool Contains(TGroup group, T option);
		protected abstract void Remove(ref TGroup group, T option);
		protected abstract void Add(ref TGroup group, T option);

		public bool MustContains(T option) => Contains(mustHave, option);
		public bool CantContains(T option) => Contains(cantHave, option);
		public void RemoveMust(T option) => Remove(ref mustHave, option);
		public void RemoveCant(T option) => Remove(ref cantHave, option);
		public void AddMust(T option) => Add(ref mustHave, option);
		public void AddCant(T option) => Add(ref cantHave, option);

		//Would be nice if it weren't tied to ThingQuery .
		public void DrawButton(Rect rect, ThingQuery parentQuery, List < T> options = null)
		{
			if (Widgets.ButtonText(rect, label))
			{
				List<FloatMenuOption> layerOptions = new();
				foreach (T option in options ?? Enum.GetValues(typeof(T)).OfType<T>().Where(t => (int)(object)t != 0))
				{
					layerOptions.Add(new FloatMenuOptionAndRefresh(
						MakeLabel(option),
						() => {
							if (Event.current.button == 1)
							{
								RemoveMust(option);
								RemoveCant(option);
							}
							//Cycle from must => cant => neither
							else if (MustContains(option))
							{
								RemoveMust(option);
								AddCant(option);
							}
							else if (CantContains(option))
							{
								RemoveCant(option);
							}
							else
							{
								AddMust(option);
							}
							SetLabel();
						},
						parentQuery,
						MustContains(option) ? Widgets.CheckboxOnTex
						: CantContains(option) ? Widgets.CheckboxOffTex
						: Widgets.CheckboxPartialTex));
				}

				Find.WindowStack.Add(new FloatMenu(layerOptions));
			}
		}
	}


	public class MaskFilterDef<T> : MaskFilter<T, List<T>> where T : Def
	{
		private Func<T, int> comparator;

		public MaskFilterDef()
		{
			mustHave = new();
			cantHave = new();
			SetLabel();
		}
		public MaskFilterDef(Func<T, int> c) : this()
		{
			comparator = c;
		}
		public override MaskFilter<T, List<T>> Clone()
		{
			MaskFilterDef<T> clone = (MaskFilterDef<T>)base.Clone();
			clone.mustHave = mustHave.ToList();
			clone.cantHave = cantHave.ToList();
			clone.comparator = comparator;
			return clone;
		}
		public override void PostExposeData()
		{
			base.PostExposeData();

			Scribe_Collections.Look(ref mustHave, "mustHave");
			Scribe_Collections.Look(ref cantHave, "cannotHave");
		}


		public override bool AppliesTo(List<T> group) =>
			mustHave.All(group.Contains) && !cantHave.Any(group.Contains);


		public override string MakeLabel(T def) => def.LabelCap;
		public override string MakeLabelGroup(List<T> defs) =>
			 string.Join(", ", defs.Select(d => d.LabelCap));

		protected override bool Contains(List<T> group, T option) => group.Contains(option);
		protected override void Remove(ref List<T> group, T option) => group.Remove(option);
		protected override void Add(ref List<T> group, T option)
		{
			group.Add(option);
			group.SortBy(comparator);
		}
	}

	public class MaskFilterEnum<T> : MaskFilter<T, T> where T : Enum
	{
		public MaskFilterEnum()
		{
			SetLabel();
		}
		public override void PostExposeData()
		{
			base.PostExposeData();

			Scribe_Values.Look(ref mustHave, "mustHave");
			Scribe_Values.Look(ref cantHave, "cannotHave");
		}


		public override bool AppliesTo(T check) =>
			(((int)(object)mustHave & (int)(object)check) == (int)(object)mustHave) 
			&&
			(((int)(object)cantHave & (int)(object)check) == 0);


		private readonly string defaultLabel = default(T).ToString();
		public override string MakeLabel(T val)
		{
			string result = val.ToString();
			if (result == defaultLabel)
				return "TD.AnyOption".Translate();
			return result;
		}
		public override string MakeLabelGroup(T val)
		{
			string result = val.ToString();
			if (result == defaultLabel)
				return "";
			return result;
		}
		

		protected override bool Contains(T group, T option) =>
			group.HasFlag(option);
		protected override void Remove(ref T group, T option) =>
			group = (T)(object)(((int)(object)group) & (~(int)(object)option));
		protected override void Add(ref T group, T option) =>
			group = (T)(object)(((int)(object)group) | ((int)(object)option));
	}
}
