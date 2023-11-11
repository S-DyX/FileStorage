using System;
using System.Collections.Generic;
using System.Linq;

namespace FileStorage.Core.Helpers
{
	public static class NullHelper
	{
		public static bool IsNullOrEmpty<T>(this IEnumerable<T> list)
		{
			return list == null || !list.Any();
		}

		public static TResult With<TInput, TResult>(this TInput o, Func<TInput, TResult> evaluator)
			where TResult : class
			where TInput : class
		{
			if (o == null) return null;
			return evaluator(o);
		}

		public static TResult Return<TInput, TResult>(this TInput o, Func<TInput, TResult> evaluator, TResult defaultValue)
			where TInput : class
		{
			if (o == null) return defaultValue;
			return evaluator(o);
		}

		public static TInput If<TInput>(this TInput o, Func<TInput, bool> evaluator)
			where TInput : class
		{
			if (o == null) return null;
			return evaluator(o) ? o : null;
		}

		public static TInput Unless<TInput>(this TInput o, Func<TInput, bool> evaluator)
			where TInput : class
		{
			if (o == null) return null;
			return evaluator(o) ? null : o;
		}

		public static TInput Do<TInput>(this TInput o, Action<TInput> action)
			where TInput : class
		{
			if (o == null) return null;
			action(o);
			return o;
		}

		public static TInput GetValueOrDefault<TInput>(this TInput o, Predicate<TInput> predicate, TInput defaultValue)
		{
			return predicate(o) ? defaultValue : o;
		}

		public static TInput GetValueOrDefault<TInput>(this TInput o, TInput defaultValue) where TInput : class
		{
			return o ?? defaultValue;
		}

		public static bool IsNull<TInput>(this TInput o) where TInput : class
		{
			return o == null;
		}
	}
}