using System.Reflection;
using HomeSdk;

try
{
    Home.Receive();
    Home.RedirectConsole();
    var student = Assembly.LoadFrom(args[0]);
    var entry = student.EntryPoint ?? throw new InvalidOperationException("Programmet har intet startpunkt.");
    var result = entry.Invoke(null, entry.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
    if (result is Task task)
        task.GetAwaiter().GetResult();
    Home.Yield(0, true);
}
catch (Exception error)
{
    Home.Report(error is TargetInvocationException { InnerException: { } cause } ? cause : error);
}
