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
	// Parent class to hold data about all fields of any type
	public abstract class FieldData : IExposable
	{
		public Type type; // declaring type, a subclass of some parent base type (often Thing)
		public Type fieldType;
		public string name; // field name / property name

		// For load/save
		private string typeName;
		private string fieldTypeName;
		public string DisableReason =>
			type == null ? $"Could not find type {typeName} for ({fieldType}) {typeName}.{name}"
			: fieldType == null ? $"Could not find field type {fieldTypeName} for ({fieldType}) {typeName}.{name}"
			: null;

		public FieldData() { }

		public FieldData(Type type, Type fieldType, string name)
		{
			this.type = type;
			this.fieldType = fieldType;
			this.name = name;
		}

		public virtual void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");

			if (Scribe.mode == LoadSaveMode.Saving)
			{
				typeName ??= type.ToString();
				fieldTypeName ??= fieldType.ToString();
				Scribe_Values.Look(ref typeName, "type");
				Scribe_Values.Look(ref fieldType, "fieldType");
			}
			if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				Scribe_Values.Look(ref typeName, "type");
				Scribe_Values.Look(ref fieldTypeName, "fieldType");

				// Maybe null if loaded 3rdparty types
				type = ParseHelper.ParseType(typeName);
				fieldType = ParseHelper.ParseType(fieldTypeName);
			}
		}

		public virtual FieldData Clone()
		{
			FieldData clone = (FieldData)Activator.CreateInstance(GetType());

			clone.type = type;
			clone.fieldType = fieldType;
			clone.name = name;
			clone.typeName = typeName;
			clone.fieldTypeName = fieldTypeName;

			return clone;
		}

		public virtual bool Draw(Listing_StandardIndent listing) => false;
		public virtual void DoFocus() { }
		public virtual bool Unfocus() => false;

		public override string ToString() => $"({fieldType}) {type}.{name}";

		// When listing for a subclass, prepend '>' if it's a parent's field
		public string DisplayName(Type selectedType)
		{
			int diff = 0;
			while (selectedType != type)
			{
				diff++;
				selectedType = selectedType.BaseType;
				if(selectedType == null)
				{
					//ABORT though this ought not happen
					return name;
				}
			}

			return new string('>', diff) + name;
		}

		// Make should use ??= to create / store an accessor
		public abstract void Make();


		// Static listers for ease of use
		// Here we hold all fields known
		// Just the type+name until they're used, then they'll be Clone()d
		// and Make()d to create an actual accessor delegate.
		// (TODO: global dict to hold those delegates since we now clone things since they hold comparision data_
		// fields starts with just Thing subclasses known
		// mainly since QueryCustom has a dropdown to select thing subclasses
		// And we'll add in other type/fields when accessed.
		private static readonly Dictionary<Type, List<FieldData>> fields = new();
		public static readonly List<Type> thingSubclasses;


		static FieldData()
		{
			thingSubclasses = new();
			foreach (Type thingType in new Type[] { typeof(Thing) }
					.Concat(typeof(Thing).AllSubclasses().Where(ThingQuery.ValidType)))
			{
				if(FieldsFor(thingType).Any())
					thingSubclasses.Add(thingType);
			}
		}
		// Public acessors:

		public static List<FieldData> FieldsFor(Type type)
		{
			if (!fields.TryGetValue(type, out var fieldsForType))
			{
				fieldsForType = new(FindFields(type));
				fields[type] = fieldsForType;
			}
			return fieldsForType;
		}
		public static FieldData FieldDataFor(Type type, string name)
		{
			// This should only be called if it exists
			return FieldsFor(type).FirstOrDefault(fd => fd.name == name)?.Clone();
		}


		// All FieldData options for a subclass and its parents
		private static readonly Dictionary<Type, List<FieldData>> typeFields = new();
		public static List<FieldData> GetOptions(Type type, Type parentType)
		{
			if (!typeFields.TryGetValue(type, out var list))
			{
				list = FieldsFor(type);
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
			// class members
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField)
				.Where(f => f.FieldType.IsClass))
				yield return new ClassFieldData(type, field.FieldType, field.Name);


			// valuetypes
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

	// FieldDataMember
	public abstract class FieldDataMember : FieldData
	{
		public FieldDataMember() { }
		public FieldDataMember(Type type, Type fieldType, string name)
			: base(type, fieldType, name)
		{ }

		public abstract object GetMember(object obj);
	}

	public class ClassFieldData : FieldDataMember
	{
		public ClassFieldData() { }
		public ClassFieldData(Type type, Type fieldType, string name)
			: base(type, fieldType, name)
		{ }

		private AccessTools.FieldRef<object, object> getter;
		public override void Make()
		{
			getter ??= AccessTools.FieldRefAccess<object, object>(AccessTools.Field(type, name));
		}

		public override object GetMember(object obj) => getter(obj);
	}


	// Comparers that end the sequence

	public abstract class FieldDataComparer : FieldData
	{
		public FieldDataComparer() { }
		public FieldDataComparer(Type type, Type fieldType, string name)
			: base(type, fieldType, name)
		{ }

		public abstract bool AppliesTo(object obj);

	}


	public abstract class BoolData : FieldDataComparer
	{
		public BoolData() { }
		public BoolData(Type type, string name)
			: base(type, typeof(bool), name)
		{ }

		public abstract bool GetBoolValue(object obj);
		public override bool AppliesTo(object obj) => GetBoolValue(obj);
	}

	public class BoolFieldData : BoolData
	{
		public BoolFieldData() { }
		public BoolFieldData(Type type, string name)
			: base(type, name)
		{		}

		private AccessTools.FieldRef<object, bool> getter;
		public override void Make()
		{
			getter ??= AccessTools.FieldRefAccess<object, bool>(AccessTools.Field(type, name));
		}

		public override bool GetBoolValue(object obj) => getter(obj);
	}

	public class BoolPropData<T> : BoolData where T : class
	{
		public BoolPropData() { }
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
	}


	public abstract class IntData : FieldDataComparer
	{
		private IntRange valueRange = new(10,15);
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref valueRange, "range");
		}
		public override FieldData Clone()
		{
			IntData clone = (IntData)base.Clone();
			clone.valueRange = valueRange;
			return clone;
		}

		public IntData() { }
		public IntData(Type type, string name)
			: base(type, typeof(int), name)
		{ }

		public abstract int GetIntValue(object obj);
		public override bool AppliesTo(object obj) => valueRange.Includes(GetIntValue(obj));

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
		public IntFieldData() { }
		public IntFieldData(Type type, string name)
			: base(type, name)
		{ }

		private AccessTools.FieldRef<object, int> getter;
		public override void Make()
		{
			getter ??= AccessTools.FieldRefAccess<object, int>(AccessTools.Field(type, name));
		}

		public override int GetIntValue(object obj) => getter(obj);
	}

	public class IntPropData<T> : IntData where T : class
	{
		public IntPropData() { }
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
	}



	public abstract class FloatData : FieldDataComparer
	{
		private FloatRange valueRange = new(10, 15);
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref valueRange, "range");
		}
		public override FieldData Clone()
		{
			FloatData clone = (FloatData)base.Clone();
			clone.valueRange = valueRange;
			return clone;
		}

		public FloatData() { }
		public FloatData(Type type, string name)
			: base(type, typeof(float), name)
		{ }

		public abstract float GetFloatValue(object obj);
		public override bool AppliesTo(object obj) => valueRange.Includes(GetFloatValue(obj));

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
		public FloatFieldData() { }
		public FloatFieldData(Type type, string name)
			: base(type, name)
		{ }

		// generics not needed for FieldRef as it can handle a float fine huh?
		private AccessTools.FieldRef<object, float> getter;
		public override void Make()
		{
			getter ??= AccessTools.FieldRefAccess<object, float>(AccessTools.Field(type, name));
		}

		public override float GetFloatValue(object obj) => getter(obj);
	}

	public class FloatPropData<T> : FloatData where T : class
	{
		public FloatPropData() { }
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
	}




	[StaticConstructorOnStartup]
	public class ThingQueryCustom : ThingQuery
	{
		public Type matchType;
		private string matchTypeName;
		private List<FieldDataMember> memberChain;
		private FieldDataComparer _member;

		public FieldDataComparer member
		{
			get => _member;
			set
			{
				_member = value;
				if(member != null)
				{
					_member.Make();
					Focus();
				}
			}
		}


		public ThingQueryCustom()
		{
			matchType = typeof(Thing);
			memberChain = new();
		}
		public override void ExposeData()
		{
			base.ExposeData();

			if (Scribe.mode == LoadSaveMode.Saving)
			{
				matchTypeName ??= matchType.ToString();
				Scribe_Values.Look(ref matchTypeName, "matchType");
			}

			Scribe_Collections.Look(ref memberChain, "memberChain");
			Scribe_Deep.Look(ref _member, "member");

			if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				Scribe_Values.Look(ref matchTypeName, "matchType");
				if (ParseHelper.ParseType(matchTypeName) is Type type)
				{
					matchType = type;
				}
				else
				{
					loadError = $"Couldn't find Type {matchTypeName}";
				}

				member?.Make();
				loadError ??= member?.DisableReason;

				foreach (var memberData in memberChain)
				{
					memberData.Make();
					loadError ??= memberData?.DisableReason;
				}
			}
		}
		protected override ThingQuery Clone()
		{
			ThingQueryCustom clone = (ThingQueryCustom)base.Clone();
			clone.matchType = matchType;
			clone.matchTypeName = matchTypeName;
			clone._member = (FieldDataComparer)member?.Clone();
			clone.member?.Make();
			clone.memberChain = new(memberChain.Select(d => d.Clone() as FieldDataMember));
			foreach (var memberData in clone.memberChain)
				memberData.Make();
			clone.loadError = loadError;
			return clone;
		}

		private string loadError = null;
		public override string DisableReason => loadError;

		private void SelectMatchType(Type type)
		{
			loadError = null;
			matchType = type;
			member = null;
			memberChain.Clear();
		}
		private void SetMember(FieldData newData)
		{
			loadError = null;

			FieldData addData = newData.Clone();

			if(addData is FieldDataComparer addDataCompare)
			{
				member = addDataCompare;
			}
			else if(addData is FieldDataMember addDataMember)
			{
				memberChain.Add(addDataMember);
				addDataMember.Make();
				member = null; //to be selected
			}
			//memberChain
		}

		private void SetMemberAt(FieldData newData, int i)
		{
			memberChain.RemoveRange(i, memberChain.Count - i);

			SetMember(newData);
		}

		protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			row.Label("Is type");
			RowButtonFloatMenu(matchType, FieldData.thingSubclasses, t => t.Name, SelectMatchType, tooltip: matchType.ToString());

			Type type = matchType;
			Type parentType = typeof(Thing);
			for (int i = 0; i < memberChain.Count; i++)
			{
				int locali = i;
				var memberData = memberChain[i];

				row.Label(".");
				//todo: dropdown name with ">>Spawned" for parent class fields but draw button without
				RowButtonFloatMenu(memberData, FieldData.GetOptions(type, parentType), v => v?.DisplayName(type) ?? "   ", newData => SetMemberAt(newData, locali), tooltip: memberData.ToString());;

				parentType = type = memberData.fieldType;
			}

			row.Label(":");
			//todo: dropdown name with ">>Spawned" for parent class fields but draw button without
			RowButtonFloatMenu(member, FieldData.GetOptions(type, parentType), v => v?.DisplayName(type) ?? "   ", SetMember, tooltip: member?.ToString());

			return false;
		}
		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			return member?.Draw(listing) ?? false;
		}
		protected override void DoFocus()
		{
			member?.DoFocus();
		}

		public override bool Unfocus()
		{
			return member?.Unfocus() ?? false;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			if (!matchType.IsAssignableFrom(thing.GetType()))
				return false;

			object obj = thing;
			foreach (var memberData in memberChain)
			{
				if (!memberData.type.IsAssignableFrom(obj.GetType()))
					// redundant on first call oh well
					// matchType is needed above if you're checking Pawn.def, as for memberData it's Thing.def
					return false;

				obj = memberData.GetMember(obj);

				if (obj == null)
					return false;
			}

			if (member != null)
				return member.AppliesTo(obj);

			return false;
		}
	}
}
