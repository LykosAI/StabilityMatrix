using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace StabilityMatrix.Core.Attributes;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
[MeansImplicitUse(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.Itself)]
[AttributeUsage(AttributeTargets.Class)]
public class ManagedServiceAttribute : Attribute;
