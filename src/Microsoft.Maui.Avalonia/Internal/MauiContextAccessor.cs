using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;

namespace Microsoft.Maui.Avalonia.Internal;

internal static class MauiContextAccessor
{
	static readonly MethodInfo? AddSpecificMethod = typeof(MauiContext)
		.GetMethod("AddSpecific", BindingFlags.Instance | BindingFlags.NonPublic);

	static readonly MethodInfo? AddWeakSpecificMethod = typeof(MauiContext)
		.GetMethod("AddWeakSpecific", BindingFlags.Instance | BindingFlags.NonPublic);

	static readonly MethodInfo? SetWindowScopeMethod = typeof(MauiContext)
		.GetMethod("SetWindowScope", BindingFlags.Instance | BindingFlags.NonPublic);

	public static void TryAddSpecific<T>(IMauiContext? context, T instance)
		where T : class
	{
		if (context is not MauiContext mauiContext || instance is null)
			return;

		InvokeGeneric(AddSpecificMethod, mauiContext, typeof(T), instance);
	}

	public static void TryAddWeakSpecific<T>(IMauiContext? context, T instance)
		where T : class
	{
		if (context is not MauiContext mauiContext || instance is null)
			return;

		InvokeGeneric(AddWeakSpecificMethod, mauiContext, typeof(T), instance);
	}

	public static void TrySetWindowScope(IMauiContext? context, IServiceScope? scope)
	{
		if (context is not MauiContext mauiContext || scope is null || SetWindowScopeMethod is null)
			return;

		try
		{
			SetWindowScopeMethod.Invoke(mauiContext, new object[] { scope });
		}
		catch (TargetInvocationException tie)
		{
			throw tie.InnerException ?? tie;
		}
	}

	static void InvokeGeneric(MethodInfo? method, MauiContext target, Type serviceType, object argument)
	{
		if (method is null)
			return;

		try
		{
			method.MakeGenericMethod(serviceType).Invoke(target, new[] { argument });
		}
		catch (TargetInvocationException tie)
		{
			throw tie.InnerException ?? tie;
		}
	}
}
