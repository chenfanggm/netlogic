// FILE: netlogic.core/Sim/Systems/EngineCommandHandlerAttribute.cs
// Marks a handler as belonging to a specific system type for auto-discovery.
namespace com.aqua.netlogic.command.handler
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class EngineCommandHandlerAttribute(Type systemType) : Attribute
    {
        public Type SystemType { get; } = systemType ?? throw new ArgumentNullException(nameof(systemType));
    }
}
