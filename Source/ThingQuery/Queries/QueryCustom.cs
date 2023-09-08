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

		// For filters
		private string fieldLower;
		private string nameLower;

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

			MakeLower();
		}

		private void MakeLower()
		{
			fieldLower = fieldType.Name.ToLower();
			nameLower = name.ToLower();
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

				MakeLower();
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
			clone.MakeLower();

			return clone;
		}

		public virtual bool Draw(Listing_StandardIndent listing) => false;
		public virtual void DoFocus() { }
		public virtual bool Unfocus() => false;



		// full readable name, e.g. GetComp<CompPower>
		// This string will be passed back into ParseTextField
		// and then passed to ShouldShow, which should return true of course
		public virtual string TextName => name;
		public virtual string Details => $"({fieldType.ToStringSimple()}) {type.ToStringSimple()}.{TextName}";
		public override string ToString() => Details;

		public virtual bool ShouldShow(List<string> filters)
		{
			foreach(string filter in filters)
			{
				if (fieldLower.Contains(filter) || nameLower.Contains(filter))
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

		
		public override string TextName =>
			$"GetComp<{fieldType.Name}>()";
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
		public Type matchType = typeof(Thing);
		public List<FieldDataClassMember> memberChain = new();
		private FieldDataComparer _member;
		public string memberStr = "";

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
			Scribe_Deep.Look(ref _member, "member");
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

				member?.Make();
				loadError = member?.DisableReason;

				foreach (var memberData in memberChain)
				{
					memberData.Make();
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

			clone._member = (FieldDataComparer)member?.Clone();
			clone.member?.Make();
			clone.memberStr = memberStr;

			clone.memberChain = new(memberChain.Select(d => d.Clone() as FieldDataClassMember));
			foreach (var memberData in clone.memberChain)
				memberData.Make();

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
			FieldData addData = newData.Clone();

			if(addData is FieldDataComparer addDataCompare)
			{
				member = addDataCompare;
				memberStr = member.TextName;

				UI.UnfocusCurrentControl();
				// Keep nextType in case you want to change the compared field

				ParseTextField();
			}
			else if(addData is FieldDataClassMember addDataMember)
			{
				memberChain.Add(addDataMember);
				addDataMember.Make();

				member = null; //to be selected
				memberStr = "";


				nextType = addDataMember.fieldType;
			}
		}


		// After text field edit, parse string for new filtered field suggestions
		private List<string> filters = new ();
		private List<FieldData> suggestions = new();
		private Vector2 suggestionScroll;
		private void ParseTextField()
		{
			filters.Clear();
			filters.AddRange(memberStr.ToLower().Split(' ', '<', '>', '(', ')'));

			suggestions.Clear();
			suggestions.AddRange(nextOptions.Where(d => d.ShouldShow(filters)));
		}

		// After tab/./enter : select first suggested field and add to memberChain / create comparable member
		// This might remove comparision settings... Could check if it already exists
		// but why would you set the name if you already had the comparison set up?
		public bool AutoComplete()
		{
			FieldData newData = suggestions.FirstOrDefault();
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
		private bool mouseOverSuggestions;
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



			// Cast the thing (so often useful it's always gonna be here)
			row.Label("thing as ");
			RowButtonFloatMenu(matchType, FieldData.thingSubclasses, t => t.Name, SetMatchType, tooltip: matchType.ToString());


			// Draw (append) The memberchain
			foreach (var memberLink in memberChain)
			{
				row.Label(".");
				row.Gap(-2);
				row.LabelWithTags(memberLink.TextName, tooltip: memberLink.Details);
				row.Gap(-2);
			}
			row.Label(".");


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

			if (keyDown && focused)
			{
				// suppress the second key events that come after pressing tab/return/period
				if ((ch == '	' || ch == '\n' || ch == '.'))
				{
					Log.Message($"Using event ch:{ch}");
					Event.current.Use();
				}
				// Autocomplete after tab/./enter
				else if (keyCode == KeyCode.Tab
					|| keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter
					|| keyCode == KeyCode.Period || keyCode == KeyCode.KeypadPeriod)
				{
					Log.Message($"Using event key:{keyCode} ({memberStr})");
					changed = AutoComplete();

					Event.current.Use();
				}
				// Backup to last member if deleting empty string after "member."
				else if (keyCode == KeyCode.Backspace)
				{
					if(memberStr == "" && memberChain.Count > 0)
					{
						Log.Message($"Using event key:{keyCode}");
						member = null;
						var lastMember = memberChain.Pop();

						needMoveLineEnd = true;
						memberStr = lastMember.TextName;
						nextType = memberChain.LastOrDefault()?.fieldType ?? matchType;

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
				// as string
				row.Gap(-2);//account for label gap
				Rect memberRect = row.LabelWithTags(member.TextName, tooltip: member.Details);
				if (Widgets.ButtonInvisible(memberRect))
				{
					member = null;
					Focus();
				}
			}
			else
			{
				//Text fiiiiield!
				Rect inputRect = rect;
				inputRect.xMin = row.FinalX;

				GUI.SetNextControlName(controlName);
				if (TDWidgets.TextField(inputRect, ref memberStr))
				{
					ParseTextField();
				}
			}


			// Draw field suggestions

			if (focused || mouseOverSuggestions)
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
				Rect suggestionRect = new(fullRect.x, fullRect.yMax, queryRect.width, (1 + showCount) * Text.LineHeight);

				// Focused opens the window, mouseover sustains the window so button works.
				if (Event.current.type == EventType.Layout)
				{
					mouseOverSuggestions = suggestionRect.ExpandedBy(16).Contains(Event.current.mousePosition);
				}

				suggestionRect.position += UI.GUIToScreenPoint(new(0, 0));

				Find.WindowStack.ImmediateWindow(id, suggestionRect, WindowLayer.Super, delegate
				{
					Rect rect = suggestionRect.AtZero();
					rect.xMax -= 20;

					// draw Field count
					Text.Anchor = TextAnchor.LowerRight;
					if (suggestions.Count > 1)
						Widgets.Label(rect, $"{suggestions.Count} fields");
					else if (suggestions.Count == 0)
						Widgets.Label(rect, $"No fields!");
					Text.Anchor = default;

					// Begin Scrolling
					bool needsScroll = suggestions.Count > 10;
					if (needsScroll)
					{
						rect.height = (1 + suggestions.Count) * Text.LineHeight;
						Widgets.BeginScrollView(suggestionRect.AtZero(), ref suggestionScroll, rect);
					}


					// Set height for row
					rect.xMin += 2;
					rect.height = Text.LineHeight;

					// Highligh selected option
					// TODO: QueryCustom field to use it elsewhere, probably by index.
					var selectedOption = suggestions.FirstOrDefault();

					foreach (var d in suggestions)
					{
						// Suggestion row: click to use
						Widgets.Label(rect, d.TextName);
						Widgets.DrawHighlightIfMouseover(rect);

						if(Widgets.ButtonInvisible(rect))
						{
							SetMember(d);
							mouseOverSuggestions = false;
							break;
						}

						// Details on selected highlighted option (key down to browse)
						if (selectedOption == d)
						{
							Widgets.DrawHighlight(rect);	// field row
							rect.y += Text.LineHeight;
							Widgets.Label(rect, $": <color=grey>{selectedOption.Details}</color>");
							Widgets.DrawHighlight(rect);	// details row
						}
						rect.y += Text.LineHeight;
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
			return member?.Draw(listing) ?? false;
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

			object obj = thing;
			foreach (var memberData in memberChain)
			{
				if (!memberData.type.IsAssignableFrom(obj.GetType()))
					// Redundant for first member call, oh well
					// direct matchType check is still needed above if you're checking matchType == Pawn with Pawn.def.x
					// Because the memberData.type is not checking for Pawn, as it's actually Thing.def
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
