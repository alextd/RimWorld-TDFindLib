using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TD_Find_Lib
{
	public static class EnumEx2<T>
	{
		public static readonly T[] Arr = (T[])Enum.GetValues(typeof(T));
	}
	public static class EnumEx
	{ 
		//Extension method must be defined in non-generic class :(
		public static T Next<T>(this T src) where T: Enum
		{
			if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));

			int i = (Array.IndexOf(EnumEx2<T>.Arr, src) + 1) % EnumEx2<T>.Arr.Length;
			return EnumEx2<T>.Arr[i];
		}
	}
}
