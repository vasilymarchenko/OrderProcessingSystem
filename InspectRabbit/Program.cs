using System.Reflection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

Console.WriteLine("Inspecting BasicAckEventArgs...");
var type = typeof(BasicAckEventArgs);
foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
{
    Console.WriteLine($"Field: {field.Name}");
}
foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
{
    Console.WriteLine($"Property: {prop.Name}");
}



