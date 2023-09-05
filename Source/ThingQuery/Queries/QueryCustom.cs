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
		public virtual string DisplayName(Type selectedType)
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


		// full readable name, e.g. GetComp<CompPower>
		// This string will be passed back into ParseTextField
		// and then passed to ShouldShow, which should return true of course
		public virtual string TextName => name;

		public virtual bool ShouldShow(List<string> filters)
		{
			foreach(string filter in filters)
			{
				if (fieldType.Name.ToLower().Contains(filter)
					|| name.ToLower().Contains(filter))
					continue;

				return false;
			}
			return true;
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


		// All FieldData options for a subclass and its parents
		private static readonly Dictionary<Type, List<FieldData>> typeFields = new();
		private static readonly Type[] baseTypes = new[] { typeof(Thing), typeof(Def), typeof(object), null};
		public static List<FieldData> GetOptions(Type type)
		{
			if (!typeFields.TryGetValue(type, out var list))
			{
				list = FieldsFor(type);
				typeFields[type] = list;

				while(!baseTypes.Contains(type))
				{
					type = type.BaseType;
					list.AddRange(FieldsFor(type));
				}
			}
			return list;
		}




		// Here we find all fields
		// TODO: more types, meaning FieldData subclasses for each type
		private static FieldData NewData(Type fieldDataType, Type inputType, params object[] args) =>
			(FieldData)Activator.CreateInstance(inputType == null ? fieldDataType : fieldDataType.MakeGenericType(inputType), args);


		private static bool ValidProp(PropertyInfo p) =>
			p.CanRead && p.GetMethod.GetParameters().Length == 0;

		private static bool ValidClassType(Type type) =>
			type.IsClass && !type.GetInterfaces().Contains(typeof(System.Collections.IEnumerable));

		const BindingFlags bFlags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;
		private static IEnumerable<FieldData> FindFields(Type type)
		{
			// class members
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (ValidClassType(field.FieldType))
					yield return new ClassFieldData(type, field.FieldType, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (ValidClassType(prop.PropertyType))
					yield return NewData(typeof(ClassPropData<>), type, prop.PropertyType, prop.Name);

			//Hard coded 
			if(type == typeof(ThingWithComps))
			{
				foreach(Type compType in GenTypes.AllSubclasses(typeof(ThingComp)))
					yield return NewData(typeof(ThingCompData<>), compType);
			}

			// valuetypes
			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (field.FieldType == typeof(bool))
					yield return new BoolFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (prop.PropertyType == typeof(bool))
					yield return NewData(typeof(BoolPropData<>), type, prop.Name);


			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (field.FieldType == typeof(int))
					yield return new IntFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (prop.PropertyType == typeof(int))
					yield return NewData(typeof(IntPropData<>), type, prop.Name);


			foreach (FieldInfo field in type.GetFields(bFlags | BindingFlags.GetField))
				if (field.FieldType == typeof(float))
					yield return new FloatFieldData(type, field.Name);

			foreach (PropertyInfo prop in type.GetProperties(bFlags | BindingFlags.GetProperty).Where(ValidProp))
				if (prop.PropertyType == typeof(float))
					yield return NewData(typeof(FloatPropData<>), type, prop.Name);
		}
	}



	// FieldData subclasses of FieldDataClassMember handle gettings members that hold/return a Class
	// FieldDataClassMember can be chained e.g. thing.def.building
	// The final FieldData after the chain must be a comparer (below) 
	// That one actually does the filter on the value that it gets
	// e.g. thing.def.building.uninstallWork > 300
	public abstract class FieldDataClassMember : FieldData
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
		public override void Make()
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
		public override void Make()
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
		public override void Make()
		{
			getter ??= AccessTools.MethodDelegate<ThingCompGetter>(AccessTools.DeclaredMethod(type, name).MakeGenericMethod(fieldType));
		}
		public override object GetMember(object obj) => getter(obj as ThingWithComps);

		
		public override string DisplayName(Type selectedType) =>
			$"{base.DisplayName(selectedType)}〈{fieldType.Name}〉"; // not < > because those tags are stripped for Text.CalcSize

		public override string TextName =>
			$"GetComp<{fieldType.Name}>";
	}


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
		public override void Make()
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
		public override void Make()
		{
			getter ??= AccessTools.MethodDelegate<BoolGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
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
		public override void Make()
		{
			getter ??= AccessTools.MethodDelegate<IntGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
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
		public override void Make()
		{
			getter ??= AccessTools.MethodDelegate<FloatGetter>(AccessTools.DeclaredPropertyGetter(typeof(T), name));
		}

		public override float GetFloatValue(object obj) => getter(obj as T);// please only pass in T
	}




	[StaticConstructorOnStartup]
	public class ThingQueryCustom : ThingQuery
	{
		public Type matchType;
		private string matchTypeName;
		private List<FieldDataClassMember> memberChain;
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
			nextType = matchType;
			nextOptions = FieldData.GetOptions(nextType);
			filteredOptions.AddRange(nextOptions);

			memberChain = new();

			controlName = $"THING_QUERY_CUSTOM_INPUT{id}";
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
			clone.memberChain = new(memberChain.Select(d => d.Clone() as FieldDataClassMember));
			foreach (var memberData in clone.memberChain)
				memberData.Make();
			clone.loadError = loadError;
			return clone;
		}

		private string loadError = null;
		public override string DisableReason => loadError;

		private Type nextType;
		private void SelectMatchType(Type type)
		{
			loadError = null;

			matchType = type;

			//TODO: Don't reset but use ParseTextField to validate
			member = null;
			memberChain.Clear();
			memberChainStr = "";
			nextType = matchType;
			nextOptions = FieldData.GetOptions(nextType);


			ParseTextField();
		}

		private bool needCursorEnd;
		private void SetMember(FieldData newData)
		{
			int cutIndex = memberChainStr.LastIndexOf('.');
			if (cutIndex > 0)
				memberChainStr = memberChainStr.Remove(cutIndex) + '.' + newData.TextName;
			else
				memberChainStr = newData.TextName;

			needCursorEnd = true;


			FieldData addData = newData.Clone();

			if(addData is FieldDataComparer addDataCompare)
			{
				member = addDataCompare;
				// Keep nextType in case you want to change the compared field


				ParseTextField();
				filteredOptions.Clear(); //no suggestions if you've set a final field
			}
			else if(addData is FieldDataClassMember addDataMember)
			{
				memberChainStr += '.';

				memberChain.Add(addDataMember);
				addDataMember.Make();

				member = null; //to be selected


				nextType = addDataMember.fieldType;
				nextOptions = FieldData.GetOptions(nextType);

				ParseTextField();
			}
			//memberChain
		}

		private void SetMemberAt(FieldData newData, int i)
		{
			memberChain.RemoveRange(i, memberChain.Count - i);

			SetMember(newData);
		}

		protected override float RowGap => 0;
		private string memberChainStr = "";
		private List<string> memberNames = new ();
		private string lastMemberStr;
		private List<string> filters = new ();
		private List<FieldData> nextOptions;
		private List<FieldData> filteredOptions = new();
		/*
		private string activeMemberName = "";
		private int activeMemberIndex = 0;
		private Type activeMemberType = 0;
		*/
		private readonly string controlName;

		// After text field edit, parse string for validity + new filtered field suggestions
		private void ParseTextField()
		{
			memberNames.Clear();
			memberNames.AddRange(memberChainStr.ToLower().Split('.'));

			// Validate memberNames to memberChain
			// Remove/create as needed

			filters.Clear();
			lastMemberStr = memberNames.LastOrDefault();
			if (lastMemberStr != null)
				filters.AddRange(lastMemberStr.Split(' ', '<', '>'));

			Log.Message($"ParseTextField ({memberChainStr}) => ({memberNames.ToStringSafeEnumerable()}) => ({filters.ToStringSafeEnumerable()})");

			FilterOptions();
		}
		private void FilterOptions()
		{
			filteredOptions.Clear();
			filteredOptions.AddRange(nextOptions.Where(d => d.ShouldShow(filters)));
		}

		// After tab/./enter : select first suggested field and add to memberChain / create comparable member
		// This might remove comparision settings... Could check if it already exists
		// but why would you have set the name if you had the comparison set up?
		public bool AutoComplete()
		{
			Log.Message($"AutoComplete ({memberNames.ToStringSafeEnumerable()}) from options ({filteredOptions.ToStringSafeEnumerable()})");

			FieldData newData = filteredOptions.FirstOrDefault();
			if (newData != null)
			{
				SetMember(newData);
				return true;
			}
			return false;
		}
		protected override bool DrawMain(Rect rect, bool locked, Rect fullRect)
		{
			/*
			row.Label("Is type");
			RowButtonFloatMenu(matchType, FieldData.thingSubclasses, t => t.Name, SelectMatchType, tooltip: matchType.ToString());

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




			// the text fielllld

			// handle special input before the textfield grabs it
			bool focused = GUI.GetNameOfFocusedControl() == controlName;
			bool keyDown = Event.current.type == EventType.KeyDown;
			KeyCode keyCode = Event.current.keyCode;
			char ch = Event.current.character;


			bool changed = false;
			if (keyDown)
			{
				TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
				Log.Message($"keyDown {keyCode}/{ch} editor.text = {editor.text}");
			}

			//suppress the second key events that come after pressing tab/return/period
			if (keyDown && focused && (ch == '	' || ch == '\n' || ch == '.'))
			{
				Event.current.Use();
			}
			else if (keyDown && focused &&
				(keyCode == KeyCode.Tab || keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter
				|| keyCode == KeyCode.Period || keyCode == KeyCode.KeypadPeriod))
			{
				changed = AutoComplete();

				Event.current.Use();

				/*
				// Dropdown all options
				if (keyCode == KeyCode.Tab)
				{
					DoFloatOptions(nextOptions, v => v.DisplayName(type), SetMember, d => d.ShouldShow(filters));
				}
				*/
			}
			


			row.Label("thing as ");
			RowButtonFloatMenu(matchType, FieldData.thingSubclasses, t => t.Name, SelectMatchType, tooltip: matchType.ToString());
			row.Label(".");
			GUI.SetNextControlName(controlName);
			Rect inputRect = rect;
			inputRect.xMin = row.FinalX;
			if (TDWidgets.TextField(inputRect, ref memberChainStr))
			{
				TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
				Log.Message($"TextField editor.text = {editor.text}");
				ParseTextField();
			}
			if (focused && needCursorEnd && Event.current.type == EventType.Layout)
			{
				needCursorEnd = false;
				if (focused)
				{
					needCursorEnd = false;
					TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
					Log.Message($@"{memberChainStr} WAS editor.cursorIndex = {editor.cursorIndex}
	editor.text = {editor.text}
	editor.SelectedText = {editor.SelectedText}
	editor.hasSelection = {editor.hasSelection}
	editor.selectIndex = {editor.selectIndex}
	editor.cursorIndex = {editor.cursorIndex}
	editor.position = {editor.position}
	editor.altCursorPosition = {editor.altCursorPosition}
	editor.graphicalCursorPos = {editor.graphicalCursorPos}
	editor.scrollOffset = {editor.scrollOffset}
	editor.isPasswordField = {editor.isPasswordField}
	editor.graphicalSelectCursorPos = {editor.graphicalSelectCursorPos}
	editor.multiline = {editor.multiline}
	editor.controlID = {editor.controlID}
	editor.hasHorizontalCursorPos = {editor.hasHorizontalCursorPos}");


					editor?.MoveLineEnd();

					Log.Message($@"{memberChainStr} NOW editor.cursorIndex = {editor.cursorIndex}
	editor.text = {editor.text}
	editor.SelectedText = {editor.SelectedText}
	editor.hasSelection = {editor.hasSelection}
	editor.selectIndex = {editor.selectIndex}
	editor.cursorIndex = {editor.cursorIndex}
	editor.position = {editor.position}
	editor.altCursorPosition = {editor.altCursorPosition}
	editor.graphicalCursorPos = {editor.graphicalCursorPos}
	editor.scrollOffset = {editor.scrollOffset}
	editor.isPasswordField = {editor.isPasswordField}
	editor.graphicalSelectCursorPos = {editor.graphicalSelectCursorPos}
	editor.multiline = {editor.multiline}
	editor.controlID = {editor.controlID}
	editor.hasHorizontalCursorPos = {editor.hasHorizontalCursorPos}");
				}
			}
			/*
			 * TODO: draw as text when finalized
			if (member != null)
			{
				Rect memberRect = row.Label(memberChainStr);
				if(Widgets.ButtonInvisible(memberRect))
				{
					member = null;
					Focus();
				}
			}
			else
			{
			}
			*/



			// Draw field suggestions
			if (focused)
			{
				int showCount = Math.Min(filteredOptions.Count, 10);


				Vector2 pos = UI.GUIToScreenPoint(new(0, 0));
				Rect suggestionRect = new(pos.x + fullRect.x, pos.y + fullRect.yMax, fullRect.width, showCount * Text.LineHeight);
				Find.WindowStack.ImmediateWindow(id, suggestionRect, WindowLayer.Super, delegate
				{
					Rect rect = suggestionRect.AtZero();
					//Widgets.BeginGroup(rect);
					//Widgets.DrawWindowBackground(rect);
					rect.height = Text.LineHeight;

					if (filteredOptions.Count > 1)
					{
						Text.Anchor = TextAnchor.UpperRight;
						Widgets.Label(rect, $"{filteredOptions.Count} fields");
						Text.Anchor = default;
					}
					foreach (var d in filteredOptions)
					{
						if (--showCount < 0) break;

						//TODO SuggestionName aka visual studio "(field) ThingDef Thing.def)
						Widgets.Label(rect, d.DisplayName(nextType));

						rect.y += Text.LineHeight;
					}
					//Widgets.EndGroup();
				}, doBackground: true);
			}

			return changed;
		}
		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			return member?.Draw(listing) ?? false;
		}

		protected override void DoFocus()
		{
			if (GUI.GetNameOfFocusedControl() != controlName)
				GUI.FocusControl(controlName);
			member?.DoFocus();
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
