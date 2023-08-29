using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;
using HarmonyLib;

namespace TD_Find_Lib
{
	public abstract class FieldData
	{
		public Type type; // declaring type, a subclass of some parent base type (often Thing)
		// There's no Type fieldType because that'll be a subclass for each type
		public string name; // field name / property name
		public FieldData(Type type, string name)
		{
			this.type = type;
			this.name = name;
		}
		public virtual void PostExposeData() { }
		public virtual FieldData Clone() => 
			(FieldData)Activator.CreateInstance(GetType(), type, name);

		public virtual bool Draw(Listing_StandardIndent listing) => false;
		public virtual void DoFocus() { }
		public virtual bool Unfocus() => false;

		public override string ToString() => $"{type}.{name}";

		// When listing for a subclass, prepend '>' if it's a parent's field
		public string DisplayName(Type selectedType)
		{
			int diff = 0;
			while (selectedType != type)
			{
				diff++;
				selectedType = selectedType.BaseType;
			}

			return new string('>', diff) + name;
		}

		// Make should use ??= to create / store an accessor
		public abstract void Make();

		public abstract bool AppliesTo(Thing thing);


		// Static listers
		// Here we hold all fields
		// Just the type+name until they're used, then they'll Make() an accessor.
		// Starting with thing subclasses so we have a dropdown
		// And we'll add in other fields when accessed.
		private static readonly Dictionary<Type, List<FieldData>> fields = new();
		public static readonly List<Type> thingSubclasses;


		static FieldData()
		{
			thingSubclasses = new();
			foreach (Type thingType in new Type[] { typeof(Thing) }
					.Concat(typeof(Thing).AllSubclasses().Where(ThingQuery.ValidType)))
			{
				if(MakeFieldData(thingType) != null)
					thingSubclasses.Add(thingType);
			}
		}

		private static List<FieldData> MakeFieldData(Type type)
		{
			List<FieldData> list = new(FindFields(type));

			if (list.Count > 0)
			{
				fields[type] = list;
				return list;
			}
			return null;
		}

		// Public acessors:

		public static IEnumerable<FieldData> FieldsFor(Type type)
		{
			if (!fields.TryGetValue(type, out var list))
			{
				list = MakeFieldData(type);
				if(list != null)
					fields[type] = list;
			}
			return list ?? Enumerable.Empty<FieldData>();
		}
		public static FieldData FieldDataFor(Type type, string name)
		{
			// This should only be called if it exists
			return FieldsFor(type).FirstOrDefault(fd => fd.name == name);
		}


		// All FieldData options for a subclass and its parents
		private static readonly Dictionary<Type, List<FieldData>> typeFields = new();
		public static List<FieldData> GetOptions(Type type, Type parentType)
		{
			if (!typeFields.TryGetValue(type, out var list))
			{
				list = new(FieldsFor(type));
				typeFields[type] = list;

				while(type != parentType)
				{
					type = type.BaseType;
					list.AddRange(FieldsFor(type));
				}
			}
			return list;
		}




		// Here we find all fields
		// TODO: more types, meaning FieldData subclasses for each type
		private static FieldData NewData(Type fieldDataType, Type inputType = null, params object[] args) =>
			(FieldData)Activator.CreateInstance(inputType == null ? fieldDataType : fieldDataType.MakeGenericType(inputType), args);

		const BindingFlags bFlags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;
		private static IEnumerable<FieldData> FindFields(Type type)
		{
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField)
				.Where(f => f.FieldType == typeof(bool)))
				yield return new BoolFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty)
				.Where(p => p.PropertyType == typeof(bool)))
				yield return NewData(typeof(BoolPropData<>), type, type, prop.Name);

			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField)
				.Where(f => f.FieldType == typeof(int)))
				yield return new IntFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty)
				.Where(p => p.PropertyType == typeof(int)))
				yield return NewData(typeof(IntPropData<>), type, type, prop.Name);

			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField)
				.Where(f => f.FieldType == typeof(float)))
				yield return new FloatFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty)
				.Where(p => p.PropertyType == typeof(float)))
				yield return NewData(typeof(FloatPropData<>), type, type, prop.Name);
		}
	}



	public abstract class BoolData : FieldData
	{
		public BoolData(Type type, string name)
			: base(type, name)
		{ }

		public abstract bool GetBoolValue(object obj);
		public override bool AppliesTo(Thing thing) => GetBoolValue(thing);
	}

	public class BoolFieldData : BoolData
	{
		public BoolFieldData(Type type, string name)
			: base(type, name)
		{		}

		private AccessTools.FieldRef<object, bool> getter;
		public override void Make()
		{
			getter ??= AccessTools.FieldRefAccess<object, bool>(AccessTools.Field(type, name));
		}

		public override bool GetBoolValue(object obj) => getter(obj);

		public override string ToString() => $"{base.ToString()} bool field";
	}

	public class BoolPropData<T> : BoolData where T : class
	{
		public BoolPropData(Type type, string name)
			: base(type, name)
		{		}

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate bool BoolGetter(T t);
		private BoolGetter getter;
		public override void Make()
		{
			getter ??= AccessTools.MethodDelegate<BoolGetter>(AccessTools.PropertyGetter(typeof(T), name));
		}

		public override bool GetBoolValue(object obj) => getter(obj as T);// please only pass in T

		public override string ToString() => $"{base.ToString()} bool property";
	}


	public abstract class IntData : FieldData
	{
		private IntRange valueRange = new(10,15);
		public override void PostExposeData()
		{
			Scribe_Values.Look(ref valueRange, "range");
		}
		public override FieldData Clone()
		{
			IntData clone = (IntData)base.Clone();
			clone.valueRange = valueRange;
			return clone;
		}

		public IntData(Type type, string name)
			: base(type, name)
		{ }

		public abstract int GetIntValue(object obj);
		public override bool AppliesTo(Thing thing) => valueRange.Includes(GetIntValue(thing));

		private string lBuffer, rBuffer;
		private string controlNameL, controlNameR;
		public override bool Draw(Listing_StandardIndent listing)
		{
			listing.NestedIndent();
			listing.Gap(listing.verticalSpacing);

			Rect rect = listing.GetRect(Text.LineHeight);
			Rect lRect = rect.LeftPart(.45f);
			Rect rRect = rect.RightPart(.45f);

			// these wil be the names generated inside TextFieldNumeric
			controlNameL = "TextField" + lRect.y.ToString("F0") + lRect.x.ToString("F0");
			controlNameR = "TextField" + rRect.y.ToString("F0") + rRect.x.ToString("F0");

			IntRange oldRange = valueRange;
			Widgets.TextFieldNumeric(lRect, ref valueRange.min, ref lBuffer, int.MinValue, int.MaxValue);
			Widgets.TextFieldNumeric(rRect, ref valueRange.max, ref rBuffer, int.MinValue, int.MaxValue);

			
			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab
				 && (GUI.GetNameOfFocusedControl() == controlNameL || GUI.GetNameOfFocusedControl() == controlNameR))
				Event.current.Use();

			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect, "-");
			Text.Anchor = default;

			listing.NestedOutdent();

			return valueRange != oldRange;
		}

		public override void DoFocus()
		{
			GUI.FocusControl(controlNameL);
		}

		public override bool Unfocus()
		{
			if (GUI.GetNameOfFocusedControl() == controlNameL || GUI.GetNameOfFocusedControl() == controlNameR)
			{
				UI.UnfocusCurrentControl();
				return true;
			}

			return false;
		}
	}

	public class IntFieldData : IntData
	{
		public IntFieldData(Type type, string name)
			: base(type, name)
		{ }

		private AccessTools.FieldRef<object, int> getter;
		public override void Make()
		{
			getter ??= AccessTools.FieldRefAccess<object, int>(AccessTools.Field(type, name));
		}

		public override int GetIntValue(object obj) => getter(obj);

		public override string ToString() => $"{base.ToString()} int field";
	}

	public class IntPropData<T> : IntData where T : class
	{
		public IntPropData(Type type, string name)
			: base(type, name)
		{ }

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate int IntGetter(T t);
		private IntGetter getter;
		public override void Make()
		{
			getter ??= AccessTools.MethodDelegate<IntGetter>(AccessTools.PropertyGetter(typeof(T), name));
		}

		public override int GetIntValue(object obj) => getter(obj as T);// please only pass in T

		public override string ToString() => $"{base.ToString()} int property";
	}



	public abstract class FloatData : FieldData
	{
		private FloatRange valueRange = new(10, 15);
		public override void PostExposeData()
		{
			Scribe_Values.Look(ref valueRange, "range");
		}
		public override FieldData Clone()
		{
			FloatData clone = (FloatData)base.Clone();
			clone.valueRange = valueRange;
			return clone;
		}

		public FloatData(Type type, string name)
			: base(type, name)
		{ }

		public abstract float GetFloatValue(object obj);
		public override bool AppliesTo(Thing thing) => valueRange.Includes(GetFloatValue(thing));

		private string lBuffer, rBuffer;
		private string controlNameL, controlNameR;
		public override bool Draw(Listing_StandardIndent listing)
		{
			listing.NestedIndent();
			listing.Gap(listing.verticalSpacing);

			Rect rect = listing.GetRect(Text.LineHeight);
			Rect lRect = rect.LeftPart(.45f);
			Rect rRect = rect.RightPart(.45f);

			// these wil be the names generated inside TextFieldNumeric
			controlNameL = "TextField" + lRect.y.ToString("F0") + lRect.x.ToString("F0");
			controlNameR = "TextField" + rRect.y.ToString("F0") + rRect.x.ToString("F0");

			FloatRange oldRange = valueRange;
			Widgets.TextFieldNumeric(lRect, ref valueRange.min, ref lBuffer, float.MinValue, float.MaxValue);
			Widgets.TextFieldNumeric(rRect, ref valueRange.max, ref rBuffer, float.MinValue, float.MaxValue);


			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab
				 && (GUI.GetNameOfFocusedControl() == controlNameL || GUI.GetNameOfFocusedControl() == controlNameR))
				Event.current.Use();

			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect, "-");
			Text.Anchor = default;

			listing.NestedOutdent();

			return valueRange != oldRange;
		}

		public override void DoFocus()
		{
			GUI.FocusControl(controlNameL);
		}

		public override bool Unfocus()
		{
			if (GUI.GetNameOfFocusedControl() == controlNameL || GUI.GetNameOfFocusedControl() == controlNameR)
			{
				UI.UnfocusCurrentControl();
				return true;
			}

			return false;
		}
	}

	public class FloatFieldData : FloatData
	{
		public FloatFieldData(Type type, string name)
			: base(type, name)
		{ }

		private AccessTools.FieldRef<object, float> getter;
		public override void Make()
		{
			getter ??= AccessTools.FieldRefAccess<object, float>(AccessTools.Field(type, name));
		}

		public override float GetFloatValue(object obj) => getter(obj);

		public override string ToString() => $"{base.ToString()} float field";
	}

	public class FloatPropData<T> : FloatData where T : class
	{
		public FloatPropData(Type type, string name)
			: base(type, name)
		{ }

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate float FloatGetter(T t);
		private FloatGetter getter;
		public override void Make()
		{
			getter ??= AccessTools.MethodDelegate<FloatGetter>(AccessTools.PropertyGetter(typeof(T), name));
		}

		public override float GetFloatValue(object obj) => getter(obj as T);// please only pass in T

		public override string ToString() => $"{base.ToString()} float property";
	}




	[StaticConstructorOnStartup]
	public class ThingQueryCustom : ThingQuery
	{
		private Type _matchType;
		private string typeName;
		private FieldData _data;
		private string fieldName;

		public Type matchType
		{
			get => _matchType;
			set
			{
				_matchType = value;
				typeName = _matchType.ToString();
			}
		}

		public FieldData data
		{
			get => _data;
			set
			{
				_data = value;
				_data?.Make();
				fieldName = _data?.name;
			}
		}



		public ThingQueryCustom()
		{
			matchType = typeof(Thing);
		}
		public override void ExposeData()
		{
			base.ExposeData();


			if (Scribe.mode == LoadSaveMode.Saving)
			{
				Scribe_Values.Look(ref typeName, "typeName");

				// FieldData just needs string name saved
				if (data != null)
				{
					Scribe_Values.Look(ref fieldName, "fieldName");
					data.PostExposeData();
				}
			}
			else if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				Scribe_Values.Look(ref typeName, "typeName");
				Scribe_Values.Look(ref fieldName, "fieldName");
				if (ParseHelper.ParseType(typeName) is Type type)
				{
					matchType = type;


					// FieldData can be null with no selection
					if (!fieldName.NullOrEmpty())
					{
						if (FieldData.FieldDataFor(matchType, fieldName) is FieldData loadedData)
						{
							data = loadedData;	// Make() and sets fieldName
							data.PostExposeData();
						}
						else
							loadError = $"Couldn't find field {typeName}.{fieldName}";
					}
				}
				else
				{
					loadError = $"Couldn't find Type {typeName}";
				}
			}
		}
		protected override ThingQuery Clone()
		{
			ThingQueryCustom clone = (ThingQueryCustom)base.Clone();
			clone.matchType = matchType;
			clone.data = data.Clone();
			return clone;
		}

		private string loadError = null;
		public override string DisableReason => loadError;

		protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			row.Label("Is type");
			RowButtonFloatMenu(matchType, FieldData.thingSubclasses, t => t.Name, newT => {matchType = newT; data = null;}, tooltip: matchType.ToString());
			row.Label("with value");
			//todo: name with ">>Spawned" for parent class fields
			RowButtonFloatMenu(data, FieldData.GetOptions(matchType, typeof(Thing)), v => v?.DisplayName(matchType) ?? "   ", newData => data = newData, tooltip: data?.ToString());

			return false;
		}
		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			return data?.Draw(listing) ?? false;
		}
		protected override void DoFocus()
		{
			data?.DoFocus();
		}

		public override bool Unfocus()
		{
			return data?.Unfocus() ?? false;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			if (!matchType.IsAssignableFrom(thing.GetType()))
				return false;

			//TODO: more types other than bool

			if (data != null)
				return data.AppliesTo(thing);

			return false;
		}
	}
}
