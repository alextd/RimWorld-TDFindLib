using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TD_Find_Lib
{
	public static class LinqEx
	{
		public static IEnumerable<TSource> MaybeWhere<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) =>
			predicate == null ? source : source.Where(predicate);

		//public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
		public static bool AnyX<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, int countMin)
		{
			// 0 means default means check 1.
			if (countMin == 0) return source.Any(predicate);

			// yes countMin could be 1 and we'd do this redundantly but why would you do that.
			int count = 0;
			foreach (TSource element in source)
				if (predicate(element))
					if(++count == countMin)
						return true;

			return false;
		}
	}
}
