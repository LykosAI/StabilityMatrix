using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace StabilityMatrix.Core.Attributes;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors), MeansImplicitUse]
[AttributeUsage(AttributeTargets.Class)]
public class ManagedServiceAttribute : Attribute;
