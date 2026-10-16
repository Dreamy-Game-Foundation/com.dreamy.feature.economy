using System;
using System.Reflection;
using NUnit.Framework;

internal static class RecoveryHarness
{
    public static void Main()
    {
        ShopPresenterHarness.Main();
        int passed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        foreach (var method in type.GetMethods())
        {
            var cases = method.GetCustomAttributes<TestCaseAttribute>();
            var inputs = new System.Collections.Generic.List<object[]>();
            foreach (var item in cases) inputs.Add(item.Arguments);
            if (method.GetCustomAttribute<TestAttribute>() != null) inputs.Add(null);
            foreach (var arguments in inputs)
            {
                var instance = Activator.CreateInstance(type);
                try
                {
                    foreach (var setup in type.GetMethods())
                        if (setup.GetCustomAttribute<SetUpAttribute>() != null) setup.Invoke(instance, null);
                    var result = method.Invoke(instance, arguments);
                    if (result is System.Threading.Tasks.Task task) task.GetAwaiter().GetResult();
                    passed++;
                }
                catch (TargetInvocationException exception) { throw new Exception(type.Name + "." + method.Name, exception.InnerException); }
                finally
                {
                    foreach (var teardown in type.GetMethods())
                        if (teardown.GetCustomAttribute<TearDownAttribute>() != null) teardown.Invoke(instance, null);
                }
            }
        }
        Console.WriteLine($"PASS {passed} managed NUnit tests");
    }
}
