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
		public string name; // field name / property name / sometimes method name

		// For load/save. These are technicaly re-discoverable but this is easier.
		private string typeName;
		private string fieldTypeName;
		public string DisableReason =>
			type == null ? $"Could not find type {typeName} for ({fieldTypeName}) {typeName}.{name}"
			: fieldType == null ? $"Could not find field type {fieldTypeName} for ({fieldTypeName}) {typeName}.{name}"
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
				typeName = type?.ToString() ?? typeName;
				fieldTypeName = fieldType?.ToString() ?? fieldTypeName;
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

		public virtual bool Draw(Listing_StandardIndent listing, bool locked) => false;
		public virtual void DoFocus() { }
		public virtual bool Unfocus() => false;



		// The base name, usually just the field name ezpz
		// override for exceptions e.g. "GetComp<CompPower>()" or "as CompPowerTradable"
		public virtual string Name => name;

		// TextName, a hopefully code-accurate part of chain, e.g. building .GetComp<CompPower>() .pct ...
		// overrides for " as CompPowerTradable" with added instead of dot.
		public virtual string TextName => $".{Name}";

		// SuggestionName as shown in the dropdown
		public virtual string SuggestionName => Name;
		// AutoFillName fills in the textfield when you back up 
		public virtual string AutoFillName => SuggestionName;
		// FilterName, what is searched to make suggestions, e.g. add "is not !="
		public virtual string FilterName => SuggestionName;

		// TooltipDetails for field/type, not so much the readable TextName: e.g. (CompPower) ThingWithComps.GetComp
		public virtual string TooltipDetails => $"({fieldType.ToStringSimple()}) {type.ToStringSimple()}.{name}";

		public override string ToString() => TooltipDetails;



		protected List<string> filterMatches;
		private void MakeFilterMatches()
		{
			if (filterMatches == null)
			{
				filterMatches = new();
				filterMatches.Add(fieldType.Name.ToLower());
				filterMatches.AddRange(FilterName.ToLower().Split(' ', '<', '>', '(', ')'));
			}
		}
		public bool ShouldShow(string filterInput)
		{
			foreach (var filterMatch in filterMatches)
				if (filterMatch.Contains(filterInput))
					return true;

			return false;
		}
		public bool ShouldShow(List<string> filterInputs)
		{
			MakeFilterMatches();
			return filterInputs.All(ShouldShow);
		}

		protected ThingQueryCustom parentQuery;
		public void Make(ThingQueryCustom p)
		{
			parentQuery = p;
			MakeAccessor();
		}
		// MakeAccessor should use ??= to create / store an accessor
		// TODO store it globally by type instead of one for each FieldData object
		public abstract void MakeAccessor();

		// Static listers for ease of use
		// Here we hold all fields known
		// Just the type+name until they're used, then they'll be Clone()d
		// and Make()d to create an actual accessor delegate.
		// (TODO: global dict to hold those delegates since we now clone things since they hold comparision data)
		// And we'll add in other type/fields when accessed.
		private static readonly Dictionary<Type, List<FieldData>> declaredFields = new();
		public static List<FieldData> FieldsFor(Type type)
		{
			if (!declaredFields.TryGetValue(type, out var fieldsForType))
			{
				fieldsForType = new(FindFields(type));
				declaredFields[type] = fieldsForType;
			}
			return fieldsForType;
		}


		// All FieldData options for a subclass and its parents
		private static readonly Dictionary<Type, List<FieldData>> typeFieldOptions = new();
		private static readonly Type[] baseTypes = new[] { typeof(Thing), typeof(Def), typeof(object), null};
		public static List<FieldData> GetOptions(Type baseType)
		{
			if (!typeFieldOptions.TryGetValue(baseType, out var list))
			{
				Type type = baseType;

				list = new();
				typeFieldOptions[type] = list;

				// Special Def comparators
				if (typeof(Def).IsAssignableFrom(type))
				{
					list.Add(NewData(typeof(DefData<>), new Type[] { type }));
				}

				// Long list of Field comparators
				list.AddRange(FieldsFor(type));

				// And parent class comparators
				while (!baseTypes.Contains(type))
				{
					type = type.BaseType;
					list.AddRange(FieldsFor(type));
				}

				// Special accessors
				foreach (Type subType in baseType.AllSubclassesNonAbstract())
					list.Add(new AsSubclassData(baseType, subType));

				// Special comparators
				list.Add(new ObjIsNull(typeof(object)));
				list.Add(new ObjIsNotNull(typeof(object)));
			}
			return list;
		}




		// Here we find all fields
		// TODO: more types, meaning FieldData subclasses for each type
		private static FieldData NewData(Type fieldDataType, Type[] genericTypes, params object[] args) =>
			(FieldData)Activator.CreateInstance(fieldDataType.MakeGenericType(genericTypes), args);


		private static bool ValidProp(PropertyInfo p) =>
			p.CanRead && p.GetMethod.GetParameters().Length == 0;

		private static readonly Type[] blacklist = new Type[]
			{ typeof(string), typeof(Type), typeof(Action), typeof(MemberInfo), typeof(Map)};
		private static bool ValidClassType(Type type) =>
			type.IsClass && !blacklist.Any(b => b.IsAssignableFrom(type));

		private static Type EnumerableType(Type type) =>
			type.GetInterfaces()
			.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			?.GetGenericArguments()[0];

		const BindingFlags bFlags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;
		private static IEnumerable<FieldData> FindFields(Type type)
		{
			// class members
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (ValidClassType(field.FieldType) && EnumerableType(field.FieldType) == null)
						yield return new ClassFieldData(type, field.FieldType, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (ValidClassType(prop.PropertyType) && EnumerableType(prop.PropertyType) == null)
						yield return NewData(typeof(ClassPropData<>), new[] { type }, prop.PropertyType, prop.Name);

			// enumerbale class members
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (ValidClassType(field.FieldType) && EnumerableType(field.FieldType) is Type enumType)
						yield return new EnumerableFieldData(type, enumType, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (ValidClassType(prop.PropertyType) && EnumerableType(prop.PropertyType) is Type enumType)
						yield return NewData(typeof(EnumerablePropData<>), new[] { type }, enumType, prop.Name);

			// ThingComp, Hard coded for ThingWithComps
			if (type == typeof(ThingWithComps))
			{
				foreach(Type compType in GenTypes.AllSubclasses(typeof(ThingComp)))
					yield return NewData(typeof(ThingCompData<>), new[] { compType });
			}

			// valuetypes
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (field.FieldType == typeof(bool))
					yield return new BoolFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (prop.PropertyType == typeof(bool))
					yield return NewData(typeof(BoolPropData<>), new[] { type }, prop.Name);


			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (field.FieldType == typeof(int))
					yield return new IntFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (prop.PropertyType == typeof(int))
					yield return NewData(typeof(IntPropData<>), new[] { type }, prop.Name);


			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (field.FieldType == typeof(float))
					yield return new FloatFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (prop.PropertyType == typeof(float))
					yield return NewData(typeof(FloatPropData<>), new[] { type }, prop.Name);


			// strings
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (field.FieldType == typeof(string))
					yield return new StringFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (prop.PropertyType == typeof(string))
					yield return NewData(typeof(StringPropData<>), new[] { type }, prop.Name);
			


			// enums
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (field.FieldType.IsEnum)
					yield return NewData(typeof(EnumFieldData<>), new[] { field.FieldType }, type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (prop.PropertyType.IsEnum)
					yield return NewData(typeof(EnumPropData<,>), new[] { type, prop.PropertyType }, prop.Name);
		}
	}



	// FieldData subclasses of FieldDataMember handle gettings members that hold/return a Class
	// FieldDataMember can be chained e.g. thing.def.building
	// The final FieldData after the chain must be a comparer (below) 
	// That one actually does the filter on the value that it gets
	// e.g. thing.def.building.uninstallWork > 300
	public abstract class FieldDataMember : FieldData
	{
		public FieldDataMember() { }
		public FieldDataMember(Type type, Type fieldType, string name)
			: base(type, fieldType, name)
		{ }
	}
	// One for classes that are IEnumerable (special handler in Query.AppliesTo does .Any())
	public abstract class FieldDataEnumerableMember : FieldDataMember
	{
		public FieldDataEnumerableMember() { }
		public FieldDataEnumerableMember(Type type, Type fieldType, string name)
			: base(type, fieldType, name)
		{ }

		public override string TextName =>
			base.TextName + ".Any: ";

		public abstract IEnumerable<object> GetMembers(object obj);
	}
	// One for any other class
	public abstract class FieldDataClassMember : FieldDataMember
	{
		public FieldDataClassMember() { }
		public FieldDataClassMember(Type type, Type fieldType, string name)
			: base(type, fieldType, name)
		{ }

		public abstract object GetMember(object obj);
	}

	// Subclasses to get a simple class object field/property
	public class ClassFieldData : FieldDataClassMember
	{
		public ClassFieldData() { }
		public ClassFieldData(Type type, Type fieldType, string name)
			: base(type, fieldType, name)
		{ }

		private AccessTools.FieldRef<object, object> getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.FieldRefAccess<object, object>(AccessTools.DeclaredField(type, name));
		}

		public override object GetMember(object obj) => getter(obj);
	}

	public class ClassPropData<T> : FieldDataClassMember
	{
		public ClassPropData() { }
		public ClassPropData(Type fieldType, string name)
			: base(typeof(T), fieldType, name)
		{ }

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate object ClassGetter(T t);
		private ClassGetter getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.MethodDelegate<ClassGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
		}

		public override object GetMember(object obj) => getter((T)obj);
	}


	// FieldData for ThingComp, a hand-picked handler for this generic method
	public class ThingCompData<T> : FieldDataClassMember where T : ThingComp 
	{
		public ThingCompData()
			: base(typeof(ThingWithComps), typeof(T), nameof(ThingWithComps.GetComp))
		{ }

		delegate T ThingCompGetter(ThingWithComps t);
		private ThingCompGetter getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.MethodDelegate<ThingCompGetter>(AccessTools.DeclaredMethod(type, name).MakeGenericMethod(fieldType));
		}
		public override object GetMember(object obj) => getter(obj as ThingWithComps);

		
		public override string TextName =>
			$".GetComp<{fieldType.Name}>()";
		public override string SuggestionName => $"GetComp<{fieldType.Name}>";
	}


	//FieldData for "as subclass"
	public class AsSubclassData: FieldDataClassMember
	{
		public AsSubclassData() { }
		public AsSubclassData(Type type, Type subType)
			: base(type, subType, "as subclass")
		{ }

		public override string TextName =>
			$" as {fieldType.Name}";
		public override string SuggestionName =>
			$"as {fieldType.Name}";

		public override void MakeAccessor() { }

		// fun fact I spend like 30 minutes writing this trying to convert obj to fieldType then realized it's returning as object anyway
		// Then later realized I needed to check the type because Thing as Pawn is null when not pawn.
		public override object GetMember(object obj)
		{
			if(fieldType.IsAssignableFrom(obj.GetType()))
				return obj;
			return null;
		}
	}



	// FieldDataEnumerableMember
	// Subclasses to get an enumerable field/property
	public class EnumerableFieldData : FieldDataEnumerableMember
	{
		public EnumerableFieldData() { }
		public EnumerableFieldData(Type type, Type fieldType, string name)
			: base(type, fieldType, name)
		{ }

		private AccessTools.FieldRef<object, IEnumerable<object>> getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.FieldRefAccess<object, IEnumerable<object>>(AccessTools.DeclaredField(type, name));
		}

		public override IEnumerable<object> GetMembers(object obj) => getter(obj);
	}

	public class EnumerablePropData<T> : FieldDataEnumerableMember
	{
		public EnumerablePropData() { }
		public EnumerablePropData(Type fieldType, string name)
			: base(typeof(T), fieldType, name)
		{ }

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate IEnumerable<object> ClassGetter(T t);
		private ClassGetter getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.MethodDelegate<ClassGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
		}

		public override IEnumerable<object> GetMembers(object obj) => getter((T)obj);
	}


	// FieldDataComparer
	// FieldData subclasses that compare valuetypes to end the sequence
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
		public override void MakeAccessor()
		{
			getter ??= AccessTools.FieldRefAccess<object, bool>(AccessTools.DeclaredField(type, name));
		}

		public override bool GetBoolValue(object obj) => getter(obj);
	}

	public class BoolPropData<T> : BoolData where T : class
	{
		public BoolPropData() { }
		public BoolPropData(string name)
			: base(typeof(T), name)
		{		}

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate bool BoolGetter(T t);
		private BoolGetter getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.MethodDelegate<BoolGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
		}

		public override bool GetBoolValue(object obj) => getter((T)obj);// please only pass in T
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
		public override bool Draw(Listing_StandardIndent listing, bool locked)
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
			if (locked)
			{
				Widgets.Label(lRect, lBuffer);
				Widgets.Label(rRect, rBuffer);
			}
			else
			{
				Widgets.TextFieldNumeric(lRect, ref valueRange.min, ref lBuffer, int.MinValue, int.MaxValue);
				Widgets.TextFieldNumeric(rRect, ref valueRange.max, ref rBuffer, int.MinValue, int.MaxValue);
			}
			
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
		public override void MakeAccessor()
		{
			getter ??= AccessTools.FieldRefAccess<object, int>(AccessTools.DeclaredField(type, name));
		}

		public override int GetIntValue(object obj) => getter(obj);
	}

	public class IntPropData<T> : IntData where T : class
	{
		public IntPropData() { }
		public IntPropData(string name)
			: base(typeof(T), name)
		{ }

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate int IntGetter(T t);
		private IntGetter getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.MethodDelegate<IntGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
		}

		public override int GetIntValue(object obj) => getter((T)obj);// please only pass in T
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
		public override bool Draw(Listing_StandardIndent listing, bool locked)
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
			if (locked)
			{
				Widgets.Label(lRect, lBuffer);
				Widgets.Label(rRect, rBuffer);
			}
			else
			{
				Widgets.TextFieldNumeric(lRect, ref valueRange.min, ref lBuffer, float.MinValue, float.MaxValue);
				Widgets.TextFieldNumeric(rRect, ref valueRange.max, ref rBuffer, float.MinValue, float.MaxValue);
			}

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
		public override void MakeAccessor()
		{
			getter ??= AccessTools.FieldRefAccess<object, float>(AccessTools.DeclaredField(type, name));
		}

		public override float GetFloatValue(object obj) => getter(obj);
	}

	public class FloatPropData<T> : FloatData where T : class
	{
		public FloatPropData() { }
		public FloatPropData(string name)
			: base(typeof(T), name)
		{ }

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate float FloatGetter(T t);
		private FloatGetter getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.MethodDelegate<FloatGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
		}

		public override float GetFloatValue(object obj) => getter((T)obj);// please only pass in T
	}



	public abstract class EnumData<TEnum> : FieldDataComparer where TEnum : Enum
	{
		public MaskFilterEnum<TEnum> mask;
		private readonly bool flags;

		public EnumData()
		{
			mask = new();
			flags = typeof(TEnum).GetCustomAttributes<FlagsAttribute>().Any();
		}
		public EnumData(Type type, string name)
			: base(type, typeof(TEnum), name)
		{
			mask = new();
			flags = typeof(TEnum).GetCustomAttributes<FlagsAttribute>().Any();
		}
		public override void ExposeData()
		{
			base.ExposeData();
			mask.PostExposeData();
		}
		public override FieldData Clone()
		{
			EnumData<TEnum> clone = (EnumData<TEnum>)base.Clone();
			clone.mask = (MaskFilterEnum<TEnum>)mask.Clone();
			return clone;
		}

		public abstract TEnum GetEnumValue(object obj);

		public override bool AppliesTo(object obj)
		{
			TEnum objEnum = GetEnumValue(obj);
			if (flags)
			{
				return mask.AppliesTo(objEnum);
			}
			else
			{
				return mask.mustHave.Equals(objEnum);
			}
		}

		public override bool Draw(Listing_StandardIndent listing, bool locked)
		{
			Rect rect = listing.GetRect(Text.LineHeight);
			if (flags)
			{
				// dropdown mask to set filter to check if flags must be on or off
				mask.DrawButton(rect, parentQuery: parentQuery);
			}
			else
			{
				Text.Anchor = TextAnchor.LowerRight;
				Widgets.Label(rect.LeftHalf(), "Is: ");
				Text.Anchor = default;
				// Dropdown enum selector
				if (Widgets.ButtonText(rect.RightHalf(), mask.mustHave.ToString()))
					parentQuery.DoFloatOptions(Enum.GetValues(typeof(TEnum)) as IEnumerable<TEnum>, e => e.ToString(), newValue => mask.mustHave = newValue);
			}
			return false;
		}
	}



	public class EnumFieldData<TEnum> : EnumData<TEnum> where TEnum : Enum
	{
		public EnumFieldData() { }
		public EnumFieldData(Type type, string name)
			: base(type, name)
		{ }

		private AccessTools.FieldRef<object, TEnum> getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.FieldRefAccess<object, TEnum>(AccessTools.DeclaredField(type, name));
		}

		public override TEnum GetEnumValue(object obj) => getter(obj);
	}

	public class EnumPropData<T, TEnum> : EnumData<TEnum> where TEnum : Enum where T : class
	{
		public EnumPropData() { }
		public EnumPropData(string name)
			: base(typeof(T), name)
		{ }

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate TEnum EnumGetter(T t);
		private EnumGetter getter;
		public override void MakeAccessor()
		{
			getter ??= AccessTools.MethodDelegate<EnumGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
		}

		public override TEnum GetEnumValue(object obj) => getter((T)obj);// please only pass in T
	}


	public class ObjIsNull : FieldDataComparer
	{
		public ObjIsNull() { }
		public ObjIsNull(Type type)
			: base(type, typeof(bool), "is null")
		{ }

		public override string TextName => " == null";
		public override string FilterName => "== is null";

		// Even though code below doesn't call this as it checks obj == null before passing to FieldDataComparer
		public override bool AppliesTo(object obj) => obj == null;
		public override void MakeAccessor() { }
	}

	public class ObjIsNotNull : FieldDataComparer
	{
		public ObjIsNotNull() { }
		public ObjIsNotNull(Type type)
			: base(type, typeof(bool), "is not null")
		{ }

		public override string TextName => " != null";
		public override string FilterName => "!= is not null";

		public override bool AppliesTo(object obj) => obj != null;
		public override void MakeAccessor() { }
	}


	public abstract class StringData : FieldDataComparer
	{
		private string compareTo;
		private bool exact;
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref compareTo, "compareTo");
			Scribe_Values.Look(ref exact, "exact");
		}
		public override FieldData Clone()
		{
			StringData clone = (StringData)base.Clone();
			clone.compareTo = compareTo;
			clone.exact = exact;
			return clone;
		}

		public StringData() { }
		public StringData(Type type, string name)
			: base(type, typeof(string), name)
		{ }

		public abstract string GetStringValue(object obj);
		public override bool AppliesTo(object obj)
		{
			string value = GetStringValue(obj);
			return exact ? value == compareTo : value.Contains(compareTo);
		}

		private string controlName;
		public override void MakeAccessor()
		{
			controlName = $"STRING_DATA{parentQuery.Id}";
		}
		public override bool Draw(Listing_StandardIndent listing, bool locked)
		{
			listing.NestedIndent();
			listing.Gap(listing.verticalSpacing);

			Rect rect = listing.GetRect(Text.LineHeight);
			Rect lRect = rect.LeftPart(.2f);
			Rect rRect = rect.RightPart(.78f);

			bool changed = false;

			if (Widgets.ButtonText(lRect, exact ? "Is: " : "Contains:"))
			{
				exact = !exact;
				changed = true;
			}

			if (locked)
			{
				Widgets.Label(rRect, compareTo);
			}
			else
			{
				changed |= TDWidgets.TextField(rRect, ref compareTo);
			}

			if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Tab
				 && (GUI.GetNameOfFocusedControl() == controlName))
				Event.current.Use();

			listing.NestedOutdent();

			return changed;
		}

		public override void DoFocus()
		{
			GUI.FocusControl(controlName);
		}

		public override bool Unfocus()
		{
			if (GUI.GetNameOfFocusedControl() == controlName)
			{
				UI.UnfocusCurrentControl();
				return true;
			}

			return false;
		}
	}

	public class StringFieldData : StringData
	{
		public StringFieldData() { }
		public StringFieldData(Type type, string name)
			: base(type, name)
		{ }

		private AccessTools.FieldRef<object, string> getter;
		public override void MakeAccessor()
		{
			base.MakeAccessor();
			getter ??= AccessTools.FieldRefAccess<object, string>(AccessTools.DeclaredField(type, name));
		}

		public override string GetStringValue(object obj) => getter(obj);
	}

	public class StringPropData<T> : StringData where T : class
	{
		public StringPropData() { }
		public StringPropData(string name)
			: base(typeof(T), name)
		{ }

		// Generics are required for this delegate to exist so that it's actually fast.
		delegate string StringGetter(T t);
		private StringGetter getter;
		public override void MakeAccessor()
		{
			base.MakeAccessor();
			getter ??= AccessTools.MethodDelegate<StringGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
		}

		public override string GetStringValue(object obj) => getter((T)obj);// please only pass in T
	}


	public class DefData<TDef> : FieldDataComparer where TDef : Def, new()
	{
		public TDef compareTo;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref compareTo, "compareTo");
		}
		public override FieldData Clone()
		{
			DefData<TDef> clone = (DefData<TDef>)base.Clone();
			clone.compareTo = compareTo;
			return clone;
		}

		public DefData()
			: base(typeof(TDef), typeof(bool), "is exact def")
		{
		}

		public override string TextName => $"== {compareTo.defName}";

		public override bool AppliesTo(object obj) => obj == compareTo;

		public override void MakeAccessor()
		{
			compareTo = DefDatabase<TDef>.AllDefsListForReading.First();
		}

		public override bool Draw(Listing_StandardIndent listing, bool locked)
		{
			listing.NestedIndent();
			listing.Gap(listing.verticalSpacing);

			Rect rect = listing.GetRect(Text.LineHeight);
			if (Widgets.ButtonText(rect, compareTo.GetLabel()))
				parentQuery.DoFloatOptions(DefDatabase<TDef>.AllDefs, LabelByDefName.GetLabel, newValue => compareTo = newValue);

			listing.NestedOutdent();

			return false;
		}
	}

	// ThingQueryCustom
	// And we finally get to the Query itself that uses the above FieldData subclasses.
	[StaticConstructorOnStartup]
	public class ThingQueryCustom : ThingQuery
	{
		public static readonly List<Type> thingSubclasses;
		static ThingQueryCustom()
		{
			thingSubclasses = new();
			foreach (Type thingType in new Type[] { typeof(Thing) }
					.Concat(typeof(Thing).AllSubclasses().Where(ValidType)))
			{
				thingSubclasses.Add(thingType);
			}
		}

		public Type matchType = typeof(Thing);
		public List<FieldDataMember> memberChain = new();
		public FieldDataComparer member;
		public string memberStr = "";



		private Type _nextType;
		private List<FieldData> nextOptions;
		public Type nextType
		{
			get => _nextType;
			set
			{
				_nextType = value;
				nextOptions = FieldData.GetOptions(_nextType);

				ParseTextField();
				suggestionI = 0;
				suggestionScroll = default;
			}
		}


		private string loadError = null;
		public override string DisableReason => loadError;

		public ThingQueryCustom()
		{
			nextType = matchType;


			controlName = $"THING_QUERY_CUSTOM_INPUT{id}";
		}

		private string matchTypeName;
		public override void ExposeData()
		{
			base.ExposeData();

			if (Scribe.mode == LoadSaveMode.Saving)
			{
				matchTypeName = matchType?.ToString() ?? matchTypeName;
				Scribe_Values.Look(ref matchTypeName, "matchType");
			}

			Scribe_Collections.Look(ref memberChain, "memberChain");
			Scribe_Deep.Look(ref member, "member");
			Scribe_Values.Look(ref memberStr, "memberStr");

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

				member?.Make(this);
				loadError = member?.DisableReason;

				foreach (var memberData in memberChain)
				{
					memberData.Make(this);
					loadError ??= memberData?.DisableReason;
				}

				nextType = memberChain.LastOrDefault()?.fieldType ?? matchType;
			}
		}
		protected override ThingQuery Clone()
		{
			ThingQueryCustom clone = (ThingQueryCustom)base.Clone();

			clone.matchType = matchType;
			clone.matchTypeName = matchTypeName;

			clone.member = (FieldDataComparer)member?.Clone();
			clone.member?.Make(clone);
			clone.memberStr = memberStr;

			clone.memberChain = new(memberChain.Select(d => d.Clone() as FieldDataMember));
			foreach (var memberData in clone.memberChain)
				memberData.Make(clone);

			clone.nextType = nextType;

			clone.loadError = loadError;

			return clone;
		}

		private void SetMatchType(Type type)
		{
			loadError = null;

			matchType = type;

			member = null;
			memberChain.Clear();
			memberStr = "";
			nextType = matchType;

			Focus();
		}

		private void SetMember(FieldData newData)
		{
			keepAliveSuggestions = false;
			focused = false;

			FieldData addData = newData.Clone();

			if (addData is FieldDataComparer addDataCompare)
			{
				member = addDataCompare;
				member.Make(this);

				UI.UnfocusCurrentControl();
				Focus();	//next frame

				// Keep nextType in case you want to change the compared field
				memberStr = "";
				ParseTextField();
			}
			else if(addData is FieldDataMember addDataMember)
			{
				memberChain.Add(addDataMember);
				addDataMember.Make(this);

				member = null; //to be selected
				memberStr = "";


				nextType = addDataMember.fieldType;
			}
		}


		// After text field edit, parse string for new filtered field suggestions
		private List<string> filters = new ();
		private List<FieldData> suggestions = new();
		private int suggestionI;
		private Vector2 suggestionScroll;
		private readonly float suggestionRowHeight = Text.LineHeightOf(GameFont.Small);
		private void ParseTextField()
		{
			filters.Clear();
			filters.AddRange(memberStr.ToLower().Split(' ', '<', '>', '(', ')'));

			suggestions.Clear();
			suggestions.AddRange(nextOptions.Where(d => d.ShouldShow(filters)));

			AdjustForSuggestion();
		}
		private void AdjustForSuggestion()
		{
			suggestionI = Mathf.Clamp(suggestionI, 0, suggestions.Count - 1);

			suggestionScroll.y = Mathf.Clamp(suggestionScroll.y, (suggestionI - 9) * suggestionRowHeight, suggestionI * suggestionRowHeight);
		}

		// After tab/./enter : select first suggested field and add to memberChain / create comparable member
		// This might remove comparision settings... Could check if it already exists
		// but why would you set the name if you already had the comparison set up?
		public bool AutoComplete()
		{
			FieldData newData = suggestions.ElementAtOrDefault(suggestionI);
			if (newData != null)
			{
				SetMember(newData);
				return true;
			}
			return false;
		}

		protected override float RowGap => 0;
		private readonly string controlName;
		private bool needMoveLineEnd;
		private bool focused;
		private bool keepAliveSuggestions;
		protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			/*
			row.Label("Is type");
			RowButtonFloatMenu(matchType, thingSubclasses, t => t.Name, SelectMatchType, tooltip: matchType.ToString());

			for (int i = 0; i < memberChain.Count; i++)
			{
				int locali = i;
				var memberData = memberChain[i];

				row.Label(".");
				//todo: dropdown name with ">>Spawned" for parent class fields but draw button without
				RowButtonFloatMenu(memberData, FieldData.GetOptions(type), v => v.DisplayName(type), newData => SetMemberAt(newData, locali), tooltip: memberData.ToString());;

				type = memberData.fieldType;
			}
			*/
			// Whatever comes out of the memberchain should be some other class, not a valuetype to be compared



			// Cast the thing (so often useful it's always gonna be here)
			row.Label("thing as ");
			RowButtonFloatMenu(matchType, thingSubclasses, t => t.Name, SetMatchType, tooltip: matchType.ToString());


			// Draw (append) The memberchain
			foreach (var memberLink in memberChain)
			{
				row.LabelWithTags(memberLink.TextName, tooltip: memberLink.TooltipDetails);
				row.Gap(-2);
			}


			// handle special input before the textfield grabs it

			// Fuckin double-events for keydown means we get two events, keycode and char,
			// so unfocusing on keycode means we're not focused for the char event
			// Remember between events but you gotta reset some time so reset it on repaint I guess
			if(Event.current.type == EventType.Repaint)
				focused = GUI.GetNameOfFocusedControl() == controlName;
			else
				focused |= GUI.GetNameOfFocusedControl() == controlName;

			bool keyDown = Event.current.type == EventType.KeyDown;
			KeyCode keyCode = Event.current.keyCode;
			char ch = Event.current.character;


			bool changed = false;

			if (keyDown)
			{
				if (focused)
				{
					// suppress the second key events that come after pressing tab/return/period
					if ((ch == '	' || ch == '\n' || ch == '.'))
					{
						Event.current.Use();
					}
					// Autocomplete after tab/./enter
					else if (keyCode == KeyCode.Tab
						|| keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter
						|| keyCode == KeyCode.Period || keyCode == KeyCode.KeypadPeriod)
					{
						changed = AutoComplete();

						Event.current.Use();
					}
					// Backup to last member if deleting empty string after "member."
					else if (keyCode == KeyCode.Backspace)
					{
						if (memberStr == "" && memberChain.Count > 0)
						{
							member = null;
							var lastMember = memberChain.Pop();

							needMoveLineEnd = true;
							memberStr = lastMember.AutoFillName;
							nextType = memberChain.LastOrDefault()?.fieldType ?? matchType;

							Event.current.Use();
						}
					}
				}

				// Maintain keypresses when navigatin'
				if (focused || keepAliveSuggestions)
				{
					if (keyCode == KeyCode.DownArrow)
					{
						suggestionI++;
						AdjustForSuggestion();

						Event.current.Use();
					}
					else if (keyCode == KeyCode.UpArrow)
					{
						suggestionI--;
						AdjustForSuggestion();

						Event.current.Use();
					}
				}

				/*
				// Dropdown all options
				if (keyCode == KeyCode.Tab)
				{
					DoFloatOptions(nextOptions, v => v.DisplayName(type), SetMember, d => d.ShouldShow(filters));
				}
				*/
			}


			// final member:
			if (member != null)
			{
				// member as string
				row.Gap(-2);//account for label gap
				Rect memberRect = row.LabelWithTags(member.TextName, tooltip: member.TooltipDetails);
				if (Widgets.ButtonInvisible(memberRect))
				{
					member = null;
					Focus();
				}
			}
			else
			{
				// unfinalized memberStr
				if (locked)
				{
					// as string
					row.Gap(-2);//account for label gap
					row.LabelWithTags(memberStr);
				}
				else
				{
					// as Text fiiiiield!

					row.Label(".");

					Rect inputRect = rect;
					inputRect.xMin = row.FinalX;

					GUI.SetNextControlName(controlName);
					if (TDWidgets.TextField(inputRect, ref memberStr))
					{
						ParseTextField();
					}
				}
			}


			// Draw field suggestions

			if (focused || keepAliveSuggestions)
			{
				if (Event.current.type == EventType.Layout)
				{
					if (needMoveLineEnd)
					{
						TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
						needMoveLineEnd = false;

						editor?.MoveLineEnd();
					}
				}

				int showCount = Math.Min(suggestions.Count, 10);
				Rect suggestionRect = new(fullRect.x, fullRect.yMax, queryRect.width, (1 + showCount) * suggestionRowHeight);

				// Focused opens the window, mouseover sustains the window so button works.
				if (Event.current.type == EventType.Layout)
				{
					keepAliveSuggestions = suggestionRect.ExpandedBy(16).Contains(Event.current.mousePosition);
				}

				suggestionRect.position += UI.GUIToScreenPoint(new(0, 0));

				Find.WindowStack.ImmediateWindow(id, suggestionRect, WindowLayer.Super, delegate
				{
					Rect rect = suggestionRect.AtZero();
					rect.xMax -= 20;

					// draw Field count
					Text.Anchor = TextAnchor.LowerRight;
					if (suggestions.Count > 1)
						Widgets.Label(rect, $"{suggestions.Count} options");
					else if (suggestions.Count == 0)
						Widgets.Label(rect, $"No options!");
					Text.Anchor = default;

					// Begin Scrolling
					bool needsScroll = suggestions.Count > 10;
					if (needsScroll)
					{
						rect.height = (1 + suggestions.Count) * suggestionRowHeight;
						Widgets.BeginScrollView(suggestionRect.AtZero(), ref suggestionScroll, rect);
					}


					// Set rect for row
					rect.width = 9999;//go ahead and spill over out of bounds instead of wrapping next line
					rect.xMin += 2;
					rect.height = suggestionRowHeight;

					// Highligh selected option
					// TODO: QueryCustom field to use it elsewhere, probably by index.
					var selectedOption = suggestions.ElementAtOrDefault(suggestionI);

					foreach (var d in suggestions)
					{
						// Suggestion row: click to use
						Widgets.Label(rect, d.SuggestionName);
						Widgets.DrawHighlightIfMouseover(rect);

						bool clicked = Widgets.ButtonInvisible(rect);

						// Details on selected highlighted option (key down to browse)
						if (selectedOption == d)
						{
							Widgets.DrawHighlight(rect);  // field row
							rect.y += suggestionRowHeight;
							Widgets.Label(rect, $": <color=grey>{selectedOption.TooltipDetails}</color>");
							Widgets.DrawHighlight(rect);  // details row
							clicked |= Widgets.ButtonInvisible(rect);
						}
						else
						{
							if(Mouse.IsOver(rect))
							{
								// Eh, <> not handled well 
								TooltipHandler.TipRegion(rect, d.TooltipDetails);
							}
						}

						if (clicked)
						{
							SetMember(d);
							Focus();

							//Need to do this manual since this is not a FloatMenuOptionsAndRefresh
							RootHolder.NotifyUpdated();

							break;
						}
						rect.y += suggestionRowHeight;
					}

					if (needsScroll)
						Widgets.EndScrollView();
				},
				doBackground: true);
			}

			return changed;
		}
		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			return member?.Draw(listing, locked) ?? false;
		}

		protected override void DoFocus()
		{
			if(member == null)
				GUI.FocusControl(controlName);
			else
				member.DoFocus();
		}

		public override bool Unfocus()
		{
			if (GUI.GetNameOfFocusedControl() == controlName)
			{
				UI.UnfocusCurrentControl();
				return true;
			}

			return member?.Unfocus() ?? false;
		}

		public override bool AppliesDirectlyTo(Thing thing)
		{
			if (!matchType.IsAssignableFrom(thing.GetType()))
				return false;

			return MemberAppliesTo(thing);
		}

		private bool MemberAppliesTo(object obj, int startI = 0)
		{
			for (int i = startI; i < memberChain.Count; i++)
			{
				var memberData = memberChain[i];

				// return false if any object in chain is null
				// Redundant on first call for Thing thing
				// Ought to be after GetMember for most cases
				// But if the final check is "is null", that needs to check the resulting obj AFTER this loop
				if (obj == null)
					return false;

				// Redundant for first member call, oh well
				// direct matchType check is still needed above if you're checking matchType == Pawn with Pawn.def.x
				// Because the memberData.type is not checking for Pawn, as it's actually Thing.def
				if (!memberData.type.IsAssignableFrom(obj.GetType()))
					return false;


				if (memberData is FieldDataEnumerableMember enumerableData)
				{
					i++;

					IEnumerable<object> items = enumerableData.GetMembers(obj);
					// Todo? something other than Any?
					return items != null && items.Any(o => MemberAppliesTo(o, i));
				}

				// else it'd better be this:
				FieldDataClassMember classData = (FieldDataClassMember)memberData;
				obj = classData.GetMember(obj);
			}

			if (obj == null)
				return member is ObjIsNull; // This or implement a bool property that's overriden true for this subclass only

			if (member != null)
				return member.AppliesTo(obj);

			return false;
		}
	}

	public static class TypeEx
	{
		public static string ToStringSimple(this Type type)
		{
			if (type == typeof(float)) return "float";
			if (type == typeof(bool)) return "bool";
			if (type == typeof(int)) return "int";
			return type.Name;
		}
	}
}
